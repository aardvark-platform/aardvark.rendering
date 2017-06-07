namespace Aardvark.Application.WPF

#if WINDOWS

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering.GL
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open System.Windows
open System.Windows.Controls
open System.Windows.Forms.Integration
open Aardvark.Application
open System.Windows.Threading
open System.Security

[<AutoOpen>]
module private DXSharingHelpers =

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern nativeint GetDesktopWindow()

    let renderbufferFormat =
        LookupTable.lookupTable [
            SharpDX.Direct3D9.Format.A8R8G8B8, RenderbufferFormat.Rgba8
            SharpDX.Direct3D9.Format.A8B8G8R8, RenderbufferFormat.Rgba8
        ]


type WglDxShareDevice =
    struct
        val mutable public Handle : nativeint

        member x.IsNull = x.Handle = 0n
        member x.NotNull = x.Handle <> 0n
        static member Null = WglDxShareDevice(0n)

        private new(h : nativeint) = { Handle = h }

    end

type WglDxShareHandle =
    struct
        val mutable public Handle : nativeint
        member x.IsNull = x.Handle = 0n
        member x.NotNull = x.Handle <> 0n

        static member Null = WglDxShareHandle(0n)

        private new(h : nativeint) = { Handle = h }
    end

type WglDXAccess =
    | ReadOnly = 0x0000 
    | ReadWrite = 0x0001
    | WriteDiscard = 0x0002


[<AutoOpen>]
module WGLDXExtensions =

    type private WglDXSetResourceShareHandleNVDel = delegate of nativeint * nativeint -> bool
    type private WglDXOpenDeviceNVDel = delegate of nativeint -> WglDxShareDevice
    type private WglDXCloseDeviceNVDel = delegate of WglDxShareDevice -> bool
    type private WglDXRegisterObjectNVDel = delegate of WglDxShareDevice * nativeint * int * All * WglDXAccess -> WglDxShareHandle
    type private WglDXUnregisterObjectNVDel = delegate of WglDxShareDevice * WglDxShareHandle -> bool
    type private WglDXObjectAccessNVDel = delegate of WglDxShareHandle * WglDXAccess -> bool
    type private WglDXLockObjectsNVDel = delegate of WglDxShareDevice * int * nativeptr<WglDxShareHandle> -> bool
    type private WglDXUnlockObjectsNVDel = delegate of WglDxShareDevice * int * nativeptr<WglDxShareHandle> -> bool

    [<AutoOpen>]
    module private MarshalExt =
        type Marshal with
            static member GetDelegateForFunctionPointer<'a>(ptr : nativeint) =
                Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>

        type ContextHandle with
            member x.Import<'a> (name : string) =
                let gc = x.Handle |> unbox<IGraphicsContextInternal>
                let ptr = gc.GetAddress name
                if ptr = 0n then
                    failwithf "could not import: %s" name
                Marshal.GetDelegateForFunctionPointer<'a>(ptr)

    type WGL private() =
        static let mutable wglDXSetResourceShareHandleNV : WglDXSetResourceShareHandleNVDel = null
        static let mutable wglDXOpenDeviceNV : WglDXOpenDeviceNVDel = null
        static let mutable wglDXCloseDeviceNV : WglDXCloseDeviceNVDel = null
        static let mutable wglDXRegisterObjectNV : WglDXRegisterObjectNVDel = null
        static let mutable wglDXUnregisterObjectNV : WglDXUnregisterObjectNVDel = null
        static let mutable wglDXObjectAccessNV : WglDXObjectAccessNVDel = null
        static let mutable wglDXLockObjectsNV : WglDXLockObjectsNVDel = null
        static let mutable wglDXUnlockObjectsNV : WglDXUnlockObjectsNVDel = null

        static let l = obj()
        static let mutable loaded = false

        static let supported =
            // TODO: check
            true
            //ExtensionHelpers.isSupported (Version(666,666,666)) "WGL_NV_DX_interop"

        static let fail fmt =
            Printf.kprintf (fun str ->
                let str = "[WGL/DX] " + str
                Log.error "%s" str
                failwith str
            ) fmt

        static let check() =
            if not supported then 
                fail "WGL_NV_DX_interop is not supported"

            assert (Option.isSome ContextHandle.Current)



        static do
            if supported then
                match ContextHandle.Current with
                    | Some handle ->
                        wglDXSetResourceShareHandleNV <- handle.Import "wglDXSetResourceShareHandleNV"
                        wglDXOpenDeviceNV <- handle.Import "wglDXOpenDeviceNV"
                        wglDXCloseDeviceNV <- handle.Import "wglDXCloseDeviceNV"
                        wglDXRegisterObjectNV <- handle.Import "wglDXRegisterObjectNV"
                        wglDXUnregisterObjectNV <- handle.Import "wglDXUnregisterObjectNV"
                        wglDXObjectAccessNV <- handle.Import "wglDXObjectAccessNV"
                        wglDXLockObjectsNV <- handle.Import "wglDXLockObjectsNV"
                        wglDXUnlockObjectsNV <- handle.Import "wglDXUnlockObjectsNV"
                    | None ->
                        failwith "[WGL] cannot load WGL_NV_DX_interop without a context"

        static member WGL_NV_DX_interop = supported

        static member SetResourceShareHandle(dxObject : nativeint, shareHandle : nativeint) =
            let worked = wglDXSetResourceShareHandleNV.Invoke(dxObject, shareHandle)
            if not worked then fail "could not set share handle"
            
        static member OpenDevice(dxDevice) =
            check()
            let dev = wglDXOpenDeviceNV.Invoke(dxDevice)
            if dev.IsNull then fail "could not open share device"
            dev

        static member CloseDevice(hDevice : WglDxShareDevice) =
            check()
            if not hDevice.IsNull then
                let worked = wglDXCloseDeviceNV.Invoke(hDevice)
                if not worked then fail "could not close share device"

        static member RegisterObject(hDevice, dxObject, glName, glType, access) =
            check()
            let res = wglDXRegisterObjectNV.Invoke(hDevice, dxObject, glName, glType, access)
            if res.IsNull then fail "could not register share object of type %A (%d)" glType glName
            res

        static member UnregisterObject(hDevice, hObject) =
            check()
            let res = wglDXUnregisterObjectNV.Invoke(hDevice, hObject)
            if not res  then fail "could not unregister share object"

        static member ObjectAccess(hObject, access) =
            check()
            wglDXObjectAccessNV.Invoke(hObject, access)

        static member LockObjects(hDevice : WglDxShareDevice, objects : WglDxShareHandle[]) =
            check()
            let pObjects = NativePtr.stackUse objects
            let worked = wglDXLockObjectsNV.Invoke(hDevice, objects.Length, pObjects)
            if not worked then fail "could not lock objects"
            
        static member UnlockObjects(hDevice : WglDxShareDevice, objects : WglDxShareHandle[]) =
            check()
            let pObjects = NativePtr.stackUse objects
            let worked = wglDXUnlockObjectsNV.Invoke(hDevice, objects.Length, pObjects)
            if not worked then fail "could not unlock objects"
            
