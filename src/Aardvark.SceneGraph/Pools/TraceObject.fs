namespace Aardvark.SceneGraph.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Collections.Generic

type TraceObject(geometry: AdaptiveTraceGeometry,
                 usage: AccelerationStructureUsage,
                 vertexAttributes: IDictionary<Symbol, BufferView> seq,
                 faceAttributes: IDictionary<Symbol, BufferView> seq,
                 geometryAttributes: IDictionary<Symbol, IAdaptiveValue> seq,
                 instanceAttributes: IDictionary<Symbol, IAdaptiveValue>,
                 hitGroups: aval<Symbol[]>,
                 transform: aval<Trafo3d>,
                 frontFace: aval<WindingOrder voption>,
                 geometryMode: aval<GeometryMode>,
                 mask: aval<VisibilityMask>) =

    /// Geometry data of the instance.
    member val Geometry           = geometry                       with get, set

    /// Usage flag of the underlying acceleration structure.
    member val Usage              = usage                          with get, set

    /// Vertex attributes of each geometry (ignored for AABBs).
    member val VertexAttributes   = Seq.asArray vertexAttributes   with get, set

    /// Face attributes of each geometry (ignored for AABBs).
    member val FaceAttributes     = Seq.asArray faceAttributes     with get, set

    /// Attributes of each geometry.
    member val GeometryAttributes = Seq.asArray geometryAttributes with get, set

    /// Attributes of the instance.
    member val InstanceAttributes = instanceAttributes             with get, set

    /// The hit groups for each geometry of the instance.
    member val HitGroups          = hitGroups                      with get, set

    /// The transformation of the instance.
    member val Transform          = transform                      with get, set

    /// The winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the instance.
    /// Only has an effect if TraceRay() is called with one of the cull flags.
    member val FrontFace          = frontFace                      with get, set

    /// Optionally overrides flags set in the geometry.
    member val GeometryMode       = geometryMode                   with get, set

    /// Visibility mask that is compared against the mask specified by TraceRay().
    member val Mask               = mask                           with get, set

