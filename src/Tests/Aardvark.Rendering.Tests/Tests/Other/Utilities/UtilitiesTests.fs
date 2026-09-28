namespace Aardvark.Rendering.Tests

open Aardvark.Rendering.Tests.Utilities
open Expecto

module ``Utilities Tests`` =

    [<Tests>]
    let tests =
        testList "Utilities" [
            CompactSet.tests
            MemoryManager.tests
        ]