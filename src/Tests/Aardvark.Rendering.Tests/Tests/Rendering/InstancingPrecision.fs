namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module InstancingPrecision =

    module private Cases =
        let private routes =
            [| "typed", typeof<Trafo3d>; "untyped trafo", typeof<Trafo3d>
               "untyped double", typeof<M44d>; "untyped float", typeof<M44f> |]

        let private scene route typ (values : aval<Array>) inner =
            let child = Sg.fullScreenQuad |> Sg.effect [] |> Sg.trafo inner
            if route = "typed" then
                child |> Sg.instanced (values |> AVal.map (fun a -> a :?> Trafo3d[]))
            else
                child |> Sg.instanced' (Map.ofList ["ModelTrafo", (typ, values)])

        let private objects (sg : ISg) = sg.RenderObjects(Ag.Scope.Root)
        let private single (objects : aset<IRenderObject>) =
            AVal.force objects.Content |> Seq.exactlyOne :?> RenderObject

        let private views (ro : RenderObject) =
            let get semantic =
                match ro.InstanceAttributes.TryGetAttribute semantic with
                | ValueSome view -> view
                | _ -> failwithf "Missing instance attribute %A" semantic
            get DefaultSemantic.InstanceTrafo, get DefaultSemantic.InstanceTrafoInv

        let private matrices (view : BufferView) =
            Expect.equal view.ElementType typeof<M44f> "matrix element type"
            Expect.equal (view.Offset, view.Stride) (0, 0) "packed matrix layout"
            let buffer = AVal.force view.Buffer :?> ArrayBuffer
            Expect.equal buffer.ElementType typeof<M44f> "array element type"
            buffer.Data :?> M44f[]

        let private checkMatrices context (expected : M44f[]) (actual : M44f[]) =
            Expect.equal actual.Length expected.Length (context + ": matrix count")
            for i in 0 .. expected.Length - 1 do
                for row in 0 .. 3 do
                    for column in 0 .. 3 do
                        Expect.equal (BitConverter.SingleToInt32Bits actual.[i].[row, column])
                                     (BitConverter.SingleToInt32Bits expected.[i].[row, column])
                                     $"{context}, matrix={i}, entry={row}/{column}"

        let private expected (inner : Trafo3d) (values : Array) =
            let pairs =
                match values with
                | :? (Trafo3d[]) as a -> a |> Array.map (fun t -> t.Forward, t.Backward)
                | :? (M44d[]) as a -> a |> Array.map (fun m -> m, m.Inverse)
                | :? (M44f[]) as a -> a |> Array.map (fun m -> M44d m, M44d m.Inverse)
                | _ -> failwith "Invalid reference fixture"
            pairs |> Array.map (fun (f, _) -> M44f(f * inner.Forward)),
            pairs |> Array.map (fun (_, b) -> M44f(inner.Backward * b))

        let private check context inner values (ro : RenderObject) =
            let f, b = views ro
            let ef, eb = expected inner values
            checkMatrices (context + ": forward") ef (matrices f)
            checkMatrices (context + ": inverse") eb (matrices b)
            match ro.DrawCalls with
            | DrawCalls.Direct calls ->
                for call in AVal.force calls do
                    Expect.equal call.InstanceCount values.Length (context + ": draw count")
                    Expect.equal call.FirstInstance 0 (context + ": first instance")
            | _ -> failwith "Expected direct draw calls"

        let private convert typ (values : Trafo3d[]) : Array =
            if typ = typeof<Trafo3d> then values :> Array
            elif typ = typeof<M44d> then values |> Array.map _.Forward :> Array
            else values |> Array.map (fun t -> M44f t.Forward) :> Array

        let largeTranslations () =
            for magnitude in [100000000.0; 16777216.0; 1099511627776.0] do
                for sign in [-1.0; 1.0] do
                    for axis in [V3d.XAxis; V3d.YAxis; V3d.ZAxis] do
                        let inner = Trafo3d.Translation(-sign * magnitude * axis)
                        let residuals = [|1.0; -3.0; 0.25|]
                        let values = residuals |> Array.map (fun r -> Trafo3d.Translation(sign * (magnitude + r) * axis))
                        for route, typ in routes |> Array.filter (fun (_, t) -> t <> typeof<M44f>) do
                            let data = convert typ values
                            let ro = scene route typ (AVal.constant data) (AVal.constant inner) |> objects |> single
                            let context = $"{route}, magnitude={magnitude}, sign={sign}, axis={axis}"
                            let f, b = views ro
                            let forward = residuals |> Array.map (fun r -> M44f(Trafo3d.Translation(sign * r * axis + V3d.Zero).Forward))
                            let backward = residuals |> Array.map (fun r -> M44f(Trafo3d.Translation(-sign * r * axis + V3d.Zero).Forward))
                            checkMatrices (context + ": forward residual") forward (matrices f)
                            checkMatrices (context + ": inverse residual") backward (matrices b)
                            check context inner data ro
                            Expect.equal (convert typ values) data (context + ": source unchanged")

        let private quarterTurn =
            let m = M44d(0.0, -1.0, 0.0, 0.0,
                        1.0,  0.0, 0.0, 0.0,
                        0.0,  0.0, 1.0, 0.0,
                        0.0,  0.0, 0.0, 1.0)
            Trafo3d(m, m.Transposed)

        let private ordinary =
            [| Trafo3d.Identity
               Trafo3d.Translation(3.0, -5.0, 7.0)
               Trafo3d.Scale(-2.0, 0.5, 4.0) * quarterTurn * Trafo3d.Translation(-8.0, 16.0, -4.0) |]

        let compositionControls () =
            let children = [| Trafo3d.Identity; Trafo3d.Translation(-4.0, 2.0, 8.0) * quarterTurn * Trafo3d.Scale(0.5, -2.0, 4.0) |]
            Expect.notEqual (ordinary.[2].Forward * children.[1].Forward) (children.[1].Forward * ordinary.[2].Forward) "noncommuting fixture"
            for route, typ in routes do
                for inner in children do
                    for count in [0; 1; 3] do
                        let data = convert typ ordinary.[0 .. count - 1]
                        let snapshot = data.Clone() :?> Array
                        let ro = scene route typ (AVal.constant data) (AVal.constant inner) |> objects |> single
                        check $"{route}, count={count}, inner={inner}" inner data ro
                        Expect.equal data snapshot "input arrays unchanged"
            // Trafo3d's supplied inverse is authoritative, even if not recomputed from Forward.
            let stored = Trafo3d(Trafo3d.Translation(3.0, 2.0, 1.0).Forward, Trafo3d.Translation(-7.0, -8.0, -9.0).Forward)
            for route in ["typed"; "untyped trafo"] do
                let data = [|stored|] :> Array
                let ro = scene route typeof<Trafo3d> (AVal.constant data) (AVal.constant children.[1]) |> objects |> single
                check (route + ": supplied inverse") children.[1] data ro
            // Keep the float inverse result rounded before composing with the child.
            let floatValues =
                [| Trafo3d.Scale(1.1, 2.3, 3.7) * Trafo3d.RotationEuler(V3d(0.2, -0.4, 0.7)) * Trafo3d.Translation(3.125, -7.25, 9.5) |]
                |> Array.map (fun t -> M44f t.Forward)
            let rounded = M44f(children.[1].Backward * M44d floatValues.[0].Inverse)
            let unrounded = M44f(children.[1].Backward * (M44d floatValues.[0]).Inverse)
            Expect.notEqual rounded unrounded "float inverse fixture distinguishes rounding before composition"
            let data = floatValues :> Array
            let ro = scene "untyped float" typeof<M44f> (AVal.constant data) (AVal.constant children.[1]) |> objects |> single
            check "float inverse semantics" children.[1] data ro

        let adaptiveUpdates () =
            for route, typ in routes do
                let values = cval (convert typ ordinary)
                let inner = cval Trafo3d.Identity
                let sg = scene route typ values inner
                let set = objects sg
                let f, b = single set |> views
                // An outer transform must not be folded into object-local instance buffers.
                let outer = sg |> Sg.trafo (AVal.constant (Trafo3d.Translation(256.0, -128.0, 64.0))) |> objects
                for frame, count in List.indexed [3; 1; 1; 0; 4; 2; 0; 3] do
                    let child =
                        if frame % 2 = 0 then Trafo3d.Translation(-100000000.0, 0.0, 0.0)
                        else quarterTurn * Trafo3d.Scale(0.5, -2.0, 4.0)
                    let next = Array.init count (fun i -> Trafo3d.Translation(100000001.0 + float i, float i, -float i)) |> convert typ
                    // Alternate child-only, source-only and combined changes.
                    transact (fun () ->
                        if frame % 3 <> 1 then values.Value <- next
                        if frame % 3 <> 2 then inner.Value <- child)
                    let ro = single set
                    let context = $"{route}, frame={frame}"
                    check context inner.Value values.Value ro
                    check (context + ": outer scope") inner.Value values.Value (single outer)
                    let nf, nb = views ro
                    Expect.isTrue (obj.ReferenceEquals(f, nf) && obj.ReferenceEquals(b, nb)) (context + ": stable views")
                    for view in [f; b] do
                        let first = AVal.force view.Buffer
                        Expect.isTrue (obj.ReferenceEquals(first, AVal.force view.Buffer)) (context + ": cached buffer reuse")

        let unsupportedArrays () =
            for data in [[|1; 2|] :> Array; [|V3d.Zero|] :> Array; Array2D.zeroCreate<M44d> 1 2 :> Array] do
                let values = cval data
                let ro = scene "untyped double" typeof<M44d> values (AVal.constant Trafo3d.Identity) |> objects |> single
                let f, b = views ro
                for view in [f; b] do
                    let error = try AVal.force view.Buffer |> ignore; None with e -> Some e
                    match error with
                    | Some e ->
                        Expect.equal (e.GetType()) typeof<Exception> "unsupported array exception type"
                        Expect.equal e.Message (sprintf "unknown trafo type: %A" (data.GetType().GetElementType())) "unsupported array error"
                    | None -> failwithf "Accepted unsupported array %A" (data.GetType())
                transact (fun () -> values.Value <- [|M44d.Identity|] :> Array)
                check "recovery after unsupported array" Trafo3d.Identity values.Value ro

    let tests (target : TestTarget) =
        [
            "Large translations retain representable residuals",  Cases.largeTranslations
            "Composition order and float inputs stay compatible", Cases.compositionControls
            "Adaptive transforms retain cache views",             Cases.adaptiveUpdates
            "Unsupported arrays retain errors and recovery",      Cases.unsupportedArrays
        ]
        |> prepareCasesCpu "Instancing precision" target
