open System.Threading
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.SceneGraph.Semantics
open Aardvark.Application.Slim

// This example illustrates how to render a simple triangle using aardvark.

[<EntryPoint>]
let main argv = 
    

    
    let prepareIt = true
    let inlineDispose = false
    let perObjTexture = true
    
    let addRemoveTest = false
    let textureTest = true
    
    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    //let win =
    //    window {
    //        backend Backend.GL
    //        display Display.Mono
    //        debug true
    //        samples 1
    //    }


    use app = new VulkanApplication()
    //use app = new OpenGlApplication()
    let win = app.CreateGameWindow(1)

    let signature =
        win.Runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
            DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
        ]

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red


    let angle = cval 0.0

    let cameraMovement () = 
        Thread.Sleep 2000
        while true do
            transact (fun _ -> 
                angle.Value <- angle.Value + 0.01
            )

    let startThread (f : unit -> unit) = 
        let t = Thread(f)
        t.IsBackground <- true
        t.Start()
        t


    let texture = cval (DefaultTextures.blackTex.GetValue())


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
        while true do
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

    let delayedDisposals : ref<list<IRenderObject>> = ref []

    let cleanup () = 
        let d = Interlocked.Exchange(delayedDisposals, [])
        for dx in d do  
           match dx with 
           | :? IPreparedRenderObject as p -> p.Dispose() 
           | _ -> ()

    win.AfterRender.Add(cleanup)

    let addThings () = 
        Thread.Sleep 2000
        let rnd = new System.Random()
        let mutable runs = 0
        while true do
            if runs % 10000 = 0 then Log.line "cnt: %A" things.Count
            if rnd.NextDouble() <= 0.5 then
                let trafo = Trafo3d.Translation(rnd.NextDouble()*10.0,rnd.NextDouble()*10.0,rnd.NextDouble()*10.0)

                let ro = 
                    { template with
                        Id = newId()
                        Uniforms = uniforms trafo (if perObjTexture then AVal.constant (createTexture C4b.Gray) else texture :> aval<_>)
                    } :> IRenderObject
                
                transact (fun _ -> 
                    let prep = if prepareIt then win.Runtime.PrepareRenderObject(signature,ro) :> IRenderObject else ro
                    things.Add prep |> ignore
                )
            elif things.Count > 0 then
                let rndIndx = rnd.Next(0,things.Count-1)
                if rndIndx < things.Count - 1 then
                    let nth = Seq.item rndIndx things
                    
                    transact (fun _ -> 
                        if inlineDispose then
                            match nth with | :? IPreparedRenderObject as p -> p.Dispose() | _ -> ()
                        else
                            delayedDisposals := nth :: !delayedDisposals
                        things.Remove nth |> ignore
                    )
            runs <- runs + 1
            //Thread.Sleep(10)

    let cameraThread = startThread cameraMovement
    let textureThread = 
        if textureTest then startThread updateTexture |> ignore else ()
    let addRemoteThread = 
        if addRemoveTest then startThread addThings |> ignore else ()


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
    
    // show the window
    win.RenderTask <- win.Runtime.CompileRender(signature,sg)
    //win.Scene <- sg
    win.Run()

    0
