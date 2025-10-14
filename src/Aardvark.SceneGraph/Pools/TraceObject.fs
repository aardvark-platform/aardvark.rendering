namespace Aardvark.SceneGraph.Raytracing

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Collections.Generic

/// <summary>
/// Represents an instance in a raytracing scene managed with a <see cref="ManagedTracePool"/>.
/// </summary>
type TraceObject private (geometry           : AdaptiveTraceGeometry,
                          usage              : AccelerationStructureUsage,
                          vertexAttributes   : IDictionary<Symbol, BufferView>[],
                          faceAttributes     : IDictionary<Symbol, BufferView>[],
                          geometryAttributes : IDictionary<Symbol, IAdaptiveValue>[],
                          instanceAttributes : IDictionary<Symbol, IAdaptiveValue>,
                          hitGroups          : aval<Symbol[]>,
                          transform          : aval<Trafo3d>,
                          frontFace          : aval<WindingOrder voption>,
                          geometryMode       : aval<GeometryMode>,
                          mask               : aval<VisibilityMask>) =

    /// Geometry data of the instance; may consist of multiple geometries of the same type.
    member val Geometry           = geometry                       with get, set

    /// Usage flags of the underlying acceleration structure.
    member val Usage              = usage                          with get, set

    /// <summary>
    /// Vertex attributes of each geometry; must contain at least <c>Geometry.Count</c> elements.
    /// </summary>
    /// <remarks>
    /// Ignored if <see cref="Geometry"/> consists of axis-aligned bounding boxes.
    /// </remarks>
    member val VertexAttributes   = Seq.asArray vertexAttributes   with get, set

    /// <summary>
    /// Face attributes of each geometry; must contain at least <c>Geometry.Count</c> elements.
    /// </summary>
    /// <remarks>
    /// Ignored if <see cref="Geometry"/> consists of axis-aligned bounding boxes.
    /// </remarks>
    member val FaceAttributes     = Seq.asArray faceAttributes     with get, set

    /// <summary>
    /// Attributes of each geometry; must contain at least <c>Geometry.Count</c> elements.
    /// </summary>
    member val GeometryAttributes = Seq.asArray geometryAttributes with get, set

    /// Attributes of the instance.
    member val InstanceAttributes = instanceAttributes             with get, set

    /// <summary>
    /// Hit group of each geometry; must contain at least <c>Geometry.Count</c> elements.
    /// </summary>
    member val HitGroups          = hitGroups                      with get, set

    /// Transformation of the instance.
    member val Transform          = transform                      with get, set

    /// <summary>
    /// Winding order of triangles considered to be front-facing, or <c>ValueNone</c> if back-face culling is to be disabled for the instance.
    /// </summary>
    /// <remarks>
    /// Only has an effect if <c>TraceRay()</c> is called with one of the cull flags.
    /// </remarks>
    member val FrontFace          = frontFace                      with get, set

    /// Optionally overrides flags set in the geometry.
    member val GeometryMode       = geometryMode                   with get, set

    /// <summary>
    /// Visibility mask that is compared against the mask specified by <c>TraceRay()</c>.
    /// </summary>
    member val Mask               = mask                           with get, set

    /// <summary>
    /// Creates a new <see cref="TraceObject"/> instance.
    /// </summary>
    /// <param name="geometry">Geometry data of the instance; may consist of multiple geometries of the same type.</param>
    /// <param name="usage">Usage flags of the underlying acceleration structure.</param>
    /// <param name="vertexAttributes">Vertex attributes of each geometry; must contain at least <c>Geometry.Count</c> elements (ignored for AABBs).</param>
    /// <param name="faceAttributes">Face attributes of each geometry; must contain at least <c>Geometry.Count</c> elements (ignore for AABBs).</param>
    /// <param name="geometryAttributes">Attributes of each geometry; must contain at least <c>Geometry.Count</c> elements.</param>
    /// <param name="instanceAttributes">Attributes of the instance.</param>
    /// <param name="hitGroups">Hit group of each geometry; must contain at least <c>Geometry.Count</c> elements.</param>
    /// <param name="transform">Transformation of the instance.</param>
    /// <param name="frontFace">Winding order of triangles considered to be front-facing, or <c>ValueNone</c> if back-face culling is to be disabled for the instance.</param>
    /// <param name="geometryMode">Optionally overrides flags set in the geometry.</param>
    /// <param name="mask">Visibility mask that is compared against the mask specified by <c>TraceRay()</c>.</param>
    new (geometry           : AdaptiveTraceGeometry,
         usage              : AccelerationStructureUsage,
         vertexAttributes   : IDictionary<Symbol, BufferView> seq,
         faceAttributes     : IDictionary<Symbol, BufferView> seq,
         geometryAttributes : IDictionary<Symbol, IAdaptiveValue> seq,
         instanceAttributes : IDictionary<Symbol, IAdaptiveValue>,
         hitGroups          : aval<Symbol[]>,
         transform          : aval<Trafo3d>,
         frontFace          : aval<WindingOrder voption>,
         geometryMode       : aval<GeometryMode>,
         mask               : aval<VisibilityMask>) =

        TraceObject(
            geometry, usage,
            Seq.asArray vertexAttributes,
            Seq.asArray faceAttributes,
            Seq.asArray geometryAttributes,
            instanceAttributes, hitGroups,
            transform, frontFace, geometryMode, mask
        )

    /// <summary>
    /// Creates a new <see cref="TraceObject"/> instance for a single geometry.
    /// </summary>
    /// <param name="geometry">Geometry data of the instance; must consist of a single geometry.</param>
    /// <param name="usage">Usage flags of the underlying acceleration structure.</param>
    /// <param name="vertexAttributes">Vertex attributes of the geometry.</param>
    /// <param name="faceAttributes">Face attributes of the geometry.</param>
    /// <param name="instanceAttributes">Attributes of the instance.</param>
    /// <param name="hitGroup">Hit group of the instance.</param>
    /// <param name="transform">Transformation of the instance.</param>
    /// <param name="frontFace">Winding order of triangles considered to be front-facing, or <c>ValueNone</c> if back-face culling is to be disabled for the instance.</param>
    /// <param name="geometryMode">Optionally overrides flags set in the geometry.</param>
    /// <param name="mask">Visibility mask that is compared against the mask specified by <c>TraceRay()</c>.</param>
    new (geometry           : AdaptiveTraceGeometry,
         usage              : AccelerationStructureUsage,
         vertexAttributes   : IDictionary<Symbol, BufferView>,
         faceAttributes     : IDictionary<Symbol, BufferView>,
         instanceAttributes : IDictionary<Symbol, IAdaptiveValue>,
         hitGroup           : aval<Symbol>,
         transform          : aval<Trafo3d>,
         frontFace          : aval<WindingOrder voption>,
         geometryMode       : aval<GeometryMode>,
         mask               : aval<VisibilityMask>) =

        TraceObject(
            geometry, usage,
            [| vertexAttributes |], [| faceAttributes |], [| Dictionary() :> IDictionary<_, _> |], instanceAttributes,
            hitGroup |> AVal.map Array.singleton,
            transform, frontFace, geometryMode, mask
        )

