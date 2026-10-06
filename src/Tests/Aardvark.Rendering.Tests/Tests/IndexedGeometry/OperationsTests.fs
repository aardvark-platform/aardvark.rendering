namespace Aardvark.Rendering.Tests.IndexedGeometry

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Expecto

module Operations =

    module Clone =

        let clone (shallow: bool) () =
            let g = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
            g.SingleAttributes <- SymDict.empty
            let g_pos = g.IndexedAttributes.[DefaultSemantic.Positions]
            let g_idx = g.IndexArray

            let gc = if shallow then g.Clone() else g.Clone(shallowCopy = false)
            let gc_pos = gc.IndexedAttributes.[DefaultSemantic.Positions]
            let gc_idx = gc.IndexArray

            Expect.isFalse (obj.ReferenceEquals(g.SingleAttributes, gc.SingleAttributes)) "Single attributes not copied"
            Expect.isFalse (obj.ReferenceEquals(g.IndexedAttributes, gc.IndexedAttributes)) "Indexed attributes not copied"

            let expect = if shallow then Expect.isTrue else Expect.isFalse
            expect (obj.ReferenceEquals(g_pos, gc_pos)) "Attribute array"
            expect (obj.ReferenceEquals(g_idx, gc_idx)) "Index array"

            Expect.isTrue gc.IsValid "Invalid"

    module Union =

        let indexed() =
            let a = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
            a.IndexArray <- a.IndexArray |> unbox<int32[]> |> Array.map int16
            let b = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
            b.IndexArray <- b.IndexArray |> unbox<int32[]> |> Array.map int16
            let c = IndexedGeometry.union a b

            let expected =
                Array.concat [
                    a.IndexArray |> unbox<int16[]>
                    b.IndexArray |> unbox<int16[]> |> Array.map ((+) (int16 a.VertexCount))
                ]

            Expect.equal (unbox<int16[]> c.IndexArray) expected "Unexpected indices"
            Expect.equal c.FaceVertexCount (a.FaceVertexCount + b.FaceVertexCount) "Unexpected face vertex count"
            Expect.isTrue c.IsValid "Invalid"

        let nonIndexed() =
            let a = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toNonIndexed
            let b = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toNonIndexed
            let c = IndexedGeometry.union a b

            Expect.isNull c.IndexArray "Unexpected index array"
            Expect.equal c.FaceVertexCount (a.FaceVertexCount + b.FaceVertexCount) "Unexpected face vertex count"
            Expect.isTrue c.IsValid "Invalid"

        let inline private nonIndexedAndIndexed (mapIndex: int32 -> 'T) () =
            let a = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toNonIndexed
            let b = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
            b.IndexArray <- b.IndexArray |> unbox<int32[]> |> Array.map mapIndex
            let c = IndexedGeometry.union a b

            let expected =
                Array.concat [
                    Array.init a.FaceVertexCount (id >> mapIndex)
                    b.IndexArray |> unbox<'T[]> |> Array.map ((+) (mapIndex a.VertexCount))
                ]

            Expect.equal (unbox<'T[]> c.IndexArray) expected "Unexpected indices"
            Expect.isTrue c.IsValid "Invalid"

        let nonIndexedAndInt16 = nonIndexedAndIndexed int16
        let nonIndexedAndInt32 = nonIndexedAndIndexed int32

    module private UnionPrefixes =

        let private modes =
            [ IndexedGeometryMode.PointList, 1
              IndexedGeometryMode.LineList, 2
              IndexedGeometryMode.TriangleList, 3
              IndexedGeometryMode.QuadList, 4 ]

        let private indexTypes : (string * Type * (int[] -> Array)) list =
            [ "int16",  typeof<int16>,  fun values -> Array.map int16 values :> Array
              "uint16", typeof<uint16>, fun values -> Array.map uint16 values :> Array
              "int32",  typeof<int32>,  fun values -> Array.map int32 values :> Array
              "uint32", typeof<uint32>, fun values -> Array.map uint32 values :> Array ]

        let private scalar = Symbol.Create "Union scalar"
        let private integer = Symbol.Create "Union integer"
        let private label = Symbol.Create "Union label"
        let private values (array : Array) = Array.init array.Length array.GetValue

        let private create mode count padding seed indexed convert =
            let live i = seed + i
            let token i = if i < count then live i else -seed - i
            let attributes = SymbolDict<Array>()
            attributes.[DefaultSemantic.Positions] <- Array.init count (fun i -> V3f(float32 (live i), float32 i, 1.0f))
            attributes.[DefaultSemantic.Normals] <- Array.init (count + padding) (fun i -> V3f(float32 (token i), 2.0f, 3.0f))
            attributes.[DefaultSemantic.Colors] <- Array.init (count + padding) (fun i -> C4b(byte (abs (token i) % 251), 17uy, 31uy, 255uy))
            attributes.[DefaultSemantic.DiffuseColorCoordinates] <- Array.init (count + padding) (fun i -> V2d(float (token i), 0.5))
            attributes.[scalar] <- Array.init (count + padding) (fun i -> float (token i) + 0.25)
            attributes.[integer] <- Array.init (count + padding) token
            attributes.[label] <- Array.init (count + padding) (fun i -> $"vertex {token i}")
            let indices =
                if indexed then
                    // Repeated and reordered vertices prevent a raw-prefix-only oracle from hiding bad remapping.
                    Array.init count (fun i -> if i % 3 = 0 then count - 1 else i - 1) |> convert
                else null
            let singles = SymbolDict<obj>()
            singles.[DefaultSemantic.Material] <- "same material"
            IndexedGeometry(mode, indices, attributes, singles)

        let private snapshot (source : IndexedGeometry) context =
            let mode, indices = source.Mode, source.IndexArray
            let indexValues = if isNull indices then [||] else values indices
            let attributes, singles = source.IndexedAttributes, source.SingleAttributes
            let arrays = [| for KeyValue(semantic, array) in attributes -> semantic, array, values array |]
            let singleValues = [| for KeyValue(semantic, value) in singles -> semantic, value |]
            fun () ->
                Expect.equal source.Mode mode $"{context}: source topology"
                Expect.isTrue (Object.ReferenceEquals(indices, source.IndexArray)) $"{context}: source index identity"
                if not (isNull indices) then Expect.equal (values indices) indexValues $"{context}: source index contents"
                Expect.isTrue (Object.ReferenceEquals(attributes, source.IndexedAttributes)) $"{context}: source attribute dictionary identity"
                Expect.equal attributes.Count arrays.Length $"{context}: source attribute keys"
                for semantic, array, original in arrays do
                    Expect.isTrue (Object.ReferenceEquals(array, attributes.[semantic])) $"{context}: source {semantic} array identity"
                    Expect.equal (values array) original $"{context}: source {semantic} contents including padding"
                Expect.isTrue (Object.ReferenceEquals(singles, source.SingleAttributes)) $"{context}: source single dictionary identity"
                Expect.equal singles.Count singleValues.Length $"{context}: source single keys"
                for semantic, value in singleValues do Expect.equal singles.[semantic] value $"{context}: source single {semantic}"

        let private union context (a : IndexedGeometry) (b : IndexedGeometry) =
            try a.Union b
            with error -> failtestf "%s: %O" context error

        let private verify context indexType (sources : IndexedGeometry[]) (result : IndexedGeometry) =
            let count = sources |> Array.sumBy (fun source -> source.VertexCount)
            let indexed = sources |> Array.exists (fun source -> source.IsIndexed)
            let mutable offset = 0
            let expectedIndices =
                [| for source in sources do
                       for i in 0 .. source.FaceVertexCount - 1 do
                           let index = if source.IsIndexed then Convert.ToInt32(source.IndexArray.GetValue i) else i
                           yield offset + index
                       offset <- offset + source.VertexCount |]
            Expect.equal result.Mode sources.[0].Mode $"{context}: topology"
            Expect.equal result.VertexCount count $"{context}: live vertex count"
            Expect.equal result.FaceVertexCount expectedIndices.Length $"{context}: face vertex count"
            Expect.equal result.IsIndexed indexed $"{context}: indexed state"
            Expect.isTrue result.IsValid $"{context}: valid union"
            if indexed then
                Expect.equal (result.IndexArray.GetType().GetElementType()) indexType $"{context}: index element type"
                Expect.equal (values result.IndexArray |> Array.map Convert.ToInt32) expectedIndices $"{context}: indices and operand offsets"
            else Expect.isNull result.IndexArray $"{context}: non-indexed union"
            Expect.equal result.IndexedAttributes.Count sources.[0].IndexedAttributes.Count $"{context}: attribute keys"
            let expanded = result.ToNonIndexed()
            Expect.isNull expanded.IndexArray $"{context}: expanded indices"
            Expect.equal expanded.VertexCount expectedIndices.Length $"{context}: expanded vertex count"
            for KeyValue(semantic, first) in sources.[0].IndexedAttributes do
                let expected =
                    [| for source in sources do
                           let array = source.IndexedAttributes.[semantic]
                           for i in 0 .. source.VertexCount - 1 do yield array.GetValue i |]
                let actual = result.IndexedAttributes.[semantic]
                // Inspect effective values before lengths so left-padding failures reveal wrong vertex data.
                Expect.equal (values expanded.IndexedAttributes.[semantic]) (expectedIndices |> Array.map (fun i -> expected.[i])) $"{context}: expanded {semantic} values"
                Expect.equal actual.Length count $"{context}: {semantic} contains exactly the live prefixes"
                Expect.equal (actual.GetType()) (first.GetType()) $"{context}: {semantic} element type"
                Expect.equal (values actual) expected $"{context}: {semantic} raw live values"
                for source in sources do
                    Expect.isFalse (Object.ReferenceEquals(actual, source.IndexedAttributes.[semantic])) $"{context}: {semantic} result must not alias source arrays"
                    Expect.isFalse (Object.ReferenceEquals(result.IndexedAttributes, source.IndexedAttributes)) $"{context}: independent result dictionary"
            Expect.equal result.SingleAttributes.[DefaultSemantic.Material] (box "same material") $"{context}: single attribute"

        let padded() =
            for mode, arity in modes do
                for leftIndexed, rightIndexed in [false, false; true, true; false, true; true, false] do
                    for name, indexType, convert in indexTypes do
                        if leftIndexed || rightIndexed || name = "int32" then
                            for leftCount, rightCount in [2 * arity, 3 * arity; 0, 2 * arity; 2 * arity, 0; 0, 0] do
                                for leftPadding, rightPadding in [2, 0; 0, 3; 2, 3] do
                                    let context = $"{mode}, {name}, indexed={leftIndexed}/{rightIndexed}, counts={leftCount}/{rightCount}, padding={leftPadding}/{rightPadding}"
                                    let left = create mode leftCount leftPadding 100 leftIndexed convert
                                    let right = create mode rightCount rightPadding 200 rightIndexed convert
                                    Expect.isTrue (left.IsValid && right.IsValid) $"{context}: fixtures are valid"
                                    let checkLeft, checkRight = snapshot left context, snapshot right context
                                    verify context indexType [| left; right |] (union context left right)
                                    checkLeft(); checkRight()

        let chained() =
            for mode, arity in modes do
                for indexed in [[| false; false; false |]; [| true; true; true |]; [| true; false; true |]; [| false; true; false |]] do
                    for name, indexType, convert in indexTypes do
                        if Array.exists id indexed || name = "int32" then
                            let pattern = indexed |> Array.map string |> String.concat "/"
                            let context = $"{mode}, {name}, chained indexed={pattern}"
                            let sources = Array.init 3 (fun i -> create mode ((i + 1) * arity) (i + 1) (100 * (i + 1)) indexed.[i] convert)
                            let checks = sources |> Array.map (fun source -> snapshot source context)
                            let first = union context sources.[0] sources.[1]
                            verify context indexType sources.[0..1] first
                            let checkFirst = snapshot first context
                            let result = union context first sources.[2]
                            verify context indexType sources result
                            let rightFirst = union context sources.[1] sources.[2]
                            verify context indexType sources.[1..2] rightFirst
                            verify context indexType sources (union context sources.[0] rightFirst)
                            checkFirst()
                            for check in checks do check()

        let emptyAndExact() =
            for mode, arity in modes do
                for leftIndexed, rightIndexed in [false, false; true, true; false, true; true, false] do
                    for name, indexType, convert in indexTypes do
                        if leftIndexed || rightIndexed || name = "int32" then
                            for leftCount, rightCount in [0, 0; 0, 2 * arity; 2 * arity, 0; 2 * arity, 3 * arity] do
                                let context = $"{mode}, {name}, exact indexed={leftIndexed}/{rightIndexed}, counts={leftCount}/{rightCount}"
                                let left = create mode leftCount 0 100 leftIndexed convert
                                let right = create mode rightCount 0 200 rightIndexed convert
                                let checkLeft, checkRight = snapshot left context, snapshot right context
                                verify context indexType [| left; right |] (union context left right)
                                checkLeft(); checkRight()

        let dictionaryAndSingleControls() =
            let convert values = Array.map int32 values :> Array
            for nullLeft, nullRight in [false, false; true, false; false, true; true, true] do
                let context = $"null dictionaries={nullLeft}/{nullRight}"
                let left = create IndexedGeometryMode.PointList 2 0 100 false convert
                let right = create IndexedGeometryMode.PointList 3 0 200 false convert
                if nullLeft then left.IndexedAttributes <- null; left.SingleAttributes <- null
                if nullRight then right.IndexedAttributes <- null; right.SingleAttributes <- null
                let result = union context left right
                if nullLeft && nullRight then
                    Expect.isNull result.IndexedAttributes $"{context}: null attributes remain null"
                    Expect.isNull result.SingleAttributes $"{context}: null singles remain null"
                elif nullLeft || nullRight then
                    let source = if nullLeft then right else left
                    Expect.isFalse (Object.ReferenceEquals(result.IndexedAttributes, source.IndexedAttributes)) $"{context}: shallow dictionary copy"
                    Expect.isFalse (Object.ReferenceEquals(result.SingleAttributes, source.SingleAttributes)) $"{context}: single dictionary copy"
                    for KeyValue(semantic, array) in source.IndexedAttributes do
                        Expect.isTrue (Object.ReferenceEquals(result.IndexedAttributes.[semantic], array)) $"{context}: passthrough array {semantic}"
                    Expect.equal result.SingleAttributes.[DefaultSemantic.Material] source.SingleAttributes.[DefaultSemantic.Material] $"{context}: passthrough single"
                else verify context typeof<int32> [| left; right |] result

            for missingLeft in [false; true] do
                let context = $"missing attributes left={missingLeft}"
                let left = create IndexedGeometryMode.PointList 2 0 100 false convert
                let right = create IndexedGeometryMode.PointList 3 0 200 false convert
                (if missingLeft then left else right).IndexedAttributes.Remove scalar |> ignore
                let uniqueLeft, uniqueRight = Symbol.Create "Left single", Symbol.Create "Right single"
                left.SingleAttributes.[uniqueLeft] <- 17
                right.SingleAttributes.[uniqueRight] <- 29
                let result = union context left right
                Expect.isFalse (result.IndexedAttributes.ContainsKey scalar) $"{context}: only paired indexed attributes are retained"
                Expect.equal result.SingleAttributes.Count 3 $"{context}: single attributes retain union semantics"
                Expect.equal result.SingleAttributes.[uniqueLeft] (box 17) $"{context}: left single"
                Expect.equal result.SingleAttributes.[uniqueRight] (box 29) $"{context}: right single"
            for nullLeft in [false; true] do
                let context = $"null array left={nullLeft}"
                let a = create IndexedGeometryMode.PointList 2 0 100 false convert
                let b = create IndexedGeometryMode.PointList 3 0 200 false convert
                (if nullLeft then a else b).IndexedAttributes.[scalar] <- null
                let source = if nullLeft then b else a
                let joined = union context a b
                Expect.isTrue (Object.ReferenceEquals(joined.IndexedAttributes.[scalar], source.IndexedAttributes.[scalar])) $"{context}: null-array passthrough"

        let errorControls() =
            let convert values = Array.map int32 values :> Array
            let make() = create IndexedGeometryMode.PointList 2 0 100 false convert
            let argument context expected action =
                let error =
                    try
                        action()
                        failtestf "%s: expected ArgumentException" context
                    with :? ArgumentException as error -> error
                Expect.equal error.Message expected $"{context}: diagnostic"
                Expect.isNull error.InnerException $"{context}: unchanged wrapping"
            let left, right = make(), make()
            right.IndexedAttributes.[scalar] <- [| 1; 2 |]
            argument "attribute element types" $"Invalid {scalar} attributes: Array element types must match to be concatenated (got {typeof<double>} and {typeof<int>})." (fun () -> left.Union right |> ignore)
            right.IndexedAttributes.[scalar] <- [| 1.0; 2.0 |]
            right.SingleAttributes.[DefaultSemantic.Material] <- "different material"
            argument "conflicting single" $"Conflicting single value attribute {DefaultSemantic.Material}." (fun () -> left.Union right |> ignore)
            right.SingleAttributes.[DefaultSemantic.Material] <- "same material"
            left.IndexArray <- [| 0s; 1s |]
            right.IndexArray <- [| 0us; 1us |]
            argument "index element types" $"Invalid indices: Array element types must match to be concatenated (got {typeof<int16>} and {typeof<uint16>})." (fun () -> left.Union right |> ignore)
            left.IndexArray <- [| 0uy; 1uy |]
            right.IndexArray <- [| 0uy; 1uy |]
            argument "unsupported index type" $"Invalid indices: Unsupported index type {typeof<byte>}." (fun () -> left.Union right |> ignore)
            left.IndexArray <- null; right.IndexArray <- null
            right.Mode <- IndexedGeometryMode.LineList
            argument "different topology" "IndexedGeometryMode must match." (fun () -> left.Union right |> ignore)
            for mode in [IndexedGeometryMode.LineStrip; IndexedGeometryMode.TriangleStrip; IndexedGeometryMode.LineAdjacencyList; IndexedGeometryMode.TriangleAdjacencyList] do
                left.Mode <- mode; right.Mode <- mode
                let accepted = [IndexedGeometryMode.PointList; IndexedGeometryMode.LineList; IndexedGeometryMode.TriangleList; IndexedGeometryMode.QuadList]
                argument $"unsupported {mode}" $"IndexedGeometryMode must be one of {accepted}." (fun () -> left.Union right |> ignore)

    module StripConversion =

        let private stripModes =
            [ IndexedGeometryMode.LineStrip; IndexedGeometryMode.TriangleStrip ]

        let private createGeometry (mode : IndexedGeometryMode) (vertexCount : int) (indices : Array) =
            let positions = Array.init vertexCount (fun i -> V3f(float32 i, float32 (i % 3), 0.0f))
            let normals = Array.create vertexCount V3f.ZAxis
            let attributes = SymbolDict<Array>()
            attributes.[DefaultSemantic.Positions] <- positions
            attributes.[DefaultSemantic.Normals] <- normals

            let singleAttributes = SymbolDict<obj>()
            singleAttributes.[DefaultSemantic.Colors] <- C4b.White

            IndexedGeometry(
                Mode = mode,
                IndexArray = indices,
                IndexedAttributes = attributes,
                SingleAttributes = singleAttributes
            )

        let private listMode (mode : IndexedGeometryMode) =
            match mode with
            | IndexedGeometryMode.LineStrip -> IndexedGeometryMode.LineList
            | IndexedGeometryMode.TriangleStrip -> IndexedGeometryMode.TriangleList
            | _ -> failwith $"Unexpected mode {mode}"

        let private toNonStripped context (source : IndexedGeometry) =
            try source.ToNonStripped()
            with error -> failtestf "%s: %O" context error

        let private verifyShallowCopy context (source : IndexedGeometry) (result : IndexedGeometry) =
            Expect.isFalse (obj.ReferenceEquals(source, result)) $"{context}: Strip conversion returned the source geometry"

            let expectDictionaryReference = if source.IsIndexed then Expect.isTrue else Expect.isFalse
            expectDictionaryReference (obj.ReferenceEquals(source.IndexedAttributes, result.IndexedAttributes)) $"{context}: Unexpected indexed attribute dictionary aliasing"
            expectDictionaryReference (obj.ReferenceEquals(source.SingleAttributes, result.SingleAttributes)) $"{context}: Unexpected single attribute dictionary aliasing"

            for KeyValue(semantic, sourceAttribute) in source.IndexedAttributes do
                let resultAttribute = result.IndexedAttributes.[semantic]
                Expect.isTrue (obj.ReferenceEquals(sourceAttribute, resultAttribute)) $"{context}: Attribute array {semantic} was not shallow-copied"

            for KeyValue(semantic, sourceAttribute) in source.SingleAttributes do
                Expect.equal result.SingleAttributes.[semantic] sourceAttribute $"{context}: Single attribute {semantic} was not preserved"

        let private checkIndexed<'T when 'T : equality> (name : string) (convert : int -> 'T) (mode : IndexedGeometryMode) =
            let context = $"{mode}, {name}"
            let sourceValues = [| 9; 2; 7; 4; 6 |]
            let sourceIndices = sourceValues |> Array.map convert
            let originalIndices = Array.copy sourceIndices
            let source = createGeometry mode 10 (sourceIndices :> Array)
            let originalAttributes = source.IndexedAttributes
            let originalSingleAttributes = source.SingleAttributes

            let expected =
                match mode with
                | IndexedGeometryMode.LineStrip ->
                    [| 9; 2; 2; 7; 7; 4; 4; 6 |]
                | IndexedGeometryMode.TriangleStrip ->
                    [| 9; 2; 7; 7; 2; 4; 7; 4; 6 |]
                | _ ->
                    failwith $"Unexpected mode {mode}"
                |> Array.map convert

            let first = toNonStripped context source
            let second = toNonStripped context source

            Expect.equal source.Mode mode $"{context}: Source mode was modified"
            Expect.isTrue (obj.ReferenceEquals(sourceIndices, source.IndexArray)) $"{context}: Source index array was replaced"
            Expect.equal sourceIndices originalIndices $"{context}: Source indices were modified"
            Expect.isTrue (obj.ReferenceEquals(originalAttributes, source.IndexedAttributes)) $"{context}: Source indexed attributes were replaced"
            Expect.isTrue (obj.ReferenceEquals(originalSingleAttributes, source.SingleAttributes)) $"{context}: Source single attributes were replaced"

            for result in [| first; second |] do
                Expect.equal result.Mode (listMode mode) $"{context}: Unexpected output mode"
                Expect.equal (unbox<'T[]> result.IndexArray) expected $"{context}: Unexpected output indices"
                verifyShallowCopy context source result

            Expect.isFalse (obj.ReferenceEquals(first, second)) $"{context}: Repeated conversion reused a geometry result"
            Expect.isFalse (obj.ReferenceEquals(first.IndexArray, second.IndexArray)) $"{context}: Repeated conversion reused an index result"
            Expect.isTrue (obj.ReferenceEquals(first, toNonStripped context first)) $"{context}: Non-strip conversion did not preserve identity"

        let indexedConversion() =
            for mode in stripModes do
                checkIndexed "int16" int16 mode
                checkIndexed "uint16" uint16 mode
                checkIndexed "int32" int32 mode
                checkIndexed "uint32" uint32 mode

        let nonIndexedConversion() =
            for mode in stripModes do
                let context = $"{mode}, non-indexed"
                let source = createGeometry mode 5 null
                let originalAttributes = source.IndexedAttributes
                let originalSingleAttributes = source.SingleAttributes

                let expected =
                    match mode with
                    | IndexedGeometryMode.LineStrip -> [| 0; 1; 1; 2; 2; 3; 3; 4 |]
                    | IndexedGeometryMode.TriangleStrip -> [| 0; 1; 2; 2; 1; 3; 2; 3; 4 |]
                    | _ -> failwith $"Unexpected mode {mode}"

                let first = toNonStripped context source
                let second = toNonStripped context source

                Expect.equal source.Mode mode $"{context}: Source mode was modified"
                Expect.isNull source.IndexArray $"{context}: Source geometry became indexed"
                Expect.isTrue (obj.ReferenceEquals(originalAttributes, source.IndexedAttributes)) $"{context}: Source indexed attributes were replaced"
                Expect.isTrue (obj.ReferenceEquals(originalSingleAttributes, source.SingleAttributes)) $"{context}: Source single attributes were replaced"

                for result in [| first; second |] do
                    Expect.equal result.Mode (listMode mode) $"{context}: Unexpected output mode"
                    Expect.equal (unbox<int32[]> result.IndexArray) expected $"{context}: Unexpected output indices"
                    verifyShallowCopy context source result

                Expect.isFalse (obj.ReferenceEquals(first.IndexArray, second.IndexArray)) $"{context}: Repeated conversion reused an index result"

        let nonStripIdentity() =
            let modes =
                [ IndexedGeometryMode.PointList
                  IndexedGeometryMode.LineList
                  IndexedGeometryMode.TriangleList
                  IndexedGeometryMode.TriangleAdjacencyList
                  IndexedGeometryMode.LineAdjacencyList
                  IndexedGeometryMode.QuadList ]

            for mode in modes do
                let geometry = createGeometry mode 6 ([| 0; 1; 2 |] :> Array)
                Expect.isTrue (obj.ReferenceEquals(geometry, toNonStripped $"{mode}" geometry)) $"Identity was not preserved for {mode}"

        let emptyAndUnderfilled() =
            let indexFactories : (string * Type * (int -> Array)) list =
                [ "int16", typeof<int16>, fun count -> Array.init count int16 :> Array
                  "uint16", typeof<uint16>, fun count -> Array.init count uint16 :> Array
                  "int32", typeof<int32>, fun count -> Array.init count int32 :> Array
                  "uint32", typeof<uint32>, fun count -> Array.init count uint32 :> Array ]

            let cases =
                [ IndexedGeometryMode.LineStrip, [ 0; 1 ]
                  IndexedGeometryMode.TriangleStrip, [ 0; 1; 2 ] ]

            for mode, counts in cases do
                for count in counts do
                    for name, elementType, createIndices in indexFactories do
                        let context = $"{mode}, {name}, count = {count}"
                        let indices = createIndices count
                        let source = createGeometry mode count indices
                        let result = toNonStripped context source

                        Expect.equal result.Mode (listMode mode) $"{context}: Unexpected output mode"
                        Expect.equal result.IndexArray.Length 0 $"{context}: Underfilled strip produced indices"
                        Expect.equal (result.IndexArray.GetType().GetElementType()) elementType $"{context}: Index type changed"
                        Expect.isTrue (obj.ReferenceEquals(indices, source.IndexArray)) $"{context}: Source indices changed"

                    let context = $"{mode}, non-indexed, count = {count}"
                    let source = createGeometry mode count null
                    let result = toNonStripped context source
                    Expect.equal result.Mode (listMode mode) $"{context}: Unexpected non-indexed output mode"
                    Expect.equal (unbox<int32[]> result.IndexArray) Array.empty $"{context}: Underfilled non-indexed strip produced indices"
                    Expect.isNull source.IndexArray $"{context}: Underfilled source became indexed"

        let attributeIsolation() =
            let source = createGeometry IndexedGeometryMode.LineStrip 3 null
            let result = source.ToNonStripped()

            result.IndexedAttributes.Remove DefaultSemantic.Normals |> ignore
            result.SingleAttributes.Remove DefaultSemantic.Colors |> ignore

            Expect.isTrue (source.IndexedAttributes.ContainsKey DefaultSemantic.Normals) "Result modification removed a source indexed attribute"
            Expect.isTrue (source.SingleAttributes.ContainsKey DefaultSemantic.Colors) "Result modification removed a source single attribute"

        let triangleMeshPreservesSource() =
            let indices = [| 5us; 2us; 7us; 4us |]
            let source = createGeometry IndexedGeometryMode.TriangleStrip 8 (indices :> Array)
            let originalAttributes = source.IndexedAttributes
            let originalSingleAttributes = source.SingleAttributes

            let mesh = TriangleMesh.FromIndexedGeometry source

            Expect.equal mesh.Primitives 2u "Unexpected triangle count"
            Expect.equal mesh.Indices.Type IndexType.UInt16 "Unexpected mesh index type"
            Expect.equal source.Mode IndexedGeometryMode.TriangleStrip "Triangle mesh conversion modified source mode"
            Expect.isTrue (obj.ReferenceEquals(indices, source.IndexArray)) "Triangle mesh conversion replaced source indices"
            Expect.equal indices [| 5us; 2us; 7us; 4us |] "Triangle mesh conversion modified source indices"
            Expect.isTrue (obj.ReferenceEquals(originalAttributes, source.IndexedAttributes)) "Triangle mesh conversion replaced indexed attributes"
            Expect.isTrue (obj.ReferenceEquals(originalSingleAttributes, source.SingleAttributes)) "Triangle mesh conversion replaced single attributes"

    let tests (target: TestTarget) =
        [
            "Clone.shallow",                     Clone.clone true
            "Clone.deep",                        Clone.clone false

            "Union.indexed",                     Union.indexed
            "Union.non-indexed",                 Union.nonIndexed
            "Union.non-indexed & int16-indexed", Union.nonIndexedAndInt16
            "Union.non-indexed & int32-indexed", Union.nonIndexedAndInt32

            "Union.Padded live attribute prefixes",        UnionPrefixes.padded
            "Union.Chained live attribute prefixes",       UnionPrefixes.chained
            "Union.Empty and exact-length attributes",     UnionPrefixes.emptyAndExact
            "Union.Dictionary and single-value controls",  UnionPrefixes.dictionaryAndSingleControls
            "Union.Type and topology error controls",      UnionPrefixes.errorControls

            "Strip conversion.Indexed conversion",               StripConversion.indexedConversion
            "Strip conversion.Non-indexed conversion",           StripConversion.nonIndexedConversion
            "Strip conversion.Non-strip identity",               StripConversion.nonStripIdentity
            "Strip conversion.Empty and underfilled strips",     StripConversion.emptyAndUnderfilled
            "Strip conversion.Attribute dictionary isolation",   StripConversion.attributeIsolation
            "Strip conversion.TriangleMesh source preservation", StripConversion.triangleMeshPreservesSource
        ]
        |> prepareCasesCpu "Operations" target