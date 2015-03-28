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

let createGoogleSource() =
    let fetchGoogle (uri : Uri) =
        let httpWebRequest = WebRequest.Create(uri) |> unbox<HttpWebRequest>
        httpWebRequest.UserAgent <- @"Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US; rv:1.9.1.7) Gecko/20091221 Firefox/3.5.7";
        httpWebRequest.Referer <- "http://maps.google.com/";
        RequestHelper.FetchImage(httpWebRequest)

    BruTile.Web.HttpTileSource(GlobalSphericalMercator(), "http://mt{s}.google.com/vt/lyrs=m@130&hl=en&x={x}&y={y}&z={z}", ["0"; "1"; "2"; "3"], tileFetcher = fetchGoogle) :> ITileSource

//let fetchTasks = ConcurrentDictionary()
//type KnownTileSources with
//    static member CreateAsync(s : KnownTileSource) =
//        let fetchGoogle (uri : Uri) =
//            
////            let httpWebRequest = WebRequest.Create(uri) |> unbox<HttpWebRequest>
////            httpWebRequest.UserAgent <- @"Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US; rv:1.9.1.7) Gecko/20091221 Firefox/3.5.7";
////            httpWebRequest.Referer <- "http://maps.google.com/";
////            RequestHelper.FetchImage(httpWebRequest)
//            
//            [||]
//
//        KnownTileSources.Create(s, tileFetcher = Func<Uri, byte[]>(fetchGoogle))


let noTileImage = 
    let bmp = new Bitmap(256,256)

    use g = Graphics.FromImage(bmp)
    g.Clear(Color.Black)

    use p = new Pen(Brushes.Red, 10.0f)
    g.DrawLine(p, 0, 0, 256, 256)
    g.DrawLine(p, 256, 0, 256, 0)

    bmp

type HttpTileSource with
    member x.GetTileAsync(tileInfo : TileInfo) =
        let url = x.GetUri(tileInfo)

        let request = WebRequest.Create(url)
        request.UseDefaultCredentials <- true
        async {
            try
                let! response = request.GetResponseAsync() |> Async.AwaitTask

                if response.ContentType.StartsWith "image" then
                    use s = response.GetResponseStream()
                    return System.Drawing.Bitmap.FromStream(s) |> unbox<System.Drawing.Bitmap>
                    //return! s.AsyncRead(int response.ContentLength)

                else
                    return noTileImage
            with e ->
                return noTileImage
        }

type ITileSource with
    member x.GetTileAsync(info : TileInfo) =
        let w = x |> unbox<HttpTileSource>
        w.GetTileAsync(info)


type MyScheduler(threads : int) as this =
    inherit TaskScheduler()

    let bag = ConcurrentBag<Task>()
    let sem = new SemaphoreSlim(0)

    let threads = 
        lazy (
            Array.init threads (fun i -> 
                let t = new Thread(ParameterizedThreadStart(this.Work), IsBackground = true)
                t.Name <- sprintf "MyScheduler[%d]" i
                t.Start(i)
                t
            )
        )

    let start() = threads.Value |> ignore

    member private x.Work (state : obj) =
        let index = state |> unbox<int>

        while true do
            sem.Wait()

            match bag.TryTake() with
                | (true, t) ->
                    base.TryExecuteTask(t) |> ignore
                | _ -> ()

        ()


    override x.GetScheduledTasks() = start(); bag |> Seq.toArray :> seq<_>
    override x.QueueTask(t : Task) =
        start()
        bag.Add(t)
        sem.Release() |> ignore

    override x.TryExecuteTaskInline(t : Task, wasEnqueued : bool) =
        start()
        if wasEnqueued then
            false
        else
            base.TryExecuteTask(t)
            


