namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering

[<Struct>] // TODO ref struct?
type RenderingLockDisposable =
    val mutable private handle : ValueOption<ContextHandle>
    val mutable private restore : ValueOption<ContextHandle>

    member x.Handle
        with get() = x.handle

    member x.Dispose() =
        match x.handle with
        | ValueSome h ->
            h.ReleaseCurrent()
            x.handle <- ValueNone

        | _ -> ()

        match x.restore with
        | ValueSome h ->
            h.MakeCurrent()
            x.restore <- ValueNone

        | _ -> ()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new (handle : ValueOption<ContextHandle>, restore : ValueOption<ContextHandle>) =
        { handle = handle
          restore = restore }


[<Struct>]
type ResourceLockDisposable =
    val mutable private handle : ValueOption<ContextHandle>
    val mutable private bag : ConcurrentBag<ContextHandle>
    val mutable private bagCount : SemaphoreSlim

    member x.Handle
        with get() = x.handle

    member x.Dispose() =
        match x.handle with
        | ValueSome h ->
            h.ReleaseCurrent()
            x.bag.Add(h)
            x.bagCount.Release() |> ignore

        | _ -> ()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new (handle : ValueOption<ContextHandle>, bag : ConcurrentBag<ContextHandle>, bagCount : SemaphoreSlim) =
        { handle = handle
          bag = bag
          bagCount = bagCount }

[<Struct>]
type ResourceLockDisposableOptional(inner : ResourceLockDisposable, success : bool) =
    static let invalid = new ResourceLockDisposableOptional(new ResourceLockDisposable(), false)
    static member Invalid = invalid
    member x.Success = success
    member x.Dispose() = inner.Dispose()
    interface IDisposable with
        member x.Dispose() = x.Dispose()

