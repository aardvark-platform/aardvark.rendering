namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open Expecto

open System.Reflection

[<AutoOpen>]
module ``Unit Test Utilities`` =

    let private backendName = function
        | Backend.GL -> "GL"
        | Backend.Vulkan -> "Vulkan"

    let testBackend (backend : Backend) (name : string) (tests : List<Backend -> Test>) =
        let name = sprintf "[%s] %s" (backendName backend) name
        testList name (tests |> List.map (fun t -> t backend))

    let prepareCases (backend : Backend) (name : string) (cases : List<string * (IRuntime -> unit)>) =
        cases |> List.map (fun (name, test) ->
            testCase name (fun () -> TestApplication.createUse test backend)
            |> testSequenced
        )
        |> testList name

    let prepareCasesBackendAgnostic (name : string) (cases : List<string * (unit -> unit)>) =
        cases |> List.map (fun (name, test) ->
            testCase name (fun () ->
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()
                test()
            )
            |> testSequenced
        )
        |> testList name