[<AutoOpen>]
module WGLDXContextExtensions =

    type ShareContext private(ctx : Context, d3d : SharpDX.Direct3D9.Direct3DEx, device : SharpDX.Direct3D9.DeviceEx, shareDevice : WglDxShareDevice) =
        static member Create(ctx : Context) =
            use __ = ctx.ResourceLock
            let d3d = new SharpDX.Direct3D9.Direct3DEx()
            let device = 
                let hndl = GetDesktopWindow()
                let parameters = 
                    SharpDX.Direct3D9.PresentParameters(
                        Windowed = SharpDX.Mathematics.Interop.RawBool(true),
                        SwapEffect = SharpDX.Direct3D9.SwapEffect.Discard,
                        DeviceWindowHandle = hndl, 
                        PresentationInterval = SharpDX.Direct3D9.PresentInterval.Immediate
                    )
        
                new SharpDX.Direct3D9.DeviceEx(
                    d3d, 0, 
                    SharpDX.Direct3D9.DeviceType.Hardware, 0n, 
                    SharpDX.Direct3D9.CreateFlags.FpuPreserve ||| SharpDX.Direct3D9.CreateFlags.HardwareVertexProcessing ||| 
                    SharpDX.Direct3D9.CreateFlags.Multithreaded, 
                    parameters
            )
            let shareDevice = WGL.OpenDevice(device.NativePointer)

            ShareContext(ctx, d3d, device, shareDevice)




        member x.Context = ctx
        member x.ShareDevice = shareDevice
        member x.Direct3D = d3d
        member x.Device = device

    let private shareContexts = System.Runtime.CompilerServices.ConditionalWeakTable<Context, ShareContext>()

    type D3DRenderbuffer(ctx : ShareContext, handle : int, size : V2i, fmt : RenderbufferFormat, samples : int, dxSurface : SharpDX.Direct3D9.Surface, shareHandle : WglDxShareHandle) =
        inherit Aardvark.Rendering.GL.Renderbuffer(ctx.Context, handle, size, fmt, samples, 0L)
        let mutable shareHandle = shareHandle


        member x.Surface = dxSurface

        member x.Lock() =
            use __ = ctx.Context.ResourceLock
            GL.Flush()
            GL.Finish()
            WGL.LockObjects(ctx.ShareDevice, [| shareHandle |])

        member x.Unlock() =
            use __ = ctx.Context.ResourceLock
            GL.Flush()
            GL.Finish()
            WGL.UnlockObjects(ctx.ShareDevice, [| shareHandle |])

        member x.Dispose() =
            if shareHandle.NotNull then
                use __ = ctx.Context.ResourceLock
                WGL.UnregisterObject(ctx.ShareDevice, shareHandle)
                shareHandle <- WglDxShareHandle.Null
                dxSurface.Dispose()
                GL.DeleteRenderbuffer(handle)
                x.Handle <- 0
        
        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let private dxFormat =
        LookupTable.lookupTable [
            RenderbufferFormat.Rgba8, SharpDX.Direct3D9.Format.A8R8G8B8
            RenderbufferFormat.Depth24Stencil8, SharpDX.Direct3D9.Format.D24S8
        ]

    type Context with
        member x.ShareContext =
            lock shareContexts (fun () ->
                match shareContexts.TryGetValue x with
                    | (true, ctx) -> ctx
                    | _ ->
                        let c = ShareContext.Create(x)
                        shareContexts.Add(x, c)
                        c
            )
        
        member x.CreateD3DRenderbuffer(size : V2i, format : RenderbufferFormat, samples : int) =
            use __ = x.ResourceLock

            let dxFormat = dxFormat format
            let ctx = x.ShareContext
            let sampleType, sampleQualityLevels =
                if samples <= 1 then
                    SharpDX.Direct3D9.MultisampleType.None, 1
                else
                    let t = unbox<SharpDX.Direct3D9.MultisampleType> samples
                    let mutable levels = 0
                    if ctx.Direct3D.CheckDeviceMultisampleType(0, SharpDX.Direct3D9.DeviceType.Hardware, dxFormat, true, t, &levels) then
                        t, levels
                    else
                        Log.warn "multisampling %d not supported" samples
                        SharpDX.Direct3D9.MultisampleType.None, 1

            let mutable wddmHandle = 0n
            let surface =
                if format = RenderbufferFormat.Depth24Stencil8 then
                    SharpDX.Direct3D9.Surface.CreateDepthStencil(
                        ctx.Device,
                        size.X, size.Y, 
                        dxFormat,
                        sampleType, sampleQualityLevels - 1,
                        true,
                        &wddmHandle
                    )
                else
                    SharpDX.Direct3D9.Surface.CreateRenderTarget(
                        ctx.Device,
                        size.X, size.Y, 
                        dxFormat,
                        sampleType, sampleQualityLevels - 1,
                        true,
                        &wddmHandle
                    )

            let b = GL.GenRenderbuffer()
            GL.Check "could not create renderbuffer"
