namespace Aardvark.Rendering.GL

open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL
open FSharp.Data.Adaptive

type internal RefCountedTexture(ctx, create : unit -> Texture, destroy : unit -> unit) =
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

type internal TextureManager(ctx : Context) =
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
        | :? NullTexture -> nullTex
        | _ ->
            let shared = get data
            shared.Acquire()
            shared :> Texture

    member x.Update(b : Texture, data : ITexture) : Texture =
        match b with
        | :? RefCountedTexture as b ->
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
            match b with
            | :? RefCountedTexture as b -> b.Release()
            | _ -> ctx.Delete b