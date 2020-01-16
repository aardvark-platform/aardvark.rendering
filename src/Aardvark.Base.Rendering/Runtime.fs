namespace Aardvark.Base

open System
open System.Threading
open Aardvark.Base
open FSharp.Data.Adaptive
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

    type nref<'a>(value : 'a) =
        static let mutable currentId = 0

        let mutable value = value
        let id = Interlocked.Increment(&currentId)

        member private x.Id = id
        member x.Value
            with get() = value
            and set v = value <- v

        override x.GetHashCode() = id
        override x.Equals o =
            match o with
                | :? nref<'a> as o -> id = o.Id
                | _ -> false

        interface IComparable with
            member x.CompareTo o =
                match o with
                    | :? nref<'a> as o -> compare id o.Id
                    | _ -> failwith "uncomparable"

                    
        interface IComparable<nref<'a>> with
            member x.CompareTo o = compare id o.Id

    let inline private (!) (r : nref<'a>) =
        r.Value
        
    let inline private (:=) (r : nref<'a>) (value : 'a) =
        r.Value <- value

    [<AllowNullLiteral>]
    type Block<'a> =
        class
            val mutable public Parent : IMemoryManager<'a>
            val mutable public Memory : nref<'a>
            val mutable public Next : Block<'a>
            val mutable public Prev : Block<'a>
            val mutable public Offset : nativeint
            val mutable public Size : nativeint
            val mutable public IsFree : bool

            override x.ToString() =
                sprintf "[%d,%d)" x.Offset (x.Offset + x.Size)

            new(parent, m, o, s, f, p, n) = { Parent = parent; Memory = m; Offset = o; Size = s; IsFree = f; Prev = p; Next = n }
            new(parent, m, o, s, f) = { Parent = parent; Memory = m; Offset = o; Size = s; IsFree = f; Prev = null; Next = null }

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
                        else 
                            let c = compare l.Offset r.Offset    
                            if c <> 0 then c
                            else compare l.Memory r.Memory   
            }

        let store = SortedSetExt<Block<'a>>(Seq.empty, comparer)
        
        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)


        member x.TryGetGreaterOrEqual(size : nativeint) =
            let query = Block(Unchecked.defaultof<_>, Unchecked.defaultof<_>, -1n, size, true)
            let (_, _, r) = store.FindNeighbours(query)
            if r.HasValue then 
                let r = r.Value
                store.Remove r |> ignore
                Some r
            else 
                None

        member x.TryGetAligned(align : nativeint, size : nativeint) =
            let min = Block(Unchecked.defaultof<_>, Unchecked.defaultof<_>, -1n, size, true)
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

    and IMemoryManager<'a> =
        interface end

    and MemoryManager<'a>(mem : Memory<'a>, initialCapacity : nativeint) as this =
        
        let free = FreeList<'a>()
        
        let store = nref <| mem.malloc initialCapacity
        let mutable capacity = initialCapacity
        let mutable first = Block<'a>(this, store, 0n, initialCapacity, true)
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
                    let o = !store
                    let n = mem.mrealloc o oldCapacity newCapacity
                    store := n
                    capacity <- newCapacity
                    let o = ()

                    let additional = newCapacity - oldCapacity
                    if additional > 0n then
                        if last.IsFree then
                            free.Remove(last) |> ignore
                            last.Size <- last.Size + additional
                            free.Insert(last)
                        else
                            let newFree = Block<'a>(this, store, oldCapacity, additional, true, last, null)
                            last.Next <- newFree
                            last <- newFree
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
            if size = 0n then
                Block<'a>(x, store, 0n, 0n, true, null, null)
            else
                lock free (fun () ->
                    match free.TryGetAligned(align, size) with
                        | Some b ->
                            let alignedOffset = next align b.Offset
                            let alignedSize = b.Size - (alignedOffset - b.Offset)
                            if alignedOffset > b.Offset then
                                let l = Block<'a>(x, store, b.Offset, alignedOffset - b.Offset, true, b.Prev, b)
                                if isNull l.Prev then first <- l
                                else l.Prev.Next <- l
                                b.Prev <- l
                                free.Insert(l)
                                b.Offset <- alignedOffset
                                b.Size <- alignedSize        
                            
                            if alignedSize > size then
                                let r = Block<'a>(x, store, alignedOffset + size, alignedSize - size, true, b, b.Next)
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
            if size = 0n then
                Block<'a>(x, store, 0n, 0n, true, null, null)
            else
                lock free (fun () ->
                    match free.TryGetGreaterOrEqual size with
                        | Some b ->
                            if b.Size > size then
                                let rest = Block<'a>(x, store, b.Offset + size, b.Size - size, true, b, b.Next)
                        
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
                    if not b.IsFree then
                        let old = b
                    
                        let b = Block(x, store, b.Offset, b.Size, b.IsFree, b.Prev, b.Next)
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
                            let r = Block(x, store, b.Offset + size, b.Size - size, false, b, b.Next)
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
                            mem.mcopy !store b.Offset !store n.Offset b.Size
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
                action !store b.Offset b.Size
            )

        member x.Use(action : 'a -> 'r) =
            ReaderWriterLock.read rw (fun () -> 
                action !store
            )

        member x.Dispose() =
            rw.Dispose()
            mem.mfree !store capacity
            first <- null
            last <- null
            free.Clear()
            capacity <- -1n

        member x.UnsafePointer = store.Value

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IMemoryManager<'a>

    and ChunkedMemoryManager<'a>(mem : Memory<'a>, chunkSize : nativeint) as this =
        
        let empty = Block<'a>(this, nref (mem.malloc 0n), 0n, 0n, true)

        let free = FreeList<'a>()
        let allocated = Dict<'a, nativeint>()
        let mutable usedMemory = 0n

