namespace Aardvark.Rendering.Tests.Rendering

open System
open System.Reflection
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module RenderTo =

    module private Cases =

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwTarget (error : exn) = raise error

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwClear (error : exn) = raise error

        type private RuntimeProxy() =
            inherit DispatchProxy()
            member val Compile = Unchecked.defaultof<IFramebufferSignature * aval<ClearValues> -> IRenderTask> with get, set
            override x.Invoke(methodInfo, args) =
                if methodInfo.Name = "CompileClear" then
                    x.Compile(args.[0] :?> IFramebufferSignature, args.[1] :?> aval<ClearValues>) :> obj
                else failwithf "Unexpected runtime operation: %s" methodInfo.Name

        type private Signature(runtime : IRuntime) =
            interface IFramebufferSignature with
                member _.Runtime = runtime :> IFramebufferRuntime
                member _.Samples = 1
                member _.LayerCount = 1
                member _.PerLayerUniforms = Set.empty
                member _.ColorAttachments = Map.empty
                member _.DepthStencilAttachment = None
                member _.Dispose() = failwith "Caller-owned signature was disposed"

        type private Framebuffer(signature : IFramebufferSignature, slot : int) =
            interface IFramebuffer with
                member _.Signature = signature
                member _.Size = V2i(32 >>> (slot / 6))
                member _.Handle = uint64 slot
                member _.Attachments = Map.empty
                member _.Dispose() = ()

        type private Task(runtime : IRuntime, signature : IFramebufferSignature, events : ResizeArray<string>, label : string) =
            inherit AdaptiveObject()
            let id = RenderTaskId.New()
            member val Error : exn option = None with get, set
            member val DisposeAttempts = 0 with get, set
            member val Disposed = false with get, set
            member val Runs = 0 with get, set
            member val LastOutput = Unchecked.defaultof<OutputDescription> with get, set
            member val LastToken = RenderToken.Empty with get, set
            member val Name : string = label with get, set
            interface IRenderTask with
                member _.Id = id
                member _.Runtime = Some runtime
                member _.FramebufferSignature = Some signature
                member x.Name with get() = x.Name and set v = x.Name <- v
                member x.FrameId = uint64 x.Runs
                member _.Use action = action()
                member _.Update(_, _) = ()
                member x.Run(_, token, output) =
                    x.Runs <- x.Runs + 1
                    x.LastToken <- token
                    x.LastOutput <- output
                    events.Add(label + " run")
                member x.Dispose() =
                    x.DisposeAttempts <- x.DisposeAttempts + 1
                    events.Add(label + " release")
                    match x.Error with
                    | Some error -> throwClear error
                    | None -> x.Disposed <- true

        type private TargetState =
            { mutable Creates : int; mutable Destroys : int; mutable Computes : int; mutable Error : exn option }

        type private Target<'T>(state : TargetState, events : ResizeArray<string>, value : 'T) =
            inherit AdaptiveResource<'T>()
            override _.Create() = state.Creates <- state.Creates + 1
            override _.Compute(_, _) =
                state.Computes <- state.Computes + 1
                value
            override _.Destroy() =
                state.Destroys <- state.Destroys + 1
                events.Add "target release"
                match state.Error with
                | Some error -> throwTarget error
                | None -> ()

        type private Route = Plain | Fixed | Adaptive | FixedPerFace | AdaptivePerFace

        type private Fixture(levels : int, route : Route, shared : bool) =
            let events = ResizeArray<string>()
            let clears = ResizeArray<Task>()
            let runtime = DispatchProxy.Create<IRuntime, RuntimeProxy>()
            let signature = new Signature(runtime) :> IFramebufferSignature
            let slots = if levels = 0 then 1 else 6 * levels
            let frames = Array.init slots (fun slot -> new Framebuffer(signature, slot) :> IFramebuffer)
            let callers = Array.init slots (fun slot -> new Task(runtime, signature, events, $"caller {slot}"))
            let target = { Creates = 0; Destroys = 0; Computes = 0; Error = None }
            let token = RenderToken.Zero
            let values slot = ClearValues.ofColor (C4f(float32 (slot + 1) / 32.0f, 0.25f, 0.5f, 1.0f))
            let uniform = AVal.constant (values 0)
            do
                (runtime :?> RuntimeProxy).Compile <- fun (actualSignature, _) ->
                    Expect.isTrue (obj.ReferenceEquals(signature, actualSignature)) "CompileClear signature"
                    let clear = new Task(runtime, signature, events, $"clear {clears.Count}")
                    clears.Add clear
                    clear :> IRenderTask
            let result, get =
                if levels = 0 then
                    let output = new Target<IFramebuffer>(target, events, frames.[0]) :> IAdaptiveResource<_>
                    let caller = callers.[0] :> IRenderTask
                    let result =
                        match route with
                        | Plain -> caller.RenderTo output
                        | Fixed -> caller.RenderTo(output, values 0)
                        | Adaptive -> caller.RenderTo(output, uniform)
                        | _ -> failwith "Per-face values require a cube"
                    result :> IAdaptiveResource, (fun () -> [| result.GetValue(AdaptiveToken.Top, token) |])
                else
                    let tasks = CubeMap.init levels (fun face level -> callers.[6 * level + int face] :> IRenderTask)
                    let output =
                        CubeMap.init levels (fun face level -> frames.[6 * level + int face])
                        |> fun frames -> new Target<_>(target, events, frames) :> IAdaptiveResource<_>
                    let result =
                        match route with
                        | Plain -> tasks.RenderTo output
                        | Fixed -> tasks.RenderTo(output, values 0)
                        | Adaptive -> tasks.RenderTo(output, uniform)
                        | FixedPerFace -> tasks.RenderTo(output, CubeMap.init levels (fun face level -> values (6 * level + int face)))
                        | AdaptivePerFace ->
                            let values = CubeMap.init levels (fun face level -> if shared then uniform else AVal.constant (values (6 * level + int face)))
                            tasks.RenderTo(output, values)
                    result :> IAdaptiveResource, (fun () -> result.GetValue(AdaptiveToken.Top, token).Data)
            member _.Result = result
            member _.Get() = get()
            member _.Target = target
            member _.Clears = clears
            member _.Callers = callers
            member _.Events = events
            member _.Frames = frames
            member _.Token = token
            member _.ExpectedClears =
                match route with
                | Plain -> 0
                | FixedPerFace -> slots
                | AdaptivePerFace when not shared -> slots
                | _ -> 1
            interface IDisposable with
                member _.Dispose() =
                    target.Error <- None
                    for clear in clears do clear.Error <- None
                    try result.ReleaseAll() with _ -> ()
                    // Original failures can strand a private task resource. Drain only fake handles here,
                    // after the assertions, without hiding the regression or changing production ownership.
                    for clear in clears do
                        if not clear.Disposed then (clear :> IRenderTask).Dispose()
                    for caller in callers do (caller :> IRenderTask).Dispose()

        let private configurations =
            [ for levels in [0; 1; 2] do
                for route in (if levels = 0 then [Fixed; Adaptive] else [Fixed; Adaptive; FixedPerFace; AdaptivePerFace]) do
                    for shared in (if route = AdaptivePerFace then [false; true] else [true]) do
                        yield levels, route, shared ]

        let private checkError context (targetError : exn option) (taskError : exn option) (actual : exn option) =
            let same (expected : exn) (actual : exn) site =
                Expect.isTrue (obj.ReferenceEquals(expected, actual)) $"{context}: {site} exception identity"
                Expect.stringContains actual.StackTrace site $"{context}: original throw site survives"
            match targetError, taskError, actual with
            | None, None, None -> ()
            | Some expected, None, Some actual -> same expected actual "throwTarget"
            | None, Some expected, Some actual -> same expected actual "throwClear"
            | Some target, Some task, Some (:? AggregateException as aggregate) ->
                Expect.equal aggregate.InnerExceptions.Count 2 $"{context}: two stages, without flattening"
                same target aggregate.InnerExceptions.[0] "throwTarget"
                same task aggregate.InnerExceptions.[1] "throwClear"
            | _ -> failtestf "%s: unexpected cleanup result %A" context actual

        let private run levels route shared owners releaseAll materialized targetError taskError =
            let context = $"levels={levels}, route={route}, shared={shared}, owners={owners}, ReleaseAll={releaseAll}, materialized={materialized}, target error={Option.isSome targetError}, task error={Option.isSome taskError}"
            use fixture = new Fixture(levels, route, shared)
            for _ in 1 .. owners do fixture.Result.Acquire()
            Expect.equal fixture.Target.Creates 1 $"{context}: target acquired once"
            Expect.equal fixture.Clears.Count 0 $"{context}: acquire does not compile clears"
            if materialized then
                for _ in 1 .. 3 do
                    let result = fixture.Get()
                    Expect.equal result.Length fixture.Frames.Length $"{context}: output count"
                    for slot in 0 .. result.Length - 1 do
                        Expect.isTrue (obj.ReferenceEquals(result.[slot], fixture.Frames.[slot])) $"{context}, slot={slot}: output identity"
                Expect.equal fixture.Target.Computes 1 $"{context}: cached target"
                Expect.equal fixture.Clears.Count fixture.ExpectedClears $"{context}: compiled-clear sharing"
                Expect.equal (fixture.Clears |> Seq.sumBy _.Runs) (if route = Plain then 0 else fixture.Frames.Length) $"{context}: cached clear execution"
                for slot in 0 .. fixture.Callers.Length - 1 do
                    let caller = fixture.Callers.[slot]
                    Expect.equal caller.Runs 1 $"{context}, slot={slot}: caller runs only once"
                    Expect.isTrue (obj.ReferenceEquals(caller.LastOutput.Framebuffer, fixture.Frames.[slot])) $"{context}, slot={slot}: framebuffer selection"
                    Expect.isTrue (obj.ReferenceEquals(caller.LastToken, fixture.Token)) $"{context}, slot={slot}: render token"
            fixture.Target.Error <- targetError
            for clear in fixture.Clears do clear.Error <- taskError
            if not releaseAll then
                for _ in 1 .. owners - 1 do
                    fixture.Result.Release()
                    Expect.equal fixture.Target.Destroys 0 $"{context}: nonfinal release retains target"
                    Expect.isTrue (fixture.Clears |> Seq.forall (fun t -> t.DisposeAttempts = 0)) $"{context}: nonfinal release retains clear tasks"
            fixture.Events.Clear()
            let actual =
                try
                    if releaseAll then fixture.Result.ReleaseAll() else fixture.Result.Release()
                    None
                with error -> Some error
            let expectedAttempts = if Option.isSome taskError && fixture.Clears.Count > 0 then 1 else fixture.Clears.Count
            Expect.equal fixture.Target.Destroys 1 $"{context}: one target release attempt"
            Expect.equal (fixture.Clears |> Seq.sumBy _.DisposeAttempts) expectedAttempts $"{context}: task resource cleanup attempted exactly once"
            Expect.equal fixture.Events.Count (1 + expectedAttempts) $"{context}: no additional cleanup"
            Expect.equal fixture.Events.[0] "target release" $"{context}: target before task"
            for caller in fixture.Callers do Expect.equal caller.DisposeAttempts 0 $"{context}: caller-owned task remains untouched"
            let taskError = if fixture.Clears.Count = 0 then None else taskError
            checkError context targetError taskError actual
            if Option.isNone taskError then
                Expect.isTrue (fixture.Clears |> Seq.forall _.Disposed) $"{context}: successful compiled-clear disposal"
            let attempts = fixture.Clears |> Seq.map _.DisposeAttempts |> Seq.toArray
            for _ in 1 .. 3 do fixture.Result.ReleaseAll()
            Expect.equal fixture.Target.Destroys 1 $"{context}: no repeated target cleanup"
            Expect.sequenceEqual (fixture.Clears |> Seq.map _.DisposeAttempts) attempts $"{context}: no repeated task cleanup"
            for caller in fixture.Callers do Expect.equal caller.DisposeAttempts 0 $"{context}: repeated release does not take caller ownership"

        let private check (failures : ResizeArray<string>) levels route shared owners releaseAll materialized targetError taskError =
            try run levels route shared owners releaseAll materialized targetError taskError
            with error -> failures.Add error.Message

        let private error nested label : exn =
            if nested then AggregateException(label, [| InvalidOperationException(label + " inner") :> exn; ArgumentException(label + " second") :> exn |])
            else InvalidOperationException label

        let targetFailure () =
            let failures = ResizeArray<string>()
            for levels, route, shared in configurations do
                for owners in [1; 3] do
                    for releaseAll in [false; true] do
                        for nested in [false; true] do
                            check failures levels route shared owners releaseAll true (Some(error nested "target")) None
            Expect.isEmpty failures (String.concat "\n" failures)

        let taskFailure () =
            let failures = ResizeArray<string>()
            for levels, route, shared in configurations do
                for owners in [1; 3] do
                    for releaseAll in [false; true] do
                        for nested in [false; true] do
                            check failures levels route shared owners releaseAll true None (Some(error nested "clear"))
            Expect.isEmpty failures (String.concat "\n" failures)

        let combinedFailure () =
            let failures = ResizeArray<string>()
            for levels, route, shared in configurations do
                for owners in [1; 3] do
                    for releaseAll in [false; true] do
                        for nestedTarget, nestedTask in [false, false; true, false; false, true; true, true] do
                            check failures levels route shared owners releaseAll true (Some(error nestedTarget "target")) (Some(error nestedTask "clear"))
            Expect.isEmpty failures (String.concat "\n" failures)

        let lifetimeControls () =
            let failures = ResizeArray<string>()
            for levels, route, shared in configurations @ [0, Plain, true; 1, Plain, true; 2, Plain, true] do
                for owners in [1; 3] do
                    for releaseAll in [false; true] do
                        for materialized in [false; true] do
                            check failures levels route shared owners releaseAll materialized None None
                        check failures levels route shared owners releaseAll false (Some(error false "unmaterialized target")) None
                        if route = Plain then check failures levels route shared owners releaseAll true (Some(error false "plain target")) None
            Expect.isEmpty failures (String.concat "\n" failures)

    let tests (target : TestTarget) =
        [
            "Target failures release compiled clears", Cases.targetFailure
            "Task failures preserve their exception",  Cases.taskFailure
            "Combined failures retain both stages",    Cases.combinedFailure
            "Deferred and caller-owned lifetimes",     Cases.lifetimeControls
        ]
        |> prepareCasesCpu "RenderTo cleanup" target