[<AutoOpen>]
module TraceObjectFSharp =

    [<AutoOpen>]
    module private Utilities =

        let inline (~~~~) (value : 'T) : IAdaptiveValue =
            if typeof<IAdaptiveValue>.IsAssignableFrom typeof<'T> then
                unbox value
            else
                ~~value

        module IDictionary =

            let inline create() =
                Dictionary() :> IDictionary<_, _>

            let inline add (key: 'K) (value: 'V) (dictionary: IDictionary<'K, 'V>) : IDictionary<'K, 'V> =
                match dictionary with
                | :? Map<'K, 'V> as map -> map |> Map.add key value :> IDictionary<_, _>
                | :? MapExt<'K, 'V> as map -> map |> MapExt.add key value :> IDictionary<_, _>
                | _ -> dictionary.[key] <- value; dictionary

            let inline mapKeys (mapping: 'K1 -> 'K2) (dictionary: IDictionary<'K1, 'V>) =
                let mapped = dictionary |> Seq.map (fun (KeyValue(k, v)) -> struct (mapping k, v)) |> Dictionary.ofSeqV
                mapped :> IDictionary<'K2, 'V>

    type TraceObject with

        /// Creates an empty trace object from the given trace geometry.
        static member inline ofAdaptiveGeometry (geometry: AdaptiveTraceGeometry) =
            TraceObject(
                geometry, AccelerationStructureUsage.Static,
                Array.init geometry.Count (ignore >> IDictionary.create),
                Array.init geometry.Count (ignore >> IDictionary.create),
                Array.init geometry.Count (ignore >> IDictionary.create),
                Dictionary(),
                AVal.constant Array.empty,
                AVal.constant Trafo3d.Identity,
                AVal.constant ValueNone,
                AVal.constant GeometryMode.Default,
                AVal.constant VisibilityMask.All
            )

        /// Creates an empty trace object from the given trace geometry.
        static member inline ofGeometry (geometry: TraceGeometry) =
            geometry |> AdaptiveTraceGeometry.FromTraceGeometry |> TraceObject.ofAdaptiveGeometry

        /// Applies the usage mode for the given trace object.
        static member inline usage (usage: AccelerationStructureUsage) (obj : TraceObject) =
            obj.Usage <- usage
            obj

        /// Sets vertex attributes for the given trace object.
        /// The names can be string or Symbol.
        static member inline vertexAttributes (attributes: #IDictionary< ^Name, BufferView> seq) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.asArray |> Array.map (IDictionary.mapKeys conv)
            fun (obj : TraceObject) -> obj.VertexAttributes <- attributes; obj

        /// Sets vertex attributes for the given trace object with a single geometry.
        /// The names can be string or Symbol.
        static member inline vertexAttributes (attributes: IDictionary< ^Name, BufferView>) =
            TraceObject.vertexAttributes [| attributes |]

        /// Sets a vertex attribute for the given trace object.
        /// The name can be a string or Symbol.
        static member inline vertexAttribute (name: ^Name, values: BufferView seq) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let values = Seq.asArray values

            fun (obj : TraceObject) ->
                let n = min obj.Geometry.Count values.Length
                for i = 0 to n - 1 do
                    obj.VertexAttributes.[i] <- obj.VertexAttributes.[i] |> IDictionary.add sym values.[i]
                obj

        /// Sets a vertex attribute for the given trace object with a single geometry.
        /// The name can be a string or Symbol.
        static member inline vertexAttribute (name: ^Name, values: BufferView) =
            TraceObject.vertexAttribute (name, [| values |])

        /// Sets face attributes for the given trace object.
        /// The names can be string or Symbol.
        static member inline faceAttributes (attributes: #IDictionary< ^Name, BufferView> seq) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.asArray |> Array.map (IDictionary.mapKeys conv)
            fun (obj : TraceObject) -> obj.FaceAttributes <- attributes; obj

        /// Sets face attributes for the given trace object with a single geometry.
        /// The names can be string or Symbol.
        static member inline faceAttributes (attributes: IDictionary< ^Name, BufferView>) =
            TraceObject.faceAttributes [| attributes |]

        /// Sets a face attribute for the given trace object.
        /// The name can be a string or Symbol.
        static member inline faceAttribute (name: ^Name, values: BufferView seq) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let values = Seq.asArray values

            fun (obj : TraceObject) ->
                let n = min obj.Geometry.Count values.Length
                for i = 0 to n - 1 do
                    obj.FaceAttributes.[i] <- obj.FaceAttributes.[i] |> IDictionary.add sym values.[i]
                obj

        /// Sets a face attribute for the given trace object with a single geometry.
        /// The name can be a string or Symbol.
        static member inline faceAttribute (name: ^Name, values: BufferView) =
            TraceObject.faceAttribute (name, [| values |])

        /// Sets geometry attributes for the given trace object.
        /// The names can be string or Symbol.
        static member inline geometryAttributes (attributes: #IDictionary< ^Name, IAdaptiveValue> seq) (obj: TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.asArray |> Array.map (IDictionary.mapKeys conv)
            obj.GeometryAttributes <- attributes
            obj

        /// Sets a geometry attribute for the given trace object.
        /// The name can be a string or Symbol, or TypedSymbol<'T>.
        static member inline geometryAttribute (name: ^Name, values: #aval<'T> seq) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            let values = Seq.asArray values

            fun (obj : TraceObject) ->
                let n = min obj.Geometry.Count values.Length
                for i = 0 to n - 1 do
                    obj.GeometryAttributes.[i] <- obj.GeometryAttributes.[i] |> IDictionary.add sym values.[i]
                obj

        /// Sets a geometry attribute for the given trace object.
        /// The name can be a string or Symbol, or TypedSymbol<'T>.
        static member inline geometryAttribute (name : ^Name, values : seq<'T>) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            let values = values |> Seq.asArray |> Array.map (~~)

            fun (obj : TraceObject) ->
                let n = min obj.Geometry.Count values.Length
                for i = 0 to n - 1 do
                    obj.GeometryAttributes.[i] <- obj.GeometryAttributes.[i] |> IDictionary.add sym values.[i]
                obj

        /// Sets instance attributes for the given trace object.
        /// The names can be string or Symbol.
        static member inline instanceAttributes (attributes: IDictionary< ^Name, IAdaptiveValue>) (obj: TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> IDictionary.mapKeys conv
            obj.InstanceAttributes <- attributes
            obj

        /// Sets an instance attribute for the given trace object.
        /// The name can be a string or Symbol, or TypedSymbol<'T>.
        static member inline instanceAttribute (name: ^Name, value: aval<'T>) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            fun (obj : TraceObject) -> obj.InstanceAttributes <- obj.InstanceAttributes |> IDictionary.add sym value; obj

        /// Sets an instance attribute for the given trace object.
        /// The name can be a string or Symbol, or TypedSymbol<'T>.
        static member inline instanceAttribute (name: ^Name, value: 'T) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            fun (obj : TraceObject) -> obj.InstanceAttributes <- obj.InstanceAttributes |> IDictionary.add sym ~~~~value; obj

        /// Sets the hit groups for the given trace object.
        static member inline hitGroups (hitConfig: aval<Symbol[]>) =
            fun (obj : TraceObject) -> obj.HitGroups <- hitConfig; obj

        /// Sets the hit groups for the given trace object.
        static member inline hitGroups (hitConfig: Symbol[]) =
            TraceObject.hitGroups ~~hitConfig

        /// Sets the hit group for the given trace object with a single geometry.
        static member inline hitGroup (group: aval<Symbol>) =
            let groups = group |> AVal.map Array.singleton
            TraceObject.hitGroups groups

        /// Sets the hit group for the given trace object with a single geometry.
        static member inline hitGroup (group: Symbol) =
            TraceObject.hitGroups [| group |]

        /// Sets the transform for the given trace object.
        static member inline transform (trafo: aval<Trafo3d>) =
            fun (obj : TraceObject) -> obj.Transform <- trafo; obj

        /// Sets the transform for the given trace object.
        static member inline transform (trafo: Trafo3d) =
            TraceObject.transform ~~trafo

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front: aval<WindingOrder voption>) =
            fun (obj : TraceObject) -> obj.FrontFace <- front; obj

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front: aval<WindingOrder option>) =
            TraceObject.frontFace (front |> AVal.mapNonAdaptive Option.toValueOption)

        /// Sets the winding order of triangles considered to be front-facing for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front: aval<WindingOrder>) =
            TraceObject.frontFace (front |> AVal.mapNonAdaptive ValueSome)

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front: WindingOrder voption) =
            TraceObject.frontFace ~~front

        /// Sets the winding order of triangles considered to be front-facing, or None if back-face culling is to be disabled for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front: WindingOrder option) =
            TraceObject.frontFace ~~front

        /// Sets the winding order of triangles considered to be front-facing for the given trace object.
        /// Only has an effect if TraceRay() is called with one of the cull flags.
        static member inline frontFace (front: WindingOrder) =
            TraceObject.frontFace (ValueSome front)

        /// Sets the geometry mode for the given trace object.
        static member inline geometryMode (mode: aval<GeometryMode>) =
            fun (obj : TraceObject) -> obj.GeometryMode <- mode; obj

        /// Sets the geometry mode for the given trace object.
        static member inline geometryMode (mode: GeometryMode) =
            TraceObject.geometryMode ~~mode

        /// Sets the visibility mask for the given trace object.
        static member inline mask (value: aval<VisibilityMask>) =
            fun (obj : TraceObject) -> obj.Mask <- value; obj

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
                let geometry = geometry.ToNonStripped()

                let attributes = Dictionary()
                for (KeyValue(sym, arr)) in geometry.IndexedAttributes do
                    attributes.[sym] <- BufferView.ofArray arr

                let mesh = AdaptiveTriangleMesh.FromIndexedGeometry(geometry, trafo, flags)

                [| mesh |]
                |> AdaptiveTraceGeometry.Triangles
                |> TraceObject.ofAdaptiveGeometry
                |> TraceObject.vertexAttributes attributes

        /// Creates a trace object from the given indexed geometry.
        static member inline ofIndexedGeometry (flags : GeometryFlags) =
            fun (trafo : Trafo3d) -> TraceObject.ofIndexedGeometry ~~flags ~~trafo