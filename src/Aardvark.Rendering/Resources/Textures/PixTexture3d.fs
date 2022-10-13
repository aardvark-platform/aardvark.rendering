namespace Aardvark.Rendering

open Aardvark.Base
open System.Runtime.InteropServices

type PixTexture3d(data : PixVolume, textureParams : TextureParams) =

    member x.PixVolume = data
    member x.TextureParams = textureParams

    new(data : PixVolume, [<Optional; DefaultParameterValue(true)>] wantMipMaps : bool) =
        PixTexture3d(data, { TextureParams.empty with wantMipMaps = wantMipMaps })

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
        | :? PixTexture3d as o ->
            data = o.PixVolume && textureParams = o.TextureParams
        | _ ->
            false

    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps