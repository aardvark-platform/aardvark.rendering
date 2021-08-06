namespace Aardvark.SceneGraph.Raytracing

open System
open System.Runtime.InteropServices;
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

#nowarn "9"

[<Struct; StructLayout(LayoutKind.Explicit, Size = 16)>]
type TraceGeometryInfo =
    {
        [<FieldOffset(0)>]  FirstIndex             : int32
        [<FieldOffset(4)>]  BaseVertex             : int32
        [<FieldOffset(8)>]  InstanceAttributeIndex : int32
        [<FieldOffset(12)>] GeometryAttributeIndex : int32
    }

type TraceInstanceSignature =
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
type AdaptiveTraceInstance =
    {
        /// Geometry data of the instance.
        Geometry : AdaptiveTraceGeometry

        /// Vertex attributes of each geometry (ignored for AABBs).
        VertexAttributes   : Map<Symbol, BufferView> list

        /// Attributes of the instance.
        InstanceAttributes : Map<Symbol, IAdaptiveValue>

        /// Attributes of each geometry.
        GeometryAttributes : Map<Symbol, IAdaptiveValue> list

        /// Usage flag of the underlying acceleration structure.
        Usage      : AccelerationStructureUsage
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveTraceInstance =

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
        { Geometry = geometry
          VertexAttributes = Map.empty |> List.replicate geometry.Count
          GeometryAttributes = Map.empty |> List.replicate geometry.Count
          InstanceAttributes = Map.empty
          Usage = AccelerationStructureUsage.Static }

    let ofGeometry (geometry : TraceGeometry) =
        geometry |> AdaptiveTraceGeometry.constant |> ofAdaptiveGeometry

    let inline vertexAttributes (attributes : Map< ^Name, BufferView> seq) (instance : AdaptiveTraceInstance) =
        let conv = Symbol.convert Symbol.Converters.untyped
        let attributes = attributes |> Seq.toList |> List.map (Map.mapKeys conv)
        { instance with VertexAttributes = attributes }

    let inline vertexAttribute (name : ^Name) (values : BufferView[]) (instance : AdaptiveTraceInstance) =
        let sym = name |> Symbol.convert Symbol.Converters.untyped

        (instance, Array.indexed values) ||> Array.fold (fun inst (geometry, value) ->
            { inst with VertexAttributes = inst.VertexAttributes |> List.updateByIndex geometry (Map.add sym value)}
        )


    let inline instanceAttributes (attributes : Map< ^Name, IAdaptiveValue>) (instance : AdaptiveTraceInstance) =
        let conv = Symbol.convert Symbol.Converters.untyped
        let attributes = attributes |> Map.toList |> List.map (fun (k, v) -> conv k, v) |> Map.ofList
        { instance with InstanceAttributes = attributes }

    let inline instanceAttribute (name : ^Name) (value : IAdaptiveValue) (instance : AdaptiveTraceInstance) =
        let sym = name |> Symbol.convert Symbol.Converters.untyped
        { instance with InstanceAttributes = instance.InstanceAttributes |> Map.add sym value }

    let inline instanceAttribute' (name : ^Name) (value : 'T) (instance : AdaptiveTraceInstance) =
        let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
        let value = AVal.constant value :> IAdaptiveValue
        { instance with InstanceAttributes = instance.InstanceAttributes |> Map.add sym value }

    let inline geometryAttributes (attributes : Map< ^Name, IAdaptiveValue> seq) (instance : AdaptiveTraceInstance) =
        let conv = Symbol.convert Symbol.Converters.untyped
        let attributes = attributes |> Seq.toList |> List.map (Map.mapKeys conv)
        { instance with GeometryAttributes = attributes }

    let inline geometryAttribute (name : ^Name) (values : IAdaptiveValue[]) (instance : AdaptiveTraceInstance) =
        let sym = name |> Symbol.convert Symbol.Converters.untyped

        (instance, Array.indexed values) ||> Array.fold (fun inst (geometry, value) ->
            { inst with GeometryAttributes = inst.GeometryAttributes |> List.updateByIndex geometry (Map.add sym value)}
        )

    let inline geometryAttribute' (name : ^Name) (values : 'T[]) (instance : AdaptiveTraceInstance) =
        let values = values |> Array.map (fun x -> AVal.constant x :> IAdaptiveValue)
        instance |> geometryAttribute name values

    let usage (usage : AccelerationStructureUsage) (instance : AdaptiveTraceInstance) =
        { instance with Usage = usage }

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
module private ManagedRaytracingPoolUtils =

    type IndexType with
        member x.Type =
            match x with
            | IndexType.UInt16 -> typeof<uint16>
            | IndexType.UInt32 -> typeof<uint32>

    type IManagedBuffer with
        member x.Add(range : Range1l, data : IndexData<aval<IBuffer>>) =
            let view = BufferView(data.Buffer, data.Type.Type, int data.Offset)
            x.Add(range, view)

