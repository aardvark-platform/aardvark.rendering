namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
open Expecto

module SceneGraphUniforms =
    module private Cases =
        let private semantic = Sym.ofString "PresenceProbe"
        let private hasSemantic = Sym.ofString "HasPresenceProbe"
        let private ordinary = Sym.ofString "OrdinaryProbe"
        let private hasOrdinary = Sym.ofString "HasOrdinaryProbe"
        let private missing = Sym.ofString "HasMissingProbe"

        let private array length = BufferView(Array.init length (fun i -> V3f(float32 i, 2.0f, 3.0f)))
        let private single() = BufferView(SingleValueBuffer(AVal.constant V3f.Zero) :> ISingleValueBuffer)

        let private withObject indirect vertex instance uniforms action =
            let call = DrawCallInfo(FaceVertexCount = 0, InstanceCount = 1)
            let sg =
                if indirect then Sg.indirectDraw' IndexedGeometryMode.PointList (IndirectBuffer.ofArray [| call |])
                else Sg.render IndexedGeometryMode.PointList call :> ISg
            let sg = match vertex with Some view -> sg |> Sg.vertexBuffer semantic view | None -> sg
            let sg = match instance with Some view -> sg |> Sg.instanceBuffer semantic view | None -> sg
            let ro = (sg |> Sg.uniforms uniforms).RenderObjects(Ag.Scope.Root) |> ASet.force |> Seq.exactlyOne :?> RenderObject
            use pipeline = (PipelineState.ofScope ro.AttributeScope).GlobalUniforms
            try
                match ro.DrawCalls with
                | DrawCalls.Direct _ -> Expect.isFalse indirect "direct node must use the render-object factory"
                | DrawCalls.Indirect _ -> Expect.isTrue indirect "indirect node must use the render-object factory"
                action ro pipeline
            finally
                ro.Uniforms.Dispose()
                ro.VertexAttributes.Dispose()
                ro.InstanceAttributes.Dispose()

        let private lookup context (scope : Ag.Scope) name (provider : IUniformProvider) =
            match provider.TryGetUniform(scope, name) with
            | ValueSome value -> value
            | ValueNone -> failwith $"{context}: missing uniform {name}"

        let private checkPresence context expected scope name provider =
            for query in 0 .. 3 do
                let value = lookup context scope name provider
                match value with
                | :? aval<bool> as value -> Expect.equal (AVal.force value) expected $"{context}, query={query}: {name}"
                | _ -> failwith $"{context}: {name} must be a Boolean adaptive, got {value.ContentType}"

        let private checkAttribute context expected (provider : IAttributeProvider) =
            match expected, provider.TryGetAttribute semantic with
            | None, ValueNone -> ()
            | Some expected, ValueSome actual -> Expect.isTrue (obj.ReferenceEquals(expected, actual)) (context + ": original buffer view")
            | _ -> failwith $"{context}: attribute presence changed"

        let private collect (failures : ResizeArray<string>) context action =
            try action() with error -> failures.Add(context + ": " + error.Message)

        let arrays() =
            let failures = ResizeArray<string>()
            for indirect in [false; true] do
                for instance in [false; true] do
                    for length in [0; 3] do
                        let view = array length
                        let vertex, perInstance = if instance then None, Some view else Some view, None
                        let context = $"indirect={indirect}, instance={instance}, array length={length}"
                        withObject indirect vertex perInstance UniformProvider.Empty (fun ro pipeline ->
                            checkAttribute context vertex ro.VertexAttributes
                            checkAttribute context perInstance ro.InstanceAttributes
                            checkPresence (context + ": pipeline control") true ro.AttributeScope hasSemantic pipeline
                            collect failures context (fun () -> checkPresence context true ro.AttributeScope hasSemantic ro.Uniforms))
            Expect.isEmpty failures (String.concat "\n" failures)

        let absentAndSingle() =
            for indirect in [false; true] do
                for instance in [false; true] do
                    for view in [None; Some(single())] do
                        let vertex, perInstance = if instance then None, view else view, None
                        let context = $"indirect={indirect}, instance={instance}, single={view.IsSome}"
                        withObject indirect vertex perInstance UniformProvider.Empty (fun ro pipeline ->
                            checkAttribute context vertex ro.VertexAttributes
                            checkAttribute context perInstance ro.InstanceAttributes
                            for provider in [ro.Uniforms; pipeline] do
                                checkPresence context false ro.AttributeScope hasSemantic provider
                                checkPresence context false ro.AttributeScope missing provider)

        let uniformPriority() =
            for indirect in [false; true] do
                for label, view in ["missing", None; "single", Some(single()); "empty array", Some(array 0); "array", Some(array 3)] do
                    let context = $"indirect={indirect}, attribute={label}"
                    for initial in [false; true] do
                        let flag = cval initial
                        let value = cval 17
                        let uniforms = UniformProvider.ofList [hasSemantic, flag :> IAdaptiveValue; ordinary, value :> IAdaptiveValue]
                        let context = $"{context}, initial override={initial}"
                        withObject indirect view None uniforms (fun ro pipeline ->
                            for provider in [ro.Uniforms; pipeline] do
                                for _ in 0 .. 2 do
                                    Expect.isTrue (obj.ReferenceEquals(lookup context ro.AttributeScope hasSemantic provider, flag)) (context + ": exact override adaptive")
                                    Expect.isTrue (obj.ReferenceEquals(lookup context ro.AttributeScope ordinary provider, value)) (context + ": cached ordinary adaptive")
                                checkPresence context initial ro.AttributeScope hasSemantic provider
                                checkPresence context true ro.AttributeScope hasOrdinary provider
                            transact (fun () -> flag.Value <- not initial; value.Value <- 23)
                            for provider in [ro.Uniforms; pipeline] do
                                checkPresence context (not initial) ro.AttributeScope hasSemantic provider
                                Expect.equal (lookup context ro.AttributeScope ordinary provider |> unbox<aval<int>> |> AVal.force) 23 (context + ": ordinary adaptive remains live"))
                    let value = AVal.constant 42
                    let uniforms = UniformProvider.ofList [semantic, value :> IAdaptiveValue]
                    withObject indirect None view uniforms (fun ro pipeline ->
                        for provider in [ro.Uniforms; pipeline] do
                            Expect.isTrue (obj.ReferenceEquals(lookup context ro.AttributeScope semantic provider, value)) (context + ": ordinary base uniform identity")
                            checkPresence (context + ": base uniform takes priority over attribute classification") true ro.AttributeScope hasSemantic provider)

        let attributePriority() =
            let failures = ResizeArray<string>()
            let values =
                [ "single before array", Some(single()), Some(array 3), false
                  "array before single", Some(array 3), Some(single()), true
                  "empty array before array", Some(array 0), Some(array 3), true
                  "instance fallback", None, Some(array 3), true
                  "array before array", Some(array 3), Some(array 0), true
                  "single before single", Some(single()), Some(single()), false ]
            for indirect in [false; true] do
                for label, vertex, instance, expected in values do
                    let context = $"indirect={indirect}, {label}"
                    withObject indirect vertex instance UniformProvider.Empty (fun ro pipeline ->
                        let expectedView, expectedInstance = match vertex with Some v -> v, false | None -> instance.Value, true
                        match ro.TryGetAttribute semantic with
                        | ValueSome struct (actual, isInstance) ->
                            Expect.isTrue (obj.ReferenceEquals(actual, expectedView)) (context + ": vertex-first selected view")
                            Expect.equal isInstance expectedInstance (context + ": selected attribute rate")
                        | ValueNone -> failwith (context + ": missing selected attribute")
                        checkPresence (context + ": pipeline control") expected ro.AttributeScope hasSemantic pipeline
                        collect failures context (fun () -> checkPresence context expected ro.AttributeScope hasSemantic ro.Uniforms))
            Expect.isEmpty failures (String.concat "\n" failures)

    let tests target =
        [
            "Array attributes report presence",                  Cases.arrays
            "Missing and single values remain absent",           Cases.absentAndSingle
            "Uniform overrides and fallback retain priority",    Cases.uniformPriority
            "Vertex attributes precede instance attributes",     Cases.attributePriority
        ]
        |> prepareCasesCpu "SceneGraphUniforms" target
