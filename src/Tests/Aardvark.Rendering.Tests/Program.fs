open Aardvark.Rendering.Tests
open Aardvark.Base
open Expecto
open Microsoft.FSharp.Reflection

let tests (targets: TestTarget list) =
    let testAllTargets  =
        if targets.IsEmpty then
            testAllTargets
        else
            fun group tests ->
                targets
                |> List.map (fun target -> testTarget target tests)
                |> testList group

    Test.ofList [
        testAllTargets "Buffers" [
            Buffer.AdaptiveBufferTests.tests
            Buffer.BufferCopy.tests
            Buffer.BufferUpload.tests
            Buffer.BufferDownload.tests
            Buffer.BufferToArray.tests
            Buffer.AttributeBuffer.tests
            Buffer.ManagedBuffer.tests
            Buffer.IndirectBuffer.tests
            Buffer.BufferView.tests
            Buffer.BufferViewBoundaries.tests
        ]

        testAllTargets "Textures" [
            Texture.AdaptiveTexture.tests
            Texture.AdaptiveAttachment.tests
            Texture.TextureUpload.tests
            Texture.TextureDownload.tests
            Texture.TextureCreate.tests
            Texture.TextureCopy.tests
            Texture.TextureClear.tests
            Texture.TextureCompression.tests
            Texture.TextureDds.tests
        ]

        testAllTargets "Rendering" [
            Rendering.AdaptiveFramebufferCube.tests
            Rendering.Blending.tests
            Rendering.ColorMasks.tests
            Rendering.Culling.tests
            Rendering.RenderTasks.tests
            Rendering.FramebufferSignature.tests
            Rendering.IntegerAttachments.tests
            Rendering.Samplers.tests
            Rendering.Uniforms.tests
            Rendering.Surfaces.tests
            Rendering.DrawCalls.tests
            Rendering.Viewport.tests
            Rendering.ResourceManagement.tests
            Rendering.Commands.tests
            Rendering.LodRenderer.tests
            Rendering.SceneGraph.tests
            Rendering.AirAttributes.tests
        ]

        testAllTargets "Compute" [
            Compute.ComputeImages.tests
            Compute.ComputeBuffers.tests
            Compute.ComputePrimitives.tests
            Compute.ComputeSorting.tests
            Compute.ComputeJpeg.tests
            Compute.MutableInputBinding.tests
        ]

        testAllTargets "Raytracing" [
            Raytracing.TraceGeometry.tests
        ]

        testAllTargets "IndexedGeometry" [
            IndexedGeometry.Operations.tests
            IndexedGeometry.Sphere.tests
            IndexedGeometry.CylinderNormals.tests
            IndexedGeometry.PrimitiveSequences.tests
        ]

        testAllTargets "Utilities" [
            Utilities.ConcurrentDeltaPriorityQueue.tests
            Utilities.CompactSet.tests
            Utilities.ColoredLock.tests
            Utilities.MemoryManager.tests
            Utilities.Trie.tests
            Utilities.TaskUtilities.tests
            Utilities.LockedSet.tests
        ]

        testAllTargets "Other" [
            Other.Camera.tests
            Other.ShapeListConcat.tests
            Other.UniformWriter.tests
            Other.AdaptiveResource.tests
            Other.Input.tests
            Other.ContextCreation.tests
            Other.VulkanWrapper.tests
            Other.IDictionaryStructuralComparer.tests
        ]
    ]

[<Tests>]
let allTests = tests []

