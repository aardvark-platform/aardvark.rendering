namespace Aardvark.Rendering

open System
open Aardvark.Base

module private IndexHelpers =

    let addOffset (offset : int) (index : Array) : Array =
        if isNull index then
            null
        else
            match index with
            | :? array<int32> as x -> x |> Array.map ((+) offset) :> Array
            | :? array<int16> as x -> x |> Array.map ((+) (int16 offset)) :> Array
            | :? array<uint32> as x -> x |> Array.map ((+) (uint32 offset)) :> Array
            | :? array<uint16> as x -> x |> Array.map ((+) (uint16 offset)) :> Array
            | _ ->
                raise <| ArgumentException($"Unsupported index type {index.GetType()}.")

    let inline private getIntConverter<'T>() : 'T -> int32 =
        if typeof<'T> = typeof<int32> then unbox<int32>
        elif typeof<'T> = typeof<int16> then unbox<int16> >> int
        elif typeof<'T> = typeof<uint32> then unbox<uint32> >> int
        elif typeof<'T> = typeof<uint16> then unbox<uint16> >> int
        else
            raise <| ArgumentException($"Unsupported index type {typeof<'T>}.")

    let applyIndex (index : Array) (data : Array) =
        if isNull index || isNull data then
            null
        else
            index.Visit
                { new ArrayVisitor<Array>() with
                    member x.Run(index : 'i[]) =
                        let toInt = getIntConverter<'i>()
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
    let faceCount (m : IndexedGeometryMode) (fvc : int) =
        match m with
            | IndexedGeometryMode.PointList -> fvc

            | IndexedGeometryMode.LineStrip -> max 0 (fvc - 1)
            | IndexedGeometryMode.LineList -> fvc / 2
            | IndexedGeometryMode.LineAdjacencyList -> fvc / 4

            | IndexedGeometryMode.TriangleList -> fvc / 3
            | IndexedGeometryMode.TriangleStrip -> max 0 (fvc - 2)
            | IndexedGeometryMode.TriangleAdjacencyList -> fvc / 6

            | IndexedGeometryMode.QuadList -> fvc / 4

            | _ -> 0

    let isAdjacency (m : IndexedGeometryMode) =
        match m with
            | IndexedGeometryMode.LineAdjacencyList
            | IndexedGeometryMode.TriangleAdjacencyList ->
                true
            | _ ->
                false

    let isStrip (m : IndexedGeometryMode) =
        match m with
            | IndexedGeometryMode.LineStrip
            | IndexedGeometryMode.TriangleStrip ->
                true
            | _ ->
                false

type IndexedGeometry =
    class
        val mutable public Mode : IndexedGeometryMode
        val mutable public IndexArray : Array
        val mutable public IndexedAttributes : SymbolDict<Array>
        val mutable public SingleAttributes : SymbolDict<obj>

        member x.IsIndexed = not (isNull x.IndexArray)

        member x.VertexCount =
            if isNull x.IndexedAttributes then 0
            else
                match Seq.tryHead x.IndexedAttributes.Values with
                | Some att -> att.Length
                | _ -> 0

        member x.FaceVertexCount =
            if isNull x.IndexArray then
                x.VertexCount
            else
                x.IndexArray.Length

        member x.FaceCount =
            IndexedGeometryMode.faceCount x.Mode x.FaceVertexCount

        member x.Clone() =
            IndexedGeometry(
                x.Mode,
                x.IndexArray,
                (if isNull x.IndexedAttributes then null else x.IndexedAttributes.Copy()),
                (if isNull x.SingleAttributes then null else x.SingleAttributes.Copy())
            )

        member x.ToIndexed() =
            if isNull x.IndexArray then
                let res = x.Clone()
                let count = x.FaceVertexCount
                res.IndexArray <- Array.init count id
                res
            else
                x

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
                let a = if x.IsIndexed then x else y.ToIndexed()
                let b = if y.IsIndexed then y else x.ToIndexed()
                a.Union b

            else
                let indices =
                    try
                        y.IndexArray
                        |> IndexHelpers.addOffset x.VertexCount
                        |> ArrayHelpers.concat x.IndexArray
                    with
                    | exn ->
                        raise <| ArgumentException($"Invalid indices: {exn.Message}")

                let singleAttributes =
                    if isNull x.SingleAttributes then y.SingleAttributes
                    elif isNull y.SingleAttributes then x.SingleAttributes
                    else
                        let r = SymbolDict<obj>()
                        for (KeyValue(sem, att)) in x.SingleAttributes do r.[sem] <- att
                        for (KeyValue(sem, att)) in y.SingleAttributes do r.[sem] <- att
                        r

                let indexedAttributes =
                    if isNull x.IndexedAttributes then y.IndexedAttributes
                    elif isNull y.IndexedAttributes then x.IndexedAttributes
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

    let inline mode (g : IndexedGeometry) = g.Mode
    let inline indexArray (g : IndexedGeometry) = g.IndexArray
    let inline indexedAttributes (g : IndexedGeometry) = g.IndexedAttributes
    let inline singleAttributes (g : IndexedGeometry) = g.SingleAttributes

    let inline isIndexed (g : IndexedGeometry) = g.IsIndexed
    let inline vertexCount (g : IndexedGeometry) = g.VertexCount
    let inline faceVertexCount (g : IndexedGeometry) = g.FaceVertexCount
    let inline faceCount (g : IndexedGeometry) = g.FaceCount
    let inline clone (g : IndexedGeometry) = g.Clone()
    let inline toIndexed (g : IndexedGeometry) = g.ToIndexed()
    let inline toNonIndexed (g : IndexedGeometry) = g.ToNonIndexed()
    let inline toNonStripped (g : IndexedGeometry) = g.ToNonStripped()
    let inline union (a : IndexedGeometry) (b : IndexedGeometry) = a.Union b