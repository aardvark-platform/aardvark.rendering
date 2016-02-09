namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AllowNullLiteral>]
type ISurface = interface end


[<AllowNullLiteral>]
type IAttributeProvider =
    inherit IDisposable
    abstract member TryGetAttribute : name : Symbol -> Option<BufferView>
    abstract member All : seq<Symbol * BufferView>

[<AllowNullLiteral>]
type IUniformProvider =
    inherit IDisposable
    abstract member TryGetUniform : scope : Ag.Scope * name : Symbol -> Option<IMod>


module private RenderObjectIds =
    open System.Threading
    let mutable private currentId = 0
    let newId() = Interlocked.Increment &currentId

type IRenderObject =
    abstract member RenderPass : uint64
    abstract member AttributeScope : Ag.Scope

[<CustomEquality>]
[<CustomComparison>]
type RenderObject =
    {
        Id : int
        mutable AttributeScope : Ag.Scope
                
        mutable IsActive            : IMod<bool>
        mutable RenderPass          : uint64
                
        mutable DrawCallInfos       : IMod<list<DrawCallInfo>>
        mutable IndirectBuffer      : IMod<IBuffer>
        mutable IndirectCount       : IMod<int>
        mutable Mode                : IMod<IndexedGeometryMode>
        

        mutable Surface             : IMod<ISurface>
                              
        mutable DepthTest           : IMod<DepthTestMode>
        mutable CullMode            : IMod<CullMode>
        mutable BlendMode           : IMod<BlendMode>
        mutable FillMode            : IMod<FillMode>
        mutable StencilMode         : IMod<StencilMode>
                
        mutable Indices             : IMod<Array>
        mutable InstanceAttributes  : IAttributeProvider
        mutable VertexAttributes    : IAttributeProvider
                
        mutable Uniforms : IUniformProvider
    }  
    interface IRenderObject with
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope

    member x.Path = 
        if System.Object.ReferenceEquals(x.AttributeScope,Ag.emptyScope) 
        then "EMPTY" 
        else x.AttributeScope.Path

    static member Create() =
        { Id = RenderObjectIds.newId()
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = 0UL
          DrawCallInfos = null
          IndirectBuffer = null
          IndirectCount = null

          Mode = null
          Surface = null
          DepthTest = null
          CullMode = null
          BlendMode = null
          FillMode = null
          StencilMode = null
          Indices = null
          InstanceAttributes = null
          VertexAttributes = null
          Uniforms = null
        }

    static member Clone(org : RenderObject) =
        { org with Id = RenderObjectIds.newId() }

    override x.GetHashCode() = x.Id
    override x.Equals o =
        match o with
            | :? RenderObject as o -> x.Id = o.Id
            | _ -> false

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? RenderObject as o -> compare x.Id o.Id
                | _ -> failwith "uncomparable"

[<AutoOpen>]
module RenderObjectExtensions =

    let private emptyUniforms =
        { new IUniformProvider with
            member x.TryGetUniform (_,_) = None
            member x.Dispose() = ()
        }

    let private emptyAttributes =
        { new IAttributeProvider with
            member x.TryGetAttribute(name : Symbol) = None 
            member x.All = Seq.empty
            member x.Dispose() = ()
        }

    let private empty =
        { Id = -1
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = 0UL
          DrawCallInfos = null
          IndirectBuffer = null
          IndirectCount = null
          Mode = null
          Surface = null
          DepthTest = null
          CullMode = null
          BlendMode = null
          FillMode = null
          StencilMode = null
          Indices = null
          InstanceAttributes = emptyAttributes
          VertexAttributes = emptyAttributes
          Uniforms = emptyUniforms
        }


    type RenderObject with
        static member Empty =
            empty

        member x.IsValid =
            x.Id >= 0


type IAdaptiveBufferReader =
    inherit IAdaptiveObject
    inherit IDisposable
    abstract member GetDirtyRanges : IAdaptiveObject -> NativeMemoryBuffer * RangeSet

type IAdaptiveBuffer =
    inherit IMod<IBuffer>
    abstract member ElementType : Type
    abstract member GetReader : unit -> IAdaptiveBufferReader

