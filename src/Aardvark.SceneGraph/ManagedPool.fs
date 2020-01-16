namespace Aardvark.SceneGraph

open System
open System.Threading
open System.Reflection
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
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
        uniforms         : Map<Symbol,IAdaptiveValue>
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

    let ofIndexedGeometry (uniforms : list<Symbol * IAdaptiveValue>) (ig : IndexedGeometry) =
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
    inherit aval<IBuffer>
    abstract member Clear : unit -> unit
    abstract member Capacity : int
    abstract member Set : Range1l * byte[] -> unit
    abstract member Add : Range1l * BufferView -> IDisposable
    abstract member Add : int * IAdaptiveValue -> IDisposable
    abstract member ElementType : Type

type IManagedBuffer<'a when 'a : unmanaged> =
    inherit IManagedBuffer
    abstract member Count : int
    abstract member Item : int -> 'a with get, set
    abstract member Set : Range1l * 'a[] -> unit

[<AutoOpen>]
module private ManagedBufferImplementation =

    type ManagedBuffer<'a when 'a : unmanaged>(runtime : IRuntime) =
        inherit AdaptiveObject()
        static let asize = sizeof<'a> |> nativeint

        let mutable store = runtime.CreateBuffer(0n)

        let bufferWriters = Dict<BufferView, ManagedBufferWriter<'a>>()
        let uniformWriters = Dict<IAdaptiveValue, ManagedBufferSingleWriter<'a>>()

        let dirtyLock = obj()
        let mutable dirty = System.Collections.Generic.HashSet<ManagedBufferWriter>()

        override x.InputChangedObject(transaction, object) =
            match object with
            | :? ManagedBufferWriter as writer -> 
                lock dirtyLock (fun () -> dirty.Add writer |> ignore)
            | _ ->
                ()

        member x.Resize (sz : nativeint) =
            let newStore = runtime.CreateBuffer(sz)
            if sz > 0n && store.SizeInBytes > 0n then
                (runtime :> IBufferRuntime).Copy(store, 0n, newStore, 0n, min sz store.SizeInBytes)
            runtime.DeleteBuffer store
            store <- newStore
            transact (fun () -> x.MarkOutdated())

        member x.Clear() =
            x.Resize(0n)
            
        member x.Store
            with get() = store

        member x.Add(range : Range1l, view : BufferView) =
            let mutable isNew = false
            let res = lock x (fun () ->
                let count = range.Size + 1L

                let writer = 
                    bufferWriters.GetOrCreate(view, fun view ->
                        isNew <- true
                        let data = BufferView.download 0 (int count) view
                        let real : aval<'a[]> = data |> PrimitiveValueConverter.convertArray view.ElementType
                        let remove w =
                            lock dirtyLock (fun () -> dirty.Remove w |> ignore)
                            bufferWriters.Remove view |> ignore
                            view.Buffer.Outputs.Remove(real) |> ignore // remove converter from Output of data Mod (in case there is no converter remove will do nothing, but Release of ManagedBufferSingleWriter will)

                        let w = new ManagedBufferWriter<'a>(remove, real, x)
                        lock dirtyLock (fun () -> dirty.Add w |> ignore)
                        w
                    )


                if writer.AddRef range then
                    let min = nativeint(range.Min + count) * asize
                    if x.Store.SizeInBytes < min then
                        x.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)
       
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

        member x.Add(index : int, data : IAdaptiveValue) =
            let mutable isNew = false
            let res = lock x (fun () ->
                let writer =
                    uniformWriters.GetOrCreate(data, fun data ->
                        isNew <- true
                        let real : aval<'a> = data |> PrimitiveValueConverter.convertValue
                        let remove w =
                            lock dirtyLock (fun () -> dirty.Remove w |> ignore)
                            uniformWriters.Remove data |> ignore
                            data.Outputs.Remove(real) |> ignore // remove converter from Output of data Mod (in case there is no converter remove will do nothing, but Release of ManagedBufferSingleWriter will)

                        let w = new ManagedBufferSingleWriter<'a>(remove, real, x)
                        lock dirtyLock (fun () -> dirty.Add w |> ignore)
                        w
                    )
 
                let range = Range1l(int64 index, int64 index)
                if writer.AddRef range then
                    let min = nativeint (index + 1) * asize
                    if x.Store.SizeInBytes < min then
                        x.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)
                            
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
            if x.Store.SizeInBytes < e then
                x.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
            try
                let ptr = gc.AddrOfPinnedObject()
                let lv = value.Length |> nativeint
                let mutable remaining = nativeint count * asize
                let mutable offset = nativeint range.Min * asize
                while remaining >= lv do
                    x.Store.Upload(offset, ptr, lv)
                    offset <- offset + lv
                    remaining <- remaining - lv

                if remaining > 0n then
                    x.Store.Upload(offset, ptr, remaining)

            finally
                gc.Free()

        member x.Set(index : int, value : 'a) =
            let e = nativeint (index + 1) * asize
            if x.Store.SizeInBytes < e then
                x.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

            let offset = nativeint index * asize
            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
            try x.Store.Upload(offset, gc.AddrOfPinnedObject(), asize)
            finally gc.Free()

        member x.Get(index : int) =
            let offset = nativeint index * asize
            let mutable res = Unchecked.defaultof<'a>
            x.Store.Download(offset, &&res |> NativePtr.toNativeInt, asize)
            res

        member x.Set(range : Range1l, value : 'a[]) =
            let e = nativeint(range.Max + 1L) * asize
            if x.Store.SizeInBytes < e then
                x.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)

            let offset = nativeint range.Min * asize
            let size = nativeint(range.Size + 1L) * asize
            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
            try x.Store.Upload(offset, gc.AddrOfPinnedObject(), size)
            finally gc.Free()

        member x.GetValue(token : AdaptiveToken) =
            x.EvaluateAlways token (fun token ->
                let dirty = 
                    lock dirtyLock (fun () ->
                        let d = dirty
                        dirty <- System.Collections.Generic.HashSet()
                        d
                    )
                for d in dirty do
                    d.Write(token)
                x.Store :> IBuffer
            )

        member x.Capacity = store.SizeInBytes
        member x.Count = store.SizeInBytes / asize |> int

        member x.Dispose() =
            runtime.DeleteBuffer store
            store <- Unchecked.defaultof<_>
            x.MarkOutdated()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IAdaptiveValue with
            member x.ContentType = typeof<IBuffer>
            member x.IsConstant = false
            member x.GetValueUntyped c = x.GetValue c :> obj

        interface aval<IBuffer> with
            member x.GetValue c = x.GetValue c
            
        interface IManagedBuffer with
            member x.Clear() = x.Clear()
            member x.Add(range : Range1l, view : BufferView) = x.Add(range, view)
            member x.Add(index : int, data : IAdaptiveValue) = x.Add(index, data)
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
                    x.Outputs.Clear()
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

    and ManagedBufferWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : aval<'a[]>, buffer : ManagedBuffer<'a>) =
        inherit ManagedBufferWriter(remove)
        static let asize = sizeof<'a> |> nativeint

        override x.Release() = 
            // in case the data Mod is a PrimitiveValueConverter, it would be garbage collected and removal of the output is not essential
            // in case the data Mod is directly from the application (no converter), removing the Writer from its Output is essential
            data.Outputs.Remove(x) |> ignore

        override x.Write(token, target) =
            let v = data.GetValue(token)
            let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
            try 
                buffer.Store.Upload(nativeint target.Min * asize, gc.AddrOfPinnedObject(), nativeint v.Length * asize)
            finally 
                gc.Free()

    and ManagedBufferSingleWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : aval<'a>, buffer : ManagedBuffer<'a>) =
        inherit ManagedBufferWriter(remove)
        static let asize = sizeof<'a> |> nativeint
            
        override x.Release() = 
            // in case the data Mod is a PrimitiveValueConverter, it would be garbage collected and removal of the output is not essential
            // in case the data Mod is directly from the application (no converter), removing the Writer from its Output is essential
            data.Outputs.Remove(x) |> ignore

        override x.Write(token, target) =
            let v = data.GetValue(token)
            let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
            try buffer.Store.Upload(nativeint target.Min * asize, gc.AddrOfPinnedObject(), asize)
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
    let instanceManager = LayoutManager<Map<Symbol, IAdaptiveValue>>()

    let indexBuffer = new ManagedBuffer<int>(runtime) :> IManagedBuffer<int>
    let vertexBuffers = signature.vertexBufferTypes |> Map.toSeq |> Seq.map (fun (k,t) -> k, ManagedBuffer.create t runtime) |> SymDict.ofSeq
    let instanceBuffers = signature.uniformTypes |> Map.toSeq |> Seq.map (fun (k,t) -> k, ManagedBuffer.create t runtime) |> SymDict.ofSeq
    let vertexDisposables = Dictionary<BufferView, IDisposable>()


    let vertexBufferTypes = Map.toArray signature.vertexBufferTypes
    let uniformTypes = Map.toArray signature.uniformTypes

    member x.Runtime = runtime

    member x.Count
        with get() = count

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
            member x.All = vertexBuffers |> Seq.map (fun v -> (v.Key, BufferView(v.Value, v.Value.ElementType)))
            member x.TryGetAttribute(sem : Symbol) =
                match vertexBuffers.TryGetValue sem with
                    | (true, v) -> Some (BufferView(v, v.ElementType))
                    | _ -> None
        }

    member x.InstanceAttributes =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = instanceBuffers |> Seq.map (fun v -> (v.Key, BufferView(v.Value, v.Value.ElementType)))
            member x.TryGetAttribute(sem : Symbol) =
                match instanceBuffers.TryGetValue sem with
                    | (true, v) -> Some (BufferView(v, v.ElementType))
                    | _ -> None
        }

    member x.IndexBuffer =
        BufferView(indexBuffer, indexBuffer.ElementType)

