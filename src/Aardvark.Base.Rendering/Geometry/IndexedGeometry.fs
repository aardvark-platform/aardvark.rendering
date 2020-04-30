namespace Aardvark.Base

open System

module private IndexHelpers =

    let applyIndex (index : Array) (data : Array) =
        
        if isNull index || isNull data then
            null
        else
            index.Visit 
                { new ArrayVisitor<Array>() with
                    member x.Run(index : 'i[]) =
                        let toInt = PrimitiveValueConverter.converter<'i, int>
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

        member x.FaceVertexCount =
            if isNull x.IndexArray then
                if isNull x.IndexedAttributes then
                    0
                else
                    match Seq.tryHead x.IndexedAttributes.Values with
                        | Some att -> att.Length
                        | None -> 0
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
                    res.IndexedAttributes <-
                        x.IndexedAttributes |> SymDict.map (fun name att ->
                            IndexHelpers.applyIndex index att
                        )
                   
                if not (isNull x.SingleAttributes) then     
                    res.SingleAttributes <- x.SingleAttributes.Copy()

                res
             
        member x.ToNonStripped() =
            match x.Mode with
                | IndexedGeometryMode.LineStrip ->
                    let x = x.ToIndexed()
                    x.IndexArray <- IndexHelpers.lineStripToList x.IndexArray
                    x

                | IndexedGeometryMode.TriangleStrip ->
                    let x = x.ToIndexed()
                    x.IndexArray <- IndexHelpers.triangleStripToList x.IndexArray
                    x

                | _ ->
                    x
          

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
    let inline faceVertexCount (g : IndexedGeometry) = g.FaceVertexCount
    let inline faceCount (g : IndexedGeometry) = g.FaceCount
    let inline clone (g : IndexedGeometry) = g.Clone()
    let inline toIndexed (g : IndexedGeometry) = g.ToIndexed()
    let inline toNonIndexed (g : IndexedGeometry) = g.ToNonIndexed()
    let inline toNonStripped (g : IndexedGeometry) = g.ToNonStripped()