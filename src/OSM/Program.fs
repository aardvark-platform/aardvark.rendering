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
open System.Drawing
open System.Drawing.Imaging
open System.Threading.Tasks
open OSM
open System.Windows.Forms
open BruTile.Web
open System.Net
open System.Web
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Net
open System.Web
open System.Globalization
open Aardvark.SceneGraph.Semantics
open Aardvark.Base.Rendering

let app = new OpenGlApplication()
let source = KnownTileSources.Create(KnownTileSource.BingHybrid)
let worldBounds = source.Schema.Extent.Box
let realTileResolution = V2i(256, 256)


[<AutoOpen>]
module Textures =
    let mutable shouldInit = true

    let init() =
        if shouldInit then
            shouldInit <- false
            IL.Init()
            IL.OriginFunc(OriginMode.LowerLeft) |> ignore
            IL.Enable(EnableCap.AbsoluteOrigin) |> ignore

    // get a chached texture for the given tile and zoom-level
    // TODO: add proper memory management (textures are never deleted)
    let private cache = System.Collections.Concurrent.ConcurrentDictionary<string * V2i, IMod<ITexture>>()
    let private scheduler = MyScheduler(25)
    let private factory = TaskFactory(scheduler)

    // create a checkerboard-texture as placeholder for unloaded texture
    let private noTexture =
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 16L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                C4b.Gray
        ) |> ignore

        app.Runtime.CreateTexture(PixTexture2d(PixImageMipMap [| pi :> PixImage |], true))

    let getTileTexure (coord : V2i) (zoom : string)=
        init()
        cache.GetOrAdd((zoom, coord), fun (zoom, coord) ->
            let info = TileInfo()
            info.Index <- TileIndex(coord.X, coord.Y, zoom)

    //            // Debugging Code (without actual maps)
    //            Task.Factory.StartNew(fun () ->
    //                 use bmp = new Bitmap(256, 256)
    //                 use g = Graphics.FromImage bmp
    //                 
    //                 g.Clear(Color.Black)
    //                 g.DrawRectangle(Pens.White, Rectangle(0,0,256,256))
    //                 
    //                 let str = sprintf "%A\r\n%d\r\n%d" zoom coord.X coord.Y
    //                 use font = new Font("Consolas", 30.0f)
    //                 
    //                 use format = new StringFormat()
    //                 format.LineAlignment <- StringAlignment.Center
    //                 format.Alignment <- StringAlignment.Center
    //
    //                 let pos = PointF(0.5f * float32 bmp.Width, 0.5f * float32 bmp.Height)
    //                 g.DrawString(str, font, Brushes.White, pos, format)
    //            
    //                 app.Runtime.CreateTexture <| BitmapTexture(bmp, false)
    //             )

            let run =
                async {
                    let! img = source.GetTileAsync info
                    return PixTexture2d(PixImageMipMap [|img|], false) |> app.Runtime.CreateTexture
                }

            run |> Async.StartAsTask |> Mod.async noTexture
        )

[<AutoOpen>]
module Geometry =

    // since tiles are rectangles we may need to define a rectangle-geometry
    let zeroOneQuad = 
        let q = IndexedGeometry()

        q.Mode <- IndexedGeometryMode.TriangleList
        q.IndexArray <- [|0;1;2; 0;2;3|]
        q.IndexedAttributes <- SymDict.ofList [
            DefaultSemantic.Positions, [|V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO|] :> Array
            DefaultSemantic.DiffuseColorCoordinates, [|V2f.OO; V2f.IO; V2f.II; V2f.OI|] :> Array
        ]

        Sg.ofIndexedGeometry q

[<AutoOpen>]
module Shader = 
    open FShade

    type Vertex = { [<Position>] pos : V4d
                    [<TexCoord>] tc : V2d }

    let vertex (v : Vertex) =
        vertex {
            return { v with pos = uniform.ModelTrafo * v.pos }
        }

    let diffuseTex = 
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
            filter Filter.MinMagLinear
        }

    let fragment (v : Vertex) =
        fragment {
            let color = diffuseTex.Sample(v.tc)
            return color
        }

