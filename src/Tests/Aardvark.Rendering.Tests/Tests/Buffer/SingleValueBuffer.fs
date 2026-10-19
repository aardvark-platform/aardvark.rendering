namespace Aardvark.Rendering.Tests.Buffer

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module SingleValueBufferTests =

    module private Cases =

        type private IMatrix =
            abstract Run<'T when 'T : unmanaged and 'T : equality> : 'T[] -> unit

        let private forTypes (matrix : IMatrix) =
            matrix.Run [|0uy; 17uy; 255uy|]
            matrix.Run [|Int32.MinValue; 0; Int32.MaxValue|]
            matrix.Run [|-3.5f; 0.0f; 7.25f|]
            matrix.Run [|-3.5; 0.0; 7.25|]
            matrix.Run [|V2i(-2, 3); V2i.Zero; V2i(7, -5)|]
            matrix.Run [|V3f(-2.0f, 3.0f, 4.0f); V3f.Zero; V3f(7.0f, -5.0f, 2.0f)|]
            matrix.Run [|C4b(1, 2, 3, 4); C4b.Black; C4b(255, 128, 64, 32)|]
            matrix.Run [|C4f(0.1f, 0.2f, 0.3f, 0.4f); C4f.Black; C4f(1.0f, 0.5f, 0.25f, 0.75f)|]

        type private Fixture<'T when 'T : unmanaged>(constant : bool, initial : 'T) =
            let state = cval initial
            let mutable evaluations = 0
            let input =
                if constant then AVal.delay (fun () -> evaluations <- evaluations + 1; initial)
                else AVal.custom (fun token -> evaluations <- evaluations + 1; state.GetValue token)
            let buffer = SingleValueBuffer<'T>(input)
            member _.Input = input
            member _.Buffer = buffer
            member _.Untyped = buffer :> IAdaptiveValue
            member _.Evaluations = evaluations
            member _.Constant = constant
            member _.Set value = transact (fun () -> state.Value <- value)

        let private forInputs (values : 'T[]) run =
            for constant in [true; false] do
                for initial in values do
                    let fixture = Fixture(constant, initial)
                    let context = sprintf "type=%A, constant=%b, initial=%A" typeof<'T> constant initial
                    run context fixture initial

        let private steps (values : 'T[]) (fixture : Fixture<'T>) initial =
            if fixture.Constant then [|initial; initial|]
            else Array.append [|initial|] values

        let private checkPayload context (expected : 'T) (payload : obj) =
            Expect.isTrue (payload :? ArrayBuffer) (context + ": payload is an ArrayBuffer")
            let buffer = payload :?> ArrayBuffer
            Expect.equal buffer.ElementType typeof<'T> (context + ": array element type")
            Expect.equal (buffer.Data :?> 'T[]) [|expected|] (context + ": single element payload")
            Expect.equal buffer.SizeInBytes (uint64 typeof<'T>.CLRSize) (context + ": payload byte size")

        let private objectCasters = [
            "AVal", AVal.cast<obj>
            "AdaptiveResource", AdaptiveResource.cast<obj>
        ]

        // Adaptive casts permit declared upcasts, not IBuffer-to-INativeBuffer narrowing.
        // Narrow the materialized payload explicitly while retaining that existing policy.
        let private nativeCasters = [
            "AVal", fun value -> AVal.cast<obj> value |> AVal.map unbox<INativeBuffer>
            "AdaptiveResource", fun value -> AdaptiveResource.cast<obj> value |> AdaptiveResource.map unbox<INativeBuffer>
        ]

        let visitorDispatch () =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : unmanaged and 'T : equality>(values : 'T[]) =
                    forInputs values (fun context fixture _ ->
                        let mutable calls = 0
                        let marker = obj()
                        let visitor = { new IAdaptiveValueVisitor<obj> with
                            member _.Visit<'U>(value : aval<'U>) =
                                calls <- calls + 1
                                Expect.equal typeof<'U> typeof<IBuffer> (context + ": visitor type matches ContentType")
                                Expect.isTrue (obj.ReferenceEquals(value, fixture.Buffer)) (context + ": visitor receives the wrapper")
                                marker
                        }
                        Expect.equal fixture.Untyped.ContentType typeof<IBuffer> (context + ": declared content type")
                        Expect.equal fixture.Untyped.IsConstant fixture.Constant (context + ": constant forwarding")
                        for read = 1 to 3 do
                            Expect.isTrue (obj.ReferenceEquals(fixture.Untyped.Accept visitor, marker)) (context + ": visitor return identity")
                            Expect.equal calls read (context + ": exactly one invocation per Accept")
                            Expect.equal fixture.Evaluations 0 (context + ": visiting does not evaluate the scalar")
                        let expected = InvalidOperationException context
                        let throwing = { new IAdaptiveValueVisitor<obj> with
                            member _.Visit<'U>(_ : aval<'U>) = calls <- calls + 1; raise expected
                        }
                        let mutable failure = None
                        try fixture.Untyped.Accept throwing |> ignore
                        with error -> failure <- Some error
                        Expect.isTrue (obj.ReferenceEquals(failure.Value, expected)) (context + ": visitor exception identity")
                        Expect.equal calls 4 (context + ": throwing visitor invoked once")
                        Expect.equal fixture.Evaluations 0 (context + ": throwing visitor remains lazy")
                    )
            }

        let objectCasts () =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : unmanaged and 'T : equality>(values : 'T[]) =
                    forInputs values (fun context fixture initial ->
                        let casts = objectCasters |> List.map (fun (name, cast) -> name, cast fixture.Untyped)
                        Expect.equal fixture.Evaluations 0 (context + ": object cast construction is lazy")
                        for value in steps values fixture initial do
                            fixture.Set value
                            for name, cast in casts do
                                let label = sprintf "%s, cast=%s, value=%A" context name value
                                Expect.equal cast.ContentType typeof<obj> (label + ": cast type")
                                for _ = 1 to 2 do checkPayload label value (AVal.force cast)
                    )
            }

        let nativeCasts () =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : unmanaged and 'T : equality>(values : 'T[]) =
                    forInputs values (fun context fixture initial ->
                        for name, cast in ["AVal", AVal.cast<INativeBuffer>; "AdaptiveResource", AdaptiveResource.cast<INativeBuffer>] do
                            Expect.throwsT<InvalidCastException> (fun () -> cast fixture.Untyped |> ignore) (context + ", cast=" + name + ": declared buffer narrowing remains rejected")
                        let casts = nativeCasters |> List.map (fun (name, cast) -> name, cast fixture.Untyped)
                        Expect.equal fixture.Evaluations 0 (context + ": native cast construction is lazy")
                        for value in steps values fixture initial do
                            fixture.Set value
                            for name, cast in casts do
                                let label = sprintf "%s, cast=%s, value=%A" context name value
                                let payload = AVal.force cast
                                checkPayload label value payload
                                Expect.equal (payload.ToArray<'T>()) [|value|] (label + ": native storage contents")
                    )
            }

        let scalarCastsRejected () =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : unmanaged and 'T : equality>(values : 'T[]) =
                    forInputs values (fun context fixture _ ->
                        for name, cast in ["AVal", AVal.cast<'T>; "AdaptiveResource", AdaptiveResource.cast<'T>] do
                            Expect.throwsT<InvalidCastException> (fun () -> cast fixture.Untyped |> ignore) (context + ", cast=" + name + ": buffer is not its scalar")
                        for name, cast in ["AVal", AVal.cast<string>; "AdaptiveResource", AdaptiveResource.cast<string>] do
                            Expect.throwsT<InvalidCastException> (fun () -> cast fixture.Untyped |> ignore) (context + ", cast=" + name + ": unrelated scalar is rejected")
                        Expect.equal fixture.Evaluations 0 (context + ": invalid casts do not evaluate the scalar")
                    )
            }

        let readsAndReplication () =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : unmanaged and 'T : equality>(values : 'T[]) =
                    forInputs values (fun context fixture initial ->
                        let scalar = (fixture.Buffer :> ISingleValueBuffer).Value
                        Expect.isTrue (obj.ReferenceEquals(scalar, fixture.Input)) (context + ": explicit scalar identity")
                        Expect.isTrue (obj.ReferenceEquals(fixture.Buffer.Value, fixture.Input)) (context + ": typed scalar identity")
                        Expect.isTrue (obj.ReferenceEquals(fixture.Untyped.Outputs, fixture.Input.Outputs)) (context + ": dependency outputs forwarding")
                        Expect.equal (fixture.Buffer.GetHashCode()) (fixture.Input.GetHashCode()) (context + ": hash forwarding")
                        Expect.isTrue (fixture.Buffer.Equals(SingleValueBuffer<'T>(fixture.Input))) (context + ": equal input identity")
                        Expect.isFalse (fixture.Buffer.Equals null) (context + ": null is not equal")
                        let views = [|
                            BufferView(fixture.Buffer :> ISingleValueBuffer)
                            BufferView(fixture.Buffer :> aval<IBuffer>, typeof<'T>, Int32.MaxValue, Int32.MaxValue, false)
                        |]
                        let downloads = [|
                            for view in views do
                                Expect.isTrue view.IsSingleValue (context + ": single-value view")
                                Expect.isTrue (obj.ReferenceEquals(view.SingleValue.Value, scalar)) (context + ": view keeps explicit scalar")
                                for start, count in [-1, -1; 0, 0; 1, 1; Int32.MaxValue, 7] do
                                    yield count, BufferView.download start count view
                        |]
                        for value in steps values fixture initial do
                            fixture.Set value
                            let label = sprintf "%s, value=%A" context value
                            checkPayload label value ((fixture.Buffer :> aval<IBuffer>).GetValue AdaptiveToken.Top)
                            checkPayload label value (fixture.Untyped.GetValueUntyped AdaptiveToken.Top)
                            let mutable calls = 0
                            let visited = scalar.Accept { new IAdaptiveValueVisitor<obj> with
                                member _.Visit<'U>(input : aval<'U>) =
                                    calls <- calls + 1
                                    Expect.equal typeof<'U> typeof<'T> (label + ": explicit scalar visitor type")
                                    Expect.isTrue (obj.ReferenceEquals(input, fixture.Input)) (label + ": explicit scalar visitor identity")
                                    box (AVal.force input)
                            }
                            Expect.equal calls 1 (label + ": explicit scalar visitor called once")
                            Expect.equal (unbox<'T> visited) value (label + ": explicit scalar visitor value")
                            Expect.equal (unbox<'T> (scalar.GetValueUntyped AdaptiveToken.Top)) value (label + ": untyped scalar value")
                            for count, download in downloads do
                                let expected = Array.replicate (if count < 0 then 1 else count) value
                                Expect.equal (AVal.force download :?> 'T[]) expected (sprintf "%s, count=%d: scalar replication" label count)
                    )
            }

    let tests (target : TestTarget) =
        [
            "Visitor identity laziness and errors",  Cases.visitorDispatch
            "Object casts return buffer payloads",   Cases.objectCasts
            "Native payload casts retain storage",   Cases.nativeCasts
            "Scalar casts are rejected",             Cases.scalarCastsRejected
            "Typed scalar and replication controls", Cases.readsAndReplication
        ]
        |> prepareCasesCpu "SingleValueBuffer" target
