namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices

[<AllowNullLiteral>]
type ITexture = 
    abstract member WantMipMaps : bool

type BitmapTexture(bmp : System.Drawing.Bitmap, wantMipMaps : bool) =
    member x.WantMipMaps = wantMipMaps
    member x.Bitmap = bmp
    interface ITexture with
        member x.WantMipMaps = x.WantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(bmp.GetHashCode(), wantMipMaps.GetHashCode())

    override x.Equals o =
        match o with
            | :? BitmapTexture as o ->
                bmp = o.Bitmap && wantMipMaps = o.WantMipMaps
            | _ ->
                false

type FileTexture(fileName : string, wantMipMaps : bool) =
    do if System.IO.File.Exists fileName |> not then failwithf "File does not exist: %s" fileName

    member x.FileName = fileName
    member x.WantMipMaps = wantMipMaps
    interface ITexture with
        member x.WantMipMaps = x.WantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(fileName.GetHashCode(), wantMipMaps.GetHashCode())

    override x.Equals o =
        match o with
            | :? FileTexture as o ->
                fileName = o.FileName && wantMipMaps = o.WantMipMaps
            | _ ->
                false

type PixTexture2d(data : PixImageMipMap, wantMipMaps : bool) =
    member x.PixImageMipMap = data
    member x.WantMipMaps = wantMipMaps
    interface ITexture with
        member x.WantMipMaps = x.WantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), wantMipMaps.GetHashCode())

    override x.Equals o =
        match o with
            | :? PixTexture2d as o ->
                data = o.PixImageMipMap && wantMipMaps = o.WantMipMaps
            | _ ->
                false

type PixTextureCube(data : PixImageCube, wantMipMaps : bool) =
    member x.PixImageCube = data
    member x.WantMipMaps = wantMipMaps
    interface ITexture with
        member x.WantMipMaps = x.WantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), wantMipMaps.GetHashCode())

    override x.Equals o =
        match o with
            | :? PixTextureCube as o ->
                data = o.PixImageCube && wantMipMaps = o.WantMipMaps
            | _ ->
                false

type PixTexture3d(data : PixVolume, wantMipMaps : bool) =
    member x.PixVolume = data
    member x.WantMipMaps = wantMipMaps
    interface ITexture with
        member x.WantMipMaps = x.WantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), wantMipMaps.GetHashCode())

    override x.Equals o =
        match o with
            | :? PixTexture3d as o ->
                data = o.PixVolume && wantMipMaps = o.WantMipMaps
            | _ ->
                false


module DefaultTextures =

    let checkerboardPix = 
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 16L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                C4b.Gray
        ) |> ignore
        pi

    let checkerboard = 
        PixTexture2d(PixImageMipMap [| checkerboardPix :> PixImage |], true) :> ITexture |> Mod.constant