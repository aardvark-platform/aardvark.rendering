namespace Aardvark.Application.Slim

open System.Reflection
open System.Runtime.InteropServices
open Aardvark.Base
open Silk.NET.GLFW
open Aardvark.Rendering
open Aardvark.Rendering.GL
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open Aardvark.Application
open Aardvark.Glfw

#nowarn "9"

[<AutoOpen>]
module private WindowHintExtensions =
    
    type WindowHintBool with
        static member CocoaRetinaFramebuffer = unbox<WindowHintBool> 0x00023001
        static member ScaleToMonitor = unbox<WindowHintBool> 0x0002200C

module private OpenGL =
    open System
    open FSharp.Data.Adaptive

    let mutable version = System.Version(3,3)
    let mutable supportsNoError = false

    let private tryCreateOffscreenWindow (version : Version) (useNoError : bool) (glfw : Glfw) =
        glfw.DefaultWindowHints()
        glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL)
        glfw.WindowHint(WindowHintInt.ContextVersionMajor, version.Major)
        glfw.WindowHint(WindowHintInt.ContextVersionMinor, version.Minor)
        glfw.WindowHint(WindowHintBool.ContextNoError, useNoError)
        glfw.WindowHint(WindowHintRobustness.ContextRobustness, Robustness.LoseContextOnReset)
        glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, true)
        glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core)
        glfw.WindowHint(WindowHintBool.Visible, false)

        let w =
            try glfw.CreateWindow(1, 1, "", NativePtr.zero, NativePtr.zero)
            with _ -> NativePtr.zero

        if w = NativePtr.zero then
            false
        else
            glfw.DestroyWindow w
            true

    let queryNoErrorSupport (version : Version) (glfw : Glfw) =
        if tryCreateOffscreenWindow version true glfw then
            true
        else
            let error, _ = glfw.GetError()
            Report.Line(2, $"OpenGL does not support KHR_no_error ({error})")
            false

    let initVersion (glfw : Glfw) =
        let defaultVersion = Version(Config.MajorVersion, Config.MinorVersion)

        let versions =
            [
                Version(4,6)
                Version(4,5)
                Version(4,4)
                Version(4,3)
                Version(4,2)
                Version(4,1)
                Version(4,0)
                Version(3,3)
            ]

        let best =
            versions
            |> List.skipWhile ((<>) defaultVersion)
            |> List.tryFind (fun v ->
                if tryCreateOffscreenWindow v false glfw then
                    Log.line "OpenGL %A working" v
                    true
                else
                    let error, _ = glfw.GetError()
                    Log.warn "OpenGL %A not working: %A" v error
                    false

            )
        match best with
        | Some b ->
            version <- b
            supportsNoError <- glfw |> queryNoErrorSupport b
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

        member x.Dispose() =
            remContext.Invoke(null, [| x :> obj |]) |> ignore
            win <- NativePtr.zero

        interface IGraphicsContext with
            member x.Dispose() =
                x.Dispose()

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
                [0, DefaultSemantic.Colors, new Renderbuffer(runtime.Context, 0, size, TextureFormat.Rgba8, signature.Samples, 0L) :> IFramebufferOutput], None
            ) 

        { new ISwapchain with
            override this.Dispose() = 
                ()
            override this.Run(task : IRenderTask, queries : IQuery list)  = 
                use __ = runtime.Context.RenderingLock ctx

                let output = OutputDescription.ofFramebuffer defaultFramebuffer

                GL.ColorMask(true, true, true, true)
                GL.DepthMask(true)
                GL.Viewport(0, 0, size.X, size.Y)
                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                GL.ClearDepth(1.0)
                GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)

                let rt = { RenderToken.Empty with Queries = queries}
                task.Run(AdaptiveToken.Top, rt, output)

                glfw.SwapBuffers(win)
                true

            override this.Size = 
                size
        }

    let mutable private lastWindow = None

    let getFramebufferSamples() =
        if GL.getVersion() >= System.Version(4, 5) then
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
            GL.Check "could not bind default framebuffer"

            let samples = GL.Dispatch.GetFramebufferParameter(FramebufferTarget.DrawFramebuffer, unbox GetFramebufferParameter.Samples)
            GL.Check "could not retrieve framebuffer samples"

            if samples = 0 then Some 1
            else
                if ResourceValidation.Framebuffers.validSampleCounts |> Set.contains samples then
                    Some samples
                else
                    None
        else
            Log.warn "[GL] could not query sample count of default framebuffer."
            None

    let createSurface (runtime : Runtime) (cfg: WindowConfig) (glfw : Glfw) (win : nativeptr<WindowHandle>) =
        let old = glfw.GetCurrentContext()
        
        glfw.MakeContextCurrent(NativePtr.zero)
        if Option.isNone lastWindow then lastWindow <- Some win

        let ctx =
            new MyGraphicsContext(glfw, win)

        let info =
            new MyWindowInfo(win)

        let mutable samples = cfg.samples

        if not (isNull ctx) then  
            glfw.MakeContextCurrent(win)
            let current = glfw.GetCurrentContext() 
            let mutable desc = Unchecked.defaultof<_>
            let error = glfw.GetError(&desc)
            if error <> Silk.NET.GLFW.ErrorCode.NoError then
                Log.error "[GLFW] error after making trying to make context current: %A" error
            if current <> win then
                Log.error "[GLFW] could not make context current"
            ctx.LoadAll()          
            glfw.SwapInterval(if cfg.vsync then 1 else 0)
            samples <- getFramebufferSamples() |> Option.defaultValue cfg.samples
            glfw.MakeContextCurrent(NativePtr.zero)

        if old <> NativePtr.zero then
            glfw.MakeContextCurrent old
        
        let signature =
            runtime.CreateFramebufferSignature([
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ], samples)

        let handle = new Aardvark.Rendering.GL.ContextHandle(ctx, info)
        do handle.Initialize(runtime.DebugConfig, setDefaultStates = true)

        { new IWindowSurface with
            override x.Signature = signature
            override this.Handle =
                handle :> obj
            override this.CreateSwapchain(size: V2i) = 
                createSwapchain runtime signature handle glfw win size
            override this.Dispose() =
                signature.Dispose()
                ctx.Dispose()
        }

    let interop (debug : DebugConfig) =
        let disableErrorChecks =
            debug.ErrorFlagCheck = ErrorFlagCheck.Disabled

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
                glfw.WindowHint(WindowHintBool.ContextNoError, disableErrorChecks && supportsNoError)
                glfw.WindowHint(WindowHintBool.SrgbCapable, false)
                if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                    glfw.WindowHint(WindowHintBool.CocoaRetinaFramebuffer, cfg.physicalSize)

                glfw.WindowHint(WindowHintBool.ScaleToMonitor, true)
                glfw.WindowHint(WindowHintInt.Samples, if cfg.samples = 1 then 0 else cfg.samples)
        }

