namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL

type ContextErrorEventArgs(msg : string) =
    inherit EventArgs()

    let mutable retry = false
    member x.Retry 
        with get() = retry
        and set (value : bool) = retry <- value

    member x.Message = msg

type ContextErrorEventHandler =
    delegate of obj * ContextErrorEventArgs -> unit


[<AutoOpen>]
module ContextHandleGLExtensions =
    type GL with
        static member SetDefaultStates() =
            GL.Enable(EnableCap.TextureCubeMapSeamless)
            GL.Check "cannot enable GL_TEXTURE_CUBE_MAP_SEAMLESS"

            // Note: This is supposed to be deprecated since OpenGL 3.2 and enabled by default.
            // However, for some AMD drivers you still need to enable it even though it should not exist anymore.
            GL.Enable(EnableCap.PointSprite)
            GL.GetError() |> ignore

            GL.Disable(EnableCap.PolygonSmooth)
            GL.Check "cannot disable GL_POLYGON_SMOOTH"

            GL.Hint(HintTarget.FragmentShaderDerivativeHint, HintMode.Nicest)
            GL.Check "cannot set GL_FRAGMENT_SHADER_DERIVATIVE_HINT to GL_NICEST"

            if RuntimeConfig.DepthRange = DepthRange.ZeroToOne then
                if GL.ARB_clip_control then
                    GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.ZeroToOne)
                    GL.Check "failed to set depth range to [0, 1]"
                else
                    failf "cannot set depth range to [0, 1] without GL_ARB_clip_control or OpenGL 4.5"

