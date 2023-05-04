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
        let ofGeometryInstance (input : RaytracingInputTypes.GeometryInstance) =
            let id = input.instanceCustomIndex + input.geometryIndex
            uniform.GeometryInfos.[id]

        [<ReflectedDefinition; Inline>]
        let ofRayIntersection (input : RayIntersectionInput) =
            ofGeometryInstance input.geometry

        [<ReflectedDefinition; Inline>]
        let ofRayHit (input : RayHitInput<'T, 'U>) =
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

[<CLIMutable>]
type TraceObject =
    {
        /// Geometry data of the instance.
        Geometry : AdaptiveTraceGeometry

        /// Usage flag of the underlying acceleration structure.
        Usage    : AccelerationStructureUsage

        /// Vertex attributes of each geometry (ignored for AABBs).
        VertexAttributes   : Map<Symbol, BufferView> list

        /// Face attributes of each geometry (ignored for AABBs).
        FaceAttributes     : Map<Symbol, BufferView> list

        /// Attributes of each geometry.
        GeometryAttributes : Map<Symbol, IAdaptiveValue> list

        /// Attributes of the instance.
        InstanceAttributes : Map<Symbol, IAdaptiveValue>

        /// The hit groups for each geometry of the instance.
        HitGroups    : aval<HitConfig>

        /// The transformation of the instance.
        Transform    : aval<Trafo3d>

        /// The winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the instance.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        FrontFace    : aval<WindingOrder option>

        /// Optionally overrides flags set in the geometry.
        GeometryMode : aval<GeometryMode>

        /// Visibility mask that is compared against the mask specified by TraceRay().
        Mask         : aval<VisibilityMask>
    }

