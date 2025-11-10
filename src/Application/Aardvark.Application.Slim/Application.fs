namespace Aardvark.Application.Slim

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.Glfw
open System
open System.Runtime.InteropServices

/// Base class for GLFW-based applications.
[<AbstractClass>]
type Application(runtime : IRuntime, interop : IWindowInterop, hideCocoaMenuBar : bool) =
    let glfw = new Instance(runtime, interop, hideCocoaMenuBar)

    member x.Instance = glfw

    abstract member Destroy : unit -> unit

    abstract member CreateGameWindow : WindowConfig -> Window
    default x.CreateGameWindow(config : WindowConfig) =
        let w = glfw.CreateWindow(config)

        w.KeyDown.Add (fun e ->
            match e.Key with
            | Keys.R when e.Ctrl && e.Shift ->
                w.RenderAsFastAsPossible <- not w.RenderAsFastAsPossible
                Log.line "[Window] RenderAsFastAsPossible: %A" w.RenderAsFastAsPossible
            | Keys.V when e.Ctrl && e.Shift ->
                w.VSync <- not w.VSync
                Log.line "[Window] VSync: %A" w.VSync
            | Keys.G when e.Ctrl && e.Shift ->
                w.MeasureGpuTime <- not w.MeasureGpuTime
                Log.line "[Window] MeasureGpuTime: %A" w.MeasureGpuTime
            | Keys.Enter when e.Ctrl && e.Shift ->
                w.Fullcreen <- not w.Fullcreen
                Log.line "[Window] Fullcreen: %A" w.Fullcreen
            | _ ->
                ()
        )

        w

    member x.CreateGameWindow([<Optional; DefaultParameterValue(1)>] samples : int,
                              [<Optional; DefaultParameterValue(false)>] physicalSize : bool,
                              [<Optional; DefaultParameterValue(true)>] vsync : bool) =
        x.CreateGameWindow {
            WindowConfig.Default with
                samples      = samples
                physicalSize = physicalSize
                vsync        = vsync
        }

    member x.CreateGameWindow(width : int, height : int,
                              [<Optional; DefaultParameterValue(1)>] samples : int,
                              [<Optional; DefaultParameterValue(false)>] physicalSize : bool,
                              [<Optional; DefaultParameterValue(true)>] vsync : bool) =
        x.CreateGameWindow {
            WindowConfig.Default with
                width        = width
                height       = height
                samples      = samples
                physicalSize = physicalSize
                vsync        = vsync
        }

    member x.CreateGameWindow(size : V2i,
                              [<Optional; DefaultParameterValue(1)>] samples : int,
                              [<Optional; DefaultParameterValue(false)>] physicalSize : bool,
                              [<Optional; DefaultParameterValue(true)>] vsync : bool) =
        x.CreateGameWindow(size.X, size.Y, samples, physicalSize, vsync)

    member x.Dispose() =
        x.Destroy()
        glfw.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()