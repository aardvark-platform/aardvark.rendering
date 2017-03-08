namespace Aardvark.SceneGraph.Pool

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
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Base.Monads.State
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

//[<ReferenceEquality; NoComparison>]
//type AdaptiveGeometry =
//    {
//        mode             : IndexedGeometryMode
//        faceVertexCount  : int
//        vertexCount      : int
//        indices          : Option<BufferView>
//        uniforms         : Map<Symbol,IMod>
//        vertexAttributes : Map<Symbol,BufferView>
//    }
//
//type GeometrySignature =
//    {
//        mode                : IndexedGeometryMode
//        indexType           : Type
//        vertexBufferTypes   : Map<Symbol, Type>
//        uniformTypes        : Map<Symbol, Type>
//    }
//
//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
//module AdaptiveGeometry =
//
//    let ofIndexedGeometry (uniforms : list<Symbol * IMod>) (ig : IndexedGeometry) =
//        let anyAtt = (ig.IndexedAttributes |> Seq.head).Value
//
//        let faceVertexCount, index =
//            match ig.IndexArray with
//                | null -> anyAtt.Length, None
//                | index -> index.Length, Some (BufferView.ofArray index)
//
//        let vertexCount =
//            anyAtt.Length
//                
//    
//        {
//            mode = ig.Mode
//            faceVertexCount = faceVertexCount
//            vertexCount = vertexCount
//            indices = index
//            uniforms = Map.ofList uniforms
//            vertexAttributes = ig.IndexedAttributes |> SymDict.toMap |> Map.map (fun _ -> BufferView.ofArray)
//        }
//
//
//
//type IManagedBufferWriter =
//    inherit IAdaptiveObject
//    abstract member Write : IAdaptiveObject -> unit
//
//type IManagedBuffer =
//    inherit IDisposable
//    inherit IMod<IBuffer>
//    abstract member Clear : unit -> unit
//    abstract member Capacity : int
//    abstract member Set : Range1l * byte[] -> unit
//    abstract member Add : Range1l * BufferView -> IDisposable
//    abstract member Add : int * IMod -> IDisposable
//    abstract member ElementType : Type
//
//type IManagedBuffer<'a when 'a : unmanaged> =
//    inherit IManagedBuffer
//    abstract member Count : int
//    abstract member Item : int -> 'a with get, set
//    abstract member Set : Range1l * 'a[] -> unit
//
//[<AutoOpen>]
//module private ManagedBufferImplementation =
//
//    type ManagedBuffer<'a when 'a : unmanaged>(runtime : IRuntime) =
//        inherit DirtyTrackingAdaptiveObject<ManagedBufferWriter>()
//        static let asize = sizeof<'a> |> nativeint
//        let store = runtime.CreateMappedBuffer()
//
//        let bufferWriters = Dict<BufferView, ManagedBufferWriter<'a>>()
//        let uniformWriters = Dict<IMod, ManagedBufferSingleWriter<'a>>()
//
//        member x.Clear() =
//            store.Resize 0n
//
//        member x.Add(range : Range1l, view : BufferView) =
//            lock x (fun () ->
//                let count = range.Size + 1L
//
//                let writer = 
//                    bufferWriters.GetOrCreate(view, fun view ->
//                        let remove w =
//                            x.Dirty.Remove w |> ignore
//                            bufferWriters.Remove view |> ignore
//
//                        let data = BufferView.download 0 (int count) view
//                        let real : IMod<'a[]> = data |> PrimitiveValueConverter.convertArray view.ElementType
//                        let w = new ManagedBufferWriter<'a>(remove, real, store)
//                        x.Dirty.Add w |> ignore
//                        w
//                    )
//
//
//                if writer.AddRef range then
//                    let min = nativeint(range.Min + count) * asize
//                    if store.Capacity < min then
//                        store.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)
//
//                    lock writer (fun () -> 
//                        if not writer.OutOfDate then
//                            writer.Write(range)
//                    )
//
//                { new IDisposable with
//                    member x.Dispose() =
//                        writer.RemoveRef range |> ignore
//                }
//            )
//
//        member x.Add(index : int, data : IMod) =
//            lock x (fun () ->
//                let mutable isNew = false
//                let writer =
//                    uniformWriters.GetOrCreate(data, fun data ->
//                        isNew <- true
//                        let remove w =
//                            x.Dirty.Remove w |> ignore
//                            uniformWriters.Remove data |> ignore
//
//                        let real : IMod<'a> = data |> PrimitiveValueConverter.convertValue
//                        let w = new ManagedBufferSingleWriter<'a>(remove, real, store)
//                        x.Dirty.Add w |> ignore
//                        w
//                    )
// 
//                let range = Range1l(int64 index, int64 index)
//                if writer.AddRef range then
//                    let min = nativeint (index + 1) * asize
//                    if store.Capacity < min then
//                        store.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)
//                            
//                    lock writer (fun () -> 
//                        if not writer.OutOfDate then
//                            writer.Write(range)
//                    )
//
//
//                        
//                { new IDisposable with
//                    member x.Dispose() =
//                        writer.RemoveRef range |> ignore
//                }
//            )
//
//        member x.Set(range : Range1l, value : byte[]) =
//            let count = range.Size + 1L
//            let e = nativeint(range.Min + count) * asize
//            if store.Capacity < e then
//                store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)
//
//            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
//            try
//                let ptr = gc.AddrOfPinnedObject()
//                let lv = value.Length |> nativeint
//                let mutable remaining = nativeint count * asize
//                let mutable offset = nativeint range.Min * asize
//                while remaining >= lv do
//                    store.Write(ptr, offset, lv)
//                    offset <- offset + lv
//                    remaining <- remaining - lv
//
//                if remaining > 0n then
//                    store.Write(ptr, offset, remaining)
//
//            finally
//                gc.Free()
//
//        member x.Set(index : int, value : 'a) =
//            let e = nativeint (index + 1) * asize
//            if store.Capacity < e then
//                store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)
//
//            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
//            try store.Write(gc.AddrOfPinnedObject(), nativeint index * asize, asize)
//            finally gc.Free()
//
//        member x.Get(index : int) =
//            let mutable res = Unchecked.defaultof<'a>
//            store.Read(&&res |> NativePtr.toNativeInt, nativeint index * asize, asize)
//            res
//
//        member x.Set(range : Range1l, value : 'a[]) =
//            let e = nativeint(range.Max + 1L) * asize
//            if store.Capacity < e then
//                store.Resize(Fun.NextPowerOfTwo(int64 e) |> nativeint)
//
//            let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
//            try store.Write(gc.AddrOfPinnedObject(), nativeint range.Min * asize, nativeint(range.Size + 1L) * asize)
//            finally gc.Free()
//
//        member x.GetValue(caller : IAdaptiveObject) =
//            x.EvaluateAlways' caller (fun dirty ->
//                for d in dirty do
//                    d.Write(x)
//                store.GetValue(x)
//            )
//
//        member x.Capacity = store.Capacity
//        member x.Count = store.Capacity / asize |> int
//
//        member x.Dispose() =
//            store.Dispose()
//
//        interface IDisposable with
//            member x.Dispose() = x.Dispose()
//
//        interface IMod with
//            member x.IsConstant = false
//            member x.GetValue c = x.GetValue c :> obj
//
//        interface IMod<IBuffer> with
//            member x.GetValue c = x.GetValue c
//
//        interface ILockedResource with
//            member x.Use f = store.Use f
//            member x.AddLock r = store.AddLock r
//            member x.RemoveLock r = store.RemoveLock r
//
//        interface IManagedBuffer with
//            member x.Clear() = x.Clear()
//            member x.Add(range : Range1l, view : BufferView) = x.Add(range, view)
//            member x.Add(index : int, data : IMod) = x.Add(index, data)
//            member x.Set(range : Range1l, value : byte[]) = x.Set(range, value)
//            member x.Capacity = x.Capacity |> int
//            member x.ElementType = typeof<'a>
//
//        interface IManagedBuffer<'a> with
//            member x.Count = x.Count
//            member x.Item
//                with get i = x.Get i
//                and set i v = x.Set(i,v)
//            member x.Set(range : Range1l, value : 'a[]) = x.Set(range, value)
//
//    and [<AbstractClass>] ManagedBufferWriter(remove : ManagedBufferWriter -> unit) =
//        inherit AdaptiveObject()
//        let mutable refCount = 0
//        let targetRegions = ReferenceCountingSet<Range1l>()
//
//        abstract member Write : Range1l -> unit
//        abstract member Release : unit -> unit
//
//        member x.AddRef(range : Range1l) : bool =
//            lock x (fun () ->
//                targetRegions.Add range
//            )
//
//        member x.RemoveRef(range : Range1l) : bool = 
//            lock x (fun () ->
//                targetRegions.Remove range |> ignore
//                if targetRegions.Count = 0 then
//                    x.Release()
//                    remove x
//                    let mutable foo = 0
//                    x.Outputs.Consume(&foo) |> ignore
//                    true
//                else
//                    false
//            )
//
//        member x.Write(caller : IAdaptiveObject) =
//            x.EvaluateIfNeeded caller () (fun () ->
//                for r in targetRegions do
//                    x.Write(r)
//            )
//
//        interface IManagedBufferWriter with
//            member x.Write c = x.Write c
//
//    and ManagedBufferWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : IMod<'a[]>, store : IMappedBuffer) =
//        inherit ManagedBufferWriter(remove)
//        static let asize = sizeof<'a> |> nativeint
//
//        override x.Release() = ()
//
//        override x.Write(target) =
//            let v = data.GetValue(x)
//            let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
//            try 
//                store.Write(gc.AddrOfPinnedObject(), nativeint target.Min * asize, nativeint v.Length * asize)
//            finally 
//                gc.Free()
//
//    and ManagedBufferSingleWriter<'a when 'a : unmanaged>(remove : ManagedBufferWriter -> unit, data : IMod<'a>, store : IMappedBuffer) =
//        inherit ManagedBufferWriter(remove)
//        static let asize = sizeof<'a> |> nativeint
//            
//        override x.Release() = ()
//
//        override x.Write(target) =
//            let v = data.GetValue(x)
//            let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
//            try store.Write(gc.AddrOfPinnedObject(), nativeint target.Min * asize, asize)
//            finally gc.Free()
//
//module ManagedBuffer =
//
//    let private ctorCache = Dict<Type, ConstructorInfo>()
//
//    let private ctor (t : Type) =
//        lock ctorCache (fun () ->
//            ctorCache.GetOrCreate(t, fun t ->
//                let tb = typedefof<ManagedBuffer<int>>.MakeGenericType [|t|]
//                tb.GetConstructor(
//                    BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.CreateInstance,
//                    Type.DefaultBinder,
//                    [| typeof<IRuntime> |],
//                    null
//                )
//            )
//        )
//
//    let create (t : Type) (runtime : IRuntime) =
//        let ctor = ctor t
//        ctor.Invoke [| runtime |] |> unbox<IManagedBuffer>
//
//
//type private LayoutManager<'a>() =
//    let manager = MemoryManager.createNop()
//    let store = Dict<'a, managedptr>()
//    let cnts = Dict<managedptr, 'a * ref<int>>()
//
//
//    member x.Alloc(key : 'a, size : int) =
//        match store.TryGetValue key with
//            | (true, v) -> 
//                let _,r = cnts.[v]
//                Interlocked.Increment &r.contents |> ignore
//                v
//            | _ ->
//                let v = manager.Alloc size
//                let r = ref 1
//                cnts.[v] <- (key,r)
//                store.[key] <- (v)
//                v
//
//
//    member x.TryAlloc(key : 'a, size : int) =
//        match store.TryGetValue key with
//            | (true, v) -> 
//                let _,r = cnts.[v]
//                Interlocked.Increment &r.contents |> ignore
//                false, v
//            | _ ->
//                let v = manager.Alloc size
//                let r = ref 1
//                cnts.[v] <- (key,r)
//                store.[key] <- (v)
//                true, v
//
//    member x.Free(value : managedptr) =
//        match cnts.TryGetValue value with
//            | (true, (k,r)) ->
//                if Interlocked.Decrement &r.contents = 0 then
//                    manager.Free value
//                    cnts.Remove value |> ignore
//                    store.Remove k |> ignore
//            | _ ->
//                ()
//
//
//type ManagedDrawCall(call : DrawCallInfo, release : IDisposable) =
//    member x.Call = call
//        
//    member x.Dispose() = release.Dispose()
//    interface IDisposable with
//        member x.Dispose() = release.Dispose()
//
//type ManagedPool(runtime : IRuntime, signature : GeometrySignature) =
//    static let zero : byte[] = Array.zeroCreate 128
//    let mutable count = 0
//    let indexManager = LayoutManager<Option<BufferView> * int>()
//    let vertexManager = LayoutManager<Map<Symbol, BufferView>>()
//    let instanceManager = LayoutManager<Map<Symbol, IMod>>()
//
//    let indexBuffer = new ManagedBuffer<int>(runtime) :> IManagedBuffer<int>
//    let vertexBuffers = signature.vertexBufferTypes |> Map.toSeq |> Seq.map (fun (k,t) -> k, ManagedBuffer.create t runtime) |> SymDict.ofSeq
//    let instanceBuffers = signature.uniformTypes |> Map.toSeq |> Seq.map (fun (k,t) -> k, ManagedBuffer.create t runtime) |> SymDict.ofSeq
//    let vertexDisposables = Dictionary<BufferView, IDisposable>()
//
//
//    let vertexBufferTypes = Map.toArray signature.vertexBufferTypes
//    let uniformTypes = Map.toArray signature.uniformTypes
//
//    member x.Runtime = runtime
//
//    member x.Add(g : AdaptiveGeometry) =
//        lock x (fun () ->
//            let ds = List()
//            let fvc = g.faceVertexCount
//            let vertexCount = g.vertexCount
//            
//            
//            let vertexPtr = vertexManager.Alloc(g.vertexAttributes, vertexCount)
//            let vertexRange = Range1l(int64 vertexPtr.Offset, int64 vertexPtr.Offset + int64 vertexCount - 1L)
//            for (k,t) in vertexBufferTypes do
//                let target = vertexBuffers.[k]
//                match Map.tryFind k g.vertexAttributes with
//                    | Some v -> target.Add(vertexRange, v) |> ds.Add
//                    | None -> target.Set(vertexRange, zero)
//            
//
//
//            let instancePtr = instanceManager.Alloc(g.uniforms, 1)
//            let instanceIndex = int instancePtr.Offset
//            for (k,t) in uniformTypes do
//                let target = instanceBuffers.[k]
//                match Map.tryFind k g.uniforms with
//                    | Some v -> target.Add(instanceIndex, v) |> ds.Add
//                    | None -> target.Set(Range1l(int64 instanceIndex, int64 instanceIndex), zero)
//
//            let isNew, indexPtr = indexManager.TryAlloc((g.indices, fvc), fvc)
//            let indexRange = Range1l(int64 indexPtr.Offset, int64 indexPtr.Offset + int64 fvc - 1L)
//            match g.indices with
//                | Some v -> indexBuffer.Add(indexRange, v) |> ds.Add
//                | None -> if isNew then indexBuffer.Set(indexRange, Array.init fvc id)
//
//            count <- count + 1
//
//            let disposable =
//                { new IDisposable with
//                    member __.Dispose() = 
//                        lock x (fun () ->
//                            count <- count - 1
//                            if count = 0 then 
//                                for b in vertexBuffers.Values do b.Clear()
//                                for b in instanceBuffers.Values do b.Clear()
//                                indexBuffer.Clear() 
//                            for d in ds do d.Dispose()
//                            vertexManager.Free vertexPtr
//                            instanceManager.Free instancePtr
//                            indexManager.Free indexPtr
//                        )
//                }
//
//            let call =
//                DrawCallInfo(
//                    FaceVertexCount = fvc,
//                    FirstIndex = int indexPtr.Offset,
//                    FirstInstance = int instancePtr.Offset,
//                    InstanceCount = 1,
//                    BaseVertex = int vertexPtr.Offset
//                )
//
//            
//            new ManagedDrawCall(call, disposable)
//        )
//
//    member x.VertexAttributes =
//        { new IAttributeProvider with
//            member x.Dispose() = ()
//            member x.All = Seq.empty
//            member x.TryGetAttribute(sem : Symbol) =
//                match vertexBuffers.TryGetValue sem with
//                    | (true, v) -> Some (BufferView(v, v.ElementType))
//                    | _ -> None
//        }
//
//    member x.InstanceAttributes =
//        { new IAttributeProvider with
//            member x.Dispose() = ()
//            member x.All = Seq.empty
//            member x.TryGetAttribute(sem : Symbol) =
//                match instanceBuffers.TryGetValue sem with
//                    | (true, v) -> Some (BufferView(v, v.ElementType))
//                    | _ -> None
//        }
//
//    member x.IndexBuffer =
//        BufferView(indexBuffer, indexBuffer.ElementType)
//
//type DrawCallBuffer(runtime : IRuntime, indexed : bool) =
//    inherit Mod.AbstractMod<IIndirectBuffer>()
//
//    let indices = Dict<DrawCallInfo, int>()
//    let calls = List<DrawCallInfo>()
//    let store = runtime.CreateMappedIndirectBuffer(indexed)
//
//    let locked x (f : unit -> 'a) =
//        lock x (fun () ->
//            store.Use f
//        )
//
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
//
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
//                        
//                    store.Count <- calls.Count
//                    true
//                | _ ->
//                    false
//        )
//
//    member x.Add (call : ManagedDrawCall) =
//        if add x call.Call then
//            transact (fun () -> x.MarkOutdated())
//            true
//        else
//            false
//
//    member x.Remove(call : ManagedDrawCall) =
//        if remove x call.Call then
//            transact (fun () -> x.MarkOutdated())
//            true
//        else
//            false
//
//    interface ILockedResource with
//        member x.Use f = store.Use f
//        member x.AddLock l = store.AddLock l
//        member x.RemoveLock l = store.RemoveLock l
//
//    override x.Compute() =
//        store.GetValue()
//
//[<AbstractClass; Sealed; Extension>]
//type IRuntimePoolExtensions private() =
//
//    [<Extension>]
//    static member CreateManagedPool(this : IRuntime, signature : GeometrySignature) =
//        new ManagedPool(this, signature)
//
//    [<Extension>]
//    static member CreateManagedBuffer<'a when 'a : unmanaged>(this : IRuntime) : IManagedBuffer<'a> =
//        new ManagedBuffer<'a>(this) :> IManagedBuffer<'a>
//
//    [<Extension>]
//    static member CreateManagedBuffer(this : IRuntime, elementType : Type) : IManagedBuffer =
//        this |> ManagedBuffer.create elementType
//
//    [<Extension>]
//    static member CreateDrawCallBuffer(this : IRuntime, indexed : bool) =
//        new DrawCallBuffer(this, indexed)
//
//
//module Sg =
//    type PoolNode(pool : ManagedPool, calls : aset<ManagedDrawCall>) =
//        interface ISg
//        member x.Pool = pool
//        member x.Calls = calls
//
//    let pool (pool : ManagedPool) (calls : aset<ManagedDrawCall>) =
//        PoolNode(pool, calls) :> ISg
//
//
//module ``Pool Semantics`` =
//    [<Aardvark.Base.Ag.Semantic>]
//    type PoolSem() =
//        member x.RenderObjects(p : Sg.PoolNode) =
//            aset {
//                let pool = p.Pool
//                let ro = Aardvark.SceneGraph.Semantics.RenderObject.create()
//
//
//                let r = p.Calls.GetReader()
//                let calls =
//                    let buffer = DrawCallBuffer(pool.Runtime, true)
//                    Mod.custom (fun self ->
//                        let deltas = r.GetDelta self
//                        for d in deltas do
//                            match d with
//                                | Add v -> buffer.Add v |> ignore
//                                | Rem v -> buffer.Remove v |> ignore
//
//                        buffer.GetValue()
//                    )
//
//                ro.Mode <- Mod.constant IndexedGeometryMode.TriangleList
//                ro.Indices <- Some pool.IndexBuffer
//                ro.VertexAttributes <- pool.VertexAttributes
//                ro.InstanceAttributes <- pool.InstanceAttributes
//                ro.IndirectBuffer <- calls // |> ASet.toMod |> Mod.map (fun calls -> calls |> Seq.toArray |> ArrayBuffer :> IBuffer)
//                //ro.DrawCallInfos <- p.Calls |> ASet.toMod |> Mod.map Seq.toList
//                yield ro :> IRenderObject
//                    
//            }

module ``Pool Tests`` =
    open FShade
    module Sem =
        let Hugo = Symbol.Create "Hugo"
        let HugoN = Symbol.Create "HugoN"

    type HugoVertex = 
        {
            [<Semantic("Hugo")>] mt : M44d
            [<Semantic("HugoN")>] nt : M33d
            [<Position>] p : V4d
            [<Normal>] n : V3d
        }

    let hugoShade (v : HugoVertex) =
        vertex {
            return { v 
                with 
                    p = v.mt * v.p 
                    n = v.nt * v.n
            }
        }

    [<Demo("Pooling Test")>]
    let sg ()=
        let r = App.Runtime
        let pool =
            r.CreateManagedPool {
                indexType = typeof<int>
                vertexBufferTypes = 
                    Map.ofList [ 
                        DefaultSemantic.Positions, typeof<V3f>
                        DefaultSemantic.Normals, typeof<V3f> 
                    ]
                uniformTypes = 
                    Map.ofList [ 
                        Sem.Hugo, typeof<M44f> 
                    ]
            }

        let rnd = Random()

        let geometry (pos : V3d) =  
            let trafo = Trafo3d.Scale 0.1 * Trafo3d.Translation pos

            Primitives.unitSphere 3
            //Primitives.unitSphere (rnd.Next(1, 7))
            //Primitives.unitCone (rnd.Next(10, 2000))
                |> AdaptiveGeometry.ofIndexedGeometry [Sem.Hugo, Mod.constant trafo :> IMod ]
                |> pool.Add


        let s = 5.0 

        let all =
            [    
                for x in -s / 2.0 .. s / 2.0 do
                    for y in -s / 2.0 .. s / 2.0 do
                        for z in -s / 2.0 .. s / 2.0 do
                            yield geometry(V3d(x,y,z))
            ]

        Log.line "count: %A" (List.length all)

        let geometries =
            CSet.ofList all

        let initial = geometries.Count
        let random = Random()
        App.Keyboard.DownWithRepeats.Values.Add(fun k ->
            if k = Keys.X then
                if geometries.Count > 0 then
                    let remove = geometries.RandomOrder() |> Seq.atMost 1024 |> Seq.toList
                    transact (fun () ->
                        geometries.ExceptWith remove
                    )

            if k = Keys.T then
                transact (fun () ->
                    geometries.Clear()
                )

            if k = Keys.R then
                transact (fun () ->
                    geometries.Clear()
                    geometries.UnionWith all
                )

            if k = Keys.Z then
                transact (fun () ->
                    
                    let rnd = Random()
                    Report.Line("adding new random stuff: Seed={0}", rnd.Next())
             
                    for i in 0 .. 100 do
                        let rx = rnd.NextDouble() * 10.0 - 5.0
                        let ry = rnd.NextDouble() * 10.0 - 5.0
                        let rz = rnd.NextDouble() * 10.0 - 5.0
                        let newStuff = geometry(V3d(rx, ry, rz))
                        geometries.Add newStuff |> ignore    
                )

                Report.Line("new geometry count: {0}", geometries.Count)
                
        )

        let mode = Mod.init FillMode.Fill
        App.Keyboard.KeyDown(Keys.K).Values.Add (fun () ->
            transact (fun () ->
                mode.Value <- 
                    match mode.Value with
                        | FillMode.Fill -> FillMode.Line
                        | _ -> FillMode.Fill
            )
        )

        let sg = 
            Sg.PoolNode(pool, geometries, Mod.constant IndexedGeometryMode.TriangleList)
                |> Sg.fillMode mode
                |> Sg.uniform "LightLocation" (Mod.constant (10.0 * V3d.III))
                |> Sg.effect [
                    hugoShade |> toEffect
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect
                ]
        sg

    [<Demo("Pooling Test (Lazy)")>]
    let sg2 ()=
        let r = App.Runtime
        let pool =
            r.CreateManagedPool {
                indexType = typeof<int>
                vertexBufferTypes = 
                    Map.ofList [ 
                        DefaultSemantic.Positions, typeof<V3f>
                        DefaultSemantic.Normals, typeof<V3f> 
                        DefaultSemantic.DiffuseColorCoordinates, typeof<V3f> 
                    ]
                uniformTypes = 
                    Map.ofList [ 
                        Sem.Hugo, typeof<M44f> 
                        Sem.HugoN, typeof<M33f> 
                    ]
            }

        let rnd = Random()

        let geometries = CSet.empty
    
        let addRandom() = 
            let pos = V3d( 
                        rnd.NextDouble() * 10.0 - 5.0, 
                        rnd.NextDouble() * 10.0 - 5.0,
                        rnd.NextDouble() * 10.0 - 5.0 )
            let trafo = Trafo3d.Scale 0.1 * Trafo3d.Translation pos
            let ig = Primitives.unitCone (rnd.Next(1000, 50000))
            geometries.Add (ig, trafo) |> ignore

        for i in 0 .. 10 do
            addRandom()

        let initial = geometries.Count

        App.Keyboard.DownWithRepeats.Values.Add(fun k ->
                    
            if k = Keys.R then
                transact (fun () ->
                    geometries.Clear()
                )

            if k = Keys.Z then
                transact (fun () ->
                    
                    Report.Line("adding new random stuff: Seed={0}", rnd.Next())
             
                    for i in 0 .. 10 do
                        addRandom()
                )

                Report.Line("new geometry count: {0}", geometries.Count)
                
        )

        let addToPool(ag : AdaptiveGeometry) = 
            
            Report.BeginTimed("add to pool: vc={0}", ag.vertexCount)
            Report.Line("ManagedBuffer.Set takes super long to fill up missing DiffuseColorCoordinates with 0")
            let mdc = pool.Add ag
            Report.End() |> ignore
            mdc

        let geometriesLazy = 
            geometries |> ASet.map (fun (ig, trafo) -> ig
                                                        |> AdaptiveGeometry.ofIndexedGeometry [ (Sem.Hugo, (Mod.constant trafo :> IMod)); (Sem.HugoN, Mod.constant (trafo.Backward.Transposed) :> IMod) ]
                                                        |> addToPool)

        // initial evaluation
        ASet.toArray geometriesLazy |> ignore

        let sg = 
            Sg.PoolNode(pool, geometriesLazy, Mod.constant IndexedGeometryMode.TriangleList)
                |> Sg.uniform "LightLocation" (Mod.constant (10.0 * V3d.III))
                |> Sg.effect [
                    hugoShade |> toEffect
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect
                ]
        sg



module Pooling =

    module LodAgain =
        open Aardvark.SceneGraph.Semantics
        open System.Threading.Tasks

        type ILodData =
            abstract member BoundingBox : Box3d
            abstract member Traverse : (LodDataNode -> (LodDataNode -> list<'a>) -> 'a) -> 'a
            abstract member GetData : node : LodDataNode -> Async<Option<IndexedGeometry>>
                  

        type LodNode(signature : GeometrySignature, data : ILodData, mode : IMod<IndexedGeometryMode>) =
            interface ISg
            member x.Signature = signature
            member x.Data = data
            member x.Mode = mode

        let disposeOnCancel<'a when 'a :> IDisposable> (f : unit -> 'a) : Async<'a> =
            async {
                let mutable call = None
                let! _ = Async.OnCancel(fun () -> call |> Option.iter Disposable.dispose)
                let r = f()
                call <- Some (r :> IDisposable)
                return r
            }

        type AsyncMod<'a>(inner : IMod<'a>) =
            inherit Mod.AbstractMod<'a>()

            let sem = new SemaphoreSlim(1)

            override x.Mark() =
                sem.Release() |> ignore
                true

            override x.Compute(token) =
                inner.GetValue(token)

            member x.GetValueAsync() =
                async {
                    let! _ = Async.AwaitIAsyncResult (sem.WaitAsync())
                    return x.GetValue()
                }

        type RoseTree<'a> =
            | Empty
            | Leaf of 'a
            | Node of 'a * list<RoseTree<'a>>

        type Loady<'a, 'b>(tag : 'b, trigger : IAdaptiveObject, release : 'a -> unit, run : Async<'a>) =
            let mutable task : Option<Task<'a>> = None
            let mutable cancel = new CancellationTokenSource()

            let run =
                async {
                    let! r = run
                    transact (fun () -> trigger.MarkOutdated())
                    return r
                }

            member x.Tag = tag

            member x.Peek =
                match task with
                    | Some t when t.IsCompleted -> Some t.Result
                    | _ -> None

            member x.Start() =
                //Log.line "starting %A" tag
                match task with
                    | None ->
                        let t = Async.StartAsTask(run, cancellationToken = cancel.Token)
                        task <- Some t
                    | Some _ -> 
                        ()

            member x.Stop() =
                //Log.line "stopping %A" tag
                match task with
                    | Some t ->
                        if t.IsCompleted then
                            release t.Result
                        elif t.IsCanceled || t.IsFaulted then
                            ()
                        else
                            cancel.Cancel()
                            t.ContinueWith (fun (t : Task<'a>) ->
                                if t.IsCanceled || t.IsFaulted then
                                    ()
                                elif t.IsCompleted then
                                    release t.Result
                            ) |> ignore

                        cancel.Dispose()
                        task <- None
                    | _ ->
                        ()
                 
        module Loady =
            let start (tag : 'b) (trigger : IAdaptiveObject) (run : Async<Option<'a>>) =
                let l = Loady(tag, trigger, Option.iter (fun a -> (a :> IDisposable).Dispose()), run)
                l.Start()
                l

        module RoseTree =
            let rec traverse<'a, 'b> (equal : 'b -> 'a -> bool) (create : 'a -> list<RoseTree<'a>> -> 'b) (destroy : 'b -> unit) (ref : RoseTree<'b>) (t : RoseTree<'a>) : RoseTree<'b> =
                let traverse = traverse equal create destroy

                match ref, t with
                    | Empty, Empty -> 
                        Empty

                    | Empty, Leaf v ->
                        create v [] |> Leaf

                    | Empty, Node(v, children) ->
                        let n = create v children
                        Node(n, children |> List.map (traverse Empty))

                    | Leaf v, Empty ->
                        destroy v
                        Empty

                    | Leaf l, Leaf r ->
                        if equal l r then 
                            Leaf l
                        else 
                            destroy l
                            create r [] |> Leaf

                    | Leaf l, Node(r, children) ->
                        if equal l r then
                            Node(l, children |> List.map (traverse Empty))
                        else
                            destroy l
                            let n = create r children
                            Node(n, children |> List.map (traverse Empty))

                    | Node(v,c), Empty ->
                        destroy v
                        c |> List.iter (fun c -> traverse c Empty |> ignore)
                        Empty

                    | Node(v,c), Leaf r ->
                        c |> List.iter (fun c -> traverse c Empty |> ignore)
                        if equal v r then
                            Leaf v
                        else
                            destroy v
                            create r [] |> Leaf
                                            
                    | Node(lv,lc), Node(rv,rc) ->
                        if equal lv rv then
                            Node(lv, List.map2 traverse lc rc)
                        else
                            destroy lv
                            let nv = create rv rc
                            Node(nv, List.map2 traverse lc rc)
    
    


        [<Aardvark.Base.Ag.Semantic>]
        type LodSem() =
            static let nop = { new IDisposable with member x.Dispose() = () }


            member x.RenderObjects(n : LodNode) =
                let runtime = n.Runtime
                let data = n.Data

                let good (view : Trafo3d) (proj : Trafo3d) (n : LodDataNode) =
                    if n.level < 2 then false
                    else true

                

                // create a pool and a DrawCallBuffer
                let pool        = runtime.CreateManagedPool n.Signature
                let callBuffer  = runtime.CreateDrawCallBuffer true

                let view = n.ViewTrafo
                let proj = n.ProjTrafo

                let load (n : LodDataNode) =
                    async {
                        let! geometry = data.GetData n
                        
                        match geometry with
                            | Some g ->
                                let! call = disposeOnCancel (fun () -> pool.Add(AdaptiveGeometry.ofIndexedGeometry [] g))
                                return Some call
                            | None ->
                                return None
                    }

                let trigger = Mod.custom ignore
                
  
                let mutable currentLoady = Empty

                let loadyTree =
                    Mod.custom (fun self ->
                        let view = view.GetValue self
                        let proj = proj.GetValue self


                        let frustum = proj |> Frustum.ofTrafo
                        let hull = view * proj |> ViewProjection.toFastHull3d

                        

                        let tree = 
                            data.Traverse(fun n children ->
                                if hull.Intersects(n.bounds) then

                                    let bounds = n.bounds

                                    let depthRange =
                                        bounds.ComputeCorners()
                                            |> Array.map view.Forward.TransformPos
                                            |> Array.map (fun v -> -v.Z)
                                            |> Range1d

                                    let depthRange = Range1d(clamp frustum.near frustum.far depthRange.Min, clamp frustum.near frustum.far depthRange.Max)

                                    let projAvgDistance =
                                        abs (n.granularity / depthRange.Min)


                                    if projAvgDistance < 0.3 then
                                        Leaf n
                                    else
                                        match children n with
                                            | [] -> Leaf n
                                            | cs -> Node(n, cs)
                                else
                                    Empty
                            )

                        let newTree = 
                            RoseTree.traverse<_, Loady<_,_>> 
                                (fun a b -> a.Tag = b) 
                                (fun a _ -> a |> load |> Loady.start a trigger) 
                                (fun l -> l.Stop()) 
                                currentLoady tree
                  
                        currentLoady <- newTree
                        newTree
                    )

                let callBuffer =
                    let mutable oldLoady = Empty
                    Mod.custom (fun self ->
                        trigger.GetValue(self)
                        let newLoady = loadyTree.GetValue self

                        let isReady (l : RoseTree<Loady<_,_>>) =
                            match l with
                                | Empty -> true
                                | Leaf v -> v.Peek |> Option.isSome
                                | Node(v, _) -> v.Peek |> Option.isSome

                        let create (l : Loady<_,_>) (children : list<_>) =
                            let r = l.Peek
                            match r with
                                | Some (Some v) -> 
                                    match children with
                                        | [] -> 
                                            callBuffer.Add v |> ignore

                                        | _ ->
                                            if List.forall isReady children then
                                                callBuffer.Remove v |> ignore
                                            else
                                                callBuffer.Add v |> ignore
                                | _ -> ()
                            r

                        let destroy (l : Option<_>) =
                            match l with
                                | Some (Some v) ->
                                    callBuffer.Remove v |> ignore
                                | _ ->
                                    ()

                        oldLoady <- RoseTree.traverse<Loady<_,_>, Option<_>> (fun b a -> a.Peek = b) create destroy oldLoady newLoady

                        callBuffer.GetValue()
                    )
                
                let ro = RenderObject.create()

                ro.Mode <- n.Mode
                ro.Indices <- Some pool.IndexBuffer
                ro.VertexAttributes <- pool.VertexAttributes
                ro.InstanceAttributes <- pool.InstanceAttributes
                ro.IndirectBuffer <- callBuffer


                ASet.single (ro :> IRenderObject)


        let rand = Random()
        let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

        type DummyDataProvider(root : Box3d) =
            static let toNode (level : int) (b : Box3d) =
                { id = b; level = level; bounds = b; inner = true; granularity = Fun.Cbrt(b.Volume / 100.0); render = true}

            let children (b : Box3d) =
                let l = b.Min
                let u = b.Max
                let c = b.Center
                [
                    Box3d(V3d(l.X, l.Y, l.Z), V3d(c.X, c.Y, c.Z))
                    Box3d(V3d(c.X, l.Y, l.Z), V3d(u.X, c.Y, c.Z))
                    Box3d(V3d(l.X, c.Y, l.Z), V3d(c.X, u.Y, c.Z))
                    Box3d(V3d(c.X, c.Y, l.Z), V3d(u.X, u.Y, c.Z))
                    Box3d(V3d(l.X, l.Y, c.Z), V3d(c.X, c.Y, u.Z))
                    Box3d(V3d(c.X, l.Y, c.Z), V3d(u.X, c.Y, u.Z))
                    Box3d(V3d(l.X, c.Y, c.Z), V3d(c.X, u.Y, u.Z))
                    Box3d(V3d(c.X, c.Y, c.Z), V3d(u.X, u.Y, u.Z))
                ]

            interface ILodData with
                member x.BoundingBox = root

                member x.Traverse f =
                    let rec traverse (level : int) (b : Box3d) =
                        let box = b
                        let n = 100.0
                        let node = { id = b; level = level; bounds = box; inner = true; granularity = Fun.Cbrt(box.Volume / n); render = true}

                        if level > 10 then
                            f node (fun _ -> [])
                        else
                            f node (fun n -> n.id |> unbox |> children |> List.map (traverse (level + 1)))

                    traverse 0 root

                member x.GetData (cell : LodDataNode) =
                    async {
                        //do! Async.SwitchToThreadPool()
                        let box = cell.bounds
                        let points = 
                            [| for x in 0 .. 9 do
                                 for y in 0 .. 9 do
                                    for z in 0 .. 9 do
                                        yield V3d(x,y,z)*0.1*box.Size + box.Min |> V3f.op_Explicit
                             |]
                        let colors = Array.create points.Length (randomColor())
                        let mutable a = 0
                        return Some <| IndexedGeometry(Mode = unbox a, IndexedAttributes = SymDict.ofList [ DefaultSemantic.Positions, points :> Array; DefaultSemantic.Colors, colors :> System.Array])
                    }

        [<Demo("LOD again")>]
        let test() =
            let data = DummyDataProvider(Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 20.0))

            let signature =
                {
                    indexType           = typeof<int>
                    vertexBufferTypes   = 
                        Map.ofList [
                            DefaultSemantic.Positions, typeof<V3f>
                            DefaultSemantic.Colors, typeof<C4b>
                        ]
                    uniformTypes        = Map.empty
                }

            let node = LodNode(signature, data, Mod.constant IndexedGeometryMode.PointList)

            node :> ISg
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.vertexColor |> toEffect
                ]


    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Optimizer =
        open Aardvark.Base.Monads.Option

        type RenderObjectSignature =
            {
                IsActive            : IMod<bool>
                RenderPass          : RenderPass
                Mode                : IndexedGeometryMode
                Surface             : IResource<IBackendSurface>
                DepthTest           : IMod<DepthTestMode>
                CullMode            : IMod<CullMode>
                BlendMode           : IMod<BlendMode>
                FillMode            : IMod<FillMode>
                StencilMode         : IMod<StencilMode>
                WriteBuffers        : Option<Set<Symbol>>
                Uniforms            : Map<Symbol, IMod>
                Geometry            : GeometrySignature
            }

        let sw = System.Diagnostics.Stopwatch()

        let modType =
            let cache = 
                Cache<Type, Type>(fun t ->
                    match t with
                        | ModOf t -> t
                        | _ -> failwith ""
                )

            cache.Invoke

        let private tryDecomposeRO (runtime : IRuntime) (signature : IFramebufferSignature) (ro : RenderObject) =
            option {
                let! surface =
                    if ro.Surface.IsConstant then
                        let s = ro.Surface |> Mod.force
                        runtime.ResourceManager.CreateSurface(signature, Mod.constant s) |> Some
                    else
                        None
                sw.Start()

                let! mode =
                    if ro.Mode.IsConstant then Some (Mod.force ro.Mode)
                    else None


                let! drawCall =
                    if ro.DrawCallInfos.IsConstant then
                        match ro.DrawCallInfos |> Mod.force with
                            | [call] when call.InstanceCount = 1 && call.BaseVertex = 0 && call.FirstInstance = 0 && call.FirstIndex = 0 -> 
                                Some call
                            | _ -> None
                    else
                        None
          
                let surf = surface.Handle |> Mod.force

                let mutable attributes = []
                let mutable uniforms = []

                
                for (n,_) in surf.Inputs do
                    let n = Symbol.Create n
                    match ro.VertexAttributes.TryGetAttribute n with
                        | Some v ->
                            attributes <- (n, v, v.ElementType) :: attributes
                        | None ->
                            match ro.Uniforms.TryGetUniform(ro.AttributeScope, n) with
                                | Some v ->
                                    uniforms <- (n, v, v.GetType() |> modType) :: uniforms
                                | _ ->
                                    ()

                let realUniforms =
                    surf.Uniforms
                        |> List.map (fun (n,_) -> Symbol.Create n)
                        |> List.choose (fun n -> match ro.Uniforms.TryGetUniform(ro.AttributeScope, n) with | Some v -> Some (n, v) | _ -> None)
                        |> Map.ofList

                

                let signature = 
                    {
                        indexType = match ro.Indices with | Some i -> i.ElementType | _ -> typeof<int>
                        vertexBufferTypes = attributes |> List.map (fun (n,_,t) -> (n,t)) |> Map.ofList
                        uniformTypes = uniforms |> List.map (fun (n,_,t) -> (n,t)) |> Map.ofList
                    }

                let! vertexCount =
                    match ro.Indices with
                        | None -> Some drawCall.FaceVertexCount
                        | Some i ->
                            let (_,v,_) = attributes |> List.head 
                            let b = Mod.force v.Buffer
                            match b with
                                | :? ArrayBuffer as b -> Some b.Data.Length
                                | :? INativeBuffer as b -> Some (b.SizeInBytes / (Marshal.SizeOf v.ElementType))
                                | _ -> None

                let geometry =
                    {
                        faceVertexCount  = drawCall.FaceVertexCount
                        vertexCount      = vertexCount
                        indices          = ro.Indices
                        uniforms         = uniforms |> List.map (fun (n,v,_) -> (n,v)) |> Map.ofList
                        vertexAttributes = attributes |> List.map (fun (n,v,_) -> (n,v)) |> Map.ofList
                    }

                let roSignature =
                    {
                        IsActive            = ro.IsActive
                        RenderPass          = ro.RenderPass
                        Mode                = mode
                        Surface             = surface
                        DepthTest           = ro.DepthTest
                        CullMode            = ro.CullMode
                        BlendMode           = ro.BlendMode
                        FillMode            = ro.FillMode
                        StencilMode         = ro.StencilMode
                        WriteBuffers        = ro.WriteBuffers
                        Uniforms            = realUniforms
                        Geometry            = signature
                    }

                sw.Stop()
                return roSignature, geometry

            }

        let tryDecompose (runtime : IRuntime) (signature : IFramebufferSignature) (ro : IRenderObject) =
            match ro with
                | :? RenderObject as ro -> tryDecomposeRO runtime signature ro
                | _ -> None

        let optimize (runtime : IRuntime) (signature : IFramebufferSignature) (objects : aset<IRenderObject>) : aset<IRenderObject> =
            
            let pools = Dict<GeometrySignature, ManagedPool>()
            let calls = Dict<RenderObjectSignature, DrawCallBuffer * IRenderObject>()
            let disposables = Dict<IRenderObject, IDisposable>()

            let reader = objects.GetReader()
            ASet.custom (fun caller state ->
                
                let output = List<_>()
                let total = System.Diagnostics.Stopwatch()
                total.Start()
                let deltas = reader.GetOperations caller
                total.Stop()
                printfn "pull: %A" total.MicroTime

                total.Restart()
                sw.Reset()
                for d in deltas do
                    match d with
                        | Add(_,ro) ->
                            let res = tryDecompose runtime signature ro
                            match res with
                                | Some (signature, o) ->

                                    let pool = pools.GetOrCreate(signature.Geometry, fun v -> runtime.CreateManagedPool v)

                                    let call = pool.Add o

                                    let callBuffer =
                                        match calls.TryGetValue signature with
                                            | (true, (calls, _)) -> 
                                                calls
                                            | _ ->
                                                let buffer = DrawCallBuffer(runtime, true)
                                                let ro =
                                                    {
                                                        RenderObject.Create() with
                                                            AttributeScope      = Ag.emptyScope
                
                                                            IsActive            = signature.IsActive
                                                            RenderPass          = signature.RenderPass
                                                            DrawCallInfos       = null
                                                            IndirectBuffer      = buffer
                                                            Mode                = Mod.constant signature.Mode
                                                            Surface             = Mod.constant (signature.Surface.Handle |> Mod.force :> ISurface)
                                                            DepthTest           = signature.DepthTest
                                                            CullMode            = signature.CullMode
                                                            BlendMode           = signature.BlendMode
                                                            FillMode            = signature.FillMode
                                                            StencilMode         = signature.StencilMode
                
                                                            Indices             = Some pool.IndexBuffer
                                                            InstanceAttributes  = pool.InstanceAttributes
                                                            VertexAttributes    = pool.VertexAttributes
                
                                                            Uniforms            = UniformProvider.ofMap signature.Uniforms

                                                            Activate            = fun () -> { new IDisposable with member x.Dispose() = () }
                                                            WriteBuffers        = signature.WriteBuffers
                                                    }

                                                calls.[signature] <- (buffer, ro :> IRenderObject)

                                                output.Add(Add (ro :> IRenderObject))

                                                buffer

                                    callBuffer.Add call |> ignore
                                    disposables.[ro] <- 
                                        { new IDisposable with
                                            member x.Dispose() = 
                                                signature.Surface.Dispose()
                                                call.Dispose()
                                                callBuffer.Remove call |> ignore
                                        }

                                | None ->
                                    output.Add (Add ro)
                                    
                        | Rem(_,ro) ->
                            match disposables.TryRemove ro with
                                | (true, d) -> d.Dispose()
                                | _ -> output.Add (Rem ro)
                total.Stop()

                printfn "total:     %A" total.MicroTime
                printfn "grounding: %A" sw.MicroTime

                output |> HDeltaSet.ofSeq
            )




   