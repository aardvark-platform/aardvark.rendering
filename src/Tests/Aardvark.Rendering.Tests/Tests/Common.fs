namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Expecto

open System
open System.Reflection

[<Struct; RequireQualifiedAccess>]
type TestTarget =
    | Cpu
    | GL      // OpenGL with any framework
    | GLFW    // OpenGL / GLFW
    | OpenTK  // OpenGL / OpenTK
    | Vulkan

    /// Returns true if common GPU target (not including GLFW and OpenTK)
    member this.IsGpu = match this with TestTarget.GL | TestTarget.Vulkan -> true | _ -> false

    member this.Backend =
        match this with
        | GL -> Some <| TestBackend.GL Framework.Default
        | GLFW -> Some <| TestBackend.GL Framework.GLFW
        | OpenTK -> Some <| TestBackend.GL Framework.OpenTK
        | Vulkan -> Some <| TestBackend.Vulkan
        | _ -> None

[<AutoOpen>]
module ``Unit Test Utilities`` =

    type private CpuRuntime private() =
        inherit DispatchProxy()
        static let instance = DispatchProxy.Create<IRuntime, CpuRuntime>()
        static member Instance = instance
        override _.Invoke(targetMethod: MethodInfo, _: obj array) =
            raise <| NotSupportedException($"CPU runtime does not support '{targetMethod.Name}'.")

    let validateRuntimeForCases (validate: IRuntime -> unit) (cases: List<string * (IRuntime -> unit)>) =
        cases |> List.map (fun (s, f) -> s, (fun r -> validate r; r) >> f)

    let testTarget (target: TestTarget) (tests: List<TestTarget -> Test>) =
        let name = string target
        testList name (tests |> List.map (fun t -> t target))

    /// Instantiates the given test entry points for all targets.
    /// An entry point may define different test cases for different targets.
    let testAllTargets (name: string) (tests: List<TestTarget -> Test>) =
        testList name [
            testTarget TestTarget.Cpu tests
            testTarget TestTarget.GL tests
            testTarget TestTarget.GLFW tests
            testTarget TestTarget.OpenTK tests
            testTarget TestTarget.Vulkan tests
        ]

    let prepareCase (name: string) (target: TestTarget) (test: IRuntime -> unit) =
        let run =
            match target.Backend with
            | Some backend ->
                fun () -> TestApplication.createUse test backend

            | _ ->
                fun () ->
                    IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                    Aardvark.Init()
                    test CpuRuntime.Instance

        testCase name run
        |> testSequenced

    let prepareCases (name: string) (target: TestTarget) (cases: List<string * (IRuntime -> unit)>) =
        cases |> List.map (fun (name, test) ->
            prepareCase name target test
        )
        |> testList name

    /// Only test for the given targets.
    let prepareCasesFor (name: string) (target: TestTarget) (targets: TestTarget seq) (cases: List<string * (IRuntime -> unit)>) =
        let cases = if Seq.contains target targets then cases else []
        cases |> prepareCases name target

    /// Only test for general GPU targets, ignoring GLFW and OpenTK targets.
    let prepareCasesGpu (name: string) (target: TestTarget) (cases: List<string * (IRuntime -> unit)>) =
        cases |> prepareCasesFor name target [TestTarget.GL; TestTarget.Vulkan]

    /// Only test for CPU target.
    let prepareCasesCpu (name: string) (target: TestTarget) (cases: List<string * (unit -> unit)>) =
        cases |> List.map (fun (name, run) -> name, fun _ -> run())
        |> prepareCasesFor name target [TestTarget.Cpu]