module AttributePackingV2 =
    open System
    open System.Threading
    open System.Collections.Generic
    open System.Runtime.InteropServices
        
    type private AdaptiveBufferReader(b : IAdaptiveBuffer, remove : AdaptiveBufferReader -> unit) =
        inherit AdaptiveObject()

        let mutable realCapacity = 0
        let mutable dirtyCapacity = -1
        let mutable dirtyRanges = RangeSet.empty

        member x.AddDirty(r : Range1i) =
            if dirtyCapacity = realCapacity then
                Interlocked.Change(&dirtyRanges, RangeSet.insert r) |> ignore

        member x.RemoveDirty(r : Range1i) =
            if dirtyCapacity = realCapacity then
                Interlocked.Change(&dirtyRanges, RangeSet.remove r) |> ignore
            
        member x.Resize(cap : int) =
            if cap <> realCapacity then
                realCapacity <- cap
                dirtyRanges <- RangeSet.empty

        member x.GetDirtyRanges(caller : IAdaptiveObject) =
            x.EvaluateAlways caller (fun () ->
                let buffer = b.GetValue x :?> NativeMemoryBuffer
                let dirtyCap = Interlocked.Exchange(&dirtyCapacity, buffer.SizeInBytes)

                if dirtyCap <> buffer.SizeInBytes then
                    buffer, RangeSet.ofList [Range1i(0, buffer.SizeInBytes-1)]
                else
                    let dirty = Interlocked.Exchange(&dirtyRanges, RangeSet.empty)
                    buffer, dirty
            )

        member x.Dispose() =
            if Interlocked.Exchange(&realCapacity, -1) >= 0 then
                b.RemoveOutput x
                remove x
                dirtyRanges <- RangeSet.empty
                dirtyCapacity <- -1

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IAdaptiveBufferReader with
            member x.GetDirtyRanges(caller) = x.GetDirtyRanges(caller)

    type private IndexFlat<'b when 'b : unmanaged>() =
        static member Copy(index : Array, src : nativeint, dst : nativeint) =
            let src = NativePtr.ofNativeInt<'b> src
            let mutable dst = dst

            match index with
                | :? array<int> as arr -> 
                    for i in arr do
                        NativePtr.write (NativePtr.ofNativeInt dst) (NativePtr.get src i)
                        dst <- dst + 4n
                | :? array<int16> as arr -> 
                    for i in arr do
                        NativePtr.write (NativePtr.ofNativeInt dst) (NativePtr.get src (int i))
                        dst <- dst + 2n
                | :? array<int8> as arr -> 
                    for i in arr do
                        NativePtr.write (NativePtr.ofNativeInt dst) (NativePtr.get src (int i))
                        dst <- dst + 1n
                | _ ->
                    failwith ""

    let private dictCache = System.Collections.Concurrent.ConcurrentDictionary<Type, Action<Array, nativeint, nativeint>>()

    let private copyIndexed (dt : Type, index : Array, src : nativeint, dst : nativeint) =
        let action = 
            dictCache.GetOrAdd(dt, fun dt ->
                let flat = typedefof<IndexFlat<int>>.MakeGenericType [| dt |]
                let meth = flat.GetMethod("Copy", Reflection.BindingFlags.NonPublic ||| Reflection.BindingFlags.Static )
                meth.CreateDelegate(typeof<Action<Array, nativeint, nativeint>>) |> unbox<Action<Array, nativeint, nativeint>>
            )
        action.Invoke(index, src, dst)

    type private AdaptiveBuffer(sem : Symbol, elementType : Type, updateLayout : IMod<Dictionary<IndexedGeometry, managedptr>>) =
        inherit Mod.AbstractMod<IBuffer>()

        let readers = HashSet<AdaptiveBufferReader>()
        let elementSize = Marshal.SizeOf elementType
        let mutable parentCapacity = 0
        let mutable storeCapacity = 0
        let mutable storage = 0n
        let writes = Dictionary<IndexedGeometry, managedptr>()

        let faceVertexCount (i : IndexedGeometry) =
            match i.IndexArray with
             | null -> i.IndexedAttributes.Values |> Seq.head |> (fun s -> s.Length)
             | arr -> arr.Length


        let removeReader (r : AdaptiveBufferReader) =
            lock readers (fun () -> readers.Remove r |> ignore)

        let iter (f : AdaptiveBufferReader -> unit) =
            lock readers (fun () ->
                for r in readers do f r
            )

        let write (g : IndexedGeometry) (offset : nativeint) =
            match g.IndexedAttributes.TryGetValue sem with
                | (true, data) ->
                    let dt = data.GetType().GetElementType()

                    if dt = elementType then
                        let count = faceVertexCount g
                        let arraySize = elementSize * count

                        #if DEBUG
                        let available = max 0 (storeCapacity - int offset)
                        if available < arraySize then
                            failwithf "writing out of bounds: { available = %A; real = %A }" available arraySize
                        #endif

                        if isNull g.IndexArray then
                            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                            try Marshal.Copy(gc.AddrOfPinnedObject(), storage + offset, arraySize)
                            finally gc.Free()
                
                        else
                            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                            try copyIndexed(dt, g.IndexArray, gc.AddrOfPinnedObject(), storage + offset) 
                            finally gc.Free()


                        let range = Range1i.FromMinAndSize(int offset, arraySize - 1)
                        iter (fun r -> r.AddDirty range)
                    else
                        failwithf "unexpected input-type: %A" dt   
                | _ ->
                    let count = faceVertexCount g
                    let clearSize = elementSize * count

                    // TODO: maybe respect NullBuffers here
                    Marshal.Set(storage + offset, 0, clearSize)

                    let range = Range1i.FromMinAndSize(int offset, clearSize - 1)
                    iter (fun r -> r.AddDirty range)

        member x.GetReader() =
            let r = new AdaptiveBufferReader(x, removeReader)
            lock readers (fun () -> readers.Add r |> ignore)
            r :> IAdaptiveBufferReader

        member x.ElementType = elementType

        /// may only be called in layout-update
        member x.Resize(count : int) =
            lock writes (fun () ->
                let capacity = count * elementSize
                if parentCapacity <> capacity then
                    parentCapacity <- capacity
                    writes.Clear()
                    
            )

        /// may only be called in layout-update
        member x.Write(ig : IndexedGeometry, ptr : managedptr) =
            if storeCapacity = parentCapacity && storage <> 0n then
                lock writes (fun () -> writes.[ig] <- ptr)

        /// may only be called in layout-update
        member x.Remove(ig : IndexedGeometry) =
            if storeCapacity = parentCapacity && storage <> 0n then
                lock writes (fun () -> writes.Remove ig |> ignore)

        override x.Compute() =
            let layout = updateLayout.GetValue x

            if parentCapacity <> storeCapacity || storage = 0n then
                storeCapacity <- parentCapacity
                iter (fun r -> r.Resize storeCapacity)

                if storage <> 0n then
                    storage <- Marshal.ReAllocHGlobal(storage, nativeint parentCapacity)
                else
                    storage <- Marshal.AllocHGlobal(parentCapacity)

                let all = lock layout (fun () -> Dictionary.toArray layout)
                for (g, ptr) in all do
                    let offset = ptr.Offset * nativeint elementSize
                    write g offset
            else
                let dirty = 
                    lock writes (fun () ->
                        let res = Dictionary.toArray writes
                        writes.Clear()
                        res
                    )

                for (g, ptr) in dirty do
                    let offset = ptr.Offset * nativeint elementSize
                    write g offset

            NativeMemoryBuffer(storage, storeCapacity) :> IBuffer

        interface IAdaptiveBuffer with
            member x.GetReader() = x.GetReader()
            member x.ElementType = elementType

    type Packer(set : aset<IndexedGeometry>, elementTypes : Map<Symbol, Type>) =
        inherit Mod.AbstractMod<Dictionary<IndexedGeometry, managedptr>>()
        let shrinkThreshold = 0.5
        
        let lockObj = obj()
        let reader = set.GetReader()
        let buffers = SymbolDict<AdaptiveBuffer>()
        let mutable manager = MemoryManager.createNop()
        let mutable dataRanges = Dictionary<IndexedGeometry, managedptr>()
        

        let mutable drawRanges = RangeSet.empty

        let faceVertexCount (i : IndexedGeometry) =
            match i.IndexArray with
             | null -> i.IndexedAttributes.Values |> Seq.head |> (fun s -> s.Length)
             | arr -> arr.Length


        let write (g : IndexedGeometry) (ptr : managedptr) =
            lock lockObj (fun () ->
                for (sem, b) in SymDict.toSeq buffers do
                    b.Resize(manager.Capacity)
                    b.Write(g, ptr)
            )

        let remove (g : IndexedGeometry) =
            lock lockObj (fun () ->
                for (sem, b) in SymDict.toSeq buffers do
                    b.Resize(manager.Capacity)
                    b.Remove(g)
            )   
    
        let resize () =
            lock lockObj (fun () ->
                for (sem, b) in SymDict.toSeq buffers do
                    b.Resize(manager.Capacity)
            )

        member private x.CreateBuffer (elementType : Type, sem : Symbol) =
            AdaptiveBuffer(sem, elementType, x)

        member private x.TryGetBuffer (sem : Symbol) =
            lock lockObj (fun () ->
                match buffers.TryGetValue sem with
                    | (true, b) -> Some b
                    | _ ->
                        match Map.tryFind sem elementTypes with
                            | Some et ->
                                let b = x.CreateBuffer(et, sem)
                                b.Resize(manager.Capacity)
                                buffers.[sem] <- b
                                Some b
                            | _ ->
                                None
            )

        override x.Compute() =
            let deltas = reader.GetDelta x

            for d in deltas do
                match d with
                    | Add g ->
                        let ptr = g |> faceVertexCount |> manager.Alloc
                        lock dataRanges (fun () -> dataRanges.[g] <- ptr)
                        write g ptr


                        let r = Range1i.FromMinAndSize(int ptr.Offset, ptr.Size - 1)
                        drawRanges <- RangeSet.insert r drawRanges


                    | Rem g ->
                        match dataRanges.TryGetValue g with
                            | (true, ptr) ->
                                dataRanges.Remove g |> ignore

                                let r = Range1i.FromMinAndSize(int ptr.Offset, ptr.Size - 1)
                                drawRanges <- RangeSet.remove r drawRanges

                                remove g
                                manager.Free ptr