type DrawCallBuffer(runtime : IRuntime, indexed : bool) =
    inherit AVal.AbstractVal<IIndirectBuffer>()

    let indices = Dict<DrawCallInfo, int>()
    let calls = List<DrawCallInfo>()
    let store = new ManagedBuffer<DrawCallInfo>(runtime)
    
    let locked x (f : unit -> 'a) =
        lock x f

    let add x (call : DrawCallInfo) =
        locked x (fun () ->
            if indices.ContainsKey call then 
                false
            else
                let index = calls.Count
                indices.[call] <- calls.Count
                calls.Add call
                store.Set(index, call)
                true
        )

    let remove x (call : DrawCallInfo) =
        locked x (fun () ->
            match indices.TryRemove call with
                | (true, index) ->
                    if calls.Count = 1 then
                        calls.Clear()
                        store.Resize(0n)
                    elif index = calls.Count-1 then
                        calls.RemoveAt index
                    else
                        let lastIndex = calls.Count - 1
                        let last = calls.[lastIndex]
                        indices.[last] <- index
                        calls.[index] <- last
                        store.Set(index, last)
                        calls.RemoveAt lastIndex
                        
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
            
    override x.Compute(token) =
        let inner = store.GetValue(token)
        IndirectBuffer(inner, calls.Count) :> IIndirectBuffer

    override x.Finalize() =
        try store.Dispose()
        with _ -> ()    


//type DrawCallBuffer(runtime : IRuntime, indexed : bool) =
//    inherit AVal.AbstractMod<IIndirectBuffer>()

//    let indices = Dict<DrawCallInfo, int>()
//    let calls = List<DrawCallInfo>()
//    let store = runtime.CreateMappedIndirectBuffer(indexed)

//    let locked x (f : unit -> 'a) =
//        lock x f

//    let add x (call : DrawCallInfo) =
//        locked x (fun () ->
//            if indices.ContainsKey call then 
//                false
//            else
//                store.Resize(Fun.NextPowerOfTwo (calls.Count + 1))
//                let count = calls.Count
//                indices.[call] <- count
//                calls.Add call
//                store.Count <- calls.Count
//                store.[count] <- call
//                true
//        )

//    let remove x (call : DrawCallInfo) =
//        locked x (fun () ->
//            match indices.TryRemove call with
//                | (true, index) ->
//                    if calls.Count = 1 then
//                        calls.Clear()
//                        store.Resize(0)
//                    elif index = calls.Count-1 then
//                        calls.RemoveAt index
//                    else
//                        let lastIndex = calls.Count - 1
//                        let last = calls.[lastIndex]
//                        indices.[last] <- index
//                        calls.[index] <- last
//                        store.[index] <- last
//                        calls.RemoveAt lastIndex
                        
//                    store.Count <- calls.Count
//                    true
//                | _ ->
//                    false
//        )

//    member x.Add (call : DrawCallInfo) =
//        if add x call then
//            transact (fun () -> x.MarkOutdated())
//            true
//        else
//            false

//    member x.Remove(call : DrawCallInfo) =
//        if remove x call then
//            transact (fun () -> x.MarkOutdated())
//            true
//        else
//            false

//    interface ILockedResource with
//        member x.Lock = store.Lock
//        member x.OnLock u = ()
//        member x.OnUnlock u = ()

//    override x.Compute(token) =
//        store.GetValue()

//    override x.Finalize() =
//        try store.Dispose()
//        with _ -> ()    

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
        type PoolNode(pool : ManagedPool, calls : aset<ManagedDrawCall>, mode : IndexedGeometryMode) =
            interface ISg
            member x.Pool = pool
            member x.Calls = calls
            member x.Mode = mode
                
        let pool (pool : ManagedPool) (calls : aset<ManagedDrawCall>) (mode : IndexedGeometryMode)=
            PoolNode(pool, calls, mode) :> ISg


module ``Pool Semantics`` =
    [<Aardvark.Base.Ag.Semantic>]
    type PoolSem() =
        member x.RenderObjects(p : Sg.PoolNode) =
            
            let pool = p.Pool
            
            let r = (p.Calls |> ASet.map (fun mdc -> mdc.Call)).GetReader()
            let calls =
                let buffer = DrawCallBuffer(pool.Runtime, true) // who manages this? using finalizer for now
                AVal.custom (fun self ->
                    let deltas = r.GetChanges self
                    for d in deltas do
                        match d with
                            | Add(_,v) -> buffer.Add v |> ignore
                            | Rem(_,v) -> buffer.Remove v |> ignore

                    buffer.GetValue()
                )
            
            let mutable ro = Unchecked.defaultof<RenderObject>

            aset {
                let! c = calls;
                if c.Count > 0 then
                    if ((ro :> obj) = null) then
                        ro <- Aardvark.SceneGraph.Semantics.RenderObject.create()
                        ro.Mode <- p.Mode
                        ro.Indices <- Some pool.IndexBuffer
                        ro.VertexAttributes <- pool.VertexAttributes
                        ro.InstanceAttributes <- pool.InstanceAttributes
                        ro.IndirectBuffer <- calls
                    yield (ro :> IRenderObject)
                else
                    ro <- Unchecked.defaultof<RenderObject>
            }

   