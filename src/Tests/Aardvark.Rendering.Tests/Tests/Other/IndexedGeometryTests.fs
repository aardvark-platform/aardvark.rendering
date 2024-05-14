namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Expecto

module ``IndexedGeometry Tests`` =

    let clone =
        test "Clone" {
            let g = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
            g.SingleAttributes <- SymDict.empty
            let g_pos = g.IndexedAttributes.[DefaultSemantic.Positions]
            let g_idx = g.IndexArray

            let gs = g.Clone() // shallow
            let gs_pos = gs.IndexedAttributes.[DefaultSemantic.Positions]
            let gs_idx = gs.IndexArray

            Expect.isFalse (obj.ReferenceEquals(g.SingleAttributes, gs.SingleAttributes)) "Single attributes not copied"
            Expect.isFalse (obj.ReferenceEquals(g.IndexedAttributes, gs.IndexedAttributes)) "Indexed attributes not copied"
            Expect.isTrue (obj.ReferenceEquals(g_pos, gs_pos)) "Attribute array copied"
            Expect.isTrue (obj.ReferenceEquals(g_idx, gs_idx)) "Index array copied"

            let gd = g.Clone(shallowCopy = false)
            let gd_pos = gd.IndexedAttributes.[DefaultSemantic.Positions]
            let gd_idx = gd.IndexArray

            Expect.isFalse (obj.ReferenceEquals(g.SingleAttributes, gd.SingleAttributes)) "Single attributes not copied"
            Expect.isFalse (obj.ReferenceEquals(g.IndexedAttributes, gd.IndexedAttributes)) "Indexed attributes not copied"
            Expect.isFalse (obj.ReferenceEquals(g_pos, gd_pos)) "Attribute array not copied"
            Expect.isFalse (obj.ReferenceEquals(g_idx, gd_idx)) "Index array not copied"
        }

    [<Tests>]
    let tests =
        testList "IndexedGeometry" [
            testList "Operations" [
                clone
            ]
        ]