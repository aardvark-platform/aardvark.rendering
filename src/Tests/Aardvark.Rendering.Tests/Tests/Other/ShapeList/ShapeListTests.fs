namespace Aardvark.Rendering.Tests

open System
open System.Collections
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering.Text
open Expecto

module ``ShapeList Tests`` =

    // Keep the original implementation as an independent ordering/metadata oracle.
    let private appendFold (many : seq<ShapeList>) =
        use e = many.GetEnumerator()
        if e.MoveNext() then
            let mutable result = e.Current
            while e.MoveNext() do
                result <- ShapeList.append result e.Current
            result
        else
            ShapeList.empty

    let private shape i =
        let color = C4b(byte (i % 256), byte ((i * 7) % 256), byte ((i * 13) % 256), byte (128 + i % 128))
        let s = ConcreteShape.fillRectangle color (Box2d(-1.0, -0.5, 1.0, 0.5))
        { s with z = i - 37; trafo = M33d.Translation(float i * 1.25, float (i % 7)) }

    let private input i count =
        let value = [for j in 0 .. count - 1 -> shape (i * 5 + j)] |> ShapeList.ofList
        { value with
            textBounds = value.bounds.EnlargedBy(3.0)
            flipViewDependent = i % 2 = 0
            renderStyle = if i % 2 = 0 then RenderStyle.Billboard else RenderStyle.NoBoundary }

    let private close tolerance actual expected label =
        if actual <> expected then
            let error = abs (actual - expected)
            let limit = tolerance + 1e-11 * max (abs actual) (abs expected)
            Expect.isTrue (Double.IsFinite actual && Double.IsFinite expected && error <= limit)
                $"{label}: expected {expected}, got {actual} (error {error}, limit {limit})"

    let private equivalent tolerance (actual : ShapeList) (expected : ShapeList) =
        Expect.equal actual.bounds expected.bounds "Bounds union"
        Expect.equal actual.textBounds expected.textBounds "Text bounds"
        Expect.equal actual.zRange expected.zRange "Z range"
        Expect.equal actual.flipViewDependent expected.flipViewDependent "View-dependent flag"
        Expect.equal actual.renderStyle expected.renderStyle "Render style"
        Expect.equal actual.renderTrafo expected.renderTrafo "Render coordinate frame"
        Expect.equal actual.concreteShapes.Length expected.concreteShapes.Length "Shape count"
        List.iter2 (fun (a : ConcreteShape) (b : ConcreteShape) ->
            Expect.isTrue (obj.ReferenceEquals(a.shape, b.shape)) "Shape identity/order"
            Expect.equal a.color b.color "Color"
            Expect.equal a.z b.z "Z value"
            for row in 0 .. 2 do
                for col in 0 .. 2 do
                    close tolerance a.trafo.[row, col] b.trafo.[row, col] $"Transform [{row}, {col}]"
        ) actual.concreteShapes expected.concreteShapes

    let private check tolerance (values : ShapeList[]) =
        let snapshots =
            values |> Array.map (fun l ->
                { l with concreteShapes = l.concreteShapes |> List.map id })
        let expected = appendFold values
        let actual = ShapeList.concat values
        equivalent tolerance actual expected
        Array.iter2 (fun original snapshot ->
            equivalent 0.0 original snapshot
            List.iter2 (fun (a : ConcreteShape) (b : ConcreteShape) ->
                Expect.equal a.trafo b.trafo "Input transforms must remain exactly unchanged"
            ) original.concreteShapes snapshot.concreteShapes
        ) values snapshots

    type private TrackedSource(values : ShapeList[], failure : (string * int) option) =
        let error = InvalidOperationException("source failure")
        let mutable acquisitions = 0
        let mutable moves = 0
        let mutable reads = 0
        let mutable disposals = 0
        let fail stage count =
            if failure = Some (stage, count) then raise error
        member _.Error = error
        member _.Acquisitions = acquisitions
        member _.Moves = moves
        member _.Reads = reads
        member _.Disposals = disposals
        interface IEnumerable<ShapeList> with
            member _.GetEnumerator() =
                acquisitions <- acquisitions + 1
                if acquisitions <> 1 then failwith "Source enumerated more than once"
                fail "GetEnumerator" acquisitions
                let mutable index = -1
                let current () =
                    reads <- reads + 1
                    fail "Current" reads
                    values.[index]
                { new IEnumerator<ShapeList> with
                    member _.Current = current ()
                  interface IEnumerator with
                    member _.Current = box (current ())
                    member _.MoveNext() =
                        moves <- moves + 1
                        fail "MoveNext" moves
                        index <- index + 1
                        index < values.Length
                    member _.Reset() = failwith "Reset must not be called"
                  interface IDisposable with
                    member _.Dispose() =
                        disposals <- disposals + 1
                        fail "Dispose" disposals }
        interface IEnumerable with
            member this.GetEnumerator() = (this :> IEnumerable<ShapeList>).GetEnumerator() :> IEnumerator

    let private allocated (values : ShapeList[]) =
        for _ in 1 .. 8 do ShapeList.concat values |> ignore
        let start = GC.GetAllocatedBytesForCurrentThread()
        let mutable shapes = 0
        for _ in 1 .. 8 do
            shapes <- shapes + (ShapeList.concat values).concreteShapes.Length
        let bytes = (GC.GetAllocatedBytesForCurrentThread() - start) / 8L
        GC.KeepAlive shapes
        bytes

    [<Tests>]
    let tests =
        testList "ShapeList.concat" [
            testCase "Empty source returns the shared empty value" (fun () ->
                Expect.isTrue (obj.ReferenceEquals(ShapeList.concat Seq.empty, ShapeList.empty)) "Empty identity")

            for value, label in [ShapeList.empty, "empty"; input 1 3, "nonempty"; { ShapeList.empty with renderStyle = RenderStyle.Billboard; flipViewDependent = false }, "custom empty"] do
                testCase $"Singleton identity: {label}" (fun () ->
                    Expect.isTrue (obj.ReferenceEquals(ShapeList.concat [value], value)) "Singleton identity")

            for count in [2; 3; 4; 17; 256] do
                testCase $"Append-fold equivalence: {count} inputs" (fun () ->
                    Array.init count (fun i -> input i (i % 4 + 1)) |> check 1e-8)

            for count in [2; 3; 129] do
                testCase $"Only empty inputs: {count}" (fun () ->
                    Array.create count ShapeList.empty |> check 0.0)

            testCase "Interspersed empty inputs still contribute metadata" (fun () ->
                let empty =
                    { ShapeList.empty with bounds = Box2d(-500.0, -200.0, 900.0, 600.0)
                                           textBounds = Box2d.Unit
                                           zRange = Range1i(-123, 456)
                                           renderStyle = RenderStyle.NoBoundary
                                           flipViewDependent = false }
                [| ShapeList.empty; input 0 2; empty; input 1 3; ShapeList.empty; input 2 1; empty |] |> check 1e-8)

            testCase "Multiple empty inputs retain bounds/z/flag semantics, not singleton style" (fun () ->
                let a = { ShapeList.empty with bounds = Box2d(-3.0, -2.0, -1.0, 2.0); zRange = Range1i(-4, -1); renderStyle = RenderStyle.Billboard }
                let b = { ShapeList.empty with bounds = Box2d(3.0, -1.0, 5.0, 4.0); zRange = Range1i(8, 12); flipViewDependent = false }
                [|a; b; ShapeList.empty|] |> check 0.0)

            for left in [RenderStyle.Normal; RenderStyle.NoBoundary; RenderStyle.Billboard] do
                for right in [RenderStyle.Normal; RenderStyle.NoBoundary; RenderStyle.Billboard] do
                    for flags in [true, true; true, false; false, true; false, false] do
                        for count in [2; 3] do
                            testCase $"Styles/flags: {left}, {right}, {flags}, {count} inputs" (fun () ->
                                let a = { input 0 2 with renderStyle = left; flipViewDependent = fst flags }
                                let b = { input 1 2 with renderStyle = right; flipViewDependent = snd flags }
                                if count = 2 then [|a; b|] |> check 1e-8
                                else [|a; b; { ShapeList.empty with flipViewDependent = true }|] |> check 1e-8)

            for emptyFirst in [false; true] do
                testCase $"Two inputs with one metadata-only empty contribution, empty first = {emptyFirst}" (fun () ->
                    let empty = { ShapeList.empty with bounds = Box2d(-500.0, -2.0, 100.0, 3.0); zRange = Range1i(-10, 10); flipViewDependent = false }
                    let nonempty = input 1 4 |> ShapeList.scale (V2d(2.0, 0.5)) |> ShapeList.translate (V2d(17.0, -3.0))
                    (if emptyFirst then [|empty; nonempty|] else [|nonempty; empty|]) |> check 1e-8)

            testCase "Z values/ranges are not renumbered or recomputed" (fun () ->
                let a = { input 0 2 with zRange = Range1i(Int32.MinValue, -100) }
                let b = { input 1 2 with zRange = Range1i(100, Int32.MaxValue) }
                [|a; b; input 2 2|] |> check 1e-8)

            testCase "Translated/scaled lists and rotated/scaled shapes" (fun () ->
                [| for i in 0 .. 16 do
                    let s = shape i |> ConcreteShape.transform (M33d.Rotation(float i * 0.17) * M33d.Scale(0.5 + float i, 1.25))
                    yield ShapeList.ofList [s] |> ShapeList.scale (V2d(0.75, 1.5)) |> ShapeList.translate (V2d(float i * -12.5, 13.25)) |]
                |> check 1e-8)

            testCase "Nontranslation render transforms use the final frame in append order" (fun () ->
                [| for i in 0 .. 11 ->
                    { input i 3 with renderTrafo = Trafo3d.RotationZ(float i * 0.13) * Trafo3d.Scale(1.25, 0.75, 1.0) * Trafo3d.Translation(float i * 3.25, -7.5, 0.0) } |]
                |> check 1e-8)

            testCase "Large finite coordinates allow only rounding-scale differences" (fun () ->
                [| for i in 0 .. 63 -> input i 2 |> ShapeList.translate (V2d(1e12 + float i * 7.125, -1e11)) |]
                |> check 0.01)

            for seed in [17; 811; 90210] do
                testCase $"Finite randomized append-fold oracle, seed {seed}" (fun () ->
                    let random = Random seed
                    let signed () = random.NextDouble() * 2000.0 - 1000.0
                    for _ in 1 .. 300 do
                        let values =
                            Array.init (random.Next(0, 33)) (fun i ->
                                if random.Next(5) = 0 then ShapeList.empty
                                else
                                    let shapes =
                                        [for j in 0 .. random.Next(1, 5) - 1 ->
                                            shape (i * 5 + j)
                                            |> ConcreteShape.transform (M33d.Translation(signed (), signed ()) * M33d.Rotation(random.NextDouble() * 6.0) * M33d.Scale(0.1 + random.NextDouble() * 3.0, 0.1 + random.NextDouble() * 3.0))]
                                    ShapeList.ofList shapes |> ShapeList.translate (V2d(signed (), signed ())))
                        check 1e-8 values)

            for count in [0; 1; 2; 3; 41] do
                testCase $"Single-use source/disposal: {count} inputs" (fun () ->
                    let values = Array.init count (fun i -> input i 2)
                    let source = TrackedSource(values, None)
                    equivalent 1e-8 (ShapeList.concat source) (appendFold values)
                    Expect.equal source.Acquisitions 1 "Enumerator acquisitions"
                    Expect.equal source.Moves (count + 1) "MoveNext calls"
                    Expect.equal source.Reads count "Current reads"
                    Expect.equal source.Disposals 1 "Enumerator disposal")

            for stage, at in ["GetEnumerator", 1; "MoveNext", 1; "MoveNext", 2; "MoveNext", 3; "MoveNext", 7; "Current", 1; "Current", 2; "Current", 3; "Current", 6; "Dispose", 1] do
                testCase $"Source failure preserves exception/disposal: {stage} {at}" (fun () ->
                    let values = Array.init 6 (fun i -> input i 2)
                    let source = TrackedSource(values, Some (stage, at))
                    let mutable caught = None
                    try ShapeList.concat source |> ignore
                    with error -> caught <- Some error
                    Expect.isTrue (caught |> Option.exists (fun error -> obj.ReferenceEquals(error, source.Error))) "Original source exception"
                    Expect.equal source.Disposals (if stage = "GetEnumerator" then 0 else 1) "Disposal after failure")

            testCase "Output construction failure still disposes the source" (fun () ->
                let values = [|input 0 1; Unchecked.defaultof<ShapeList>; input 1 1|]
                let source = TrackedSource(values, None)
                Expect.throws (fun () -> ShapeList.concat source |> ignore) "Invalid input must still throw"
                Expect.equal source.Disposals 1 "Disposal after processing failure")

            testCase "Allocated output grows linearly, not with copied prefixes" (fun () ->
                let value = input 0 1
                let small = allocated (Array.create 256 value)
                let large = allocated (Array.create 1024 value)
                Expect.isTrue (large <= 6L * small + 16384L) $"Allocation scaling: {small} -> {large} B"
                Expect.isTrue (large < 512L * 1024L) $"Excess allocation: {large} B")

            testCase "Empty contributions do not accumulate retained storage" (fun () ->
                let small = allocated (Array.create 32 ShapeList.empty)
                let large = allocated (Array.create 8192 ShapeList.empty)
                Expect.isTrue (large <= small + 8192L) $"Empty-input storage scaling: {small} -> {large} B")
        ]