[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    

    let w = app.CreateSimpleRenderWindow()
    //w.Size <- V2i(1280, 1024)

    // initialize a viewport (small part of the world currently)
    let viewportOrigin, viewportSize = 
        let horizontal = worldBounds.Size.X * 0.05
        let vertical = (float (w.Sizes.GetValue().Y) / float (w.Sizes.GetValue().X)) * horizontal
        let box = Box2d.FromCenterAndSize(worldBounds.Center, V2d(horizontal, vertical))

        Mod.init box.Min, Mod.init box.Size

    // compose viewportOrigin/Size to a Box2d
    let viewport = Mod.map2 (schönfinkel Box2d.FromMinAndSize) viewportOrigin viewportSize

    // whenever the window size changes we'd like to adjust the viewport's aspect
    // Note that we don't do this in the mod-system since we want to be able to override
    // this behavoiur.
    w.Sizes |> Mod.registerCallback (fun s ->
        let aspect = float s.X / float s.Y
        transact (fun () ->
            let vp = viewport.GetValue()
            let targetAspect = vp.SizeX / vp.SizeY

            let newVp = vp.ScaledFromCenterBy(V2d(aspect / targetAspect, 1.0))

            viewportOrigin.Value <- newVp.Min
            viewportSize.Value <- newVp.Size
        )
    ) |> ignore


    // in order to determine the appropriate grid-size we will need the view's size
    let viewResolution = w.Sizes

    // determine the zoom-level using the current viewport and view-size
    let zoomLevel = 
        adaptive {
            let! viewportSize = viewportSize
            let! viewResolution = viewResolution

            let res = viewportSize / V2d viewResolution

            return Utilities.GetNearestLevel(source.Schema.Resolutions, max res.X res.Y)
        } |> Mod.onPush

    // determine the tile-size in world-space
    let tileSize = 
        adaptive {
            let! level = zoomLevel
            let gridSize = V2d(source.Schema.GetMatrixWidth level, source.Schema.GetMatrixHeight level)
            return worldBounds.Size / gridSize
        }

    // calculate the relative size of tiles for the current view
    let relativeTileResolution =
        adaptive {
            let! tileSize = tileSize
            let! viewportSize = viewportSize

            return tileSize / viewportSize
        }

    // calculate the total grid size (todo: +2 may be to conservative)
    let gridSize = 
        adaptive {
            let! rel = relativeTileResolution

            let res =  V2i(ceil (1.0 / rel.X), ceil (1.0 / rel.Y)) + V2i.II
            return res
        } |> Mod.onPush


    // calculate the integer index for the first (upper-left) tile 
    // and a fractional offset for that tile
    let firstTileAndOffset =
        adaptive {
            let! tileSize = tileSize
            let! vp = viewportOrigin

            let vpOffset = vp - worldBounds.Min
            let floatTile = vpOffset / tileSize

            let offset = (V2d(vpOffset.X % tileSize.X, vpOffset.Y % tileSize.Y) / tileSize)
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


    // function for calculating the appropriate transformations for 
    // a specific tile-coord using firstTileAndOffset and the grid-size
    let getTileTrafo (coord : V2i) =
        adaptive {
            //let! res = viewResolution
            //let! tileResolution = tileViewResolution
            let! (f,o) = firstTileAndOffset
            
            let! relativeTileSize = relativeTileResolution
            let relativePosition = relativeTileSize * (V2d coord - o) 

            return Trafo3d.Scale(V3d(relativeTileSize.X, relativeTileSize.Y, 1.0)) * 
                   Trafo3d.Translation(relativePosition.X,relativePosition.Y, 0.0)
        }

    // function for calculating the appropriate texture for 
    // a specific tile-coord using firstTileAndOffset and the grid-size
    let getTileTexture (coord : V2i) =
        adaptive {
            let! z = zoomLevel
            let! (f,_) = firstTileAndOffset
            let t = getTileTexure (f + coord) z
            return! t
        } 

   


    // calculate a set of SceneGraph nodes having the appropriate transformations for 
    // all tiles (using the FullScreenQuad from above)
    let sgs =
        aset {
            for coord in tileIndices do
                yield zeroOneQuad |> Sg.trafo (getTileTrafo coord)
                                  |> Sg.diffuseTexture (getTileTexture coord)
                
        }

    // apply an effect and a trafo making the screen [0,1]x[0,1] starting
    // at the upper left corner.
    let sg =
        let normal01FrameTrafo = Trafo3d.ViewTrafo(V3d(-1.0, 1.0, 0.0), V3d.IOO * 2.0, -V3d.OIO * 2.0, V3d.OOI).Inverse
        sgs |> Sg.set
            |> Sg.effect [toEffect vertex; toEffect fragment]
            |> Sg.trafo (Mod.constant normal01FrameTrafo)



//    let test =
//        IndexedGeometryMode.TriangleList
//            |> Sg.draw
//            |> Sg.index'                [|0;1;2; 0;2;3|]
//            |> Sg.vertexAttribute'      DefaultSemantic.Positions                           [|V3f.OOI; V3f.IOI; V3f.III; V3f.OII|]
//            |> Sg.vertexAttribute'      DefaultSemantic.DiffuseColorCoordinates             [|V2f.OO; V2f.IO; V2f.II; V2f.OI|]
//            |> Sg.diffuseFileTexture'   @"E:\Development\WorkDirectory\bricksDiffuse.png"   true
//            |> Sg.effect                [toEffect Shader.fragment]

    //let sg = Sg.group' [sg]

    let mode = Mod.init FillMode.Fill

    w.Keyboard.KeyDown(Aardvark.Application.Keys.X).Values.Subscribe(fun () ->
        transact (fun () ->
            match mode.Value with
                | FillMode.Fill -> Mod.change mode FillMode.Line
                | FillMode.Line -> Mod.change mode FillMode.Point
                | _ -> Mod.change mode FillMode.Fill
        )
    ) |> ignore

    let sg = sg |> Sg.fillMode mode

    // compile the rendertask and pass it to the window
    w.RenderTask <- app.Runtime.CompileRender(ExecutionEngine.Native ||| ExecutionEngine.Optimized, sg.RenderJobs())

    // a very sketch controller for changing the viewport
    let lastPos = ref V2d.Zero
    let down = ref false
    let down = w.Mouse.IsDown Aardvark.Application.MouseButtons.Left

    w.Mouse.Move.Values.Subscribe(fun (op, np) ->
        if down.GetValue() then
            transact (fun () ->
                viewportOrigin.Value <- viewportOrigin.Value + (op.NormalizedPosition - np.NormalizedPosition) * viewportSize.Value
            )

    ) |> ignore

    w.Mouse.Scroll.Values.Subscribe(fun delta ->
        let delta = delta / 120.0

        let vp = Box2d.FromMinAndSize(viewportOrigin.Value, viewportSize.Value)
        let zoomCenter = vp.Size * w.Mouse.Position.GetValue().NormalizedPosition + vp.Min

        let newViewport = vp.Translated(-zoomCenter).Scaled(V2d.II * Fun.Pow(0.9, delta)).Translated(zoomCenter)

        transact (fun () ->
            viewportOrigin.Value <- newViewport.Min
            viewportSize.Value <- newViewport.Size
        )
    ) |> ignore




//
//    w.Mouse.Events.Values.Subscribe(fun e ->
//        match e with
//            | MouseDown p ->
//                down := true
//                lastPos := p.location.NormalizedPosition
//            | MouseUp p ->
//                down := false
//                lastPos := p.location.NormalizedPosition
//            | MouseMove pos ->
//                let pos = pos.NormalizedPosition
//                if !down then
//                    transact (fun () ->
//                        viewportOrigin.Value <- viewportOrigin.Value + (!lastPos - pos) * viewportSize.Value
//                    )
//
//                lastPos := pos
//
//            | MouseScroll(delta,pos) ->
//                let delta = delta / 120.0
//
//                let vp = Box2d.FromMinAndSize(viewportOrigin.Value, viewportSize.Value)
//                let zoomCenter = vp.Size * pos.NormalizedPosition + vp.Min
//
//                let newViewport = vp.Translated(-zoomCenter).Scaled(V2d.II * Fun.Pow(0.9, delta)).Translated(zoomCenter)
//
//                transact (fun () ->
//                    viewportOrigin.Value <- newViewport.Min
//                    viewportSize.Value <- newViewport.Size
//                )
//
//                ()
//
//            | _ -> ()
//    ) |> ignore


    // finally run the application
    w.Run()

    0
