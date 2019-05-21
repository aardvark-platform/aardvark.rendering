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
    static let current = new ThreadLocal<Option<ContextHandle>>(fun () -> None)
    static let contextError = new Event<ContextErrorEventHandler, ContextErrorEventArgs>()

    let l = obj()
    let mutable debugCallbackInstalled = false
    let mutable onMakeCurrent : ConcurrentHashSet<unit -> unit> = null
    let mutable debugOutputEnabled = false
    let mutable driverInfo = None

    static member Current
        with get() = 
            match current.Value with
                | Some ctx when ctx.IsCurrent -> Some ctx
                | _ -> None

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
        match ContextHandle.Current with
            | Some handle -> handle.ReleaseCurrent()
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

        ContextHandle.Current <- Some x

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
        ContextHandle.Current <- None

    member x.Use (action : unit -> 'a) =
        match ContextHandle.Current with
            | Some h ->
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
            | None ->
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
        x.AttachDebugOutputIfNeeded false

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
    
    let private windows = ConcurrentDictionary<ContextHandle, NativeWindow>()

    let mutable primaryContext : ContextHandle = null

    /// <summary>
    /// creates a new context using the default configuration
    /// </summary>
    let create (enableDebug : bool) =
        let window, context =
            if not (isNull primaryContext) then 
                primaryContext.MakeCurrent()
            
            let mode = Graphics.GraphicsMode(ColorFormat(Config.BitsPerPixel), Config.DepthBits, Config.StencilBits, 1, ColorFormat.Empty, Config.Buffers, false)
            let window = new NativeWindow(16, 16, "background", GameWindowFlags.Default, mode, DisplayDevice.Default)
            let context = new GraphicsContext(GraphicsMode.Default, window.WindowInfo, Config.MajorVersion, Config.MinorVersion, Config.ContextFlags);
            context.MakeCurrent(window.WindowInfo)
            let ctx = context |> unbox<IGraphicsContextInternal>
            ctx.LoadAll()

            GL.Hint(HintTarget.PointSmoothHint, HintMode.Fastest)
            GL.Enable(EnableCap.TextureCubeMapSeamless)
            GL.Disable(EnableCap.PolygonSmooth)
            GL.Hint(HintTarget.FragmentShaderDerivativeHint, HintMode.Nicest)

            context.MakeCurrent(null)
            window, context
    
        
        let handle = new ContextHandle(context, window.WindowInfo)

        if enableDebug then
            handle.AttachDebugOutputIfNeeded(true)

        if isNull primaryContext then
            primaryContext <- handle

        // add the window to the windows-table to save it from being
        // garbage collected.
        if not <| windows.TryAdd(handle, window) then failwith "failed to add new context to live-set"
    
    
        handle

    let createContexts enableDebug resourceContextCount  =
        // if there is a current context release it before creating
        // the GameWindow since the GameWindow makes itself current
        GraphicsContext.ShareContexts <- true;

        let current = ContextHandle.Current
        match current with
            | Some handle -> handle.ReleaseCurrent()
            | None -> ()

        let contexts =
            [ for i in 1..resourceContextCount do
                yield create (enableDebug)
            ]

        // make the old context current again
        match current with
            | Some handle -> handle.MakeCurrent()
            | None -> ()

        contexts

    /// <summary>
    /// deletes the given context also destroying its associated window-info
    /// </summary>
    let delete(ctx : ContextHandle) =
        if ctx.IsCurrent then
            ctx.ReleaseCurrent()

        match windows.TryRemove ctx with
            | (true, w) -> w.Dispose()
            | _ -> ()

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



