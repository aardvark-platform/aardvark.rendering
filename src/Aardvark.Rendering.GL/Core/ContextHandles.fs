namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
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

/// <summary>
/// a handle represents a GL context which can be made current and released
/// for one thread at a time.
/// </summary>
[<AllowNullLiteral>]
type ContextHandle(handle : IGraphicsContext, window : IWindowInfo) =
    static let current = new ThreadLocal<ValueOption<ContextHandle>>(fun () -> ValueOption.None)
    static let contextError = new Event<ContextErrorEventHandler, ContextErrorEventArgs>()

    let l = obj()
    let mutable debugCallbackInstalled = false
    let mutable onMakeCurrent : ConcurrentHashSet<unit -> unit> = null
    let mutable debugOutputEnabled = false
    let mutable driverInfo = None

    static member Current
        with get() = 
            let curr = current.Value
            match current.Value with
                | ValueSome ctx when ctx.IsCurrent -> curr
                | _ -> ValueNone

        and set v = current.Value <- v
        
    [<CLIEvent>]
    static member ContextError = contextError.Publish

    member x.GetProcAddress(name : string) =
        (handle |> unbox<IGraphicsContextInternal>).GetAddress(name)

    member x.OnMakeCurrent(f : unit -> unit) =
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

    member x.IsCurrent =
        handle.IsCurrent

    member x.MakeCurrent() =
        Monitor.Enter l

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
        if actions <> null then
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



    // Installs debug callback if not yet installed (context is assumed to be current)
    member x.AttachDebugOutputIfNeeded(enable : bool) =
        if debugCallbackInstalled then ()
        else
            debugCallbackInstalled <- true
            x.Use (fun () ->
                if enable then
                    let works = GL.SetupDebugOutput()
                    if works then 
                        GL.Enable(EnableCap.DebugOutput)
                        // GL.Enable(EnableCap.DebugOutputSynchronous)
                    
                        let str = "debug output enabled"
                        GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, DebugType.DebugTypeOther, 1234, DebugSeverity.DebugSeverityLow, str.Length, str)
            )

    member x.AttachDebugOutputIfNeeded() =
        x.AttachDebugOutputIfNeeded true

    member x.DebugOutputEnabled
        with get() = debugOutputEnabled
        and set (value : bool) =
            if value <> debugOutputEnabled then
                if value then
                    x.AttachDebugOutputIfNeeded()
                    GL.Enable(EnableCap.DebugOutput)
                else
                    GL.Disable(EnableCap.DebugOutput)
                debugOutputEnabled <- value


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

        //match windows.TryRemove ctx with
        //    | (true, w) -> w.Dispose()
        //    | _ -> ()

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

    /// Initialize global GL config state
    let initGlConfig() =
        GL.Hint(HintTarget.PointSmoothHint, HintMode.Fastest)
        GL.Enable(EnableCap.TextureCubeMapSeamless)
        GL.Disable(EnableCap.PolygonSmooth)
        GL.Hint(HintTarget.FragmentShaderDerivativeHint, HintMode.Nicest)
        if Config.DepthRange = DepthRange.ZeroToOne then
            GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.ZeroToOne)



module ContextHandleOpenTK =
    
    let private windows = System.Collections.Concurrent.ConcurrentDictionary<ContextHandle, NativeWindow>()

    /// <summary>
    /// creates a new context using the default configuration
    /// </summary>
    let create (enableDebug : bool) =
        let window, context =
            let prev = ContextHandle.Current

            let mode = Graphics.GraphicsMode(ColorFormat(Config.BitsPerPixel), Config.DepthBits, Config.StencilBits, 1, ColorFormat.Empty, Config.Buffers, false)
            let window = new NativeWindow(16, 16, "background", GameWindowFlags.Default, mode, DisplayDevice.Default)
            let context = new GraphicsContext(GraphicsMode.Default, window.WindowInfo, Config.MajorVersion, Config.MinorVersion, Config.ContextFlags);
            context.MakeCurrent(window.WindowInfo)
            let ctx = context |> unbox<IGraphicsContextInternal>
            ctx.LoadAll()

            ContextHandle.initGlConfig()

            // unbind created context and optionally restore previous
            match prev with 
            | ValueSome old -> old.Handle.MakeCurrent(old.WindowInfo)
            | ValueNone -> context.MakeCurrent(null)

            window, context
    
        
        let handle = new ContextHandle(context, window.WindowInfo)

        if enableDebug then
            handle.AttachDebugOutputIfNeeded(true)

        // add the window to the windows-table to save it from being
        // garbage collected.
        if not <| windows.TryAdd(handle, window) then failwith "failed to add new context to live-set"
    
    
        handle


