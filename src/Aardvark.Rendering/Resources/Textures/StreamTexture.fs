namespace Aardvark.Rendering

open Aardvark.Base

open System.IO
open System.Runtime.InteropServices

type StreamTexture(openStream : unit -> Stream, textureParams : TextureParams, [<Optional; DefaultParameterValue(null : IPixLoader)>] loader : IPixLoader) =
    member x.TextureParams = textureParams
    member x.PreferredLoader = loader

    member x.Open([<Optional; DefaultParameterValue(false)>] seekable : bool) =
        let stream = openStream()
        if stream.CanSeek || not seekable then stream
        else
            try
                let temp = new MemoryStream()
                stream.CopyTo(temp)
                temp :> Stream
            finally
                stream.Dispose()

    new(openStream : unit -> Stream,
        [<Optional; DefaultParameterValue(true)>] wantMipMaps : bool,
        [<Optional; DefaultParameterValue(null : IPixLoader)>] loader : IPixLoader) =
        StreamTexture(openStream, { TextureParams.empty with wantMipMaps = wantMipMaps }, loader)

    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps