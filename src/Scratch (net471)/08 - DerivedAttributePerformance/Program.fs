open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Application.WinForms.Vulkan

module Shader =
    open FShade 

    type Vertex =
        {
            [<Position>] pos    : V4d
            [<WorldPosition>] wp : V4d
        }

    let niceTrafo (v : Vertex) =
        vertex {
            let wp = uniform.ModelTrafo * v.pos
            let p = uniform.ViewProjTrafo * wp
            return { pos = p; wp = wp }
        }

[<EntryPoint>]
let main argv = 
    
    
    Aardvark.Init()

    use app = new OpenGlApplication()
    let win = app.CreateSimpleRenderWindow(samples = 8)

    let glCtrl = win.Control.Implementation :?> OpenGlRenderControl

    glCtrl.KeyDown.Add(fun e ->
        if e.KeyCode = System.Windows.Forms.Keys.End && e.Control then 
            glCtrl.RenderContinuously <- not glCtrl.RenderContinuously
            glCtrl.Invalidate()
            e.Handled <- true
    )

    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    let frustum = 
        win.Sizes 
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let rnd = Random(1123)
    let stuff = cset()
    let cnt = 1000
    let size = sqrt (float cnt)
    for i in 1..cnt do
        let sphere = IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere Sphere3d.Unit 8 C4b.White
        let trafo = Trafo3d.Translation(rnd.NextDouble() * size, rnd.NextDouble() * size, 0.0)
        let sg = Sg.ofIndexedGeometry sphere
                    |> Sg.trafo (AVal.init trafo)
        stuff.Add(sg) |> ignore

    let sg =
            stuff
            |> Sg.set
            |> Sg.effect [
                    Shader.niceTrafo                       |> toEffect // using ViewProj should only create one view*proj computation
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                ]
            |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo    )


    let renderTask = 
        app.Runtime.CompileRender(win.FramebufferSignature, sg)
        
    win.RenderTask <- renderTask
    win.Run()
    
    0
