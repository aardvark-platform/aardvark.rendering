namespace Aardvark.Rendering.Tests.Other

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module ProviderDisposal =

    module private Cases =
        type private Failure =
            | Original of exn * string
            | Together of Failure * Failure

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwProvider (error : exn) : unit = raise error

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwCallback (error : exn) : unit = raise error

        let private errors kind count =
            let shared = InvalidOperationException("shared failure") :> exn
            Array.init count (fun i ->
                match kind with
                | 0 -> InvalidOperationException($"stage {i}") :> exn
                | 1 -> AggregateException($"existing {i}", [| shared; AggregateException(shared) :> exn |]) :> exn
                | _ -> shared
            )

        let private failure (errors : exn[]) mask index =
            if mask &&& (1 <<< index) = 0 then None else Some errors.[index]

        let private invoke (events : ResizeArray<int>) id callback error =
            events.Add id
            match error with
            | Some error -> if callback then throwCallback error else throwProvider error
            | None -> ()

        let private expected callback = Option.map (fun e -> Original(e, if callback then "throwCallback" else "throwProvider"))

        let private combine left right =
            match left, right with
            | Some l, Some r -> Some(Together(l, r))
            | Some _, None -> left
            | _ -> right

        let rec private checkFailure context expected (actual : exn) =
            match expected with
            | Original(error, _) ->
                Expect.isTrue (obj.ReferenceEquals(actual, error)) $"{context}: original exception identity"
            | Together(left, right) ->
                match actual with
                | :? AggregateException as aggregate ->
                    Expect.equal aggregate.InnerExceptions.Count 2 $"{context}: unflattened failure count"
                    checkFailure (context + "/first") left aggregate.InnerExceptions.[0]
                    checkFailure (context + "/second") right aggregate.InnerExceptions.[1]
                | _ -> failtestf "%s: expected two ordered failures, got %O" context actual

        let private check context (events : ResizeArray<int>) order expected (provider : IDisposable) =
            events.Clear()
            let actual = try provider.Dispose(); None with e -> Some e
            Expect.sequenceEqual events order $"{context}: every stage attempted once in invocation order"
            match expected, actual with
            | None, None -> ()
            | Some expected, Some actual ->
                checkFailure context expected actual
                match expected with
                | Original(_, origin) ->
                    Expect.isNotNull actual.StackTrace $"{context}: throwing stack"
                    Expect.stringContains actual.StackTrace origin $"{context}: original throwing frame"
                | _ -> ()
            | _ -> failtestf "%s: unexpected success/failure outcome %A" context actual

        let private provider uniform action : IDisposable =
            if uniform then
                { new IUniformProvider with
                    member _.Dispose() = action()
                    member _.TryGetUniform(_, _) = ValueNone }
            else
                { new IAttributeProvider with
                    member _.Dispose() = action()
                    member _.All = Seq.empty
                    member _.TryGetAttribute _ = ValueNone }

        let private union uniform (left : IDisposable) (right : IDisposable) : IDisposable =
            if uniform then UniformProvider.union (left :?> IUniformProvider) (right :?> IUniformProvider)
            else AttributeProvider.union (left :?> IAttributeProvider) (right :?> IAttributeProvider)

        let unions () =
            for uniform in [false; true] do
                for kind in 0 .. 2 do
                    for mask in 0 .. 3 do
                        let events = ResizeArray<int>()
                        let errors = errors kind 2
                        let stage i = failure errors mask i
                        let left = provider uniform (fun () -> invoke events 0 false (stage 0))
                        let right = provider uniform (fun () -> invoke events 1 false (stage 1))
                        let composed = union uniform left right
                        for call in 1 .. 3 do
                            check $"union uniform={uniform}, kind={kind}, mask={mask}, call={call}" events [0; 1]
                                  (combine (stage 0 |> expected false) (stage 1 |> expected false)) composed

        let wrappers () =
            for kind in 0 .. 2 do
                for mask in 0 .. 3 do
                    let events = ResizeArray<int>()
                    let errors = errors kind 2
                    let stage i = failure errors mask i
                    let wrapped = provider false (fun () -> invoke events 1 false (stage 1)) :?> IAttributeProvider
                    let composed = AttributeProvider.onDispose (fun () -> invoke events 0 true (stage 0)) wrapped
                    for call in 1 .. 3 do
                        check $"wrapper kind={kind}, mask={mask}, call={call}" events [0; 1]
                              (combine (stage 0 |> expected true) (stage 1 |> expected false)) composed

        let nested () =
            for uniform in [false; true] do
                let count = if uniform then 4 else 5
                for kind in 0 .. 2 do
                    for mask in 0 .. (1 <<< count) - 1 do
                        let events = ResizeArray<int>()
                        let errors = errors kind count
                        let stage i = failure errors mask i
                        let leaf i = provider uniform (fun () -> invoke events i false (stage i))
                        let e i = stage i |> expected false
                        let composed, result =
                            if uniform then
                                union true (union true (leaf 0) (leaf 1)) (union true (leaf 2) (leaf 3)),
                                combine (combine (e 0) (e 1)) (combine (e 2) (e 3))
                            else
                                let left = union false (leaf 1) (leaf 2) :?> IAttributeProvider
                                let wrapped = AttributeProvider.onDispose (fun () -> invoke events 0 true (stage 0)) left
                                union false wrapped (union false (leaf 3) (leaf 4)),
                                combine (combine (stage 0 |> expected true) (combine (e 1) (e 2))) (combine (e 3) (e 4))
                        for call in 1 .. 2 do
                            check $"nested uniform={uniform}, kind={kind}, mask={mask}, call={call}" events [0 .. count - 1] result composed

        let duplicates () =
            for uniform in [false; true] do
                for kind in 0 .. 2 do
                    for mask in 0 .. 3 do
                        let events = ResizeArray<int>()
                        let errors = errors kind 2
                        let mutable invocations = 0
                        let duplicate = provider uniform (fun () ->
                            let stage = invocations % 2
                            invocations <- invocations + 1
                            invoke events 7 false (failure errors mask stage))
                        let composed = union uniform duplicate duplicate
                        let result = combine (failure errors mask 0 |> expected false) (failure errors mask 1 |> expected false)
                        for call in 1 .. 3 do
                            check $"duplicate uniform={uniform}, kind={kind}, mask={mask}, call={call}" events [7; 7] result composed
                            Expect.equal invocations (2 * call) "A duplicate provider was skipped or made one-shot"

            for kind in 0 .. 2 do
                for mask in 0 .. 7 do
                    let events = ResizeArray<int>()
                    let errors = errors kind 3
                    let mutable callbacks = 0
                    let callback () =
                        let stage = callbacks % 2
                        callbacks <- callbacks + 1
                        invoke events 0 true (failure errors mask stage)
                    let wrapped = provider false (fun () -> invoke events 2 false (failure errors mask 2)) :?> IAttributeProvider
                    let composed = AttributeProvider.onDispose callback (AttributeProvider.onDispose callback wrapped)
                    let result = combine (failure errors mask 0 |> expected true)
                                         (combine (failure errors mask 1 |> expected true) (failure errors mask 2 |> expected false))
                    for call in 1 .. 3 do
                        check $"duplicate callbacks kind={kind}, mask={mask}, call={call}" events [0; 0; 2] result composed
                        Expect.equal callbacks (2 * call) "A repeated callback was skipped or made one-shot"

        let lookups () =
            let common, leftOnly, rightOnly, nullValue, missing =
                Symbol.Create "common", Symbol.Create "left", Symbol.Create "right", Symbol.Create "null", Symbol.Create "missing"
            let leftView, rightView = BufferView([| 1uy |]), BufferView([| 2uy |])
            let leftUniform, rightUniform = AVal.constant 17 :> IAdaptiveValue, AVal.constant 29 :> IAdaptiveValue
            let log = ResizeArray<string>()
            let mutable callbacks = 0
            let leftEntries = seq { log.Add "enumerate left"; yield common, leftView; yield common, rightView; yield leftOnly, leftView }
            let rightEntries = seq { log.Add "enumerate right"; yield common, rightView; yield rightOnly, rightView }
            let attribute left =
                { new IAttributeProvider with
                    member _.Dispose() = ()
                    member _.All =
                        log.Add(if left then "all left" else "all right")
                        if left then leftEntries else rightEntries
                    member _.TryGetAttribute name =
                        log.Add(if left then "attribute left" else "attribute right")
                        if left && name = nullValue then ValueSome Unchecked.defaultof<BufferView>
                        elif name = common || name = (if left then leftOnly else rightOnly) then ValueSome(if left then leftView else rightView)
                        else ValueNone }
            let left = attribute true
            let attributes = AttributeProvider.union left (attribute false)
            let wrapped = AttributeProvider.onDispose (fun () -> callbacks <- callbacks + 1) attributes
            let uniform left =
                { new IUniformProvider with
                    member _.Dispose() = ()
                    member _.TryGetUniform(scope, name) =
                        Expect.isTrue (obj.ReferenceEquals(scope, Ag.Scope.Root)) "Uniform lookup changed the scope"
                        log.Add(if left then "uniform left" else "uniform right")
                        if left && name = nullValue then ValueSome Unchecked.defaultof<IAdaptiveValue>
                        elif name = common || name = (if left then leftOnly else rightOnly) then ValueSome(if left then leftUniform else rightUniform)
                        else ValueNone }
            let uniforms = UniformProvider.union (uniform true) (uniform false)
            for name, side in [common, 0; leftOnly, 0; rightOnly, 1; nullValue, 2; missing, 3] do
                log.Clear()
                let actual = wrapped.TryGetAttribute name
                let expected = if side = 3 then ValueNone else ValueSome(if side = 2 then Unchecked.defaultof<BufferView> elif side = 0 then leftView else rightView)
                Expect.equal actual expected $"Attribute lookup {name}"
                Expect.sequenceEqual log (if side = 1 || side = 3 then ["attribute left"; "attribute right"] else ["attribute left"]) $"Attribute short circuit {name}"
                log.Clear()
                let actual = uniforms.TryGetUniform(Ag.Scope.Root, name)
                let expected = if side = 3 then ValueNone else ValueSome(if side = 2 then Unchecked.defaultof<IAdaptiveValue> elif side = 0 then leftUniform else rightUniform)
                Expect.equal actual expected $"Uniform lookup {name}"
                Expect.sequenceEqual log (if side = 1 || side = 3 then ["uniform left"; "uniform right"] else ["uniform left"]) $"Uniform short circuit {name}"
            log.Clear()
            let all = wrapped.All
            Expect.sequenceEqual log ["all left"; "all right"] "Enumeration must remain deferred after obtaining All"
            let entries = Seq.toArray all
            Expect.sequenceEqual (entries |> Array.map fst) [common; leftOnly; rightOnly] "Enumeration key order and duplicate removal"
            Expect.sequenceEqual (entries |> Array.map snd) [leftView; leftView; rightView] "Enumeration retains the first value"
            Expect.sequenceEqual log ["all left"; "all right"; "enumerate left"; "enumerate right"] "Enumeration visits both inputs in order"
            let forwarding = AttributeProvider.onDispose (fun () -> callbacks <- callbacks + 1) left
            Expect.isTrue (obj.ReferenceEquals(forwarding.All, leftEntries)) "Wrapper must forward the original sequence"
            Expect.equal callbacks 0 "Lookup or enumeration invoked a disposal callback"
            let constructedAttributes = AttributeProvider.ofList [common, leftView]
            let constructedUniforms = UniformProvider.ofList [common, leftUniform]
            Expect.equal (constructedAttributes.TryGetAttribute common) (ValueSome leftView) "Attribute constructor control"
            Expect.equal (constructedUniforms.TryGetUniform(Ag.Scope.Root, common)) (ValueSome leftUniform) "Uniform constructor control"
            Expect.isEmpty AttributeProvider.Empty.All "Empty attribute enumeration"
            Expect.equal (AttributeProvider.Empty.TryGetAttribute missing) ValueNone "Empty attribute lookup"
            Expect.equal (UniformProvider.Empty.TryGetUniform(Ag.Scope.Root, missing)) ValueNone "Empty uniform lookup"
            constructedAttributes.Dispose()
            constructedUniforms.Dispose()
            AttributeProvider.Empty.Dispose()
            UniformProvider.Empty.Dispose()

    let tests (target : TestTarget) =
        [
            "Unions attempt both sides after failures",           Cases.unions
            "Callback wrappers finish provider cleanup",          Cases.wrappers
            "Nested failures retain ordered exception structure", Cases.nested
            "Duplicate providers and repeated calls",             Cases.duplicates
            "Left-biased lookup and attribute enumeration",       Cases.lookups
        ]
        |> prepareCasesCpu "ProviderDisposal" target
