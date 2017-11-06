namespace Aardvark.Base

open System
open System.Threading
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices


module Management =
    
    type Memory<'a> =
        {
            malloc : nativeint -> 'a
            mfree : 'a -> nativeint -> unit
            mcopy : 'a -> nativeint -> 'a -> nativeint -> nativeint -> unit
            mrealloc : 'a -> nativeint -> nativeint -> 'a
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Memory =
        open System.IO.MemoryMappedFiles
        open System.Runtime.InteropServices

        let hglobal =
            {
                malloc = Marshal.AllocHGlobal
                mfree = fun ptr _ -> Marshal.FreeHGlobal ptr
                mcopy = fun src srcOff dst dstOff size -> Marshal.Copy(src + srcOff, dst + dstOff, size)
                mrealloc = fun ptr _ n -> Marshal.ReAllocHGlobal(ptr, n)
            }

        let cotask =
            {
                malloc = fun s -> Marshal.AllocCoTaskMem(int s)
                mfree = fun ptr _ -> Marshal.FreeCoTaskMem ptr
                mcopy = fun src srcOff dst dstOff size -> Marshal.Copy(src + srcOff, dst + dstOff, size)
                mrealloc = fun ptr _ n -> Marshal.ReAllocCoTaskMem(ptr, int n)
            }
        
        let array<'a> =
            {
                malloc = fun s -> Array.zeroCreate<'a> (int s)

                mfree = fun a s -> 
                    ()

                mcopy = fun src srcOff dst dstOff size -> 
                    Array.Copy(src, int64 srcOff, dst, int64 dstOff, int64 size)

                mrealloc = fun ptr o n -> 
                    let mutable ptr = ptr
                    Array.Resize(&ptr, int n)
                    ptr
            }

        let nop =
            {
                malloc = fun _ -> ()
                mfree = fun _ _ -> ()
                mrealloc = fun _ _ _ -> ()
                mcopy = fun _ _ _ _ _ -> ()
            }


    [<AllowNullLiteral>]
    type Block<'a> =
        class
            val mutable public Parent : MemoryManager<'a>
            val mutable public Next : Block<'a>
            val mutable public Prev : Block<'a>
            val mutable public Offset : nativeint
            val mutable public Size : nativeint
            val mutable public IsFree : bool

            override x.ToString() =
                sprintf "[%d,%d)" x.Offset (x.Offset + x.Size)

            new(parent, o, s, f, p, n) = { Parent = parent; Offset = o; Size = s; IsFree = f; Prev = p; Next = n }
            new(parent, o, s, f) = { Parent = parent; Offset = o; Size = s; IsFree = f; Prev = null; Next = null }

        end

    and FreeList<'a>() =
        static let comparer =
            { new System.Collections.Generic.IComparer<Block<'a>> with
                member x.Compare(l : Block<'a>, r : Block<'a>) =
                    if isNull l then
                        if isNull r then 0
                        else 1
                    elif isNull r then
                        -1
                    else
                        let c = compare l.Size r.Size
                        if c <> 0 then c
                        else compare l.Offset r.Offset       
            }

        let store = SortedSetExt<Block<'a>>(Seq.empty, comparer)
        
        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)


        member x.TryGetGreaterOrEqual(size : nativeint) =
            let query = Block(Unchecked.defaultof<_>, -1n, size, true)
            let (_, _, r) = store.FindNeighbours(query)
            if r.HasValue then 
                let r = r.Value
                store.Remove r |> ignore
                Some r
            else 
                None

        member x.TryGetAligned(align : nativeint, size : nativeint) =
            let min = Block(Unchecked.defaultof<_>, -1n, size, true)
            let view = store.GetViewBetween(min, null)

            let res = 
                view |> Seq.tryFind (fun b ->
                    let o = next align b.Offset
                    let s = b.Size - (o - b.Offset)
                    s >= size
                )

            match res with
                | Some res -> 
                    store.Remove res |> ignore
                    Some res
                | None ->
                    None

        member x.Insert(b : Block<'a>) =
            store.Add b |> ignore

        member x.Remove(b : Block<'a>) =
            store.Remove b |> ignore

        member x.Clear() =
            store.Clear()

    and MemoryManager<'a>(mem : Memory<'a>, initialCapacity : nativeint) as this =
        
        let free = FreeList<'a>()
        
        let mutable store = mem.malloc initialCapacity
        let mutable capacity = initialCapacity
        let mutable first = Block<'a>(this, 0n, initialCapacity, true)
        let mutable last = first
        do free.Insert(first)

        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)

        let rw = new ReaderWriterLockSlim()

        let changeCapacity (newCapacity : nativeint) =
            let newCapacity = max newCapacity initialCapacity
            let oldCapacity = capacity
            if newCapacity <> oldCapacity then
                ReaderWriterLock.write rw (fun () ->
                    let o = store
                    let n = mem.mrealloc o oldCapacity newCapacity
                    store <- n
                    capacity <- newCapacity
                    let o = ()

                    let additional = newCapacity - oldCapacity
                    if additional > 0n then
                        if last.IsFree then
                            free.Remove(last) |> ignore
                            last.Size <- last.Size + additional
                            free.Insert(last)
                        else
                            let newFree = Block<'a>(this, oldCapacity, additional, true, last, null)
                            last.Next <- newFree
                            free.Insert(newFree)
                    else (* additional < 0 *)
                        let freed = -additional
                        if not last.IsFree  || last.Size < freed then
                            failwith "invalid memory manager state"
        
                        if last.Size > freed then
                            free.Remove(last) |> ignore
                            last.Size <- last.Size - freed
                            free.Insert(last)
                        else (* last.Size = freed *)
                            free.Remove(last) |> ignore
                            let l = last
                            if isNull l.Prev then first <- null
                            else l.Prev.Next <- null
                            last <- l.Prev
                )

        let grow (additional : nativeint) =
            let newCapacity = Fun.NextPowerOfTwo(int64 (capacity + additional)) |> nativeint
            changeCapacity newCapacity
            
        member x.Alloc(align : nativeint, size : nativeint) =
            lock free (fun () ->
                match free.TryGetAligned(align, size) with
                    | Some b ->
                        let alignedOffset = next align b.Offset
                        let alignedSize = b.Size - (alignedOffset - b.Offset)
                        if alignedOffset > b.Offset then
                            let l = Block<'a>(x, b.Offset, alignedOffset - b.Offset, true, b.Prev, b)
                            if isNull l.Prev then first <- l
                            else l.Prev.Next <- l
                            b.Prev <- l
                            free.Insert(l)
                            b.Offset <- alignedOffset
                            b.Size <- alignedSize        
                            
                        if alignedSize > size then
                            let r = Block<'a>(x, alignedOffset + size, alignedSize - size, true, b, b.Next)
                            if isNull r.Next then last <- r
                            else r.Next.Prev <- r
                            b.Next <- r
                            free.Insert(r)
                            b.Size <- size

                        b.IsFree <- false
                        b
                    | None ->
                        grow size
                        x.Alloc(align, size)

            )

        member x.Alloc(size : nativeint) =
            lock free (fun () ->
                match free.TryGetGreaterOrEqual size with
                    | Some b ->
                        if b.Size > size then
                            let rest = Block<'a>(x, b.Offset + size, b.Size - size, true, b, b.Next)
                        
                            if isNull rest.Next then last <- rest
                            else rest.Next.Prev <- rest
                            b.Next <- rest

                            free.Insert(rest)
                            b.Size <- size

                        b.IsFree <- false
                        b
                    | None ->
                        grow size
                        x.Alloc size
            )

        member x.Free(b : Block<'a>) =
            if not b.IsFree then
                lock free (fun () ->
                    let old = b
                    
                    let b = Block(x, b.Offset, b.Size, b.IsFree, b.Prev, b.Next)
                    if isNull b.Prev then first <- b
                    else b.Prev.Next <- b

                    if isNull b.Next then last <- b
                    else b.Next.Prev <- b

                    old.Next <- null
                    old.Prev <- null
                    old.IsFree <- true
                    old.Offset <- -1n
                    old.Size <- 0n


                    let prev = b.Prev
                    let next = b.Next
                    if not (isNull prev) && prev.IsFree then
                        free.Remove(prev) |> ignore
                        
                        b.Prev <- prev.Prev
                        if isNull prev.Prev then first <- b
                        else prev.Prev.Next <- b

                        b.Offset <- prev.Offset
                        b.Size <- b.Size + prev.Size

                    if not (isNull next) && next.IsFree then
                        free.Remove(next) |> ignore
                        b.Next <- next.Next
                        if isNull next.Next then last <- b
                        else next.Next.Prev <- b
                        b.Next <- next.Next

                        b.Size <- b.Size + next.Size


                    b.IsFree <- true
                    free.Insert(b)

                    if last.IsFree then
                        let c = Fun.NextPowerOfTwo (int64 last.Offset) |> nativeint
                        changeCapacity c

                )

        member x.Realloc(b : Block<'a>, align : nativeint, size : nativeint) =
            if b.Size <> size then
                lock free (fun () ->
                    if b.IsFree then
                        let n = x.Alloc(align, size)

                        b.Prev <- n.Prev
                        b.Next <- n.Next
                        b.Size <- n.Size
                        b.Offset <- n.Offset
                        b.IsFree <- false

                        if isNull b.Prev then first <- b
                        else b.Prev.Next <- b
                        if isNull b.Next then last <- b
                        else b.Next.Prev <- b

                    elif b.Size > size then
                        if size = 0n then
                            x.Free(b)
                        else
                            let r = Block(x, b.Offset + size, b.Size - size, false, b, b.Next)
                            b.Next <- r
                            if isNull r.Next then last <- r
                            else r.Next.Prev <- r
                            x.Free(r)

                    elif b.Size < size then
                        let next = b.Next
                        let missing = size - b.Size
                        if not (isNull next) && next.IsFree && next.Size >= missing then
                            free.Remove next |> ignore

                            if missing < next.Size then
                                next.Offset <- next.Offset + missing
                                next.Size <- next.Size - missing
                                b.Size <- size
                                free.Insert(next)

                            else
                                b.Next <- next.Next
                                if isNull b.Next then last <- b
                                else b.Next.Prev <- b
                                b.Size <- size


                        else
                            let n = x.Alloc(align, size)
                            mem.mcopy store b.Offset store n.Offset b.Size
                            x.Free b

                            b.Prev <- n.Prev
                            b.Next <- n.Next
                            b.Size <- n.Size
                            b.Offset <- n.Offset
                            b.IsFree <- false

                            if isNull b.Prev then first <- b
                            else b.Prev.Next <- b
                            if isNull b.Next then last <- b
                            else b.Next.Prev <- b
            
                )

        member x.Realloc(b : Block<'a>, size : nativeint) =
            x.Realloc(b, 1n, size)

        member x.Capactiy = lock free (fun () -> capacity)

        member x.Use(b : Block<'a>, action : 'a -> nativeint -> nativeint -> 'r) =
            if b.IsFree then failwith "cannot use free block"
            ReaderWriterLock.read rw (fun () -> 
                action store b.Offset b.Size
            )

        member x.Dispose() =
            rw.Dispose()
            mem.mfree store capacity
            first <- null
            last <- null
            free.Clear()
            capacity <- -1n

        interface IDisposable with
            member x.Dispose() = x.Dispose()

   
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MemoryManager =
        let createNop() = new MemoryManager<_>(Memory.nop, 16n) 



type IStreamingTexture =
    inherit IMod<ITexture>
    abstract member Update : format : PixFormat * size : V2i * data : nativeint -> unit
    abstract member UpdateAsync : format : PixFormat * size : V2i * data : nativeint -> Transaction
    abstract member ReadPixel : pos : V2i -> C4f


type SamplerDescription = { textureName : Symbol; samplerState : SamplerStateDescription }

type IBackendSurface =
    inherit ISurface
    abstract member Handle : obj
    abstract member UniformGetters : SymbolDict<IMod>
    abstract member Samplers : list<string * int * SamplerDescription>
    abstract member Inputs : list<string * Type>
    abstract member Outputs : list<string * Type>
    abstract member Uniforms : list<string * Type>

type IPreparedRenderObject =
    inherit IRenderObject
    inherit IDisposable

    abstract member Update : AdaptiveToken * RenderToken -> unit
    abstract member Original : Option<RenderObject>

type ShaderStage =
    | Vertex = 1
    | TessControl = 2
    | TessEval = 3
    | Geometry = 4
    | Fragment = 5
    | Compute = 6

type BackendSurface(code : string, entryPoints : Dictionary<ShaderStage, string>, builtIns : Map<ShaderStage, Map<FShade.Imperative.ParameterKind, Set<string>>>, uniforms : SymbolDict<IMod>, samplers : Dictionary<string * int, SamplerDescription>, expectsRowMajorMatrices : bool) =
    interface ISurface
    member x.Code = code
    member x.EntryPoints = entryPoints
    member x.BuiltIns = builtIns
    member x.Uniforms = uniforms
    member x.Samplers = samplers
    member x.ExpectsRowMajorMatrices = expectsRowMajorMatrices
    new(code, entryPoints) = BackendSurface(code, entryPoints, Map.empty, SymDict.empty, Dictionary.empty, false)
    new(code, entryPoints, builtIns) = BackendSurface(code, entryPoints, builtIns, SymDict.empty, Dictionary.empty, false)
    new(code, entryPoints, builtIns, uniforms) = BackendSurface(code, entryPoints, builtIns, uniforms, Dictionary.empty, false)
    new(code, entryPoints, builtIns, uniforms, samplers) = BackendSurface(code, entryPoints, builtIns, uniforms, samplers, false)


type IGeometryPool =
    inherit IDisposable
    abstract member Count : int
    abstract member UsedMemory : Mem
    abstract member Alloc : int * IndexedGeometry -> Management.Block<unit>
    abstract member Free : Management.Block<unit> -> unit
    abstract member TryGetBufferView : Symbol -> Option<BufferView>
    

type IComputeShader =
    abstract member LocalSize : V3i

type IComputeShaderInputBinding =
    inherit IDisposable
    abstract member Item : string -> obj with set
    abstract member Flush : unit -> unit



[<AbstractClass; Sealed; Extension>]
type IGeometryPoolExtensions private() =
    [<Extension>]
    static member Alloc(this : IGeometryPool, g : IndexedGeometry) =
        let fvc =
            if isNull g.IndexArray then
                match g.IndexedAttributes.Values |> Seq.tryHead with
                    | None -> 0
                    | Some a -> a.Length
            else
                g.IndexArray.Length

        this.Alloc(fvc, g)


[<AllowNullLiteral>]
type IResourceManager =
    abstract member CreateSurface : signature : IFramebufferSignature * surface : IMod<ISurface> -> IResource<IBackendSurface>
    abstract member CreateBuffer : buffer : IMod<IBuffer> -> IResource<IBackendBuffer>
    abstract member CreateTexture : texture : IMod<ITexture> -> IResource<IBackendTexture>

and IRuntime =
    abstract member OnDispose : Microsoft.FSharp.Control.IEvent<unit>
    abstract member ResourceManager : IResourceManager
    abstract member ContextLock : IDisposable

    abstract member CreateFramebufferSignature : attachments : SymbolDict<AttachmentSignature> * Set<Symbol> -> IFramebufferSignature
    abstract member DeleteFramebufferSignature : IFramebufferSignature -> unit

    abstract member AssembleEffect : FShade.Effect * IFramebufferSignature -> BackendSurface

    abstract member PrepareBuffer : IBuffer -> IBackendBuffer
    abstract member PrepareTexture : ITexture -> IBackendTexture
    abstract member PrepareSurface : IFramebufferSignature * ISurface -> IBackendSurface
    abstract member PrepareRenderObject : IFramebufferSignature * IRenderObject -> IPreparedRenderObject

    abstract member MaxLocalSize : V3i
    abstract member Compile : FShade.ComputeShader -> IComputeShader
    abstract member Delete : IComputeShader -> unit

    abstract member NewInputBinding : IComputeShader -> IComputeShaderInputBinding
    abstract member Invoke : shader : IComputeShader * groupCount : V3i * input : IComputeShaderInputBinding -> unit

    abstract member DeleteBuffer : IBackendBuffer -> unit
    abstract member DeleteTexture : IBackendTexture -> unit
    abstract member DeleteSurface : IBackendSurface -> unit


    abstract member CreateBuffer : size : nativeint -> IBackendBuffer
    abstract member Copy : srcData : nativeint * dst : IBackendBuffer * dstOffset : nativeint * size : nativeint -> unit
    abstract member Copy : srcBuffer : IBackendBuffer * srcOffset : nativeint * dstData : nativeint * size : nativeint -> unit





    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member CreateSparseTexture<'a when 'a : unmanaged> : size : V3i * levels : int * slices : int * dim : TextureDimension * format : Col.Format * brickSize : V3i * maxMemory : int64 -> ISparseTexture<'a>


    abstract member CreateTexture : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture
    abstract member CreateTextureArray : size : V2i * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture
    abstract member CreateTextureCube : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture
    //abstract member CreateTextureCubeArray : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture
    abstract member CreateRenderbuffer : size : V2i * format : RenderbufferFormat * samples : int -> IRenderbuffer
    abstract member CreateFramebuffer : signature : IFramebufferSignature * attachments : Map<Symbol, IFramebufferOutput> -> IFramebuffer
    abstract member CreateMappedBuffer : unit -> IMappedBuffer
    abstract member CreateMappedIndirectBuffer : indexed : bool -> IMappedIndirectBuffer

    abstract member CreateGeometryPool : Map<Symbol, Type> -> IGeometryPool

    abstract member DeleteStreamingTexture : IStreamingTexture -> unit
    abstract member DeleteRenderbuffer : IRenderbuffer -> unit
    abstract member DeleteFramebuffer : IFramebuffer -> unit

    abstract member CompileClear : fboSignature : IFramebufferSignature * clearColors : IMod<Map<Symbol, C4f>> * clearDepth : IMod<Option<double>> -> IRenderTask
    abstract member CompileRender : fboSignature : IFramebufferSignature * BackendConfiguration * aset<IRenderObject> -> IRenderTask
    
    abstract member GenerateMipMaps : IBackendTexture -> unit
    abstract member ResolveMultisamples : IFramebufferOutput * IBackendTexture * ImageTrafo -> unit
    abstract member Upload : texture : IBackendTexture * level : int * slice : int * source : PixImage -> unit
    abstract member Download : texture : IBackendTexture * level : int * slice : int * target : PixImage -> unit
    abstract member DownloadStencil : texture : IBackendTexture * level : int * slice : int * target : Matrix<int> -> unit
    abstract member DownloadDepth : texture : IBackendTexture * level : int * slice : int * target : Matrix<float32> -> unit

and IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member Update : AdaptiveToken * RenderToken -> unit
    abstract member Run : AdaptiveToken * RenderToken * OutputDescription -> unit
    abstract member FrameId : uint64
    abstract member Use : (unit -> 'a) -> 'a

and [<AllowNullLiteral>] IFramebufferSignature =
    abstract member Runtime : IRuntime
    abstract member ColorAttachments : Map<int, Symbol * AttachmentSignature>
    abstract member DepthAttachment : Option<AttachmentSignature>
    abstract member StencilAttachment : Option<AttachmentSignature>
    abstract member Images : Map<int, Symbol>

    abstract member IsAssignableFrom : other : IFramebufferSignature -> bool


and IFramebuffer =
    inherit IDisposable
    abstract member Signature : IFramebufferSignature
    abstract member Size : V2i
    abstract member GetHandle : IAdaptiveObject -> obj
    abstract member Attachments : Map<Symbol, IFramebufferOutput>

and [<Flags>]ColorWriteMask = 
    | Red = 0x1
    | Green = 0x2
    | Blue = 0x4
    | Alpha = 0x8
    | All = 0xf
    | None = 0x0

and OutputDescription =
    {
        framebuffer : IFramebuffer
        images      : Map<Symbol, BackendTextureOutputView>
        viewport    : Box2i
        overrides   : Map<string, obj>
    }

type RenderFragment() =
    
    let mutable refCount = 0

    abstract member Start : unit -> unit
    default x.Start() = ()

    abstract member Stop : unit -> unit
    default x.Stop() = ()

    abstract member Run : AdaptiveToken * RenderToken * OutputDescription -> unit
    default x.Run(_,_,_) = ()

    member x.AddRef() =
        if Interlocked.Increment(&refCount) = 1 then
            x.Start()

    member x.RemoveRef() =
        if Interlocked.Decrement(&refCount) = 0 then
            x.Stop()

type RenderTaskObject(scope : Ag.Scope, pass : RenderPass, t : RenderFragment) =
    let id = newId()
    interface IRenderObject with
        member x.AttributeScope = scope
        member x.Id = id
        member x.RenderPass = pass

    interface IPreparedRenderObject with
        member x.Dispose() = ()
        member x.Update(token, rt) = ()
        member x.Original = None

    member x.Pass = pass

    member x.Fragment = t


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OutputDescription =

    let ofFramebuffer (framebuffer : IFramebuffer) =
        { 
            framebuffer = framebuffer
            images = Map.empty
            viewport = Box2i.FromMinAndSize(V2i.OO, framebuffer.Size)
            overrides = Map.empty
        }
   

[<Extension>]
type RenderTaskRunExtensions() =
    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : IFramebuffer) =
        t.Run(AdaptiveToken.Top, token, OutputDescription.ofFramebuffer fbo)

    [<Extension>]
    static member Run(t : IRenderTask, token : RenderToken, fbo : OutputDescription) =
        t.Run(AdaptiveToken.Top, token, fbo)

type IGeneratedSurface =
    inherit ISurface

    abstract member Generate : IRuntime * IFramebufferSignature -> BackendSurface

type BinaryShader private(content : Lazy<byte[]>) =
    member x.Content = content.Value

    new(arr : byte[]) = 
        BinaryShader(lazy(arr))

    new(file : string) = 
        if not (System.IO.File.Exists file) then failwithf "could not load surface from file %A" file
        BinaryShader(lazy(System.IO.File.ReadAllBytes file))

type BinarySurface(shaders : Map<ShaderStage, BinaryShader>) =
    interface ISurface

    member x.Shaders = shaders

    new(l) = BinarySurface(Map.ofList l)
    new() = BinarySurface(Map.empty)


//type RenderToFramebufferMod(task : IRenderTask, fbo : IMod<OutputDescription>) =
//    inherit Mod.AbstractMod<OutputDescription * FrameStatistics>()
//
//    member x.Task = task
//    member x.Framebuffer = fbo
//
//    override x.Inputs =
//        seq {
//            yield task :> _
//            yield fbo :> _
//        }
//
//    override x.Compute() =
//        let handle = fbo.GetValue x
//        let stats = task.Run(x, handle)
//        handle, stats
//
//type RenderingResultMod(res : RenderToFramebufferMod, semantic : Symbol) =
//    inherit Mod.AbstractMod<ITexture>()
//    let mutable lastStats = FrameStatistics.Zero
//
//    member x.LastStatistics = lastStats
//    member x.Task = res.Task
//    member x.Framebuffer = res.Framebuffer
//    member x.Semantic = semantic
//    member x.Inner = res
//
//    override x.Inputs = Seq.singleton (res :> _)
//
//    override x.Compute() =
//        lock res (fun () ->
//            let wasOutDated = res.OutOfDate
//            let (output, stats) = res.GetValue x
//            if wasOutDated then
//                lastStats <- stats
//            else
//                lastStats <- FrameStatistics.Zero
//                    
//            match Map.tryFind semantic output.framebuffer.Attachments with
//                | Some o ->
//                    match o with
//                        | :? BackendTextureOutputView as o ->
//                            o.texture :> ITexture
//                        | _ ->
//                            failwithf "unexpected output: %A" o
//                | None ->
//                    failwithf "could not get output: %A" semantic
//        )
//


[<AutoOpen>]
module NullResources =

    let isNullResource (obj : obj) =
        match obj with 
         | :? NullTexture -> true
         | _ -> false
         
    let isValidResourceAdaptive (m : IMod) =
        match m with
            | :? SingleValueBuffer -> Mod.constant false
            | _ -> 
              [m :> IAdaptiveObject] |> Mod.mapCustom (fun s ->
                    not <| isNullResource (m.GetValue s)
              ) 


    type BackendBuffer(runtime : IRuntime, real : IBackendBuffer) =
        member x.Handle = real

        member x.Dispose() = runtime.DeleteBuffer real

        member x.Upload(offset : nativeint, data : nativeint, size : nativeint) =
            runtime.Copy(data, real, offset, size)

        member x.Download(offset : nativeint, data : nativeint, size : nativeint) =
            runtime.Copy(real, offset, data, size)

        interface IDisposable with
            member x.Dispose() = x.Dispose()

type IBufferView =
    abstract member Buffer : BackendBuffer
    abstract member Offset : nativeint
    abstract member Size : nativeint

type IBufferView<'a when 'a : unmanaged> =
    inherit IBufferView
    abstract member Count : int
    
type IBuffer<'a when 'a : unmanaged> =
    inherit IBuffer
    inherit IBufferView<'a>
    inherit IDisposable

type private BackendBufferView<'a when 'a : unmanaged>(buffer : BackendBuffer, offset : nativeint, count : int) =

    member x.Buffer = buffer
    member x.Offset = offset
    member x.Count = count

    interface IBufferView<'a> with
        member x.Buffer = buffer
        member x.Offset = offset
        member x.Count = count        
        member x.Size = nativeint count * nativeint sizeof<'a>

type private BackendBuffer<'a when 'a : unmanaged>(buffer : BackendBuffer) =
    inherit BackendBufferView<'a>(buffer, 0n, int (buffer.Handle.SizeInBytes / nativeint sizeof<'a>))
    interface IBuffer<'a> with
        member x.Dispose() = buffer.Dispose()

[<AutoOpen>]
module TypedBufferExtensions =
    open System.Runtime.InteropServices

    let private nsa<'a when 'a : unmanaged> = nativeint sizeof<'a>
        
    type IBufferView<'a when 'a : unmanaged> with
        member x.Upload(src : 'a[], srcIndex : int, dstIndex : int, count : int) =
            let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
            try
                let ptr = gc.AddrOfPinnedObject()
                x.Buffer.Upload(nativeint dstIndex * nsa<'a>, ptr + nsa<'a> * nativeint srcIndex, nsa<'a> * nativeint count)
            finally
                gc.Free()
                
        member x.Download(srcIndex : int, dst : 'a[], dstIndex : int, count : int) =
            let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
            try
                let ptr = gc.AddrOfPinnedObject()
                x.Buffer.Download(nativeint srcIndex * nsa<'a>, ptr + nsa<'a> * nativeint dstIndex, nsa<'a> * nativeint count)
            finally
                gc.Free()

        member x.Upload(src : 'a[], dstIndex : int, count : int) = x.Upload(src, 0, dstIndex, count)
        member x.Upload(src : 'a[], count : int) = x.Upload(src, 0, 0, count)
        member x.Upload(src : 'a[]) = x.Upload(src, 0, 0, src.Length)

            
        member x.Download(srcIndex : int, dst : 'a[], count : int) = x.Download(srcIndex, dst, 0, count)
        member x.Download(dst : 'a[], count : int) = x.Download(0, dst, 0, count)
        member x.Download(dst : 'a[]) = x.Download(0, dst, 0, dst.Length)
        member x.Download() = 
            let dst = Array.zeroCreate x.Count 
            x.Download(0, dst, 0, dst.Length)
            dst

        member x.GetSlice(min : Option<int>, max : Option<int>) =
            let min = defaultArg min 0
            let max = defaultArg max (x.Count - min)
            BackendBufferView<'a>(x.Buffer, x.Offset + nativeint min * nsa<'a>, 1 + max - min) :> IBufferView<_>

    type IRuntime with
        member x.CreateBuffer<'a when 'a : unmanaged>(count : int) =
            let buffer = new BackendBuffer(x, x.CreateBuffer(nsa<'a> * nativeint count))
            new BackendBuffer<'a>(buffer) :> IBuffer<'a>

        member x.CreateBuffer<'a when 'a : unmanaged>(data : 'a[]) =
            let buffer = new BackendBuffer(x, x.CreateBuffer(nsa<'a> * nativeint data.Length))
            let res = new BackendBuffer<'a>(buffer) :> IBuffer<'a>
            res.Upload(data)
            res

        member x.CompileCompute (shader : 'a -> 'b) =
            let sh = FShade.ComputeShader.ofFunction x.MaxLocalSize shader
            x.Compile(sh)
            
        member x.Invoke(cShader : IComputeShader, groupCount : V2i, input : IComputeShaderInputBinding) =
            x.Invoke(cShader, V3i(groupCount, 1), input)
            
        member x.Invoke(cShader : IComputeShader, groupCount : int, input : IComputeShaderInputBinding) =
            x.Invoke(cShader, V3i(groupCount, 1, 1), input)

        member x.Invoke(cShader : IComputeShader, groupCount : V3i, values : seq<string * obj>) =
            use i = x.NewInputBinding cShader
            for (name, value) in values do
                i.[name] <- value
            i.Flush()
            x.Invoke(cShader, groupCount, i)

        member x.Invoke(cShader : IComputeShader, groupCount : V2i, values : seq<string * obj>) =
            x.Invoke(cShader, V3i(groupCount, 1), values)

        member x.Invoke(cShader : IComputeShader, groupCount : int, values : seq<string * obj>) =
            x.Invoke(cShader, V3i(groupCount, 1, 1), values)
            
            