namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL
open Aardvark.Base

type private ContextToken(obtain : ContextToken -> ContextHandle, release : ContextToken -> unit) as this =
    let mutable handle = None
    let mutable isObtained = false

    do this.Obtain()

    member x.Handle
        with get() = handle
        and set h = handle <- h

    member x.Release() = 
        isObtained <- false
        release x
        //handle <- None

    member x.Obtain() = 
        handle <- Some <| obtain x
        isObtained <- true

    member x.Dispose() =
        if isObtained then
            x.Release()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


type MemoryUsage() =
    class
        [<DefaultValue>] val mutable public TextureCount : int
        [<DefaultValue>] val mutable public TextureMemory : int64

        [<DefaultValue>] val mutable public BufferCount : int
        [<DefaultValue>] val mutable public BufferMemory : int64

        [<DefaultValue>] val mutable public UniformBufferCount : int
        [<DefaultValue>] val mutable public UniformBufferMemory : int64

        [<DefaultValue>] val mutable public UniformPoolCount : int
        [<DefaultValue>] val mutable public UniformPoolMemory : int64

        [<DefaultValue>] val mutable public PhysicalVertexArrayObjectCount : int
        [<DefaultValue>] val mutable public VirtualVertexArrayObjectCount : int
        [<DefaultValue>] val mutable public ShaderProgramCount : int
        [<DefaultValue>] val mutable public SamplerStateCount : int
        [<DefaultValue>] val mutable public PhysicalFramebufferCount : int
        [<DefaultValue>] val mutable public VirtualFramebufferCount : int
        [<DefaultValue>] val mutable public RenderBufferCount : int
        [<DefaultValue>] val mutable public RenderBufferMemory : int64
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
type Context(runtime : IRuntime, resourceContextCount : int) =
    static let nopDisposable = { new IDisposable with member x.Dispose() = () }
    let resourceContexts = ContextHandle.createContexts resourceContextCount
    let resourceContextCount = resourceContexts.Length

    let memoryUsage = MemoryUsage()

    let bag = ConcurrentBag(resourceContexts)
    let bagCount = new SemaphoreSlim(resourceContextCount)
    let renderingContexts = ConcurrentDictionary<ContextHandle, SemaphoreSlim>()

    let currentToken = new ThreadLocal<Option<ContextToken>>(fun () -> None)
    
    let mutable driverInfo = None

    let mutable packAlignment = None

    member x.MemoryUsage = memoryUsage

    member x.CurrentContextHandle
        with get() =  currentToken.Value.Value.Handle
        and set ctx =
            match ctx with
                | Some ctx ->
                    currentToken.Value <- Some <| new ContextToken((fun _ -> ctx), ignore)
                | None ->
                    currentToken.Value <- None

    member x.Runtime = runtime

    member x.Driver =
        match driverInfo with
            | None ->
                let v = Driver.readInfo()
                driverInfo <- Some v
                v
            | Some v -> v

    member x.PackAlignment =
        match packAlignment with
            | Some p -> p
            | None ->
                let p = 
                    using x.ResourceLock (fun _ ->
                        GL.GetInteger(GetPName.PackAlignment)
                    )
                packAlignment <- Some p
                p

    /// <summary>
    /// makes the given render context current providing a re-entrant
    /// behaviour useful for several things.
    /// WARNING: the given handle must not be one of the resource
    ///          handles implicitly created by the context.
    /// </summary>
    member x.RenderingLock(handle : ContextHandle) : IDisposable =
        let sem = renderingContexts.GetOrAdd(handle, fun _ -> new SemaphoreSlim(1))

        // ensure that lock is "re-entrant"
        match currentToken.Value with
            | Some token ->

                if token.Handle.Value = handle then
                    // if the current token uses the same context as requested
                    // we don't need to perform any operations here since
                    // the outer token will take care of everything
                    nopDisposable

                else
                    // if the current token is using a different context
                    // simply release it before obtaining the new token
                    // and obtain it again after releasing this one.
                    new ContextToken (
                        ( fun x ->
                            token.Release()
                            handle.MakeCurrent()
                            currentToken.Value <- Some x
                            handle),
                        ( fun x ->
                            handle.ReleaseCurrent()
                            currentToken.Value <- None
                            token.Obtain())
                    ) :> _
  


            | None ->
                // if there is no current token we must create a new
                // one obtaining/releasing the desired context.
                new ContextToken (
                    ( fun x ->
                        handle.MakeCurrent()
                        currentToken.Value <- Some x
                        handle),
                    ( fun x ->
                        handle.ReleaseCurrent()
                        currentToken.Value <- None
                        ())
                ) :> _
                    
    /// <summary>
    /// makes one of the underlying context current on the calling thread
    /// and returns a disposable for releasing it again
    /// </summary>
    member x.ResourceLock : IDisposable =

        // ensure that lock is "re-entrant"
        match currentToken.Value with
            | Some token ->
                // if the calling thread already posesses the token
                // simply return a dummy disposable and do no perform any operation
                nopDisposable

            | None -> 
                // create a token for the obtained context
                new ContextToken(
                    ( fun x ->
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

                        GL.Check("Error while making current.")

                        // store the token as current
                        currentToken.Value <- Some x
                        handle
                    ),
                    ( fun x ->
                        let handle = x.Handle.Value
                        GL.Check("Error before releasing current")
                        handle.ReleaseCurrent()
                        bag.Add(handle)
                        bagCount.Release() |> ignore
                        currentToken.Value <- None)
                ) :> _


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
        
            renderingContexts.Clear()
        
            for i in 0..resourceContextCount-1 do
                let s = resourceContexts.[i]
                ContextHandle.delete s
        with _ ->
            ()
            
    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new(runtime) = new Context(runtime, Config.NumberOfResourceContexts)

