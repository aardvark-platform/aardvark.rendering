namespace Aardvark.SceneGraph.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

open System
open System.Runtime.InteropServices;
open System.Collections.Generic

#nowarn "9"

/// Struct containing offsets and indices for looking up
/// attributes of a trace instance in a shader.
[<Struct; StructLayout(LayoutKind.Explicit, Size = 20)>]
type TraceGeometryInfo =
    {
        /// The base index of the first primitive within the index buffer.
        [<FieldOffset(0)>]  FirstIndex             : int32

        /// The value to be added to the vertex index before indexing into a vertex attribute buffer.
        [<FieldOffset(4)>]  BaseVertex             : int32

        /// The value to be added to the primitive index before indexing into a face attribute buffer.
        [<FieldOffset(8)>]  BasePrimitive          : int32

        /// The index to look up values in a geometry attribute buffer.
        [<FieldOffset(12)>] GeometryAttributeIndex : int32

        /// The index to look up values in an instance attribute buffer.
        [<FieldOffset(16)>] InstanceAttributeIndex : int32
    }

[<AutoOpen>]
module FShadeInterop =
    open FShade

    module DefaultSemantic =
        let TraceGeometryBuffer = Sym.ofString "TraceGeometryBuffer"

    type UniformScope with
        member x.GeometryInfos : TraceGeometryInfo[] = uniform?StorageBuffer?TraceGeometryBuffer

    module TraceGeometryInfo =

        [<ReflectedDefinition; Inline>]
        let ofGeometryInstance (input: RaytracingInputTypes.GeometryInstance) =
            let id = input.instanceCustomIndex + input.geometryIndex
            uniform.GeometryInfos.[id]

        [<ReflectedDefinition; Inline>]
        let ofRayIntersection (input: RayIntersectionInput) =
            ofGeometryInstance input.geometry

        [<ReflectedDefinition; Inline>]
        let ofRayHit (input: RayHitInput<'T, 'U>) =
            ofGeometryInstance input.geometry


[<CLIMutable>]
type TraceObjectSignature =
    {
        /// Index type (if indices are provided)
        IndexType              : IndexType

        /// Types of attributes defined for each vertex.
        VertexAttributeTypes   : Map<Symbol, Type>

        /// Types of attributes defined for each face (i.e. triangle).
        FaceAttributeTypes     : Map<Symbol, Type>

        /// Attributes defined for each geometry of each instance.
        GeometryAttributeTypes : Map<Symbol, Type>

        /// Attributes defined for each instance.
        InstanceAttributeTypes : Map<Symbol, Type>
    }

[<AutoOpen>]
module private ManagedTracePoolUtils =

    type IndexType with
        member inline this.Type =
            match this with
            | IndexType.UInt16 -> typeof<uint16>
            | _                -> typeof<uint32>

    type IManagedBuffer with
        member inline x.Add(data: AdaptiveIndexData, range: Range1l) =
            let view = BufferView(data.Buffer, data.Type.Type, int data.Offset)
            x.Add(view, range)

type internal TracePoolResources =
    {
        Pool                  : ManagedTracePool
        GeometryPtr           : managedptr
        FaceAttributePtrs     : List<managedptr>
        GeometryAttributePtrs : List<managedptr>
        InstanceAttributePtr  : managedptr
        IndexPtrs             : List<managedptr>
        VertexPtrs            : List<managedptr>
        Disposables           : List<IDisposable>
    }

and ManagedTraceObject internal(index: int, geometry: aval<IAccelerationStructure>, obj: TraceObject, poolResources: TracePoolResources) =

    let customIndex = AVal.constant <| uint32 index

    /// Index indicating position in geometry buffer.
    member x.Index = index

    /// Acceleration structure of the object.
    member x.Geometry = geometry

    member internal x.Resources = poolResources

    member x.Dispose() =
        poolResources.Pool.Free(x)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface ITraceInstance with
        member x.Geometry     = geometry
        member x.HitGroups    = obj.HitGroups
        member x.Transform    = obj.Transform
        member x.FrontFace    = obj.FrontFace
        member x.GeometryMode = obj.GeometryMode
        member x.Mask         = obj.Mask
        member x.CustomIndex  = customIndex

