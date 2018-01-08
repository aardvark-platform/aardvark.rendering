﻿namespace Aardvark.SceneGraph

open System
open System.Threading
open System.Reflection
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Base.Monads.State
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<ReferenceEquality; NoComparison>]
type AdaptiveGeometry =
    {
        faceVertexCount  : int
        vertexCount      : int
        indices          : Option<BufferView>
        uniforms         : Map<Symbol,IMod>
        vertexAttributes : Map<Symbol,BufferView>
    }

type GeometrySignature =
    {
        indexType           : Type
        vertexBufferTypes   : Map<Symbol, Type>
        uniformTypes        : Map<Symbol, Type>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveGeometry =

    let ofIndexedGeometry (uniforms : list<Symbol * IMod>) (ig : IndexedGeometry) =
        let anyAtt = (ig.IndexedAttributes |> Seq.head).Value

        let faceVertexCount, index =
            match ig.IndexArray with
                | null -> anyAtt.Length, None
                | index -> index.Length, Some (BufferView.ofArray index)

        let vertexCount =
            anyAtt.Length
                
    
        {
            faceVertexCount = faceVertexCount
            vertexCount = vertexCount
            indices = index
            uniforms = Map.ofList uniforms
            vertexAttributes = ig.IndexedAttributes |> SymDict.toMap |> Map.map (fun _ -> BufferView.ofArray)
        }



type IManagedBufferWriter =
    inherit IAdaptiveObject
    abstract member Write : AdaptiveToken -> unit

type IManagedBuffer =
    inherit IDisposable
    inherit IMod<IBuffer>
    abstract member Clear : unit -> unit
    abstract member Capacity : int
    abstract member Set : Range1l * byte[] -> unit
    abstract member Add : Range1l * BufferView -> IDisposable
    abstract member Add : int * IMod -> IDisposable
    abstract member ElementType : Type

type IManagedBuffer<'a when 'a : unmanaged> =
    inherit IManagedBuffer
    abstract member Count : int
    abstract member Item : int -> 'a with get, set
    abstract member Set : Range1l * 'a[] -> unit

[<AutoOpen>]
module private ManagedBufferImplementation =

    type ManagedBuffer<'a when 'a : unmanaged>(runtime : IRuntime) =
        inherit DirtyTrackingAdaptiveObject<ManagedBufferWriter>()
        static let asize = sizeof<'a> |> nativeint
        let store = runtime.CreateMappedBuffer()

        let bufferWriters = Dict<BufferView, ManagedBufferWriter<'a>>()
        let uniformWriters = Dict<IMod, ManagedBufferSingleWriter<'a>>()

        member x.Clear() =
            store.Resize 0n

        member x.Add(range : Range1l, view : BufferView) =
            let mutable isNew = false
            let res = lock x (fun () ->
                let count = range.Size + 1L

                let writer = 
                    bufferWriters.GetOrCreate(view, fun view ->
                        isNew <- true
                        let remove w =
                            x.Dirty.Remove w |> ignore
                            bufferWriters.Remove view |> ignore

                        let data = BufferView.download 0 (int count) view
                        let real : IMod<'a[]> = data |> PrimitiveValueConverter.convertArray view.ElementType
                        let w = new ManagedBufferWriter<'a>(remove, real, store)
                        x.Dirty.Add w |> ignore
                        w
                    )


                if writer.AddRef range then
                    let min = nativeint(range.Min + count) * asize
                    if store.Capacity < min then
                        store.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)
       
                    lock writer (fun () -> 
                        if not writer.OutOfDate then
                            writer.Write(AdaptiveToken.Top, range)
                    )

                { new IDisposable with
                    member x.Dispose() =
                        writer.RemoveRef range |> ignore
                }
            )
            if isNew then transact (fun () -> x.MarkOutdated ())
            res

        member x.Add(index : int, data : IMod) =
            let mutable isNew = false
            let res = lock x (fun () ->
                let writer =
                    uniformWriters.GetOrCreate(data, fun data ->
                        isNew <- true
                        let remove w =
                            x.Dirty.Remove w |> ignore
                            uniformWriters.Remove data |> ignore

                        let real : IMod<'a> = data |> PrimitiveValueConverter.convertValue
                        let w = new ManagedBufferSingleWriter<'a>(remove, real, store)
                        x.Dirty.Add w |> ignore
                        w
                    )
 
                let range = Range1l(int64 index, int64 index)
                if writer.AddRef range then
                    let min = nativeint (index + 1) * asize
                    if store.Capacity < min then
                        store.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)
                            
                    lock writer (fun () -> 
                        if not writer.OutOfDate then
                            writer.Write(AdaptiveToken.Top, range)
                    )



                        
                { new IDisposable with
                    member x.Dispose() =
                        writer.RemoveRef range |> ignore
                }
            )
            if isNew then
                transact (fun () -> x.MarkOutdated ())
            res

        member x.Set(range : Range1l, value : byte[]) =
            let count = range.Size + 1L
            let e = nativeint(range.Min + count) * asize
            if store.Capacity < e then
                store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
            try
                let ptr = gc.AddrOfPinnedObject()
                let lv = value.Length |> nativeint
                let mutable remaining = nativeint count * asize
                let mutable offset = nativeint range.Min * asize
                while remaining >= lv do
                    store.Write(ptr, offset, lv)
                    offset <- offset + lv
                    remaining <- remaining - lv

                if remaining > 0n then
                    store.Write(ptr, offset, remaining)

            finally
                gc.Free()

        member x.Set(index : int, value : 'a) =
            let e = nativeint (index + 1) * asize
            if store.Capacity < e then
                store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
            try store.Write(gc.AddrOfPinnedObject(), nativeint index * asize, asize)
            finally gc.Free()

        member x.Get(index : int) =
            let mutable res = Unchecked.defaultof<'a>
            store.Read(&&res |> NativePtr.toNativeInt, nativeint index * asize, asize)
            res

        member x.Set(range : Range1l, value : 'a[]) =
            let e = nativeint(range.Max + 1L) * asize
            if store.Capacity < e then
                store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
            try store.Write(gc.AddrOfPinnedObject(), nativeint range.Min * asize, nativeint(range.Size + 1L) * asize)
            finally gc.Free()

        member x.GetValue(token : AdaptiveToken) =
            x.EvaluateAlways' token (fun token dirty ->
                for d in dirty do
                    d.Write(token)
                store.GetValue(token)
            )

        member x.Capacity = store.Capacity
        member x.Count = store.Capacity / asize |> int

        member x.Dispose() =
            store.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IMod with
            member x.IsConstant = false
            member x.GetValue c = x.GetValue c :> obj

        interface IMod<IBuffer> with
            member x.GetValue c = x.GetValue c

        interface ILockedResource with
            member x.Lock = store.Lock
            member x.OnLock u = ()
            member x.OnUnlock u = ()

        interface IManagedBuffer with
            member x.Clear() = x.Clear()
            member x.Add(range : Range1l, view : BufferView) = x.Add(range, view)
            member x.Add(index : int, data : IMod) = x.Add(index, data)
            member x.Set(range : Range1l, value : byte[]) = x.Set(range, value)
            member x.Capacity = x.Capacity |> int
            member x.ElementType = typeof<'a>

        interface IManagedBuffer<'a> with
            member x.Count = x.Count
            member x.Item
                with get i = x.Get i
                and set i v = x.Set(i,v)
            member x.Set(range : Range1l, value : 'a[]) = x.Set(range, value)

    and [<AbstractClass>] ManagedBufferWriter(remove : ManagedBufferWriter -> unit) =
        inherit AdaptiveObject()
        let mutable refCount = 0
        let targetRegions = ReferenceCountingSet<Range1l>()

        abstract member Write : AdaptiveToken * Range1l -> unit
        abstract member Release : unit -> unit

        member x.AddRef(range : Range1l) : bool =
            lock x (fun () ->
                targetRegions.Add range
            )

        member x.RemoveRef(range : Range1l) : bool = 
            lock x (fun () ->
                targetRegions.Remove range |> ignore
                if targetRegions.Count = 0 then
                    x.Release()
                    remove x
                    let mutable foo = 0
                    x.Outputs.Consume(&foo) |> ignore
                    true
                else
                    false
            )

        member x.Write(token : AdaptiveToken) =
            x.EvaluateAlways token (fun token ->
                if x.OutOfDate then
                    for r in targetRegions do
                        x.Write(token, r)
            )

        interface IManagedBufferWriter with
            member x.Write c = x.Write c

    and ManagedBufferWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : IMod<'a[]>, store : IMappedBuffer) =
        inherit ManagedBufferWriter(remove)
        static let asize = sizeof<'a> |> nativeint

        override x.Release() = ()

        override x.Write(token, target) =
            let v = data.GetValue(token)
            let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
            try 
                store.Write(gc.AddrOfPinnedObject(), nativeint target.Min * asize, nativeint v.Length * asize)
            finally 
                gc.Free()

    and ManagedBufferSingleWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : IMod<'a>, store : IMappedBuffer) =
        inherit ManagedBufferWriter(remove)
        static let asize = sizeof<'a> |> nativeint
            
        override x.Release() = ()

        override x.Write(token, target) =
            let v = data.GetValue(token)
            let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
            try store.Write(gc.AddrOfPinnedObject(), nativeint target.Min * asize, asize)
            finally gc.Free()

module ManagedBuffer =

    let private ctorCache = Dict<Type, ConstructorInfo>()

    let private ctor (t : Type) =
        lock ctorCache (fun () ->
            ctorCache.GetOrCreate(t, fun t ->
                let tb = typedefof<ManagedBuffer<int>>.MakeGenericType [|t|]
                tb.GetConstructor(
                    BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.CreateInstance,
                    Type.DefaultBinder,
                    [| typeof<IRuntime> |],
                    null
                )
            )
        )

    let create (t : Type) (runtime : IRuntime) =
        let ctor = ctor t
        ctor.Invoke [| runtime |] |> unbox<IManagedBuffer>


type private LayoutManager<'a>() =
    let manager = MemoryManager.createNop()
    let store = Dict<'a, managedptr>()
    let cnts = Dict<managedptr, 'a * ref<int>>()


    member x.Alloc(key : 'a, size : int) =
        match store.TryGetValue key with
            | (true, v) -> 
                let _,r = cnts.[v]
                Interlocked.Increment &r.contents |> ignore
                v
            | _ ->
                let v = manager.Alloc (nativeint size)
                let r = ref 1
                cnts.[v] <- (key,r)
                store.[key] <- (v)
                v


    member x.TryAlloc(key : 'a, size : int) =
        match store.TryGetValue key with
            | (true, v) -> 
                let _,r = cnts.[v]
                Interlocked.Increment &r.contents |> ignore
                false, v
            | _ ->
                let v = manager.Alloc (nativeint size)
                let r = ref 1
                cnts.[v] <- (key,r)
                store.[key] <- (v)
                true, v

    member x.Free(value : managedptr) =
        match cnts.TryGetValue value with
            | (true, (k,r)) ->
                if Interlocked.Decrement &r.contents = 0 then
                    manager.Free value
                    cnts.Remove value |> ignore
                    store.Remove k |> ignore
            | _ ->
                ()


type ManagedDrawCall(call : DrawCallInfo, release : IDisposable) =
    let mutable isDisposed = false
    member x.Call = call
        
    member x.Dispose() = if not isDisposed then 
                            release.Dispose()
                            isDisposed <- true
    interface IDisposable with
        member x.Dispose() = x.Dispose()

type ManagedPool(runtime : IRuntime, signature : GeometrySignature) =
    static let zero : byte[] = Array.zeroCreate 1280000
    let mutable count = 0
    let indexManager = LayoutManager<Option<BufferView> * int>()
    let vertexManager = LayoutManager<Map<Symbol, BufferView>>()
    let instanceManager = LayoutManager<Map<Symbol, IMod>>()

    let indexBuffer = new ManagedBuffer<int>(runtime) :> IManagedBuffer<int>
    let vertexBuffers = signature.vertexBufferTypes |> Map.toSeq |> Seq.map (fun (k,t) -> k, ManagedBuffer.create t runtime) |> SymDict.ofSeq
    let instanceBuffers = signature.uniformTypes |> Map.toSeq |> Seq.map (fun (k,t) -> k, ManagedBuffer.create t runtime) |> SymDict.ofSeq
    let vertexDisposables = Dictionary<BufferView, IDisposable>()


    let vertexBufferTypes = Map.toArray signature.vertexBufferTypes
    let uniformTypes = Map.toArray signature.uniformTypes

    member x.Runtime = runtime

    member x.Add(g : AdaptiveGeometry) =
        lock x (fun () ->
            let ds = List()
            let fvc = g.faceVertexCount
            let vertexCount = g.vertexCount
            
            
            let vertexPtr = vertexManager.Alloc(g.vertexAttributes, vertexCount)
            let vertexRange = Range1l(int64 vertexPtr.Offset, int64 vertexPtr.Offset + int64 vertexCount - 1L)
            for (k,t) in vertexBufferTypes do
                let target = vertexBuffers.[k]
                match Map.tryFind k g.vertexAttributes with
                    | Some v -> target.Add(vertexRange, v) |> ds.Add
                    | None -> target.Set(vertexRange, zero)
            


            let instancePtr = instanceManager.Alloc(g.uniforms, 1)
            let instanceIndex = int instancePtr.Offset
            for (k,t) in uniformTypes do
                let target = instanceBuffers.[k]
                match Map.tryFind k g.uniforms with
                    | Some v -> target.Add(instanceIndex, v) |> ds.Add
                    | None -> target.Set(Range1l(int64 instanceIndex, int64 instanceIndex), zero)

            let isNew, indexPtr = indexManager.TryAlloc((g.indices, fvc), fvc)
            let indexRange = Range1l(int64 indexPtr.Offset, int64 indexPtr.Offset + int64 fvc - 1L)
            match g.indices with
                | Some v -> indexBuffer.Add(indexRange, v) |> ds.Add
                | None -> if isNew then indexBuffer.Set(indexRange, Array.init fvc id)

            count <- count + 1

            let disposable =
                { new IDisposable with
                    member __.Dispose() = 
                        lock x (fun () ->
                            count <- count - 1
                            if count = 0 then 
                                for b in vertexBuffers.Values do b.Clear()
                                for b in instanceBuffers.Values do b.Clear()
                                indexBuffer.Clear() 
                            for d in ds do d.Dispose()
                            vertexManager.Free vertexPtr
                            instanceManager.Free instancePtr
                            indexManager.Free indexPtr
                        )
                }

            let call =
                DrawCallInfo(
                    FaceVertexCount = fvc,
                    FirstIndex = int indexPtr.Offset,
                    FirstInstance = int instancePtr.Offset,
                    InstanceCount = 1,
                    BaseVertex = int vertexPtr.Offset
                )

            
            new ManagedDrawCall(call, disposable)
        )

    member x.VertexAttributes =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = Seq.empty
            member x.TryGetAttribute(sem : Symbol) =
                match vertexBuffers.TryGetValue sem with
                    | (true, v) -> Some (BufferView(v, v.ElementType))
                    | _ -> None
        }

    member x.InstanceAttributes =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = Seq.empty
            member x.TryGetAttribute(sem : Symbol) =
                match instanceBuffers.TryGetValue sem with
                    | (true, v) -> Some (BufferView(v, v.ElementType))
                    | _ -> None
        }

    member x.IndexBuffer =
        BufferView(indexBuffer, indexBuffer.ElementType)

type DrawCallBuffer(runtime : IRuntime, indexed : bool) =
    inherit Mod.AbstractMod<IIndirectBuffer>()

    let indices = Dict<DrawCallInfo, int>()
    let calls = List<DrawCallInfo>()
    let store = runtime.CreateMappedIndirectBuffer(indexed)

    let locked x (f : unit -> 'a) =
        lock x f

    let add x (call : DrawCallInfo) =
        locked x (fun () ->
            if indices.ContainsKey call then 
                false
            else
                store.Resize(Fun.NextPowerOfTwo (calls.Count + 1))
                let count = calls.Count
                indices.[call] <- count
                calls.Add call
                store.Count <- calls.Count
                store.[count] <- call
                true
        )

    let remove x (call : DrawCallInfo) =
        locked x (fun () ->
            match indices.TryRemove call with
                | (true, index) ->
                    if calls.Count = 1 then
                        calls.Clear()
                        store.Resize(0)
                    elif index = calls.Count-1 then
                        calls.RemoveAt index
                    else
                        let lastIndex = calls.Count - 1
                        let last = calls.[lastIndex]
                        indices.[last] <- index
                        calls.[index] <- last
                        store.[index] <- last
                        calls.RemoveAt lastIndex
                        
                    store.Count <- calls.Count
                    true
                | _ ->
                    false
        )

    member x.Add (call : DrawCallInfo) =
        if add x call then
            transact (fun () -> x.MarkOutdated())
            true
        else
            false

    member x.Remove(call : DrawCallInfo) =
        if remove x call then
            transact (fun () -> x.MarkOutdated())
            true
        else
            false

    interface ILockedResource with
        member x.Lock = store.Lock
        member x.OnLock u = ()
        member x.OnUnlock u = ()

    override x.Compute(token) =
        store.GetValue()

    override x.Finalize() =
        try store.Dispose()
        with _ -> ()    

[<AbstractClass; Sealed; Extension>]
type IRuntimePoolExtensions private() =

    [<Extension>]
    static member CreateManagedPool(this : IRuntime, signature : GeometrySignature) =
        new ManagedPool(this, signature)

    [<Extension>]
    static member CreateManagedBuffer<'a when 'a : unmanaged>(this : IRuntime) : IManagedBuffer<'a> =
        new ManagedBuffer<'a>(this) :> IManagedBuffer<'a>

    [<Extension>]
    static member CreateManagedBuffer(this : IRuntime, elementType : Type) : IManagedBuffer =
        this |> ManagedBuffer.create elementType

    [<Extension>]
    static member CreateDrawCallBuffer(this : IRuntime, indexed : bool) =
        new DrawCallBuffer(this, indexed)

[<AutoOpen>]
module ManagedPoolSg =

    module Sg =
        type PoolNode(pool : ManagedPool, calls : aset<ManagedDrawCall>, mode : IMod<IndexedGeometryMode>) =
            interface ISg
            member x.Pool = pool
            member x.Calls = calls
            member x.Mode = mode

        let pool (pool : ManagedPool) (calls : aset<ManagedDrawCall>) (mode : IMod<IndexedGeometryMode>)=
            PoolNode(pool, calls, mode) :> ISg


module ``Pool Semantics`` =
    [<Aardvark.Base.Ag.Semantic>]
    type PoolSem() =
        member x.RenderObjects(p : Sg.PoolNode) =
            aset {
                let pool = p.Pool
                let ro = Aardvark.SceneGraph.Semantics.RenderObject.create()


                let r = (p.Calls |> ASet.map (fun mdc -> mdc.Call)).GetReader()
                let calls =
                    let buffer = DrawCallBuffer(pool.Runtime, true) // who manages this? using finalizer for now
                    Mod.custom (fun self ->
                        let deltas = r.GetOperations self
                        for d in deltas do
                            match d with
                                | Add(_,v) -> buffer.Add v |> ignore
                                | Rem(_,v) -> buffer.Remove v |> ignore

                        buffer.GetValue()
                    )

                ro.Mode <- p.Mode
                ro.Indices <- Some pool.IndexBuffer
                ro.VertexAttributes <- pool.VertexAttributes
                ro.InstanceAttributes <- pool.InstanceAttributes
                ro.IndirectBuffer <- calls // |> ASet.toMod |> Mod.map (fun calls -> calls |> Seq.toArray |> ArrayBuffer :> IBuffer)
                //ro.DrawCallInfos <- p.Calls |> ASet.toMod |> Mod.map Seq.toList
                yield ro :> IRenderObject
                    
            }





   