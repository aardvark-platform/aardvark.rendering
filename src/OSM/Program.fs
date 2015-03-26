// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open System.IO
open System.Net
open BruTile
open BruTile.Cache
open BruTile.Predefined
open Aardvark.Base
open DevILSharp

let e = 8.1819190842622E-02
let a = 6378137.0

type ITileSchema with
    member x.GetBounds(level : string) =
        let minx = x.GetMatrixFirstCol level
        let miny = x.GetMatrixFirstRow level
        let sizeX = x.GetMatrixWidth level
        let sizeY = x.GetMatrixHeight level
        Box2i.FromMinAndSize(V2i(minx, miny), V2i(sizeX-1, sizeY-1))

    member x.GetExtent (region : Box2d) =
        let world = Box2d(x.Extent.MinX, x.Extent.MinY, x.Extent.MaxX, x.Extent.MaxY)
        //http://earth-info.nga.mil/GandG/wgs84/web_mercator/%28U%29%20NGA_SIG_0011_1.0.0_WEBMERC.pdf
        let thetaMin = region.Min.Y * Constant.RadiansPerDegree
        let thetaMax = region.Max.Y * Constant.RadiansPerDegree
        let minY = a * (Fun.Atanh (sin thetaMin) (*- e *  Fun.Atanh (e * sin thetaMin)*))
        let maxY = a * (Fun.Atanh (sin thetaMax) (*- e *  Fun.Atanh (e * sin thetaMax)*))

        Extent(
            x.Extent.Width  * (region.Min.X / 360.0),
            minY,
            x.Extent.Width  * (region.Max.X / 360.0),
            maxY
        )



type TileInfo with
    member x.Bounds =
        Box2d(Constant.DegreesPerRadian * x.Extent.MinX / a, Constant.DegreesPerRadian * Fun.Asin(Fun.Tanh(x.Extent.MinY / a)), Constant.DegreesPerRadian * x.Extent.MaxX / a, Constant.DegreesPerRadian * Fun.Asin(Fun.Tanh(x.Extent.MaxY / a)))


type Extent with
    member x.RelativeTo(other : Extent) =
        let rMinX = (x.MinX - other.MinX) / other.Width
        let rMinY = (x.MinY - other.MinY) / other.Height
        let rMaxX = (x.MaxX - other.MinX) / other.Width
        let rMaxY = (x.MaxY - other.MinY) / other.Height
        Extent(rMinX, rMinY, rMaxX, rMaxY)

    member x.Box =
        Box2d(x.MinX, x.MinY, x.MaxX, x.MaxY)

[<EntryPoint>]
let main argv = 
    let source = KnownTileSources.Create(KnownTileSource.OpenStreetMap)

    let imageSize = V2i(1024, 768)
    let region = Box2d(16.3, 48.15, 16.4, 48.25)

    
    let extent = source.Schema.GetExtent(region)

    let sizeX = extent.Height * (float imageSize.X / float imageSize.Y)
    let delta = sizeX - extent.Width
    let extent = Extent(extent.MinX - delta / 2.0, extent.MinY, extent.MaxX + delta / 2.0, extent.MaxY)

    let resolution = V2d((extent.Width / float imageSize.X),(extent.Height / float imageSize.Y))
    let tileInfos = source.Schema.GetTileInfos(extent, max resolution.X resolution.Y)

    let pi = PixImage<byte>(Col.Format.RGBA, imageSize.X, imageSize.Y, 4)
    DevILSharp.IL.Enable(DevILSharp.EnableCap.AbsoluteOrigin) |> ignore
    DevILSharp.IL.OriginFunc(DevILSharp.OriginMode.LowerLeft) |> ignore

    for t in tileInfos do

        let tileData = source.GetTile t
        let path = sprintf @"C:\Users\schorsch\Desktop\geo\tile_%d_%d_%s.png" t.Index.Row t.Index.Col t.Index.Level
        File.WriteAllBytes(path, tileData)
        let tileSize = V2i(256, 256)
        let bounds = t.Extent.Intersect(extent)

        let rTile = bounds.RelativeTo(t.Extent)
        let rTarget = bounds.RelativeTo(extent)

        let imageRegion = Box2i(V2i(V2d tileSize * V2d(rTile.MinX, rTile.MinY)), V2i(V2d tileSize * V2d(rTile.MaxX, rTile.MaxY)))
        let targetRegion = Box2i(V2i(V2d imageSize * V2d(rTarget.MinX, rTarget.MinY)), V2i(V2d imageSize * V2d(rTarget.MaxX, rTarget.MaxY)))
        
//        let imageRegion = Box2i(imageRegion.Min.X, tileSize.Y - imageRegion.Max.Y, imageRegion.Max.X, tileSize.Y - imageRegion.Min.Y)
//        let targetRegion = Box2i(targetRegion.Min.X, imageSize - targetRegion.Max.Y, targetRegion.Max.X, imageSize - targetRegion.Min.Y)

        let cropped = Path.ChangeExtension(path, ".jpg")

        let i = IL.GenImage()
        IL.BindImage(i)
        IL.LoadImage path |> ignore
        ILU.Crop(imageRegion.Min.X, imageRegion.Min.Y, 0, imageRegion.SizeX, imageRegion.SizeY, 0) |> ignore
        ILU.SetFilter(Filter.Lanczos3)
        ILU.Scale(targetRegion.SizeX, targetRegion.SizeY, 0) |> ignore
        IL.SaveImage cropped |> ignore
        IL.BindImage(0)
        IL.DeleteImage(i)

        let tile = PixImage.Create(cropped).ToPixImage<byte>(Col.Format.RGBA)

        pi.SubImage(targetRegion).Set(tile)
        //pi.SaveAsImage(@"C:\Users\schorsch\Desktop\geo\res.png")

        //let tileIndex = V2i (ceil ((t.Bounds.Min.X - region.Min.X) / t.Bounds.Size.X), ceil ((t.Bounds.Min.Y - region.Min.Y) / t.Bounds.Size.Y))
        printfn "%A" imageRegion


        ()
    pi.SaveAsImage(@"C:\Users\schorsch\Desktop\geo\res.png")

    0 // return an integer exit code
