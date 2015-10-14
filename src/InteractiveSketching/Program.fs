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

type HighlightConfig =
    {
        viewProj : IMod<Trafo3d>
        mousePosition : IMod<PixelPosition>
        pointSize : IMod<float>
        lineWidth : IMod<float>
        highlightColor : C4b
        lineColor : C4b
        pointColor : C4b
    }

module Sg =

    let composeEffect l =
        l |> SequentialComposition.compose 
          |> FShadeSurface :> ISurface 
          |> Mod.constant

    let private lineSurface =
        composeEffect [ 
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.thickLine |> toEffect
            DefaultSurfaces.sgColor |> toEffect
            DefaultSurfaces.thickLineRoundCaps |> toEffect
        ]

    let private solidSurface =
        composeEffect [ 
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.sgColor |> toEffect
            DefaultSurfaces.simpleLighting |> toEffect
        ]



    let private lineWithoutPointSurface =
        composeEffect [ 
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.thickLine |> toEffect
            DefaultSurfaces.vertexColor |> toEffect
            DefaultSurfaces.thickLineSparePointSizeCaps |> toEffect
        ]

    let private pointSurface =
        composeEffect [ 
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.pointSprite |> toEffect
            DefaultSurfaces.vertexColor |> toEffect
            DefaultSurfaces.pointSpriteFragment |> toEffect
        ]


    let wirePolygon (lineWidth : IMod<float>) (color : IMod<C4f>) (poly : IMod<Polygon3d>)=
        let positions = 
            poly |> Mod.map (fun p ->
                p.Points.PairChainWrap() 
                    |> Seq.collect (fun p -> [| p.E0; p.E1 |]) 
                    |> Seq.map V3f
                    |> Seq.toArray
            ) 

        IndexedGeometryMode.LineList
            |> Sg.draw
            |> Sg.vertexAttribute DefaultSemantic.Positions positions
            |> Sg.surface lineSurface
            |> Sg.uniform "LineWidth" lineWidth
            |> Sg.uniform "Color" color

    let solidPolygon (color : IMod<C4f>) (poly : IMod<Polygon3d>) =
        let foldr seq seed f = Seq.foldBack f seq seed

        let polys =
            poly |> Mod.map (fun p -> 
                [p]
            )

        let indices =
            polys |> Mod.map (fun p ->
                let (indices, _) =
                    foldr p ([], 0) (fun pi (indices, offset) ->
                        let pii = 
                            pi.ComputeTriangulationOfConcavePolygon(Constant.PositiveTinyValue)
                                |> Seq.map ((+) offset)
                                |> Seq.toArray

                        (pii :: indices, offset + pii.Length) 
                    ) 

                Array.concat indices
            )

        let positions = 
            polys |> Mod.map (
                Seq.collect (fun pi -> pi.Points |> Seq.map V3f) >>
                Seq.toArray
            ) 

        let normals =
            polys |> Mod.map (
                Seq.collect (fun pi -> Array.create pi.PointCount (V3f (pi.ComputeNormal()))) >>
                Seq.toArray
            )

        IndexedGeometryMode.TriangleList
            |> Sg.draw
            |> Sg.vertexAttribute DefaultSemantic.Positions positions
            |> Sg.vertexAttribute DefaultSemantic.Normals normals
            |> Sg.index indices
            |> Sg.surface solidSurface
            |> Sg.uniform "Color" color



    let highlightedWirePolygon (config : HighlightConfig) (active : IMod<bool>) (poly : IMod<Polygon3d>) : ISg =

        let screenPosition (bounds : Box2i) (viewProj : Trafo3d) (p : V3d) =
            let ndc = viewProj.Forward.TransformPosProj p
            let nn = V2d(0.5 * ndc.X + 0.5, 0.5 - 0.5 * ndc.Y)

            V3d(V2d bounds.Min + nn * V2d bounds.Size, 0.0)



        let positions = 
            poly |> Mod.map (fun p ->
                p.Points.PairChainWrap() 
                    |> Seq.collect (fun p -> [| p.E0; p.E1 |]) 
                    |> Seq.map V3f
                    |> Seq.toArray
            ) 

        let highlightedPoint =
            adaptive {
                let! pointSize = config.pointSize
                let! active = active
                if active then
                    let! p = poly
                    let! vp = config.viewProj
                    let! mp = config.mousePosition

                    let screenPosition = screenPosition mp.Bounds vp


                    let closestIndex, closestDistance = 
                        p.Points
                            |> Seq.map screenPosition
                            |> Seq.mapi(fun i l -> i, V3d.Distance(l, V3d(V2d mp.Position, 0.0)))
                            |> Seq.minBy snd

                    if closestDistance < 0.5 * pointSize then
                        return Some closestIndex
                    else
                        return None
                else
                    return None
            }

        let highlightedLine =
            adaptive {
                let! lineWidth = config.lineWidth
                let! active = active
                let! pp = highlightedPoint
                if active && pp.IsNone then
                    let! p = poly
                    let! vp = config.viewProj
                    let! mp = config.mousePosition


                    let screenPosition = screenPosition mp.Bounds vp

                    let closestIndex, closestDistance = 
                        p.EdgeLines
                            |> Seq.map (fun l -> Line3d(screenPosition l.P0, screenPosition l.P1))
                            |> Seq.mapi(fun i l -> i, l.GetMinimalDistanceTo(V3d(V2d mp.Position, 0.0)))
                            |> Seq.minBy snd

                    if closestDistance < 0.5 * lineWidth then
                        return Some closestIndex
                    else
                        return None
                else
                    return None
            }


        let colors = 
            adaptive {
                let! p = positions
                let! h = Mod.onPush highlightedLine

                match h with
                    | Some hi ->
                        return p |> Array.mapi (fun i _ -> if i / 2 = hi then config.highlightColor else config.lineColor)
                    | None ->
                        return p |> Array.map (fun _ -> config.lineColor)

            }

        let pointPositions =
            poly |> Mod.map (fun p -> 
                p.GetPointArray() |> Array.map V3f
            )

        let pointColors =
            adaptive {
                let! p = poly
                let! h = Mod.onPush highlightedPoint

                match h with
                    | Some h ->
                        return Array.init p.PointCount (fun i -> if i = h then config.highlightColor else config.pointColor)
                    | None ->
                        return Array.create p.PointCount config.pointColor
            }

        let lines = 
            IndexedGeometryMode.LineList
                |> Sg.draw
                |> Sg.vertexAttribute DefaultSemantic.Positions positions
                |> Sg.vertexAttribute DefaultSemantic.Colors colors
                |> Sg.surface lineWithoutPointSurface

        let points =
            IndexedGeometryMode.PointList
                |> Sg.draw
                |> Sg.vertexAttribute DefaultSemantic.Positions pointPositions
                |> Sg.vertexAttribute DefaultSemantic.Colors pointColors
                |> Sg.surface pointSurface

        Sg.group' [lines; points]
            |> Sg.uniform "LineWidth" config.lineWidth
            |> Sg.uniform "PointSize" config.pointSize

