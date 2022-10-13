namespace Aardvark.Rendering

open System.IO
open System.Runtime.InteropServices
open Aardvark.Base

type FileTexture(fileName : string, textureParams : TextureParams, [<Optional; DefaultParameterValue(null : IPixLoader)>] loader : IPixLoader) =
    inherit StreamTexture((fun () -> File.OpenRead(fileName) :> Stream), textureParams, loader)

    member x.FileName = fileName
    member x.TextureParams = textureParams

    new(fileName : string,
        [<Optional; DefaultParameterValue(true)>] wantMipMaps : bool,
        [<Optional; DefaultParameterValue(null : IPixLoader)>] loader : IPixLoader) =
        FileTexture(fileName, { TextureParams.empty with wantMipMaps = wantMipMaps }, loader)

    override x.GetHashCode() =
        HashCode.Combine(fileName.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
        | :? FileTexture as o ->
            fileName = o.FileName && textureParams = o.TextureParams
        | _ ->
            false