namespace Aardvark.Rendering.Tests

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Rendering.Raytracing
open Expecto

module ``IndexedGeometry Tests`` =

    module Clone =

        let clone (shallow: bool) =
            let name = if shallow then "shallow" else "deep"

            test $"Clone ({name})" {
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
            }

    module Union =

        let unionIndexed =
            test "Union (indexed)" {
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
            }

        let unionNonIndexed =
            test "Union (non-indexed)" {
                let a = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toNonIndexed
                let b = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toNonIndexed
                let c = IndexedGeometry.union a b

                Expect.isNull c.IndexArray "Unexpected index array"
                Expect.equal c.FaceVertexCount (a.FaceVertexCount + b.FaceVertexCount) "Unexpected face vertex count"
                Expect.isTrue c.IsValid "Invalid"
            }

        let inline private unionNonIndexedAndIndexed (name: string) (mapIndex: int32 -> 'T) =
            test $"Union (non-indexed & {name}-indexed)" {
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
            }

        let unionNonIndexedAndInt16 = unionNonIndexedAndIndexed "int16" int16
        let unionNonIndexedAndInt32 = unionNonIndexedAndIndexed "int32" int32

    module StripConversion =

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

        let private verifyShallowCopy (source : IndexedGeometry) (result : IndexedGeometry) =
            Expect.isFalse (obj.ReferenceEquals(source, result)) "Strip conversion returned the source geometry"

            let expectDictionaryReference = if source.IsIndexed then Expect.isTrue else Expect.isFalse
            expectDictionaryReference (obj.ReferenceEquals(source.IndexedAttributes, result.IndexedAttributes)) "Unexpected indexed attribute dictionary aliasing"
            expectDictionaryReference (obj.ReferenceEquals(source.SingleAttributes, result.SingleAttributes)) "Unexpected single attribute dictionary aliasing"

            for KeyValue(semantic, sourceAttribute) in source.IndexedAttributes do
                let resultAttribute = result.IndexedAttributes.[semantic]
                Expect.isTrue (obj.ReferenceEquals(sourceAttribute, resultAttribute)) $"Attribute array {semantic} was not shallow-copied"

            for KeyValue(semantic, sourceAttribute) in source.SingleAttributes do
                Expect.equal result.SingleAttributes.[semantic] sourceAttribute $"Single attribute {semantic} was not preserved"

        let private indexedCase<'T when 'T : equality> (name : string) (convert : int -> 'T) (mode : IndexedGeometryMode) =
            test $"{mode} ({name})" {
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

                let first = source.ToNonStripped()
                let second = source.ToNonStripped()

                Expect.equal source.Mode mode "Source mode was modified"
                Expect.isTrue (obj.ReferenceEquals(sourceIndices, source.IndexArray)) "Source index array was replaced"
                Expect.equal sourceIndices originalIndices "Source indices were modified"
                Expect.isTrue (obj.ReferenceEquals(originalAttributes, source.IndexedAttributes)) "Source indexed attributes were replaced"
                Expect.isTrue (obj.ReferenceEquals(originalSingleAttributes, source.SingleAttributes)) "Source single attributes were replaced"

                for result in [| first; second |] do
                    Expect.equal result.Mode (listMode mode) "Unexpected output mode"
                    Expect.equal (unbox<'T[]> result.IndexArray) expected "Unexpected output indices"
                    verifyShallowCopy source result

                Expect.isFalse (obj.ReferenceEquals(first, second)) "Repeated conversion reused a geometry result"
                Expect.isFalse (obj.ReferenceEquals(first.IndexArray, second.IndexArray)) "Repeated conversion reused an index result"
                Expect.isTrue (obj.ReferenceEquals(first, first.ToNonStripped())) "Non-strip conversion did not preserve identity"
            }

        let indexedCases =
            [
                for mode in [ IndexedGeometryMode.LineStrip; IndexedGeometryMode.TriangleStrip ] do
                    indexedCase "int16" int16 mode
                    indexedCase "uint16" uint16 mode
                    indexedCase "int32" int32 mode
                    indexedCase "uint32" uint32 mode
            ]

        let private nonIndexedCase (mode : IndexedGeometryMode) =
            test $"{mode} (non-indexed)" {
                let source = createGeometry mode 5 null
                let originalAttributes = source.IndexedAttributes
                let originalSingleAttributes = source.SingleAttributes

                let expected =
                    match mode with
                    | IndexedGeometryMode.LineStrip -> [| 0; 1; 1; 2; 2; 3; 3; 4 |]
                    | IndexedGeometryMode.TriangleStrip -> [| 0; 1; 2; 2; 1; 3; 2; 3; 4 |]
                    | _ -> failwith $"Unexpected mode {mode}"

                let first = source.ToNonStripped()
                let second = source.ToNonStripped()

                Expect.equal source.Mode mode "Source mode was modified"
                Expect.isNull source.IndexArray "Source geometry became indexed"
                Expect.isTrue (obj.ReferenceEquals(originalAttributes, source.IndexedAttributes)) "Source indexed attributes were replaced"
                Expect.isTrue (obj.ReferenceEquals(originalSingleAttributes, source.SingleAttributes)) "Source single attributes were replaced"

                for result in [| first; second |] do
                    Expect.equal result.Mode (listMode mode) "Unexpected output mode"
                    Expect.equal (unbox<int32[]> result.IndexArray) expected "Unexpected output indices"
                    verifyShallowCopy source result

                Expect.isFalse (obj.ReferenceEquals(first.IndexArray, second.IndexArray)) "Repeated conversion reused an index result"
            }

        let nonIndexedCases =
            [ nonIndexedCase IndexedGeometryMode.LineStrip
              nonIndexedCase IndexedGeometryMode.TriangleStrip ]

        let nonStripIdentity =
            test "Non-strip identity" {
                let modes =
                    [ IndexedGeometryMode.PointList
                      IndexedGeometryMode.LineList
                      IndexedGeometryMode.TriangleList
                      IndexedGeometryMode.TriangleAdjacencyList
                      IndexedGeometryMode.LineAdjacencyList
                      IndexedGeometryMode.QuadList ]

                for mode in modes do
                    let geometry = createGeometry mode 6 ([| 0; 1; 2 |] :> Array)
                    Expect.isTrue (obj.ReferenceEquals(geometry, geometry.ToNonStripped())) $"Identity was not preserved for {mode}"
            }

        let emptyAndUnderfilled =
            test "Empty and underfilled strips" {
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
                            let indices = createIndices count
                            let source = createGeometry mode count indices
                            let result = source.ToNonStripped()

                            Expect.equal result.Mode (listMode mode) $"Unexpected output mode ({name}, count = {count})"
                            Expect.equal result.IndexArray.Length 0 $"Underfilled strip produced indices ({name}, count = {count})"
                            Expect.equal (result.IndexArray.GetType().GetElementType()) elementType $"Index type changed ({name}, count = {count})"
                            Expect.isTrue (obj.ReferenceEquals(indices, source.IndexArray)) $"Source indices changed ({name}, count = {count})"

                        let source = createGeometry mode count null
                        let result = source.ToNonStripped()
                        Expect.equal result.Mode (listMode mode) $"Unexpected non-indexed output mode (count = {count})"
                        Expect.equal (unbox<int32[]> result.IndexArray) Array.empty $"Underfilled non-indexed strip produced indices (count = {count})"
                        Expect.isNull source.IndexArray $"Underfilled source became indexed (count = {count})"
            }

        let attributeIsolation =
            test "Attribute dictionaries are isolated" {
                let source = createGeometry IndexedGeometryMode.LineStrip 3 null
                let result = source.ToNonStripped()

                result.IndexedAttributes.Remove DefaultSemantic.Normals |> ignore
                result.SingleAttributes.Remove DefaultSemantic.Colors |> ignore

                Expect.isTrue (source.IndexedAttributes.ContainsKey DefaultSemantic.Normals) "Result modification removed a source indexed attribute"
                Expect.isTrue (source.SingleAttributes.ContainsKey DefaultSemantic.Colors) "Result modification removed a source single attribute"
            }

        let triangleMeshPreservesSource =
            test "TriangleMesh.FromIndexedGeometry preserves source" {
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
            }

    [<Tests>]
    let tests =
        testList "IndexedGeometry" [
            testList "Clone" [
                Clone.clone true
                Clone.clone false
            ]

            testList "Union" [
                Union.unionIndexed
                Union.unionNonIndexed
                Union.unionNonIndexedAndInt16
                Union.unionNonIndexedAndInt32
            ]

            testList "Strip conversion" [
                yield! StripConversion.indexedCases
                yield! StripConversion.nonIndexedCases
                StripConversion.nonStripIdentity
                StripConversion.emptyAndUnderfilled
                StripConversion.attributeIsolation
                StripConversion.triangleMeshPreservesSource
            ]
        ]
