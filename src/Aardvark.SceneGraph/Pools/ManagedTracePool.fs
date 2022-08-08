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

[<Struct; StructLayout(LayoutKind.Explicit, Size = 16)>]
type TraceGeometryInfo =
    {
        [<FieldOffset(0)>]  FirstIndex             : int32
        [<FieldOffset(4)>]  BaseVertex             : int32
        [<FieldOffset(8)>]  InstanceAttributeIndex : int32
        [<FieldOffset(12)>] GeometryAttributeIndex : int32
    }

type TraceObjectSignature =
    {
        /// Index type (if indices are provided)
        IndexType              : IndexType

        /// Types of attributes defined for each vertex.
        VertexAttributeTypes   : Map<Symbol, Type>

        /// Attributes defined for each instance.
        InstanceAttributeTypes : Map<Symbol, Type>

        /// Attributes defined for each geometry of each instance.
        GeometryAttributeTypes : Map<Symbol, Type>
    }

[<CLIMutable>]
type TraceObject =
    {
        /// Geometry data of the instance.
        Geometry : AdaptiveTraceGeometry

        /// Usage flag of the underlying acceleration structure.
        Usage    : AccelerationStructureUsage

        /// Vertex attributes of each geometry (ignored for AABBs).
        VertexAttributes   : Map<Symbol, BufferView> list

        /// Attributes of the instance.
        InstanceAttributes : Map<Symbol, IAdaptiveValue>

        /// Attributes of each geometry.
        GeometryAttributes : Map<Symbol, IAdaptiveValue> list

        /// The hit groups for each geometry of the instance.
        HitGroups    : aval<HitConfig>

        /// The transformation of the instance.
        Transform    : aval<Trafo3d>

        /// The cull mode of the instance. Only has an effect if TraceRay() is called with one of the cull flags.
        Culling      : aval<CullMode>

        /// Optionally overrides flags set in the geometry.
        GeometryMode : aval<GeometryMode>

        /// Visibility mask that is compared against the mask specified by TraceRay().
        Mask         : aval<VisibilityMask>
    }

