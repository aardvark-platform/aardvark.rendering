namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering

[<Struct>] // TODO ref struct?
type RenderingLockDisposable =
    
    val mutable handle : ValueOption<ContextHandle>
    val mutable restore : ValueOption<ContextHandle>
    val mutable current : ThreadLocal<ValueOption<ContextHandle>>
    
    member x.Handle
        with get() = x.handle

    member x.Dispose() =
        if not (isNull x.current) then // check if nop disposable
            match x.handle with 
            | ValueSome h -> h.ReleaseCurrent()
                             x.handle <- ValueNone
            | _ -> ()

            match x.restore with
            | ValueSome h -> h.MakeCurrent()
                             x.current.Value <- ValueSome h
                             x.restore <- ValueNone
            | _ -> x.current.Value <- ValueNone

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new (handle : ValueOption<ContextHandle>, restore : ValueOption<ContextHandle>, current : ThreadLocal<ValueOption<ContextHandle>>) =
        {
            handle = handle
            restore = restore
            current = current
        }


[<Struct>]
type ResourceLockDisposable =
    
    val mutable handle : ValueOption<ContextHandle>
    val mutable bag : ConcurrentBag<ContextHandle>
    val mutable bagCount : SemaphoreSlim
    val mutable current : ThreadLocal<ValueOption<ContextHandle>>
    
    member x.Handle
        with get() = x.handle

    member x.Dispose() =
        match x.handle with 
        | ValueSome h -> h.ReleaseCurrent()
                         x.bag.Add(h)
                         x.bagCount.Release() |> ignore
                         x.current.Value <- ValueNone
        | _ -> ()


    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new (handle : ValueOption<ContextHandle>, bag : ConcurrentBag<ContextHandle>, bagCount : SemaphoreSlim, current : ThreadLocal<ValueOption<ContextHandle>>) =
        {
            handle = handle
            bag = bag
            bagCount = bagCount
            current = current
        }


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
type Context(runtime : IRuntime, createContext : unit -> ContextHandle) as this =

    static let defaultShaderCachePath = 
        Path.combine [
            CachingProperties.CacheDirectory
            "Shaders"
            "OpenGL"
        ]

    let resourceContexts = Array.init Config.NumberOfResourceContexts (fun _ -> createContext())
    let resourceContextCount = resourceContexts.Length

    let memoryUsage = MemoryUsage()

    let bag = ConcurrentBag(resourceContexts)
    let bagCount = new SemaphoreSlim(resourceContextCount)

    let currentHandle = new ThreadLocal<ValueOption<ContextHandle>>(fun () -> ValueNone)

    let shaderCache = ShaderCache()

    let mutable driverInfo : Option<Driver> = None

    let mutable packAlignment : Option<int> = None

    let mutable unpackAlignment : Option<int> = None

    let mutable maxComputeWorkGroupSize : Option<V3i> = None

    let mutable maxComputeWorkGroupInvocations : Option<int> = None

    let mutable shaderCachePath : Option<string> = Some defaultShaderCachePath

    let formatSampleCounts = FastConcurrentDict()

    let mipmapGenerationSupport = FastConcurrentDict()

    let sharedMemoryManager = SharedMemoryManager(fun _ -> this.ResourceLock)

    let getOrQuery (var : byref<'T option>) (query : unit -> 'T) =
        match var with
        | None ->
            use __ = this.ResourceLock
            let value = query()
            var <- Some value
            value
        | Some v -> v

    /// <summary>
    /// Creates custom OpenGl context. Usage:
    /// let customCtx = app.Context.CreateContext()
    /// use __ = app.Context.RenderingLock(customCtx)
    /// </summary>
    member x.CreateContext() = createContext()

    member internal x.ShaderCache = shaderCache

    // TODO: Why is this an option?
    static member DefaultShaderCachePath = Some defaultShaderCachePath

    member x.ShaderCachePath
        with get() = shaderCachePath
        and set p = shaderCachePath <- p

    member x.MemoryUsage = memoryUsage

    member x.CurrentContextHandle
        with get() = currentHandle.Value

    member x.Runtime = runtime

    member x.Driver =
        getOrQuery &driverInfo Driver.readInfo

    member x.PackAlignment =
        getOrQuery &packAlignment (fun _ ->
            GL.GetInteger(GetPName.PackAlignment)
        )

    member x.UnpackAlignment =
        getOrQuery &unpackAlignment (fun _ ->
            GL.GetInteger(GetPName.UnpackAlignment)
        )

    member x.MaxComputeWorkGroupSize =
        getOrQuery &maxComputeWorkGroupSize (fun _ ->
            let arr = Array.zeroCreate<int> 3
            GL.GetInteger(GetPName.MaxComputeWorkGroupSize, arr)
            V3i arr
        )

    member x.MaxComputeWorkGroupInvocations =
        getOrQuery &maxComputeWorkGroupInvocations (fun _ ->
            GL.GetInteger(GetPName.MaxComputeWorkGroupInvocations)
        )

    member internal x.ImportMemoryBlock(external : ExternalMemoryBlock) =
        sharedMemoryManager.Import external

    /// <summary>
    /// makes the given render context current providing a re-entrant
    /// behaviour useful for several things.
    /// WARNING: the given handle must not be one of the resource
    ///          handles implicitly created by the context.
    /// </summary>
    member x.RenderingLock(handle : ContextHandle) : RenderingLockDisposable =

        // ensure that lock is "re-entrant"
        match currentHandle.Value with
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
                    currentHandle.Value <- ValueSome handle

                    // no release: handle.ReleaseCurrent, current.MakeCurrent(), reset currentHandle
                    new RenderingLockDisposable(ValueSome handle, ValueSome current, currentHandle)
  


            | ValueNone ->
                // if there is no current token we must create a new
                // one obtaining/releasing the desired context.

                handle.MakeCurrent()
                currentHandle.Value <- ValueSome handle

                // no release: handle.ReleaseCurrent, reset currentHandle ot None
                new RenderingLockDisposable(ValueSome handle, ValueNone, currentHandle)
                    
                    
    /// <summary>
    /// makes one of the underlying context current on the calling thread
    /// and returns a disposable for releasing it again
    /// </summary>
    member x.ResourceLock : ResourceLockDisposable =

        // ensure that lock is "re-entrant"
        match currentHandle.Value with
            | ValueSome _ ->
                // if the calling thread already posesses the token
                // simply return a dummy disposable and do no perform any operation
                new ResourceLockDisposable()

            | ValueNone -> 
                // create a token for the obtained context

                // wait until there is at least one context in the bag
                bagCount.Wait()

                // take one context from the bag. Since the bagCount should be in
                // sync with the bag's actual count the exception should never
                // be reached.
                let handle = 
                    match bag.TryTake() with
                        | (true, handle) -> handle
                        | _ -> failwith "could not dequeue resource-context"

                // make the obtained handle current
                handle.MakeCurrent()
                GL.GetError() |> ignore

                GL.Check("Error while making current.")

                // store the token as current
                currentHandle.Value <- ValueSome handle
                
                // no release: put resource context back in bag and reset current to None
                new ResourceLockDisposable(ValueSome handle, bag, bagCount, currentHandle)


    /// <summary>
    /// Returns the number of samples supported by the given target and format.
    /// </summary>
    member internal x.GetFormatSamples(target : ImageTarget, format : TextureFormat) =
        if GL.ARB_internalformat_query then
            formatSampleCounts.GetOrCreate((target, format), fun _ ->
                let format = TextureFormat.toSizedInternalFormat format

                let count = GL.Dispatch.GetInternalformat(target, format, InternalFormatParameter.NumSampleCounts)
                GL.Check "could not query number of sample counts"

                if count > 0 then
                    let buffer = GL.Dispatch.GetInternalformat(target, format, InternalFormatParameter.Samples, count)
                    GL.Check "could not query sample counts"

                    Set.ofArray buffer
                else
                    Set.empty
            )
        else
            Log.warn "[GL] Internal format queries not supported, assuming all sample counts are supported"
            Set.ofList [1; 2; 4; 8; 16; 32; 64]

    /// <summary>
    /// Returns the mipmap generation support for the given target and format.
    /// </summary>
    member internal x.GetFormatMipmapGeneration(target : ImageTarget, format : TextureFormat) =
        if GL.ARB_internalformat_query2 then
            mipmapGenerationSupport.GetOrCreate((target, format), fun _ ->
                let support = GL.Dispatch.GetInternalformatMipmapGenerationSupport(target, TextureFormat.toSizedInternalFormat format)
                GL.Check "could not query mipmap generation support"

                support
            )
        else
            Log.warn "[GL] Internal format queries not supported, assuming mipmap generation is supported"
            MipmapGenerationSupport.Full

    /// <summary>
    /// releases all resources created by the context
    /// </summary>
    member x.Dispose() =
        try
//            bagCount.Wait()
//            let handle = 
//                match bag.TryTake() with
//                    | (true, handle) -> handle
//                    | _ -> failwith "could not dequeue resource-context"
//            handle.MakeCurrent()
                
            for i in 0..resourceContextCount-1 do
                let s = resourceContexts.[i]
                ContextHandle.delete s
        with _ ->
            ()
            
    interface IDisposable with
        member x.Dispose() = x.Dispose()
