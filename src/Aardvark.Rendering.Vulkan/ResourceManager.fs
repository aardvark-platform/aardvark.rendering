namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Collections.Concurrent
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.Vulkan

module ResourceSketch =
    
    

    [<AbstractClass>]
    type Resource<'h>() =
        inherit AdaptiveObject()

        let mutable live = 0
        let mutable refCount = 0
        let mutable handle = Unchecked.defaultof<_>

        abstract member CreateResource  : unit -> IMod<'h>
        abstract member UpdateResource  : unit -> Command<unit>
        abstract member DestroyResource : 'h -> unit

        member x.Update(caller : IAdaptiveObject) =
            x.EvaluateIfNeeded caller Command.nop (fun () ->
                if live = 0 then
                    failf "cannot update disposed resource"

                x.UpdateResource()
            )

        member x.Handle =
            if live <> 1 then failf "cannot get handle of disposed resource"
            handle

        member x.AddRef() =
            if Interlocked.Increment(&refCount) = 1 then
                if Interlocked.Exchange(&live, 1) = 0 then
                    let h = x.CreateResource()
                    handle <- h

        member x.RemoveRef() =
            if Interlocked.Decrement(&refCount) = 0 then
                if Interlocked.Exchange(&live, 0) = 1 then
                    x.DestroyResource (handle.GetValue())

        member x.Dispose() =
            x.RemoveRef()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    [<AbstractClass>]
    type RecreateResource<'h>() =
        inherit Resource<'h>()

        let mutable handle = Unchecked.defaultof<ModRef<_>>

        abstract member Recreate : Option<'h> -> 'h
        abstract member Release : 'h -> unit

        override x.DestroyResource h =
            x.Release h
            handle <- Unchecked.defaultof<_>

        override x.CreateResource() =
            let h = x.Recreate None
            handle <- Mod.init h
            handle :> IMod<_>

        override x.UpdateResource() =
            let o = handle.Value
            let n = x.Recreate (Some o)
            transact (fun () -> handle.Value <- n)

            Command.nop



    type BufferResource(ctx : Context, input : IMod<IBuffer>, usage : VkBufferUsageFlags) =
        inherit Resource<Buffer>()

        let mutable created = false
        let mutable handle : ModRef<Buffer> = Unchecked.defaultof<_>

        let bufferSize (data : IBuffer) =
            match data with
                | :? ArrayBuffer as ab ->
                    let et = ab.GetType().GetElementType()
                    let es = Marshal.SizeOf et
                    int64 (es * ab.Data.Length)

                | :? INativeBuffer as nb ->
                    int64 nb.SizeInBytes

                | :? Buffer as b ->
                    b.Size

                | _ -> 0L

        let createOrUpload(input : 'a) (upload : Buffer -> 'a -> Command<unit>) =
            let h = handle.Value
            let size = bufferSize (input :> IBuffer)
            if h.Size <> size || not created then
                command {
                    let b = ctx.CreateBuffer(size, usage)
                    try 
                        do! upload b input
                    finally 
                        transact (fun () -> handle.Value <- b)
                        if created then ctx.Delete h
                }
            else
                upload h input
        
        override x.CreateResource() =
            match input.GetValue(x) with
                | :? Buffer as b ->
                    created <- false
                    handle <- Mod.init b
                | b ->
                    created <- true
                    let s = bufferSize b
                    let b = ctx.CreateBuffer(s, usage)
                    handle <- Mod.init b

            handle :> IMod<_>

        override x.UpdateResource() =
            let h = handle.Value
            match input.GetValue(x) with
                | :? Buffer as b ->
                    if created then ctx.Delete handle.Value
                    transact (fun () -> handle.Value <- b)

                    Command.nop

                | :? ArrayBuffer as ab ->
                    createOrUpload ab (fun b ab ->
                        b.Upload(ab.Data, 0, ab.Data.Length)
                    ) 

                | :? INativeBuffer as nb ->
                    createOrUpload nb (fun b nb ->
                        command {
                            let ptr = nb.Pin()
                            try do! b.Upload(ptr, int64 nb.SizeInBytes)
                            finally nb.Unpin()
                        }
                    )

                | b -> failf "unknown buffer type: %A" b

        override x.DestroyResource(b) =
            ctx.Delete(b)
            created <- false
            handle <- Unchecked.defaultof<_>

    type BufferViewResource(ctx : Context, input : Aardvark.Base.BufferView, buffer : Resource<Buffer>) =
        inherit RecreateResource<BufferView>()

        override x.Recreate(old) =
            match old with
                | Some o -> ctx.Delete o
                | None -> buffer.AddRef()

            let buffer = buffer.Handle.GetValue(x)
            ctx.CreateBufferView(
                buffer, 
                input.ElementType.GetVertexInputFormat(),
                int64 input.Offset,
                buffer.Size - int64 input.Offset 
            )

        override x.Release h =
            buffer.RemoveRef()
            ctx.Delete h

