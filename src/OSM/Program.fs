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
open Aardvark.Base.Incremental
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open FShade
open Aardvark.Rendering.GL
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

[<AutoOpen>]
module Shader = 
    type Vertex =
        {
            [<Position>] pos : V4d
            [<TexCoord>] tc : V2d
        }

    // define a very simple shader for rendering the tiles and apply it to the scenegraph
    // furthermore normalize the view-space to [0.0, 1.0] x [0.0, 1.0] (starting top-left)
    let vertex (v : Vertex) =
        vertex {
            return { v with pos = uniform.ModelTrafo * v.pos }
        }

    let diffuseTex = 
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }

    let fragment (v : Vertex) =
        fragment {
            let color = diffuseTex.Sample(v.tc)

            return V4d(1.0 * color.XYZ, color.W)
        }


[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.ThrowException)
    
    let source = KnownTileSources.Create(KnownTileSource.BingAerial)



//    let imageSize = V2i(1024, 768)
//    let region = Box2d(16.3, 48.15, 16.4, 48.25)
//
//    
//    let extent = source.Schema.GetExtent(region)
//
//    let sizeX = extent.Height * (float imageSize.X / float imageSize.Y)
//    let delta = sizeX - extent.Width
//    let extent = Extent(extent.MinX - delta / 2.0, extent.MinY, extent.MaxX + delta / 2.0, extent.MaxY)
//
//    let resolution = V2d((extent.Width / float imageSize.X),(extent.Height / float imageSize.Y))
//    let tileInfos = source.Schema.GetTileInfos(extent, max resolution.X resolution.Y)
//
//    let pi = PixImage<byte>(Col.Format.RGBA, imageSize.X, imageSize.Y, 4)
//    DevILSharp.IL.Enable(DevILSharp.EnableCap.AbsoluteOrigin) |> ignore
//    DevILSharp.IL.OriginFunc(DevILSharp.OriginMode.LowerLeft) |> ignore


    let app = new OpenGlApplication()
    let w = app.CreateSimpleRenderWindow()


    // let's assume the tile resolution is constant
    let realTileResolution = V2i(256, 256)
    let worldBounds = source.Schema.Extent.Box

    // initialize a viewport (small part of the world currently)
    // NOTE that the viewport matches the aspect-ratio of the initial
    //      window-size. therefore tiles will appear quadradic.
    let viewport = 
        let horizontal = worldBounds.Size.X * 0.1
        let vertical = (3.0 / 4.0) * horizontal
        Mod.initMod (Box2d.FromMinAndSize(worldBounds.Center, V2d(horizontal, vertical)))

    // in order to determine the appropriate grid-size we will need the view's size
    let viewResolution = w.Sizes.Mod




    // determine the zoom-level using the current viewport and view-size
    let zoomLevel = 
        adaptive {
            let! vp = viewport
            let! s = viewResolution
            let res = vp.Size / V2d s
            return Utilities.GetNearestLevel(source.Schema.Resolutions, max res.X res.Y)
        }

    let tileCounts =
        zoomLevel |> Mod.map (fun l -> V2i(source.Schema.GetMatrixWidth(l), source.Schema.GetMatrixHeight(l)))


    // determine the tile-size in world-space
    let tileSize = 
        zoomLevel |> Mod.map (fun l -> let gridSize = V2d(source.Schema.GetMatrixWidth(l), source.Schema.GetMatrixHeight(l)) in worldBounds.Size / gridSize)

    // calculate the real resolution of tiles for the current view
    let tileViewResolution =
        adaptive {
            let! tileSize = tileSize
            let! viewport = viewport
            let r = tileSize / viewport.Size

            let! viewResolution = viewResolution

            return V2i (V2d viewResolution * r)

        }

    // calculate the total grid size (todo: +2 may be to conservative)
    let gridSize = 
        adaptive {
            let! size = viewResolution
            let! tileResolution = tileViewResolution
            let res =  (size / tileResolution) + 2*V2i.II
            return res
        }


    // calculate the integer index for the first (upper-left) tile 
    // and a fractional offset for that tile
    let firstTileAndOffset =
        adaptive {
            let! tileSize = tileSize
            let! vp = viewport

            let vpOffset = vp.Min - worldBounds.Min
            let floatTile = vpOffset / tileSize

            let offset = V2d(vpOffset.X % tileSize.X, vpOffset.Y % tileSize.Y) / tileSize
            let tile = V2i(floor floatTile.X, floor floatTile.Y)

            return tile, offset
        }

    // create the desired tile-indices ([0..gridSize.X-1] x [0..gridSize.Y-1])
    let tileIndices =
        aset {
            let! gridSize = gridSize
            
            for x in 0..gridSize.X-1 do
                for y in 0..gridSize.Y-1 do
                    yield V2i(x,y)
        }


    // since tiles are rectangles we may need to define a rectangle-geometry
    let fsq = 
        let q = IndexedGeometry()

        q.Mode <- IndexedGeometryMode.TriangleList
        q.IndexArray <- [|0;1;2; 0;2;3|]
        q.IndexedAttributes <- SymDict.ofList [
            DefaultSemantic.Positions, [|V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO|] :> Array
            DefaultSemantic.DiffuseColorCoordinates, [|V2f.OO; V2f.IO; V2f.II; V2f.OI|] :> Array
        ]

        Sg.ofIndexedGeometry q


    // function for calculating the appropriate transformations for 
    // a specific tile-coord using firstTileAndOffset and the grid-size
    let calcTileTrafo (coord : V2i) =
        adaptive {
            let! res = viewResolution
            let! tileResolution = tileViewResolution
            let! (f,o) = firstTileAndOffset
            
            let relativeTileSize = V2d tileResolution / V2d res
            let relativePosition = relativeTileSize * (V2d coord - o) 

            return Trafo3d.Scale(V3d(relativeTileSize.X, relativeTileSize.Y, 1.0)) * 
                   Trafo3d.Translation(relativePosition.X,relativePosition.Y, 0.0)
        }

    let ctx = (app.Runtime |> unbox<Aardvark.Rendering.GL.Runtime>).Context

    let cache = System.Collections.Concurrent.ConcurrentDictionary<string * V2i, ITexture>()

    let getTileTexure (coord : V2i) (zoom : string) =
        cache.GetOrAdd((zoom, coord), fun (zoom, coord) ->
            let info = TileInfo()
            info.Index <- TileIndex(coord.X, coord.Y, zoom)

            let data = source.GetTile(info)
            use ms = new System.IO.MemoryStream(data)
            let bmp = System.Drawing.Bitmap.FromStream(ms) |> unbox<System.Drawing.Bitmap>

            //let tex = ctx.CreateTexture <| BitmapTexture(bmp, true)

            BitmapTexture(bmp, true) :> ITexture
        )

    let getTileColor (coord : V2i) =
        adaptive {
            let! counts = tileCounts
            let! (first,o) = firstTileAndOffset
            let coord = first + coord


            if coord.AnySmaller 0 || coord.AnyGreaterOrEqual counts then
                return C4f.Black
            else
                let r = V2d coord / V2d counts
                return C4f(r.X, r.Y, 1.0, 1.0)
        }

    // calculate a set of SceneGraph nodes having the appropriate transformations for 
    // all tiles (using the FullScreenQuad from above)
    let sgs =
        aset {
            for coord in tileIndices do
                let tex = zoomLevel |> Mod.map (getTileTexure coord)
                yield fsq |> Sg.trafo (calcTileTrafo coord)
                          |> Sg.diffuseTexture tex
                          |> Sg.uniform "TileColor" (getTileColor coord)
                
        }



    let sg =
        sgs |> Sg.set
            |> Sg.effect [toEffect vertex; toEffect fragment]
            |> Sg.trafo (Mod.initConstant (Trafo3d.ViewTrafo(V3d(-1.0, 1.0, 0.0), V3d.IOO * 2.0, -V3d.OIO * 2.0, V3d.OOI)).Inverse)


    // compile the rendertask and pass it to the window
    w.RenderTask <- app.Runtime.CompileRender(sg.RenderJobs())

    // a very sketch controller for changing the viewport
    let lastPos = ref V2d.Zero
    let down = ref false
    w.Mouse.Events.Values.Subscribe(fun e ->
        match e with
            | MouseDown p ->
                down := true
                lastPos := p.location.NormalizedPosition
            | MouseUp p ->
                down := false
                lastPos := p.location.NormalizedPosition
            | MouseMove pos ->
                if !down then
                    let pos = pos.NormalizedPosition
                    let vp = viewport.Value

                    transact (fun () ->
                        viewport.Value <- vp.Translated((!lastPos - pos) * vp.Size)
                    )

                    lastPos := pos
            | _ -> ()
    ) |> ignore


    // finally run the application
    System.Windows.Forms.Application.Run w
    //Environment.Exit 0

