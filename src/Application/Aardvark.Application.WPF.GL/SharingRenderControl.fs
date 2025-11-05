namespace Aardvark.Application.WPF

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.GL
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open System.Windows
open System.Windows.Controls
open Aardvark.Application
open System.Windows.Threading
open System.Security
open System.Threading
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AutoOpen>]
module private DXSharingHelpers =

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern nativeint GetDesktopWindow()


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
            lock l (fun () ->
                if not supported then 
                    fail "WGL_NV_DX_interop is not supported"

                if not loaded then
                    loaded <- true
                    match ContextHandle.Current with
                        | ValueSome handle ->
                            wglDXSetResourceShareHandleNV <- handle.Import "wglDXSetResourceShareHandleNV"
                            wglDXOpenDeviceNV <- handle.Import "wglDXOpenDeviceNV"
                            wglDXCloseDeviceNV <- handle.Import "wglDXCloseDeviceNV"
                            wglDXRegisterObjectNV <- handle.Import "wglDXRegisterObjectNV"
                            wglDXUnregisterObjectNV <- handle.Import "wglDXUnregisterObjectNV"
                            wglDXObjectAccessNV <- handle.Import "wglDXObjectAccessNV"
                            wglDXLockObjectsNV <- handle.Import "wglDXLockObjectsNV"
                            wglDXUnlockObjectsNV <- handle.Import "wglDXUnlockObjectsNV"
                        | ValueNone ->
                            failwith "[WGL] cannot load WGL_NV_DX_interop without a context"


                assert (ValueOption.isSome ContextHandle.Current)
            )

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
    open Silk.NET.Core
    open Silk.NET.Direct3D9

    let inline spanny<'a when 'a : unmanaged> (ptr : nativeptr<'a>) =
        System.Span<'a>(NativePtr.toVoidPtr ptr, 1)

    let inline private checkHResult (result: int) =
        if result <> 0 then raise <| Marshal.GetExceptionForHR(result)

    type ShareContext private(ctx : Context, d3d : nativeptr<IDirect3D9Ex>, device : nativeptr<IDirect3DDevice9Ex>, shareDevice : WglDxShareDevice) =
        static member Create(ctx : Context) =
            use __ = ctx.ResourceLock

            let d3d = D3D9.GetApi(null)
            let mutable d3d9ex = Unchecked.defaultof<_>

            d3d.Direct3DCreate9Ex(uint32 D3D9.SdkVersion, &d3d9ex) |> checkHResult

            let pDevice = 
                let hndl = GetDesktopWindow()
                let mutable parameters = 
                    PresentParameters(
                        backBufferWidth = Nullable 10u,
                        backBufferHeight = Nullable 10u ,
                        backBufferFormat = Nullable Format.A8R8G8B8,
                        backBufferCount = Nullable 0u,
                        multiSampleType = Nullable MultisampleType.MultisampleNone,
                        multiSampleQuality = Nullable 0u,
                        swapEffect = Nullable Swapeffect.Discard,
                        windowed = Nullable (Bool32 true),
                        hDeviceWindow = Nullable hndl,
                        presentationInterval = Nullable (uint32 D3D9.PresentIntervalDefault)
                    )

                let createFlags = D3D9.CreateFpuPreserve ||| D3D9.CreateMultithreaded ||| D3D9.CreateHardwareVertexprocessing
                let mutable dev = Unchecked.defaultof<_>

                spanny(d3d9ex).[0].CreateDeviceEx(
                    0u,
                    Devtype.Hal,
                    0n,
                    uint32 createFlags,
                    &parameters,
                    NativePtr.zero,
                    &dev
                ) |> checkHResult

                dev

            let shareDevice = WGL.OpenDevice(NativePtr.toNativeInt pDevice)
            ShareContext(ctx, d3d9ex, pDevice, shareDevice)

        member x.Context = ctx
        member x.ShareDevice = shareDevice
        member x.Direct3D = d3d
        member x.Device = device

    let private shareContexts = System.Runtime.CompilerServices.ConditionalWeakTable<Context, ShareContext>()

    type D3DRenderbuffer(ctx : ShareContext, resolveBuffer : int, renderBuffer : int, size : V2i, fmt : TextureFormat, samples : int, dxSurface : nativeptr<IDirect3DSurface9>, shareHandle : WglDxShareHandle) =
        inherit Aardvark.Rendering.GL.Renderbuffer(ctx.Context, renderBuffer, size, fmt, samples, 0L)
        let mutable shareHandle = shareHandle

        let hateBuffer = // for amd double blit
            match ctx.Context.Driver.device with
                | GPUVendor.AMD when samples > 1 -> 
                    let b = GL.GenRenderbuffer()
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, b)
                    GL.Check "could not bind renderbuffer"
                    GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, unbox (int fmt), size.X, size.Y)
                    GL.Check "renderbuffer storage"
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
                    b
                | _ -> -1

        static let blit (size : V2i) (srcBuffer : int) (dstBuffer : int) (flip : bool) =

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
            GL.Enable(EnableCap.Multisample)

            let srcFbo = GL.GenFramebuffer()

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, srcFbo)
            GL.FramebufferRenderbuffer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, srcBuffer)

            let dstFbo = GL.GenFramebuffer()
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, dstFbo)
            GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, dstBuffer)

            GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0)

            if flip then
                GL.BlitFramebuffer(0, 0, size.X, size.Y, 0, size.Y, size.X, 0, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest)
            else
                GL.BlitFramebuffer(0, 0, size.X, size.Y, 0, 0, size.X, size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest)
            GL.Check "blit failed"

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)

            GL.DeleteFramebuffer(srcFbo)
            GL.DeleteFramebuffer(dstFbo)




        member x.Surface = dxSurface

        member x.Lock() =