[<EntryPoint>]
let main argv = 
    use app = new OpenGlApplication()
    let win = app.CreateSimpleRenderWindow(8)

    Aardvark.Init()

    let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y) |> Frustum.toTrafo) //CameraProjectionPerspective(60.0, 0.1, 1000.0, float win.Width / float win.Height)

    let geometry = 
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = [| 0; 1; 2; 0; 2; 3 |],
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions,                  [| V3f(0.0f, 0.0f, -0.01f); V3f(1.0f, 0.0f, -0.01f); V3f(1.0f, 1.0f, -0.01f); V3f(0.0f, 1.0f, -0.01f) |] :> Array
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
    let vp = Mod.map2 ((*)) viewTrafo proj

    let plane = Mod.init Plane3d.ZPlane

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
                    let ndc = V3d(2.0*n.X - 1.0, 1.0 - 2.0*n.Y,0.0)

                    let ndcToWorld = vp |> Mod.force |> Trafo.backward
                    let near = Mat.transformPosProj ndcToWorld ndc
                    let cam = viewTrafo.GetValue().Backward.TransformPos V3d.Zero

                    
                    let ray = Ray3d(cam, near - cam |> Vec.normalize)
                    let world = ray.Intersect (Mod.force plane)


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


    let intermediate = 
        sketched 
            |> Workflow.intermediates |> AStream.latest 
            |> Mod.map (fun p ->
                match p with 
                 | Some p -> p
                 | None -> Polygon3d()
               ) 
            |> Sg.wirePolygon (Mod.constant 10.0) (Mod.constant C4f.Green)

    let config = 
        { 
            viewProj = vp
            mousePosition = win.Mouse.Position
            pointSize = Mod.constant 40.0
            lineWidth = Mod.constant 15.0 
            highlightColor = C4b.Red
            pointColor = C4b.Yellow
            lineColor = C4b.Green
        }

    let active = Mod.init true

    let finals = 
        sketched 
            |> Workflow.finals 
            |> AStream.all 
            |> ASet.map (Mod.constant >> Sg.highlightedWirePolygon config active)


    let sg =
        [ intermediate; 
          Sg.highlightedWirePolygon config active (Mod.constant (Polygon3d [V3d.OOO; V3d.IOO; V3d.IIO; V3d.OIO]))

          Sg.set finals ]
            |> Sg.group
            |> Sg.viewTrafo viewTrafo
            |> Sg.projTrafo proj
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect; DefaultSurfaces.simpleLighting |> toEffect ]
            |> Sg.uniform "ViewportSize" win.Sizes

    let main = app.Runtime.CompileRender(BackendConfiguration.NativeOptimized, sg) |> DefaultOverlays.withStatistics

    win.Keyboard.KeyDown(Keys.H).Values.Subscribe(fun _ ->
        transact (fun () ->
            active.Value <- not active.Value
            printfn "%A" active.Value
        )
    ) |> ignore


    win.RenderTask <- RenderTask.ofList [main]
    win.Run()
    0 
