module VulkanTests


open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Incremental
open System.Diagnostics
open System.Collections.Generic
open Aardvark.Base.Runtime
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

#nowarn "9"
#nowarn "51"

let run() =
    let app = new VulkanApplication(true)
    let runtime = app.Runtime
    let win = app.CreateSimpleRenderWindow(1)

    let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    let perspective = win.Sizes  |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))
    let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

    let box = Box3d(-V3d.Half, V3d.Half)

    let simpleThings =
        Sg.ofList [
            let cnt = 10
            let size = 1.5 * float cnt
            let off = -size / 2.0
            let rand = RandomSystem()

            for x in 0 .. cnt - 1 do
                for y in 0 .. cnt - 1 do
                    let p = V2d(off, off) + V2d(x,y) * 1.5

                    let prim =
                        match rand.UniformInt(3) with
                            | 0 -> Sg.wireBox' C4b.Red box
                            | 1 -> Sg.sphere' 5 C4b.Green 0.5
                            | _ -> Sg.box' C4b.Blue box

                    yield prim |> Sg.translate p.X p.Y 0.0
        ]

    let sg = 
        simpleThings
            |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
            }

    let objects = sg.RenderObjects() |> ASet.toList

    let task = new RenderTaskNew.RenderTask(runtime.Device, unbox win.FramebufferSignature, false, false)
    
    let tokens = HashSet()

    for o in objects do
        task.Add(o) |> tokens.Add |> ignore

    win.Keyboard.KeyDown(Keys.X).Values.Add (fun _ ->
        if tokens.Count > 0 then
            let t = tokens |> Seq.head
            tokens.Remove t |> ignore
            transact (fun () -> task.Remove t)
    )

    win.RenderTask <- task

    win.Run()

