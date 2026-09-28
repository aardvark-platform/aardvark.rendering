namespace Aardvark.Rendering.Tests

open Aardvark.Rendering.Tests.IndexedGeometry
open Expecto

module ``IndexedGeometry Tests`` =

    [<Tests>]
    let tests =
        testList "IndexedGeometry" [
            Operations.tests
            Sphere.tests
            CylinderNormals.tests
            PrimitiveSequences.tests
        ]