//            let mutable a = 0
//            for i in 1 .. 1 <<< 30 do
//                a <- a + i
//            Log.line "lock"
            //System.Threading.Thread.Sleep(10)
            ()

        member x.Unlock() = 
            use __ = ctx.Context.ResourceLock
            WGL.LockObjects(ctx.ShareDevice, [| shareHandle |])
            if hateBuffer >= 0 then 
                blit size renderBuffer hateBuffer true // amd double blit
                blit size hateBuffer resolveBuffer false
            else
                blit size renderBuffer resolveBuffer true // non amd just works
            WGL.UnlockObjects(ctx.ShareDevice, [| shareHandle |])
            GL.Flush()
            GL.Finish()

        member x.Dispose() =
            if shareHandle.NotNull then
                use __ = ctx.Context.ResourceLock
                WGL.UnregisterObject(ctx.ShareDevice, shareHandle)
                shareHandle <- WglDxShareHandle.Null
                spanny(dxSurface).[0].Release() |> ignore
                GL.DeleteRenderbuffer(resolveBuffer)
                GL.DeleteRenderbuffer(renderBuffer)
                x.Handle <- 0
        
        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let private dxFormat =
        LookupTable.lookup [
            TextureFormat.Rgba8, Format.A8R8G8B8
            TextureFormat.Depth24Stencil8, Format.D24S8
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
        
        member x.CreateD3DRenderbuffer(size : V2i, format : TextureFormat, samples : int) =
            use __ = x.ResourceLock

            let dxFormat = dxFormat format
            let ctx = x.ShareContext

            let mutable wddmHandle = Unchecked.defaultof<_>
            let mutable surface = Unchecked.defaultof<_>
            let _ =
                spanny(ctx.Device).[0].CreateRenderTarget(
                    uint32 size.X, uint32 size.Y, 
                    dxFormat,
                    MultisampleType.MultisampleNone, 0u,
                    Bool32 true,
                    &surface,
                    &wddmHandle
                )

            let resolveBuffer = GL.GenRenderbuffer()
            GL.Check "could not create renderbuffer"
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, resolveBuffer)
            GL.Check "could not bind renderbuffer"
            //GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, unbox (int format), size.X, size.Y)
            GL.Check "renderbuffer storage"
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)

            let renderBuffer = GL.GenRenderbuffer()
            GL.Check "could not create renderbuffer"
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderBuffer)
            GL.Check "could not bind renderbuffer"

            if samples > 1 then
                GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, unbox (int format), size.X, size.Y)
            else
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, unbox (int format), size.X, size.Y)
            GL.Check "could not allocate renderbuffer"

            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
            GL.Check "could not unbind renderbuffer"



            WGL.SetResourceShareHandle(NativePtr.toNativeInt surface, NativePtr.toNativeInt (NativePtr.ofVoidPtr<int> wddmHandle))
            let shareHandle = WGL.RegisterObject(ctx.ShareDevice, NativePtr.toNativeInt surface, resolveBuffer, All.Renderbuffer, WglDXAccess.WriteDiscard)
            //let shareHandle = WglDxShareHandle.Null

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
//            Log.line "format:   %A (%A)" (unbox<TextureFormat> sfmt) format
//            Log.stop()


            new D3DRenderbuffer(ctx, resolveBuffer, renderBuffer, size, format, samples, surface, shareHandle)