//                                if float manager.AllocatedBytes < shrinkThreshold * float manager.Capacity then
//                                    let mutable newDrawRanges = RangeSet.empty
//                                    let newManager = MemoryManager.createNop()
//                                    let newRanges = Dictionary.empty
//
//                                    for (g,ptr) in Dictionary.toList dataRanges do
//                                        let nptr = newManager.Alloc(ptr.Size)
//                                        newRanges.[g] <- nptr
//                                        let r = Range1i.FromMinAndSize(int nptr.Offset, nptr.Size - 1)
//                                        newDrawRanges <- RangeSet.insert r newDrawRanges
//                                    
//                                    manager.Dispose()
//                                    manager <- newManager
//                                    drawRanges <- newDrawRanges
//                                    dataRanges <- newRanges
//                                    resize()



                            | _ ->
                                ()
                        ()


            dataRanges

        member x.Dispose() =
            ()

        member x.AttributeProvider =
            { new IAttributeProvider with
                member __.TryGetAttribute(sem : Symbol) =
                    match x.TryGetBuffer sem with
                        | Some b -> BufferView(b, b.ElementType) |> Some
                        | None -> None

                member __.Dispose() =
                    x.Dispose()

                member x.All = Seq.empty
            }

        member x.DrawCallInfos =
            Mod.custom (fun self ->
                x.GetValue self |> ignore

                drawRanges
                    |> Seq.toList
                    |> List.map (fun range ->
                        DrawCallInfo(
                            FirstIndex = range.Min,
                            FaceVertexCount = range.Size + 1,
                            FirstInstance = 0,
                            InstanceCount = 1,
                            BaseVertex = 0
                        )
                    )
            )


    type ChangeableBuffer(elementType : Type) =
        inherit Mod.AbstractMod<IBuffer>()

        let elementSize = Marshal.SizeOf elementType
        let rw = new ReaderWriterLockSlim()
        let mutable capacity = 0n
        let mutable storage = 0n
        let readers = HashSet<AdaptiveBufferReader>()

        let removeReader(r : AdaptiveBufferReader) =
            lock readers (fun () -> readers.Remove r |> ignore)

        let addDirty (r : Range1i) =
            let all = lock readers (fun () -> readers |> HashSet.toArray)
            all |> Array.iter (fun reader -> reader.AddDirty r)
            
        member x.Capacity = capacity

        member x.Resize(newCapacity : nativeint) =
            let changed = 
                ReaderWriterLock.write rw (fun () ->
                    if newCapacity = 0n then
                        if storage <> 0n then
                            Marshal.FreeHGlobal(storage)
                            storage <- 0n
                            capacity <- 0n
                            true
                        else
                            false
                    elif newCapacity <> capacity then
                        if storage = 0n then
                            capacity <- newCapacity
                            storage <- Marshal.AllocHGlobal(capacity)
                        else
                            capacity <- newCapacity
                            storage <- Marshal.ReAllocHGlobal(storage, capacity)

                        addDirty (Range1i(0, int capacity - 1))
                        true
                    else
                        false
                )

            if changed then transact (fun () -> x.MarkOutdated())

        member x.Write(index : int, data : Array) =
            let size = elementSize * data.Length |> nativeint
            let offset = index * elementSize |> nativeint

            ReaderWriterLock.read rw (fun () ->
                let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                try Marshal.Copy(gc.AddrOfPinnedObject(), storage + offset, int size)
                finally gc.Free()
            )

            addDirty (Range1i.FromMinAndSize(int offset, int size - 1))
            transact (fun () -> x.MarkOutdated())

        override x.Compute() =
            NativeMemoryBuffer(storage, int capacity) :> IBuffer

        member private x.GetReader() =
            let r = new AdaptiveBufferReader(x, removeReader)
            lock readers (fun () -> readers.Add r |> ignore)
            r :> IAdaptiveBufferReader

        interface IAdaptiveBuffer with
            member x.GetReader() = x.GetReader()
            member x.ElementType = elementType

    
    



