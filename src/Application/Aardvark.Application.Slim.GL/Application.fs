namespace Aardvark.Application.Slim

open System.Reflection
open Aardvark.Base
open Silk.NET.GLFW
open Aardvark.Rendering
open Aardvark.Rendering.GL
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Microsoft.FSharp.NativeInterop
open Aardvark.Application
open Aardvark.Glfw

#nowarn "9"

module private OpenGL =
    open System.Runtime.InteropServices
    open FSharp.Data.Adaptive
    open System.Runtime.InteropServices

    let mutable version = System.Version(3,3)
    let initVersion (glfw : Glfw) =
        let defaultVersion = System.Version(Config.MajorVersion, Config.MinorVersion)

        let versions =
            [
                System.Version(4,6)
                System.Version(4,5)
                System.Version(4,4)
                System.Version(4,3)
                System.Version(4,2)
                System.Version(4,1)
                System.Version(4,0)
                System.Version(3,3)
            ]

        let startAt =
            match versions |> List.tryFindIndex ((=) defaultVersion) with
            | Some idx -> idx
            | _ ->
                Log.warn "OpenGL version %A is invalid" defaultVersion
                0

        let best = 
            versions
            |> List.skip startAt
            |> List.tryFind (fun v -> 
                glfw.DefaultWindowHints()
                glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL)
                glfw.WindowHint(WindowHintInt.ContextVersionMajor, v.Major)
                glfw.WindowHint(WindowHintInt.ContextVersionMinor, v.Minor)
                glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, true)
                glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core)
                glfw.WindowHint(WindowHintBool.Visible, false)

                let w = 
                    try glfw.CreateWindow(1, 1, "", NativePtr.zero, NativePtr.zero)
                    with _ -> NativePtr.zero

                if w = NativePtr.zero then
                    Log.warn "OpenGL %A not working" v
                    false
                else
                    glfw.DestroyWindow(w)
                    Log.line "OpenGL %A working" v
                    true

            )
        match best with
        | Some b -> version <- b
        | None -> failwith "no compatible OpenGL version found"

    type MyWindowInfo(win : nativeptr<WindowHandle>) =
        let mutable win = win
        interface IWindowInfo with
            member x.Dispose(): unit = 
                win <- NativePtr.zero

            member x.Handle: nativeint = 
                NativePtr.toNativeInt win
    
    [<AllowNullLiteral>]
    type MyGraphicsContext(glfw : Glfw, win : nativeptr<WindowHandle>) as this =
        //[<System.ThreadStaticAttribute; DefaultValue>]
        //static val mutable private CurrentContext : OpenTK.ContextHandle

        let mutable win = win

        static let addContext = typeof<GraphicsContext>.GetMethod("AddContext", BindingFlags.NonPublic ||| BindingFlags.Static)
        static let remContext = typeof<GraphicsContext>.GetMethod("RemoveContext", BindingFlags.NonPublic ||| BindingFlags.Static)

        let handle = ContextHandle(NativePtr.toNativeInt win)
        static let mutable inited = false
        do 
            if not inited then
                inited <- true
                let get = GraphicsContext.GetCurrentContextDelegate(fun () -> ContextHandle(NativePtr.toNativeInt (glfw.GetCurrentContext())))
                let t = typeof<GraphicsContext>
                let f = t.GetField("GetCurrentContext", BindingFlags.NonPublic ||| BindingFlags.Static)
                f.SetValue(null, get)

        do addContext.Invoke(null, [| this :> obj |]) |> ignore

        member x.LoadAll(): unit = 
            let t = typeof<OpenTK.Graphics.OpenGL4.GL>
            let m = t.GetMethod("LoadEntryPoints", BindingFlags.NonPublic ||| BindingFlags.Instance)
            let gl = OpenTK.Graphics.OpenGL4.GL()
            m.Invoke(gl, null) |> ignore

            let t = typeof<OpenTK.Graphics.OpenGL.GL>
            let m = t.GetMethod("LoadEntryPoints", BindingFlags.NonPublic ||| BindingFlags.Instance)
            let gl = OpenTK.Graphics.OpenGL.GL()
            m.Invoke(gl, null) |> ignore
        
        interface IGraphicsContext with
            member x.Dispose(): unit = 
                remContext.Invoke(null, [| x :> obj |]) |> ignore
                win <- NativePtr.zero
                ()

            member x.ErrorChecking
                with get () = false
                and set _ = ()

            member x.GraphicsMode = 
                GraphicsMode.Default

            member x.IsCurrent =
                glfw.GetCurrentContext() = win
            member x.IsDisposed: bool = 
                win = NativePtr.zero
            member x.LoadAll() = x.LoadAll()
            member x.MakeCurrent(window: IWindowInfo): unit = 
                if isNull window then 
                    if OpenTK.Graphics.GraphicsContext.CurrentContextHandle <> handle then
                        failwith "overriding context"
                    glfw.MakeContextCurrent(NativePtr.zero)
                else 
                    if OpenTK.Graphics.GraphicsContext.CurrentContextHandle.Handle <> 0n then
                        failwith "overriding context"
                    glfw.MakeContextCurrent(win)

            member x.SwapBuffers(): unit = 
                glfw.SwapBuffers(win)
            member x.SwapInterval
                with get() = 0
                and set v = ()
            member x.Update(window: IWindowInfo): unit = 
                ()

        interface IGraphicsContextInternal with
            member x.Context = 
                handle
            member x.GetAddress(name : string): nativeint = 
                glfw.GetProcAddress name
            member x.GetAddress(name: nativeint): nativeint = 
                let str = Marshal.PtrToStringAnsi name
                glfw.GetProcAddress str
            member x.Implementation: IGraphicsContext = 
                x :> _
            member x.LoadAll() = x.LoadAll()
        




    let createSwapchain (runtime : Runtime) (signature : IFramebufferSignature) (ctx : Aardvark.Rendering.GL.ContextHandle) (glfw : Glfw) (win : nativeptr<WindowHandle>) (size : V2i) =
        
        let defaultFramebuffer = 
            new Framebuffer(
                runtime.Context, signature, 
                (fun _ -> 0), 
                ignore, 
                [0, DefaultSemantic.Colors, Aardvark.Rendering.GL.Renderbuffer(runtime.Context, 0, size, TextureFormat.Rgba8, signature.Samples, 0L) :> IFramebufferOutput], None
            ) 

        { new ISwapchain with
            override this.Dispose() = 
                ()
            override this.Run(task : IRenderTask, query : IQuery)  = 
                using (runtime.Context.RenderingLock ctx) (fun _ ->
                    let output = OutputDescription.ofFramebuffer defaultFramebuffer

                    GL.ColorMask(true, true, true, true)
                    GL.DepthMask(true)
                    GL.Viewport(0, 0, size.X, size.Y)
                    GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                    GL.ClearDepth(1.0)
                    GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)

                    let rt = RenderToken.Empty |> RenderToken.withQuery query
                    task.Run(AdaptiveToken.Top, rt, output)

                    glfw.SwapBuffers(win)
                )
            override this.Size = 
                size
        }

    let mutable private lastWindow = None

    let createSurface (runtime : Runtime) (cfg: WindowConfig) (glfw : Glfw) (win : nativeptr<WindowHandle>) =
        let old = glfw.GetCurrentContext()
        
        glfw.MakeContextCurrent(NativePtr.zero)
        if Option.isNone lastWindow then lastWindow <- Some win

        let ctx =
            new MyGraphicsContext(glfw, win)

        let info =
            new MyWindowInfo(win)

        if not (isNull ctx) then  
            glfw.MakeContextCurrent(win)
            let current = glfw.GetCurrentContext() 
            let mutable desc = Unchecked.defaultof<_>
            glfw.GetError(&desc) |> Log.warn "%A"
            Log.warn "worked: %A" (current = win)
            ctx.LoadAll()          
            glfw.SwapInterval(if cfg.vsync then 1 else 0)

            glfw.MakeContextCurrent(NativePtr.zero)

        if old <> NativePtr.zero then
            glfw.MakeContextCurrent old
        
        let signature =
            runtime.CreateFramebufferSignature([
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ], cfg.samples)
        let handle = new Aardvark.Rendering.GL.ContextHandle(ctx, info)

        { new IWindowSurface with
            override x.Signature = signature
            override this.Handle =
                handle :> obj
            override this.CreateSwapchain(size: V2i) = 
                createSwapchain runtime signature handle glfw win size
            override this.Dispose() = 
                
                ()
        }

    let interop =
        { new IWindowInterop with
            override __.Boot(glfw) =
                initVersion glfw

            override __.CreateSurface(runtime : IRuntime, cfg: WindowConfig, glfw: Glfw, win: nativeptr<WindowHandle>) = 
                createSurface (runtime :?> _) cfg glfw win

            override __.WindowHints(cfg: WindowConfig, glfw: Glfw) = 
                glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL)
                glfw.WindowHint(WindowHintInt.ContextVersionMajor, version.Major)
                glfw.WindowHint(WindowHintInt.ContextVersionMinor, version.Minor)
                glfw.WindowHint(WindowHintInt.DepthBits, 24)
                glfw.WindowHint(WindowHintInt.StencilBits, 8)


                let m = glfw.GetPrimaryMonitor()
                let mode = glfw.GetVideoMode(m) |> NativePtr.read
                glfw.WindowHint(WindowHintInt.RedBits, 8)
                glfw.WindowHint(WindowHintInt.GreenBits, 8)
                glfw.WindowHint(WindowHintInt.BlueBits, 8)
                glfw.WindowHint(WindowHintInt.AlphaBits, 8)
                glfw.WindowHint(WindowHintInt.RefreshRate, mode.RefreshRate)
                glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core)
                glfw.WindowHint(WindowHintRobustness.ContextRobustness, Robustness.LoseContextOnReset)
                glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, true)
                glfw.WindowHint(WindowHintBool.DoubleBuffer, true)
                glfw.WindowHint(WindowHintBool.OpenGLDebugContext, false)
                glfw.WindowHint(WindowHintBool.ContextNoError, true)
                glfw.WindowHint(WindowHintBool.SrgbCapable, false)
                if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                    glfw.WindowHint(unbox<WindowHintBool> 0x00023001, cfg.physicalSize)

                glfw.WindowHint(unbox<WindowHintBool> 0x0002200C, true)
                glfw.WindowHint(WindowHintInt.Samples, cfg.samples)
        }

