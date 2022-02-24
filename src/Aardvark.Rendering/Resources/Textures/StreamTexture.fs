namespace Aardvark.Rendering

open System.IO
open System.Runtime.InteropServices

type StreamTexture(openStream : unit -> Stream, textureParams : TextureParams) =
    member x.TextureParams = textureParams

    member x.Open([<Optional; DefaultParameterValue(false)>] seekable : bool) =
        let stream = openStream()
        if stream.CanSeek || not seekable then stream
        else
            let temp = new MemoryStream()
            stream.CopyTo(temp)
            temp :> Stream

    new(openStream : unit -> Stream, wantMipMaps : bool) =
        StreamTexture(openStream, { TextureParams.empty with wantMipMaps = wantMipMaps })

    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps