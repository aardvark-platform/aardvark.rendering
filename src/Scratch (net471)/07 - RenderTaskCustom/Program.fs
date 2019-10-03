open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Diagnostics


[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication()
    let win = app.CreateSimpleRenderWindow(samples = 8)

    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    let frustum = 
        win.Sizes 
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let sphere = IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere Sphere3d.Unit 10 C4b.White

    let sg =
            Sg.ofIndexedGeometry sphere
            |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                ]
            |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
            |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo    )


    let renderTask = 
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    
    let renderTask =
        RenderTask.custom (fun (self,token,outputDesc) -> 
            let sw = Stopwatch.StartNew()
            renderTask.Run(self, token, outputDesc)
            Log.line "RenderTime: %dms" sw.ElapsedMilliseconds
        )
        
    win.RenderTask <- renderTask
    win.Run()
    
    0
