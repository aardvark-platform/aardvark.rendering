namespace Meixner
//
//open System
//open System.IO
//open System.Net
//open BruTile
//open BruTile.Cache
//open BruTile.Predefined
//open Aardvark.Base
//open DevILSharp
//open Aardvark.Base.Incremental
//open Aardvark.Application
//open Aardvark.Application.WinForms
//open Aardvark.SceneGraph
//open FShade
//open Aardvark.Rendering.GL
//open System.Drawing
//open System.Drawing.Imaging
//open System.Threading.Tasks
//open OSM
//open System.Windows.Forms
//open BruTile.Web
//open System.Net
//open System.Web
//open System.Collections.Generic
//open System.Collections.Concurrent
//open System.Threading
//open System.Net
//open System.Web
//open System.Globalization
//open CSharpStuff
//
//[<AutoOpen>]
//module Geometry =
//
//    // since tiles are rectangles we may need to define a rectangle-geometry
//    let zeroOneQuad = 
//        let q = IndexedGeometry()
//
//        q.Mode <- IndexedGeometryMode.TriangleList
//        q.IndexArray <- [|0;1;2; 0;2;3|]
//        q.IndexedAttributes <- SymDict.ofList [
//            DefaultSemantic.Positions, [|V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO|] :> Array
//            DefaultSemantic.DiffuseColorCoordinates, [|V2f.OI; V2f.II; V2f.IO; V2f.OO|] :> Array
//        ]
//
//        Sg.ofIndexedGeometry q
//
//[<AutoOpen>]
//module Shader = 
//    open FShade
//
//    type Vertex = { [<Position>] pos : V4d
//                    [<TexCoord>] tc : V2d }
//
//    let vertex (v : Vertex) =
//        vertex {
//            let world = uniform.ModelTrafo * v.pos
//            return { v with pos = uniform.ViewProjTrafo * world }
//        }
//
//    let diffuseTex = 
//        sampler2d {
//            texture uniform?DiffuseColorTexture
//            addressU WrapMode.Clamp
//            addressV WrapMode.Clamp
//            filter Filter.MinMagLinear
//        }
//
//    let fragment (v : Vertex) =
//        fragment {
//            let color = diffuseTex.Sample(v.tc)
//            return color
//        }
//
//
//type TileData = { bounds : Box3d; texture : IMod<PixImage>; trafo : Trafo3d }
//
//type Tile =
//    | Pending 
//    | Invalid
//    | Tile of TileData
//
////type ILayer =
////    abstract member IndexBounds : Box2i
////    abstract member GetTile : V2i -> Tile
////
////type ITile
//
//
//[<AutoOpen>]
//module Application =
//
//    type StreamReader with
//        member x.ReadAllLinesAsync() =
//            async {
//                let! content = x.ReadToEndAsync() |> Async.AwaitTask
//                return content.Split([|Environment.NewLine|], StringSplitOptions.None)
//            }
//
//    type Box2i with
//        member x.All =
//            seq {
//                for xi in x.Min.X..x.Max.X-1 do
//                    for yi in x.Min.Y..x.Max.Y-1 do
//                        yield V2i(xi,yi)
//            }
//
//    let createScene (viewProjTrafo : IMod<Trafo3d>) =
//        let globalTrafo = Trafo3d.Translation(V3d(-530500, -5213300, 0))
//        let tileScale = Trafo3d.Scale(V3d(4096, 4096,1))
//
//        let emptyImage = PixImage<byte>(Col.Format.RGBA, 1, 1) :> PixImage
//
//        let loadTile (index : V2i) =
//            let tileName = String.Format(@"tile_{0:0000}_{1:0000}", index.X, index.Y);
//            let tileDir = @"F:\Holistic\Input\orthos\graz_neu\";
//            let tfwName = tileDir + tileName + ".dsm.tfw";
//            let tifName = tileDir + tileName + ".photo.tif"
//
//            if not (File.Exists tfwName) then
//                Mod.initConstant Invalid
//            else
//                let parseTrafo (lines : string[]) =
//                    let mutable t2wMat = M44d.Identity
//                    let ci = CultureInfo.InvariantCulture
//                    t2wMat.M00 <- Double.Parse(lines.[0], ci); t2wMat.M10 <- Double.Parse(lines.[1], ci)
//                    t2wMat.M01 <- Double.Parse(lines.[2], ci); t2wMat.M11 <- Double.Parse(lines.[3], ci)
//                    t2wMat.M03 <- Double.Parse(lines.[4], ci); t2wMat.M13 <- Double.Parse(lines.[5], ci)
//                    t2wMat
//
//                let loadTexture (file : string) =
//                    async {
//                        return PixImage.Create(file)
//                    }
//
//                let createTile =
//                    async {
//                        Log.line "init %A" index
//
//                        use s = new StreamReader(new FileStream(tfwName, FileMode.Open))
//                        let! lines = s.ReadAllLinesAsync()
//                        let t2wMat = parseTrafo lines
//
//                        let bounds =
//                            Box3d.FromPoints(t2wMat.TransformPos V3d.Zero, t2wMat.TransformPos <| V3d(4096,4096,0))
//
//                        return Tile { 
//                            bounds = bounds
//                            texture = tifName |> loadTexture |> Mod.lazyAsync emptyImage
//                            trafo = Trafo3d(t2wMat, t2wMat.Inverse) 
//                        }
//                    }
//
//                createTile |> Async.StartAsTask |> Mod.async Pending
//         
//        let indexBounds = Box2i.FromMinAndSize(0,0,3,3)   
//
//        let tiles = indexBounds.All |> Seq.map loadTile |> ASet.ofSeq
//
//        let tilesInView =
//            aset {
//                for t in tiles do
//                    let! t = t
//                    
//                    match t with
//                        | Tile data ->
//                            let b = data.bounds.Transformed globalTrafo
//                            let! vp = viewProjTrafo
//                            if b.IntersectsFrustum vp.Forward then
//                                Log.line "yeah"
//                                yield data
//                            else
//                                Log.line "nope"
//
//                        | _ -> ()
//
//            }
//
//        let loadTexture (img : PixImage) =
//            let tex = PixTexture2d(PixImageMipMap [|img|], false)
//            tex :> ITexture
//
//
//        let sceneGraphs =
//            aset {
//                for tile in tilesInView do
//                    let tex = tile.texture |> Mod.map loadTexture
//                    yield zeroOneQuad 
//                        |> Sg.trafo (Mod.initConstant (tileScale * tile.trafo * globalTrafo))
//                        |> Sg.diffuseTexture tex
//            }
//
//        sceneGraphs
//            |> Sg.set
//            |> Sg.effect [toEffect vertex; toEffect fragment]
//
//
//    let run() =
//        use app = new OpenGlApplication()
//        use window = app.CreateSimpleRenderWindow()
//
//        let cameraView = CameraViewWithSky(Location = V3d(0,0,600), Forward = -V3d.OOI.Normalized, Sky = V3d.OIO)
//        let cameraProj = CameraProjectionPerspective(60.0, 1.0, 8000.0, 1.0)
//
//        let viewProj = Mod.map2 (*) cameraView.ViewTrafos.Mod cameraProj.ProjectionTrafos.Mod
//
//        let sg = createScene viewProj
//
//        let sg =
//            sg |> Sg.viewTrafo cameraView.ViewTrafos.Mod
//               |> Sg.projTrafo cameraProj.ProjectionTrafos.Mod
//
//        let task = app.Runtime.CompileRender(sg.RenderJobs())
//        window.RenderTask <- task
//
//        let controller = 
//            DefaultCameraControllers(
//                HciMouseWinFormsAsync (window.Control.Implementation), 
//                HciKeyboardWinFormsAsync (window.Control.Implementation), 
//                cameraView,
//                isEnabled = EventSource true,
//                moveSpeed = Constant.PiTimesTwo
//            )
//
//        window.Sizes.Values.Subscribe(fun s ->
//            cameraProj.AspectRatio <- float s.X / float s.Y
//        ) |> ignore
//
//        Application.Run window
//        ()