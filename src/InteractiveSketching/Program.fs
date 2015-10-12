// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open FShade
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering
open Aardvark.Rendering.NanoVg
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics


[<EntryPoint>]
let main argv = 
    use app = new OpenGlApplication()
    use win = app.CreateSimpleRenderWindow()

    Aardvark.Init()

    let proj = CameraProjectionPerspective(60.0, 0.1, 1000.0, float win.Width / float win.Height)

    let geometry = 
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = [| 0; 1; 2; 0; 2; 3 |],
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions,                  [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                    DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                ]
        )


    let cam = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI
    let cam = DefaultCameraController.control  win.Mouse win.Keyboard win.Time cam

    let clicks = 
        win.Mouse.Click.Values 
            |> AStream.ofObservable 
            |> AStream.filter (fun b -> b = MouseButtons.Left)
            |> AStream.map (fun _ -> Mod.force win.Mouse.Position)

    let active = win.Keyboard.IsDown(Keys.X)

    let viewTrafo = cam |> Mod.map CameraView.viewTrafo
    let vp = Mod.map2 ((*)) viewTrafo proj.ProjectionTrafos.Mod

    let sketched =
        workflow {
            // partition the stream into its parts
            // while active "is" true
            // in detail this means that the latest value of active is true
            for s in AStream.splitWhile (AStream.ofMod active) clicks do
                // for every substream we create a list accumulating 
                // its values
                let result = System.Collections.Generic.List()
 
 
                // for every new value in our substream we'd like to
                // add it to the current list (e.g. a polygon)
                for v in s do
                    let n = v.NormalizedPosition 
                    let ndc = V3d(2.0*n.X+1.0,1.0-2.0*n.Y,0.0)
                    let world = vp |> Mod.force |> Trafo.backward |> (flip Mat.transformPosProj <| ndc)
                    result.Add world
 
                    // furthermore we could want to visualize the current
                    // state somehow which means that we need to
                    // "yield" an intermediate result replacing the
                    // last one (if any)
                    yield Polygon3d result
 
                // when we're done with our substream (active became false)
                // we'd like to return the final result.
                // Note that the intermediate- and final types do not 
                // necessarily need to match. 
                yield Polygon3d()
                return Polygon3d result
        }

    let createSg (p : IMod<Polygon3d>) : ISg =
        let buffer = p |> Mod.map (fun p ->
            let a = p.Points.PairChainWrap () |> Seq.collect (fun p -> [| p.E0; p.E1 |]) |> Seq.toArray
            a|> Array.map V3f |> ArrayBuffer 
        ) 
        let bufferView = BufferView(buffer |> Mod.map (fun x -> x :> IBuffer),typeof<V3f>)
        Sg.VertexAttributeApplicator(DefaultSemantic.Positions,bufferView, Mod.constant <| Sg.draw IndexedGeometryMode.LineList) :> ISg


    let intermediates = 
        sketched 
            |> Workflow.intermediates |> AStream.latest 
            |> Mod.map (fun p ->
                match p with 
                 | Some p -> p
                 | None -> Polygon3d()
               ) 

    let finals = 
        sketched 
            |> Workflow.finals |> AStream.all 
            |> ASet.map (createSg << Mod.constant)


    let sg =
        [ geometry |> Sg.ofIndexedGeometry; 
          createSg intermediates; 
          Sg.set finals |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.Red |> toEffect ] ]
            |> Sg.group
            |> Sg.viewTrafo viewTrafo
            |> Sg.projTrafo proj.ProjectionTrafos.Mod
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect ]


    let main = app.Runtime.CompileRender(BackendConfiguration.NativeOptimized, sg) |> DefaultOverlays.withStatistics

    win.RenderTask <- RenderTask.ofList [main]
    win.Run()
    0 
