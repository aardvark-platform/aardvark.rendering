namespace Aardvark.Rendering.ImGui

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open System
open System.IO
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Text
open FSharp.Data.Adaptive
open Hexa.NET.ImGui
open Hexa.NET.ImGui.Backends.GLFW

#nowarn "9"

type Instance internal (control: IRenderControl) =
    static let mutable isInitialized = 0
    let mutable isDisposed = 0
    let mutable pConfigFile = NativePtr.zero<uint8>

    let window =
        match control with
        | :? Aardvark.Glfw.Window as window -> window
        | _ -> raise <| NotSupportedException($"Render control needs to be a GLFW window.")

    do if Interlocked.Exchange(&isInitialized, 1) <> 0 then
        raise <| InvalidOperationException($"Cannot create multiple ImGui instances.")

    let context = ImGui.CreateContext()

    let mutable io =
        let io = ImGui.GetIO()
        &io.ConfigFlags |||= ImGuiConfigFlags.NavEnableKeyboard
        &io.ConfigFlags |||= ImGuiConfigFlags.DockingEnable
        &io.BackendFlags |||= ImGuiBackendFlags.RendererHasVtxOffset
        &io.BackendFlags |||= ImGuiBackendFlags.RendererHasTextures
        io

    do
        ImGuiImplGLFW.SetCurrentContext context
        if not <| ImGuiImplGLFW.InitForOther(window.HandlePtr, true) then
            ImGui.DestroyContext context
            failwith "[ImGui] Failed to initialize GLFW backend."

    let mutable render : Action<unit> = ignore
    let state = new RenderState(window.Runtime)

    let transaction = new Transaction()

    let beforeRenderCb =
        window.BeforeRender.Subscribe(fun _ ->
            useTransaction transaction (fun _ ->
                ImGuiImplGLFW.NewFrame()
                ImGui.NewFrame()
                render.Invoke()
                ImGui.Render()

                let data = ImGui.GetDrawData()
                state.Update data
            )

            transaction.Commit()
            transaction.Dispose()
        )

    let afterRenderCb =
        window.AfterRender.Subscribe(fun _ ->
            window.RenderAsFastAsPossible <- true
            window.DispatchKeyboardEvents <- not io.WantCaptureKeyboard
            window.DispatchMouseEvents    <- not io.WantCaptureMouse
        )

    member internal _.Scene = state.Scene

    /// The render method describing the UI.
    /// This method is called every frame (if not null), dispatching ImGui API calls to update the draw data of the underlying scene graph.
    member _.Render
        with get()    = render
        and set value = render <- if isNull value then ignore else value

    /// The path to the file that is used to automatically save and load the state of the UI.
    /// Null if automatic saving and loading of settings is disabled.
    member _.ConfigFile
        with get() = String.ofPtrUtf8 <| io.IniFilename
        and set value =
            NativePtr.free pConfigFile
            pConfigFile <- String.toPtrUtf8 value
            io.IniFilename <- pConfigFile

    /// <summary>
    /// Loads the UI state from a stream.
    /// </summary>
    /// <param name="stream">The stream to load the state from.</param>
    member this.LoadConfig(stream: Stream) =
        use pData = fixed Stream.readAllBytes stream
        ImGui.LoadIniSettingsFromMemory(pData)

    /// <summary>
    /// Loads the UI state from a file.
    /// </summary>
    /// <param name="path">The path to load the state from. If null or empty, <see cref="ConfigFile"/> is used.</param>
    member this.LoadConfig([<Optional; DefaultParameterValue(null : string)>] path: string) =
        let path = if String.IsNullOrEmpty path then this.ConfigFile else path
        use file = File.OpenRead path
        this.LoadConfig(file)

    /// <summary>
    /// Saves the UI state to a stream
    /// </summary>
    /// <param name="stream">The stream to save the state to.</param>
    member this.SaveConfig(stream: Stream) =
        let data = ImGui.SaveIniSettingsToMemoryS()
        use writer = new StreamWriter(stream, Encoding.UTF8, 1024, true)
        writer.Write(data)

    /// <summary>
    /// Saves the UI state to a file.
    /// </summary>
    /// <param name="path">The path to save the state to. If null or empty, <see cref="ConfigFile"/> is used.</param>
    member this.SaveConfig([<Optional; DefaultParameterValue(null : string)>] path: string) =
        let path = if String.IsNullOrEmpty path then this.ConfigFile else path
        use file = File.OpenWrite path
        this.SaveConfig(file)

    member this.Dispose() =
        if Interlocked.Exchange(&isDisposed, 1) = 0 then
            afterRenderCb.Dispose()
            beforeRenderCb.Dispose()
            state.Dispose()
            ImGuiImplGLFW.Shutdown()
            ImGuiImplGLFW.SetCurrentContext ImGuiContextPtr.Null
            ImGui.DestroyContext context
            NativePtr.free pConfigFile
            isInitialized <- 0

    interface IDisposable with
        member this.Dispose() = this.Dispose()

    interface ISg

module internal AgRules =
    open Aardvark.Base.Ag

    [<Rule>]
    type InstanceSemantics() =
        member x.RenderObjects(instance: Instance, scope: Scope) : aset<IRenderObject> =
            instance.Scene?RenderObjects(scope)

        member _.LocalBoundingBox(instance: Instance, scope: Scope) : aval<Box3d> =
            instance.Scene?LocalBoundingBox(scope)

[<AbstractClass; Sealed; Extension>]
type RenderControlExtensions =

    /// <summary>
    /// Initializes ImGui and hooks the instance to the event loop of the render control.
    /// The user-provided render method is called every frame to update the draw data of the UI.
    /// The instance can be inserted into the scene graph to render the UI.
    /// Note that on-demand rendering will be disabled for the given render control.
    /// </summary>
    /// <remarks>
    /// Only supported for GLFW windows.
    /// </remarks>
    /// <param name="control">The render control to hook ImGui into. Must be a GLFW window.</param>
    /// <returns>An ImGui instance that can be inserted into the scene graph.</returns>
    [<Extension>]
    static member InitializeImGui(control: IRenderControl) =
        let inst = new Instance(control)
        inst.ConfigFile <- null
        inst