[<AutoOpen>]
module TraceObjectFSharp =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TraceObject =

        // TODO: Remove once this is moved to base
        module Utilities =

            [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
            module Map =
                let mapKeys (mapping : 'T -> 'U) (map : Map<'T, 'V>) =
                    map |> Map.toList |> List.map (fun (k, v) -> mapping k, v) |> Map.ofList

            [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
            module List =
                let updateByIndex (index : int) (mapping : 'T -> 'T) (list : 'T list) =
                    list |> List.mapi (fun i v -> if i <> index then v else mapping v)

        open Utilities

        let ofAdaptiveGeometry (geometry : AdaptiveTraceGeometry) =
            { Geometry           = geometry
              Usage              = AccelerationStructureUsage.Static
              VertexAttributes   = Map.empty |> List.replicate geometry.Count
              GeometryAttributes = Map.empty |> List.replicate geometry.Count
              InstanceAttributes = Map.empty
              HitGroups          = AVal.constant []
              Transform          = AVal.constant Trafo3d.Identity
              Culling            = AVal.constant CullMode.Disabled
              GeometryMode       = AVal.constant GeometryMode.Default
              Mask               = AVal.constant VisibilityMask.All }

        let ofGeometry (geometry : TraceGeometry) =
            geometry |> AdaptiveTraceGeometry.constant |> ofAdaptiveGeometry


        let usage (usage : AccelerationStructureUsage) (obj : TraceObject) =
            { obj with Usage = usage }


        let inline vertexAttributes (attributes : Map< ^Name, BufferView> seq) (obj : TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.toList |> List.map (Map.mapKeys conv)
            { obj with VertexAttributes = attributes }

        let inline vertexAttribute (name : ^Name) (values : seq<BufferView>) (obj : TraceObject) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let values = Array.ofSeq values

            (obj, Array.indexed values) ||> Array.fold (fun inst (geometry, value) ->
                { inst with VertexAttributes = inst.VertexAttributes |> List.updateByIndex geometry (Map.add sym value)}
            )


        let inline instanceAttributes (attributes : Map< ^Name, IAdaptiveValue>) (obj : TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Map.toList |> List.map (fun (k, v) -> conv k, v) |> Map.ofList
            { obj with InstanceAttributes = attributes }

        let inline instanceAttribute (name : ^Name) (value : IAdaptiveValue) (obj : TraceObject) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            { obj with InstanceAttributes = obj.InstanceAttributes |> Map.add sym value }

        let inline instanceAttribute' (name : ^Name) (value : 'T) (obj : TraceObject) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            let value = AVal.constant value :> IAdaptiveValue
            { obj with InstanceAttributes = obj.InstanceAttributes |> Map.add sym value }


        let inline geometryAttributes (attributes : Map< ^Name, IAdaptiveValue> seq) (obj : TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.toList |> List.map (Map.mapKeys conv)
            { obj with GeometryAttributes = attributes }

        let inline geometryAttribute (name : ^Name) (values : seq<IAdaptiveValue>) (obj : TraceObject) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let values = Array.ofSeq values

            (obj, Array.indexed values) ||> Array.fold (fun inst (geometry, value) ->
                { inst with GeometryAttributes = inst.GeometryAttributes |> List.updateByIndex geometry (Map.add sym value)}
            )

        let inline geometryAttribute' (name : ^Name) (values : seq<'T>) (obj : TraceObject) =
            let values = values |> Array.ofSeq |> Array.map (fun x -> AVal.constant x :> IAdaptiveValue)
            obj |> geometryAttribute name values


        let hitGroups (hitConfig : aval<HitConfig>) (obj : TraceObject) =
            { obj with HitGroups = hitConfig }

        let hitGroups' (hitConfig : HitConfig) (obj : TraceObject) =
            obj |> hitGroups (AVal.constant hitConfig)

        let hitGroup (group : aval<Symbol>) (obj : TraceObject) =
            let groups = group |> AVal.map List.singleton
            obj  |> hitGroups groups

        let hitGroup' (group : Symbol) (obj : TraceObject) =
            obj |> hitGroups' [group]


        let transform (trafo : aval<Trafo3d>) (obj : TraceObject) =
            { obj with Transform = trafo }

        let transform' (trafo : Trafo3d) (obj : TraceObject) =
            obj |> transform (AVal.constant trafo)


        let culling (mode : aval<CullMode>) (obj : TraceObject) =
            { obj with Culling = mode }

        let culling' (mode : CullMode) (obj : TraceObject) =
            obj |> culling (AVal.constant mode)


        let geometryMode (mode : aval<GeometryMode>) (obj : TraceObject) =
            { obj with GeometryMode = mode }

        let geometryMode' (mode : GeometryMode) (obj : TraceObject) =
            obj |> geometryMode (AVal.constant mode)


        let mask (value : aval<VisibilityMask>) (obj : TraceObject) =
            { obj with Mask = value }

        let mask' (value : VisibilityMask) (obj : TraceObject) =
            obj |> mask (AVal.constant value)


        let ofIndexedGeometry (flags : aval<GeometryFlags>) (trafo : aval<Trafo3d>) (geometry : IndexedGeometry) =
            let g = geometry.ToNonStripped()

            let attributes =
                g.IndexedAttributes |> SymDict.toMap |> Map.map (fun _ -> BufferView.ofArray)

            let ag =
                TriangleMesh.ofIndexedGeometry g
                |> AdaptiveTriangleMesh.constant
                |> AdaptiveTriangleMesh.transform trafo
                |> AdaptiveTriangleMesh.flags flags
                |> AdaptiveTraceGeometry.ofTriangleMesh

            ofAdaptiveGeometry ag
            |> vertexAttributes [| attributes |]

        let ofIndexedGeometry' (flags : GeometryFlags) (trafo : Trafo3d) (geometry : IndexedGeometry) =
            geometry |> ofIndexedGeometry (AVal.constant flags) (AVal.constant trafo)


[<AutoOpen>]
module TraceObjectBuilder =
    open FSharp.Data.Adaptive.Operators

    type GeometryMustBeSpecified = GeometryMustBeSpecified

    type TraceObjectBuilder() =
        member x.Yield(_) = GeometryMustBeSpecified

        [<CustomOperation("geometry")>]
        member x.Geometry(_ : GeometryMustBeSpecified, geometry : TraceGeometry) =
            TraceObject.ofGeometry geometry

        member x.Geometry(_ : GeometryMustBeSpecified, geometry : AdaptiveTraceGeometry) =
            TraceObject.ofAdaptiveGeometry geometry

        [<CustomOperation("indexedGeometry")>]
        member x.IndexedGeometry(_ : GeometryMustBeSpecified, geometry : IndexedGeometry) =
            TraceObject.ofIndexedGeometry' GeometryFlags.None Trafo3d.Identity geometry

        member x.IndexedGeometry(_ : GeometryMustBeSpecified, (geometry : IndexedGeometry, trafo : Trafo3d)) =
            TraceObject.ofIndexedGeometry' GeometryFlags.None trafo geometry

        member x.IndexedGeometry(_ : GeometryMustBeSpecified, (geometry : IndexedGeometry, trafo : Trafo3d, flags : GeometryFlags)) =
            TraceObject.ofIndexedGeometry' flags trafo geometry

        member x.IndexedGeometry(_ : GeometryMustBeSpecified, (geometry : IndexedGeometry, trafo : aval<Trafo3d>)) =
            TraceObject.ofIndexedGeometry ~~GeometryFlags.None trafo geometry

        member x.IndexedGeometry(_ : GeometryMustBeSpecified, (geometry : IndexedGeometry, trafo : aval<Trafo3d>, flags : aval<GeometryFlags>)) =
            TraceObject.ofIndexedGeometry flags trafo geometry

        [<CustomOperation("vertexAttributes")>]
        member x.VertexAttributes(o : TraceObject, attr : Map<Symbol, BufferView> seq) =
            o |> TraceObject.vertexAttributes attr

        member x.VertexAttributes(o : TraceObject, attr : Map<string, BufferView> seq) =
            o |> TraceObject.vertexAttributes attr

        [<CustomOperation("vertexAttribute")>]
        member x.VertexAttribute(o : TraceObject, name : Symbol, values : seq<BufferView>) =
            o |> TraceObject.vertexAttribute name values

        member x.VertexAttribute(o : TraceObject, name : string, values : seq<BufferView>) =
            o |> TraceObject.vertexAttribute name values

        [<CustomOperation("instanceAttributes")>]
        member x.InstanceAttributes(o : TraceObject, attr : Map<Symbol, IAdaptiveValue>) =
            o |> TraceObject.instanceAttributes attr

        member x.InstanceAttributes(o : TraceObject, attr : Map<string, IAdaptiveValue>) =
            o |> TraceObject.instanceAttributes attr

        [<CustomOperation("instanceAttribute")>]
        member x.InstanceAttribute(o : TraceObject, name : Symbol, value : IAdaptiveValue) =
            o |> TraceObject.instanceAttribute name value

        member x.InstanceAttribute(o : TraceObject, name : string, value : IAdaptiveValue) =
            o |> TraceObject.instanceAttribute name value

        member x.InstanceAttribute(o : TraceObject, name : Symbol, value : 'T) =
            o |> TraceObject.instanceAttribute' name value

        member x.InstanceAttribute(o : TraceObject, name : string, value : 'T) =
            o |> TraceObject.instanceAttribute' name value

        [<CustomOperation("geometryAttributes")>]
        member x.GeometryAttributes(o : TraceObject, attr : Map<Symbol, IAdaptiveValue> seq) =
            o |> TraceObject.geometryAttributes attr

        member x.GeometryAttributes(o : TraceObject, attr : Map<string, IAdaptiveValue> seq) =
            o |> TraceObject.geometryAttributes attr

        [<CustomOperation("geometryAttribute")>]
        member x.GeometryAttribute(o : TraceObject, name : Symbol, values : seq<IAdaptiveValue>) =
            o |> TraceObject.geometryAttribute name values

        member x.GeometryAttribute(o : TraceObject, name : string, values : seq<IAdaptiveValue>) =
            o |> TraceObject.geometryAttribute name values

        member x.GeometryAttribute(o : TraceObject, name : Symbol, values : seq<'T>) =
            o |> TraceObject.geometryAttribute' name values

        member x.GeometryAttribute(o : TraceObject, name : string, values : seq<'T>) =
            o |> TraceObject.geometryAttribute' name values

        [<CustomOperation("hitGroups")>]
        member x.HitGroups(o : TraceObject, g : aval<HitConfig>) =
            o |> TraceObject.hitGroups g

        member x.HitGroups(o : TraceObject, g : HitConfig) =
            o |> TraceObject.hitGroups' g

        [<CustomOperation("hitGroup")>]
        member x.HitGroup(o : TraceObject, g : aval<Symbol>) =
            o |> TraceObject.hitGroup g

        member x.HitGroup(o : TraceObject, g : Symbol) =
            o |> TraceObject.hitGroup' g

        [<CustomOperation("transform")>]
        member x.Transform(o : TraceObject, t : aval<Trafo3d>) =
            o |> TraceObject.transform t

        member x.Transform(o : TraceObject, t : Trafo3d) =
            o |> TraceObject.transform' t

        [<CustomOperation("culling")>]
        member x.Culling(o : TraceObject, m : aval<CullMode>) =
            o |> TraceObject.culling m

        member x.Culling(o : TraceObject, m : CullMode) =
            o |> TraceObject.culling' m

        [<CustomOperation("geometryMode")>]
        member x.GeometryMode(o : TraceObject, m : aval<GeometryMode>) =
            o |> TraceObject.geometryMode m

        member x.GeometryMode(o : TraceObject, m : GeometryMode) =
            o |> TraceObject.geometryMode' m

        [<CustomOperation("mask")>]
        member x.Mask(o : TraceObject, m : aval<VisibilityMask>) =
            o |> TraceObject.mask m

        member x.Mask(o : TraceObject, m : aval<int8>) =
            o |> TraceObject.mask (m |> AVal.mapNonAdaptive VisibilityMask)

        member x.Mask(o : TraceObject, m : aval<uint8>) =
            o |> TraceObject.mask (m |> AVal.mapNonAdaptive VisibilityMask)

        member x.Mask(o : TraceObject, m : aval<int32>) =
            o |> TraceObject.mask (m |> AVal.mapNonAdaptive VisibilityMask)

        member x.Mask(o : TraceObject, m : aval<uint32>) =
            o |> TraceObject.mask (m |> AVal.mapNonAdaptive VisibilityMask)

        member x.Mask(o : TraceObject, m : VisibilityMask) =
            o |> TraceObject.mask' m

        member x.Mask(o : TraceObject, m : int8) =
            o |> TraceObject.mask' (VisibilityMask m)

        member x.Mask(o : TraceObject, m : uint8) =
            o |> TraceObject.mask' (VisibilityMask m)

        member x.Mask(o : TraceObject, m : int32) =
            o |> TraceObject.mask' (VisibilityMask m)

        member x.Mask(o : TraceObject, m : uint32) =
            o |> TraceObject.mask' (VisibilityMask m)

        member x.Run(h : TraceObject) =
            h

    let traceObject = TraceObjectBuilder()


[<AutoOpen>]
module private ManagedTracePoolUtils =

    type IndexType with
        member x.Type =
            match x with
            | IndexType.UInt16 -> typeof<uint16>
            | _                -> typeof<uint32>

    type IManagedBuffer with
        member x.Add(data : IndexData<aval<IBuffer>>, range : Range1l) =
            let view = BufferView(data.Buffer, data.Type.Type, int data.Offset)
            x.Add(view, range)

type internal TracePoolResources =
    {
        Pool                  : ManagedTracePool
        GeometryPtr           : managedptr
        GeometryAttributePtr  : managedptr
        InstanceAttributePtr  : managedptr
        IndexPtrs             : List<managedptr>
        VertexPtrs            : List<managedptr>
        Disposables           : List<IDisposable>
    }

and ManagedTraceObject internal(index : int, geometry : aval<IAccelerationStructure>, obj : TraceObject, poolResources : TracePoolResources) =

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
        member x.Culling      = obj.Culling
        member x.GeometryMode = obj.GeometryMode
        member x.Mask         = obj.Mask
        member x.CustomIndex  = customIndex

and ManagedTracePool(runtime : IRuntime, signature : TraceObjectSignature,
                     indexBufferStorage : BufferStorage,
                     vertexBufferStorage : Symbol -> BufferStorage,
                     instanceAttributeBufferStorage : Symbol -> BufferStorage,
                     geometryAttributeBufferStorage : Symbol -> BufferStorage,
                     geometryBufferStorage : BufferStorage) =

    static let zero : byte[] = ManagedPool.Zero

    let indexType = signature.IndexType.Type
    let instanceAttributeTypes = signature.InstanceAttributeTypes
    let geometryAttributeTypes = signature.GeometryAttributeTypes

    let indexManager             = LayoutManager<Option<IndexData<aval<IBuffer>>> * int>()
    let vertexManager            = LayoutManager<Map<Symbol, BufferView>>()
    let geometryManager          = LayoutManager<int32[] * int32[] * int32 * int32>()
    let instanceAttributeManager = LayoutManager<Map<Symbol, IAdaptiveValue>>()
    let geometryAttributeManager = LayoutManager<Map<Symbol, IAdaptiveValue>[]>()

    let createManagedBuffer t u s =
        let b = runtime.CreateManagedBuffer(t, u, s)
        b.Acquire()
        b

    let indexBuffer =
        let usage = BufferUsage.ReadWrite ||| BufferUsage.Storage ||| BufferUsage.AccelerationStructure
        createManagedBuffer indexType usage indexBufferStorage

    let vertexBuffers =
        let usage = BufferUsage.ReadWrite ||| BufferUsage.Storage ||| BufferUsage.AccelerationStructure
        signature.VertexAttributeTypes |> Map.map (fun s t ->
            createManagedBuffer t usage (vertexBufferStorage s)
        )

    let instanceAttributeBuffers =
        let usage = BufferUsage.ReadWrite ||| BufferUsage.Storage
        instanceAttributeTypes |> Map.map (fun s t ->
            createManagedBuffer t usage (instanceAttributeBufferStorage s)
        )

    let geometryAttributeBuffers =
        let usage = BufferUsage.ReadWrite ||| BufferUsage.Storage
        geometryAttributeTypes |> Map.map (fun s t ->
            createManagedBuffer t usage (geometryAttributeBufferStorage s)
        )

    let geometryBuffer =
        let usage = BufferUsage.ReadWrite ||| BufferUsage.Storage
        createManagedBuffer typeof<TraceGeometryInfo> usage geometryBufferStorage

    let accelerationStructures =
        Dict<AdaptiveTraceGeometry * AccelerationStructureUsage, aval<IAccelerationStructure>>()

    let objects = HashSet<ManagedTraceObject>()

    let free (obj : ManagedTraceObject) =
        obj.Geometry.Release()
        for d in obj.Resources.Disposables do d.Dispose()

        geometryManager.Free(obj.Resources.GeometryPtr)
        geometryAttributeManager.Free(obj.Resources.GeometryAttributePtr)
        instanceAttributeManager.Free(obj.Resources.InstanceAttributePtr)

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
        for KeyValue(_, b) in instanceAttributeBuffers do b.Clear()
        for KeyValue(_, b) in geometryAttributeBuffers do b.Clear()

    new (runtime : IRuntime, signature : TraceObjectSignature,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] indexBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] vertexBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Host)>] instanceAttributeBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] geometryAttributeBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] geometryBufferStorage : BufferStorage) =
        new ManagedTracePool(
            runtime, signature, indexBufferStorage,
            (fun _ -> vertexBufferStorage),
            (fun _ -> instanceAttributeBufferStorage),
            (fun _ -> geometryAttributeBufferStorage),
            geometryBufferStorage
        )

    member x.Runtime = runtime
    member x.Count = objects.Count

    member internal x.Free(obj : ManagedTraceObject) =
        lock x (fun _ ->
            if objects.Remove(obj) then
                free obj

                if objects.Count = 0 then
                    clear()
        )

    member x.Add(obj : TraceObject) =
        if obj.Geometry.Count = 0 then
            failwithf "[ManagedTracePool] Trace object does not contain any geometry"

        lock x (fun _ ->
            let ds = List()
            let vptrs = List()
            let iptrs = List()

            let geometryCount      = obj.Geometry.Count
            let vertexAttributes   = obj.VertexAttributes |> List.toArray
            let geometryAttributes = obj.GeometryAttributes |> List.toArray
            let instanceAttributes = obj.InstanceAttributes

            // Geometry attributes
            let geometryAttributePtr   = geometryAttributeManager.Alloc(geometryAttributes, geometryCount)
            let geometryAttributeIndex = int geometryAttributePtr.Offset

            for i = 0 to geometryAttributes.Length - 1 do
                let attributes = geometryAttributes.[i]

                for KeyValue(k, _) in geometryAttributeTypes do
                    let target = geometryAttributeBuffers.[k]
                    match attributes |> Map.tryFind k with
                    | Some v -> target.Add(v, geometryAttributeIndex + i) |> ds.Add
                    | None -> target.Set(zero, Range1l.FromMinAndSize(int64 geometryAttributeIndex + int64 i, 0L))

            // Instance attributes
            let instanceAttributePtr   = instanceAttributeManager.Alloc(instanceAttributes, 1)
            let instanceAttributeIndex = int instanceAttributePtr.Offset

            for KeyValue(k, _) in instanceAttributeTypes do
                let target = instanceAttributeBuffers.[k]
                match instanceAttributes |> Map.tryFind k with
                | Some v -> target.Add(v, instanceAttributeIndex) |> ds.Add
                | None -> target.Set(zero, Range1l.FromMinAndSize(int64 instanceAttributeIndex, 0L))

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
                                match Map.tryFind k vertexAttributes with
                                | Some v -> target.Add(v, vertexRange) |> ds.Add
                                | None -> target.Set(zero, vertexRange)

                            vptrs.Add(vertexPtr)
                            int32 vertexPtr.Offset
                        )

                    let indexOffsets =
                        meshes |> Array.mapi (fun i m ->
                            let fvc = int m.Primitives * 3

                            let isNew, indexPtr = indexManager.TryAlloc((m.Indices, fvc), fvc)
                            let indexRange = Range1l.FromMinAndSize(int64 indexPtr.Offset, int64 fvc - 1L)

                            match m.Indices with
                            | Some idx -> indexBuffer.Add(idx, indexRange) |> ds.Add
                            | None ->
                                if isNew then
                                    let conv = PrimitiveValueConverter.getArrayConverter typeof<int> indexType
                                    let data = Array.init fvc id |> conv
                                    indexBuffer.Set(data.UnsafeCoerce<byte>(), indexRange)

                            iptrs.Add(indexPtr)
                            int32 indexPtr.Offset
                        )

                    let geometryKey   = (vertexOffsets, indexOffsets, instanceAttributeIndex, geometryAttributeIndex)
                    let geometryPtr   = geometryManager.Alloc(geometryKey, geometryCount)
                    let geometryIndex = int geometryPtr.Offset

                    for i = 0 to meshes.Length - 1 do
                        let info =
                            { FirstIndex             = indexOffsets.[i]
                              BaseVertex             = vertexOffsets.[i]
                              InstanceAttributeIndex = instanceAttributeIndex
                              GeometryAttributeIndex = geometryAttributeIndex + i }

                        geometryBuffer.Set(info, geometryIndex + i)

                    geometryIndex, geometryPtr

                | AdaptiveTraceGeometry.AABBs aabbs ->
                    let geometryKey   = ([||], [||], instanceAttributeIndex, geometryAttributeIndex)
                    let geometryPtr   = geometryManager.Alloc(geometryKey, geometryCount)
                    let geometryIndex = int geometryPtr.Offset

                    for i = 0 to aabbs.Length - 1 do
                        let info =
                            { FirstIndex             = 0
                              BaseVertex             = 0
                              InstanceAttributeIndex = instanceAttributeIndex
                              GeometryAttributeIndex = geometryAttributeIndex + i }

                        geometryBuffer.Set(info, geometryIndex + i)

                    geometryIndex, geometryPtr

            let accel =
                let key = (obj.Geometry, obj.Usage)

                match accelerationStructures.TryGetValue(key) with
                | (true, accel) -> accel
                | _ ->
                    let data = obj.Geometry |> AdaptiveTraceGeometry.toAVal
                    let accel = runtime.CreateAccelerationStructure(data, obj.Usage)
                    accelerationStructures.[key] <- accel
                    accel :> aval<_>

            accel.Acquire()

            let resources =
                {
                    Pool                  = x
                    GeometryPtr           = geometryPtr
                    GeometryAttributePtr  = geometryAttributePtr
                    InstanceAttributePtr  = instanceAttributePtr
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

    member x.TryGetVertexAttribute(semantic : Symbol) =
        vertexBuffers |> Map.tryFind semantic
        |> Option.map AdaptiveResource.cast<IBuffer>

    member x.GetVertexAttribute(semantic : Symbol) =
        match x.TryGetVertexAttribute semantic with
        | Some attr -> attr
        | None _ -> failwithf "[ManagedTracePool] could not find vertex attribute %A" semantic

    member x.TryGetInstanceAttribute(semantic : Symbol) =
        instanceAttributeBuffers |> Map.tryFind semantic
        |> Option.map AdaptiveResource.cast<IBuffer>

    member x.GetInstanceAttribute(semantic : Symbol) =
        match x.TryGetInstanceAttribute semantic with
        | Some attr -> attr
        | None _ -> failwithf "[ManagedTracePool] could not find instance attribute %A" semantic

    member x.TryGetGeometryAttribute(semantic : Symbol) =
        geometryAttributeBuffers |> Map.tryFind semantic
        |> Option.map AdaptiveResource.cast<IBuffer>

    member x.GetGeometryAttribute(semantic : Symbol) =
        match x.TryGetGeometryAttribute semantic with
        | Some attr -> attr
        | None _ -> failwithf "[ManagedTracePool] could not find geometry attribute %A" semantic

    member x.Dispose() =
        lock x (fun _ ->
            clear()

            indexBuffer.Release()
            geometryBuffer.Release()
            for KeyValue(_, b) in vertexBuffers do b.Release()
            for KeyValue(_, b) in instanceAttributeBuffers do b.Release()
            for KeyValue(_, b) in geometryAttributeBuffers do b.Release()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AutoOpen>]
module ManagedTracePoolSceneExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module RaytracingSceneDescription =

        let ofPool (pool : ManagedTracePool) (objects : aset<TraceObject>) =
            let reader = objects.GetReader()
            let deltas = List<SetOperation<ManagedTraceObject>>()

            let mtos = Dict<TraceObject, ManagedTraceObject>()

            let add (o : TraceObject) =
                match mtos.TryGetValue o with
                | (true, _) -> ()
                | _ ->
                    let mto = pool.Add(o)
                    mtos.Add(o, mto)
                    deltas.Add(SetOperation.add mto)

            let rem (o : TraceObject) =
                match mtos.TryRemove o with
                | (true, mto) ->
                    deltas.Add(SetOperation.rem mto)
                    mto.Dispose()
                | _ -> ()

            let objects =
                ASet.custom (fun t _ ->
                    deltas.Clear()

                    let ops = reader.GetChanges(t)

                    for o in ops do
                        match o with
                        | Add(_, value) -> add value
                        | Rem(_, value) -> rem value

                    HashSetDelta.ofSeq deltas
                )

            RaytracingSceneDescription.ofASet objects