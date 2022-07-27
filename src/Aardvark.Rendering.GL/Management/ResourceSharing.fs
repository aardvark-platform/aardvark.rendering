namespace Aardvark.Rendering.GL

#nowarn "9"
#nowarn "51"

open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL
open FSharp.Data.Adaptive

module Sharing =
    
    type RefCountedBuffer(ctx, create : unit -> Buffer, destroy : unit -> unit) =
        inherit Buffer(ctx, 0n, 0)

        let mutable refCount = 0
        let mutable handle = Unchecked.defaultof<_>

        member x.Acquire() =
            if Interlocked.Increment &refCount = 1 then
                let b = Operators.using ctx.ResourceLock (fun _ -> create())
                x.Handle <- b.Handle
                x.SizeInBytes <- b.SizeInBytes
                handle <- b

        member x.Release() =
            if Interlocked.Decrement &refCount = 0 then
                destroy()
                Operators.using ctx.ResourceLock (fun _ -> ctx.Delete handle)
                x.Handle <- 0
                x.SizeInBytes <- 0n

    type RefCountedTexture(ctx, create : unit -> Texture, destroy : unit -> unit) =
        inherit Texture(ctx, 0, TextureDimension.Texture2D, 0, 0, V3i.Zero, None, TextureFormat.Rgba8, 0L)

        let mutable refCount = 0
        let mutable handle = Unchecked.defaultof<_>

        member x.Acquire() =
            if Interlocked.Increment &refCount = 1 then
                let b = Operators.using ctx.ResourceLock (fun _ -> create())
                x.IsArray <- b.IsArray
                x.Handle <- b.Handle
                x.Dimension <- b.Dimension
                x.Multisamples <- b.Multisamples
                x.Size <- b.Size
                x.Count <- b.Count
                x.Format <- b.Format
                x.MipMapLevels <- b.MipMapLevels
                x.SizeInBytes <- b.SizeInBytes
                handle <- b

        member x.Release() =
            if Interlocked.Decrement &refCount = 0 then
                destroy()
                Operators.using ctx.ResourceLock (fun _ -> ctx.Delete handle)
                x.Handle <- 0


    type BufferManager(ctx : Context, active : bool) =
        let cache = ConcurrentDictionary<IBuffer, RefCountedBuffer>()

        let get (b : IBuffer) =
            cache.GetOrAdd(b, fun v -> 
                new RefCountedBuffer(
                    ctx,
                    (fun () -> ctx.CreateBuffer b),
                    (fun () -> cache.TryRemove b |> ignore)
                )
            )

        member x.Create(data : IBuffer) =
            if active then
                let shared = get data
                shared.Acquire()
                shared :> Buffer
            else
                ctx.CreateBuffer data

        member x.Update(b : Buffer, data : IBuffer) : Buffer =
            match b with
                | :? RefCountedBuffer as b when active ->
                    
                    let newShared = get data
                    if newShared = b then
                        b :> Buffer
                    else
                        newShared.Acquire()
                        b.Release()
                        newShared :> Buffer
                | _ ->
                    if b.Handle = 0 then
                        x.Create(data)
                    else
                        ctx.Upload(b, data)
                        b

        member x.Delete(b : Buffer) =
            if b.Handle <> 0 then
                if active then
                    match b with
                        | :? RefCountedBuffer as b -> b.Release()
                        | _ -> ctx.Delete b
                else
                    ctx.Delete b


    type TextureManager(ctx : Context, active : bool) =
        let cache = ConcurrentDictionary<ITexture, RefCountedTexture>()

        let nullTex = new Texture(ctx, 0, TextureDimension.Texture2D, 1, 1, V3i.Zero, None, TextureFormat.Rgba8, 0L)

        let get (b : ITexture) =
            cache.GetOrAdd(b, fun v -> 
                new RefCountedTexture(
                    ctx,
                    (fun () -> ctx.CreateTexture b),
                    (fun () -> cache.TryRemove b |> ignore)
                )
            )

        member x.Create(data : ITexture) =
            match data with
                | :? NullTexture as t -> nullTex
                | _ ->
                    if active then
                        let shared = get data
                        shared.Acquire()
                        shared :> Texture
                    else
                        ctx.CreateTexture data

        member x.Update(b : Texture, data : ITexture) : Texture =
            match b with
                | :? RefCountedTexture as b when active ->
                    
                    let newShared = get data
                    if newShared = b then
                        b :> Texture
                    else
                        newShared.Acquire()
                        b.Release()
                        newShared :> Texture
                | _ ->
                    if b.Handle <> 0 then
                        ctx.Delete b

                    x.Create(data)

        member x.Delete(b : Texture) =
            if b.Handle <> 0 then
                if active then
                    match b with
                        | :? RefCountedTexture as b -> b.Release()
                        | _ -> ctx.Delete b
                else
                    ctx.Delete b