//        do
//            let store = mem.malloc chunkSize
//            free.Insert(Block<'a>(this, nref store, 0n, chunkSize, true))
//            allocated.Add (store, chunkSize) |> ignore
//            usedMemory <- chunkSize

        static let next (align : nativeint) (v : nativeint) =
            if v % align = 0n then v
            else v + (align - v % align)

        let rw = new ReaderWriterLockSlim()

        let grow (additional : nativeint) =
            let blockCap = max chunkSize additional
            usedMemory <- usedMemory + blockCap
            let newMem = mem.malloc blockCap
            allocated.Add(newMem, blockCap) |> ignore
            free.Insert(Block<'a>(this, nref newMem, 0n, blockCap, true))

        let freed(block : Block<'a>) =
            if isNull block.Prev && isNull block.Next then
                match allocated.TryRemove !block.Memory with
                    | (true, size) -> 
                        usedMemory <- usedMemory - size
                        mem.mfree !block.Memory size
                    | _ ->
                        failwith "bad inconsistent hate"
            else
                free.Insert(block)

        member x.Alloc(align : nativeint, size : nativeint) =
            if size = 0n then
                empty
            else
                lock free (fun () ->
                    match free.TryGetAligned(align, size) with
                        | Some b ->
                            let alignedOffset = next align b.Offset
                            let alignedSize = b.Size - (alignedOffset - b.Offset)
                            if alignedOffset > b.Offset then
                                let l = Block<'a>(x, b.Memory, b.Offset, alignedOffset - b.Offset, true, b.Prev, b)
                                if not (isNull l.Prev) then l.Prev.Next <- l
                                b.Prev <- l
                                free.Insert(l)
                                b.Offset <- alignedOffset
                                b.Size <- alignedSize        
                            
                            if alignedSize > size then
                                let r = Block<'a>(x, b.Memory, alignedOffset + size, alignedSize - size, true, b, b.Next)
                                if not (isNull r.Next) then r.Next.Prev <- r
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
            if size = 0n then
                empty
            else
                lock free (fun () ->
                    match free.TryGetGreaterOrEqual size with
                        | Some b ->
                            if b.Size > size then
                                let rest = Block<'a>(x, b.Memory, b.Offset + size, b.Size - size, true, b, b.Next)
                        
                                if not (isNull rest.Next) then rest.Next.Prev <- rest
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
                    
                    let b = Block(x, b.Memory, b.Offset, b.Size, b.IsFree, b.Prev, b.Next)
                    if not (isNull b.Prev) then b.Prev.Next <- b

                    if not (isNull b.Next) then b.Next.Prev <- b

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
                        if not (isNull prev.Prev) then prev.Prev.Next <- b

                        b.Offset <- prev.Offset
                        b.Size <- b.Size + prev.Size

                    if not (isNull next) && next.IsFree then
                        free.Remove(next) |> ignore
                        b.Next <- next.Next
                        if not (isNull next.Next) then next.Next.Prev <- b
                        b.Next <- next.Next

                        b.Size <- b.Size + next.Size


                    b.IsFree <- true
                    freed(b)

                )

        member x.Realloc(b : Block<'a>, align : nativeint, size : nativeint) =
            if b.Size <> size then
                lock free (fun () ->
                    if size = 0n then
                        x.Free b
                    elif b.IsFree then
                        let n = x.Alloc(align, size)

                        b.Prev <- n.Prev
                        b.Next <- n.Next
                        b.Size <- n.Size
                        b.Offset <- n.Offset
                        b.IsFree <- false

                        if not (isNull b.Prev) then b.Prev.Next <- b
                        if not (isNull b.Next) then b.Next.Prev <- b

                    elif b.Size > size then
                        if size = 0n then
                            x.Free(b)
                        else
                            let r = Block(x, b.Memory, b.Offset + size, b.Size - size, false, b, b.Next)
                            b.Next <- r
                            if not (isNull r.Next) then r.Next.Prev <- r
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
                                if not (isNull b.Next) then b.Next.Prev <- b
                                b.Size <- size


                        else
                            failwithf "[MemoryManager] cannot realloc when no mcopy given"
                )

        member x.Realloc(b : Block<'a>, size : nativeint) =
            x.Realloc(b, 1n, size)

        member x.Capactiy = lock free (fun () -> usedMemory)

        member x.Use(b : Block<'a>, action : 'a -> nativeint -> nativeint -> 'r) =
            if b.IsFree && b.Size > 0n then failwith "cannot use free block"
            ReaderWriterLock.read rw (fun () -> 
                action !b.Memory b.Offset b.Size
            )

        member x.Dispose() =
            rw.Dispose()
            for (KeyValue(a, s)) in allocated do mem.mfree a s
            allocated.Clear()
            free.Clear()

        interface IDisposable with
            member x.Dispose() = x.Dispose()
            
        interface IMemoryManager<'a>
   

   
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MemoryManager =
        let createNop() = new MemoryManager<_>(Memory.nop, 16n) 

   
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ChunkedMemoryManager =
        let createNop() = new ChunkedMemoryManager<_>(Memory.nop, 16n) 



type IStreamingTexture =
    inherit aval<ITexture>
    abstract member Update : format : PixFormat * size : V2i * data : nativeint -> unit
    abstract member UpdateAsync : format : PixFormat * size : V2i * data : nativeint -> Transaction
    abstract member ReadPixel : pos : V2i -> C4f


type SamplerDescription = { textureName : Symbol; samplerState : SamplerStateDescription }

type IBackendSurface =
    inherit ISurface
    abstract member Handle : obj

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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderStage =
    let ofFShade =
        LookupTable.lookupTable [
            FShade.ShaderStage.Vertex,      ShaderStage.Vertex
            FShade.ShaderStage.TessControl, ShaderStage.TessControl
            FShade.ShaderStage.TessEval,    ShaderStage.TessEval
            FShade.ShaderStage.Geometry,    ShaderStage.Geometry
            FShade.ShaderStage.Fragment,    ShaderStage.Fragment
            FShade.ShaderStage.Compute,     ShaderStage.Compute
        ]

    let toFShade =
        LookupTable.lookupTable [
            ShaderStage.Vertex,         FShade.ShaderStage.Vertex
            ShaderStage.TessControl,    FShade.ShaderStage.TessControl
            ShaderStage.TessEval,       FShade.ShaderStage.TessEval
            ShaderStage.Geometry,       FShade.ShaderStage.Geometry
            ShaderStage.Fragment,       FShade.ShaderStage.Fragment
            ShaderStage.Compute,        FShade.ShaderStage.Compute
        ]
type BackendSurface(code : string, entryPoints : Dictionary<ShaderStage, string>, builtIns : Map<ShaderStage, Map<FShade.Imperative.ParameterKind, Set<string>>>, uniforms : SymbolDict<IAdaptiveValue>, samplers : Dictionary<string * int, SamplerDescription>, expectsRowMajorMatrices : bool, iface : obj) =
    interface ISurface
    member x.Code = code
    member x.EntryPoints = entryPoints
    member x.BuiltIns = builtIns
    member x.Uniforms = uniforms
    member x.Samplers = samplers
    member x.ExpectsRowMajorMatrices = expectsRowMajorMatrices
    member x.Interface = iface

    new(code, entryPoints) = BackendSurface(code, entryPoints, Map.empty, SymDict.empty, Dictionary.empty, false, null)
    new(code, entryPoints, builtIns) = BackendSurface(code, entryPoints, builtIns, SymDict.empty, Dictionary.empty, false, null)
    new(code, entryPoints, builtIns, uniforms) = BackendSurface(code, entryPoints, builtIns, uniforms, Dictionary.empty, false, null)
    new(code, entryPoints, builtIns, uniforms, samplers) = BackendSurface(code, entryPoints, builtIns, uniforms, samplers, false, null)


type IGeometryPool =
    inherit IDisposable
    abstract member Count : int
    abstract member UsedMemory : Mem
    abstract member Alloc : int * IndexedGeometry -> Management.Block<unit>
    abstract member Free : Management.Block<unit> -> unit
    abstract member TryGetBufferView : Symbol -> Option<BufferView>


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
    abstract member CreateSurface : signature : IFramebufferSignature * surface : aval<ISurface> -> IResource<IBackendSurface>
    abstract member CreateBuffer : buffer : aval<IBuffer> -> IResource<IBackendBuffer>
    abstract member CreateTexture : texture : aval<ITexture> -> IResource<IBackendTexture>

and [<Struct>] LodRendererStats =
    {
        quality         : float
        maxQuality      : float
        totalPrimitives : int64
        totalNodes      : int
        allocatedMemory : Mem
        usedMemory      : Mem
        renderTime      : MicroTime
    }

and LodRendererConfig =
    {
        fbo : IFramebufferSignature
        time : aval<DateTime>
        surface : Surface
        state : PipelineState
        pass : RenderPass
        model : aval<Trafo3d>
        view : aval<Trafo3d>
        proj : aval<Trafo3d>
        budget : aval<int64>
        splitfactor : aval<float>
        renderBounds : aval<bool>
        maxSplits : aval<int>
        stats : cval<LodRendererStats>
        pickTrees : Option<cmap<ILodTreeNode,SimplePickTree>>
        alphaToCoverage : bool
    }

and IRuntime =
    inherit IBufferRuntime
    inherit ITextureRuntime
    inherit IComputeRuntime
    abstract member DeviceCount : int

    abstract member OnDispose : Microsoft.FSharp.Control.IEvent<unit>
    abstract member ResourceManager : IResourceManager
    abstract member ContextLock : IDisposable

    abstract member CreateFramebufferSignature : attachments : SymbolDict<AttachmentSignature> * textures : Set<Symbol> * layers : int * perLayerUniforms : Set<string> -> IFramebufferSignature
    abstract member DeleteFramebufferSignature : IFramebufferSignature -> unit

    abstract member AssembleEffect : FShade.Effect * IFramebufferSignature * IndexedGeometryMode -> BackendSurface
    abstract member AssembleModule : FShade.Effect * IFramebufferSignature * IndexedGeometryMode -> FShade.Imperative.Module

    abstract member PrepareSurface : IFramebufferSignature * ISurface -> IBackendSurface
    abstract member PrepareRenderObject : IFramebufferSignature * IRenderObject -> IPreparedRenderObject

    // type LodNode(quality : IModRef<float>, maxQuality : IModRef<float>, budget : aval<int64>, culling : bool, renderBounds : aval<bool>, maxSplits : aval<int>, time : aval<DateTime>, clouds : aset<LodTreeInstance>) =
    abstract member CreateLodRenderer : config : LodRendererConfig * data : aset<LodTreeInstance> -> IPreparedRenderObject

//    abstract member MaxLocalSize : V3i
//    abstract member Compile : FShade.ComputeShader -> IComputeShader
//    abstract member Delete : IComputeShader -> unit
//    abstract member NewInputBinding : IComputeShader -> IComputeShaderInputBinding
//    abstract member Invoke : shader : IComputeShader * groupCount : V3i * input : IComputeShaderInputBinding -> unit

    abstract member DeleteSurface : IBackendSurface -> unit


//    abstract member PrepareBuffer : IBuffer -> IBackendBuffer
//    abstract member DeleteBuffer : IBackendBuffer -> unit
//    abstract member CreateBuffer : size : nativeint -> IBackendBuffer
//    abstract member Copy : srcData : nativeint * dst : IBackendBuffer * dstOffset : nativeint * size : nativeint -> unit
//    abstract member Copy : srcBuffer : IBackendBuffer * srcOffset : nativeint * dstData : nativeint * size : nativeint -> unit


//    abstract member PrepareTexture : ITexture -> IBackendTexture
//    abstract member CreateTexture : size : V3i * dim : TextureDimension * format : TextureFormat * slices : int * levels : int * samples : int -> IBackendTexture
//    abstract member CreateRenderbuffer : size : V2i * format : RenderbufferFormat * samples : int -> IRenderbuffer
//    abstract member DeleteTexture : IBackendTexture -> unit
    
//    abstract member CreateTexture : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture
//    abstract member CreateTextureArray : size : V2i * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture
//    abstract member CreateTextureCube : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture
   
//    abstract member GenerateMipMaps : IBackendTexture -> unit
//    abstract member ResolveMultisamples : src : IFramebufferOutput * target : IBackendTexture * imgTrafo : ImageTrafo -> unit
//    abstract member Upload : texture : IBackendTexture * level : int * slice : int * source : PixImage -> unit
//    abstract member Download : texture : IBackendTexture * level : int * slice : int * target : PixImage -> unit
//    abstract member DownloadStencil : texture : IBackendTexture * level : int * slice : int * target : Matrix<int> -> unit
//    abstract member DownloadDepth : texture : IBackendTexture * level : int * slice : int * target : Matrix<float32> -> unit
//    abstract member Copy : src : IBackendTexture * srcBaseSlice : int * srcBaseLevel : int * dst : IBackendTexture * dstBaseSlice : int * dstBaseLevel : int * slices : int * levels : int -> unit
//


    abstract member CreateStreamingTexture : mipMaps : bool -> IStreamingTexture
    abstract member CreateSparseTexture<'a when 'a : unmanaged> : size : V3i * levels : int * slices : int * dim : TextureDimension * format : Col.Format * brickSize : V3i * maxMemory : int64 -> ISparseTexture<'a>
    
    abstract member CreateFramebuffer : signature : IFramebufferSignature * attachments : Map<Symbol, IFramebufferOutput> -> IFramebuffer

    abstract member CreateGeometryPool : Map<Symbol, Type> -> IGeometryPool

    abstract member DeleteStreamingTexture : IStreamingTexture -> unit
    abstract member DeleteRenderbuffer : IRenderbuffer -> unit
    abstract member DeleteFramebuffer : IFramebuffer -> unit

    abstract member CompileClear : fboSignature : IFramebufferSignature * clearColors : aval<Map<Symbol, C4f>> * clearDepth : aval<Option<double>> -> IRenderTask
    abstract member CompileRender : fboSignature : IFramebufferSignature * BackendConfiguration * aset<IRenderObject> -> IRenderTask

    abstract member Clear : fbo : IFramebuffer * clearColors : Map<Symbol, C4f> * depth : Option<float> * stencil : Option<int> -> unit

    //abstract member CreateQuery : string -> int
    //abstract member DeleteQuery : int


and ICustomRenderObject =
    inherit IRenderObject
    
    abstract member Create : IRuntime * IFramebufferSignature -> IPreparedRenderObject


and IRenderTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Id : int
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

    abstract member LayerCount : int
    abstract member PerLayerUniforms : Set<string>

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
            viewport = Box2i.FromMinAndSize(V2i.OO, framebuffer.Size - V2i.II)
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

    abstract member Generate : IRuntime * IFramebufferSignature * IndexedGeometryMode -> BackendSurface

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




[<AutoOpen>]
module NullResources =

    let isNullResource (obj : obj) =
        match obj with 
         | :? NullTexture -> true
         | _ -> false
         
    let isValidResourceAdaptive (m : IAdaptiveValue) =
        match m with
            | :? SingleValueBuffer -> AVal.constant false
            | _ -> 
                AVal.custom (fun t ->
                    not <| isNullResource (m.GetValueUntyped t)
                )