type OpenGlApplication private (runtime : Runtime, shaderCachePath : Option<string>, hideCocoaMenuBar : bool) as this =
    inherit Application(runtime, OpenGL.interop runtime.DebugConfig, hideCocoaMenuBar)

    let createContext() =
        let w = this.Instance.CreateWindow WindowConfig.Default
        let h = w.Surface.Handle :?> Aardvark.Rendering.GL.ContextHandle
        this.Instance.RemoveExistingWindow w
        h.OnDisposed.Add w.Dispose
        h

    let ctx = new Context(runtime, fun () -> this.Instance.Invoke createContext)

    do ctx.ShaderCachePath <- shaderCachePath
       runtime.Initialize(ctx)

    new(forceNvidia : bool, debug : IDebugConfig, shaderCachePath : Option<string>,
        [<Optional; DefaultParameterValue(false)>] hideCocoaMenuBar : bool) =
        if forceNvidia && RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            DynamicLinker.tryLoadLibrary "nvapi64.dll" |> ignore

        new OpenGlApplication(new Runtime(debug), shaderCachePath, hideCocoaMenuBar)

    new(forceNvidia : bool, debug : bool, shaderCachePath : Option<string>) =
        new OpenGlApplication(forceNvidia, DebugLevel.ofBool debug, shaderCachePath)

    new(forceNvidia : bool, debug : IDebugConfig) = new OpenGlApplication(forceNvidia, debug, Context.DefaultShaderCachePath)
    new(forceNvidia : bool, debug : bool) = new OpenGlApplication(forceNvidia, DebugLevel.ofBool debug, Context.DefaultShaderCachePath)

    new(debug : IDebugConfig) = new OpenGlApplication(true, debug)
    new(debug : bool) = new OpenGlApplication(true, DebugLevel.ofBool debug)

    new() = new OpenGlApplication(true, DebugLevel.None)

    member x.Context = ctx
    member x.Runtime = runtime

    member x.Initialize(ctrl : IRenderControl, samples : int) = 
        failwithf "unknown control type: %A" ctrl

    override x.Destroy() =
        // first dispose runtime in order to properly dispose resources..
        runtime.Dispose()
        ctx.Dispose()

    override x.CreateGameWindow(config : WindowConfig) =
        base.CreateGameWindow { config with opengl = true }

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()