type MemoryUsage() =
    class
        [<DefaultValue>] val mutable public TextureCount : int
        [<DefaultValue>] val mutable public TextureMemory : int64
        [<DefaultValue>] val mutable public TextureViewCount : int

        [<DefaultValue>] val mutable public RenderBufferCount : int
        [<DefaultValue>] val mutable public RenderBufferMemory : int64

        [<DefaultValue>] val mutable public BufferCount : int
        [<DefaultValue>] val mutable public BufferMemory : int64

        [<DefaultValue>] val mutable public UniformBufferCount : int
        [<DefaultValue>] val mutable public UniformBufferMemory : int64

        [<DefaultValue>] val mutable public UniformPoolCount : int
        [<DefaultValue>] val mutable public UniformPoolMemory : int64

        [<DefaultValue>] val mutable public UniformBufferViewCount : int
        [<DefaultValue>] val mutable public UniformBufferViewMemory : int64

        [<DefaultValue>] val mutable public PhysicalVertexArrayObjectCount : int
        [<DefaultValue>] val mutable public VirtualVertexArrayObjectCount : int
        [<DefaultValue>] val mutable public PhysicalFramebufferCount : int
        [<DefaultValue>] val mutable public VirtualFramebufferCount : int

        [<DefaultValue>] val mutable public ShaderProgramCount : int
        [<DefaultValue>] val mutable public SamplerCount : int


        static member private MemoryString(m : int64) =
            if m > (1L <<< 30) then
                float m / float (1 <<< 30) |> sprintf "%.3fGB"
            elif m > (1L <<< 20) then
                float m / float (1 <<< 20) |> sprintf "%.2fMB"
            elif m > (1L <<< 10) then
                float m / float (1 <<< 10) |> sprintf "%.2fkB"
            else
                m |> sprintf "%db"
                
        override x.ToString() =
            let mutable anyNonZero = false
            let b = System.Text.StringBuilder()

            b.Append("MemoryUsage {\r\n") |> ignore
            if x.TextureCount <> 0 then
                b.AppendFormat("    TextureCount        = {0}\r\n", x.TextureCount) |> ignore
                b.AppendFormat("    TextureMemory       = {0}\r\n", MemoryUsage.MemoryString x.TextureMemory) |> ignore
                anyNonZero <- true

            if x.RenderBufferCount <> 0 then
                b.AppendFormat("    RenderBufferCount   = {0}\r\n", x.RenderBufferCount) |> ignore
                b.AppendFormat("    RenderBufferMemory  = {0}\r\n", MemoryUsage.MemoryString x.RenderBufferMemory) |> ignore
                anyNonZero <- true

            if x.BufferCount <> 0 then
                b.AppendFormat("    BufferCount         = {0}\r\n", x.BufferCount) |> ignore
                b.AppendFormat("    BufferMemory        = {0}\r\n", MemoryUsage.MemoryString x.BufferMemory) |> ignore
                anyNonZero <- true

            if x.UniformBufferCount <> 0 then
                b.AppendFormat("    UniformBufferCount  = {0}\r\n", x.UniformBufferCount) |> ignore
                b.AppendFormat("    UniformBufferMemory = {0}\r\n", MemoryUsage.MemoryString x.UniformBufferMemory) |> ignore
                anyNonZero <- true

            if x.UniformPoolCount <> 0 then
                b.AppendFormat("    UniformPoolCount    = {0}\r\n", x.UniformPoolCount) |> ignore
                b.AppendFormat("    UniformPoolMemory   = {0}\r\n", MemoryUsage.MemoryString x.UniformPoolMemory) |> ignore
                anyNonZero <- true

            if x.UniformBufferViewCount <> 0 then
                b.AppendFormat("    UniformViewCount    = {0}\r\n", x.UniformBufferViewCount) |> ignore
                b.AppendFormat("    UniformViewMemory   = {0}\r\n", MemoryUsage.MemoryString x.UniformBufferViewMemory) |> ignore
                anyNonZero <- true

            if x.VirtualVertexArrayObjectCount <> 0 || x.PhysicalVertexArrayObjectCount <> 0 then
                b.AppendFormat("    VertexArrayObjects  = {0} ({1})\r\n", x.VirtualVertexArrayObjectCount, x.PhysicalVertexArrayObjectCount) |> ignore
                anyNonZero <- true

            if x.VirtualFramebufferCount <> 0 || x.PhysicalFramebufferCount <> 0 then
                b.AppendFormat("    Framebuffers        = {0} ({1})\r\n", x.VirtualFramebufferCount, x.PhysicalFramebufferCount) |> ignore
                anyNonZero <- true

            if x.ShaderProgramCount <> 0 then
                b.AppendFormat("    ShaderPrograms      = {0}\r\n", x.ShaderProgramCount) |> ignore
                anyNonZero <- true

            if x.SamplerCount <> 0 then
                b.AppendFormat("    Samplers            = {0}\r\n", x.SamplerCount) |> ignore
                anyNonZero <- true

            b.Append "}" |> ignore


            if anyNonZero then
                b.ToString()
            else
                "MemoryUsage { Empty }"
    end

