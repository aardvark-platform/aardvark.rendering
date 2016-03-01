namespace Aardvark.Base.Rendering


open System
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental


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
    let pointers = Dict<IndexedGeometry, managedptr>()
    let buffers = SymbolDict<TypedCBuffer>()
        
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
                buffers.GetOrCreate(sem, fun sem ->
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
                pointers.GetOrCreate(g, fun g ->
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


type private TypedMappedBuffer(runtime : IRuntime)  =
    let lockObj = obj()
    let mutable elementTypeAndSize = None
    let mutable sizeInElements = 0n
    let buffer = runtime.CreateMappedBuffer()

    member x.AdjustToCount(count : nativeint) =
        lock lockObj (fun () ->
            sizeInElements <- count
            match elementTypeAndSize with
                | Some(_,s) -> buffer.Resize(int (s * count))
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
                        buffer.Resize(int <| s * sizeInElements)
                        s
            )

        assert (count <= nativeint data.Length)

        let offsetInBytes = elementSize * offsetInElements
        let sizeInBytes = elementSize * count

        let arr = GCHandle.Alloc(data,GCHandleType.Pinned)
        try buffer.Write(arr.AddrOfPinnedObject(), int offsetInBytes, int sizeInBytes)
        finally arr.Free()

    member x.Buffer = buffer :> IMod<IBuffer>

    member x.Dispose() =
        elementTypeAndSize <- None
        sizeInElements <- 0n
        buffer.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type GeometryPoolMapped(runtime : IRuntime) =
    let manager = MemoryManager.createNop()
    let pointers = Dict<IndexedGeometry, managedptr>()
    let buffers = SymbolDict<TypedMappedBuffer>()

    let pointersRW = new ReaderWriterLockSlim()
    let buffersRW = new ReaderWriterLockSlim()

    let faceVertexCount (g : IndexedGeometry) =
        if isNull g.IndexArray then
            let att = g.IndexedAttributes.Values |> Seq.head
            att.Length
        else
            g.IndexArray.Length


    let write (g : IndexedGeometry) (sem : Symbol) (ptr : managedptr) (buffer : TypedMappedBuffer) =
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
                buffers.GetOrCreate(sem, fun sem ->
                    isNew := true

                    // TODO remove from
                    let destroy (t : cbuffer) =
                        buffers.TryRemove sem |> ignore
    

                    new TypedMappedBuffer(runtime)
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
                pointers.GetOrCreate(g, fun g ->
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

//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
//module GeometryPoolMapped =
//    let inline create() = GeometryPool()
//
//    let inline getBuffer (sem : Symbol) (pool : GeometryPool) =
//        pool.GetBuffer sem
//
//    let inline add (g : IndexedGeometry) (pool : GeometryPool) =
//        pool.Add g
//
//    let inline remove (g : IndexedGeometry) (pool : GeometryPool) =
//        pool.Remove g
//
//    let inline contains (g : IndexedGeometry) (pool : GeometryPool) =
//        pool.Contains g


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeometryPoolMapped =
    let inline create runtime = GeometryPoolMapped runtime

    let inline getBuffer (sem : Symbol) (pool : GeometryPoolMapped) =
        pool.GetBuffer sem

    let inline add (g : IndexedGeometry) (pool : GeometryPoolMapped) =
        pool.Add g

    let inline remove (g : IndexedGeometry) (pool : GeometryPoolMapped) =
        pool.Remove g

    let inline contains (g : IndexedGeometry) (pool : GeometryPoolMapped) =
        pool.Contains g