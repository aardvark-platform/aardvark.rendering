namespace OSM 

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

[<AutoOpen>]
module Extensions =

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
        member x.Box =
            Box2d(x.MinX, x.MinY, x.MaxX, x.MaxY)


    module Mod =
        

        let async (defaultValue : 'a) (t : Task<'a>) =

            let aw = ref None
            let r = Mod.initMod defaultValue

            let a = t.GetAwaiter()
            a.OnCompleted(fun () ->
                transact (fun () -> r.Value <- a.GetResult())
            )
            
            r :> IMod<_>


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

    type KnownTileSource with
        static member CreateGooleMapsSource() =
            let fetchGoogle (uri : Uri) =
                let httpWebRequest = WebRequest.Create(uri) |> unbox<HttpWebRequest>
                httpWebRequest.UserAgent <- @"Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US; rv:1.9.1.7) Gecko/20091221 Firefox/3.5.7";
                httpWebRequest.Referer <- "http://maps.google.com/";
                RequestHelper.FetchImage(httpWebRequest)

            BruTile.Web.HttpTileSource(GlobalSphericalMercator(), "http://mt{s}.google.com/vt/lyrs=m@130&hl=en&x={x}&y={y}&z={z}", ["0"; "1"; "2"; "3"], tileFetcher = fetchGoogle) :> ITileSource

        static member CreateGoogleTerrainSource() =
            let fetchGoogle (uri : Uri) =
                let httpWebRequest = WebRequest.Create(uri) |> unbox<HttpWebRequest>
                httpWebRequest.UserAgent <- @"Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US; rv:1.9.1.7) Gecko/20091221 Firefox/3.5.7";
                httpWebRequest.Referer <- "http://maps.google.com/";
                RequestHelper.FetchImage(httpWebRequest)

            BruTile.Web.HttpTileSource(GlobalSphericalMercator(), "http://mt{s}.google.com/vt/lyrs=m@130&hl=en&x={x}&y={y}&z={z}", ["0"; "1"; "2"; "3"], tileFetcher = fetchGoogle) :> ITileSource


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