[<AutoOpen>]
module TraceObjectFSharp =

    [<AutoOpen>]
    module private Utilities =

        let inline requireUnmanaged<'T when 'T : unmanaged> = ()

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
                geometry, AccelerationStructureUsage.Static ||| AccelerationStructureUsage.Update,
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

        /// Applies the usage flags for the given trace object.
        static member inline usage (usage: AccelerationStructureUsage) (obj : TraceObject) =
            obj.Usage <- usage
            obj

        /// Creates an empty trace object from the given trace geometry.
        static member inline ofGeometry (geometry: TraceGeometry) =
            geometry
            |> AdaptiveTraceGeometry.FromTraceGeometry
            |> TraceObject.ofAdaptiveGeometry
            |> TraceObject.usage AccelerationStructureUsage.Static

        /// <summary>
        /// Sets vertex attributes for the geometries of the given trace object.
        /// The names can be string or Symbol.
        /// </summary>
        /// <remarks>
        /// Vertex attributes are ignored if the trace object consists of axis-aligned bounding boxes.
        /// </remarks>
        /// <param name="attributes">Vertex attributes for each geometry; must contain an element for each geometry in the trace object.</param>
        static member inline vertexAttributes (attributes: #IDictionary< ^Name, BufferView> seq) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.asArray |> Array.map (IDictionary.mapKeys conv)
            fun (obj : TraceObject) -> obj.VertexAttributes <- attributes; obj

        /// <summary>
        /// Sets vertex attributes for the given trace object with a single geometry.
        /// The names can be string or Symbol.
        /// </summary>
        /// <remarks>
        /// Vertex attributes are ignored if the trace object consists of axis-aligned bounding boxes.
        /// </remarks>
        /// <param name="attributes">Vertex attributes for the geometry.</param>
        static member inline vertexAttributes (attributes: IDictionary< ^Name, BufferView>) =
            TraceObject.vertexAttributes [| attributes |]

        /// <summary>
        /// Sets a vertex attribute for the geometries of the given trace object.
        /// The name can be a string or Symbol.
        /// </summary>
        /// <remarks>
        /// Vertex attributes are ignored if the trace object consists of axis-aligned bounding boxes.
        /// </remarks>
        /// <param name="name">Name of the attribute; can be a string or Symbol.</param>
        /// <param name="values">Vertex attribute values for each geometry; must contain an element for each geometry in the trace object.</param>
        static member inline vertexAttribute (name: ^Name, values: BufferView seq) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let values = Seq.asArray values

            fun (obj : TraceObject) ->
                let n = min obj.Geometry.Count values.Length
                for i = 0 to n - 1 do
                    obj.VertexAttributes.[i] <- obj.VertexAttributes.[i] |> IDictionary.add sym values.[i]
                obj

        /// <summary>
        /// Sets a vertex attribute for the given trace object with a single geometry.
        /// The names can be string or Symbol.
        /// </summary>
        /// <remarks>
        /// Vertex attributes are ignored if the trace object consists of axis-aligned bounding boxes.
        /// </remarks>
        /// <param name="name">Name of the attribute; can be a string or Symbol.</param>
        /// <param name="values">Vertex attribute values for the geometry.</param>
        static member inline vertexAttribute (name: ^Name, values: BufferView) =
            TraceObject.vertexAttribute (name, [| values |])

        /// <summary>
        /// Sets face attributes for the geometries of the given trace object.
        /// The names can be string or Symbol.
        /// </summary>
        /// <remarks>
        /// Face attributes are ignored if the trace object consists of axis-aligned bounding boxes.
        /// </remarks>
        /// <param name="attributes">Face attributes for each geometry; must contain an element for each geometry in the trace object.</param>
        static member inline faceAttributes (attributes: #IDictionary< ^Name, BufferView> seq) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.asArray |> Array.map (IDictionary.mapKeys conv)
            fun (obj : TraceObject) -> obj.FaceAttributes <- attributes; obj

        /// <summary>
        /// Sets face attributes for the given trace object with a single geometry.
        /// The names can be string or Symbol.
        /// </summary>
        /// <remarks>
        /// Face attributes are ignored if the trace object consists of axis-aligned bounding boxes.
        /// </remarks>
        /// <param name="attributes">Face attributes for the geometry.</param>
        static member inline faceAttributes (attributes: IDictionary< ^Name, BufferView>) =
            TraceObject.faceAttributes [| attributes |]

        /// <summary>
        /// Sets a face attribute for the geometries of the given trace object.
        /// The name can be a string or Symbol.
        /// </summary>
        /// <remarks>
        /// Face attributes are ignored if the trace object consists of axis-aligned bounding boxes.
        /// </remarks>
        /// <param name="name">Name of the attribute; can be a string or Symbol.</param>
        /// <param name="values">Face attribute values for each geometry; must contain an element for each geometry in the trace object.</param>
        static member inline faceAttribute (name: ^Name, values: BufferView seq) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let values = Seq.asArray values

            fun (obj : TraceObject) ->
                let n = min obj.Geometry.Count values.Length
                for i = 0 to n - 1 do
                    obj.FaceAttributes.[i] <- obj.FaceAttributes.[i] |> IDictionary.add sym values.[i]
                obj

        /// <summary>
        /// Sets a face attribute for the given trace object with a single geometry.
        /// The names can be string or Symbol.
        /// </summary>
        /// <remarks>
        /// Face attributes are ignored if the trace object consists of axis-aligned bounding boxes.
        /// </remarks>
        /// <param name="name">Name of the attribute; can be a string or Symbol.</param>
        /// <param name="values">Face attribute values for the geometry.</param>
        static member inline faceAttribute (name: ^Name, values: BufferView) =
            TraceObject.faceAttribute (name, [| values |])

        /// <summary>
        /// Sets geometry attributes for the given trace object.
        /// The names can be string or Symbol.
        /// </summary>
        /// <param name="attributes">Attributes for each geometry; must contain an element for each geometry in the trace object.</param>
        /// <param name="obj">Trace object to apply the attributes to.</param>
        static member inline geometryAttributes (attributes: #IDictionary< ^Name, IAdaptiveValue> seq) (obj: TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> Seq.asArray |> Array.map (IDictionary.mapKeys conv)
            obj.GeometryAttributes <- attributes
            obj

        /// <summary>
        /// Sets a geometry attribute for the given trace object.
        /// The name can be a string, Symbol, or TypedSymbol&lt;'T&gt;.
        /// </summary>
        /// <param name="name">Name of the attribute; can be a string, Symbol, or TypedSymbol&lt;'T&gt;.</param>
        /// <param name="values">Attribute values for each geometry; must contain an element for each geometry in the trace object.</param>
        static member inline geometryAttribute (name: ^Name, values: #aval<'T> seq) =
            requireUnmanaged<'T>
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            let values = Seq.asArray values

            fun (obj : TraceObject) ->
                let n = min obj.Geometry.Count values.Length
                for i = 0 to n - 1 do
                    obj.GeometryAttributes.[i] <- obj.GeometryAttributes.[i] |> IDictionary.add sym values.[i]
                obj

        /// <summary>
        /// Sets a geometry attribute for the given trace object.
        /// The name can be a string, Symbol, or TypedSymbol&lt;'T&gt;.
        /// </summary>
        /// <param name="name">Name of the attribute; can be a string, Symbol, or TypedSymbol&lt;'T&gt;.</param>
        /// <param name="values">Attribute values for each geometry; must contain an element for each geometry in the trace object.</param>
        static member inline geometryAttribute (name : ^Name, values : seq<'T>) =
            requireUnmanaged<'T>
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            let values = values |> Seq.asArray |> Array.map (~~)

            fun (obj : TraceObject) ->
                let n = min obj.Geometry.Count values.Length
                for i = 0 to n - 1 do
                    obj.GeometryAttributes.[i] <- obj.GeometryAttributes.[i] |> IDictionary.add sym values.[i]
                obj

        /// <summary>
        /// Sets instance attributes for the given trace object.
        /// The names can be string or Symbol.
        /// </summary>
        /// <param name="attributes">Attributes for the instance.</param>
        /// <param name="obj">Trace object to apply the attributes to.</param>
        static member inline instanceAttributes (attributes: IDictionary< ^Name, IAdaptiveValue>) (obj: TraceObject) =
            let conv = Symbol.convert Symbol.Converters.untyped
            let attributes = attributes |> IDictionary.mapKeys conv
            obj.InstanceAttributes <- attributes
            obj

        /// <summary>
        /// Sets an instance attribute for the given trace object.
        /// The name can be a string, Symbol, or TypedSymbol&lt;'T&gt;.
        /// </summary>
        /// <param name="name">Name of the attribute; can be a string, Symbol, or TypedSymbol&lt;'T&gt;.</param>
        /// <param name="value">Attribute value.</param>
        static member inline instanceAttribute (name: ^Name, value: aval<'T>) =
            requireUnmanaged<'T>
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            fun (obj : TraceObject) -> obj.InstanceAttributes <- obj.InstanceAttributes |> IDictionary.add sym value; obj

        /// <summary>
        /// Sets an instance attribute for the given trace object.
        /// The name can be a string, Symbol, or TypedSymbol&lt;'T&gt;.
        /// </summary>
        /// <param name="name">Name of the attribute; can be a string, Symbol, or TypedSymbol&lt;'T&gt;.</param>
        /// <param name="value">Attribute value.</param>
        static member inline instanceAttribute (name: ^Name, value: 'T) =
            requireUnmanaged<'T>
            let sym = name |> Symbol.convert Symbol.Converters.typed<'T>
            fun (obj : TraceObject) -> obj.InstanceAttributes <- obj.InstanceAttributes |> IDictionary.add sym ~~value; obj

        /// <summary>
        /// Sets the hit groups for the geometries of the given trace object.
        /// </summary>
        /// <param name="groups">Hit group for each geometry; must contain an element for each geometry in the trace object.</param>
        static member inline hitGroups (groups: aval<Symbol[]>) =
            fun (obj : TraceObject) -> obj.HitGroups <- groups; obj

        /// <summary>
        /// Sets the hit groups for the geometries of the given trace object.
        /// </summary>
        /// <param name="groups">Hit group for each geometry must contain an element for each geometry in the trace object.</param>
        static member inline hitGroups (groups: Symbol[]) =
            TraceObject.hitGroups ~~groups

        /// <summary>
        /// Sets the hit group for the given trace object with a single geometry.
        /// </summary>
        /// <param name="group">Hit group for the trace object.</param>
        static member inline hitGroup (group: aval<Symbol>) =
            let groups = group |> AVal.map Array.singleton
            TraceObject.hitGroups groups

        /// <summary>
        /// Sets the hit group for the given trace object with a single geometry.
        /// </summary>
        /// <param name="group">Hit group for the trace object.</param>
        static member inline hitGroup (group: Symbol) =
            TraceObject.hitGroups [| group |]

        /// Sets the instance transform for the given trace object.
        static member inline transform (trafo: aval<Trafo3d>) =
            fun (obj : TraceObject) -> obj.Transform <- trafo; obj

        /// Sets the instance transform for the given trace object.
        static member inline transform (trafo: Trafo3d) =
            TraceObject.transform ~~trafo

        /// <summary>
        /// Sets the winding order of triangles considered to be front-facing, or <c>ValueNone</c> if back-face culling is to be disabled for the given trace object.
        /// </summary>
        /// <remarks>
        /// Only has an effect if <c>TraceRay()</c> is called with one of the cull flags.
        /// </remarks>
        static member inline frontFace (front: aval<WindingOrder voption>) =
            fun (obj : TraceObject) -> obj.FrontFace <- front; obj

        /// <summary>
        /// Sets the winding order of triangles considered to be front-facing, or <c>None</c> if back-face culling is to be disabled for the given trace object.
        /// </summary>
        /// <remarks>
        /// Only has an effect if <c>TraceRay()</c> is called with one of the cull flags.
        /// </remarks>
        static member inline frontFace (front: aval<WindingOrder option>) =
            TraceObject.frontFace (front |> AVal.mapNonAdaptive Option.toValueOption)

        /// <summary>
        /// Sets the winding order of triangles considered to be front-facing for the given trace object.
        /// </summary>
        /// <remarks>
        /// Only has an effect if <c>TraceRay()</c> is called with one of the cull flags.
        /// </remarks>
        static member inline frontFace (front: aval<WindingOrder>) =
            TraceObject.frontFace (front |> AVal.mapNonAdaptive ValueSome)

        /// <summary>
        /// Sets the winding order of triangles considered to be front-facing, or <c>ValueNone</c> if back-face culling is to be disabled for the given trace object.
        /// </summary>
        /// <remarks>
        /// Only has an effect if <c>TraceRay()</c> is called with one of the cull flags.
        /// </remarks>
        static member inline frontFace (front: WindingOrder voption) =
            TraceObject.frontFace ~~front

        /// <summary>
        /// Sets the winding order of triangles considered to be front-facing, or <c>None</c> if back-face culling is to be disabled for the given trace object.
        /// </summary>
        /// <remarks>
        /// Only has an effect if <c>TraceRay()</c> is called with one of the cull flags.
        /// </remarks>
        static member inline frontFace (front: WindingOrder option) =
            TraceObject.frontFace ~~front

        /// <summary>
        /// Sets the winding order of triangles considered to be front-facing for the given trace object.
        /// </summary>
        /// <remarks>
        /// Only has an effect if <c>TraceRay()</c> is called with one of the cull flags.
        /// </remarks>
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

        /// <summary>
        /// Creates a trace object from the given indexed geometry and micromap.
        /// </summary>
        /// <remarks>
        /// The given transform is applied to the geometry.
        /// Use <see cref="TraceObject.transform"/> to apply an instance transform.
        /// </remarks>
        static member ofIndexedGeometryWithMicromap (flags : aval<GeometryFlags>) =
            fun (trafo : aval<Trafo3d>) (micromap : aval<#IMicromap>) (geometry : IndexedGeometry) ->
                let geometry = geometry.ToNonStripped()

                let attributes = Dictionary()
                for KeyValue(sym, arr) in geometry.IndexedAttributes do
                    attributes.[sym] <- BufferView.ofArray arr

                let mesh = AdaptiveTriangleMesh.FromIndexedGeometry(geometry, trafo, flags, micromap)

                let usage =
                    if trafo.IsConstant then
                        AccelerationStructureUsage.Static
                    else
                        AccelerationStructureUsage.Static ||| AccelerationStructureUsage.Update

                [| mesh |]
                |> AdaptiveTraceGeometry.Triangles
                |> TraceObject.ofAdaptiveGeometry
                |> TraceObject.vertexAttributes attributes
                |> TraceObject.usage usage

        /// <summary>
        /// Creates a trace object from the given indexed geometry.
        /// </summary>
        /// <remarks>
        /// The given transform is applied to the geometry.
        /// Use <see cref="TraceObject.transform"/> to apply an instance transform.
        /// </remarks>
        static member ofIndexedGeometry (flags : aval<GeometryFlags>) =
            fun (trafo : aval<Trafo3d>) (geometry : IndexedGeometry) ->
                TraceObject.ofIndexedGeometryWithMicromap<IMicromap> flags trafo ~~null geometry

        /// <summary>
        /// Creates a trace object from the given indexed geometry and micromap.
        /// </summary>
        /// <remarks>
        /// The given transform is applied to the geometry.
        /// Use <see cref="TraceObject.transform"/> to apply an instance transform.
        /// </remarks>
        static member ofIndexedGeometryWithMicromap (flags : GeometryFlags) =
            fun (trafo : Trafo3d) (micromap : IMicromap) (geometry : IndexedGeometry) ->
                TraceObject.ofIndexedGeometryWithMicromap ~~flags ~~trafo ~~micromap geometry

        /// <summary>
        /// Creates a trace object from the given indexed geometry.
        /// </summary>
        /// <remarks>
        /// The given transform is applied to the geometry.
        /// Use <see cref="TraceObject.transform"/> to apply an instance transform.
        /// </remarks>
        static member inline ofIndexedGeometry (flags : GeometryFlags) =
            fun (trafo : Trafo3d) (geometry : IndexedGeometry) -> TraceObject.ofIndexedGeometry ~~flags ~~trafo geometry