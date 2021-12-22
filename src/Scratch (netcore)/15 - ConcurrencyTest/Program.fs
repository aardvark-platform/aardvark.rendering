open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.SceneGraph.Semantics
open Aardvark.Application.Slim
open System
open System.Collections.Generic

// This example illustrates how to render a simple triangle using aardvark.

[<EntryPoint>]
let main argv = 
    

    (* see: https://github.com/aardvark-platform/aardvark.rendering/issues/69 *)

    let mutable running = true
    let prepareIt = true       // OK
    let inlineDispose = true   // OK
    let perObjTexture = true   // OK
    let prepareTexture = true  // OK
    let addRemoveTest = true   // OK
    let textureTest = true     // OK
    let jitterFrames = false   // OK

    Aardvark.Init()

    use app = new VulkanApplication([], Some Vulkan.DebugConfig.Default)
    //GL.Config.UseNewRenderTask <- true
    //use app = new OpenGlApplication()
    let win = app.CreateGameWindow(1)

    let signature = win.FramebufferSignature
        //win.Runtime.CreateFramebufferSignature [
        //    DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
        //    DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
        //]

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red


    let angle = cval 0.0

    let cameraMovement () = 
        Thread.Sleep 2000
        while running do
            transact (fun _ -> 
                angle.Value <- angle.Value + 0.01
            )

    let startThread (name : string) (f : unit -> unit) = 
        let t = Thread(f)
        t.IsBackground <- true
        t.Name <- name
        t.Start()
        t


    let texture = cval (DefaultTextures.blackTex.GetValue())
    let preparedObjects = HashSet<IPreparedRenderObject>()

    // Prepared textures that have not been deleted yet. If ROs are not prepared immediately
    // it can be the case that prepared textures are not deleted because the
    // activate IDisposable of a newly added RO is never disposed (as the RO is never prepared in the render task).
    // I can't think of a way to circumvent this by design, users must be careful
    // to dispose of their manually prepared resources. In this concurrent scenario, this
    // is quite obscure but it probably can't be helped.
    let textureLock = obj()
    let preparedTextures = HashSet<IBackendTexture>()

    let addTexture (t : IBackendTexture) =
        lock textureLock (fun _ ->
            preparedTextures.Add(t) |> ignore
        )

    let removeTexture (t : IBackendTexture) =
        lock textureLock (fun _ ->
            preparedTextures.Remove(t) |> ignore
        )

    let createTexture (d : C4b) = 
        let checkerboardPix = 
            let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
            pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
                let c = c / 16L
                if (c.X + c.Y) % 2L = 0L then
                    d
                else
                    C4b.Gray
            ) |> ignore
            pi

        let tex = 
            PixTexture2d(PixImageMipMap [| checkerboardPix :> PixImage |], true) :> ITexture //|> AVal.constant

        tex

    let updateTexture () = 
        Thread.Sleep 2000
        let rnd = new System.Random()
        while running do
            let d = if rnd.NextDouble() < 0.5 then C4b.White else C4b.Gray
            let tex = createTexture d

            transact (fun _ -> texture.Value <- tex)


    let cameraView = 
        angle |> AVal.map (fun angle -> 
            let r = Trafo3d.RotationZInDegrees angle
            CameraView.lookAt (r.Forward.TransformPos(V3d.III * 5.0)) V3d.OOO V3d.OOI
        )
    let viewTrafo = cameraView |> AVal.map CameraView.viewTrafo
    let frustum = 
        win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 150.0 (float s.X / float s.Y))
    let projTrafo = frustum |> AVal.map Frustum.projTrafo


    let geometry = Primitives.unitBox

    let template =
        geometry
            |> Sg.ofIndexedGeometry
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.simpleLighting

            }
            |> Sg.diffuseTexture texture
    let template =
        template.RenderObjects(Ag.Scope.Root).Content |> AVal.force |> HashSet.toList |> List.head |> unbox<RenderObject>
    let cam = viewTrafo |> AVal.map (fun v -> v.Backward.TransformPosProj V3d.Zero)

    let uniforms (t : Trafo3d) (tex : aval<ITexture>) =
        UniformProvider.ofList [
            "ModelTrafo", AVal.constant t :> IAdaptiveValue
            "ViewTrafo", viewTrafo :> IAdaptiveValue
            "CameraLocation", cam :> IAdaptiveValue
            "LightLocation", cam :> IAdaptiveValue
            "ProjTrafo", projTrafo :> IAdaptiveValue
            "DiffuseColorTexture", tex :> IAdaptiveValue
        ]

    let things = cset []

    let cleanupLock = obj()
    let mutable delayedDisposals = HashSet<IPreparedRenderObject>()

    let addForCleanup (p : IPreparedRenderObject) =
        lock cleanupLock (fun _ ->
            delayedDisposals.Add(p) |> ignore
        )

    let cleanup () =
        let disposals =
            lock cleanupLock (fun _ ->
                let mine = delayedDisposals
                delayedDisposals <- HashSet()
                mine
            )

        for d in disposals do
            d.Dispose()

    win.AfterRender.Add(cleanup)

    let addThing (trafo : Trafo3d) =
        let texture, activate = 
            if perObjTexture then
                 let tex = createTexture C4b.Gray
                 if prepareTexture then 
                    let pTex = win.Runtime.PrepareTexture(tex)
                    addTexture pTex

                    let activate =
                        { new IDisposable with
                            member x.Dispose() =
                                removeTexture pTex
                                win.Runtime.DeleteTexture pTex
                        }

                    AVal.constant (pTex :> ITexture), fun () -> activate
                 else
                    AVal.constant tex, template.Activate
            else texture :> aval<_>, template.Activate

        let ro =
            { template with
                Id = newId()
                Uniforms = uniforms trafo texture
                Activate = activate
            } :> IRenderObject

        let ro =
            if prepareIt then
                let p = win.Runtime.PrepareRenderObject(signature, ro)
                preparedObjects.Add(p) |> ignore
                p :> IRenderObject
            else
                ro

        transact (fun _ ->
            things.Add ro |> ignore
        )

    let addThings () = 
        Thread.Sleep 2000
        let rnd = new System.Random()
        let mutable runs = 0
        while running do
            if runs % 10000 = 0 then Log.line "cnt: %A" things.Count
            if rnd.NextDouble() <= 0.5 then
                let trafo = Trafo3d.Translation(rnd.NextDouble()*10.0,rnd.NextDouble()*10.0,rnd.NextDouble()*10.0)
                addThing trafo
            elif things.Count > 0 then
                let rndIndx = rnd.Next(0,things.Count-1)
                if rndIndx < things.Count - 1 then
                    let nth = Seq.item rndIndx things

                    transact (fun _ -> 
                        things.Remove nth |> ignore
                    )

                    match nth with
                    | :? IPreparedRenderObject as p ->
                        preparedObjects.Remove(p) |> ignore
                        if inlineDispose then p.Dispose() else addForCleanup p
                    | _ -> ()

            runs <- runs + 1
            //Thread.Sleep(10)

    let rnd = new System.Random()

    for i in 1 .. 10 do
        let trafo = Trafo3d.Translation(rnd.NextDouble()*10.0,rnd.NextDouble()*10.0,rnd.NextDouble()*10.0)
        addThing trafo

    let threads =
        [
            startThread "cameraThread" cameraMovement

            if textureTest then
                startThread "textureThread" updateTexture

            if addRemoveTest then
                startThread "addRemoveThread" addThings
        ]

    let sg =
        // create a red box with a simple shader
        Sg.box (AVal.constant color) (AVal.constant box)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.simpleLighting
            }
        |> Sg.andAlso (Sg.renderObjectSet (things :> aset<_>))
        |> Sg.diffuseTexture texture
        |> Sg.viewTrafo viewTrafo
        |> Sg.projTrafo projTrafo

    use task =
        let rnd = System.Random()
        RenderTask.ofList [
            if jitterFrames then RenderTask.custom (fun (a,rt,ot,q) -> Thread.Sleep(rnd.Next(0,100))) else RenderTask.empty
            win.Runtime.CompileRender(signature,sg)
        ]

    win.RenderTask <- task
    win.Run()

    running <- false
    threads |> List.iter (fun t -> t.Join())

    for o in preparedObjects do
        o.Dispose()

    for t in preparedTextures do
       win.Runtime.DeleteTexture(t)

    cleanup()

    0
