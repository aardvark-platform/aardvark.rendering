namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open Expecto

open System.Reflection
open System.Runtime.InteropServices

[<AutoOpen>]
module ``Unit Test Utilities`` =

    let testBackend (backend : Backend) (name : string) (tests : List<Backend -> Test>) =
        let name = sprintf "[%s] %s" (string backend) name
        testList name (tests |> List.map (fun t -> t backend))

    let validateRuntimeForCases (validate: IRuntime -> unit) (cases : List<string * (IRuntime -> unit)>) =
        cases |> List.map (fun (s, f) -> s, (fun r -> validate r; r) >> f)

    let prepareCases (backend : Backend) (name : string) (cases : List<string * (IRuntime -> unit)>) =
        let backend =
            match backend with
            | Backend.GL -> TestBackend.GL (if Aardvark.GetOSPlatform() <> OSPlatform.Windows then Framework.GLFW else Framework.OpenTK)
            | Backend.Vulkan -> TestBackend.Vulkan

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