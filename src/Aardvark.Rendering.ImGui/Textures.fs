namespace Aardvark.Rendering.ImGui

open Aardvark.Base
open Aardvark.Rendering
open System
open Hexa.NET.ImGui

type internal Textures(runtime: IRuntime) =
    let mutable store = Dict<uint64, IBackendTexture>()

    let getNextId =
        let mutable current = 0UL
        fun () ->
            &current += 1UL
            ImTextureID current

    member _.Item (id: ImTextureID) = store.[id.Handle]

    member _.Update(data: ImVector<ImTextureDataPtr> inref) =
        for i = 0 to data.Size - 1 do
            let data = data.[i]

            match data.Status with
            | ImTextureStatus.WantCreate ->
                let format =
                    match data.Format with
                    | ImTextureFormat.Rgba32 -> TextureFormat.Rgba8
                    | fmt -> raise <| NotSupportedException($"Unsupported texture format: {fmt}")

                let id = getNextId()
                let texture = runtime.CreateTexture2D(data.Size, format)
                texture.Name <- $"ImGui Texture #{id.Handle}"
                store.[id.Handle] <- texture

                let src = data.GetNativeTensor()
                texture.Runtime.Upload(texture.[TextureAspect.Color, 0, 0], src, Col.Format.RGBA)

                data.SetTexID id
                data.SetStatus ImTextureStatus.Ok

            | ImTextureStatus.WantUpdates ->
                let texture = store.[data.TexID.Handle]

                for i = 0 to data.Updates.Size - 1 do
                    let rect = data.Updates.[i]
                    let src = data.GetNativeTensor rect
                    texture.Runtime.Upload(texture.[TextureAspect.Color, 0, 0], src, Col.Format.RGBA, rect.Offset.XYO, rect.Size.XYI)

                data.SetStatus ImTextureStatus.Ok

            | ImTextureStatus.WantDestroy when data.UnusedFrames > 0 ->
                let mutable texture = Unchecked.defaultof<_>
                if store.TryRemove(data.TexID.Handle, &texture) then texture.Dispose()
                data.SetStatus ImTextureStatus.Destroyed
                data.SetTexID ImTextureID.Null

            | _ -> ()

    member this.Dispose() =
        for KeyValue(_, t) in store do t.Dispose()
        store.Clear()

    interface IDisposable with
        member this.Dispose() = this.Dispose()