namespace Aardvark.Base.Rendering


open System
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental

[<AutoOpen>]
module private TypedBuffers =
    type ITypedBuffer =
        inherit IDisposable
        abstract member Write : data : Array * offset : nativeint * size : nativeint -> unit
        abstract member AdjustToCount : nativeint -> unit
        abstract member Buffer : IMod<IBuffer>

    type TypedCBuffer(runtime : IRuntime, release : unit -> unit)  =
        let lockObj = obj()
        let mutable elementTypeAndSize = None
        let mutable sizeInElements = 0n
        let buffer = new cbuffer(0n, fun b -> release())

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

        interface ITypedBuffer with
            member x.Write(data, off, cnt) = x.Write(data, off, cnt)
            member x.AdjustToCount(cnt) = x.AdjustToCount(cnt)
            member x.Buffer = buffer :> IMod<IBuffer>

    type TypedMappedBuffer(runtime : IRuntime, release : unit -> unit) =
        let lockObj = new ReaderWriterLockSlim()

        [<VolatileField>]
        let mutable elementTypeAndSize = None

        [<VolatileField>]
        let mutable sizeInElements = 0n

        let buffer = runtime.CreateMappedBuffer()

        let subscription = buffer.OnDispose.Subscribe release

        member x.AdjustToCount(count : nativeint) =
            if sizeInElements <> count then
                ReaderWriterLock.write lockObj (fun () ->
                    if sizeInElements <> count then
                        match elementTypeAndSize with
                            | Some(_,s) -> buffer.Resize(s * count)
                            | _ -> ()
                        sizeInElements <- count
                )

        member x.Write(data : Array, offsetInElements : nativeint, count : nativeint) =

            let elementSize = 
                match elementTypeAndSize with
                    | Some (t,s) ->
                        assert (t = data.GetType().GetElementType())
                        s
                    | None ->
                        ReaderWriterLock.write lockObj (fun () ->
                            match elementTypeAndSize with
                                | None -> 
                                    let t = data.GetType().GetElementType()
                                    let s = nativeint (Marshal.SizeOf t)
                                    buffer.Resize(s * sizeInElements)
                                    elementTypeAndSize <- Some(t,s)
                                    s
                                | Some(_,s) ->
                                    s
                        )

            assert (count <= nativeint data.Length)

            let offsetInBytes = elementSize * offsetInElements
            let sizeInBytes = elementSize * count

            let arr = GCHandle.Alloc(data,GCHandleType.Pinned)
            try 
                ReaderWriterLock.read lockObj (fun () ->   // why is here read? it works
                    buffer.Write(arr.AddrOfPinnedObject(), offsetInBytes, sizeInBytes)
                )
            finally 
                arr.Free()

        member x.Buffer = buffer :> IMod<IBuffer>

        member x.Dispose() =
            elementTypeAndSize <- None
            sizeInElements <- 0n
            buffer.Dispose()
            subscription.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface ITypedBuffer with
            member x.Write(data, off, cnt) = x.Write(data, off, cnt)
            member x.AdjustToCount(cnt) = x.AdjustToCount(cnt)
            member x.Buffer = buffer :> IMod<IBuffer>

type GeometryPool(runtime : IRuntime, asyncWrite : bool) =
    let mutable manager = MemoryManager.createNop()
    let pointers = Dict<IndexedGeometry, managedptr>()
    let buffers = SymbolDict<ITypedBuffer>()
        
    let pointersRW = new ReaderWriterLockSlim()
    let buffersRW = new ReaderWriterLockSlim()

    let faceVertexCount (g : IndexedGeometry) =
        if isNull g.IndexArray then
            let att = g.IndexedAttributes.Values |> Seq.head
            att.Length
        else
            g.IndexArray.Length

    let write (g : IndexedGeometry) (sem : Symbol) (ptr : managedptr) (buffer : ITypedBuffer) =
        
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
                    let destroy() = buffers.TryRemove sem |> ignore
                    if asyncWrite then new TypedMappedBuffer(runtime, destroy) :> ITypedBuffer
                    else new TypedCBuffer(runtime, destroy) :> ITypedBuffer
                )
            )

        if !isNew then
            ReaderWriterLock.read pointersRW (fun () ->
                result.AdjustToCount(nativeint manager.Capacity)
                for (KeyValue(g,ptr)) in pointers do
                    write g sem ptr result
            )
        result.Buffer

    member x.Add(g : IndexedGeometry) =
        //let isNew = ref false
        let ptr = 
            ReaderWriterLock.write pointersRW (fun () ->
                pointers.GetOrCreate(g, fun g ->
                    let count = faceVertexCount g
                    let ptr = manager.Alloc (nativeint count)

                    if count <> 0 then
                        ReaderWriterLock.read buffersRW (fun () ->
                            for (KeyValue(sem, buffer)) in buffers do
                                buffer.AdjustToCount(nativeint manager.Capacity)
                                write g sem ptr buffer
                        )
                    ptr
                )
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

    member x.Dispose() =
        buffers.Values |> Seq.toList |> List.iter (fun b -> b.Dispose())
        manager.Dispose()
        pointers.Clear()
        buffers.Clear()
        manager <- MemoryManager.createNop()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeometryPool =
    let inline create runtime = new GeometryPool(runtime, false)
    let inline createAsync runtime = new GeometryPool(runtime, true)


    let inline getBuffer (sem : Symbol) (pool : GeometryPool) =
        pool.GetBuffer sem

    let inline add (g : IndexedGeometry) (pool : GeometryPool) =
        pool.Add g

    let inline remove (g : IndexedGeometry) (pool : GeometryPool) =
        pool.Remove g

    let inline contains (g : IndexedGeometry) (pool : GeometryPool) =
        pool.Contains g