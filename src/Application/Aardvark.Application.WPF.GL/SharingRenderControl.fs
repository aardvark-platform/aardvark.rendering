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

type ShareDevice =
    struct
        val mutable public Handle : nativeint

        member x.IsNull = x.Handle = 0n
    end

type ShareHandle =
    struct
        val mutable public Handle : nativeint
        member x.IsNull = x.Handle = 0n
    end

type WglDXAccess =
    | ReadOnly = 0x0000 
    | ReadWrite = 0x0001
    | WriteDiscard = 0x0002

type WglDXSetResourceShareHandleNVDel = delegate of nativeint * nativeint -> bool
type WglDXOpenDeviceNVDel = delegate of nativeint -> ShareDevice
type WglDXCloseDeviceNVDel = delegate of ShareDevice -> bool
type WglDXRegisterObjectNVDel = delegate of ShareDevice * nativeint * int * All * WglDXAccess -> ShareHandle
type WglDXUnregisterObjectNVDel = delegate of ShareDevice * ShareHandle -> bool
type WglDXObjectAccessNVDel = delegate of ShareHandle * WglDXAccess -> bool
type WglDXLockObjectsNVDel = delegate of ShareDevice * int * nativeptr<ShareHandle> -> bool
type WglDXUnlockObjectsNVDel = delegate of ShareDevice * int * nativeptr<ShareHandle> -> bool


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


type WGLDX(handle : ContextHandle) =
    
    let mutable wglDXSetResourceShareHandleNV : WglDXSetResourceShareHandleNVDel = null
    let mutable wglDXOpenDeviceNV : WglDXOpenDeviceNVDel = null
    let mutable wglDXCloseDeviceNV : WglDXCloseDeviceNVDel = null
    let mutable wglDXRegisterObjectNV : WglDXRegisterObjectNVDel = null
    let mutable wglDXUnregisterObjectNV : WglDXUnregisterObjectNVDel = null
    let mutable wglDXObjectAccessNV : WglDXObjectAccessNVDel = null
    let mutable wglDXLockObjectsNV : WglDXLockObjectsNVDel = null
    let mutable wglDXUnlockObjectsNV : WglDXUnlockObjectsNVDel = null

    let l = obj()
    let mutable loaded = false

    let load() = 
        lock l (fun () ->
            if not loaded then
                loaded <- true
                handle.MakeCurrent()

                wglDXSetResourceShareHandleNV <- handle.Import "wglDXSetResourceShareHandleNV"
                wglDXOpenDeviceNV <- handle.Import "wglDXOpenDeviceNV"
                wglDXCloseDeviceNV <- handle.Import "wglDXCloseDeviceNV"
                wglDXRegisterObjectNV <- handle.Import "wglDXRegisterObjectNV"
                wglDXUnregisterObjectNV <- handle.Import "wglDXUnregisterObjectNV"
                wglDXObjectAccessNV <- handle.Import "wglDXObjectAccessNV"
                wglDXLockObjectsNV <- handle.Import "wglDXLockObjectsNV"
                wglDXUnlockObjectsNV <- handle.Import "wglDXUnlockObjectsNV"

                handle.ReleaseCurrent()
        )


    member x.SetResourceShareHandle(dxObject, shareHandle) =
        load()
        wglDXSetResourceShareHandleNV.Invoke(dxObject, shareHandle)
        
    member x.OpenDevice(dxDevice) =
        load()
        handle.MakeCurrent()
        let dev = wglDXOpenDeviceNV.Invoke(dxDevice)
        handle.ReleaseCurrent()
        if dev.IsNull then Log.warn "[DX] could not open share device"
        dev

    member x.CloseDevice(hDevice : ShareDevice) =
        load()
        if not hDevice.IsNull then
            let worked = wglDXCloseDeviceNV.Invoke(hDevice)
            if not worked then Log.warn "[DX] could not close share device"

    member x.RegisterObject(hDevice, dxObject, glName, glType, access) =
        load()
        let res = wglDXRegisterObjectNV.Invoke(hDevice, dxObject, glName, glType, access)
        if res.IsNull then Log.warn "[DX] could not register share object of type %A (%d)" glType glName
        res

    member x.Unregister(hDevice, hStuff) =
        load()
        let res = wglDXUnregisterObjectNV.Invoke(hDevice, hStuff)
        if not res  then Log.warn "[DX] could not unregister share object"
        res
    member x.ObjectAccess(hObject, access) =
        load()
        wglDXObjectAccessNV.Invoke(hObject, access)

    member x.LockObjects(hDevice : ShareDevice, objects : ShareHandle[]) =
        load()
        let pObjects = NativePtr.stackUse objects
        wglDXLockObjectsNV.Invoke(hDevice, objects.Length, pObjects)
        

    member x.UnlockObjects(hDevice : ShareDevice, objects : ShareHandle[]) =
        load()
        let pObjects = NativePtr.stackUse objects
        wglDXUnlockObjectsNV.Invoke(hDevice, objects.Length, pObjects)
        

