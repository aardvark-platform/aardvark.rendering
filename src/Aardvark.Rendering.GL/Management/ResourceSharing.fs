namespace Aardvark.Rendering.GL

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Collections.Concurrent
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.GL

module Sharing =
    
    type RefCountedBuffer(ctx, create : unit -> Buffer, destroy : unit -> unit) =
        inherit Buffer(ctx, 0n, 0)

        let mutable refCount = 0



        member x.Acquire() =
            if Interlocked.Increment &refCount = 1 then
                let b = Operators.using ctx.ResourceLock (fun _ -> create())
                x.Handle <- b.Handle
                x.SizeInBytes <- b.SizeInBytes

        member x.Release() =
            if Interlocked.Decrement &refCount = 0 then
                destroy()
                Operators.using ctx.ResourceLock (fun _ -> ctx.Delete x)
                x.Handle <- 0
                x.SizeInBytes <- 0n

    type RefCountedTexture(ctx, create : unit -> Texture, destroy : unit -> unit) =
        inherit Texture(ctx, 0, TextureDimension.Texture2D, 0, 0, V3i.Zero, None, TextureFormat.Rgba, 0L, true)

        let mutable refCount = 0

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
                x.ImmutableFormat <- b.ImmutableFormat

        member x.Release() =
            if Interlocked.Decrement &refCount = 0 then
                destroy()
                Operators.using ctx.ResourceLock (fun _ -> ctx.Delete x)
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
            match data with
                | _ ->
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

        let nullTex = Texture(ctx, 0, TextureDimension.Texture2D, 1, 1, V3i.Zero, None, TextureFormat.Rgba, 0L, true)

        let get (b : ITexture) =
            cache.GetOrAdd(b, fun v -> 
                RefCountedTexture(
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
                    if b.Handle = 0 then
                        x.Create(data)
                    else
                        ctx.Upload(b, data)
                        b

        member x.Delete(b : Texture) =
            if b.Handle <> 0 then
                if active then
                    match b with
                        | :? RefCountedTexture as b -> b.Release()
                        | _ -> ctx.Delete b
                else
                    ctx.Delete b

