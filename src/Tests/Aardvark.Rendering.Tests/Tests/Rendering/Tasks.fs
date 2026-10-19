namespace Aardvark.Rendering.Tests.Rendering

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Expecto

module RenderTasks =

    module Cases =

        let update (runtime : IRuntime) =
            let color0 = C3b.Aquamarine
            let color1 = C3b.Chocolate

            let color = AVal.init color0
            let size = AVal.constant <| V2i(256)

            let makeSg (color : C3b) =
                Sg.fullScreenQuad
                |> Sg.uniform' "Color" color
                |> Sg.shader {
                    do! DefaultSurfaces.sgColor
                }

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            use task =
                color
                |> AVal.map makeSg
                |> Sg.dynamic
                |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor size
            output.Acquire()

            try
                let res0 = output.GetValue().Download().AsPixImage<uint8>()
                res0 |> PixImage.isColor (color0.ToArray())

                transact (fun _ ->
                    color.Value <- color1
                )

                Expect.isTrue task.OutOfDate "Task was not marked out of date"

                task.Update()

                Expect.isFalse task.OutOfDate "Task is still out of date"

                let res1 = output.GetValue().Download().AsPixImage<uint8>()
                res1 |> PixImage.isColor (color1.ToArray())

            finally
                output.Release()

        let occlusionQuery (contextSwitch : bool) (runtime : IRuntime) =
            let size = V2i(256)

            use query = runtime.CreateOcclusionQuery()

            use __ : IDisposable =
                match runtime with
                | :? GL.Runtime as gl when contextSwitch ->
                    let context = gl.Context.CreateContext()

                    try
                        context.MakeCurrent()
                        query.Begin()
                        query.End()
                        context
                    finally
                        context.ReleaseCurrent()

                | _ ->
                    Disposable.empty

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            use task =
                Sg.fullScreenQuad
                |> Sg.shader {
                    do! DefaultSurfaces.constantColor C4f.White
                }
                |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor (AVal.constant <| size)
            output.Acquire()

            try
                let _ = output.GetValue(AdaptiveToken.Top, { RenderToken.Empty with Queries = [query] })
                let samples = query.GetResult()
                let expectedSamples = uint64 (size.X * size.Y)

                // For Vulkan, AMD seems to count the clear task as well
                let valid = samples = expectedSamples || samples = expectedSamples * 2UL

                Expect.isGreaterThan samples 0UL "Non-positive sample count"
                Expect.isTrue valid $"Expected sample count {expectedSamples} but got {samples}"
            finally
                output.Release()

        let orderedCommandsUpdate (runtime : IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ]

            let color = AVal.init C4b.Red

            let single =
                color |> AVal.map (fun c ->
                    Sg.fullScreenQuad
                    |> Sg.shader {
                        do! DefaultSurfaces.constantColor (C4f c)
                    }
                )
                |> Sg.dynamic

            let commands = clist( [single] )

            use task =
                Sg.execute (
                    RenderCommand.Ordered(commands : alist<ISg>)
                )
                |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor (AVal.constant <| V2i 256)
            output.Acquire()

            let check() =
                let result = output.GetValue().Download().AsPixImage<uint8>()
                let expected = if commands.IsEmpty then [| 0uy; 0uy; 0uy |] else (color.Value.ToArray())
                result |> PixImage.isColor expected

            try
                check()

                transact (fun _ -> color.Value <- C4b.Blue)
                check()

                transact (fun _ -> commands.Clear())
                check()

                transact (fun _ -> color.Value <- C4b.Green)
                check()

                transact (fun _ -> commands.Add single |> ignore)
                check()

            finally
                output.Release()

    module private Disposal =

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwChild (error : exn) = raise error

        type private RecordingTask(id : int, events : ResizeArray<int>, failure : int -> exn option) =
            inherit AdaptiveObject()
            let taskId = RenderTaskId.New()
            let mutable calls = 0
            member _.Calls = calls
            interface IRenderTask with
                member _.Id = taskId
                member _.FramebufferSignature = None
                member _.Runtime = None
                member _.FrameId = 0UL
                member _.Name with get() = null and set _ = ()
                member _.Update(_, _) = failwith "Unexpected child update"
                member _.Run(_, _, _) = failwith "Unexpected child rendering"
                member _.Use f = f()
                member _.Dispose() =
                    calls <- calls + 1
                    events.Add id
                    match failure (events.Count - 1) with
                    | Some error -> throwChild error
                    | None -> ()

        type private OneShotTask(id : int, events : ResizeArray<int>, error : exn option) =
            inherit AbstractRenderTask()
            override _.FramebufferSignature = None
            override _.Runtime = None
            override _.PerformUpdate(_, _) = failwith "Unexpected child update"
            override _.Perform(_, _, _) = failwith "Unexpected child rendering"
            override _.Use f = f()
            override _.Release() =
                events.Add id
                match error with Some error -> throwChild error | None -> ()

        let private builders : (string * (IRenderTask[] -> IRenderTask)) list =
            [
                "direct", (fun children -> new SequentialRenderTask(children) :> IRenderTask)
                "ofArray", RenderTask.ofArray
                "ofList", (Array.toList >> RenderTask.ofList)
                "ofSeq", (fun children -> seq { for child in children do yield child } |> RenderTask.ofSeq)
            ]

        let private outcome action = try action(); None with error -> Some error

        let private checkFailure context (expected : exn list) (actual : exn option) =
            match expected, actual with
            | [], None -> ()
            | [expected], Some actual ->
                Expect.isTrue (obj.ReferenceEquals(actual, expected)) (context + ": lone exception identity")
            | expected, Some (:? AggregateException as aggregate) when expected.Length > 1 ->
                Expect.equal aggregate.InnerExceptions.Count expected.Length (context + ": aggregate is not flattened")
                for index, error in List.indexed expected do
                    Expect.isTrue (obj.ReferenceEquals(aggregate.InnerExceptions.[index], error)) (sprintf "%s, error=%d: disposal-order identity" context index)
            | _ -> failtestf "%s: expected %d failures, actual %A" context expected.Length actual
            for error in expected do
                Expect.stringContains error.StackTrace "throwChild" (context + ": original throwing frame retained")

        let private checkRepeats context (parent : IRenderTask) (events : ResizeArray<int>) =
            let before = events.ToArray()
            for repeat = 1 to 3 do
                Expect.isNone (outcome parent.Dispose) (sprintf "%s, repeat=%d: parent disposal is one-shot" context repeat)
                Expect.sequenceEqual events before (context + ": repeated disposal cannot retry a partial cleanup")

        let private run context (build : IRenderTask[] -> IRenderTask) (entries : int[]) (failures : (int * exn) list) =
            let events = ResizeArray<int>()
            let errors = Map.ofList failures
            let children =
                entries |> Array.distinct |> Array.map (fun id ->
                    id, new RecordingTask(id, events, fun position -> Map.tryFind position errors)) |> Map.ofArray
            let tasks = entries |> Array.map (fun id -> children.[id] :> IRenderTask)
            use parent = build tasks
            let failure = outcome parent.Dispose
            checkRepeats context parent events
            Expect.sequenceEqual events entries (context + ": every array entry attempted in order")
            for KeyValue(id, child) in children do
                Expect.equal child.Calls (entries |> Array.filter ((=) id) |> Array.length) (sprintf "%s, child=%d: duplicate entry count" context id)
            failures |> List.sortBy fst |> List.map snd |> fun expected -> checkFailure context expected failure

        let successful _ =
            for builder, build in builders do
                for count in [0; 1; 2; 7; 32] do
                    run (sprintf "builder=%s, count=%d" builder count) build [|0 .. count - 1|] []
                run (builder + ": successful duplicate entries") build [|0; 1; 0; 2; 1; 0|] []

        let singleFailures _ =
            for builder, build in builders do
                for count in [1; 2; 4; 8; 17] do
                    for position = 0 to count - 1 do
                        let error = InvalidOperationException(sprintf "child %d" position)
                        run (sprintf "builder=%s, count=%d, failure=%d" builder count position) build [|0 .. count - 1|] [position, error]

        let multipleFailures _ =
            for builder, build in builders do
                for count in [2; 3; 4; 7] do
                    for mask = 1 to (1 <<< count) - 1 do
                        let positions = [for index = 0 to count - 1 do if mask &&& (1 <<< index) <> 0 then index]
                        if positions.Length > 1 then
                            let failures = positions |> List.map (fun index -> index, InvalidOperationException(sprintf "child %d" index) :> exn)
                            run (sprintf "builder=%s, count=%d, failures=%A" builder count positions) build [|0 .. count - 1|] failures

        let nestedAggregates _ =
            for builder, build in builders do
                let inner = InvalidOperationException("nested leaf")
                let nested = AggregateException("inner aggregate", [|inner :> exn; ArgumentException("other nested leaf")|])
                let first = AggregateException("first existing aggregate", [|nested :> exn|]) :> exn
                let second = AggregateException("second existing aggregate", [|ArgumentException("second leaf") :> exn|]) :> exn
                let plain = InvalidOperationException("plain") :> exn
                let layouts = [[1, first]; [0, first; 3, plain]; [0, plain; 2, second]; [0, first; 1, second; 3, plain]]
                for index, failures in List.indexed layouts do
                    run (sprintf "builder=%s, layout=%d" builder index) build [|0; 1; 2; 3|] failures
                    Expect.isTrue (obj.ReferenceEquals((first :?> AggregateException).InnerExceptions.[0], nested)) (builder + ": existing nested aggregate remains intact")

        let duplicates _ =
            for builder, build in builders do
                for entries in [[|0; 0; 0|]; [|0; 1; 0; 2; 1; 0|]] do
                    let context = sprintf "builder=%s, entries=%A" builder entries
                    for position = 0 to entries.Length - 1 do
                        run (sprintf "%s, failure=%d" context position) build entries [position, InvalidOperationException("duplicate entry")]
                    let shared = InvalidOperationException("same exception from repeated entries") :> exn
                    let failures = entries |> Array.indexed |> Array.choose (fun (index, id) -> if id = 0 then Some (index, shared) else None) |> Array.toList
                    run (context + ": repeated error identity") build entries failures
                for throws in [false; true] do
                    let events = ResizeArray<int>()
                    let error = InvalidOperationException("one-shot child") :> exn
                    use first = new OneShotTask(0, events, if throws then Some error else None)
                    use second = new OneShotTask(1, events, None)
                    use parent = build [|first; first; second; first|]
                    let context = sprintf "builder=%s, abstract child throws=%b" builder throws
                    let failure = outcome parent.Dispose
                    checkRepeats context parent events
                    Expect.sequenceEqual events [|0; 1|] (context + ": abstract children retain one-shot releases")
                    checkFailure context (if throws then [error] else []) failure

    let tests (target: TestTarget) =
        [
            if target = TestTarget.Cpu then
                "Sequential disposal.Successful empty and duplicate entries", Disposal.successful
                "Sequential disposal.Single failures complete cleanup",       Disposal.singleFailures
                "Sequential disposal.Multiple failures retain order",         Disposal.multipleFailures
                "Sequential disposal.Nested aggregates remain intact",        Disposal.nestedAggregates
                "Sequential disposal.Duplicate entry failure semantics",      Disposal.duplicates

            elif target.IsGpu then
                "Update", Cases.update

                "Occlusion query", Cases.occlusionQuery false

                if target = TestTarget.GL then
                    "Occlusion query with context switch", Cases.occlusionQuery true

                "Ordered commands update", Cases.orderedCommandsUpdate
        ]
        |> prepareCases "Render tasks" target