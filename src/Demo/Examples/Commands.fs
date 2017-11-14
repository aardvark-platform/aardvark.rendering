namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.Vulkan
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open FShade
open FShade.Imperative

module CommandTest =
    let run() =
        use app = new VulkanApplication(true)
        let win = app.CreateSimpleRenderWindow(8)
        let runtime = app.Runtime
        let device = runtime.Device



        let cameraView  = DefaultCameraController.control win.Mouse win.Keyboard win.Time (CameraView.LookAt(3.0 * V3d.III, V3d.OOO, V3d.OOI))    
        let frustum     = win.Sizes    |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))       
        let viewTrafo   = cameraView    |> Mod.map CameraView.viewTrafo
        let projTrafo   = frustum       |> Mod.map Frustum.projTrafo        

        

        let sg1 =
            Sg.box' C4b.Red Box3d.Unit
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.Red
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo viewTrafo
                |> Sg.projTrafo projTrafo
                
        let sg2 =
            Sg.unitSphere' 5 C4b.Red
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.Green
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo viewTrafo
                |> Sg.projTrafo projTrafo

        let condition = Mod.init true

        win.Keyboard.DownWithRepeats.Values.Add (fun k ->
            match k with    
                | Keys.Space -> transact (fun () -> condition.Value <- not condition.Value)
                | _ -> ()
        )


        let objects1 = sg1.RenderObjects()
        let objects2 = sg2.RenderObjects()
        let cmd = 
            RuntimeCommand.IfThenElse(
                condition,
                RuntimeCommand.Render objects1,
                RuntimeCommand.Render objects2
            )


        win.RenderTask <- new RenderTask.CommandTask(device, unbox win.FramebufferSignature, cmd)
        win.Run()
  


