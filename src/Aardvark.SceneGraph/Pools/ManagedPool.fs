namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type GeometrySignature =
    {
        IndexType              : Type
        VertexAttributeTypes   : Map<Symbol, Type>
        InstanceAttributeTypes : Map<Symbol, Type>
    }

type internal PoolResources =
    {
        Pool        : ManagedPool
        IndexPtr    : managedptr
        VertexPtr   : managedptr
        InstancePtr : managedptr
        Disposables : List<IDisposable>
    }

and ManagedDrawCall internal(call: DrawCallInfo, poolResources: PoolResources voption) =
    static let empty = new ManagedDrawCall(DrawCallInfo())

    static member Empty = empty

    member x.Call = call

    member internal x.Resources = poolResources

    new (call: DrawCallInfo) =
        new ManagedDrawCall(call, ValueNone)

    internal new (call: DrawCallInfo, poolResources: PoolResources) =
        new ManagedDrawCall(call, ValueSome poolResources)

    member x.Dispose() =
        poolResources |> ValueOption.iter (_.Pool.Free(x))

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

    let indexManager = LayoutManager<struct (BufferView * int)>()
    let vertexManager = LayoutManager<IDictionary<Symbol, BufferView>>(Dictionary.StructuralComparer.Instance)
    let instanceManager = LayoutManager<IDictionary<Symbol, IAdaptiveValue>>(Dictionary.StructuralComparer.Instance)

    let toDict f = Map.toArray >> Array.map f >> Dictionary.ofArrayV

    let createManagedBuffer n t u s =
        let b = runtime.CreateManagedBuffer(t, u, s)
        b.Acquire()
        b.Name <- n
        b

    let indexBuffer =
        let usage = indexBufferUsage ||| BufferUsage.Index ||| BufferUsage.ReadWrite
        let name = if runtime.DebugLabelsEnabled then "Index Buffer (ManagedPool)" else null
        createManagedBuffer name signature.IndexType usage indexBufferStorage

    let vertexBuffers =
        let usage = vertexBufferUsage ||| BufferUsage.Vertex ||| BufferUsage.ReadWrite
        signature.VertexAttributeTypes |> toDict (fun (k, t) ->
            let name = if runtime.DebugLabelsEnabled then $"{k} (ManagedPool Buffer)" else null
            k, createManagedBuffer name t usage (vertexBufferStorage k)
        )

    let instanceBuffers =
        let usage = instanceBufferUsage ||| BufferUsage.Vertex ||| BufferUsage.ReadWrite
        signature.InstanceAttributeTypes |> toDict (fun (k, t) ->
            let name = if runtime.DebugLabelsEnabled then $"{k} (ManagedPool Buffer)" else null
            k, createManagedBuffer name t usage (instanceBufferStorage k)
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
    ///<param name="indexOffset">An offset added to the FirstIndex field of the resulting draw call. Default is zero.</param>
    ///<param name="faceVertexCount">The face vertex count of the resulting draw call. Ignored if greater than <see cref="geometry.FaceVertexCount"/>, default is <see cref="Int32.MaxValue"/>.</param>
    member x.Add(geometry : AdaptiveGeometry,
                 [<Optional; DefaultParameterValue(0)>] indexOffset : int,
                 [<Optional; DefaultParameterValue(Int32.MaxValue)>] faceVertexCount : int) =
        let faceVertexCount = min faceVertexCount geometry.FaceVertexCount

        if faceVertexCount <= 0 then
            ManagedDrawCall.Empty
        else
            lock x (fun () ->
                let ds = List()
                let fvc = geometry.FaceVertexCount
                let vertexCount = geometry.VertexCount

                let vertexPtr = vertexManager.Alloc(geometry.VertexAttributes, vertexCount)
                let vertexRange = Range1ul.FromManagedPtr(vertexPtr, vertexCount)
                for k, _ in vertexBufferTypes do
                    let target = vertexBuffers.[k]
                    match geometry.VertexAttributes.TryGetValue k with
                    | true, v ->
                        try
                            target.Add(v, vertexRange) |> ds.Add
                        with
                        | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                            failf "cannot convert vertex attribute '%A' from %A to %A" k exn.Source exn.Target

                    | _ ->
                        target.Set(zero, vertexRange)

                let instancePtr = instanceManager.Alloc(geometry.InstanceAttributes, 1)
                let instanceIndex = int instancePtr.Offset
                for k, _ in uniformTypes do
                    let target = instanceBuffers.[k]
                    match geometry.InstanceAttributes.TryGetValue k with
                    | true, v ->
                        try
                            target.Add(v, instanceIndex) |> ds.Add
                        with
                        | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                            failf "cannot convert instance attribute '%A' from %A to %A" k exn.Source exn.Target

                    | _ ->
                        target.Set(zero, Range1ul.FromMinAndSize(uint64 instanceIndex, 0UL))

                let indexPtr =
                    if geometry.IsIndexed then
                        let indexPtr = indexManager.Alloc((geometry.Indices, 0), fvc)
                        let indexRange = Range1ul.FromManagedPtr(indexPtr, fvc)
                        indexBuffer.Add(geometry.Indices, indexRange) |> ds.Add
                        indexPtr
                    else
                        let isNew, indexPtr = indexManager.TryAlloc((Unchecked.defaultof<_>, fvc), fvc)
                        let indexRange = Range1ul.FromManagedPtr(indexPtr, fvc)

                        if isNew then
                            let conv = Aardvark.Base.PrimitiveValueConverter.getArrayConverter typeof<int> signature.IndexType
                            let data = Array.init fvc id |> conv
                            indexBuffer.Set(data, indexRange)

                        indexPtr

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
                | (true, v) -> ValueSome (BufferView(v, v.ElementType))
                | _ -> ValueNone
        }

    member x.InstanceAttributes =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = instanceBuffers |> Seq.map (fun v -> (v.Key, BufferView(v.Value, v.Value.ElementType)))
            member x.TryGetAttribute(sem : Symbol) =
                match instanceBuffers.TryGetValue sem with
                | (true, v) -> ValueSome (BufferView(v, v.ElementType))
                | _ -> ValueNone
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

            if runtime.DebugLabelsEnabled then
                buffer.Name <- "Indirect Buffer (ManagedPool)"

            (buffer.Count, buffer) ||> AdaptiveResource.map2 (
                IndirectBuffer.ofBuffer true 0UL sizeof<DrawCallInfo>
            )

    [<Rule>]
    type PoolSem() =
        member x.RenderObjects(p : Sg.PoolNode, scope : Ag.Scope) : aset<IRenderObject> =
            let pool = p.Pool

            let calls =
                scope.IsActive
                |> ASet.bind (fun isActive -> if isActive then p.Calls else ASet.empty)
                |> DrawCallBuffer.create pool.Runtime p.Storage

            let mutable ro = Unchecked.defaultof<RenderObject>

            ro <- RenderObject.ofScope scope
            ro.Mode <- p.Mode
            ro.Indices <- Some pool.IndexBuffer
            ro.VertexAttributes <- pool.VertexAttributes
            ro.InstanceAttributes <- pool.InstanceAttributes
            ro.DrawCalls <- DrawCalls.Indirect calls

            ASet.single (ro :> IRenderObject)