and ManagedTracePool(runtime: IRuntime, signature: TraceObjectSignature,
                     indexBufferStorage: BufferStorage,
                     vertexBufferStorage: Symbol -> BufferStorage,
                     faceAttributeBufferStorage: Symbol -> BufferStorage,
                     geometryAttributeBufferStorage: Symbol -> BufferStorage,
                     instanceAttributeBufferStorage: Symbol -> BufferStorage,
                     geometryBufferStorage : BufferStorage) =

    static let failf fmt =
        Printf.kprintf (fun str ->
            Log.error "[ManagedTracePool] %s" str
            failwith ("[ManagedTracePool] " + str)
        ) fmt

    static let zero : byte[] = ManagedPool.Zero

    let indexType = signature.IndexType.Type
    let faceAttributeTypes = signature.FaceAttributeTypes |> Seq.map (fun (KeyValue(k, v)) -> struct (k, v)) |> Seq.toArray
    let instanceAttributeTypes = signature.InstanceAttributeTypes |> Seq.map (fun (KeyValue(k, v)) -> struct (k, v)) |> Seq.toArray
    let geometryAttributeTypes = signature.GeometryAttributeTypes |> Seq.map (fun (KeyValue(k, v)) -> struct (k, v)) |> Seq.toArray

    let indexManager             = LayoutManager<struct (AdaptiveIndexData * int)>()
    let vertexManager            = LayoutManager<IDictionary<Symbol, BufferView>>(Dictionary.StructuralComparer.Instance)
    let geometryManager          = LayoutManager<int32[] * int32[] * int32[] * int32[] * int32>()
    let faceAttributeManager     = LayoutManager<IDictionary<Symbol, BufferView>>(Dictionary.StructuralComparer.Instance)
    let instanceAttributeManager = LayoutManager<IDictionary<Symbol, IAdaptiveValue>>(Dictionary.StructuralComparer.Instance)
    let geometryAttributeManager = LayoutManager<IDictionary<Symbol, IAdaptiveValue>>(Dictionary.StructuralComparer.Instance)

    let toDict f = Map.toArray >> Array.map f >> Dictionary.ofArrayV

    let createManagedBuffer n t u s =
        let b = runtime.CreateManagedBuffer(t, u, s)
        b.Acquire()
        b.Name <- n
        b

    let indexBuffer =
        let usage = BufferUsage.Write ||| BufferUsage.Storage
        let name = if runtime.DebugLabelsEnabled then "Index Buffer (ManagedTracePool)" else null
        createManagedBuffer name indexType usage indexBufferStorage

    let vertexBuffers =
        let usage = BufferUsage.Write ||| BufferUsage.Storage
        signature.VertexAttributeTypes |> toDict (fun (s, t) ->
            let name = if runtime.DebugLabelsEnabled then $"{s} (ManagedTracePool Buffer)" else null
            s, createManagedBuffer name t usage (vertexBufferStorage s)
        )

    let faceAttributeBuffers =
        let usage = BufferUsage.Write ||| BufferUsage.Storage
        signature.FaceAttributeTypes |> toDict (fun (s, t) ->
            let name = if runtime.DebugLabelsEnabled then $"{s} (ManagedTracePool Buffer)" else null
            s, createManagedBuffer name t usage (faceAttributeBufferStorage s)
        )

    let geometryAttributeBuffers =
        let usage = BufferUsage.Write ||| BufferUsage.Storage
        signature.GeometryAttributeTypes |> toDict (fun (s, t) ->
            let name = if runtime.DebugLabelsEnabled then $"{s} (ManagedTracePool Buffer)" else null
            s, createManagedBuffer name t usage (geometryAttributeBufferStorage s)
        )

    let instanceAttributeBuffers =
        let usage = BufferUsage.Write ||| BufferUsage.Storage
        signature.InstanceAttributeTypes |> toDict (fun (s, t) ->
            let name = if runtime.DebugLabelsEnabled then $"{s} (ManagedTracePool Buffer)" else null
            s, createManagedBuffer name t usage (instanceAttributeBufferStorage s)
        )

    let geometryBuffer =
        let usage = BufferUsage.Write ||| BufferUsage.Storage
        let name = if runtime.DebugLabelsEnabled then $"Geometry Buffer (ManagedTracePool)" else null
        createManagedBuffer name typeof<TraceGeometryInfo> usage geometryBufferStorage

    let accelerationStructures =
        Dictionary<struct (AdaptiveTraceGeometry * AccelerationStructureUsage), aval<IAccelerationStructure>>()

    let objects = HashSet<ManagedTraceObject>()

    let free (obj: ManagedTraceObject) =
        obj.Geometry.Release()
        for d in obj.Resources.Disposables do d.Dispose()

        geometryManager.Free(obj.Resources.GeometryPtr)
        instanceAttributeManager.Free(obj.Resources.InstanceAttributePtr)

        for p in obj.Resources.FaceAttributePtrs do faceAttributeManager.Free(p)
        for p in obj.Resources.GeometryAttributePtrs do geometryAttributeManager.Free(p)
        for p in obj.Resources.IndexPtrs do indexManager.Free(p)
        for p in obj.Resources.VertexPtrs do vertexManager.Free(p)

    let clear() =
        for obj in objects do
            free obj

        indexBuffer.Clear()
        geometryBuffer.Clear()
        accelerationStructures.Clear()
        objects.Clear()
        for KeyValue(_, b) in vertexBuffers do b.Clear()
        for KeyValue(_, b) in faceAttributeBuffers do b.Clear()
        for KeyValue(_, b) in geometryAttributeBuffers do b.Clear()
        for KeyValue(_, b) in instanceAttributeBuffers do b.Clear()

    new (runtime: IRuntime, signature : TraceObjectSignature,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] indexBufferStorage: BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] vertexBufferStorage: BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] faceAttributeBufferStorage: BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] geometryAttributeBufferStorage: BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Host)>] instanceAttributeBufferStorage: BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] geometryBufferStorage: BufferStorage) =
        new ManagedTracePool(
            runtime, signature, indexBufferStorage,
            (fun _ -> vertexBufferStorage),
            (fun _ -> faceAttributeBufferStorage),
            (fun _ -> geometryAttributeBufferStorage),
            (fun _ -> instanceAttributeBufferStorage),
            geometryBufferStorage
        )

    member x.Runtime = runtime
    member x.Signature = signature
    member x.Count = objects.Count

    member internal x.Free(obj: ManagedTraceObject) =
        lock x (fun _ ->
            if objects.Remove(obj) then
                free obj

                if objects.Count = 0 then
                    clear()
        )

    member x.Add(obj: TraceObject) =
        if obj.Geometry.Count = 0 then
            failf "trace object does not contain any geometry"

        lock x (fun _ ->
            let ds = List()
            let vptrs = List()
            let iptrs = List()
            let fptrs = List()
            let gptrs = List()

            let geometryCount      = obj.Geometry.Count
            let vertexAttributes   = obj.VertexAttributes
            let faceAttributes     = obj.FaceAttributes
            let geometryAttributes = obj.GeometryAttributes
            let instanceAttributes = obj.InstanceAttributes

            // Geometry attributes
            let geometryAttributeIndices =
                Array.init geometryCount (fun i ->
                    let geometryAttributes = geometryAttributes.[i]
                    let geometryAttributePtr = geometryAttributeManager.Alloc(geometryAttributes, 1)
                    let geometryAttributeIndex = int geometryAttributePtr.Offset

                    for k, _ in geometryAttributeTypes do
                        let target = geometryAttributeBuffers.[k]
                        match geometryAttributes.TryGetValue k with
                        | true, v ->
                            try
                                target.Add(v, geometryAttributeIndex) |> ds.Add
                            with
                            | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                                failf "cannot convert geometry attribute '%A' from %A to %A" k exn.Source exn.Target

                        | _ ->
                            target.Set(zero, Range1l.FromMinAndSize(int64 geometryAttributeIndex, 0L))

                    gptrs.Add(geometryAttributePtr)
                    int32 geometryAttributePtr.Offset
                )

            // Instance attributes
            let instanceAttributePtr   = instanceAttributeManager.Alloc(instanceAttributes, 1)
            let instanceAttributeIndex = int instanceAttributePtr.Offset

            for k, _ in instanceAttributeTypes do
                let target = instanceAttributeBuffers.[k]
                match instanceAttributes.TryGetValue k with
                | true, v ->
                    try
                        target.Add(v, instanceAttributeIndex) |> ds.Add
                    with
                    | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                        failf "cannot convert instance attribute '%A' from %A to %A" k exn.Source exn.Target

                | _ ->
                    target.Set(zero, Range1l.FromMinAndSize(int64 instanceAttributeIndex, 0L))

            // Geometry data
            let geometryIndex, geometryPtr =
                match obj.Geometry with
                | AdaptiveTraceGeometry.Triangles meshes ->

                    let vertexOffsets =
                        meshes |> Array.mapi (fun i m ->
                            let vertexCount = int m.Vertices.Count
                            let vertexAttributes = vertexAttributes.[i]
                            let vertexPtr = vertexManager.Alloc(vertexAttributes, vertexCount)
                            let vertexRange = Range1l.FromMinAndSize(int64 vertexPtr.Offset, int64 vertexCount - 1L)

                            for KeyValue(k, _) in signature.VertexAttributeTypes do
                                let target = vertexBuffers.[k]
                                match vertexAttributes.TryGetValue k with
                                | true, v ->
                                    try
                                        target.Add(v, vertexRange) |> ds.Add
                                    with
                                    | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                                        failf "cannot convert vertex attribute '%A' from %A to %A" k exn.Source exn.Target

                                | _ ->
                                    target.Set(zero, vertexRange)

                            vptrs.Add(vertexPtr)
                            int32 vertexPtr.Offset
                        )

                    let indexOffsets =
                        meshes |> Array.map (fun m ->
                            let fvc = int m.Primitives * 3

                            let indexPtr =
                                if m.IsIndexed then
                                    if m.Indices.Type <> signature.IndexType then
                                        failf $"Expected indices of type {signature.IndexType} but got {m.Indices.Type}"

                                    let indexPtr = indexManager.Alloc((m.Indices, 0), fvc)
                                    let indexRange = Range1l.FromMinAndSize(int64 indexPtr.Offset, int64 fvc - 1L)
                                    indexBuffer.Add(m.Indices, indexRange) |> ds.Add
                                    indexPtr
                                else
                                    let isNew, indexPtr = indexManager.TryAlloc((null, fvc), fvc)
                                    let indexRange = Range1l.FromMinAndSize(int64 indexPtr.Offset, int64 fvc - 1L)

                                    if isNew then
                                        let conv = Aardvark.Base.PrimitiveValueConverter.getArrayConverter typeof<int> indexType
                                        let data = Array.init fvc id |> conv
                                        indexBuffer.Set(data, indexRange)

                                    indexPtr

                            iptrs.Add(indexPtr)
                            int32 indexPtr.Offset
                        )

                    let faceAttributeOffsets =
                        meshes |> Array.mapi (fun i m ->
                            let faceCount = int m.Primitives
                            let faceAttributes = faceAttributes.[i]
                            let faceAttributePtr = faceAttributeManager.Alloc(faceAttributes, faceCount)
                            let faceAttributeRange = Range1l.FromMinAndSize(int64 faceAttributePtr.Offset, int64 faceCount - 1L)

                            for k, _ in faceAttributeTypes do
                                let target = faceAttributeBuffers.[k]
                                match faceAttributes.TryGetValue k with
                                | true, v ->
                                    try
                                        target.Add(v, faceAttributeRange) |> ds.Add
                                    with
                                    | :? Aardvark.Base.PrimitiveValueConverter.InvalidConversionException as exn ->
                                        failf "cannot convert face attribute '%A' from %A to %A" k exn.Source exn.Target

                                | _ ->
                                    target.Set(zero, faceAttributeRange)

                            fptrs.Add(faceAttributePtr)
                            int32 faceAttributePtr.Offset
                        )

                    let geometryKey   = (vertexOffsets, indexOffsets, faceAttributeOffsets, geometryAttributeIndices, instanceAttributeIndex)
                    let geometryPtr   = geometryManager.Alloc(geometryKey, geometryCount)
                    let geometryIndex = int geometryPtr.Offset

                    for i = 0 to meshes.Length - 1 do
                        let info =
                            { FirstIndex             = indexOffsets.[i]
                              BaseVertex             = vertexOffsets.[i]
                              BasePrimitive          = faceAttributeOffsets.[i]
                              GeometryAttributeIndex = geometryAttributeIndices.[i]
                              InstanceAttributeIndex = instanceAttributeIndex }

                        geometryBuffer.Set(info, geometryIndex + i)

                    geometryIndex, geometryPtr

                | AdaptiveTraceGeometry.AABBs aabbs ->
                    let geometryKey   = ([||], [||], [||], geometryAttributeIndices, instanceAttributeIndex)
                    let geometryPtr   = geometryManager.Alloc(geometryKey, geometryCount)
                    let geometryIndex = int geometryPtr.Offset

                    for i = 0 to aabbs.Length - 1 do
                        let info =
                            { FirstIndex             = 0
                              BaseVertex             = 0
                              BasePrimitive          = 0
                              GeometryAttributeIndex = geometryAttributeIndices.[i]
                              InstanceAttributeIndex = instanceAttributeIndex }

                        geometryBuffer.Set(info, geometryIndex + i)

                    geometryIndex, geometryPtr

            let accel =
                let key = struct (obj.Geometry, obj.Usage)

                match accelerationStructures.TryGetValue(key) with
                | true, accel -> accel
                | _ ->
                    let data = obj.Geometry.ToAdaptiveValue()
                    let accel = runtime.CreateAccelerationStructure(data, obj.Usage)
                    if runtime.DebugLabelsEnabled then accel.Name <- "TraceObject (ManagedTracePool)"
                    accelerationStructures.[key] <- accel
                    accel :> aval<_>

            accel.Acquire()

            let resources =
                {
                    Pool                  = x
                    GeometryPtr           = geometryPtr
                    GeometryAttributePtrs = gptrs
                    InstanceAttributePtr  = instanceAttributePtr
                    FaceAttributePtrs     = fptrs
                    IndexPtrs             = iptrs
                    VertexPtrs            = vptrs
                    Disposables           = ds
                }

            let mto = new ManagedTraceObject(geometryIndex, accel, obj, resources)
            objects.Add(mto) |> ignore
            mto
        )

    /// Buffer of TraceGeometryInfo structs.
    member x.GeometryBuffer =
        geometryBuffer |> AdaptiveResource.cast<IBuffer>

    member x.IndexBuffer =
        indexBuffer |> AdaptiveResource.cast<IBuffer>

    member x.TryGetVertexAttribute(semantic: Symbol) =
        match vertexBuffers.TryGetValue semantic with
        | true, buffer -> ValueSome <| AdaptiveResource.cast<IBuffer> buffer
        | _ -> ValueNone

    member x.GetVertexAttribute(semantic: Symbol) =
        match x.TryGetVertexAttribute semantic with
        | ValueSome attr -> attr
        | ValueNone -> failf "could not find vertex attribute '%A'" semantic

    member x.TryGetFaceAttribute(semantic: Symbol) =
        match faceAttributeBuffers.TryGetValue semantic with
        | true, buffer -> ValueSome <| AdaptiveResource.cast<IBuffer> buffer
        | _ -> ValueNone

    member x.GetFaceAttribute(semantic: Symbol) =
        match x.TryGetFaceAttribute semantic with
        | ValueSome attr -> attr
        | ValueNone -> failf "could not find face attribute '%A'" semantic

    member x.TryGetGeometryAttribute(semantic: Symbol) =
        match geometryAttributeBuffers.TryGetValue semantic with
        | true, buffer -> ValueSome <| AdaptiveResource.cast<IBuffer> buffer
        | _ -> ValueNone

    member x.GetGeometryAttribute(semantic: Symbol) =
        match x.TryGetGeometryAttribute semantic with
        | ValueSome attr -> attr
        | ValueNone -> failf "could not find geometry attribute '%A'" semantic

    member x.TryGetInstanceAttribute(semantic: Symbol) =
        match instanceAttributeBuffers.TryGetValue semantic with
        | true, buffer -> ValueSome <| AdaptiveResource.cast<IBuffer> buffer
        | _ -> ValueNone

    member x.GetInstanceAttribute(semantic: Symbol) =
        match x.TryGetInstanceAttribute semantic with
        | ValueSome attr -> attr
        | ValueNone -> failf "could not find instance attribute '%A'" semantic

    member x.Dispose() =
        lock x (fun _ ->
            clear()

            indexBuffer.Release()
            geometryBuffer.Release()
            for KeyValue(_, b) in vertexBuffers do b.Release()
            for KeyValue(_, b) in faceAttributeBuffers do b.Release()
            for KeyValue(_, b) in geometryAttributeBuffers do b.Release()
            for KeyValue(_, b) in instanceAttributeBuffers do b.Release()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AutoOpen>]
module ManagedTracePoolSceneExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RaytracingSceneDescription =

        let ofPool (pool: ManagedTracePool) (objects: aset<TraceObject>) =
            let reader = objects.GetReader()
            let mtos = Dict<TraceObject, ManagedTraceObject>()

            let add (deltas : List<_>) (o : TraceObject) =
                match mtos.TryGetValue o with
                | (true, _) -> ()
                | _ ->
                    let mto = pool.Add(o)
                    mtos.Add(o, mto)
                    deltas.Add(SetOperation.add mto)

            let rem (deltas : List<_>) (o : TraceObject) =
                match mtos.TryRemove o with
                | (true, mto) ->
                    deltas.Add(SetOperation.rem mto)
                    mto.Dispose()
                | _ -> ()

            let objects =
                ASet.custom (fun t _ ->
                    let deltas = List<_>()
                    let ops = reader.GetChanges(t)

                    for o in ops do
                        match o with
                        | Add(_, value) -> add deltas value
                        | Rem(_, value) -> rem deltas value

                    HashSetDelta.ofSeq deltas
                )

            RaytracingSceneDescription.ofASet objects