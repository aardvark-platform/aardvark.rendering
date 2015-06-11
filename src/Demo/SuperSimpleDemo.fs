namespace Demo

open System
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.Rendering.GL
open Aardvark.SceneGraph
open Aardvark.SceneGraph.CSharp
open Aardvark.SceneGraph.Semantics
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.CSharp
open Aardvark.Base.Rendering

open Aardvark.Application
open Aardvark.Application.WinForms

open Aardvark.VRVis

module Demo =
    let demo () =
        Aardvark.Init()

        use app = new OpenGlApplication()
        let ctrl = app.CreateGameWindow(1)

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let proj = CameraProjectionPerspective(60.0, 0.1, 10000.0, float ctrl.Sizes.Latest.X / float ctrl.Sizes.Latest.Y)
        let mode = Mod.initMod FillMode.Fill


        let sg = PolyMeshPrimitives.Box(C4b.Green).GetIndexedGeometry() |> Sg.ofIndexedGeometry

        let view = DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time view

        let sg =
            sg |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.uniformColor (Mod.initConstant C4f.Red) |> toEffect
                    //DefaultSurfaces.diffuseTexture |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect
                  ]
               |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
               |> Sg.projTrafo proj.ProjectionTrafos.Mod
               //|> Sg.trafo (Mod.initConstant <| Trafo3d.ChangeYZ)
               //|> Sg.fillMode mode
               //|> Sg.blendMode (Mod.initConstant BlendMode.Blend)
    

        ctrl.Sizes.Values.Subscribe(fun s ->
            let aspect = float s.X / float s.Y
            proj.AspectRatio <- aspect
        ) |> ignore

        let task = app.Runtime.CompileRender(sg.RenderJobs())
        ctrl.RenderTask <- task

        ctrl.Run()
        0