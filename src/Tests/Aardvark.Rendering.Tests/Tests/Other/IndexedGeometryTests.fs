namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
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
        ]