//    for t in tileInfos do
//
//        let tileData = source.GetTile t
//        let path = sprintf @"C:\Users\schorsch\Desktop\geo\tile_%d_%d_%s.png" t.Index.Row t.Index.Col t.Index.Level
//        File.WriteAllBytes(path, tileData)
//        let tileSize = V2i(256, 256)
//        let bounds = t.Extent.Intersect(extent)
//
//        let rTile = bounds.RelativeTo(t.Extent)
//        let rTarget = bounds.RelativeTo(extent)
//
//        let imageRegion = Box2i(V2i(V2d tileSize * V2d(rTile.MinX, rTile.MinY)), V2i(V2d tileSize * V2d(rTile.MaxX, rTile.MaxY)))
//        let targetRegion = Box2i(V2i(V2d imageSize * V2d(rTarget.MinX, rTarget.MinY)), V2i(V2d imageSize * V2d(rTarget.MaxX, rTarget.MaxY)))
//        
////        let imageRegion = Box2i(imageRegion.Min.X, tileSize.Y - imageRegion.Max.Y, imageRegion.Max.X, tileSize.Y - imageRegion.Min.Y)
////        let targetRegion = Box2i(targetRegion.Min.X, imageSize - targetRegion.Max.Y, targetRegion.Max.X, imageSize - targetRegion.Min.Y)
//
//        let cropped = Path.ChangeExtension(path, ".jpg")
//
//        let i = IL.GenImage()
//        IL.BindImage(i)
//        IL.LoadImage path |> ignore
//        ILU.Crop(imageRegion.Min.X, imageRegion.Min.Y, 0, imageRegion.SizeX, imageRegion.SizeY, 0) |> ignore
//        ILU.SetFilter(Filter.Lanczos3)
//        ILU.Scale(targetRegion.SizeX, targetRegion.SizeY, 0) |> ignore
//        IL.SaveImage cropped |> ignore
//        IL.BindImage(0)
//        IL.DeleteImage(i)
//
//        let tile = PixImage.Create(cropped).ToPixImage<byte>(Col.Format.RGBA)
//
//        pi.SubImage(targetRegion).Set(tile)
//        //pi.SaveAsImage(@"C:\Users\schorsch\Desktop\geo\res.png")
//
//        //let tileIndex = V2i (ceil ((t.Bounds.Min.X - region.Min.X) / t.Bounds.Size.X), ceil ((t.Bounds.Min.Y - region.Min.Y) / t.Bounds.Size.Y))
//        printfn "%A" imageRegion
//
//
//        ()
//    pi.SaveAsImage(@"C:\Users\schorsch\Desktop\geo\res.png")

    0 // return an integer exit code
