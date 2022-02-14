namespace Aardvark.Rendering

open System.IO

type StreamTexture(openStream : unit -> Stream, textureParams : TextureParams) =
    member x.Open() = openStream()
    member x.TextureParams = textureParams

    new(openStream : unit -> Stream, wantMipMaps : bool) =
        StreamTexture(openStream, { TextureParams.empty with wantMipMaps = wantMipMaps })

    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps