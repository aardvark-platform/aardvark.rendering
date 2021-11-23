namespace Aardvark.Rendering.Tests

open Aardvark.Application
open Expecto

[<AutoOpen>]
module ``Unit Test Utilities`` =

    let private backendName = function
        | Backend.GL -> "GL"
        | Backend.Vulkan -> "Vulkan"

    let testBackend (backend : Backend) (name : string) (tests : List<Backend -> Test>) =
        let name = sprintf "[%s] %s" (backendName backend) name
        testList name (tests |> List.map (fun t -> t backend))