[<AutoOpen>]
module TraceObjectFSharp =
    open FSharp.Data.Adaptive.Operators

    // TODO: Remove once this is moved to base
    module TraceObjectUtilities =

        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Map =
            let mapKeys (mapping : 'T -> 'U) (map : Map<'T, 'V>) =
                if typeof<'T> <> typeof<'U> then
                    map |> Map.toList |> List.map (fun (k, v) -> mapping k, v) |> Map.ofList
                else
                    unbox map

        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module List =
            let updateByIndex (index : int) (mapping : 'T -> 'T) (list : 'T list) =
                list |> List.mapi (fun i v -> if i <> index then v else mapping v)

        let inline (~~~) (value : 'T) : IAdaptiveValue =
            if typeof<IAdaptiveValue>.IsAssignableFrom typeof<'T> then
                unbox value
            else
                ~~value

    open TraceObjectUtilities

    type TraceObject with

        /// Creates an empty trace object from the given adaptive trace geometry.
        static member inline ofAdaptiveGeometry (geometry : AdaptiveTraceGeometry) =
            { Geometry           = geometry
              Usage              = AccelerationStructureUsage.Static
              VertexAttributes   = Map.empty |> List.replicate geometry.Count
              FaceAttributes     = Map.empty |> List.replicate geometry.Count
              GeometryAttributes = Map.empty |> List.replicate geometry.Count
              InstanceAttributes = Map.empty
              HitGroups          = AVal.constant []
              Transform          = AVal.constant Trafo3d.Identity
              FrontFace          = AVal.constant None
              GeometryMode       = AVal.constant GeometryMode.Default
              Mask               = AVal.constant VisibilityMask.All }

        /// Creates an empty trace object from the given trace geometry.
        static member inline ofGeometry (geometry : TraceGeometry) =
            geometry |> AdaptiveTraceGeometry.constant |> TraceObject.ofAdaptiveGeometry

        /// Applies the usage mode for the given trace object.
        static member inline usage (usage : AccelerationStructureUsage) (obj : TraceObject) =
            { obj with Usage = usage }

        /// Sets vertex attributes for the given trace object.
        /// The names can be string or Symbol.
        static member inline vertexAttributes (attributes : Map< ^Name, BufferView> seq) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.toList |> List.map (Map.mapKeys conv)
            fun (obj : TraceObject) -> { obj with VertexAttributes = attributes }

        /// Sets vertex attributes for the given trace object with a single geometry.
        /// The names can be string or Symbol.
        static member inline vertexAttributes (attributes : Map< ^Name, BufferView>) =
            TraceObject.vertexAttributes [attributes]

        /// Sets a vertex attribute for the given trace object.
        /// The name can be a string or Symbol.
        static member inline vertexAttribute (name : ^Name, values : seq<BufferView>) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let values = Array.ofSeq values

            fun (obj : TraceObject) ->
                (obj, Array.indexed values) ||> Array.fold (fun inst (geometry, value) ->
                    { inst with VertexAttributes = inst.VertexAttributes |> List.updateByIndex geometry (Map.add sym value)}
                )

        /// Sets a vertex attribute for the given trace object with a single geometry.
        /// The name can be a string or Symbol.
        static member inline vertexAttribute (name : ^Name, values : BufferView) =
            TraceObject.vertexAttribute (name, [values])

        /// Sets face attributes for the given trace object.
        /// The names can be string or Symbol.
        static member inline faceAttributes (attributes : Map< ^Name, BufferView> seq) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.toList |> List.map (Map.mapKeys conv)
            fun (obj : TraceObject) -> { obj with FaceAttributes = attributes }

        /// Sets face attributes for the given trace object with a single geometry.
        /// The names can be string or Symbol.
        static member inline faceAttributes (attributes : Map< ^Name, BufferView>) =
            TraceObject.faceAttributes [attributes]

        /// Sets a face attribute for the given trace object.
        /// The name can be a string or Symbol.
        static member inline faceAttribute (name : ^Name, values : seq<BufferView>) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let values = Array.ofSeq values

            fun (obj : TraceObject) ->
                (obj, Array.indexed values) ||> Array.fold (fun inst (geometry, value) ->
                    { inst with FaceAttributes = inst.FaceAttributes |> List.updateByIndex geometry (Map.add sym value)}
                )

        /// Sets a face attribute for the given trace object with a single geometry.
        /// The name can be a string or Symbol.
        static member inline faceAttribute (name : ^Name, values : BufferView) =
            TraceObject.faceAttribute (name, [values])

        /// Sets geometry attributes for the given trace object.
        /// The names can be string or Symbol.
        static member inline geometryAttributes (attributes : Map< ^Name, IAdaptiveValue> seq) (obj : TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.toList |> List.map (Map.mapKeys conv)
            { obj with GeometryAttributes = attributes }

        /// Sets a geometry attribute for the given trace object.
        /// The name can be a string or Symbol, or TypedSymbol<'T>.
        static member inline geometryAttribute (name : ^Name, values : aval<'T> seq) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            let values = Array.ofSeq values

            fun (obj : TraceObject) ->
                (obj, Array.indexed values) ||> Array.fold (fun inst (geometry, value) ->
                    { inst with GeometryAttributes = inst.GeometryAttributes |> List.updateByIndex geometry (Map.add sym value)}
                )

        /// Sets a geometry attribute for the given trace object.
        /// The name can be a string or Symbol, or TypedSymbol<'T>.
        static member inline geometryAttribute (name : ^Name, values : seq<'T>) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            let values = values |> Array.ofSeq |> Array.map (~~~)

            fun (obj : TraceObject) ->
                (obj, Array.indexed values) ||> Array.fold (fun inst (geometry, value) ->
                    { inst with GeometryAttributes = inst.GeometryAttributes |> List.updateByIndex geometry (Map.add sym value)}
                )

        /// Sets instance attributes for the given trace object.
        /// The names can be string or Symbol.
        static member inline instanceAttributes (attributes : Map< ^Name, IAdaptiveValue>) (obj : TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Map.toList |> List.map (fun (k, v) -> conv k, v) |> Map.ofList
            { obj with InstanceAttributes = attributes }

        /// Sets an instance attribute for the given trace object.
        /// The name can be a string or Symbol, or TypedSymbol<'T>.
        static member inline instanceAttribute (name : ^Name, value : aval<'T>) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            fun (obj : TraceObject) -> { obj with InstanceAttributes = obj.InstanceAttributes |> Map.add sym value }

        /// Sets an instance attribute for the given trace object.
        /// The name can be a string or Symbol, or TypedSymbol<'T>.
        static member inline instanceAttribute (name : ^Name, value : 'T) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            fun (obj : TraceObject) -> { obj with InstanceAttributes = obj.InstanceAttributes |> Map.add sym ~~~value }

        /// Sets the hit groups for the given trace object.
        static member inline hitGroups (hitConfig : aval<HitConfig>) =
            fun (obj : TraceObject) -> { obj with HitGroups = hitConfig }

        /// Sets the hit groups for the given trace object.
        static member inline hitGroups (hitConfig : HitConfig) =
            TraceObject.hitGroups ~~hitConfig

        /// Sets the hit group for the given trace object with a single geometry.
        static member inline hitGroup (group : aval<Symbol>) =
            let groups = group |> AVal.map List.singleton
            TraceObject.hitGroups groups

        /// Sets the hit group for the given trace object with a single geometry.
        static member inline hitGroup (group : Symbol) =
            TraceObject.hitGroups [group]

        /// Sets the transform for the given trace object.
        static member inline transform (trafo : aval<Trafo3d>) =
            fun (obj : TraceObject) -> { obj with Transform = trafo }

        /// Sets the transform for the given trace object.
        static member inline transform (trafo : Trafo3d) =
            TraceObject.transform ~~trafo

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : aval<WindingOrder option>) =
            fun (obj : TraceObject) -> { obj with FrontFace = front }

        /// Sets the winding order of triangles considered to be front-facing for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : aval<WindingOrder>) =
            TraceObject.frontFace (front |> AVal.mapNonAdaptive Some)

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : WindingOrder option) =
            TraceObject.frontFace ~~front

        /// Sets the winding order of triangles considered to be front-facing for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front : WindingOrder) =
            TraceObject.frontFace (Some front)

        /// Sets the geometry mode for the given trace object.
        static member inline geometryMode (mode : aval<GeometryMode>) =
            fun (obj : TraceObject) -> { obj with GeometryMode = mode }

        /// Sets the geometry mode for the given trace object.
        static member inline geometryMode (mode : GeometryMode) =
            TraceObject.geometryMode ~~mode

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : aval<VisibilityMask>) =
            fun (obj : TraceObject) -> { obj with Mask = value }

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : aval<uint8>) =
            TraceObject.mask (value |> AVal.mapNonAdaptive VisibilityMask)

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : aval<int8>) =
            TraceObject.mask (value |> AVal.mapNonAdaptive VisibilityMask)

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : aval<uint32>) =
            TraceObject.mask (value |> AVal.mapNonAdaptive VisibilityMask)

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : aval<int32>) =
            TraceObject.mask (value |> AVal.mapNonAdaptive VisibilityMask)

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : VisibilityMask) =
            TraceObject.mask (AVal.constant value)

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : uint8) =
            TraceObject.mask (VisibilityMask value)

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : int8) =
            TraceObject.mask (VisibilityMask value)

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : uint32) =
            TraceObject.mask (VisibilityMask value)

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value : int32) =
            TraceObject.mask (VisibilityMask value)

        /// Creates a trace object from the given indexed geometry.
        static member ofIndexedGeometry (flags : aval<GeometryFlags>) =
            fun (trafo : aval<Trafo3d>) (geometry : IndexedGeometry) ->
                let g = geometry.ToNonStripped()

                let attributes =
                    g.IndexedAttributes |> SymDict.toMap |> Map.map (fun _ -> BufferView.ofArray)

                let ag =
                    TriangleMesh.ofIndexedGeometry g
                    |> AdaptiveTriangleMesh.constant
                    |> AdaptiveTriangleMesh.transform trafo
                    |> AdaptiveTriangleMesh.flags flags
                    |> AdaptiveTraceGeometry.ofTriangleMesh

                TraceObject.ofAdaptiveGeometry ag
                |> TraceObject.vertexAttributes [| attributes |]

        /// Creates a trace object from the given indexed geometry.
        static member inline ofIndexedGeometry (flags : GeometryFlags) =
            fun (trafo : Trafo3d) -> TraceObject.ofIndexedGeometry ~~flags ~~trafo


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
        FaceAttributePtrs     : List<managedptr>
        GeometryAttributePtrs : List<managedptr>
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
        member x.FrontFace    = obj.FrontFace
        member x.GeometryMode = obj.GeometryMode
        member x.Mask         = obj.Mask
        member x.CustomIndex  = customIndex

