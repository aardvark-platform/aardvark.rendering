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

    let sg = 
        Sg.wireBox' C4b.Red Box3d.Unit
            |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
            }

    let objects = sg.RenderObjects() |> ASet.toList


    
    let task = new RenderTaskNew.RenderTask(runtime.Device, unbox win.FramebufferSignature, false, false)
    
    for o in objects do
        task.Add(o) |> ignore

    win.RenderTask <- task

    win.Run()