type private DummyObject() =
    inherit AdaptiveObject()

type OpenGlSharingRenderControl(runtime : Runtime, samples : int) as this =
    inherit UserControl()

    let ctx = runtime.Context
    let handle = ContextHandleOpenTK.create runtime.DebugConfig

    let img = System.Windows.Interop.D3DImage()
    let content = Windows.Controls.Image(Source = img)

    do content.Margin <- Thickness(0.0)
    do content.HorizontalAlignment <- HorizontalAlignment.Stretch
    do content.VerticalAlignment <- VerticalAlignment.Stretch
    do content.Stretch <- Media.Stretch.UniformToFill
    do this.Content <- content

    let mutable pending = 1
    let trigger() = pending <- 1

    let caller = DummyObject()
    let subscription = caller.AddMarkingCallback trigger
    do this.SizeChanged.Add (fun _ -> trigger())

    let size = AVal.init V2i.II

    let mutable renderTask = RenderTask.empty
    let mutable cursor = Cursor.Default

    let signature =
        runtime.CreateFramebufferSignature([
            DefaultSemantic.Colors, TextureFormat.Rgba8
            DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
        ], samples)

    let startTime = DateTime.Now
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let time = AVal.custom (fun _ -> startTime + sw.Elapsed)
    
    let mutable running = false
    let mutable color : Option<D3DRenderbuffer> = None
    let mutable depth : Option<Renderbuffer> = None
    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()

    let colorBufferLock = obj()

    let render (size : V2i) =
        lock colorBufferLock (fun () ->
            use __ = ctx.RenderingLock(handle)
            
            beforeRender.Trigger()

            let backBuffer =
                match color with
                    | None -> 
                        ctx.CreateD3DRenderbuffer(size, TextureFormat.Rgba8, samples)
                    | Some o when o.Size = size -> o
                    | Some o ->
                        o.Dispose()
                        ctx.CreateD3DRenderbuffer(size, TextureFormat.Rgba8, samples)

            color <- Some backBuffer

            let depthBuffer =
                match depth with
                    | None ->
                        ctx.CreateRenderbuffer(size, TextureFormat.Depth24Stencil8, samples)
                    | Some d when d.Size <> size ->
                        ctx.Delete d
                        ctx.CreateRenderbuffer(size, TextureFormat.Depth24Stencil8, samples)
                    | Some d ->
                        d

            depth <- Some depthBuffer

            let fbo = GL.GenFramebuffer()
            GL.Check "could not create frambuffer"

            // create the framebuffer
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo)
            GL.Check "could not bind frambuffer"
            GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, backBuffer.Handle)
            GL.Check "could not attach color to frambuffer"
            GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthBuffer.Handle)
            GL.Check "could not attach depth to frambuffer"
        
            // clear the framebuffer
            GL.Viewport(0, 0, size.X, size.Y)
            GL.Check "could not set viewport"
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
            GL.Check "could not set clear color"
            GL.ClearDepth(1.0)
            GL.Check "could not set clear depth"
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
            GL.Check "could not clear framebuffer"

            // unbind the framebuffer
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
            GL.Check "could not unbind frambuffer"
            
            // render to the framebuffer
            let framebuffer = new Framebuffer(ctx, signature, (fun _ -> fbo), ignore, [0, DefaultSemantic.Colors, backBuffer :> IFramebufferOutput], Some (depthBuffer :> IFramebufferOutput))
            let output = OutputDescription.ofFramebuffer framebuffer
            caller.EvaluateAlways AdaptiveToken.Top (fun token ->
                renderTask.Run(token, RenderToken.Empty, output)
            )

            GL.DeleteFramebuffer fbo
            GL.Flush()
            GL.Finish()

            afterRender.Trigger()
        )

    //let mutable backBufferDirty = 0
    let mutable isRendering = 0


    let renderTick (s : obj) (e : EventArgs) =

        if Interlocked.CompareExchange(&isRendering, 1, 0) = 0  then
            if Interlocked.Exchange(&pending, 0) = 1 then
                let s = V2i(round this.ActualWidth, round this.ActualHeight)
                if s.AllDifferent 0 then
                    let sctx = DispatcherSynchronizationContext.Current

                    let doit =
                        async {
                            do! Async.SwitchToThreadPool()
                            try render s
                            with e -> Log.error "render faulted: %A" e

                            do! Async.SwitchToContext sctx

                            try
                                lock colorBufferLock (fun () ->
                                    match color with
                                        | Some c -> 
                                            use __ = ctx.RenderingLock(handle)
                                            img.Lock()
                                            c.Lock()
                                            img.SetBackBuffer(Interop.D3DResourceType.IDirect3DSurface9, NativePtr.toNativeInt c.Surface)

                                            c.Unlock()
                                            img.AddDirtyRect(Int32Rect(0,0,img.PixelWidth, img.PixelHeight))
                                            img.Unlock()
                                        | None ->
                                            ()
                                )
                            with e ->
                                Log.error "swap faulted"


                            do! Async.SwitchToThreadPool()
                            transact(fun () -> size.Value <- s)
                            transact (fun () -> time.MarkOutdated())
                            isRendering <- 0
                        }
                    
                    Async.Start doit
            else
                isRendering <- 0

    let renderTimer = 
        running <- true
        DispatcherTimer(
            TimeSpan.FromMilliseconds(1000.0 / 120.0), 
            DispatcherPriority.Render, 
            EventHandler(renderTick), 
            this.Dispatcher
        )

    let keyboard = EventKeyboard()
    let mouse = EventMouse(true)
    
    do this.bootIO()

    member private x.bootIO() =
        let key (k : Input.Key) =
            int k |> unbox<Aardvark.Application.Keys>

        let button (b : Input.MouseButton) =
            match b with
                | Input.MouseButton.Left -> MouseButtons.Left
                | Input.MouseButton.Middle -> MouseButtons.Middle
                | Input.MouseButton.Right -> MouseButtons.Right
                | _ -> MouseButtons.None

        let mousePos() =
            let pt = Input.Mouse.GetPosition(x)
            PixelPosition(int pt.X, int pt.Y, int x.ActualWidth, int x.ActualHeight)

        x.Focusable <- true

        x.KeyDown.Add(fun e ->
            keyboard.KeyDown(key e.Key)
        )

        x.KeyUp.Add(fun e ->
            keyboard.KeyUp(key e.Key)
        )

        x.PreviewKeyDown.Add (fun e ->
            keyboard.KeyDown(key e.Key)
            e.Handled <- true
        )

        x.TextInput.Add (fun e ->
            for c in e.Text do
                keyboard.KeyPress(c)
            e.Handled <- true
        )

        

        x.MouseEnter.Add (fun e ->
            mouse.Enter(mousePos())
        )

        x.MouseLeave.Add (fun e ->
            mouse.Leave(mousePos())
        )

        x.MouseDown.Add (fun e ->
            let button = button e.ChangedButton
            x.Focus() |> ignore
            mouse.Down(mousePos(), button)
        )

        x.MouseUp.Add (fun e ->
            let button = button e.ChangedButton
            mouse.Up(mousePos(), button)
        )

        x.MouseMove.Add (fun e ->
            mouse.Move(mousePos())
        )

        x.MouseWheel.Add (fun e ->
            mouse.Scroll(mousePos(), float e.Delta)
        )


        ()

    member private x.Stop() =
        if running then
            running <- false
            Log.warn "stop"
            renderTimer.Stop()
            img.Lock()
            img.SetBackBuffer(Interop.D3DResourceType.IDirect3DSurface9, 0n)
            img.Unlock()

            match color with
                | Some b -> 
                    b.Dispose()
                    color <- None
                | _ -> ()
            
            match depth with
                | Some b -> 
                    ctx.Delete b
                    depth <- None
                | _ -> ()
    
    member private x.Start() =
        if not running then
            running <- true
            Log.warn "start"
            renderTimer.Start()

    override x.OnVisualParentChanged(oldParent) =
        let newParent = x.VisualParent

        match oldParent, newParent with
            | null, null -> ()
            | null, _ -> x.Start()
            | _, null -> x.Stop()
            | _ -> ()

        base.OnVisualParentChanged(oldParent)

    member x.ContextHandle = handle

    member x.FramebufferSignature = signature
    
    member x.Keyboard = keyboard :> IKeyboard
    member x.Mouse = mouse :> IMouse
    
    member x.RenderTask
        with get() = renderTask
        and set t = renderTask <- t

    member x.Sizes = size :> aval<_>
    
    member x.BeforeRender = beforeRender
    member x.AfterRender = afterRender
    
    member x.SubSampling
        with get() = 1.0
        and set v = if v <> 1.0 then failwith "[OpenGLSharing] SubSampling not implemented"

    member x.Cursor
        with get() = cursor
        and set c =
            if c <> cursor then
                cursor <- c
                match c with
                | Cursor.Default -> base.Cursor <- null
                | Cursor.None -> base.Cursor <- Input.Cursors.None
                | Cursor.Arrow -> base.Cursor <- Input.Cursors.Arrow
                | Cursor.Hand -> base.Cursor <- Input.Cursors.Hand
                | Cursor.Crosshair -> base.Cursor <- Input.Cursors.Cross
                | Cursor.HorizontalResize -> base.Cursor <- Input.Cursors.SizeWE
                | Cursor.VerticalResize -> base.Cursor <- Input.Cursors.SizeNS
                | Cursor.Text -> base.Cursor <- Input.Cursors.IBeam
                | Cursor.Custom _ -> Log.error "[WPF] custom cursors not supported atm."

    interface IRenderTarget with
        member x.FramebufferSignature = x.FramebufferSignature
        member x.Samples = 1
        member x.Runtime = runtime :> IRuntime
        member x.Time = time
        member x.RenderTask
            with get() = x.RenderTask
            and set t = x.RenderTask <- t
        member x.SubSampling
            with get() = x.SubSampling
            and set v = x.SubSampling <- v
        member x.Sizes = x.Sizes
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

    interface IRenderControl with
        member this.Cursor
            with get() = this.Cursor
            and set c = this.Cursor <- c
        member x.Mouse = mouse :> IMouse
        member x.Keyboard = keyboard :> IKeyboard
