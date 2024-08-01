namespace Aardvark.Rendering

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open Aardvark.Base

[<AutoOpen>]
module private SymDictExtensions =

    type SymbolDict<'T> with
        member inline x.CopyOrNull() =
            if isNull x then null else x.Copy()

module private IndexHelpers =

    [<AutoOpen>]
    module private Operations =
        let private indexOperations =
            let d = Dictionary<Type, obj * obj * obj>()
            d.[typeof<int32>]  <- (id<int32>,     id<int32>,     (fun (x: int32) (y: int32) -> x + y) :> obj)
            d.[typeof<int16>]  <- (int32<int16>,  int16<int32>,  (fun (x: int16) (y: int32) -> x + int16 y) :> obj)
            d.[typeof<uint32>] <- (int32<uint32>, uint32<int32>, (fun (x: uint32) (y: int32) -> x + uint32 y) :> obj)
            d.[typeof<uint16>] <- (int32<uint16>, uint16<int32>, (fun (x: uint16) (y: int32) -> x + uint16 y) :> obj)
            d

        let private getIndexOps<'T> : obj * obj * obj =
            match indexOperations.TryGetValue typeof<'T> with
            | (true, ops) -> ops
            | _ ->
                raise <| ArgumentException($"Unsupported index type {typeof<'T>}.")

        let toIntConverter<'T> =
            let f, _, _ = getIndexOps<'T>
            unbox<'T -> int> f

        let ofIntConverter<'T> : int -> 'T =
            let _, f, _ = getIndexOps<'T>
            unbox<int -> 'T> f

        let addIntOp<'T> : 'T -> int -> 'T =
            let _, _, f = getIndexOps<'T>
            unbox<'T -> int -> 'T> f

    let createIndex (indexType: Type) (count: int) =
        let arr = Array.CreateInstance(indexType, count)
        arr.Visit { new ArrayVisitor<Array>() with
            member x.Run(data: 'T[]) =
                let conv = ofIntConverter<'T>
                for i = 0 to count - 1 do
                    data.[i] <- conv i
                arr
        }

    let addOffset (startAt: int) (offset: int) (index: Array) : Array =
        if isNull index then
            null
        else
            index.Visit { new ArrayVisitor<Array>() with
                member x.Run(data: 'T[]) =
                    let addInt = addIntOp<'T>
                    for i = startAt to data.Length - 1 do
                        data.[i] <- addInt data.[i] offset
                    index
            }

    let applyIndex (index : Array) (data : Array) =
        if isNull index || isNull data then
            null
        else
            index.Visit
                { new ArrayVisitor<Array>() with
                    member x.Run(index : 'i[]) =
                        let toInt = toIntConverter<'i>
                        data.Visit
                            { new ArrayVisitor<Array>() with
                                member x.Run(data : 'a[]) =
                                    let l = data.Length
                                    index |> Array.map (fun i -> data.[(toInt i) % l]) :> Array
                            }
                }

    let lineStripToList (data : Array) =
        if isNull data then
            null
        else
            data.Visit
                {
                    new ArrayVisitor<Array>() with
                        member x.Run(index : 'i[]) =
                            let res = Array.zeroCreate (2 * (index.Length - 1))

                            let mutable oi = 0
                            let mutable i0 = index.[0]
                            for i in 1 .. index.Length - 1 do
                                let i1 = index.[i]
                                res.[oi + 0] <- i0
                                res.[oi + 1] <- i1
                                i0 <- i1
                                oi <- oi + 2

                            res :> Array
                }

    let triangleStripToList (data : Array) =
        if isNull data then
            null
        else
            data.Visit
                {
                    new ArrayVisitor<Array>() with
                        member x.Run(index : 'i[]) =
                            let res = Array.zeroCreate (3 * (index.Length - 2))

                            let mutable oi = 0
                            let mutable i0 = index.[0]
                            let mutable i1 = index.[1]
                            for i in 2 .. index.Length - 1 do
                                let i2 = index.[i]

                                res.[oi + 0] <- i0
                                res.[oi + 1] <- i1
                                res.[oi + 2] <- i2

                                // 0 1 2   2 1 3   2 3 4   4 3 5   4 5 6
                                if i &&& 1 = 0 then i0 <- i2
                                else i1 <- i2

                                oi <- oi + 3

                            res :> Array
                }

module private ArrayHelpers =

    let concat (a : Array) (b : Array) =
        if isNull a then b
        elif isNull b then a
        else
            a.Visit (
                { new ArrayVisitor<Array>() with
                    member x.Run(a : 'T[]) =
                        b.Visit (
                            { new ArrayVisitor<Array>() with
                                member x.Run(b : 'U[]) =
                                    if typeof<'T> <> typeof<'U> then
                                        raise <| ArgumentException($"Array element types must match to be concatenated (got {typeof<'T>} and {typeof<'U>}).")
                                    else
                                        Array.concat [a; unbox<'T[]> b]
                            }
                        )
                }
            )


type IndexedGeometryMode =
    | PointList = 0
    | LineStrip = 1
    | LineList = 2
    | TriangleStrip = 3
    | TriangleList = 4
    | TriangleAdjacencyList = 5
    | LineAdjacencyList = 6
    | QuadList = 7

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndexedGeometryMode =
    let faceCount (mode: IndexedGeometryMode) (faceVertexCount: int) =
        match mode with
        | IndexedGeometryMode.PointList -> faceVertexCount

        | IndexedGeometryMode.LineStrip -> max 0 (faceVertexCount - 1)
        | IndexedGeometryMode.LineList -> faceVertexCount / 2
        | IndexedGeometryMode.LineAdjacencyList -> faceVertexCount / 4

        | IndexedGeometryMode.TriangleList -> faceVertexCount / 3
        | IndexedGeometryMode.TriangleStrip -> max 0 (faceVertexCount - 2)
        | IndexedGeometryMode.TriangleAdjacencyList -> faceVertexCount / 6

        | IndexedGeometryMode.QuadList -> faceVertexCount / 4

        | _ -> 0

    let isAdjacency (mode: IndexedGeometryMode) =
        match mode with
        | IndexedGeometryMode.LineAdjacencyList
        | IndexedGeometryMode.TriangleAdjacencyList ->
            true
        | _ ->
            false

    let isStrip (mode: IndexedGeometryMode) =
        match mode with
        | IndexedGeometryMode.LineStrip
        | IndexedGeometryMode.TriangleStrip ->
            true
        | _ ->
            false

type IndexedGeometry =
    class
        /// Primitive topology of the geometry.
        val mutable public Mode : IndexedGeometryMode

        /// Index array (null for non-indexed geometry).
        val mutable public IndexArray : Array

        /// Per-vertex attributes (can be null).
        val mutable public IndexedAttributes : SymbolDict<Array>

        /// Single value attributes (can be null).
        val mutable public SingleAttributes : SymbolDict<obj>

        /// Indicates whether the geometry is indexed.
        member inline x.IsIndexed =
            not (isNull x.IndexArray)

        /// Indicates whether the geometry is valid (i.e. it has a position attribute and all attribute arrays are of sufficient length)
        member inline x.IsValid =
            if isNull x.IndexedAttributes then false
            else
                match x.IndexedAttributes.TryGetValue DefaultSemantic.Positions with
                | (true, positions) -> x.IndexedAttributes |> Seq.forall (fun attr -> attr.Value.Length >= positions.Length)
                | _ -> false

        /// Total number of vertices in the geometry.
        member x.VertexCount =
            if isNull x.IndexedAttributes then 0
            else
                match x.IndexedAttributes.TryGetValue DefaultSemantic.Positions with
                | (true, positions) -> positions.Length
                | _ ->
                    match x.IndexedAttributes |> Seq.tryHead with
                    | Some (KeyValue(_, att)) -> att.Length
                    | _ -> 0

        /// Effective number of vertices in the geometry (i.e. the index count if indexed and the vertex count if non-indexed).
        member inline x.FaceVertexCount =
            if isNull x.IndexArray then
                x.VertexCount
            else
                x.IndexArray.Length

        /// Indicates whether the geometry has a non-zero face vertex count.
        member inline x.IsEmpty =
            x.FaceVertexCount = 0

        /// Number of faces in the geometry.
        member inline x.FaceCount =
            IndexedGeometryMode.faceCount x.Mode x.FaceVertexCount

        ///<summary>Creates a copy.</summary>
        ///<param name="shallowCopy">If true, the index and attribute arrays are reused instead of being copied. Default is true.</param>
        member x.Clone([<Optional; DefaultParameterValue(true)>] shallowCopy: bool) =
            let indices =
                if isNull x.IndexArray || shallowCopy then
                    x.IndexArray
                else
                    x.IndexArray.Copy()

            let indexedAttributes =
                if isNull x.IndexedAttributes then
                    null
                else
                    let d = SymbolDict<Array>(initialCapacity = x.IndexedAttributes.Count)
                    for (KeyValue(sem, attr)) in x.IndexedAttributes do
                        d.[sem] <- if shallowCopy then attr else attr.Copy()
                    d

            let singleAttributes =
                if isNull x.SingleAttributes then null
                else x.SingleAttributes.Copy()

            IndexedGeometry(x.Mode, indices, indexedAttributes, singleAttributes)

        /// Returns an indexed copy of the geometry.
        /// If it is already indexed, it is returned unmodified.
        member x.ToIndexed(indexType: Type) =
            if isNull x.IndexArray then
                let copy = x.Clone()
                copy.IndexArray <- IndexHelpers.createIndex indexType x.FaceVertexCount
                copy
            else
                x

        /// Returns an indexed copy of the geometry.
        /// If it is already indexed, it is returned unmodified.
        member inline x.ToIndexed() =
            x.ToIndexed typeof<int32>

        /// Returns a non-indexed copy of the geometry.
        /// If it is already non-indexed, it is returned unmodified.
        member x.ToNonIndexed() =
            if isNull x.IndexArray then
                x
            else
                let index = x.IndexArray
                let res = IndexedGeometry(Mode = x.Mode)

                if not (isNull x.IndexedAttributes) then
                    res.IndexedAttributes <- SymbolDict<Array>(initialCapacity = x.IndexedAttributes.Count)
                    for (KeyValue(sem, att)) in x.IndexedAttributes do
                        res.IndexedAttributes.[sem] <- IndexHelpers.applyIndex index att

                if not (isNull x.SingleAttributes) then
                    res.SingleAttributes <- x.SingleAttributes.Copy()

                res

        /// Returns a copy of the geometry with a non-stripped primitive topology.
        /// If the topology is not line or triangle strips, the geometry is returned unmodified.
        member x.ToNonStripped() =
            match x.Mode with
            | IndexedGeometryMode.LineStrip ->
                let x = x.ToIndexed()
                x.IndexArray <- IndexHelpers.lineStripToList x.IndexArray
                x.Mode <- IndexedGeometryMode.LineList
                x

            | IndexedGeometryMode.TriangleStrip ->
                let x = x.ToIndexed()
                x.IndexArray <- IndexHelpers.triangleStripToList x.IndexArray
                x.Mode <- IndexedGeometryMode.TriangleList
                x

            | _ ->
                x

        /// Returns a union of the geometry with another.
        /// The geometries must have the same attributes and primitive topology.
        member x.Union(y : IndexedGeometry) =
            if x.Mode <> y.Mode then
                raise <| ArgumentException("IndexedGeometryMode must match.")

            let acceptedTopologies = [
                IndexedGeometryMode.PointList
                IndexedGeometryMode.LineList
                IndexedGeometryMode.TriangleList
                IndexedGeometryMode.QuadList
            ]

            if not <| List.contains x.Mode acceptedTopologies then
                raise <| ArgumentException($"IndexedGeometryMode must be one of {acceptedTopologies}.")

            if x.IsIndexed <> y.IsIndexed then
                let a = if x.IsIndexed then x else x.ToIndexed <| y.IndexArray.GetType().GetElementType()
                let b = if y.IsIndexed then y else y.ToIndexed <| x.IndexArray.GetType().GetElementType()
                a.Union b

            else
                let indices =
                    if x.IsIndexed then
                        try
                            y.IndexArray
                            |> ArrayHelpers.concat x.IndexArray
                            |> IndexHelpers.addOffset x.IndexArray.Length x.VertexCount
                        with
                        | exn ->
                            raise <| ArgumentException($"Invalid indices: {exn.Message}")
                    else
                        null

                let singleAttributes =
                    if isNull x.SingleAttributes then y.SingleAttributes.CopyOrNull()
                    elif isNull y.SingleAttributes then x.SingleAttributes.CopyOrNull()
                    else
                        let r = SymbolDict<obj>()

                        for (KeyValue(sem, a)) in x.SingleAttributes do
                            match y.SingleAttributes.TryGetValue sem with
                            | (true, b) when a <> b ->
                                raise <| ArgumentException($"Conflicting single value attribute {sem}.")
                            | _ ->
                                r.[sem] <- a

                        for (KeyValue(sem, a)) in y.SingleAttributes do
                            match x.SingleAttributes.TryGetValue sem with
                            | (true, b) when a <> b ->
                                raise <| ArgumentException($"Conflicting single value attribute {sem}.")
                            | _ ->
                                r.[sem] <- a

                        r

                let indexedAttributes =
                    if isNull x.IndexedAttributes then y.IndexedAttributes.CopyOrNull()
                    elif isNull y.IndexedAttributes then x.IndexedAttributes.CopyOrNull()
                    else
                        let r = SymbolDict<Array>()

                        for KeyValue(sem, a) in x.IndexedAttributes do
                            match y.IndexedAttributes.TryGetValue(sem) with
                            | (true, b) ->
                                try
                                    r.[sem] <- ArrayHelpers.concat a b
                                with
                                | exn ->
                                    raise <| ArgumentException($"Invalid {sem} attributes: {exn.Message}")
                            | _ -> ()

                        r

                IndexedGeometry (
                    Mode              = x.Mode,
                    IndexArray        = indices,
                    SingleAttributes  = singleAttributes,
                    IndexedAttributes = indexedAttributes
                )

        new() =
            { Mode = IndexedGeometryMode.TriangleList;
              IndexArray = null;
              IndexedAttributes = null;
              SingleAttributes = null }

        new(indexArray, indexedAttributes, singleAttributes) =
            { Mode = IndexedGeometryMode.TriangleList;
              IndexArray = indexArray;
              IndexedAttributes = indexedAttributes;
              SingleAttributes = singleAttributes }

        new(mode, indexArray, indexedAttributes, singleAttributes) =
            { Mode = mode
              IndexArray = indexArray;
              IndexedAttributes = indexedAttributes;
              SingleAttributes = singleAttributes }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndexedGeometry =

    /// Returns the primitive topology of the given geometry.
    let inline mode (g : IndexedGeometry) = g.Mode

    /// Returns the index array of the given geometry (null if non-indexed).
    let inline indexArray (g : IndexedGeometry) = g.IndexArray

    /// Returns the per-vertex attributes of the given geometry (can be null).
    let inline indexedAttributes (g : IndexedGeometry) = g.IndexedAttributes

    /// Returns the single value attributes of the given geometry (can be null).
    let inline singleAttributes (g : IndexedGeometry) = g.SingleAttributes

    /// Returns whether the given geometry has a non-zero face vertex count.
    let inline isEmpty (g : IndexedGeometry) = g.IsEmpty

    /// Returns whether the given geometry is valid (i.e. it has a position attribute and all attribute arrays are of sufficient length)
    let inline isValid (g : IndexedGeometry) = g.IsValid

    /// Returns whether the given geometry is indexed.
    let inline isIndexed (g : IndexedGeometry) = g.IsIndexed

    /// Returns the total number of vertices in the given geometry.
    let inline vertexCount (g : IndexedGeometry) = g.VertexCount

    /// Returns the effective number of vertices in the given geometry (i.e. the index count if indexed and the vertex count if non-indexed).
    let inline faceVertexCount (g : IndexedGeometry) = g.FaceVertexCount

    /// Returns the number of faces in the given geometry.
    let inline faceCount (g : IndexedGeometry) = g.FaceCount

    /// Returns a shallow copy of the given geometry (index and attribute arrays are reused).
    let inline clone (g : IndexedGeometry) = g.Clone()

    /// Returns an indexed copy of the given geometry.
    /// If it is already indexed, it is returned unmodified.
    let inline toIndexed (g : IndexedGeometry) = g.ToIndexed()

    /// Returns a non-indexed copy of the geometry.
    /// If it is already non-indexed, it is returned unmodified.
    let inline toNonIndexed (g : IndexedGeometry) = g.ToNonIndexed()

    /// Returns a copy of the geometry with a non-stripped primitive topology.
    /// If the topology is not line or triangle strips, the geometry is returned unmodified.
    let inline toNonStripped (g : IndexedGeometry) = g.ToNonStripped()

    /// Returns a union of two geometries with another.
    /// The geometries must have the same attributes and primitive topology.
    let inline union (a : IndexedGeometry) (b : IndexedGeometry) = a.Union b