open System.Security
module WindowsStuff =

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern nativeint GetDesktopWindow()

[<AllowNullLiteral>]
type SharedTexture(ctx : Context, dxDev : SharpDX.Direct3D9.DeviceEx, signature : FramebufferSignature, shareDevice : ShareDevice, sharing : WGLDX, size : V2i, glDepth : int) =

    static let dxFormat = SharpDX.Direct3D9.Format.X8R8G8B8
    static let glFormat = SizedInternalFormat.Rgba8

    let mutable sharedHandle = 0n
    let dxTexture =
        new SharpDX.Direct3D9.Texture(
            dxDev, size.X, size.Y, 1, 
            SharpDX.Direct3D9.Usage.RenderTarget, 
            dxFormat, 
            SharpDX.Direct3D9.Pool.Default,
            &sharedHandle
        )

    let glTexture = GL.GenTexture()
    do  GL.BindTexture(TextureTarget.Texture2D, glTexture)
        GL.TexStorage2D(TextureTarget2d.Texture2D, 1, glFormat, size.X, size.Y)
        GL.BindTexture(TextureTarget.Texture2D, 0)

    let glFbo = GL.GenFramebuffer()
    do
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, glFbo)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, glTexture, 0)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, glDepth)
        let status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
        if status <> FramebufferErrorCode.FramebufferComplete then
            Log.warn "[GL] framebuffer incomplete: %A" status
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)

    let fbo =
        let color = { texture = Texture(ctx, glTexture, TextureDimension.Texture2D, 1, 1, V3i(size.X, size.Y, 1), None, TextureFormat.Rgba8, 0L, true); slice = 0; level = 0 } :> IFramebufferOutput
        let depth = Renderbuffer(ctx, glDepth, size, RenderbufferFormat.Depth24Stencil8, 1, 0L) :> IFramebufferOutput
        new Framebuffer(ctx, signature, (fun _ -> glFbo), ignore, [0, DefaultSemantic.Colors, color], Some depth)

    do sharing.SetResourceShareHandle(dxTexture.NativePointer, sharedHandle) |> ignore
    let shareHandle = sharing.RegisterObject(shareDevice, dxTexture.NativePointer, glTexture, All.Texture2D, WglDXAccess.WriteDiscard)

    member x.Lock() =
        sharing.LockObjects(shareDevice, [| shareHandle |])

    member x.Unlock() =
        sharing.UnlockObjects(shareDevice, [| shareHandle |])

    member x.Framebuffer = fbo

    member x.DXSurface = dxTexture.GetSurfaceLevel(0)

    member x.Dispose() =
        GL.DeleteFramebuffer(glFbo)
        GL.DeleteTexture(glTexture)
        dxTexture.Dispose()

[<AllowNullLiteral>]
type SharedSurface(ctx : Context, dxDev : SharpDX.Direct3D9.DeviceEx, signature : FramebufferSignature, shareDevice : ShareDevice, sharing : WGLDX, size : V2i) =

    static let dxFormat = SharpDX.Direct3D9.Format.A8R8G8B8
    static let glFormat = SizedInternalFormat.Rgba8

    let mutable sharedHandle = 0n
    let dxSurface =
        SharpDX.Direct3D9.Surface.CreateRenderTarget(
            dxDev,
            size.X, size.Y, 
            dxFormat,
            SharpDX.Direct3D9.MultisampleType.None, 0,
            true,
            &sharedHandle
        )