module ResourceSketch2 =

    [<AbstractClass>]
    type Resource<'h when 'h : equality>(cache : ResourceCache<'h>) =
        inherit AdaptiveObject()

        let handle = Mod.init None
        let h = handle |> Mod.map Option.get
        let mutable refCount = 0
        let mutable key = []
        
        abstract member Create  : Option<'h> -> 'h * Command<unit>
        abstract member Destroy : 'h -> unit
   
        member x.Handle = h

        member x.Key
            with get() = key
            and set k = key <- k

        member x.AddRef() =
            Interlocked.Increment(&refCount) |> ignore

        member x.RemoveRef() =
            if Interlocked.Decrement(&refCount) = 0 then
                cache.Remove x.Key
                x.Kill()

        member x.Update(caller : IAdaptiveObject) =
            x.EvaluateIfNeeded caller Command.nop (fun () ->
                if refCount <= 0 then
                    failf "cannot update disposed resource"

                let old = handle.Value
                let h, update = x.Create old

                match old with
                    | Some o when o = h -> 
                        update

                    | Some o ->
                        command {
                            try do! update
                            finally
                                transact (fun () -> handle.Value <- Some h)
                                x.Destroy o
                        }
                    
                    | None ->
                        transact (fun () -> handle.Value <- Some h)
                        update
            )
            
        member x.Dispose() = x.RemoveRef()

        member internal x.Kill() =
            refCount <- 0
            key <- []
            match handle.Value with
                | Some h -> 
                    transact (fun () -> handle.Value <- None)
                    x.Destroy h

                | None -> 
                    ()

        interface IDisposable with 
            member x.Dispose() = x.Dispose()
                
    and ResourceCache<'h when 'h : equality>() =
        let cache = ConcurrentDictionary<list<obj>, Resource<'h>>()

        member x.Remove (key : list<obj>) : unit =
            match cache.TryRemove key with
                | (true, res) -> res.Key <- []
                | _ -> ()

        member x.GetOrCreate(key : list<obj>, f : unit -> Resource<'h>) =
            cache.GetOrAdd(key, fun _ -> 
                let res = f()
                res.Key <- key
                res
            ) 

        member x.Clear() =
            cache.Values |> Seq.iter (fun r -> r.Kill())
            cache.Clear()
    
    type ResourceManager(ctx : Context) =
        let bufferCache = ResourceCache<Buffer>()
        let bufferViewCache = ResourceCache<BufferView>()
        let imageCache = ResourceCache<Image>()
        let imageViewCache = ResourceCache<ImageView>()

        // buffer functions

        member x.CreateBuffer(data : IMod<IBuffer>, usage : VkBufferUsageFlags) =
            bufferCache.GetOrCreate(
                [data; usage],
                fun () ->
                    let mutable created = false
                    { new Resource<Buffer>(bufferCache) with
                        member x.Create(old) =
                            let input = data.GetValue(x)

                            match input with
                                | :? Buffer as b ->
                                    if created then 
                                        old |> Option.iter ctx.Delete
                                        created <- false

                                    b, Command.nop

                                | :? ArrayBuffer as ab ->
                                    let data = ab.Data
                                    let es = Marshal.SizeOf (data.GetType().GetElementType())
                                    let size = data.Length * es |> int64
                                            
                                    match old with
                                        | Some old when created && old.Size = size ->
                                            old, old.Upload(data, 0, data.Length)

                                        | _ ->
                                            if created then old |> Option.iter ctx.Delete
                                            else created <- true

                                            let b = ctx.CreateBuffer(size, usage)
                                            b, b.Upload(data, 0, data.Length)
                                
                                | :? INativeBuffer as nb ->
                                    let size = nb.SizeInBytes |> int64

                                    let upload (b : Buffer) =
                                        command {
                                            let ptr = nb.Pin()
                                            try do! b.Upload(ptr, size)
                                            finally nb.Unpin()
                                        }

                                    match old with
                                        | Some old when created && old.Size = size ->   
                                            old, upload old

                                        | _ ->
                                            if created then old |> Option.iter ctx.Delete
                                            else created <- true

                                            let b = ctx.CreateBuffer(size, usage)
                                            b, upload b
                                                     
                                | b ->
                                    failf "unsupported buffer type: %A" b

                        member x.Destroy(h) =
                            ctx.Delete h
                            created <- false
                    }

            )

        member x.CreateBufferView(elementType : Type, offset : int64, size : int64, buffer : Resource<Buffer>) =
            let format = elementType.GetVertexInputFormat()
            bufferViewCache.GetOrCreate(
                [elementType; offset; size; buffer],
                fun () ->
                    { new Resource<BufferView>(bufferViewCache) with
                        member x.Create(old) =
                            old |> Option.iter ctx.Delete

                            let buffer = buffer.Handle.GetValue(x)
                            let size =
                                if size < 0L then buffer.Size - offset
                                else size

                            let view =
                                ctx.CreateBufferView(
                                    buffer, 
                                    format,
                                    offset,
                                    size
                                )

                            view, Command.nop

                        member x.Destroy(h) =
                            ctx.Delete(h)
                    }
            )

        member x.CreateBufferView(view : Aardvark.Base.BufferView, buffer : Resource<Buffer>) =
            x.CreateBufferView(view.ElementType, int64 view.Offset, -1L, buffer)


        // image functions

        member x.CreateImage(data : IMod<ITexture>) =
            imageCache.GetOrCreate(
                [data],
                fun () ->
                    let mutable created = false
                    { new Resource<Image>(imageCache) with
                        member x.Create old =
                            let tex = data.GetValue(x)

                            match tex with
                                | :? Image as t ->
                                    if created then 
                                        old |> Option.iter ctx.Delete
                                        created <- false
                                    t, Command.nop

                                | :? FileTexture as ft ->
                                    if created then old |> Option.iter ctx.Delete
                                    else created <- true

                                    let vol, fmt, release = ImageSubResource.loadFile ft.FileName
                                    
                                    let size = V2i(int vol.Info.Size.X, int vol.Info.Size.Y)
                                    let levels =
                                        if ft.TextureParams.wantMipMaps then 
                                            Fun.Log2(max size.X size.Y |> float) |> ceil |> int
                                        else 
                                            1

                                    if ft.TextureParams.wantSrgb || ft.TextureParams.wantCompressed then
                                        warnf "compressed / srgb textures not implemented atm."

                                    let texFormat = VkFormat.toTextureFormat fmt

                                    let img =
                                        ctx.CreateImage2D(
                                            texFormat,
                                            size,
                                            levels,
                                            VkImageUsageFlags.SampledBit
                                        )

                                    let update =
                                        command {
                                            do! img.UploadLevel(0, vol, fmt)
                                            do! Command.barrier MemoryTransfer

                                            if ft.TextureParams.wantMipMaps then
                                                do! img.GenerateMipMaps()

                                            do! img.ToLayout(VkImageLayout.ShaderReadOnlyOptimal)
                                        }

                                    img, update

                                | :? PixTexture2d as pt ->
                                    if created then old |> Option.iter ctx.Delete
                                    else created <- true

                                    let data = pt.PixImageMipMap
                                    let l0 = data.[0]

                                    let generate, levels =
                                        if pt.TextureParams.wantMipMaps then
                                            if data.LevelCount > 1 then 
                                                false, data.LevelCount
                                            else 
                                                true, Fun.Log2(max l0.Size.X l0.Size.Y |> float) |> ceil |> int
                                        else 
                                            false, 1

                                    let texFormat =
                                        TextureFormat.ofPixFormat l0.PixFormat pt.TextureParams

                                    let img =
                                        ctx.CreateImage2D(
                                            texFormat,
                                            l0.Size,
                                            levels,
                                            VkImageUsageFlags.SampledBit
                                        )

                                    let update =
                                        command {
                                            do! img.UploadLevel(0, l0)

                                            if generate then
                                                do! img.GenerateMipMaps()
                                            else
                                                for l in 1 .. data.LevelCount-1 do
                                                    do! img.UploadLevel(l, data.[l])

                                            do! img.ToLayout(VkImageLayout.ShaderReadOnlyOptimal)
                                        }

                                    img, update

                                | t ->
                                    failf "unknown texture type: %A" t

                        member x.Destroy h =
                            ctx.Delete h
                            created <- false
                    }
            )

        member x.CreateImageView(image : Resource<Image>) =
            imageViewCache.GetOrCreate(
                [image],
                fun () ->
                    { new Resource<ImageView>(imageViewCache) with
                        member x.Create old =
                            let img = image.Handle.GetValue(x)
                            let view = ctx.CreateImageView(img)
                            view, Command.nop

                        member x.Destroy h = 
                            ctx.Delete h
                    }
            )




type ResourceDescription<'data, 'res> =
    {
        dynamicHandle : bool
        create  : 'data -> 'res
        update  : 'res -> 'data -> 'res * Command<unit>
        destroy : 'res -> unit
    }

type IResource<'res> =
    inherit IAdaptiveObject
    abstract member AddRef    : unit -> unit
    abstract member Resource  : IMod<'res>
    abstract member Update    : IAdaptiveObject -> Command<unit>
    abstract member Destroy   : unit -> unit

type Resource<'data, 'res>(input : IMod<'data>, remove : unit -> unit, createDesc : IResource<'res> -> ResourceDescription<'data, 'res>) =
    inherit AdaptiveObject()
    static let noCommand = Command.ofValue()

    let mutable dynamicHandle = None
    let mutable handle = None
    let mutable refCount = 0

    let mutable desc = None

    let getDesc(self : Resource<_,_>) =
        match desc with
            | Some d -> d
            | None ->
                let d = createDesc self
                desc <- Some d
                d

    let getHandle(self : Resource<_,_>) =
        match handle with
            | None -> 
                let desc = getDesc self 
                let h = input.GetValue(self) |> desc.create
                if desc.dynamicHandle then
                    let r = Mod.init h
                    dynamicHandle <- Some r
                    handle <- Some (r :> IMod<_>)
                    r :> IMod<_>
                else
                    let c = Mod.constant h
                    handle <- Some c
                    c
                
            | Some h ->
                h

    
    member x.Resource = getHandle x

    member x.Update(caller : IAdaptiveObject) =
        x.EvaluateIfNeeded caller noCommand (fun () ->
            let desc = getDesc x
            let i = input.GetValue x
            let h = getHandle(x).GetValue()
            let nh, cmd = desc.update h i

            if not (Unchecked.equals h nh) then
                transact (fun () ->
                    Mod.change (dynamicHandle.Value) nh
                )

            cmd
        )

    member x.Destroy() =
        if Interlocked.Decrement(&refCount) = 0 then
            remove()
            input.Outputs.Remove x |> ignore

            match handle with
                | Some h -> 
                    match desc with
                        | Some d -> d.destroy (Mod.force h)
                        | _ -> ()

                    desc <- None

                | _ -> ()

            dynamicHandle <- None
            handle <- None

    member x.AddRef() =
        Interlocked.Increment(&refCount) |> ignore

    interface IResource<'res> with
        member x.Resource = x.Resource
        member x.Update c = x.Update c
        member x.Destroy() = x.Destroy()
        member x.AddRef() = x.AddRef()

[<AutoOpen>]
module private ResourceCaching = 

    type ResourceCache<'data, 'res>() =
        let cache = ConcurrentDictionary<list<obj> * IMod<'data>, IResource<'res>>()

        member x.GetOrCreate(key : list<obj>, data : IMod<'data>, f : IResource<'res> -> ResourceDescription<'data, 'res>) =
            let key = key, data
            let creator (k : list<obj>, data : IMod<'data>) =
                let remove() = cache.TryRemove key |> ignore
                Resource<_,_>(data, remove, f) :> IResource<_>
            
            cache.GetOrAdd(key, creator)





type ResourceManager(ctx : Context) =
    let buffers = ResourceCache<IBuffer, Buffer>()
    let views = ResourceCache<Buffer, BufferView>()

    member x.CreateBuffer (b : IMod<IBuffer>) =
        let mutable created = false

        let bufferSize (data : IBuffer) =
            match data with
                | :? ArrayBuffer as ab ->
                    let et = ab.GetType().GetElementType()
                    let es = Marshal.SizeOf et
                    int64 (es * ab.Data.Length)

                | :? INativeBuffer as nb ->
                    int64 nb.SizeInBytes

                | :? Buffer as b ->
                    b.Size

                | _ -> 0L
        
        buffers.GetOrCreate([], b, fun self ->
            {
                dynamicHandle = true
                create = fun data ->
                    match data with
                        | :? Buffer as b -> 
                            created <- false
                            b
                        | _ ->
                            let size = bufferSize data
                            created <- true
                            ctx.CreateBuffer(size, VkBufferUsageFlags.VertexBufferBit)

                update = fun res data ->
                    match data with
                        | :? Buffer as b ->
                            if created && b <> res then
                                ctx.Delete res
                                created <- false

                            b, command { () }

                        | _ ->
                            let mutable res = res
                            let size = bufferSize data
                            if size <> res.Size then
                                if created then
                                    ctx.Delete res
                                res <- ctx.CreateBuffer(size, VkBufferUsageFlags.VertexBufferBit)
                                created <- false

                            match data with
                                | :? ArrayBuffer as ab ->
                                    res, res.Memory.Upload(ab.Data, 0, ab.Data.Length)

                                | :? INativeBuffer as nb ->
                                    res, 
                                    command {
                                        let ptr = nb.Pin()
                                        try do! res.Memory.Upload(ptr, size)
                                        finally nb.Unpin()
                                    }

                                | b ->
                                    failf "unknown buffer type: %A" b
  

                destroy = fun res -> ctx.Delete res
            }
        )

    member x.CreateBufferView (t : Type, off : int64, size : int64, b : IResource<Buffer>) =
        views.GetOrCreate([t; off; size], b.Resource, fun self ->
            {
                dynamicHandle = true

                create = fun data ->
                    let fmt = t.GetVertexInputFormat()
                    ctx.CreateBufferView(data, fmt, off, size)

                update = fun res data -> 
                    let fmt = t.GetVertexInputFormat()
                    ctx.Delete res
                    ctx.CreateBufferView(data, fmt, off, size), command { () }

                destroy = fun res -> 
                    ctx.Delete res
            }
        )

        