type OpenGlApplication(forceNvidia : bool, debug : DebugLevel, shaderCachePath : Option<string>) =
    do if forceNvidia then Aardvark.Base.DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore
       // hs, 01-02.2021, this should NOT be necessary in slim. 
       //OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore
       
    let runtime = new Runtime(debug)
    let glfw = Application(runtime, OpenGL.interop, false)
    
    let windowConfig =
        {
            WindowConfig.title = "Aardvark rocks \\o/"
            WindowConfig.width = 1024
            WindowConfig.height = 768
            WindowConfig.resizable = true
            WindowConfig.focus = true
            WindowConfig.vsync = true
            WindowConfig.opengl = true
            WindowConfig.physicalSize = false
            WindowConfig.transparent = false
            WindowConfig.samples = 1
        }
        
    let createContext() =
        let w = glfw.CreateWindow windowConfig
        let h = w.Surface.Handle :?> Aardvark.Rendering.GL.ContextHandle
        h.Initialize(debug)
        glfw.RemoveExistingWindow w
        h

    let ctx = new Context(runtime, fun () -> glfw.Invoke createContext)

    do ctx.ShaderCachePath <- shaderCachePath
       runtime.Initialize(ctx)
 
    new(forceNvidia, debug, shaderCachePath) =
        new OpenGlApplication(forceNvidia, DebugLevel.ofBool debug, shaderCachePath)

    new(forceNvidia, debug : DebugLevel) = new OpenGlApplication(forceNvidia, debug, Context.DefaultShaderCachePath)
    new(forceNvidia, debug : bool) = new OpenGlApplication(forceNvidia, DebugLevel.ofBool debug, Context.DefaultShaderCachePath)

    new(debug : DebugLevel) = new OpenGlApplication(true, debug)
    new(debug : bool) = new OpenGlApplication(true, DebugLevel.ofBool debug)

    new() = new OpenGlApplication(true, DebugLevel.None)

    member x.Context = ctx
    member x.Runtime = runtime

    member x.Dispose() =
        // first dispose runtime in order to properly dispose resources..
        runtime.Dispose()
        ctx.Dispose()

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        failwithf "unknown control type: %A" ctrl
        

    member x.CreateGameWindow(?samples : int) =
        let samples = defaultArg samples 1
        let w = glfw.CreateWindow { windowConfig with samples = samples }

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

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()