type ManagedTraceInstance(index : int, geometry : aval<IAccelerationStructure>, disposable : IDisposable) =

    /// Index indicating position in geometry buffer.
    member x.Index = index

    /// Acceleration structure of the object.
    member x.Geometry = geometry

    member x.Dispose() =
        disposable.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type ManagedRaytracingPool(runtime : IRuntime, signature : TraceInstanceSignature) =

    static let zero : byte[] = ManagedPool.Zero

    let indexType = signature.IndexType.Type
    let instanceAttributeTypes = signature.InstanceAttributeTypes
    let geometryAttributeTypes = signature.GeometryAttributeTypes

    let indexManager             = LayoutManager<Option<IndexData<aval<IBuffer>>> * int>()
    let vertexManager            = LayoutManager<Map<Symbol, BufferView>>()
    let geometryManager          = LayoutManager<int32[] * int32[] * int32 * int32>()
    let instanceAttributeManager = LayoutManager<Map<Symbol, IAdaptiveValue>>()
    let geometryAttributeManager = LayoutManager<Map<Symbol, IAdaptiveValue>[]>()

    let indexBuffer =
        ManagedBuffer.create indexType runtime (BufferUsage.ReadWrite ||| BufferUsage.Storage ||| BufferUsage.AccelerationStructure)

    let vertexBuffers =
        signature.VertexAttributeTypes |> Map.map (fun _ t ->
            ManagedBuffer.create t runtime (BufferUsage.ReadWrite ||| BufferUsage.Storage ||| BufferUsage.AccelerationStructure)
        )

    let instanceAttributeBuffers =
        instanceAttributeTypes |> Map.map (fun _ t ->
            ManagedBuffer.create t runtime (BufferUsage.ReadWrite ||| BufferUsage.Storage)
        )

    let geometryAttributeBuffers =
        geometryAttributeTypes |> Map.map (fun _ t ->
            ManagedBuffer.create t runtime (BufferUsage.ReadWrite ||| BufferUsage.Storage)
        )

    let geometryBuffer =
        ManagedBuffer.create typeof<TraceGeometryInfo> runtime (BufferUsage.ReadWrite ||| BufferUsage.Storage)

    let accelerationStructures =
        Dict<AdaptiveTraceGeometry * AccelerationStructureUsage, aval<IAccelerationStructure>>()

    let mutable count = 0

    let clear() =
        indexBuffer.Clear()
        geometryBuffer.Clear()
        for KeyValue(_, b) in vertexBuffers do b.Clear()
        for KeyValue(_, b) in instanceAttributeBuffers do b.Clear()
        for KeyValue(_, b) in geometryAttributeBuffers do b.Clear()

    member x.Runtime = runtime
    member x.Count = count

    member x.Add(instance : AdaptiveTraceInstance) =
        if instance.Geometry.Count = 0 then
            failwithf "[ManagedRaytracingPool] Instance does not contain any geometry"

        lock x (fun _ ->
            let ds = List()
            let vptrs = List()
            let iptrs = List()

            let geometryCount      = instance.Geometry.Count
            let vertexAttributes   = instance.VertexAttributes |> List.toArray
            let geometryAttributes = instance.GeometryAttributes |> List.toArray
            let instanceAttributes = instance.InstanceAttributes

            // Geometry attributes
            let geometryAttributePtr   = geometryAttributeManager.Alloc(geometryAttributes, geometryCount)
            let geometryAttributeIndex = int geometryAttributePtr.Offset

            for i = 0 to geometryAttributes.Length - 1 do
                let attributes = geometryAttributes.[i]

                for KeyValue(k, _) in geometryAttributeTypes do
                    let target = geometryAttributeBuffers.[k]
                    match attributes|> Map.tryFind k with
                    | Some v -> target.Add(geometryAttributeIndex + i, v) |> ds.Add
                    | None -> target.Set(Range1l.FromMinAndSize(int64 geometryAttributeIndex + int64 i, 0L), zero)

            // Instance attributes
            let instanceAttributePtr   = instanceAttributeManager.Alloc(instanceAttributes, 1)
            let instanceAttributeIndex = int instanceAttributePtr.Offset

            for KeyValue(k, _) in instanceAttributeTypes do
                let target = instanceAttributeBuffers.[k]
                match instanceAttributes |> Map.tryFind k with
                | Some v -> target.Add(instanceAttributeIndex, v) |> ds.Add
                | None -> target.Set(Range1l.FromMinAndSize(int64 instanceAttributeIndex, 0L), zero)

            // Geometry data
            let geometryIndex, geometryPtr =
                match instance.Geometry with
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
                                | Some v -> target.Add(vertexRange, v) |> ds.Add
                                | None -> target.Set(vertexRange, zero)

                            vptrs.Add(vertexPtr)
                            int32 vertexPtr.Offset
                        )

                    let indexOffsets =
                        meshes |> Array.mapi (fun i m ->
                            let fvc = int m.Primitives * 3

                            let isNew, indexPtr = indexManager.TryAlloc((m.Indices, fvc), fvc)
                            let indexRange = Range1l.FromMinAndSize(int64 indexPtr.Offset, int64 fvc - 1L)

                            match m.Indices with
                            | Some idx -> indexBuffer.Add(indexRange, idx) |> ds.Add
                            | None ->
                                if isNew then
                                    let conv = PrimitiveValueConverter.getArrayConverter typeof<int> indexType
                                    let data = Array.init fvc id |> conv
                                    indexBuffer.Set(indexRange, data.UnsafeCoerce<byte>())

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

                        geometryBuffer.Add(geometryIndex + i, AVal.constant info) |> ds.Add

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

                        geometryBuffer.Add(geometryIndex + i, AVal.constant info) |> ds.Add

                    geometryIndex, geometryPtr

            let accel =
                let key = (instance.Geometry, instance.Usage)

                match accelerationStructures.TryGetValue(key) with
                | (true, accel) -> accel
                | _ ->
                    let data = instance.Geometry |> AdaptiveTraceGeometry.toAVal
                    let accel = runtime.CreateAccelerationStructure(data, instance.Usage)
                    accelerationStructures.[key] <- accel
                    accel :> aval<_>

            count <- count + 1

            let disposable =
                { new IDisposable with
                    member __.Dispose() =
                        lock x (fun () ->
                            count <- count - 1
                            if count = 0 then
                                clear()

                            for d in ds do d.Dispose()

                            geometryManager.Free(geometryPtr)
                            geometryAttributeManager.Free(geometryAttributePtr)
                            instanceAttributeManager.Free(instanceAttributePtr)

                            for p in iptrs do indexManager.Free(p)
                            for p in vptrs do vertexManager.Free(p)
                        )
                }

            new ManagedTraceInstance(geometryIndex, accel, disposable)
        )

    /// Buffer of TraceGeometryInfo structs.
    member x.GeometryBuffer =
        geometryBuffer :> aval<IBuffer>

    member x.IndexBuffer =
        indexBuffer :> aval<IBuffer>

    member x.TryGetVertexAttribute(semantic : Symbol) =
        vertexBuffers |> Map.tryFind semantic
        |> Option.map (fun v -> v :> aval<IBuffer>)

    member x.GetVertexAttribute(semantic : Symbol) =
        x.TryGetVertexAttribute semantic |> Option.get

    member x.TryGetInstanceAttribute(semantic : Symbol) =
        instanceAttributeBuffers |> Map.tryFind semantic
        |> Option.map (fun v -> v :> aval<IBuffer>)

    member x.GetInstanceAttribute(semantic : Symbol) =
        x.TryGetInstanceAttribute semantic |> Option.get

    member x.TryGetGeometryAttribute(semantic : Symbol) =
        geometryAttributeBuffers |> Map.tryFind semantic
        |> Option.map (fun v -> v :> aval<IBuffer>)

    member x.GetGeometryAttribute(semantic : Symbol) =
        x.TryGetGeometryAttribute semantic |> Option.get