/// <summary>
/// a handle represents a GL context which can be made current and released
/// for one thread at a time.
/// </summary>
[<AllowNullLiteral>]
type ContextHandle(handle : IGraphicsContext, window : IWindowInfo) =
    static let current = new ThreadLocal<ValueOption<ContextHandle>>(fun () -> ValueOption.None)
    static let contextError = new Event<ContextErrorEventHandler, ContextErrorEventArgs>()

    let l = obj()
    let onDisposed = Event<unit>()
    let mutable isDisposed = false
    let mutable debugOutput = None
    let mutable onMakeCurrent : ConcurrentHashSet<unit -> unit> = null
    let mutable driverInfo = None

    static member Current
        with get() =
            let curr = current.Value
            match current.Value with
            | ValueSome ctx when ctx.IsCurrent -> curr
            | _ -> ValueNone

        and private set v = current.Value <- v

    [<CLIEvent>]
    static member ContextError = contextError.Publish

    [<CLIEvent>]
    member x.OnDisposed = onDisposed.Publish

    member x.GetProcAddress(name : string) =
        (handle |> unbox<IGraphicsContextInternal>).GetAddress(name)

    member x.OnMakeCurrent(f : unit -> unit) =
        if isDisposed then failwith "Failed to register OnMakeCurrent callback, the context is already disposed!"
        Interlocked.CompareExchange(&onMakeCurrent, ConcurrentHashSet(), null) |> ignore
        onMakeCurrent.Add f |> ignore

    member x.Lock = l

    member x.WindowInfo = window

    member x.Handle = handle

    member x.Driver =
        match driverInfo with
        | None ->
            let v = Driver.readInfo()
            driverInfo <- Some v
            v
        | Some v -> v

    member x.IsDisposed = isDisposed

    member x.IsCurrent =
        handle.IsCurrent

    member x.MakeCurrent() =
        Monitor.Enter l

        if isDisposed then
            failf' (fun msg -> ObjectDisposedException(null, msg)) "cannot use disposed ContextHandle"

        match ContextHandle.Current with
        | ValueSome handle -> handle.ReleaseCurrent()
        | _ -> ()

        let mutable retry = true
        while retry do
            try
                handle.MakeCurrent(window) // wglMakeCurrent
                retry <- false
            with
            | :? OpenTK.Graphics.GraphicsContextException as ex ->
                Log.line "context error triggered"
                let args = ContextErrorEventArgs(ex.Message)
                contextError.Trigger(x, args)
                retry <- args.Retry
                if retry then
                    Log.line "application requested retry"
                    Thread.Sleep 100
                else
                    reraise()

        ContextHandle.Current <- ValueSome x

        GLVM.hglCleanup((unbox<IGraphicsContextInternal> handle).Context.Handle)
        let actions = Interlocked.Exchange(&onMakeCurrent, null)
        if notNull actions then
            for a in actions do
                a()

    member x.ReleaseCurrent() =
        if handle.IsCurrent then
            handle.MakeCurrent(null)
        else
            Log.warn "cannot release context which is not current"
        ContextHandle.Current <- ValueNone

        Monitor.Exit l

    member x.Use (action : unit -> 'a) =
        match ContextHandle.Current with
        | ValueSome h ->
            if h = x then 
                action()
            else
                try
                    h.ReleaseCurrent()
                    x.MakeCurrent()
                    action()
                finally
                    x.ReleaseCurrent()
                    h.MakeCurrent()
        | ValueNone ->
            try
                x.MakeCurrent()
                action()
            finally
                x.ReleaseCurrent()

    /// Sets default API states and initializes the debug output if required.
    member x.Initialize (debug : IDebugConfig, [<Optional; DefaultParameterValue(true)>] setDefaultStates : bool) =
        let debug = DebugConfig.unbox debug

        x.Use (fun _ ->  
            if setDefaultStates then
                GL.SetDefaultStates()

            debugOutput <-
                if debug.DebugOutput.IsNone && not debug.DebugLabels then None
                else
                    match DebugOutput.TryInitialize() with
                    | Some dbg ->
                        debug.DebugOutput |> Option.iter (fun cfg ->
                            dbg.Enable(cfg.Synchronous, cfg.Verbosity)
                            dbg.Print(DebugType.DebugTypeOther, DebugSeverity.DebugSeverityLow, 1234, "Debug output enabled")
                        )

                        Some dbg

                    | _ ->
                        Log.warn "[GL] Failed to initialize debug output"
                        None
        )

    /// Returns all errors reported by the debug output.
    member x.GetDebugErrors() =
        match debugOutput with
        | Some dbg -> dbg.GetErrors()
        | _ -> [||]

    member x.PrintDebug(typ: DebugType, severity: DebugSeverity, id: int, message: string) =
        match debugOutput with
        | Some dbg -> dbg.Print(typ, severity, id, message)
        | _ -> ()

    member x.PushDebugGroup(message: string) =
        match debugOutput with
        | Some dbg -> dbg.PushGroup(message)
        | _ -> ()

    member x.PopDebugGroup() =
        match debugOutput with
        | Some dbg -> dbg.PopGroup()
        | _ -> ()

    member _.SetObjectLabel(id: ObjectLabelIdentifier, name: int, label: string) =
        match debugOutput with
        | Some dbg -> dbg.SetObjectLabel(id, name, label)
        | _ -> ()

    member _.GetObjectLabel(id: ObjectLabelIdentifier, name: int) =
        match debugOutput with
        | Some dbg -> dbg.GetObjectLabel(id, name)
        | _ -> null

    member x.Dispose() =
        let mutable lockTaken = false

        try
            Monitor.TryEnter(l, TimeSpan.FromSeconds 1.0, &lockTaken)

            if lockTaken then
                if not isDisposed then

                    // release potentially pending UnsharedObjects
                    x.Use(fun () -> 
                        GLVM.hglCleanup((unbox<IGraphicsContextInternal> handle).Context.Handle)
                    
                        let actions = Interlocked.Exchange(&onMakeCurrent, null)
                        if notNull actions then
                                for a in actions do
                                    a()            
                            )
                    
                    isDisposed <- true
                    onDisposed.Trigger()
            else
                Log.warn "[GL] ContextHandle.Dispose() timed out"
        finally
            if lockTaken then Monitor.Exit l

    interface IDisposable with
        member x.Dispose() = x.Dispose()

/// <summary>
/// A module for managing context handles
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ContextHandle =

    /// <summary>
    /// deletes the given context also destroying its associated window-info
    /// </summary>
    let delete(ctx : ContextHandle) =
        if ctx.IsCurrent then
            ctx.ReleaseCurrent()
        ctx.Dispose()

    /// <summary>
    /// checks whether the given context is current on the calling thread
    /// </summary>
    let isCurrent (ctx : ContextHandle) = ctx.IsCurrent

    /// <summary>
    /// makes the given context current on the calling thread.
    /// releases any context being current before doing so.
    /// </summary>
    let makeCurrent (ctx : ContextHandle) = ctx.MakeCurrent()

    /// <summary>
    /// releases the given context from the calling thread
    /// </summary>
    let releaseCurrent (ctx : ContextHandle) = ctx.ReleaseCurrent()

module ContextHandleOpenTK =

    // This is a workaround for closing the X11 display, since OpenTK leaks the display even when the
    // window is disposed. This leads to problems when running multiple unit tests one after another.
    // We currently use a custom version of OpenTK in which this issue isn't fixed yet.
    // https://github.com/krauthaufen/OpenTKHack
    // See: https://github.com/opentk/opentk/pull/773
    module private X11 =
        open OpenTK.Platform.X11
        open System.Reflection

        [<AutoOpen>]
        module private Internals =
            let asm = typeof<NativeWindow>.Assembly

            let fiImplementation =
                typeof<NativeWindow>.GetField("implementation", BindingFlags.NonPublic ||| BindingFlags.Instance)

            let fiWindow =
                let t = asm.GetType("OpenTK.Platform.X11.X11GLNative")
                if isNull t then null
                else t.GetField("window", BindingFlags.NonPublic ||| BindingFlags.Instance)

            let fCloseDisplay =
                let t = asm.GetType("OpenTK.Platform.X11.Functions")
                if isNull t then null
                else t.GetMethod("XCloseDisplay")

        let closeDisplay (window : NativeWindow) =
            if RuntimeInformation.IsOSPlatform OSPlatform.Linux then
                try
                    let x11GLNative = fiImplementation.GetValue(window)
                    let window = unbox<X11WindowInfo> <| fiWindow.GetValue(x11GLNative)

                    if window.Display <> 0n then
                        fCloseDisplay.Invoke(null, [| window.Display |]) |> ignore
                with exn ->
                    Log.warn "[GL] Failed to close X11 display: %A" exn.Message
            else
                ()

    /// <summary>
    /// Creates a new context using the default configuration.
    /// The given context is used as parent for sharing. If parent is None, OpenTK chooses a context to use as parent.
    /// </summary>
    let createWithParent (debug : IDebugConfig) (parent : ContextHandle option) =
        let window, context =
            let prev = ContextHandle.Current

            let mode = Graphics.GraphicsMode(ColorFormat(Config.BitsPerPixel), Config.DepthBits, Config.StencilBits, 1, ColorFormat.Empty, Config.Buffers, false)
            let window = new NativeWindow(16, 16, "background", GameWindowFlags.Default, mode, DisplayDevice.Default)

            let context =
                match parent with
                | Some p ->
                    new GraphicsContext(GraphicsMode.Default, window.WindowInfo, p.Handle, Config.MajorVersion, Config.MinorVersion, Config.ContextFlags);
                | _ ->
                    new GraphicsContext(GraphicsMode.Default, window.WindowInfo, Config.MajorVersion, Config.MinorVersion, Config.ContextFlags);

            context.MakeCurrent(window.WindowInfo)
            let ctx = context |> unbox<IGraphicsContextInternal>
            ctx.LoadAll()

            GL.SetDefaultStates()

            // unbind created context and optionally restore previous
            match prev with 
            | ValueSome old -> old.Handle.MakeCurrent(old.WindowInfo)
            | ValueNone -> context.MakeCurrent(null)

            window, context

        let dispose() =
            context.Dispose()
            window.Dispose()
            X11.closeDisplay window

        let handle = new ContextHandle(context, window.WindowInfo)
        handle.OnDisposed.Add dispose
        handle.Initialize(debug, setDefaultStates = false)

        handle

    /// <summary>
    /// Creates a new context using the default configuration.
    /// </summary>
    let create (debug : IDebugConfig) =
        createWithParent debug None