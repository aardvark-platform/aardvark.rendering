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
        let mutable win = win

        static let addContext = typeof<GraphicsContext>.GetMethod("AddContext", BindingFlags.NonPublic ||| BindingFlags.Static)
        static let remContext = typeof<GraphicsContext>.GetMethod("RemoveContext", BindingFlags.NonPublic ||| BindingFlags.Static)

        let handle = ContextHandle(NativePtr.toNativeInt win)

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

    let createStereoSwapchain (runtime : Runtime) (signature : IFramebufferSignature) (ctx : Aardvark.Rendering.GL.ContextHandle)
                              (glfw : Glfw) (win : nativeptr<WindowHandle>) (size : V2i) =
        let colors = runtime.CreateTexture2DArray(size, TextureFormat.Rgba8, samples = signature.Samples, count = 2) |> unbox<Texture>
        colors.Name <- "Colors Attachment (Stereo Swapchain)"

        let depth = runtime.CreateTexture2DArray(size, TextureFormat.Depth24Stencil8, samples = signature.Samples, count = 2) |> unbox<Texture>
        depth.Name <- "Depth / Stencil Attachment (Stereo Swapchain)"

        let framebuffer =
            runtime.CreateFramebuffer(signature, [
                DefaultSemantic.Colors, colors.[TextureAspect.Color,0,*] :> IFramebufferOutput
                DefaultSemantic.DepthStencil, depth.[TextureAspect.Depth,0,*] :> IFramebufferOutput
            ])

        { new ISwapchain with
            override this.Dispose() =
                framebuffer.Dispose()
                colors.Dispose()
                depth.Dispose()

            // TODO: It may be possible to simplify this
            override this.Run(task : IRenderTask, queries : IQuery list)  =
                use __ = runtime.Context.RenderingLock ctx
                ctx.PushDebugGroup("Swapchain")

                let output = OutputDescription.ofFramebuffer framebuffer

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (framebuffer |> unbox<Framebuffer>).Handle)
                GL.ColorMask(true, true, true, true)
                GL.DepthMask(true)
                GL.Viewport(0, 0, size.X, size.Y)
                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                GL.ClearDepth(1.0)
                GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)

                let rt = { RenderToken.Empty with Queries = queries}
                task.Run(AdaptiveToken.Top, rt, output)

                let fSrc = GL.GenFramebuffer()
                let fDst = GL.GenFramebuffer()

                let mutable temp = 0
                GL.CreateTextures(TextureTarget.Texture2D, 1, &temp)
                if temp = 0 then failwith ""
                GL.TextureStorage2D(temp, 1, unbox (int colors.Format), size.X, size.Y)

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fDst)
                GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, colors.Handle, 0, 0)
                GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, temp, 0)
                GL.BlitFramebuffer(0, 0, colors.Size.X, colors.Size.Y, 0, 0, size.X, size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear)

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fDst)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.DrawBuffer(DrawBufferMode.BackLeft)
                GL.BlitFramebuffer(0, 0, size.X, size.Y, 0, 0, size.X, size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear)

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fDst)
                GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, colors.Handle, 0, 1)
                GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, temp, 0)
                GL.BlitFramebuffer(0, 0, colors.Size.X, colors.Size.Y, 0, 0, size.X, size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear)

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fDst)
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.DrawBuffer(DrawBufferMode.BackRight)
                GL.BlitFramebuffer(0, 0, size.X, size.Y, 0, 0, size.X, size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear)

                GL.DeleteFramebuffer fSrc
                GL.DeleteFramebuffer fDst
                GL.DeleteTexture temp

                glfw.SwapBuffers(win)
                ctx.PopDebugGroup()
                true

            override this.Size =
                size
        }

    let createSwapchain (runtime : Runtime) (signature : IFramebufferSignature) (ctx : Aardvark.Rendering.GL.ContextHandle)
                        (glfw : Glfw) (win : nativeptr<WindowHandle>) (size : V2i) =

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
                ctx.PushDebugGroup("Swapchain")

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
                ctx.PopDebugGroup()
                true

            override this.Size =
                size
        }

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
            Log.warn "[GL] Could not query sample count of default framebuffer."
            None

    let createSurface (runtime : Runtime) (cfg: WindowConfig) (glfw : Glfw) (win : nativeptr<WindowHandle>) =
        let old = glfw.GetCurrentContext()

        glfw.MakeContextCurrent(NativePtr.zero)

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
                let errorMessage =
                    let str =
                        if NativePtr.isNullPtr desc then
                            null
                        else
                            let mutable len = 0
                            while desc.[len] <> 0uy do inc &len
                            System.Text.Encoding.UTF8.GetString(desc, len)                    

                    if String.IsNullOrEmpty str then
                        $"Error after trying to make context current"
                    else
                        str

                Log.error $"[GLFW] {errorMessage} (Error code: {error})"

            if current <> win then
                Log.error "[GLFW] Could not make context current"

            ctx.LoadAll()
            glfw.SwapInterval(if cfg.vsync then 1 else 0)
            samples <- getFramebufferSamples() |> Option.defaultValue cfg.samples
            glfw.MakeContextCurrent(NativePtr.zero)

        if old <> NativePtr.zero then
            glfw.MakeContextCurrent old

        let handle = new Aardvark.Rendering.GL.ContextHandle(ctx, info)

        let signature =
            handle.Use (fun _ ->
                handle.Initialize(runtime.DebugConfig, setDefaultStates = true)

                let layers, perLayerUniforms =
                    if cfg.stereo then
                        2, Set.ofList [
                            "ProjTrafo"
                            "ViewTrafo"
                            "ModelViewTrafo"
                            "ViewProjTrafo"
                            "ModelViewProjTrafo"

                            "ProjTrafoInv"
                            "ViewTrafoInv"
                            "ModelViewTrafoInv"
                            "ViewProjTrafoInv"
                            "ModelViewProjTrafoInv"
                        ]
                    else
                        1, Set.empty

                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ], samples, layers, perLayerUniforms)
            )

        { new IWindowSurface with
            override x.Signature = signature
            override this.Handle =
                handle :> obj
            override this.CreateSwapchain(size: V2i) =
                if cfg.stereo then 
                    createStereoSwapchain runtime signature handle glfw win size
                else 
                    createSwapchain runtime signature handle glfw win size
            override this.Dispose() =
                signature.Dispose()
                ctx.Dispose()
        }

    let getInterop (debug : DebugConfig) =
        let disableErrorChecks =
            debug.ErrorFlagCheck = ErrorFlagCheck.Disabled

        let getCurrentContext = typeof<GraphicsContext>.GetField("GetCurrentContext", BindingFlags.NonPublic ||| BindingFlags.Static)
        let mutable getCurrentContextDelegate = null

        let install (glfw: Glfw) =
            getCurrentContextDelegate <-
                GraphicsContext.GetCurrentContextDelegate(
                    fun () -> ContextHandle(NativePtr.toNativeInt <| glfw.GetCurrentContext())
                )

            if getCurrentContext.GetValue(null) <> null then
                Log.warn "[GLFW] Overriding OpenTK GetCurrentContext"

            getCurrentContext.SetValue(null, getCurrentContextDelegate)

        let cleanup() =
            if not <| isNull getCurrentContextDelegate then
                if getCurrentContext.GetValue(null) = getCurrentContextDelegate then
                    getCurrentContext.SetValue(null, null)
                    getCurrentContextDelegate <- null

        { new IWindowInterop with
            override __.Boot(glfw) =
                initVersion glfw
                install glfw

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
                glfw.WindowHint(WindowHintBool.Stereo, cfg.stereo)
                glfw.WindowHint(WindowHintBool.OpenGLDebugContext, false)
                glfw.WindowHint(WindowHintBool.ContextNoError, disableErrorChecks && supportsNoError)
                glfw.WindowHint(WindowHintBool.SrgbCapable, false)
                if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                    glfw.WindowHint(WindowHintBool.CocoaRetinaFramebuffer, cfg.physicalSize)

                glfw.WindowHint(WindowHintBool.ScaleToMonitor, true)
                glfw.WindowHint(WindowHintInt.Samples, if cfg.samples = 1 then 0 else cfg.samples)

            override x.Dispose() = cleanup()
        }

type OpenGlApplication private (runtime : Runtime, shaderCachePath : Option<string>, hideCocoaMenuBar : bool) as this =
    inherit Application(runtime, OpenGL.getInterop runtime.DebugConfig, hideCocoaMenuBar)

    // Note: We ignore the passed parent since we determine the parent context in the CreateWindow method.
    // This is always the first created context and should therefore match the passed one anyway.
    let createContext (_parent : Aardvark.Rendering.GL.ContextHandle option) =
        this.Instance.Invoke (fun _ ->
            let w = this.Instance.CreateWindow WindowConfig.Default
            let h = w.Surface.Handle :?> Aardvark.Rendering.GL.ContextHandle
            this.Instance.RemoveExistingWindow w
            h.OnDisposed.Add w.Dispose
            h
        )

    let ctx = new Context(runtime, createContext)

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
        runtime.Dispose()

    interface IApplication with
        member x.Initialize(ctrl : IRenderControl, samples : int) = x.Initialize(ctrl, samples)
        member x.Runtime = x.Runtime :> IRuntime
        member x.Dispose() = x.Dispose()