//        new SharpDX.Direct3D9.Texture(
//            dxDev, size.X, size.Y, 1, 
//            SharpDX.Direct3D9.Usage.RenderTarget, 
//            dxFormat, 
//            SharpDX.Direct3D9.Pool.Default,
//            &sharedHandle
//        )

    let glDepth = GL.GenRenderbuffer()
    do  GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, glDepth)
        GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, 1, RenderbufferStorage.Depth24Stencil8, size.X, size.Y)
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)

    let glBuffer= GL.GenRenderbuffer()
    do  GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, glBuffer)
        GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, 1, RenderbufferStorage.Rgba8, size.X, size.Y)
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)


    do GL.Flush()
       GL.Finish()
       let ok = sharing.SetResourceShareHandle(dxSurface.NativePointer, sharedHandle) 
       if not ok then Log.warn "[GL] SetResourceShareHandle failed"
    let shareHandle = sharing.RegisterObject(shareDevice, dxSurface.NativePointer, glBuffer, All.Renderbuffer, WglDXAccess.WriteDiscard)

    let glFbo = GL.GenFramebuffer()
    do
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, glFbo)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, glBuffer)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, glDepth)
        let status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
        if status <> FramebufferErrorCode.FramebufferComplete then
            Log.warn "[GL] framebuffer incomplete: %A" status
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)

    let fbo =
        let color = Renderbuffer(ctx, glBuffer, size, RenderbufferFormat.Rgba8, 1, 0L) :> IFramebufferOutput
        let depth = Renderbuffer(ctx, glDepth, size, RenderbufferFormat.Depth24Stencil8, 1, 0L) :> IFramebufferOutput
        new Framebuffer(ctx, signature, (fun _ -> glFbo), ignore, [0, DefaultSemantic.Colors, color], Some depth)

    member x.Size = size

    member x.Lock() =
        GL.Flush()
        GL.Finish()
        sharing.LockObjects(shareDevice, [| shareHandle |])

    member x.Unlock() =
        GL.Flush()
        GL.Finish()
        sharing.UnlockObjects(shareDevice, [| shareHandle |])

    member x.Framebuffer = fbo

    member x.DXSurface = dxSurface

    member x.Dispose() =
        sharing.Unregister(shareDevice, shareHandle) |> ignore
        GL.DeleteFramebuffer(glFbo)
        GL.DeleteRenderbuffer(glBuffer)
        GL.DeleteRenderbuffer(glDepth)
        dxSurface.Dispose()


type OpenGlSharingRenderControl(runtime : Runtime) as this =
    inherit ContentControl()

    let ctx = runtime.Context
    let handle = ContextHandle.create(true)
    let sharing = WGLDX(handle)

    let dx = new SharpDX.Direct3D9.Direct3DEx()
    let hndl = WindowsStuff.GetDesktopWindow()
    let p = SharpDX.Direct3D9.PresentParameters(Windowed=SharpDX.Mathematics.Interop.RawBool(true),SwapEffect = SharpDX.Direct3D9.SwapEffect.Discard,DeviceWindowHandle = hndl, PresentationInterval = SharpDX.Direct3D9.PresentInterval.Immediate)
    let dxDev = new SharpDX.Direct3D9.DeviceEx(dx, 0, SharpDX.Direct3D9.DeviceType.Hardware, 0n, SharpDX.Direct3D9.CreateFlags.FpuPreserve ||| SharpDX.Direct3D9.CreateFlags.HardwareVertexProcessing ||| SharpDX.Direct3D9.CreateFlags.Multithreaded, p)
    let shareDevice = sharing.OpenDevice(dxDev.NativePointer)


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
            Map.ofList [0, (DefaultSemantic.Colors, { samples = 1; format = RenderbufferFormat.Rgba8 })], 
            Map.empty, 
            Some { samples = 1; format = RenderbufferFormat.Depth24Stencil8 }, 
            None
        )

    let createSharedTexture(size : V2i) =
        SharedSurface(ctx, dxDev, signature, shareDevice, sharing, size)

    let startTime = DateTime.Now
    let sw = System.Diagnostics.Stopwatch.StartNew()
    let time = Mod.custom (fun _ -> startTime + sw.Elapsed)

    let mutable ping = Unchecked.defaultof<SharedSurface>
    let mutable pong = Unchecked.defaultof<SharedSurface>

    let renderLock = obj()

    let render (size : V2i) (action : Framebuffer -> unit) =
        use __ = ctx.RenderingLock(handle)
            
        ping <-
            match ping with
                | null -> 
                    Log.warn "init %A" size
                    createSharedTexture size
                | o when o.Size = size -> o
                | o ->
                    Log.warn "resize to %A" size
                    o.Dispose()
                    createSharedTexture size

        let locked = ping.Lock()
        if not locked then
            Log.warn "[DX] could not lock dx texture"

        
        action ping.Framebuffer
      
        let unlocked = ping.Unlock()
        if not unlocked then
            Log.warn "[DX] could not unlock dx texture"

    let renderTimer = 
        let doit s e =
            if System.Threading.Interlocked.Exchange(&pending, 0) = 1 then
                lock renderLock (fun () ->
                    let s = V2i(this.ActualWidth, this.ActualHeight)
                    if s.AllDifferent 0 then
                        img.Lock()
                        transact (fun () -> size.Value <- s)

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

                        img.SetBackBuffer(Interop.D3DResourceType.IDirect3DSurface9, ping.DXSurface.NativePointer)
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