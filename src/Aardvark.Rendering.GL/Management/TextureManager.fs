namespace Aardvark.Rendering.GL

open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL

type internal RefCountedTexture(ctx, create : unit -> Texture, destroy : unit -> unit) =
    inherit Texture(ctx, 0, TextureDimension.Texture2D, 0, 0, V3i.Zero, None, TextureFormat.Rgba8, 0L)

    let mutable refCount = 0
    let mutable handle = Unchecked.defaultof<Texture>

    override x.Name
        with get() = if refCount > 0 then handle.Name else null
        and set name = if refCount > 0 then handle.Name <- name

    member x.Acquire(name: string) =
        if Interlocked.Increment &refCount = 1 then
            let b = Operators.using ctx.ResourceLock (fun _ -> create())
            if name <> null && b.Name = null then b.Name <- name
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
    let cache = ConcurrentDictionary<ITexture * TextureProperties, RefCountedTexture>()

    let get (properties : TextureProperties) (data : ITexture) =
        cache.GetOrAdd((data, properties), fun key ->
            new RefCountedTexture(
                ctx,
                (fun () -> ctx.CreateTexture(data, ValueSome properties)),
                (fun () -> cache.TryRemove key |> ignore)
            )
        )

    member x.Create(name : string, data : ITexture, properties : TextureProperties) =
        let shared = data |> get properties
        shared.Acquire(name)
        shared :> Texture

    member x.Update(name : string, texture : Texture, data : ITexture, properties : TextureProperties) : Texture =
        match texture with
        | :? RefCountedTexture as texture ->
            let newShared = data |> get properties
            if newShared = texture then
                texture :> Texture
            else
                texture.Release()
                newShared.Acquire(name)
                newShared :> Texture
        | _ ->
            if texture.Handle <> 0 then
                ctx.Delete texture

            x.Create(name, data, properties)

    member x.Delete(texture : Texture) =
        if texture.Handle <> 0 then
            match texture with
            | :? RefCountedTexture as b -> b.Release()
            | _ -> ctx.Delete texture