namespace Aardvark.SceneGraph


open System
open System.Threading
open System.Runtime.InteropServices
open System.Collections.Concurrent
open Aardvark.Base
open System.Collections.Generic
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental

module private Mem =
    let inline alloc (size : nativeint) =
        if size <= 0n then 0n
        else Marshal.AllocHGlobal size

    let inline free (ptr : nativeint) =
        if ptr <> 0n then Marshal.FreeHGlobal ptr

    let inline realloc (ptr : nativeint) (size : nativeint) =
        if ptr = 0n then alloc size
        elif size <= 0n then free ptr; 0n
        else Marshal.ReAllocHGlobal(ptr, size)

    let inline copy (source : nativeint) (target : nativeint) (size : nativeint) =
        if size > 0n then
            Marshal.Copy(source, target, int size)

module private Range = 
    let inline ofOffsetAndSize (offset : nativeint) (size : nativeint) =
        Range1i.FromMinAndSize(int offset, int size - 1)

    let inline ofPtr (ptr : managedptr) =
        Range1i.FromMinAndSize(int ptr.Offset, ptr.Size - 1)



[<CompiledName("ChangeableBuffer")>]
type cbuffer(sizeInBytes : nativeint, release : cbuffer -> unit) =
    inherit Mod.AbstractMod<IBuffer>()

    let mutable capacity = sizeInBytes
    let mutable ptr = Mem.alloc capacity
    let ptrLock = new ReaderWriterLockSlim()

    let readers = HashSet<CBufferReader>()

    let changed (self : cbuffer) (offset : nativeint) (size : nativeint) =
        if size > 0n then
            let range = Range.ofOffsetAndSize offset size
            let transaction = Transaction()
            transaction.Enqueue self

            lock readers (fun () ->
                for r in readers do
                    r.Add range
                    transaction.Enqueue r
            )

            transaction.Commit()


    member x.GetReader() =
        let r = new CBufferReader(x)
        lock readers (fun () -> readers.Add r |> ignore)
        r :> IAdaptiveBufferReader

    member internal x.RemoveReader(r : CBufferReader) =
        lock readers (fun () -> readers.Remove r |> ignore)

    member x.SizeInBytes = capacity

    member x.NativeBuffer = 
        { new INativeBuffer with
            member x.SizeInBytes = int capacity
            member x.Use (f : nativeint -> 'a) =
                ReaderWriterLock.read ptrLock (fun () -> f ptr )
        }

    member x.AdjustToSize(capacityInBytes : nativeint) =
        ReaderWriterLock.write ptrLock (fun () ->
            let old = Interlocked.Exchange(&capacity, capacityInBytes)
            if old <> capacityInBytes then
                ptr <- Mem.realloc ptr capacityInBytes
        )

    member x.Write(source : nativeint, offsetInBytes : nativeint, sizeInBytes : nativeint) =
        // check for out of bounds writes
        assert(offsetInBytes >= 0n && sizeInBytes >= 0n)
        assert(offsetInBytes + sizeInBytes < capacity)

        ReaderWriterLock.read ptrLock (fun () ->
            // copy the content
            Mem.copy source (ptr + offsetInBytes) sizeInBytes
        )

        // notify everyone interested about the change
        changed x offsetInBytes sizeInBytes

    member x.Write(source : Array, offsetInBytes : nativeint, sizeInBytes : nativeint) =
        let gc = GCHandle.Alloc(source, GCHandleType.Pinned)
        try x.Write(gc.AddrOfPinnedObject(), offsetInBytes, sizeInBytes)
        finally gc.Free()

    member x.Dispose() =
        let old = Interlocked.Exchange(&ptr, 0n)
        if old <> 0n then
            release x
            Mem.free ptr
            capacity <- 0n
            ptrLock.Dispose()

    override x.Compute() =
        x.NativeBuffer :> IBuffer

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IAdaptiveBuffer with
        member x.GetReader() = x.GetReader()

and internal CBufferReader(buffer : cbuffer) =
    inherit AdaptiveObject()

    let mutable dirty = RangeSet.empty
    let mutable lastCapacity = -1n

    member x.Add(r : Range1i) =
        lock x (fun () ->
            if lastCapacity = buffer.SizeInBytes then
                Interlocked.Change(&dirty, RangeSet.insert r) |> ignore
        )
    member x.Dispose() =
        buffer.RemoveReader x
        dirty <- RangeSet.empty
        lastCapacity <- -1n

    member x.GetDirtyRanges (caller : IAdaptiveObject) : INativeBuffer * RangeSet =
        x.EvaluateAlways caller (fun () ->
            let ranges = Interlocked.Exchange(&dirty, RangeSet.empty)

            let nb = buffer.NativeBuffer
            let size = nativeint nb.SizeInBytes
            let oldCap = Interlocked.Exchange(&lastCapacity, size)

            if oldCap = size then
                nb,ranges
            else
                nb, RangeSet.ofList [Range1i(0, int size - 1)]
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IAdaptiveBufferReader with
        member x.GetDirtyRanges caller = x.GetDirtyRanges caller

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CBuffer =
    
    let inline create (sizeInBytes : nativeint) =
        new cbuffer(sizeInBytes, ignore)

    let inline destroy (b : cbuffer) =
        b.Dispose()

    let inline resize (size : nativeint) (b : cbuffer) =
        b.AdjustToSize size

    let inline capacity (b : cbuffer) =
        b.SizeInBytes


    let inline writeArray (source : #Array) (offset : nativeint) (size : nativeint) (b : cbuffer) =
        b.Write(source, offset, size)

    let inline write (source : nativeint) (offset : nativeint) (size : nativeint) (b : cbuffer) =
        b.Write(source, offset, size)


    let inline toAdaptiveBuffer (b : cbuffer) = b :> IAdaptiveBuffer

    let inline toMod (b : cbuffer) = b :> IMod<_>



type private TypedCBuffer(release : cbuffer -> unit)  =
    let lockObj = obj()
    let mutable elementTypeAndSize = None
    let mutable sizeInElements = 0n
    let buffer = new cbuffer(0n, release)

    member x.AdjustToCount(count : nativeint) =
        lock lockObj (fun () ->
            sizeInElements <- count
            match elementTypeAndSize with
                | Some(_,s) -> buffer.AdjustToSize(s * count)
                | _ -> ()
        )

    member x.Write(data : Array, offsetInElements : nativeint, count : nativeint) =
        let elementSize = 
            lock lockObj (fun () ->
                match elementTypeAndSize with
                    | Some (t,s) ->
                        assert (t = data.GetType().GetElementType())
                        s
                    | None ->
                        let t = data.GetType().GetElementType()
                        let s = nativeint (Marshal.SizeOf t)
                        elementTypeAndSize <- Some (t,s)
                        buffer.AdjustToSize(s * sizeInElements)
                        s
            )

        assert (count <= nativeint data.Length)

        let offsetInBytes = elementSize * offsetInElements
        let sizeInBytes = elementSize * count
        buffer.Write(data, offsetInBytes, sizeInBytes)

    member x.Buffer = buffer :> IAdaptiveBuffer

    member x.Dispose() =
        elementTypeAndSize <- None
        sizeInElements <- 0n
        buffer.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type GeometryPool() =
    let manager = MemoryManager.createNop()
    let pointers = ConcurrentDictionary<IndexedGeometry, managedptr>()
    let buffers = ConcurrentDictionary<Symbol, TypedCBuffer>()
        
    let pointersRW = new ReaderWriterLockSlim()
    let buffersRW = new ReaderWriterLockSlim()

    let faceVertexCount (g : IndexedGeometry) =
        if isNull g.IndexArray then
            let att = g.IndexedAttributes.Values |> Seq.head
            att.Length
        else
            g.IndexArray.Length

    let write (g : IndexedGeometry) (sem : Symbol) (ptr : managedptr) (buffer : TypedCBuffer) =
        buffer.AdjustToCount(nativeint manager.Capacity)
        match g.IndexedAttributes.TryGetValue sem with
            | (true, array) -> 
                if isNull g.IndexArray then
                    buffer.Write(array, ptr.Offset, nativeint ptr.Size)
                else
                    failwith "[GeometryLayout] indexed geometries are not supported atm."
            | _ -> ()

    member x.GetBuffer(sem : Symbol) =
        let isNew = ref false
            
        let result = 
            ReaderWriterLock.write buffersRW (fun () ->
                buffers.GetOrAdd(sem, fun sem ->
                    isNew := true

                    let destroy (t : cbuffer) =
                        buffers.TryRemove sem |> ignore

                    new TypedCBuffer(destroy)
                )
            )

        if !isNew then
            ReaderWriterLock.read pointersRW (fun () ->
                for (KeyValue(g,ptr)) in pointers do
                    write g sem ptr result
            )
        result.Buffer

    member x.Add(g : IndexedGeometry) =
        let isNew = ref false
        let ptr = 
            ReaderWriterLock.write pointersRW (fun () ->
                pointers.GetOrAdd(g, fun g ->
                    let count = faceVertexCount g
                    isNew := true
                    manager.Alloc count
                )
            )

        if !isNew then
            ReaderWriterLock.read buffersRW (fun () ->
                for (KeyValue(sem, buffer)) in buffers do
                    write g sem ptr buffer
            )
            
        Range.ofPtr ptr

    member x.Remove(g : IndexedGeometry) =
        ReaderWriterLock.write pointersRW (fun () ->
            match pointers.TryRemove g with
                | (true, ptr) -> 
                    let range = Range.ofPtr ptr
                    manager.Free ptr
                    range
                | _ ->
                    Range1i.Invalid
        )

    member x.Contains(g : IndexedGeometry) =
        pointers.ContainsKey g

    member x.Buffers = buffers |> Seq.map (fun (KeyValue(k,v)) -> k,v.Buffer)

    member x.Count = pointers.Count

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeometryPool =
    let inline create() = GeometryPool()

    let inline getBuffer (sem : Symbol) (pool : GeometryPool) =
        pool.GetBuffer sem

    let inline add (g : IndexedGeometry) (pool : GeometryPool) =
        pool.Add g

    let inline remove (g : IndexedGeometry) (pool : GeometryPool) =
        pool.Remove g

    let inline contains (g : IndexedGeometry) (pool : GeometryPool) =
        pool.Contains g



type DrawCallSet(collapseAdjacent : bool) =
    inherit Mod.AbstractMod<DrawCallInfo[]>()

    let all = HashSet<Range1i>()
    let mutable ranges = RangeSet.empty

    member x.Add(r : Range1i) =
        let result = 
            lock x (fun () ->
                if all.Add r then
                    ranges <- RangeSet.insert r ranges
                    true
                else
                    false
            )

        if result then transact (fun () -> x.MarkOutdated())

        result

    member x.Remove(r : Range1i) =
        let result =
            lock x (fun () ->
                if all.Remove r then
                    ranges <- RangeSet.remove r ranges
                    true
                else
                    false
            )

        if result then transact (fun () -> x.MarkOutdated())
        result

    override x.Compute() =
        let drawRanges = 
            if collapseAdjacent then ranges :> seq<_>
            else all :> seq<_>

        drawRanges 
            |> Seq.map (fun range ->
                DrawCallInfo(
                    FirstIndex = range.Min,
                    FaceVertexCount = range.Size + 1,
                    FirstInstance = 0,
                    InstanceCount = 1,
                    BaseVertex = 0
                )
               ) 
            |> Seq.toArray
            
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DrawCallSet =
    let inline create() = DrawCallSet(true)

    let inline add (r : Range1i) (set : DrawCallSet) = set.Add r
    let inline remvoe (r : Range1i) (set : DrawCallSet) = set.Remove r

    let inline toMod (set : DrawCallSet) = set :> IMod<_>