//            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, b)
//            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, unbox (int format), size.X, size.Y)
//            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)

            WGL.SetResourceShareHandle(surface.NativePointer, wddmHandle)
            let shareHandle = WGL.RegisterObject(ctx.ShareDevice, surface.NativePointer, b, All.Renderbuffer, WglDXAccess.WriteDiscard)

//            let mutable ssamples = 0
//            let mutable ssize = V2i.Zero
//            let mutable sfmt = 0
//            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, b)
//
//            GL.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, RenderbufferParameterName.RenderbufferSamples, &ssamples)
//            GL.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, RenderbufferParameterName.RenderbufferWidth, &ssize.X)
//            GL.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, RenderbufferParameterName.RenderbufferHeight, &ssize.Y)
//            GL.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, RenderbufferParameterName.RenderbufferInternalFormat, &sfmt)
//            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
//
//            Log.start "shared renderbuffer %d" b
//            Log.line "size:     %A (%A)" ssize size
//            Log.line "samples:  %A (%A)" ssamples samples
//            Log.line "format:   %A (%A)" (unbox<RenderbufferFormat> sfmt) format
//            Log.stop()


            new D3DRenderbuffer(ctx, b, size, format, samples, surface, shareHandle)

type OpenGlSharingRenderControl(runtime : Runtime, samples : int) as this =
    inherit ContentControl()

    let ctx = runtime.Context
    let handle = ContextHandle.create(true)

    let img = System.Windows.Interop.D3DImage()
    let content = Windows.Controls.Image(Source = img)
    do content.Stretch <- Media.Stretch.Fill
    do this.Content <- content

    let mutable pending = 1
    let trigger() = pending <- 1
    let caller = AdaptiveObject()
    let subscription = caller.AddMarkingCallback trigger
    do this.SizeChanged.Add (fun _ -> trigger())


    let mutable textureSize = V2i.Zero
    let size = Mod.init V2i.II

    let mutable renderTask = RenderTask.empty

    let signature =
        new FramebufferSignature(
            runtime, 
            Map.ofList [0, (DefaultSemantic.Colors, { samples = samples; format = RenderbufferFormat.Rgba8 })], 
            Map.empty, 
            Some { samples = samples; format = RenderbufferFormat.Depth24Stencil8 }, 
            None
        )


    let startTime = DateTime.Now
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let time = Mod.custom (fun _ -> startTime + sw.Elapsed)

    let mutable ping : Option<D3DRenderbuffer> = None
    let mutable pong : Option<D3DRenderbuffer> = None
    let mutable depth : Option<D3DRenderbuffer> = None

    let renderLock = obj()

    let render (size : V2i) (action : Framebuffer -> unit) =
        use __ = ctx.RenderingLock(handle)
            
        let backBuffer =
            match ping with
                | None -> 
                    ctx.CreateD3DRenderbuffer(size, RenderbufferFormat.Rgba8, samples)
                | Some o when o.Size = size -> o
                | Some o ->
                    o.Dispose()
                    ctx.CreateD3DRenderbuffer(size, RenderbufferFormat.Rgba8, samples)

        ping <- Some backBuffer

        let depthBuffer =
            match depth with
                | None ->
                    ctx.CreateD3DRenderbuffer(size, RenderbufferFormat.Depth24Stencil8, samples)
                | Some d when d.Size <> size ->
                    d.Dispose()
                    ctx.CreateD3DRenderbuffer(size, RenderbufferFormat.Depth24Stencil8, samples)
                | Some d ->
                    d

        depth <- Some depthBuffer

        let fbo = GL.GenFramebuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, backBuffer.Handle)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthBuffer.Handle)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)

        let framebuffer = new Framebuffer(ctx, signature, (fun _ -> fbo), ignore, [0, DefaultSemantic.Colors, backBuffer :> IFramebufferOutput], Some (depthBuffer :> IFramebufferOutput))

        try 
            depthBuffer.Lock()
            backBuffer.Lock()
            action framebuffer
            backBuffer
        finally 
            GL.DeleteFramebuffer fbo
            backBuffer.Unlock()
            depthBuffer.Unlock()

    let renderTimer = 
        let doit s e =
            if System.Threading.Interlocked.Exchange(&pending, 0) = 1 then
                lock renderLock (fun () ->
                    let s = V2i(this.ActualWidth, this.ActualHeight)
                    if s.AllDifferent 0 then
                        img.Lock()
                        transact (fun () -> size.Value <- s)

                        let buffer = 
                            render s (fun fbo ->
                                GL.Check "before wtf"
                                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.Handle)
                                GL.Check "first bind framebuffer"
                                GL.Viewport(0, 0, s.X, s.Y)
                                GL.Check "viewport"
                                GL.ClearColor(1.0f, 0.0f, 0.0f, 1.0f)
                                GL.ClearDepth(1.0)
                                GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
                                GL.Check "clear framebuffer"
                                GL.BindFramebuffer(FramebufferTarget.Framebuffer,0)
                                GL.Check "Unbind framebuffer"
                                let output = OutputDescription.ofFramebuffer fbo
                                caller.EvaluateAlways AdaptiveToken.Top (fun token ->
                                    renderTask.Run(token, RenderToken.Empty, output)
                                )
                            )

                        img.SetBackBuffer(Interop.D3DResourceType.IDirect3DSurface9, buffer.Surface.NativePointer)
                        img.AddDirtyRect(Int32Rect(0, 0, img.PixelWidth, img.PixelHeight))
                        img.Unlock()
                        Fun.Swap(&ping, &pong)
                        transact (fun () -> time.MarkOutdated())
                )
        DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / 60.0), DispatcherPriority.Render, EventHandler(doit), this.Dispatcher)


    let keyboard = EventKeyboard()
    let mouse = EventMouse(false)

    member x.ContextHandle = handle

    member x.FramebufferSignature = signature :> IFramebufferSignature
    
    member x.ForceRedraw() =
        x.Dispatcher.Invoke(fun () -> x.InvalidateVisual())

    member x.Keyboard = keyboard :> IKeyboard
    member x.Mouse = mouse :> IMouse
    
    member x.RenderTask
        with get() = renderTask
        and set t = renderTask <- t

    member x.Sizes = size :> IMod<_>
    

    interface IRenderTarget with
        member x.FramebufferSignature = x.FramebufferSignature
        member x.Samples = 1
        member x.Runtime = runtime :> IRuntime
        member x.Time = time
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.Sizes = x.Sizes


#endif