[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    
    let app = new OpenGlApplication()
    let w = app.CreateSimpleRenderWindow(1)
    w.Width <- 1280
    w.Height <- 1024
    let source = KnownTileSources.Create(KnownTileSource.BingHybrid)

    // let's assume the tile resolution is constant
    let realTileResolution = V2i(256, 256)
    let worldBounds = source.Schema.Extent.Box

    // initialize a viewport (small part of the world currently)
    // NOTE that the viewport matches the aspect-ratio of the initial
    //      window-size. therefore tiles will appear quadradic.
    let viewportOrigin, viewportSize = 
        let horizontal = worldBounds.Size.X * 0.05
        let vertical = (float w.Sizes.Latest.Y / float w.Sizes.Latest.X) * horizontal
        let box = Box2d.FromCenterAndSize(worldBounds.Center, V2d(horizontal, vertical))

        Mod.initMod box.Min, Mod.initMod box.Size

    let viewport = Mod.map2 (schönfinkel Box2d.FromMinAndSize) viewportOrigin viewportSize

    w.Sizes.Values.Subscribe(fun s ->
        let aspect = float s.X / float s.Y
        transact (fun () ->
            let vp = viewport.GetValue()
            let targetAspect = vp.SizeX / vp.SizeY

            let newVp = vp.ScaledFromCenterBy(V2d(aspect / targetAspect, 1.0))

            viewportOrigin.Value <- newVp.Min
            viewportSize.Value <- newVp.Size


            ()
        )
    ) |> ignore


    // in order to determine the appropriate grid-size we will need the view's size
    let viewResolution = w.Sizes.Mod

    // determine the zoom-level using the current viewport and view-size
    let zoomLevel = 
        adaptive {
            let! vpSize = viewportSize
            let! s = viewResolution
            let res = vpSize / V2d s
            return Utilities.GetNearestLevel(source.Schema.Resolutions, max res.X res.Y)
        } |> Mod.always
        


    // determine the tile-size in world-space
    let tileSize = 
        zoomLevel |> Mod.map (fun l -> let gridSize = V2d(source.Schema.GetMatrixWidth(l), source.Schema.GetMatrixHeight(l)) in worldBounds.Size / gridSize)

    // calculate the real resolution of tiles for the current view
    let tileViewResolution =
        adaptive {
            let! viewResolution = viewResolution

            let! tileSize = tileSize
            let! viewportSize = viewportSize

            let r = tileSize / viewportSize
            return V2i (V2d viewResolution * r)
        }

    // calculate the total grid size (todo: +2 may be to conservative)
    let gridSize = 
        adaptive {
            let! size = viewResolution
            let! tileResolution = tileViewResolution

            let res =  V2i(ceil (float size.X / float tileResolution.X), ceil (float size.Y / float tileResolution.Y)) + V2i.II
            return res
        } |> Mod.always


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
            //printfn "offset: %A" offset

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
            DefaultSemantic.DiffuseColorCoordinates, [|V2f.OI; V2f.II; V2f.IO; V2f.OO|] :> Array
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

    

    
    // create a checkerboard-texture as placeholder for unloaded texture
    let noTexture =
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 16L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                C4b.Gray
        ) |> ignore

        app.Runtime.CreateTexture(PixTexture2d(PixImageMipMap [| pi :> PixImage |], true))

    // get a chached texture for the given tile and zoom-level
    // TODO: add proper memory management (textures are never deleted)
    let cache = System.Collections.Concurrent.ConcurrentDictionary<string * V2i, IMod<ITexture>>()


    let scheduler = MyScheduler(25)
    let factory = TaskFactory(scheduler)
    let dataLock = obj()
    let getTileTexure (coord : V2i) (zoom : string)=
        cache.GetOrAdd((zoom, coord), fun (zoom, coord) ->
            let info = TileInfo()
            info.Index <- TileIndex(coord.X, coord.Y, zoom)

            // printfn "creating texture for %s/%d/%d" zoom coord.X coord.Y

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
                    let! bmp = source.GetTileAsync info
                    return BitmapTexture(bmp, false) |> app.Runtime.CreateTexture
                }
            let tcs = TaskCompletionSource<ITexture>()
            factory.StartNew(fun () -> Async.StartWithContinuations(run, tcs.SetResult, tcs.SetException, fun _ -> tcs.SetCanceled())) |> ignore
            tcs.Task |> Mod.async noTexture

//            Task.Factory.StartNew(fun () ->
//                let data = source.GetTile(info)
//                use ms = new System.IO.MemoryStream(data)
//                use bmp = System.Drawing.Bitmap.FromStream(ms) |> unbox<System.Drawing.Bitmap>
//                
//                let tex = app.Runtime.CreateTexture <| BitmapTexture(bmp, false)
//
//                tex
//            )
        )

    // calculate a set of SceneGraph nodes having the appropriate transformations for 
    // all tiles (using the FullScreenQuad from above)
    let sgs =
        aset {
            for coord in tileIndices do
                let tex = 
                    adaptive {
                        let! z = zoomLevel
                        let! (f,_) = firstTileAndOffset
                        //printfn "%A: %A %A" coord (f + coord) z
                        let t = getTileTexure (f + coord) z
                        return! t
                    } //Mod.bind2 (fun z (f,_) -> getTileTexure (f + coord) z) zoomLevel firstTileAndOffset
                yield fsq |> Sg.trafo (calcTileTrafo coord)
                          |> Sg.diffuseTexture tex
                
        }


    // apply an effect and a trafo making the screen [0,1]x[0,1] starting
    // at the upper left corner.
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
                let pos = pos.NormalizedPosition
                if !down then
                    transact (fun () ->
                        viewportOrigin.Value <- viewportOrigin.Value + (!lastPos - pos) * viewportSize.Value
                    )

                lastPos := pos

            | MouseScroll(delta,pos) ->
                let delta = delta / 120.0

                let vp = Box2d.FromMinAndSize(viewportOrigin.Value, viewportSize.Value)
                let zoomCenter = vp.Size * pos.NormalizedPosition + vp.Min

                let newViewport = vp.Translated(-zoomCenter).Scaled(V2d.II * Fun.Pow(0.9, delta)).Translated(zoomCenter)

                transact (fun () ->
                    viewportOrigin.Value <- newViewport.Min
                    viewportSize.Value <- newViewport.Size
                )

                ()

            | _ -> ()
    ) |> ignore


    // finally run the application
    Application.Run w

    0