/// <summary>
/// Context is the core datastructure for managing implicit state
/// of the OpenGL implementation. 
/// OpenGL internally uses some sort of command queue being current
/// for one single thread at a time. Since it is very cumbersome to
/// manage this implicit state manually Context provides methods 
/// simplifying this task.
/// Note that Context may contain several "GraphicsContexts" allowing
/// multiple threads to submit GL calls concurrently.
/// </summary>
[<AllowNullLiteral>]
type Context(runtime : IRuntime, createContext : ContextHandle option -> ContextHandle) as this =

    static let defaultShaderCachePath =
        Path.combine [
            CachingProperties.CacheDirectory
            "Shaders"
            "OpenGL"
        ]

    // Hidden unused context for sharing.
    // Note: If None is passed to createContext, it's up to the implementation how to choose the parent.
    let parentContext =
        if RuntimeConfig.RobustContextSharing then Some <| createContext None
        else None

    let resourceContexts =
        let n = max 1 RuntimeConfig.NumberOfResourceContexts
        Array.init n (fun _ -> createContext parentContext)

    let memoryUsage = MemoryUsage()

    let bag = ConcurrentBag(resourceContexts)
    let bagCount = new SemaphoreSlim(resourceContexts.Length)

    let shaderCache = new ShaderCache()

    let mutable isDisposed = 0

    let mutable driverInfo : Option<Driver> = None

    let mutable packAlignment : Option<int> = None

    let mutable unpackAlignment : Option<int> = None

    let mutable maxTextureSize : Option<V2i> = None

    let mutable maxTextureSize3d : Option<V3i> = None

    let mutable maxTextureSizeCube : Option<int> = None

    let mutable maxTextureArrayLayers : Option<int> = None

    let mutable maxRenderbufferSize : Option<V2i> = None

    let mutable maxComputeWorkGroupSize : Option<V3i> = None

    let mutable maxComputeWorkGroupInvocations : Option<int> = None

    let mutable numProgramBinaryFormats : Option<int> = None

    let mutable maxSamples : Option<int> = None

    let mutable maxColorTextureSamples : Option<int> = None

    let mutable maxIntegerSamples : Option<int> = None

    let mutable maxDepthTextureSamples : Option<int> = None

    let mutable maxFramebufferSamples : Option<int> = None

    let mutable shaderCachePath : Option<string> = Some defaultShaderCachePath

    let formatSampleCounts = FastConcurrentDict()

    let mipmapGenerationSupport = FastConcurrentDict()

    let sharedMemoryManager = SharedMemoryManager(fun _ -> this.ResourceLock)

    let debugLabelsEnabled = (runtime.DebugConfig :?> DebugConfig).DebugLabels

    let getOrQuery (description : string) (var : byref<'T option>) (query : unit -> 'T) =
        match var with
        | None ->
            use __ = this.ResourceLock
            let value = query()
            GL.Check $"Failed to query {description}"
            var <- Some value
            value
        | Some v -> v

    [<Obsolete("Use overload with createContext that accepts an optional parent context.")>]
    new (runtime : IRuntime, createContext : unit -> ContextHandle) =
        new Context(runtime, fun (_ : ContextHandle option) -> createContext())

    /// <summary>
    /// Creates custom OpenGl context. Usage:
    /// let customCtx = app.Context.CreateContext()
    /// use __ = app.Context.RenderingLock(customCtx)
    /// </summary>
    member x.CreateContext() = createContext parentContext

    member internal x.ShaderCache = shaderCache

    // TODO: Why is this an option?
    static member DefaultShaderCachePath = Some defaultShaderCachePath

    member x.ShaderCachePath
        with get() = shaderCachePath
        and set p = shaderCachePath <- p

    member x.MemoryUsage = memoryUsage

    member x.Runtime = runtime

    member x.IsDisposed = isDisposed = 1

    member x.Driver =
        getOrQuery "driver info" &driverInfo Driver.readInfo

    member x.PackAlignment =
        getOrQuery "pack alignment" &packAlignment (fun _ ->
            GL.GetInteger(GetPName.PackAlignment)
        )

    member x.UnpackAlignment =
        getOrQuery "unpack alignment" &unpackAlignment (fun _ ->
            GL.GetInteger(GetPName.UnpackAlignment)
        )

    member x.MaxTextureSize =
        getOrQuery "max texture size" &maxTextureSize (fun _ ->
            let s = GL.GetInteger(GetPName.MaxTextureSize)
            V2i s
        )

    member x.MaxTextureSize3D =
        getOrQuery "max 3D texture size" &maxTextureSize3d (fun _ ->
            let s = GL.GetInteger(GetPName.Max3DTextureSize)
            V3i s
        )

    member x.MaxTextureSizeCube =
        getOrQuery "max cube texture size" &maxTextureSizeCube (fun _ ->
            GL.GetInteger(GetPName.MaxCubeMapTextureSize)
        )

    member x.MaxTextureArrayLayers =
        getOrQuery "max texture array layers" &maxTextureArrayLayers (fun _ ->
            GL.GetInteger(GetPName.MaxArrayTextureLayers)
        )

    member x.MaxRenderbufferSize =
        getOrQuery "max renderbuffer size" &maxRenderbufferSize (fun _ ->
            let s = GL.GetInteger(GetPName.MaxRenderbufferSize)
            V2i s
        )

    member x.MaxComputeWorkGroupSize =
        getOrQuery "max compute work group size" &maxComputeWorkGroupSize (fun _ ->
            let mutable res = V3i.Zero
            GL.GetInteger(GetIndexedPName.MaxComputeWorkGroupSize, 0, &res.X)
            GL.GetInteger(GetIndexedPName.MaxComputeWorkGroupSize, 1, &res.Y)
            GL.GetInteger(GetIndexedPName.MaxComputeWorkGroupSize, 2, &res.Z)
            res
        )

    member x.MaxComputeWorkGroupInvocations =
        getOrQuery "max compute work group invocations" &maxComputeWorkGroupInvocations (fun _ ->
            GL.GetInteger(GetPName.MaxComputeWorkGroupInvocations)
        )

    member x.NumProgramBinaryFormats =
        getOrQuery "number of program binary formats" &numProgramBinaryFormats (fun _ ->
            GL.GetInteger(GetPName.NumProgramBinaryFormats)
        )

    member x.MaxSamples =
        getOrQuery "max samples" &maxSamples (fun _ ->
            GL.GetInteger(GetPName.MaxSamples)
        )

    member x.MaxColorTextureSamples =
        getOrQuery "max color texture samples" &maxColorTextureSamples (fun _ ->
            GL.GetInteger(GetPName.MaxColorTextureSamples)
        )

    member x.MaxIntegerSamples =
        getOrQuery "max integer samples" &maxIntegerSamples (fun _ ->
            GL.GetInteger(GetPName.MaxIntegerSamples)
        )

    member x.MaxDepthTextureSamples =
        getOrQuery "max depth texture samples" &maxDepthTextureSamples (fun _ ->
            GL.GetInteger(GetPName.MaxDepthTextureSamples)
        )

    member x.MaxFramebufferSamples =
        getOrQuery "max framebuffer samples" &maxFramebufferSamples (fun _ ->
            GL.GetInteger(unbox<GetPName> 0x9318)
        )

    member x.DebugLabelsEnabled = debugLabelsEnabled

    member internal x.ImportMemoryBlock(external : IExternalMemoryBlock) =
        sharedMemoryManager.Import external

    /// <summary>
    /// makes the given render context current providing a re-entrant
    /// behaviour useful for several things.
    /// WARNING: the given handle must not be one of the resource
    ///          handles implicitly created by the context.
    /// </summary>
    member x.RenderingLock(handle : ContextHandle) : RenderingLockDisposable =

        // ensure that lock is "re-entrant"
        match ContextHandle.Current with
        | ValueSome current ->

            if current = handle then
                // if the current token uses the same context as requested
                // we don't need to perform any operations here since
                // the outer token will take care of everything
                new RenderingLockDisposable()

            else
                // if the current token is using a different context
                // simply release it before obtaining the new token
                // and obtain it again after releasing this one.
                current.ReleaseCurrent()
                handle.MakeCurrent()

                // no release: handle.ReleaseCurrent, current.MakeCurrent(), reset currentHandle
                new RenderingLockDisposable(ValueSome handle, ValueSome current)

        | ValueNone ->
            // if there is no current token we must create a new
            // one obtaining/releasing the desired context.
            handle.MakeCurrent()

            // no release: handle.ReleaseCurrent, reset currentHandle ot None
            new RenderingLockDisposable(ValueSome handle, ValueNone)

    /// <summary>
    /// Makes one of the underlying context current on the calling thread
    /// and returns a disposable for releasing it again
    /// </summary>
    member x.ResourceLock : ResourceLockDisposable =

        // ensure that lock is "re-entrant"
        match ContextHandle.Current with
        | ValueSome _ ->
            // if the calling thread already posesses the token
            // simply return a dummy disposable and do no perform any operation
            new ResourceLockDisposable()

        | ValueNone ->
            // wait until there is at least one context in the bag
            bagCount.Wait()

            // take one context from the bag. Since the bagCount should be in
            // sync with the bag's actual count the exception should never
            // be reached.
            let handle =
                match bag.TryTake() with
                | (true, handle) -> handle
                | _ -> failf "could not dequeue resource-context"

            // make the obtained handle current
            handle.MakeCurrent()
            GL.GetError() |> ignore
            GL.Check("Error while making current.")

            // no release: put resource context back in bag and reset current to None
            new ResourceLockDisposable(ValueSome handle, bag, bagCount)

    /// <summary>
    /// Tries to make one of the underlying context current on the calling thread
    /// and returns a disposable for releasing it again. If the context was already disposed,
    /// the Success member of the returned disposable returns false.
    /// </summary>
    member x.TryResourceLock : ResourceLockDisposableOptional =
        try new ResourceLockDisposableOptional(x.ResourceLock, true)
        with :? ObjectDisposedException -> ResourceLockDisposableOptional.Invalid

    /// <summary>
    /// Returns the number of samples supported by the given target and format.
    /// </summary>
    member internal x.GetFormatSamples(target : ImageTarget, format : TextureFormat) =
        formatSampleCounts.GetOrCreate((target, format), fun _ ->
            let estimate() =
                let maxSamples =
                    [
                        x.MaxSamples

                        if format.IsColorRenderable then
                            x.MaxColorTextureSamples

                        if format.IsIntegerFormat then
                            x.MaxIntegerSamples

                        if format.HasDepth || format.HasStencil then
                            x.MaxDepthTextureSamples
                    ]
                    |> List.min
                    |> max 1

                Report.Line(3, $"[GL] Internal format queries not supported, assuming up to {maxSamples} are supported (target = {target}, format = {format})")

                [1; 2; 4; 8; 16; 32; 64]
                |> List.filter ((>=) maxSamples)
                |> Set.ofList

            if GL.ARB_internalformat_query then
                let format = TextureFormat.toSizedInternalFormat format

                let count = GL.Dispatch.GetInternalformat(target, format, InternalFormatParameter.NumSampleCounts)
                GL.Check "could not query number of sample counts"

                if count > 0 then
                    let buffer = GL.Dispatch.GetInternalformat(target, format, InternalFormatParameter.Samples, count)
                    GL.Check "could not query sample counts"

                    buffer
                    |> Set.ofArray
                    |> Set.add 1
                else
                    estimate()
            else
                estimate()
        )

    /// <summary>
    /// Returns the mipmap generation support for the given target and format.
    /// </summary>
    member internal x.GetFormatMipmapGeneration(target : ImageTarget, format : TextureFormat) =
        mipmapGenerationSupport.GetOrCreate((target, format), fun _ ->
            if GL.ARB_internalformat_query2 then
                let support = GL.Dispatch.GetInternalformatMipmapGenerationSupport(target, TextureFormat.toSizedInternalFormat format)
                GL.Check "could not query mipmap generation support"

                support
            else
                Log.warn "[GL] Internal format queries not supported, assuming mipmap generation is supported (target = %A, format = %A)" target format
                MipmapGenerationSupport.Full
        )

    /// Returns all errors reported by the debug output on the resource context handles.
    member x.GetDebugErrors() =
        resourceContexts |> Array.collect (fun h -> h.GetDebugErrors())

    member x.PrintDebug(typ: DebugType, severity: DebugSeverity, id: int, message: string) =
        match ContextHandle.Current with
        | ValueSome ctx -> ctx.PrintDebug(typ, severity, id, message)
        | _ -> ()

    member x.PushDebugGroup(message: string) =
        if debugLabelsEnabled then
            match ContextHandle.Current with
            | ValueSome ctx -> ctx.PushDebugGroup(message)
            | _ -> ()

    member x.PopDebugGroup() =
        if debugLabelsEnabled then
            match ContextHandle.Current with
            | ValueSome ctx -> ctx.PopDebugGroup()
            | _ -> ()

    member x.SetObjectLabel(id: ObjectLabelIdentifier, name: int, label: string) =
        if debugLabelsEnabled && name > 0 then
            use __ = x.ResourceLock
            ContextHandle.Current.Value.SetObjectLabel(id, name, label)

    member x.GetObjectLabel(id: ObjectLabelIdentifier, name: int) =
        if debugLabelsEnabled && name > 0 then
            use __ = x.ResourceLock
            ContextHandle.Current.Value.GetObjectLabel(id, name)
        else
            null

    /// <summary>
    /// releases all resources created by the context
    /// </summary>
    member x.Dispose() =
        if Interlocked.Exchange(&isDisposed, 1) = 0 then
            try
                shaderCache.Dispose()

                for c in resourceContexts do
                    ContextHandle.delete c

                parentContext |> Option.iter ContextHandle.delete
            with exn ->
                Log.error "[GL] Failed to dispose context: %A" exn

    interface IDisposable with
        member x.Dispose() = x.Dispose()
