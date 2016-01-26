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
                
        mutable IsActive   : IMod<bool>
        mutable RenderPass : uint64
                
        mutable DrawCallInfos : IMod<list<DrawCallInfo>>
        mutable Mode          : IMod<IndexedGeometryMode>
        mutable Surface       : IMod<ISurface>
                              
        mutable DepthTest     : IMod<DepthTestMode>
        mutable CullMode      : IMod<CullMode>
        mutable BlendMode     : IMod<BlendMode>
        mutable FillMode      : IMod<FillMode>
        mutable StencilMode   : IMod<StencilMode>
                
        mutable Indices            : IMod<Array>
        mutable InstanceAttributes : IAttributeProvider
        mutable VertexAttributes   : IAttributeProvider
                
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

module AttributePacking =

    open System
    open System.Threading
    open System.Collections.Generic
    open System.Runtime.InteropServices

    let private indexArray = Symbol.Create "__IndexArray__"

    module private Converter =
        open System.Collections.Concurrent

        let convert =
            LookupTable.lookupTable [
                (typeof<int16>,     typeof<uint32>),    (fun (v : int16)    -> uint32 v) :> obj
                (typeof<int8>,      typeof<uint32>),    (fun (v : int8)     -> uint32 v) :> obj
                (typeof<uint16>,    typeof<uint32>),    (fun (v : uint16)   -> uint32 v) :> obj
                (typeof<uint8>,     typeof<uint32>),    (fun (v : uint8)    -> uint32 v) :> obj
            ]

        type CopyImpl<'a, 'b when 'a : unmanaged and 'b : unmanaged> () =
            static let sa = nativeint sizeof<'a>
            static let sb = nativeint sizeof<'b>
 
            static let copy =
                if sa = sb then
                    fun (src : nativeint,dst : nativeint,s : int) -> Marshal.Copy(src, dst, s)
                else
                    let convert = convert (typeof<'a>, typeof<'b>) |> unbox<'a -> 'b>
                    fun (src : nativeint, dst : nativeint, s : int) ->
                        let mutable src = src
                        let mutable dst = dst
                        let mutable s = s
                        while s > 0 do
                            NativePtr.write (NativePtr.ofNativeInt<'b> dst) (convert (NativePtr.read (NativePtr.ofNativeInt<'a> src)))
                            s <- s - 1
                            src <- src + sa
                            dst <- dst + sb 

            static member Copy(src : nativeint, dst : nativeint, size : int) =
                copy(src, dst, size)

        let private copyCache = ConcurrentDictionary<Type * Type,nativeint * nativeint * int -> unit>()
        let private createCopyFun (src : Type, dst : Type) =
            let t = typedefof<CopyImpl<int,int>>.MakeGenericType [|src; dst|]
            let d = t.GetMethod("Copy").CreateDelegate(typeof<Action<nativeint, nativeint, int>>) |> unbox<Action<nativeint, nativeint, int>>
            d.Invoke
        let copy (src : Type) (dst : Type) =
            copyCache.GetOrAdd((src,dst), Func<_,_>(createCopyFun))

        type private CopyOffsetImpl<'a when 'a : unmanaged>() =
            static let sa = nativeint sizeof<'a>
            static let sb = nativeint sizeof<uint32>
 
            static let copy =
                let convert = convert (typeof<'a>, typeof<uint32>) |> unbox<'a -> uint32>
                fun (src : nativeint, dst : nativeint, off : uint32, s : int) ->
                    let mutable src = src
                    let mutable dst = dst
                    let mutable s = s
                    while s > 0 do
                        let v = convert (NativePtr.read (NativePtr.ofNativeInt<'a> src))
                        NativePtr.write (NativePtr.ofNativeInt<uint32> dst) (off + v)
                        s <- s - 1
                        src <- src + sa
                        dst <- dst + sb 

            static member Copy(src : nativeint, dst : nativeint, off : uint32, size : int) =
                copy(src, dst, off, size)
        let private copyOffCache = ConcurrentDictionary<Type,nativeint * nativeint * uint32 * int -> unit>()
        let private createCopyOffFun (src : Type) =
            let t = typedefof<CopyOffsetImpl<int>>.MakeGenericType [|src|]
            let d = t.GetMethod("Copy").CreateDelegate(typeof<Action<nativeint, nativeint, uint32, int>>) |> unbox<Action<nativeint, nativeint, uint32, int>>
            d.Invoke
        let copyOffset (src : Type) =
            copyOffCache.GetOrAdd(src, Func<_,_>(createCopyOffFun))

    [<AbstractClass>]
    type private AdaptiveBuffer() =
        inherit Mod.AbstractMod<IBuffer>()

        abstract member ElementType : Type
        abstract member Reset : unit -> unit
        abstract member Capacity : int
        abstract member GetReader : unit -> IAdaptiveBufferReader
        abstract member Dispose : unit -> unit
        abstract member AddGeometry : IndexedGeometry * Range1i -> unit
        abstract member RemoveGeometry : IndexedGeometry -> unit

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IAdaptiveBuffer with
            member x.ElementType = x.ElementType
            member x.GetReader() = x.GetReader()

    type private AdaptiveBufferReader<'a>(buffer : AdaptiveBuffer<'a>) =
        inherit AdaptiveObject()

        let mutable dirty = RangeSet.empty
        let mutable initial = true

        member x.Dispose() =
            buffer.RemoveReader x
            dirty <- RangeSet.empty
            initial <- true

        member x.GetDirtyRanges(caller : IAdaptiveObject) =
            x.EvaluateAlways caller (fun () ->
                let b = buffer.GetValue x |> unbox<NativeMemoryBuffer>
                let dirty = Interlocked.Exchange(&dirty, RangeSet.empty)
                if initial then
                    initial <- false
                    b, RangeSet.ofList [Range1i(0, buffer.Capacity-1)]
                else
                    b, dirty
            )
    
        member x.Reset() =
            initial <- true
            dirty <- RangeSet.empty

        member x.AddDirty (r : Range1i) =
            Interlocked.Change(&dirty, RangeSet.insert r) |> ignore

        member x.RemoveDirty (r : Range1i) =
            Interlocked.Change(&dirty, RangeSet.remove r) |> ignore

        interface IAdaptiveBufferReader with
            member x.GetDirtyRanges(caller) = x.GetDirtyRanges(caller)

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    and private AdaptiveBuffer<'a>(sem : Symbol, input : IMod<Dictionary<IndexedGeometry, managedptr> * int>) =
        inherit AdaptiveBuffer()
        let elementSize = sizeof<'a>

        
        let mutable storage = 0n
        let mutable myCapacity = 0
        let mutable dirtyGeometries = Dictionary()
        let readers = HashSet<AdaptiveBufferReader<'a>>()

        let tryGetAttribute (sem : Symbol) (g : IndexedGeometry) =
            if sem = indexArray then Some g.IndexArray
            else
                match g.IndexedAttributes.TryGetValue sem with
                    | (true, arr) -> Some arr
                    | _ -> None

        let copy (arr : Array) (offset : nativeint) (size : int) =
            let et = arr.GetType().GetElementType()
            let memcpy = Converter.copy et typeof<'a>
            let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
            try
                memcpy(gc.AddrOfPinnedObject(), storage + offset, size)
            finally
                gc.Free() 

        let copyWithOffset (arr : Array) (offset : nativeint) (size : int) =
            if isNull arr then 
                let off = uint32 offset
                let index = Array.init (size / 4) (fun i -> off + uint32 i)
                let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                try
                    Converter.CopyImpl<uint32, uint32>.Copy(gc.AddrOfPinnedObject(), storage + offset, size)
                finally
                    gc.Free() 

            else
                let et = arr.GetType().GetElementType()
                let memcpy = Converter.copyOffset et
                let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                try
                    memcpy(gc.AddrOfPinnedObject(), storage + offset, uint32 offset, size)
                finally
                    gc.Free() 

        let write =
            if sem = indexArray then copyWithOffset
            else copy

        override x.Capacity = myCapacity

        override x.GetReader() =
            let r = new AdaptiveBufferReader<'a>(x)
            lock readers (fun () -> readers.Add r |> ignore)
            r :> IAdaptiveBufferReader

        member x.RemoveReader (r : IAdaptiveBufferReader) : unit =
            match r with
                | :? AdaptiveBufferReader<'a> as r ->
                    lock readers (fun () -> readers.Remove r |> ignore)
                    x.RemoveOutput r
                | _ ->
                    ()

        override x.ElementType = typeof<'a>

        override x.AddGeometry(geometry : IndexedGeometry, range : Range1i) =
            if storage <> 0n then
                let range = Range1i(elementSize * range.Min, elementSize * range.Max + elementSize - 1)
                dirtyGeometries.[geometry] <- range
                lock readers (fun () -> for r in readers do r.AddDirty range)

        override x.RemoveGeometry(geometry : IndexedGeometry) =
            if storage <> 0n then
                match dirtyGeometries.TryGetValue geometry with
                    | (true, range) ->
                        lock readers (fun () -> for r in readers do r.RemoveDirty range)
                        dirtyGeometries.Remove geometry |> ignore
                    | _ ->
                        ()

        override x.Reset() =
            let old = Interlocked.Exchange(&storage, 0n)
            if old <> 0n then
                Marshal.FreeHGlobal storage
                myCapacity <- 0
                dirtyGeometries.Clear()
                lock readers (fun () -> for r in readers do r.Reset())

        override x.Dispose() =
            let old = Interlocked.Exchange(&storage, 0n)
            if old <> 0n then
                Marshal.FreeHGlobal storage
                myCapacity <- 0
                dirtyGeometries.Clear()
                lock readers (fun () -> for r in readers do x.RemoveOutput r)
                readers.Clear()

        override x.Compute() =
            let ptrs, capacity = input.GetValue(x)
            let capacity = elementSize * capacity

            if storage = 0n then
                storage <- Marshal.AllocHGlobal capacity
                myCapacity <- capacity

                for (g, ptr) in Dictionary.toSeq ptrs do
                    match tryGetAttribute sem g with
                        | Some arr ->
                            let offset = ptr.Offset * nativeint elementSize
                            let size = ptr.Size * elementSize
                            let arraySize = arr.Length * elementSize
                            write arr offset (min size arraySize)
                        | _ ->
                            // TODO: what to do here? maybe respect NullBuffers here
                            ()

            else
                let dirty = dirtyGeometries |> Dictionary.toArray
                dirtyGeometries.Clear()

                if capacity <> myCapacity then
                    storage <- Marshal.ReAllocHGlobal(storage, nativeint capacity)
                    myCapacity <- capacity

                for (d, ptr) in dirty do
                    match tryGetAttribute sem d with
                        | Some arr ->
                            let offset = nativeint ptr.Min
                            let size = ptr.Size + 1
                            let arraySize = arr.Length * elementSize
                            write arr offset (min size arraySize)
                        | _ ->
                            ()

            NativeMemoryBuffer(storage, capacity) :> IBuffer


    let private createAdaptiveBuffer (t : Type) (sem : Symbol) (self : IMod<Dictionary<IndexedGeometry, managedptr> * int>) =
        let t = typedefof<AdaptiveBuffer<_>>.MakeGenericType [|t|]
        let ctor = t.GetConstructor [|typeof<Symbol>; typeof<IMod<Dictionary<IndexedGeometry, managedptr> * int>> |]
        ctor.Invoke [|sem :> obj; self :> obj|] |> unbox<AdaptiveBuffer>

    type PackingLayout(set : aset<IndexedGeometry>, elementTypes : Map<Symbol, Type>, shrinkThreshold : float) =
        inherit Mod.AbstractMod<Dictionary<IndexedGeometry, managedptr> * int>()
        //let elementTypes = Map.add indexArray typeof<uint32> elementTypes

        let mutable isDisposed = 0
        let reader = set.GetReader()
        let ptrs = Dictionary<IndexedGeometry, managedptr>()
        let mutable manager = MemoryManager.createNop()
        let mutable ranges = RangeSet.empty

        let buffers = Dictionary<Symbol, AdaptiveBuffer>()

        let faceVertexCount (i : IndexedGeometry) =
            match i.IndexArray with
             | null -> i.IndexedAttributes.Values |> Seq.head |> (fun s -> s.Length)
             | arr -> arr.Length

        let vertexCount (i : IndexedGeometry) =
            i.IndexedAttributes.Values |> Seq.head |> (fun s -> s.Length)


        member x.TryGetBuffer(sem : Symbol) =
            match buffers.TryGetValue(sem) with
                | (true, b) -> Some (b :> IAdaptiveBuffer)
                | _ ->
                    match Map.tryFind sem elementTypes with
                        | Some et ->
                            let b = createAdaptiveBuffer et sem x
                            buffers.[sem] <- b
                            Some (b :> IAdaptiveBuffer)
                        | None ->
                            None


        member x.DrawCallInfos =
            Mod.custom (fun self ->
                let _ = x.GetValue self
                ranges
                    |> Seq.toList
                    |> List.map (fun range ->
                        printfn "range = %A" range
                        DrawCallInfo(
                            FirstIndex = range.Min,
                            FaceVertexCount = range.Size + 1,
                            FirstInstance = 0,
                            InstanceCount = 1,
                            BaseVertex = 0
                        )
                    )
            )

//        member x.IndexBuffer =
//            match x.TryGetBuffer indexArray with
//                | Some b -> b
//                | None -> failwith "not possible"

        override x.Compute () =
            let deltas = reader.GetDelta(x)

            for d in deltas do
                match d with
                    | Add g ->
                        let ptr = g |> faceVertexCount |> manager.Alloc
                        ptrs.[g] <- ptr
                        ranges <- RangeSet.insert (Range1i.FromMinAndSize(int ptr.Offset, ptr.Size-1)) ranges
                        for b in buffers.Values do b.AddGeometry(g, Range1i.FromMinAndSize(int ptr.Offset, ptr.Size-1))

                    | Rem g ->
                        match ptrs.TryGetValue g with
                            | (true, ptr) ->
                                ranges <- RangeSet.remove (Range1i.FromMinAndSize(int ptr.Offset, ptr.Size-1)) ranges
                                for b in buffers.Values do b.RemoveGeometry(g)
                                manager.Free ptr
                                ptrs.Remove g |> ignore

                                // shrinking code:
                                if float manager.AllocatedBytes < shrinkThreshold * float manager.Capacity then
                                    ranges <- RangeSet.empty
                                    let newMan = MemoryManager.createNop()

                                    for (g,ptr) in Dictionary.toArray ptrs do
                                        let newPtr = newMan.Alloc(ptr.Size)
                                        ptrs.[g] <- newPtr
                                        ranges <- RangeSet.insert (Range1i.FromMinAndSize(int newPtr.Offset, newPtr.Size - 1)) ranges
                                    
                                    for b in buffers.Values do
                                        b.Reset()

                                    manager.Dispose()
                                    manager <- newMan


                            | _ ->
                                ()

            ptrs, manager.Capacity
            
        member x.Dispose() =
            if Interlocked.Exchange(&isDisposed, 1) = 0 then
                reader.Dispose()
                ptrs.Clear()
                manager.Dispose()
                ranges <- RangeSet.empty
                          

        interface IDisposable with
            member x.Dispose() = x.Dispose()
                

        interface IAttributeProvider with

            member x.All =
                Seq.empty

            member x.TryGetAttribute(s : Symbol) =
                match x.TryGetBuffer s with
                    | Some b -> BufferView(b, b.ElementType) |> Some
                    | None -> None

        new(set : aset<IndexedGeometry>, elementTypes : Map<Symbol, Type>) = new PackingLayout(set, elementTypes, 0.45)

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

                                if float manager.AllocatedBytes < shrinkThreshold * float manager.Capacity then
                                    let mutable newDrawRanges = RangeSet.empty
                                    let newManager = MemoryManager.createNop()
                                    let newRanges = Dictionary.empty

                                    for (g,ptr) in Dictionary.toList dataRanges do
                                        let nptr = newManager.Alloc(ptr.Size)
                                        newRanges.[g] <- nptr
                                        let r = Range1i.FromMinAndSize(int nptr.Offset, nptr.Size - 1)
                                        newDrawRanges <- RangeSet.insert r newDrawRanges
                                    
                                    manager.Dispose()
                                    manager <- newManager
                                    drawRanges <- newDrawRanges
                                    dataRanges <- newRanges
                                    resize()



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




