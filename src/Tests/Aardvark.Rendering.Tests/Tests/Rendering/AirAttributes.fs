namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Expecto

module AirAttributes =

    module private Cases =

        type private Provider(bindings : (Symbol * BufferView) list) =
            member val Disposals = 0 with get, set
            interface IAttributeProvider with
                member _.All = bindings :> seq<_>
                member _.TryGetAttribute name =
                    bindings |> List.tryPick (fun (key, view) -> if key = name then Some view else None) |> Option.toValueOption
                member x.Dispose() = x.Disposals <- x.Disposals + 1

        let private unreadable (reads : int ref) offset =
            let buffer : aval<IBuffer> =
                AVal.custom (fun _ ->
                    reads.Value <- reads.Value + 1
                    failwith "Attribute buffer contents must not be evaluated")
            BufferView(buffer, typeof<int>, offset, 8)

        let private stateFor (vertex : IAttributeProvider) (instance : IAttributeProvider) =
            let parent = RenderObject()
            parent.VertexAttributes <- vertex
            parent.InstanceAttributes <- instance
            { isActive = parent.IsActive
              drawCalls = parent.DrawCalls
              mode = parent.Mode
              surface = parent.Surface
              blendState = parent.BlendState
              depthState = parent.DepthState
              stencilState = parent.StencilState
              rasterizerState = parent.RasterizerState
              indices = parent.Indices
              instanceAttributes = Map.empty
              vertexAttributes = Map.empty
              uniforms = Map.empty
              trafos = []
              scope = parent
              final = [] }

        let private bind vertex bulk (bindings : (Symbol * BufferView) list) =
            if vertex then
                if bulk then Air.BindVertexBuffers bindings
                else air { for key, view in bindings do do! Air.BindVertexBuffer(key, view) }
            else
                if bulk then Air.BindInstanceBuffers bindings
                else air { for key, view in bindings do do! Air.BindInstanceBuffer(key, view) }

        let private draw instanced =
            if instanced then Air.DrawInstanced(2, 3) else Air.Draw(3)

        let private provider vertex (state : AirState) =
            let renderObject = List.last state.final :?> RenderObject
            if vertex then renderObject.VertexAttributes else renderObject.InstanceAttributes

        let private expected (local : (Symbol * BufferView) list) (inherited : IAttributeProvider) =
            // The union is an enumeration oracle only; it must not dispose borrowed inputs.
            (AttributeProvider.union (AttributeProvider.ofMap (Map.ofList local)) inherited).All |> Seq.toArray

        let private compare (errors : ResizeArray<string>) context (actual : (Symbol * BufferView)[]) (expected : (Symbol * BufferView)[]) =
            if actual.Length <> expected.Length then errors.Add $"{context}: expected {expected.Length} bindings, got {actual.Length}"
            if (actual |> Array.map fst |> Set.ofArray |> Set.count) <> actual.Length then errors.Add (context + ": duplicate symbols")
            for i in 0 .. min actual.Length expected.Length - 1 do
                let key, view = actual.[i]
                let expectedKey, expectedView = expected.[i]
                if key <> expectedKey then errors.Add $"{context}: entry {i}, expected symbol {expectedKey}, got {key}"
                if not (obj.ReferenceEquals(view, expectedView)) then errors.Add $"{context}: entry {i}, BufferView identity changed"

        let private lookup context (provider : IAttributeProvider) bindings =
            for key, view in bindings do
                match provider.TryGetAttribute key with
                | ValueSome actual -> Expect.isTrue (obj.ReferenceEquals(actual, view)) $"{context}: lookup identity for {key}"
                | _ -> failtestf "%s: missing lookup for %A" context key
            Expect.equal (provider.TryGetAttribute (Sym.ofString "AirMissing")) ValueNone (context + ": absent lookup")

        let localAndInherited() =
            let errors = ResizeArray<string>()
            let reads = ref 0
            let views = Array.init 7 (fun i -> unreadable reads (i * 4))
            let shared, localKey, inheritedKey, otherKey = Sym.ofString "AirShared", Sym.ofString "AirLocal", Sym.ofString "AirInherited", Sym.ofString "AirOther"
            let layouts =
                [ "empty", [], []
                  "local", [shared, views.[0]], []
                  "inherited", [], [shared, views.[1]]
                  "distinct", [localKey, views.[0]], [inheritedKey, views.[1]]
                  "overlap", [shared, views.[0]], [shared, views.[1]; inheritedKey, views.[2]]
                  "duplicates", [shared, views.[0]; localKey, views.[1]; shared, views.[2]],
                                [shared, views.[3]; inheritedKey, views.[4]; inheritedKey, views.[5]; otherKey, views.[6]] ]
            for vertex in [false; true] do
                for bulk in [false; true] do
                    for name, local, inheritedBindings in layouts do
                        let inherited = new Provider(inheritedBindings)
                        let input = inherited :> IAttributeProvider
                        let mutable state = stateFor (if vertex then input else AttributeProvider.Empty) (if vertex then AttributeProvider.Empty else input)
                        (bind vertex bulk local).RunUnit(&state)
                        (draw bulk).RunUnit(&state)
                        let actual = provider vertex state
                        let expected = expected local input
                        let context = $"vertex={vertex}, bulk={bulk}, layout={name}"
                        lookup context actual expected
                        let sequence = actual.All
                        for iteration in 1 .. 3 do
                            compare errors (context + $", enumeration={iteration}") (Seq.toArray sequence) expected
                        actual.Dispose()
                        Expect.equal inherited.Disposals 0 (context + ": inherited provider remains borrowed")
            Expect.equal reads.Value 0 "Construction lookup and enumeration must not evaluate buffer contents"
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

        let snapshotsAndEnumerators() =
            let errors = ResizeArray<string>()
            let reads = ref 0
            let views = Array.init 6 (fun i -> unreadable reads (i * 4))
            let shared, added, inheritedKey = Sym.ofString "AirShared", Sym.ofString "AirAdded", Sym.ofString "AirInherited"
            for vertex in [false; true] do
                for bulk in [false; true] do
                    let inherited = new Provider([shared, views.[4]; inheritedKey, views.[5]])
                    let input = inherited :> IAttributeProvider
                    let mutable state = stateFor (if vertex then input else AttributeProvider.Empty) (if vertex then AttributeProvider.Empty else input)
                    let context = $"vertex={vertex}, bulk={bulk}"
                    let first = [shared, views.[0]]
                    (bind vertex bulk first).RunUnit(&state)
                    (draw false).RunUnit(&state)
                    let firstProvider = provider vertex state
                    let sequence = firstProvider.All
                    use pending = sequence.GetEnumerator()
                    let prefix = if pending.MoveNext() then [|pending.Current|] else [||]
                    let second = [shared, views.[1]; added, views.[2]]
                    (bind vertex bulk second).RunUnit(&state)
                    (draw true).RunUnit(&state)
                    let secondProvider = provider vertex state
                    let third = [shared, views.[3]; added, views.[2]]
                    (bind vertex bulk [shared, views.[3]]).RunUnit(&state)
                    (draw false).RunUnit(&state)
                    let thirdProvider = provider vertex state
                    let remainder = [| while pending.MoveNext() do yield pending.Current |]
                    compare errors (context + ": partially consumed earlier enumeration") (Array.append prefix remainder) (expected first input)
                    for label, result, local in ["first", firstProvider, first; "second", secondProvider, second; "third", thirdProvider, third] do
                        let expected = expected local input
                        lookup (context + ", " + label) result expected
                        compare errors (context + ", " + label) (Seq.toArray result.All) expected
                    compare errors (context + ": deferred first sequence") (Seq.toArray sequence) (expected first input)
                    let expected = expected second input
                    let sequence = secondProvider.All
                    use left = sequence.GetEnumerator()
                    use right = sequence.GetEnumerator()
                    let leftValues, rightValues = ResizeArray(), ResizeArray()
                    for _ in 0 .. expected.Length do
                        if left.MoveNext() then leftValues.Add left.Current
                        if right.MoveNext() then rightValues.Add right.Current
                    compare errors (context + ": first concurrent enumerator") (leftValues.ToArray()) expected
                    compare errors (context + ": second concurrent enumerator") (rightValues.ToArray()) expected
            Expect.equal reads.Value 0 "Draw snapshots and iterator state require metadata only"
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

        let metadataAndOwnership() =
            for vertex in [false; true] do
                let reads = ref 0
                let local, inheritedView = unreadable reads 0, unreadable reads 4
                let key = Sym.ofString "AirShared"
                let inheritedKey = Sym.ofString "AirInherited"
                let inherited = new Provider([key, inheritedView; inheritedKey, inheritedView])
                let other = new Provider([])
                let input, otherInput = inherited :> IAttributeProvider, other :> IAttributeProvider
                try
                    let mutable state = stateFor (if vertex then input else otherInput) (if vertex then otherInput else input)
                    (bind vertex false [key, local]).RunUnit(&state)
                    (draw true).RunUnit(&state)
                    let result = provider vertex state
                    lookup $"vertex={vertex}" result [key, local; inheritedKey, inheritedView]
                    for _ in 1 .. 3 do result.All |> Seq.iter (fun (_, view) -> Expect.equal view.ElementType typeof<int> "Only view metadata is needed")
                    for _ in 1 .. 2 do
                        result.Dispose()
                        (provider (not vertex) state).Dispose()
                    Expect.equal (inherited.Disposals, other.Disposals) (0, 0) "Draw providers do not own inherited providers"
                    lookup $"vertex={vertex}, after disposal" result [key, local; inheritedKey, inheritedView]
                    result.All |> Seq.toArray |> ignore
                    Expect.equal reads.Value 0 "Enumeration and non-owning disposal do not evaluate adaptive buffers"
                    Expect.throws (fun () -> AVal.force local.Buffer |> ignore) "Fixture rejects actual content evaluation"
                    Expect.equal reads.Value 1 "The fixture detects buffer evaluation"
                finally
                    input.Dispose()
                    otherInput.Dispose()
                Expect.equal (inherited.Disposals, other.Disposals) (1, 1) "Only the explicit owner disposes inherited providers"

    let tests (target : TestTarget) =
        [
            "Local and inherited bindings agree with lookup", Cases.localAndInherited
            "Draw snapshots and iterators remain independent", Cases.snapshotsAndEnumerators
            "Metadata access and borrowed lifetimes",          Cases.metadataAndOwnership
        ]
        |> prepareCasesCpu "Air attributes" target
