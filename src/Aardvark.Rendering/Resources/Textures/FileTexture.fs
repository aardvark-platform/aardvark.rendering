namespace Aardvark.Rendering

open System.IO
open Aardvark.Base

type FileTexture(fileName : string, textureParams : TextureParams) =
    inherit StreamTexture((fun () -> File.OpenRead(fileName) :> Stream), textureParams)

    do if File.Exists fileName |> not then failwithf "File does not exist: %s" fileName

    member x.FileName = fileName
    member x.TextureParams = textureParams

    new(fileName : string, wantMipMaps : bool) =
        FileTexture(fileName, { TextureParams.compressed with wantMipMaps = wantMipMaps })

    override x.GetHashCode() =
        HashCode.Combine(fileName.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
        | :? FileTexture as o ->
            fileName = o.FileName && textureParams = o.TextureParams
        | _ ->
            false