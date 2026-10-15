namespace Aardvark.Rendering.Tests.Other

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.SceneGraph.CSharp
open FSharp.Data.Adaptive
open Expecto

module SceneGraphCSharpAttributes =

    module private Cases =

        type private Rate = Vertex | Instance

        type private IMatrix =
            abstract Run<'T when 'T : struct and 'T : equality> : 'T[] -> unit

        let private forTypes (matrix : IMatrix) =
            matrix.Run [|0; -17; 42|]
            matrix.Run [|0.0f; -3.5f; 7.25f|]
            matrix.Run [|V3f.Zero; V3f.IOO; V3f.OIO|]
            matrix.Run [|C4b.Red; C4b.Green; C4b.Blue|]
            matrix.Run [|M33f.Zero; M33f.Identity|]
            matrix.Run [|M44f.Zero; M44f.Identity|]
            matrix.Run [|M44d.Zero; M44d.Identity|]
            matrix.Run [|Trafo3d.Identity; Trafo3d.Translation(1.0, 2.0, 3.0)|]

        let private data (values : 'T[]) seed count =
            Array.init count (fun index -> values.[(index + seed) % values.Length])

        type private Producer<'T>(constant : bool, initial : 'T[]) =
            let state = cval initial
            let mutable calls = 0
            let input =
                if constant then AVal.delay (fun () -> calls <- calls + 1; initial)
                else AVal.custom (fun token -> calls <- calls + 1; state.GetValue token)
            member _.Input = input
            member _.Calls = calls
            member _.Replace value = transact (fun () -> state.Value <- value)

        let private semantic = Sym.ofString "DeferredAttribute"
        let private child() = Sg.RenderNode(3, IndexedGeometryMode.PointList) :> ISg

        let private apply rate child (input : aval<'T[]>) =
            match rate with
            | Vertex -> SceneGraphExtensions.VertexAttribute<'T>(child, semantic, input)
            | Instance -> SceneGraphExtensions.InstanceAttribute(child, semantic, input)

        let private applyUntyped rate child (input : aval<Array>) =
            match rate with
            | Vertex -> SceneGraphExtensions.VertexAttribute(child, semantic, input)
            | Instance -> SceneGraphExtensions.InstanceAttribute(child, semantic, input)

        let private view rate expectedChild elementType (node : ISg) context =
            let attributes, actualChild =
                match rate, node with
                | Vertex, (:? Sg.VertexAttributeApplicator as app) -> app.Values, AVal.force app.Child
                | Instance, (:? Sg.InstanceAttributeApplicator as app) -> app.Values, AVal.force app.Child
                | _ -> failtestf "%s: wrong applicator type" context
            Expect.isTrue (obj.ReferenceEquals(actualChild, expectedChild)) (context + ": child identity")
            Expect.equal (attributes |> Map.toList |> List.map fst) [semantic] (context + ": semantic binding")
            let result = attributes.[semantic]
            Expect.equal result.ElementType elementType (context + ": element metadata")
            Expect.equal (result.Offset, result.Stride, result.Normalized, result.IsSingleValue) (0, 0, true, false) (context + ": layout metadata")
            Expect.equal result.Buffer.ContentType typeof<IBuffer> (context + ": adaptive buffer type")
            result

        let private payload context (expected : Array) (view : BufferView) =
            let buffer = AVal.force view.Buffer :?> ArrayBuffer
            Expect.isTrue (obj.ReferenceEquals(buffer.Data, expected)) (context + ": original array identity")
            Expect.equal buffer.ElementType (expected.GetType().GetElementType()) (context + ": payload element type")
            Expect.equal buffer.Data.Length expected.Length (context + ": payload length")
            buffer

        let private expectError context expected action =
            let mutable failure = None
            try action()
            with error -> failure <- Some error
            Expect.isSome failure (context + ": producer exception is observable")
            Expect.isTrue (obj.ReferenceEquals(failure.Value, expected)) (context + ": producer exception identity")

        let constructionAndFirstUse () =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : struct and 'T : equality>(values : 'T[]) =
                    for rate in [Vertex; Instance] do
                        for constant in [true; false] do
                            for count in [0; 1; 7] do
                                for replaceBeforeUse in (if constant then [false] else [false; true]) do
                                    let initial = data values 0 count
                                    let producer = Producer(constant, initial)
                                    let leaf = child()
                                    let context = sprintf "type=%A, rate=%A, constant=%b, count=%d, replace=%b" typeof<'T> rate constant count replaceBeforeUse
                                    let node = apply rate leaf producer.Input
                                    Expect.equal producer.Calls 0 (context + ": construction is deferred")
                                    let attribute = view rate leaf typeof<'T> node context
                                    Expect.equal attribute.Buffer.IsConstant constant (context + ": constant classification")
                                    Expect.equal producer.Calls 0 (context + ": metadata does not evaluate the producer")
                                    let expected =
                                        if replaceBeforeUse then
                                            producer.Replace (data values 1 3)
                                            let latest = data values 2 2
                                            producer.Replace latest
                                            latest
                                        else initial
                                    Expect.equal producer.Calls 0 (context + ": pre-use transactions remain deferred")
                                    let first = payload context expected attribute
                                    Expect.equal producer.Calls 1 (context + ": first buffer read evaluates once")
                                    for read = 1 to 3 do
                                        Expect.isTrue (obj.ReferenceEquals(AVal.force attribute.Buffer, first)) (sprintf "%s, read=%d: existing adaptive caching" context read)
                                        Expect.equal producer.Calls 1 (context + ": unchanged reads stay cached")
            }

        let throwingProducers () =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : struct and 'T : equality>(_ : 'T[]) =
                    for rate in [Vertex; Instance] do
                        for constant in [true; false] do
                            let context = sprintf "type=%A, rate=%A, constant=%b" typeof<'T> rate constant
                            let expected = InvalidOperationException context
                            let mutable calls = 0
                            let produce() : 'T[] = calls <- calls + 1; raise expected
                            let input = if constant then AVal.delay produce else AVal.custom (fun _ -> produce())
                            let leaf = child()
                            let node = apply rate leaf input
                            let attribute = view rate leaf typeof<'T> node context
                            Expect.equal calls 0 (context + ": failing producer does not abort construction or metadata")
                            expectError context expected (fun () -> AVal.force attribute.Buffer |> ignore)
                            Expect.equal calls 1 (context + ": failure occurs on first buffer read")
            }

        let replacementAndResizing () =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : struct and 'T : equality>(values : 'T[]) =
                    for rate in [Vertex; Instance] do
                        let initial = data values 0 1
                        let producer = Producer(false, initial)
                        let leaf = child()
                        let context = sprintf "type=%A, rate=%A" typeof<'T> rate
                        let node = apply rate leaf producer.Input
                        let attribute = view rate leaf typeof<'T> node context
                        Expect.equal producer.Calls 0 (context + ": initial payload is deferred")
                        let mutable reads = 0
                        let mutable previous : ArrayBuffer option = None
                        for frame, expected in [initial; data values 1 7; data values 0 0; data values 2 3; data values 1 1] |> List.indexed do
                            if frame > 0 then producer.Replace expected
                            Expect.equal producer.Calls reads (context + ": transaction does not evaluate the array")
                            let current = payload (sprintf "%s, frame=%d" context frame) expected attribute
                            reads <- reads + 1
                            Expect.equal producer.Calls reads (context + ": one evaluation per dirty read")
                            match previous with
                            | Some old -> Expect.isFalse (obj.ReferenceEquals(old, current)) (context + ": replacement creates a new ArrayBuffer")
                            | None -> ()
                            previous <- Some current
                            Expect.isTrue (obj.ReferenceEquals(AVal.force attribute.Buffer, current)) (context + ": repeated read retains adapted payload")
                            Expect.equal producer.Calls reads (context + ": repeated read does not revisit the producer")
                            view rate leaf typeof<'T> node context |> ignore
            }

        type private Base(value : int) = member _.Value = value
        type private Derived(value : int) = inherit Base(value)

        let private referenceCase (initial : 'T[]) runtimeType (replacement : 'T[]) =
            for rate in [Vertex; Instance] do
                for constant in [true; false] do
                    let producer = Producer(constant, initial)
                    let leaf = child()
                    let context = sprintf "declared=%A, runtime=%A, rate=%A, constant=%b" typeof<'T> runtimeType rate constant
                    let node = apply rate leaf producer.Input
                    Expect.equal producer.Calls 1 (context + ": reference inference remains eager")
                    let attribute = view rate leaf runtimeType node context
                    payload context initial attribute |> ignore
                    Expect.equal producer.Calls 1 (context + ": first payload uses the evaluated array")
                    if not constant then
                        producer.Replace replacement
                        payload context replacement attribute |> ignore
                        Expect.equal producer.Calls 2 (context + ": reference replacement is adaptive")
                        Expect.equal attribute.ElementType runtimeType (context + ": metadata retains the initial runtime type")

        let referenceCovariance () =
            referenceCase [|"first"; "second"|] typeof<string> [|"replacement"|]
            referenceCase (box [|"first"; "second"|] :?> obj[]) typeof<string> [|box 42|]
            referenceCase (box [|Derived 1; Derived 2|] :?> Base[]) typeof<Derived> [|Base 3|]
            referenceCase (box Array.empty<string> :?> obj[]) typeof<string> [|box 42|]
            for rate in [Vertex; Instance] do
                let expected = InvalidOperationException "reference inference"
                let input : aval<string[]> = AVal.delay (fun () -> raise expected)
                expectError (sprintf "rate=%A: reference construction" rate) expected (fun () -> apply rate (child()) input |> ignore)

        let untypedCompatibility () =
            for rate in [Vertex; Instance] do
                for constant in [true; false] do
                    for initial in [[||] :> Array; [|1; 2; 3|] :> Array; [|V3f.IOO|] :> Array; [|M44f.Identity|] :> Array] do
                        let state = cval initial
                        let mutable calls = 0
                        let input =
                            if constant then AVal.delay (fun () -> calls <- calls + 1; initial)
                            else AVal.custom (fun token -> calls <- calls + 1; state.GetValue token)
                        let leaf = child()
                        let context = sprintf "untyped=%A, rate=%A, constant=%b" (initial.GetType()) rate constant
                        let node = applyUntyped rate leaf input
                        Expect.equal calls 1 (context + ": untyped runtime discovery remains eager")
                        let attribute = view rate leaf (initial.GetType().GetElementType()) node context
                        payload context initial attribute |> ignore
                        Expect.equal calls 1 (context + ": untyped first read remains cached")
                        if not constant then
                            let replacement = [|4.0; 5.0|] :> Array
                            transact (fun () -> state.Value <- replacement)
                            payload context replacement attribute |> ignore
                            Expect.equal calls 2 (context + ": untyped replacement remains adaptive")
                            Expect.equal attribute.ElementType (initial.GetType().GetElementType()) (context + ": untyped initial metadata is preserved")
                    let expected = InvalidOperationException "untyped inference"
                    let input : aval<Array> = if constant then AVal.delay (fun () -> raise expected) else AVal.custom (fun _ -> raise expected)
                    expectError (sprintf "rate=%A, constant=%b: untyped construction" rate constant) expected (fun () -> applyUntyped rate (child()) input |> ignore)
                    let nullInput : aval<Array> = AVal.constant null
                    Expect.throwsT<NullReferenceException> (fun () -> applyUntyped rate (child()) nullInput |> ignore) "untyped null-array construction remains rejected"

    let tests (target : TestTarget) =
        [
            "Value-array construction and first use",         Cases.constructionAndFirstUse
            "Producer failures are deferred",                 Cases.throwingProducers
            "Adaptive replacement resizing and identity",     Cases.replacementAndResizing
            "Reference covariance retains runtime inference", Cases.referenceCovariance
            "Untyped overload compatibility",                 Cases.untypedCompatibility
        ]
        |> prepareCasesCpu "SceneGraph CSharp attributes" target