[<EntryPoint>]
let main argv =
    let targets =
        FSharpType.GetUnionCases typeof<TestTarget>
        |> Array.map (fun case ->
            let value = FSharpValue.MakeUnion(case, [||]) :?> TestTarget
            case.Name.ToLowerInvariant(), value
        )
        |> List.ofArray

    let options =
        let targets = targets |> List.map fst |> String.concat ", "

        [
            "target",       ("<target>", $"Run test target. <target> may be {targets}.")
            "filter",       ("<string>", "Only run tests whose full name (Group.Target.Test...) contains <string>.")
            "exclude",      ("<string>", "Do not run tests whose full name contains <string>.")
            "test",         ("<name>",   "Only run the test with the given full name.")
            "stop-on-fail", (null,       "Stop as soon as a test fails.")
            "list",         (null,       "List the names of the selected tests without running them.")
            "help",         (null,       "Show this help text.")
        ]

    let switches = options |> List.choose (fun (n, (a, _)) -> if a = null then Some n else None)
    let arguments = options |> List.choose (fun (n, (a, _)) -> if a <> null then Some n else None)

    let switch name =
        switches |> List.contains name && argv |> Array.contains $"--{name}"

    let withoutPrefix (prefix: string) (str: string) =
        if str.StartsWith(prefix, System.StringComparison.Ordinal) then
            Some <| str.Substring prefix.Length
        else
            None

    let argument name =
        if arguments |> List.contains name then
            argv |> Array.choose (withoutPrefix $"--{name}=") |> List.ofArray
        else
            []

    let unknownArgs =
        argv |> Array.filter (fun arg ->
            let isSwitch = switches |> List.exists (fun n -> arg = $"--{n}")
            let isArgument = arguments |> List.exists (fun n -> arg.StartsWith($"--{n}=", System.StringComparison.Ordinal))
            not (isSwitch || isArgument)
        )

    let error (message: string) =
        eprintfn $"Error: {message}"
        eprintfn "Use --help to show the available options."
        1

    if switch "help" then
        let options =
            options |> List.map (fun (name, (arg, desc)) ->
                let name = if arg = null then name else $"{name}={arg}"
                name, desc
            )

        printfn "Run tests"
        printfn ""
        printfn "OPTIONS"
        let n = options |> List.map (fst >> _.Length) |> List.max
        for name, desc in options do
            let desc = desc |> String.getLines
            let prefix = $"    --{name.PadRight n}    "
            printfn $"{prefix}{desc.[0]}"
            let prefix = String.replicate prefix.Length " "
            for i = 1 to desc.Length - 1 do
                printfn $"{prefix}{desc.[i]}"

        printfn ""
        printfn "Options with values can be repeated and must be passed as --<name>=<value>."
        printfn "A test is run if it matches any --filter or --test (or neither is given) and no --exclude."
        printfn "Matching of targets and test names is case-insensitive."
        0

    elif unknownArgs.Length > 0 then
        let args = unknownArgs |> Array.map (sprintf "'%s'") |> String.concat ", "
        error $"Unknown argument(s): {args}. Arguments with values must be passed as --<name>=<value>."

    else
        let selectedTargets =
            let targets = targets |> Map.ofList
            argument "target" |> List.map (fun t -> t, Map.tryFind (t.ToLowerInvariant()) targets)

        let invalidTargets =
            selectedTargets |> List.choose (fun (t, v) -> if v.IsNone then Some $"'{t}'" else None)

        if not invalidTargets.IsEmpty then
            let invalid = invalidTargets |> String.concat ", "
            let valid = targets |> List.map fst |> String.concat ", "
            error $"Unknown target(s): {invalid}. Valid targets: {valid}."

        else
            let filter =
                let ignoreCase = System.StringComparison.OrdinalIgnoreCase
                let contains (pattern: string) (name: string) = name.Contains(pattern, ignoreCase)
                let equals (pattern: string) (name: string) = name.Equals(pattern, ignoreCase)

                let selected =
                    (argument "filter" |> List.map contains) @
                    (argument "test" |> List.map equals)

                let excluded =
                    argument "exclude" |> List.map contains

                fun (name: string) ->
                    (selected.IsEmpty || selected |> List.exists (fun f -> f name)) &&
                    excluded |> List.forall (fun f -> not <| f name)

            let tests =
                match selectedTargets |> List.choose snd |> List.distinct with
                | [] -> allTests
                | targets -> tests targets
                |> Test.filter "." (String.concat "." >> filter)

            if switch "list" then
                let tests = Test.toTestCodeList tests
                for t in tests do
                    printfn "%s" (t.fullName ".")
                printfn ""
                printfn $"Total: {tests.Length}"
                0

            elif List.isEmpty (Test.toTestCodeList tests) then
                eprintfn "Error: No tests match the given --target, --filter, --test, and --exclude options."
                eprintfn "Use --list to check the selection."
                1

            else
                let stopOnFail = switch "stop-on-fail"
                runTestsSynchronously stopOnFail tests