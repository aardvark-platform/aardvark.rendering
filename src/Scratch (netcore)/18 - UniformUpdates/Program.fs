open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

open System.Collections.Generic
open Aardvark.Application.Slim


module Shader = 


    open FShade
    open Aardvark.Rendering.Effects

    [<ReflectedDefinition>]
    let transformNormal (n : V3d) =
        uniform.ModelViewTrafoInv.Transposed * V4d(n, 0.0)
        |> Vec.xyz
        |> Vec.normalize

    let stableInstanced (v : Vertex) = 
        vertex { 
            let u : M44d = uniform.ModelViewTrafo
            let vp = uniform.ModelViewTrafo * v.pos
            let p = uniform.ProjTrafo * vp
            return { v with pos = p }
        }

// This example illustrates how to render a simple triangle using aardvark.

[<EntryPoint>]
let main argv = 
    
    
    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    //let win =
    //    window {
    //        backend Backend.GL
    //        display Display.Mono
    //        debug false
    //        samples 1
            
    //    }

    use app = new VulkanApplication()
    // SimpleRenderWindow is a System.Windows.Forms.Form which contains a render control
    // of course you can a custum form and add a control to it.
    // Note that there is also a WPF binding for OpenGL. For more complex GUIs however,
    // we recommend using aardvark-media anyways..
    let win = app.CreateGameWindow(samples =1)
    //win.VSync <- false

    // Given eye, target and sky vector we compute our initial camera pose
    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    // the class Frustum describes camera frusta, which can be used to compute a projection matrix.
    let frustum = 
        // the frustum needs to depend on the window size (in oder to get proper aspect ratio)
        win.Sizes 
            // construct a standard perspective frustum (60 degrees horizontal field of view,
            // near plane 0.1, far plane 50.0 and aspect ratio x/y.
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    // create a controlled camera using the window mouse and keyboard input devices
    // the window also provides a so called time mod, which serves as tick signal to create
    // animations - seealso: https://github.com/aardvark-platform/aardvark.docs/wiki/animation
    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red
    let s = 8
    let trafos = List<Trafo3d>()
    for x in -s .. s do
        for y in -s .. s do
            for z in -s .. s do
                trafos.Add(Trafo3d.Translation(float x,float y,float z))

    let trafos = trafos.ToArray()

    let singleSg = Sg.sphere' 2 C4b.White 1.0

    printfn "objs: %A" trafos.Length
    let sgs = 
        trafos |> Seq.map (fun t -> singleSg |> Sg.trafo' t)
                    

    let onOff (b : aval<bool>) (sg : ISg) = b |> AVal.map (function true -> sg | _ -> Sg.empty) |> Sg.dynamic

    let stableTrafo = cval 1

    win.Keyboard.KeyDown(Keys.O).Values.Add(fun _ -> 
        transact (fun _ -> 
            stableTrafo.Value <- (stableTrafo.Value + 1) % 4
            let s = 
                match stableTrafo.Value with
                | 0 -> "no stable"
                | 1 -> "stable"
                | 2 -> "stableinstance"
                | 3 -> "instance"
                | _ -> failwith ""
            printfn "stable: %A" s
        )
    )

    let noStable = 
        Sg.ofSeq sgs
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
        }
        |> onOff (AVal.map (fun v -> v = 0) stableTrafo)

    let stable = 
        Sg.ofSeq sgs
        |> Sg.shader {
            do! DefaultSurfaces.stableTrafo
            do! DefaultSurfaces.vertexColor
        }
        |> onOff (AVal.map (fun v -> v = 1) stableTrafo)

    let mvs = 
        cameraView |> AVal.map (fun v ->
            trafos |> Array.map (fun t -> 
                t * v.ViewTrafo
            )
        )

    let mvF = mvs |> AVal.map (fun vs -> vs |> Array.map (fun (v : Trafo3d) -> M44f v.Forward) :> System.Array)
    let mvB = mvs |> AVal.map (fun vs -> vs |> Array.map (fun (v : Trafo3d) -> M44f v.Backward) :> System.Array)

    let stableInstanced = 
        let instancedUniforms =
            Map.ofList [
                "ModelViewTrafo",    (typeof<M44f>,   mvF)
                "ModelViewTrafoInv", (typeof<M44f>,   mvB)
            ]

        Sg.instanced' instancedUniforms singleSg
        |> Sg.shader {
            do! Shader.stableInstanced
        }
        |> onOff (stableTrafo |> AVal.map (fun v -> v = 2))

    let instanced = 
        Sg.instanced (AVal.constant trafos) singleSg
        |> Sg.shader {
            do! DefaultSurfaces.instanceTrafo
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
        }
        |> onOff (stableTrafo |> AVal.map (fun v -> v = 3))
        
    
    // show the window
    let sg = 
        Sg.ofSeq [noStable; stable; instanced; stableInstanced]
        |> Sg.viewTrafo (cameraView |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
    win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, sg) 
    win.Run()

    0
