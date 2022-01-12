namespace Rendering.Examples

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.GL


module HelloWorld =

    let run () =

        
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
        win.Text <- "Aardvark rocks \\o/"

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let quadSg =
            let quad =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = ([|0;1;2; 0;2;3|] :> Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions,                  [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                            DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                            DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                        ]
                )
                
            quad |> Sg.ofIndexedGeometry

        let sg =
            quadSg 
                |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                  ]
               |> Sg.viewTrafo (viewTrafo   |> AVal.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> AVal.map Frustum.projTrafo    )

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg.RenderObjects(Ag.Scope.Root))

        win.RenderTask <- task //|> DefaultOverlays.withStatistics
        win.Run()
        0





