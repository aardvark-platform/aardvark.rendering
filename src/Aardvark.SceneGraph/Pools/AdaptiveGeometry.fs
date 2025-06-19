namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Collections.Generic

type AdaptiveGeometry(faceVertexCount: int, vertexCount: int, indices: BufferView,
                      vertexAttributes: IDictionary<Symbol, BufferView>,
                      instanceAttributes: IDictionary<Symbol, IAdaptiveValue>) =

    /// Effective number of vertices in the geometry (i.e. the index count if indexed and the vertex count if non-indexed).
    member val FaceVertexCount    = faceVertexCount    with get, set

    /// Total number of vertices in the geometry.
    member val VertexCount        = vertexCount        with get, set

    /// Index buffer (null for non-indexed geometry).
    member val Indices            = indices            with get, set

    /// Per-vertex attributes.
    member val VertexAttributes   = vertexAttributes   with get, set

    /// Per-instance attributes.
    member val InstanceAttributes = instanceAttributes with get, set

    /// Indicates whether the geometry is indexed, i.e. there is a valid index buffer.
    member this.IsIndexed =
        not <| obj.ReferenceEquals(this.Indices, null)

    new (faceVertexCount: int, vertexCount: int, indices: BufferView option,
         vertexAttributes: IDictionary<Symbol, BufferView>, instanceAttributes: IDictionary<Symbol, IAdaptiveValue>) =
        let indices = indices |> Option.defaultValue Unchecked.defaultof<_>
        AdaptiveGeometry(faceVertexCount, vertexCount, indices, vertexAttributes, instanceAttributes)

    new (faceVertexCount: int, vertexCount: int, indices: BufferView voption,
         vertexAttributes: IDictionary<Symbol, BufferView>, instanceAttributes: IDictionary<Symbol, IAdaptiveValue>) =
        let indices = indices |> ValueOption.defaultValue Unchecked.defaultof<_>
        AdaptiveGeometry(faceVertexCount, vertexCount, indices, vertexAttributes, instanceAttributes)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveGeometry =

    let ofIndexedGeometry (instanceAttributes: seq<Symbol * IAdaptiveValue>) (geometry: IndexedGeometry) =
        let indexBuffer =
            if geometry.IsIndexed then BufferView.ofArray geometry.IndexArray
            else Unchecked.defaultof<_>

        let vertexAttributes =
            geometry.IndexedAttributes |> SymDict.map (fun _ -> BufferView.ofArray)

        AdaptiveGeometry(
            geometry.FaceVertexCount, geometry.VertexCount,
            indexBuffer, vertexAttributes,
            Dictionary.ofSeq instanceAttributes
        )