and ManagedTracePool(runtime : IRuntime, signature : TraceObjectSignature,
                     indexBufferStorage : BufferStorage,
                     vertexBufferStorage : Symbol -> BufferStorage,
                     faceAttributeBufferStorage : Symbol -> BufferStorage,
                     geometryAttributeBufferStorage : Symbol -> BufferStorage,
                     instanceAttributeBufferStorage : Symbol -> BufferStorage,
                     geometryBufferStorage : BufferStorage) =

    static let failf fmt =
        Printf.kprintf (fun str ->
            Log.error "[ManagedTracePool] %s" str
            failwith ("[ManagedTracePool] " + str)
        ) fmt

    static let zero : byte[] = ManagedPool.Zero

    let indexType = signature.IndexType.Type
    let instanceAttributeTypes = signature.InstanceAttributeTypes
    let geometryAttributeTypes = signature.GeometryAttributeTypes

    let indexManager             = LayoutManager<Option<IndexData<aval<IBuffer>>> * int>()
    let vertexManager            = LayoutManager<Map<Symbol, BufferView>>()
    let geometryManager          = LayoutManager<int32[] * int32[] * int32[] * int32[] * int32>()
    let faceAttributeManager     = LayoutManager<Map<Symbol, BufferView>>()
    let instanceAttributeManager = LayoutManager<Map<Symbol, IAdaptiveValue>>()
    let geometryAttributeManager = LayoutManager<Map<Symbol, IAdaptiveValue>>()

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

    let faceAttributeBuffers =
        let usage = BufferUsage.ReadWrite ||| BufferUsage.Storage ||| BufferUsage.AccelerationStructure
        signature.FaceAttributeTypes |> Map.map (fun s t ->
            createManagedBuffer t usage (faceAttributeBufferStorage s)
        )

    let geometryAttributeBuffers =
        let usage = BufferUsage.ReadWrite ||| BufferUsage.Storage
        geometryAttributeTypes |> Map.map (fun s t ->
            createManagedBuffer t usage (geometryAttributeBufferStorage s)
        )

    let instanceAttributeBuffers =
        let usage = BufferUsage.ReadWrite ||| BufferUsage.Storage
        instanceAttributeTypes |> Map.map (fun s t ->
            createManagedBuffer t usage (instanceAttributeBufferStorage s)
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

    new (runtime : IRuntime, signature : TraceObjectSignature,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] indexBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] vertexBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] faceAttributeBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] geometryAttributeBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Host)>] instanceAttributeBufferStorage : BufferStorage,
         [<Optional; DefaultParameterValue(BufferStorage.Device)>] geometryBufferStorage : BufferStorage) =
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

    member internal x.Free(obj : ManagedTraceObject) =
        lock x (fun _ ->
            if objects.Remove(obj) then
                free obj

                if objects.Count = 0 then
                    clear()
        )

    member x.Add(obj : TraceObject) =
        if obj.Geometry.Count = 0 then
            failf "trace object does not contain any geometry"

        lock x (fun _ ->
            let ds = List()
            let vptrs = List()
            let iptrs = List()
            let fptrs = List()
            let gptrs = List()

            let geometryCount      = obj.Geometry.Count
            let vertexAttributes   = obj.VertexAttributes |> List.toArray
            let faceAttributes     = obj.FaceAttributes |> List.toArray
            let geometryAttributes = obj.GeometryAttributes |> List.toArray
            let instanceAttributes = obj.InstanceAttributes

            // Geometry attributes
            let geometryAttributeIndices =
                Array.init geometryCount (fun i ->
                    let geometryAttributes = geometryAttributes.[i]
                    let geometryAttributePtr = geometryAttributeManager.Alloc(geometryAttributes, 1)
                    let geometryAttributeIndex = int geometryAttributePtr.Offset

                    for KeyValue(k, _) in geometryAttributeTypes do
                        let target = geometryAttributeBuffers.[k]
                        match geometryAttributes |> Map.tryFind k with
                        | Some v ->
                            try
                                target.Add(v, geometryAttributeIndex) |> ds.Add
                            with
                            | :? PrimitiveValueConverter.InvalidConversionException as exn ->
                                failf "cannot convert geometry attribute '%A' from %A to %A" k exn.Source exn.Target

                        | None ->
                            target.Set(zero, Range1l.FromMinAndSize(int64 geometryAttributeIndex, 0L))

                    gptrs.Add(geometryAttributePtr)
                    int32 geometryAttributePtr.Offset
                )

            // Instance attributes
            let instanceAttributePtr   = instanceAttributeManager.Alloc(instanceAttributes, 1)
            let instanceAttributeIndex = int instanceAttributePtr.Offset

            for KeyValue(k, _) in instanceAttributeTypes do
                let target = instanceAttributeBuffers.[k]
                match instanceAttributes |> Map.tryFind k with
                | Some v ->
                    try
                        target.Add(v, instanceAttributeIndex) |> ds.Add
                    with
                    | :? PrimitiveValueConverter.InvalidConversionException as exn ->
                        failf "cannot convert instance attribute '%A' from %A to %A" k exn.Source exn.Target

                | None ->
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
                                match Map.tryFind k vertexAttributes with
                                | Some v ->
                                    try
                                        target.Add(v, vertexRange) |> ds.Add
                                    with
                                    | :? PrimitiveValueConverter.InvalidConversionException as exn ->
                                        failf "cannot convert vertex attribute '%A' from %A to %A" k exn.Source exn.Target

                                | None ->
                                    target.Set(zero, vertexRange)

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

                    let faceAttributeOffsets =
                        meshes |> Array.mapi (fun i m ->
                            let faceCount = int m.Primitives
                            let faceAttributes = faceAttributes.[i]
                            let faceAttributePtr = faceAttributeManager.Alloc(faceAttributes, faceCount)
                            let faceAttributeRange = Range1l.FromMinAndSize(int64 faceAttributePtr.Offset, int64 faceCount - 1L)

                            for KeyValue(k, _) in signature.FaceAttributeTypes do
                                let target = faceAttributeBuffers.[k]
                                match Map.tryFind k faceAttributes with
                                | Some v ->
                                    try
                                        target.Add(v, faceAttributeRange) |> ds.Add
                                    with
                                    | :? PrimitiveValueConverter.InvalidConversionException as exn ->
                                        failf "cannot convert face attribute '%A' from %A to %A" k exn.Source exn.Target

                                | None ->
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

    member x.TryGetVertexAttribute(semantic : Symbol) =
        vertexBuffers |> Map.tryFind semantic
        |> Option.map AdaptiveResource.cast<IBuffer>

    member x.GetVertexAttribute(semantic : Symbol) =
        match x.TryGetVertexAttribute semantic with
        | Some attr -> attr
        | None _ -> failf "could not find vertex attribute '%A'" semantic

    member x.TryGetFaceAttribute(semantic : Symbol) =
        faceAttributeBuffers |> Map.tryFind semantic
        |> Option.map AdaptiveResource.cast<IBuffer>

    member x.GetFaceAttribute(semantic : Symbol) =
        match x.TryGetFaceAttribute semantic with
        | Some attr -> attr
        | None _ -> failf "could not find face attribute '%A'" semantic

    member x.TryGetGeometryAttribute(semantic : Symbol) =
        geometryAttributeBuffers |> Map.tryFind semantic
        |> Option.map AdaptiveResource.cast<IBuffer>

    member x.GetGeometryAttribute(semantic : Symbol) =
        match x.TryGetGeometryAttribute semantic with
        | Some attr -> attr
        | None _ -> failf "could not find geometry attribute '%A'" semantic

    member x.TryGetInstanceAttribute(semantic : Symbol) =
        instanceAttributeBuffers |> Map.tryFind semantic
        |> Option.map AdaptiveResource.cast<IBuffer>

    member x.GetInstanceAttribute(semantic : Symbol) =
        match x.TryGetInstanceAttribute semantic with
        | Some attr -> attr
        | None _ -> failf "could not find instance attribute '%A'" semantic

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