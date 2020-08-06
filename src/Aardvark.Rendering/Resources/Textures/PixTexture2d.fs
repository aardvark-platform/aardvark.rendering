namespace Aardvark.Rendering

open Aardvark.Base

type PixTexture2d(data : PixImageMipMap, textureParams : TextureParams) =

    member x.PixImageMipMap = data
    member x.TextureParams = textureParams

    new(data : PixImageMipMap, wantMipMaps : bool) =
        PixTexture2d(data, { TextureParams.empty with wantMipMaps = wantMipMaps })

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
        | :? PixTexture2d as o ->
            data = o.PixImageMipMap && textureParams = o.TextureParams
        | _ ->
            false

    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps