namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Base.Monads.State

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.CompilerServices

#nowarn "9"
#nowarn "51"

[<ReferenceEquality; NoComparison>]
type AdaptiveGeometry =
    {
        FaceVertexCount    : int
        VertexCount        : int
        Indices            : Option<BufferView>
        VertexAttributes   : Map<Symbol, BufferView>
        InstanceAttributes : Map<Symbol, IAdaptiveValue>
    }

/// NOTE: temporary data structure to avoid conflicts and will be reworked in 5.6 (probably become record)
[<NoComparison>]
type PooledGeometry (faceVertexCount : int, vertexCount : int, indices : voption<BufferView>, vertexAttributes : SymbolDict<BufferView>, instanceAttributes : SymbolDict<IAdaptiveValue>) =
    member x.FaceVertexCount = faceVertexCount
    member x.VertexCount = vertexCount
    member x.Indices = indices
    member x.VertexAttributes = vertexAttributes
    member x.InstanceAttributes = instanceAttributes

type GeometrySignature =
    {
        IndexType              : Type
        VertexAttributeTypes   : Map<Symbol, Type>
        InstanceAttributeTypes : Map<Symbol, Type>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GeometrySignature =

    let create (indexType : Type) (vertexAttributeTypes : Map<Symbol, Type>) (instanceAttributeTypes : Map<Symbol, Type>) =
        { IndexType              = indexType
          VertexAttributeTypes   = vertexAttributeTypes
          InstanceAttributeTypes = instanceAttributeTypes }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveGeometry =

    let ofIndexedGeometry (instanceAttributes : list<Symbol * IAdaptiveValue>) (ig : IndexedGeometry) =
        let anyAtt = (ig.IndexedAttributes |> Seq.head).Value

        let faceVertexCount, index =
            match ig.IndexArray with
                | null -> anyAtt.Length, None
                | index -> index.Length, Some (BufferView.ofArray index)

        let vertexCount =
            anyAtt.Length

        {
            FaceVertexCount = faceVertexCount
            VertexCount = vertexCount
            Indices = index
            VertexAttributes = ig.IndexedAttributes |> SymDict.toMap |> Map.map (fun _ -> BufferView.ofArray)
            InstanceAttributes = Map.ofList instanceAttributes
        }


type private LayoutManager<'a when 'a : equality>(cmp : IEqualityComparer<'a>)=

    let manager = MemoryManager.createNop()
    let store = Dictionary<'a, managedptr>(cmp)
    let cnts = Dictionary<managedptr, struct('a * ref<int>)>()

    member x.Alloc(key : 'a, size : int) =
        match store.TryGetValue key with
        | (true, v) ->
            let struct(_,r) = cnts.[v]
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
            let struct(_,r) = cnts.[v]
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

    new() = LayoutManager(null)

type internal PoolResources =
    {
        Pool        : ManagedPool
        IndexPtr    : managedptr
        VertexPtr   : managedptr
        InstancePtr : managedptr
        Disposables : List<IDisposable>
    }

and ManagedDrawCall internal(call : DrawCallInfo, poolResources : voption<PoolResources>) =
    static let empty = new ManagedDrawCall(DrawCallInfo())

    static member Empty = empty

    member x.Call = call

    member internal x.Resources = poolResources

    new (call : DrawCallInfo) =
        new ManagedDrawCall(call, ValueNone)

    internal new (call : DrawCallInfo, poolResources : PoolResources) =
        new ManagedDrawCall(call, ValueSome poolResources)

    member x.Dispose() =
        poolResources |> ValueOption.iter (fun r ->
            r.Pool.Free(x)
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and ManagedPool(runtime : IRuntime, signature : GeometrySignature,
                indexBufferUsage : BufferUsage, indexBufferStorage : BufferStorage,
                vertexBufferUsage : BufferUsage, vertexBufferStorage : Symbol -> BufferStorage,
                instanceBufferUsage : BufferUsage, instanceBufferStorage : Symbol -> BufferStorage) =

    static let failf fmt =
        Printf.kprintf (fun str ->
            Log.error "[ManagedPool] %s" str
            failwith ("[ManagedPool] " + str)
        ) fmt

    static let zero : byte[] = Array.zeroCreate 4096

    
    static let cmp = { new IEqualityComparer<obj> with

                   // NOTE: SymbolDict enumeration only guaranteed to be equal when created "identical" (order of inserts, resizes, capacity, ...)
                   //       GetHashCode -> build sum of key and value hashes
                   //       Equals -> lookup each

                   // NOTE2: Implementing a SymbolDict (SymbolMap?) that keeps it entries sorted by hash might also be an option

                   member x.Equals(a, b) : bool =
                        if Object.ReferenceEquals(a, b) then
                            true
                        else 
                            match a with
                            | :? SymbolDict<BufferView> as d1 ->
                                match b with 
                                | :? SymbolDict<BufferView> as d2 ->
                                    if d1.Count <> d2.Count then
                                        false
                                    else
                                        let mutable e1 = d1.GetEnumerator()
                                        let mutable equal = true
                                        while equal && e1.MoveNext() do
                                            let kva = e1.Current
                                            equal <- 
                                                match d2.TryGetValue kva.Key with
                                                | (true, vb) -> 
                                                    kva.Value.Equals(vb)
                                                | _ -> false
                                        equal

                                | _ -> false
                            | _ -> a.Equals(b)

                   member x.GetHashCode (obj: obj): int = 
                       match obj with 
                       | :? SymbolDict<BufferView> as d ->
                                let mutable symHashSum = 0
                                let mutable valHashSum = 0
                                let mutable e = d.GetEnumerator()
                                while (e.MoveNext()) do
                                    let v = e.Current
                                    symHashSum <- symHashSum + v.Key.GetHashCode()
                                    valHashSum <- valHashSum + v.Value.GetHashCode()
                                Aardvark.Base.HashCode.Combine(symHashSum, valHashSum)
                       | _ -> obj.GetHashCode()
                }

    let indexManager = LayoutManager<struct(obj * int)>(null)
    let vertexManager = LayoutManager<obj>(cmp)
    let instanceManager = LayoutManager<obj>(cmp)

    let toDict f = Map.toSeq >> Seq.map f >> SymDict.ofSeq

    let createManagedBuffer t u s =
        let b = runtime.CreateManagedBuffer(t, u, s)
        b.Acquire()
        b

    let indexBuffer =
        let usage = indexBufferUsage ||| BufferUsage.Index ||| BufferUsage.ReadWrite
        createManagedBuffer signature.IndexType usage indexBufferStorage

    let vertexBuffers =
        let usage = vertexBufferUsage ||| BufferUsage.Vertex ||| BufferUsage.ReadWrite
        signature.VertexAttributeTypes |> toDict (fun (k, t) ->
            k, createManagedBuffer t usage (vertexBufferStorage k)
        )

    let instanceBuffers =
        let usage = instanceBufferUsage ||| BufferUsage.Vertex ||| BufferUsage.ReadWrite
        signature.InstanceAttributeTypes |> toDict (fun (k, t) ->
            k, createManagedBuffer t usage (instanceBufferStorage k)
        )

    let vertexBufferTypes = Map.toArray signature.VertexAttributeTypes |> Array.map (fun (a,b) -> struct(a, b))
    let uniformTypes = Map.toArray signature.InstanceAttributeTypes |> Array.map (fun (a,b) -> struct(a, b))

    let drawCalls = HashSet<ManagedDrawCall>()

    let free (mdc : ManagedDrawCall) =
        for d in mdc.Resources.Value.Disposables do d.Dispose()
        vertexManager.Free mdc.Resources.Value.VertexPtr
        instanceManager.Free mdc.Resources.Value.InstancePtr
        indexManager.Free mdc.Resources.Value.IndexPtr

    let clear() =
        for mdc in drawCalls do
            free mdc

        indexBuffer.Clear()
        for KeyValue(_, b) in vertexBuffers do b.Clear()
        for KeyValue(_, b) in instanceBuffers do b.Clear()
        drawCalls.Clear()

    new (runtime : IRuntime, signature : GeometrySignature,
         indexBufferStorage : BufferStorage, vertexBufferStorage : Symbol -> BufferStorage, instanceBufferStorage : Symbol -> BufferStorage) =
        new ManagedPool(
            runtime, signature,
            BufferUsage.None, indexBufferStorage,
            BufferUsage.None, vertexBufferStorage,
            BufferUsage.None, instanceBufferStorage
        )

    new (runtime : IRuntime, signature : GeometrySignature,
         indexBufferUsage : BufferUsage, vertexBufferUsage : BufferUsage, instanceBufferUsage : BufferUsage) =
        new ManagedPool(
            runtime, signature,
            indexBufferUsage, BufferStorage.Device,
            vertexBufferUsage, (fun _ -> BufferStorage.Device),
            instanceBufferUsage, (fun _ -> BufferStorage.Host)
        )

    new (runtime : IRuntime, signature : GeometrySignature, usage : BufferUsage) =
        new ManagedPool(runtime, signature, usage, usage, usage)

    new (runtime : IRuntime, signature : GeometrySignature) =
        new ManagedPool(runtime, signature, BufferUsage.None)

    static member internal Zero = zero

    member x.Runtime = runtime
    member x.Signature = signature
    member x.Count = drawCalls.Count

    member internal x.Free(mdc : ManagedDrawCall) =
        lock x (fun _ ->
            if drawCalls.Remove(mdc) then
                free mdc

                if drawCalls.Count = 0 then
                    clear()
            else
                raise <| ObjectDisposedException("ManagedDrawCall")
        )

    ///<summary>Adds the given geometry to the pool and returns a managed draw call.</summary>
    ///<param name="geometry">The geometry to add.</param>
    member x.Add(geometry : AdaptiveGeometry) =
        x.Add(geometry, 0, geometry.FaceVertexCount)

    ///<summary>Adds the given geometry to the pool and returns a managed draw call.</summary>
    ///<param name="geometry">The geometry to add.</param>
    member x.Add(geometry : PooledGeometry) =
        x.Add(geometry, 0, geometry.FaceVertexCount)

    ///<summary>Adds the given geometry to the pool and returns a managed draw call.</summary>
    ///<param name="geometry">The geometry to add.</param>
    ///<param name="indexOffset">An offset added to the FirstIndex field of the resulting draw call.</param>
    ///<param name="faceVertexCount">The face vertex count of the resulting draw call.</param>
    member x.Add(geometry : AdaptiveGeometry, indexOffset : int, faceVertexCount : int) =
        let faceVertexCount = min faceVertexCount geometry.FaceVertexCount

        if faceVertexCount <= 0 then
            ManagedDrawCall.Empty
        else
            lock x (fun () ->
                let ds = List()
                let fvc = geometry.FaceVertexCount
                let vertexCount = geometry.VertexCount

                let vertexPtr = vertexManager.Alloc(geometry.VertexAttributes, vertexCount)
                let vertexRange = Range1l(int64 vertexPtr.Offset, int64 vertexPtr.Offset + int64 vertexCount - 1L)
                for (k,_) in vertexBufferTypes do
                    let target = vertexBuffers.[k]
                    match Map.tryFind k geometry.VertexAttributes with
                    | Some v ->
                        try
                            target.Add(v, vertexRange) |> ds.Add
                        with
                        | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                            failf "cannot convert vertex attribute '%A' from %A to %A" k exn.Source exn.Target

                    | None ->
                        target.Set(zero, vertexRange)

                let instancePtr = instanceManager.Alloc(geometry.InstanceAttributes, 1)
                let instanceIndex = int instancePtr.Offset
                for (k,_) in uniformTypes do
                    let target = instanceBuffers.[k]
                    match Map.tryFind k geometry.InstanceAttributes with
                    | Some v ->
                        try
                            target.Add(v, instanceIndex) |> ds.Add
                        with
                        | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                            failf "cannot convert instance attribute '%A' from %A to %A" k exn.Source exn.Target

                    | None ->
                        target.Set(zero, Range1l(int64 instanceIndex, int64 instanceIndex))

                let isNew, indexPtr = indexManager.TryAlloc((geometry.Indices, fvc), fvc)
                let indexRange = Range1l(int64 indexPtr.Offset, int64 indexPtr.Offset + int64 fvc - 1L)
                match geometry.Indices with
                | Some v -> indexBuffer.Add(v, indexRange) |> ds.Add
                | None ->
                    if isNew then
                        let conv = Aardvark.Base.PrimitiveValueConverter.getArrayConverter typeof<int> signature.IndexType
                        let data = Array.init fvc id |> conv
                        indexBuffer.Set(data, indexRange)

                let resources =
                    {
                        Pool        = x
                        IndexPtr    = indexPtr
                        VertexPtr   = vertexPtr
                        InstancePtr = instancePtr
                        Disposables = ds
                    }

                let call =
                    DrawCallInfo(
                        FaceVertexCount = faceVertexCount,
                        FirstIndex = int indexPtr.Offset + indexOffset,
                        FirstInstance = int instancePtr.Offset,
                        InstanceCount = 1,
                        BaseVertex = int vertexPtr.Offset
                    )

                let mdc = new ManagedDrawCall(call, resources)
                drawCalls.Add(mdc) |> ignore
                mdc
            )

    ///<summary>Adds the given geometry to the pool and returns a managed draw call.</summary>
    ///<param name="geometry">The geometry to add.</param>
    ///<param name="indexOffset">An offset added to the FirstIndex field of the resulting draw call.</param>
    ///<param name="faceVertexCount">The face vertex count of the resulting draw call.</param>
    member x.Add(geometry : PooledGeometry, indexOffset : int, faceVertexCount : int) =
        let faceVertexCount = min faceVertexCount geometry.FaceVertexCount

        if faceVertexCount <= 0 then
            ManagedDrawCall.Empty
        else
            lock x (fun () ->
                let ds = List()
                let fvc = geometry.FaceVertexCount
                let vertexCount = geometry.VertexCount

                let vertexPtr = vertexManager.Alloc(geometry.VertexAttributes, vertexCount)
                let vertexRange = Range1l(int64 vertexPtr.Offset, int64 vertexPtr.Offset + int64 vertexCount - 1L)
                for (k,_) in vertexBufferTypes do
                    let target = vertexBuffers.[k]
                    match geometry.VertexAttributes.TryGetValue k with
                    | (true, v) ->
                        try
                            target.Add(v, vertexRange) |> ds.Add
                        with
                        | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                            failf "cannot convert vertex attribute '%A' from %A to %A" k exn.Source exn.Target

                    | _ ->
                        target.Set(zero, vertexRange)

                let instancePtr = instanceManager.Alloc(geometry.InstanceAttributes, 1)
                let instanceIndex = int instancePtr.Offset
                for (k,_) in uniformTypes do
                    let target = instanceBuffers.[k]
                    match geometry.InstanceAttributes.TryGetValue k with
                    | (true, v) ->
                        try
                            target.Add(v, instanceIndex) |> ds.Add
                        with
                        | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                            failf "cannot convert instance attribute '%A' from %A to %A" k exn.Source exn.Target

                    | _ ->
                        target.Set(zero, Range1l(int64 instanceIndex, int64 instanceIndex))

                let isNew, indexPtr = indexManager.TryAlloc((geometry.Indices, fvc), fvc)
                let indexRange = Range1l(int64 indexPtr.Offset, int64 indexPtr.Offset + int64 fvc - 1L)
                match geometry.Indices with
                | ValueSome v -> indexBuffer.Add(v, indexRange) |> ds.Add
                | ValueNone ->
                    if isNew then
                        let conv = Aardvark.Base.PrimitiveValueConverter.getArrayConverter typeof<int> signature.IndexType
                        let data = Array.init fvc id |> conv
                        indexBuffer.Set(data, indexRange)

                let resources =
                    {
                        Pool        = x
                        IndexPtr    = indexPtr
                        VertexPtr   = vertexPtr
                        InstancePtr = instancePtr
                        Disposables = ds
                    }

                let call =
                    DrawCallInfo(
                        FaceVertexCount = faceVertexCount,
                        FirstIndex = int indexPtr.Offset + indexOffset,
                        FirstInstance = int instancePtr.Offset,
                        InstanceCount = 1,
                        BaseVertex = int vertexPtr.Offset
                    )

                let mdc = new ManagedDrawCall(call, resources)
                drawCalls.Add(mdc) |> ignore
                mdc
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

    member x.Dispose() =
        lock x (fun _ ->
            clear()

            indexBuffer.Release()
            for KeyValue(_, b) in vertexBuffers do b.Release()
            for KeyValue(_, b) in instanceBuffers do b.Release()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AbstractClass; Sealed; Extension>]
type IRuntimePoolExtensions private() =

    /// Creates a managed pool with the given geometry signature.
    /// Index and vertex buffers use BufferStorage.Device, instance buffers use BufferStorage.Host.
    [<Extension>]
    static member CreateManagedPool(this : IRuntime, signature : GeometrySignature) =
        new ManagedPool(this, signature)

    /// Creates a managed pool with the given geometry signature using the given additional usage flags for all buffers.
    /// Index and vertex buffers use BufferStorage.Device, instance buffers use BufferStorage.Host.
    [<Extension>]
    static member CreateManagedPool(this : IRuntime, signature : GeometrySignature, usage : BufferUsage) =
        new ManagedPool(this, signature, usage)

    /// Creates a managed pool with the given geometry signature using the given additional usage flags for the corresponding buffers.
    /// Index and vertex buffers use BufferStorage.Device, instance buffers use BufferStorage.Host.
    [<Extension>]
    static member CreateManagedPool(this : IRuntime, signature : GeometrySignature,
                                    indexBufferUsage : BufferUsage, vertexBufferUsage : BufferUsage, instanceBufferUsage : BufferUsage) =
        new ManagedPool(this, signature, indexBufferUsage, vertexBufferUsage, instanceBufferUsage)

    /// Creates a managed pool with the given geometry signature using the given storage type for the corresponding buffers.
    [<Extension>]
    static member CreateManagedPool(this : IRuntime, signature : GeometrySignature,
                                    indexBufferStorage : BufferStorage,
                                    vertexBufferStorage : Symbol -> BufferStorage,
                                    instanceBufferStorage : Symbol -> BufferStorage) =
        new ManagedPool(this, signature, indexBufferStorage, vertexBufferStorage, instanceBufferStorage)

    /// Creates a managed pool with the given geometry signature using the given storage type and additional usage flags for the corresponding buffers.
    [<Extension>]
    static member CreateManagedPool(this : IRuntime, signature : GeometrySignature,
                                    indexBufferUsage : BufferUsage, indexBufferStorage : BufferStorage,
                                    vertexBufferUsage : BufferUsage, vertexBufferStorage : Symbol -> BufferStorage,
                                    instanceBufferUsage : BufferUsage, instanceBufferStorage : Symbol -> BufferStorage) =
        new ManagedPool(
            this, signature,
            indexBufferUsage, indexBufferStorage,
            vertexBufferUsage, vertexBufferStorage,
            instanceBufferUsage, instanceBufferStorage
        )

[<AutoOpen>]
module ManagedPoolSg =
    open System.Runtime.InteropServices

    module Sg =
        type PoolNode(pool : ManagedPool, calls : aset<ManagedDrawCall>, mode : IndexedGeometryMode,
                      [<Optional; DefaultParameterValue(BufferStorage.Device)>] storage : BufferStorage) =
            interface ISg
            member x.Pool = pool
            member x.Calls = calls
            member x.Mode = mode
            member x.Storage = storage

        /// Draws an adaptive set of managed draw calls of the given pool.
        let pool (pool : ManagedPool) (mode : IndexedGeometryMode) (calls : aset<ManagedDrawCall>) =
            PoolNode(pool, calls, mode) :> ISg


module ``Pool Semantics`` =
    open Aardvark.SceneGraph.Semantics

    module internal DrawCallBuffer =

        let private evaluate (mdc : ManagedDrawCall) =
            let mutable c = mdc.Call
            DrawCallInfo.ToggleIndexed(&c)
            c

        let create (runtime : IRuntime) (storage : BufferStorage) (calls : aset<ManagedDrawCall>) =
            let buffer =
                runtime.CreateCompactBuffer(
                    evaluate, calls,
                    BufferUsage.Indirect ||| BufferUsage.ReadWrite,
                    storage
                )

            (buffer.Count, buffer) ||> AdaptiveResource.map2 (
                IndirectBuffer.ofBuffer true sizeof<DrawCallInfo>
            )

    [<Rule>]
    type PoolSem() =
        member x.RenderObjects(p : Sg.PoolNode, scope : Ag.Scope) : aset<IRenderObject> =
            let pool = p.Pool

            let calls =
                scope.IsActive
                |> ASet.bind (fun isActive ->
                    if isActive then p.Calls else ASet.empty
                )
                |> DrawCallBuffer.create pool.Runtime p.Storage

            let mutable ro = Unchecked.defaultof<RenderObject>

            ro <- RenderObject.ofScope scope
            ro.Mode <- p.Mode
            ro.Indices <- Some pool.IndexBuffer
            ro.VertexAttributes <- pool.VertexAttributes
            ro.InstanceAttributes <- pool.InstanceAttributes
            ro.DrawCalls <- Indirect calls

            ASet.single (ro :> IRenderObject)