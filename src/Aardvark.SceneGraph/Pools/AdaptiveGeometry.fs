namespace Aardvark.SceneGraph

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Collections.Generic

type AdaptiveGeometry =

    /// Effective number of vertices in the geometry (i.e. the index count if indexed and the vertex count if non-indexed).
    val public FaceVertexCount    : int

    /// Total number of vertices in the geometry.
    val public VertexCount        : int

    /// Index buffer (null for non-indexed geometry).
    val public Indices            : BufferView

    /// Per-vertex attributes.
    val public VertexAttributes   : IDictionary<Symbol, BufferView>

    /// Per-instance attributes.
    val public InstanceAttributes : IDictionary<Symbol, IAdaptiveValue>

    /// Indicates whether the geometry is indexed, i.e. there is a valid index buffer.
    member this.IsIndexed =
        not <| obj.ReferenceEquals(this.Indices, null)

    new (faceVertexCount: int, vertexCount: int, indices: BufferView,
         vertexAttributes: IDictionary<Symbol, BufferView>, instanceAttributes: IDictionary<Symbol, IAdaptiveValue>) =
        { FaceVertexCount = faceVertexCount; VertexCount = vertexCount; Indices = indices
          VertexAttributes = vertexAttributes; InstanceAttributes = instanceAttributes }

    new (faceVertexCount: int, vertexCount: int, indices: BufferView option,
         vertexAttributes: IDictionary<Symbol, BufferView>, instanceAttributes: IDictionary<Symbol, IAdaptiveValue>) =
        let indices = indices |> Option.defaultValue Unchecked.defaultof<_>
        AdaptiveGeometry(faceVertexCount, vertexCount, indices, vertexAttributes, instanceAttributes)

    new (faceVertexCount: int, vertexCount: int, indices: BufferView voption,
         vertexAttributes: IDictionary<Symbol, BufferView>, instanceAttributes: IDictionary<Symbol, IAdaptiveValue>) =
        let indices = indices |> ValueOption.defaultValue Unchecked.defaultof<_>
        AdaptiveGeometry(faceVertexCount, vertexCount, indices, vertexAttributes, instanceAttributes)

    new (faceVertexCount: int, vertexCount: int, indices: BufferView,
         vertexAttributes: HashMap<Symbol, BufferView>, instanceAttributes: HashMap<Symbol, IAdaptiveValue>) =
        let vertexAttributes = vertexAttributes |> HashMap.asDictionary
        let instanceAttributes = instanceAttributes |> HashMap.asDictionary
        AdaptiveGeometry(faceVertexCount, vertexCount, indices, vertexAttributes, instanceAttributes)

    new (faceVertexCount: int, vertexCount: int, indices: BufferView option,
         vertexAttributes: HashMap<Symbol, BufferView>, instanceAttributes: HashMap<Symbol, IAdaptiveValue>) =
        let vertexAttributes = vertexAttributes |> HashMap.asDictionary
        let instanceAttributes = instanceAttributes |> HashMap.asDictionary
        AdaptiveGeometry(faceVertexCount, vertexCount, indices, vertexAttributes, instanceAttributes)

    new (faceVertexCount: int, vertexCount: int, indices: BufferView voption,
         vertexAttributes: HashMap<Symbol, BufferView>, instanceAttributes: HashMap<Symbol, IAdaptiveValue>) =
        let vertexAttributes = vertexAttributes |> HashMap.asDictionary
        let instanceAttributes = instanceAttributes |> HashMap.asDictionary
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