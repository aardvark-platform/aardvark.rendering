open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

// This more complex example shows how to implement (naive) culling by using an additional renderTask
// which checks visibiliy for each object on the CPU.
// The idea of this example is to show 
//   - how to use a GameWindow (which in contrast to standard aardvark windows uses a MainLoop which renders as fast as possible)
//   - how to dynamically activate/deactivate objects efficiently (Sg.onOff)
//   - how go write a custum render task.

type Object = {
    bb        : Box3d // world space bb
    sg        : ISg   // scene
    isVisible : cval<bool>
}

[<EntryPoint>]
let main argv = 
   
    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication()
    let win = app.CreateGameWindow(samples = 1)

    let initialView = CameraView.LookAt(V3d(20.0,20.0,20.0), V3d.Zero, V3d.OOI)
    let frustum = 
        win.Sizes 
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let radius = 0.3
    let sphere = Sg.sphere' 9 C4b.White radius
    let min,max = -8,12
    let min,max = -5,5
    let objs =
        [|
            for x in min .. max do 
                for y in min .. max do
                    for z in min .. max do
                        let pos = V3d(float x,float y, float z)
                        let box = Box3d.FromCenterAndSize(pos, V3d.III * radius * 2.0)
                        let isVisible = AVal.init true
                        yield {
                            bb = box
                            sg = sphere |> Sg.onOff isVisible |> Sg.translate pos.X pos.Y pos.Z
                            isVisible = isVisible
                        }
         |]
        
    let mutable cullingDisabled = false
    let cullTask =
        let ndc = Box3d(-V3d.III, V3d.III)
        let mutable lastVisibleCnt = 0
        RenderTask.custom (fun (task,token,output) -> 
            let view = cameraView.GetValue()
            let frustum = frustum.GetValue ()
            let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
            let mutable visibleCnt = 0
            transact (fun _ -> 
                for obj in objs do
                    let isVisible = obj.bb.IntersectsFrustum viewProj.Forward
                    visibleCnt <- if isVisible || cullingDisabled then visibleCnt + 1 else visibleCnt
                    obj.isVisible.Value <- cullingDisabled || isVisible
            )
            if lastVisibleCnt <> visibleCnt then
                let enabled = if cullingDisabled then "disabled" else "enabled"
                Log.line "[CullTask %s] now are %d objects visible" enabled visibleCnt
                lastVisibleCnt <- visibleCnt
        )

    let sg =
        objs 
            |> Array.map (fun obj -> obj.sg) 
            |> Sg.ofArray
            |> Sg.effect [
                DefaultSurfaces.trafo                 |> toEffect
                DefaultSurfaces.vertexColor           |> toEffect
                DefaultSurfaces.simpleLighting        |> toEffect
                ] 
            |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo    )

    let renderTask = 
        RenderTask.ofList [
            cullTask
            app.Runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    win.Keyboard.KeyDown(Keys.C).Values.Subscribe(fun _ -> 
        cullingDisabled <- not cullingDisabled
        if cullingDisabled then
            Log.line "[CullTask] culling disabled"
        else Log.line "[CullTask] culling enabled"
    ) |> ignore 

    win.RenderTask <- renderTask
    win.Run()

    0
