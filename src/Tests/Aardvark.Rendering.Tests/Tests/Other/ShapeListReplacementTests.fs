namespace Aardvark.Rendering.Tests

open System
open Expecto
open Aardvark.Base
open Aardvark.Rendering.Text

module ``ShapeList Replacement Tests`` =

    let private font = lazy DefaultFonts.Hack.Regular
    let private rectangle = lazy (ConcreteShape.fillRectangle C4b.Cyan (Box2d(V2d(-2.0, -1.0), V2d(3.0, 4.0))))

    // Construct code points directly: matching must not depend on the font's coverage or the
    // text-layout decoder. The bundled font's missing-glyph outline is sufficient for these tests.
    let private shape point index =
        let geometry : Shape =
            if point = -2 then rectangle.Value.shape
            else
                let glyph = font.Value.GetGlyph(CodePoint point)
                Expect.equal glyph.CodePoint.Value point "fixture preserves the full code point"
                glyph :> Shape
        { shape = geometry
          trafo = M33d.Translation(float (index * 3 - 7), float (index % 5 - 2)) *
                  M33d.Rotation(0.13 * float (index % 4)) * M33d.Scale(1.0 + float (index % 3), -0.5)
          color = C4b(byte (index % 255), 31uy, 193uy, 117uy)
          z = index - 9 }

    let private input shapes =
        { bounds = Box2d(V2d(-99.0, -77.0), V2d(123.0, 456.0))
          textBounds = Box2d(V2d(-3.0, 17.0), V2d(5.0, 31.0))
          concreteShapes = shapes
          zRange = Range1i(-53, 199)
          renderTrafo = Trafo3d.Scale(2.0, 3.0, 4.0) * Trafo3d.Translation(999.0, -321.0, 71.0)
          flipViewDependent = true
          renderStyle = RenderStyle.Billboard }

    let private same expected actual message =
        Expect.isTrue (Object.ReferenceEquals(expected, actual)) message

    let private equalShapes expected actual =
        Expect.equal (List.length actual) (List.length expected) "output length"
        (expected, actual) ||> List.iter2 (fun expected actual ->
            same expected.shape actual.shape "shape identity"
            Expect.equal actual.trafo expected.trafo "shape transform"
            Expect.equal actual.color expected.color "shape color"
            Expect.equal actual.z expected.z "shape z")

    let private equalMetadata (expected : ShapeList) (actual : ShapeList) =
        Expect.equal actual.bounds expected.bounds "bounds are not recomputed"
        Expect.equal actual.textBounds expected.textBounds "text bounds"
        Expect.equal actual.zRange expected.zRange "z range"
        Expect.equal actual.renderTrafo expected.renderTrafo "render transform"
        Expect.equal actual.flipViewDependent expected.flipViewDependent "view dependence"
        Expect.equal actual.renderStyle expected.renderStyle "render style"

    let private markers = lazy [shape -2 201; shape -2 202]
    let private bmp (text : string) = text |> Seq.map int |> Seq.toArray
    // Build malformed UTF-16 at runtime; string-literal emission may replace lone surrogates.
    let private utf16 values = String(values |> List.map char |> List.toArray)

    // Explicit match ranges are independent of the matching implementation.
    let private check name points pattern ranges replacement =
        testCase name <| fun _ ->
            let shapes = points |> Array.mapi (fun i p -> shape p i)
            let original = shapes |> Array.toList
            let source = input original
            let calls = ResizeArray<Box2d>()
            let expectedBounds =
                ranges |> List.map (fun (start, count) ->
                    shapes.[start .. start + count - 1]
                    |> Array.fold (fun box s -> Box.Union(box, s.bounds)) Box2d.Invalid)
            let expected =
                let mutable i = 0
                [ for start, count in ranges do
                    while i < start do
                        yield shapes.[i]
                        i <- i + 1
                    yield! replacement
                    i <- start + count
                  while i < shapes.Length do
                    yield shapes.[i]
                    i <- i + 1 ]
            let actual = ShapeList.replaceString pattern (fun box -> calls.Add box; replacement) source
            equalShapes expected actual.concreteShapes
            Expect.sequenceEqual calls expectedBounds "callback bounds and left-to-right order"
            equalMetadata source actual
            same original source.concreteShapes "input list identity"
            equalShapes (Array.toList shapes) source.concreteShapes

    // Original implementation, deliberately restricted to nonempty patterns and safe-sized BMP
    // inputs. This checks compatibility, not the Unicode or stack-safety fixes.
    let private legacy pattern rep (source : ShapeList) =
        let rec front (str : string) i current shapes =
            if i >= str.Length then Some (current, shapes)
            else
                match shapes with
                | h :: rest ->
                    match h.shape with
                    | :? Glyph as g when g.CodePoint.String.[0] = str.[i] ->
                        front str (i + 1) (Box.Union(current, h.bounds)) rest
                    | _ -> None
                | [] -> None
        let rec replace shapes =
            match front pattern 0 Box2d.Invalid shapes with
            | Some (box, rest) -> rep box @ replace rest
            | None ->
                match shapes with
                | [] -> []
                | h :: rest -> h :: replace rest
        { source with concreteShapes = replace source.concreteShapes }

    let tests =
        testSequenced <| testList "ShapeList replacement" [
            testList "BMP and boundaries" [
                let cases = [
                    "empty", "", "A", []
                    "absent", "BCDE", "A", []
                    "exact", "AB", "AB", [0, 2]
                    "single", "A", "A", [0, 1]
                    "adjacent", "ABABAB", "AB", [0, 2; 2, 2; 4, 2]
                    "separated", "CABDABEF", "AB", [1, 2; 4, 2]
                    "overlapping", "ABABA", "ABA", [0, 3]
                    "repeat overlap", "AAAAA", "AA", [0, 2; 2, 2]
                    "failed prefix", "AAB", "AB", [1, 2]
                    "shared prefix", "AAAAB", "AAB", [2, 3]
                    "short suffix", "ABCA", "AB", [0, 2]
                    "too short", "AB", "ABC", []
                    "BMP Unicode", "éΩ中éΩ中", "éΩ中", [0, 3; 3, 3]
                    "no normalization", "e\u0301é", "é", [2, 1]
                    "combining sequence", "e\u0301é", "e\u0301", [0, 2]
                    "ordinal case", "aAa", "A", [1, 1]
                    "ordinal Turkish", "Iıİi", "i", [3, 1]
                    "zero code point", "\u0000A\u0000", "\u0000", [0, 1; 2, 1]
                    "noncharacter", "\uFFFFA", "\uFFFF", [0, 1]
                    "empty glyph bounds", " \n ", " \n", [0, 2]
                ]
                for name, text, pattern, ranges in cases do
                    for remove in [false; true] do
                        check $"{name}/remove={remove}" (bmp text) pattern ranges (if remove then [] else markers.Value)
                check "non-glyph interrupts" [|65; -2; 66; 65; 66|] "AB" [3, 2] markers.Value
                check "non-glyph at ends" [|-2; 65; 66; -2|] "AB" [1, 2] markers.Value
                check "non-glyph is not a space" [|65; -2; 66|] "A B" [] markers.Value
                check "only non-glyphs" [|-2; -2|] "A" [] markers.Value
            ]
            testList "Unicode code points" [
                for point in [0x10000; 0x103FF; 0x10400; 0x1F600; 0x1F601; 0x1FFFF; 0x20000; 0x20041; 0x2FFFF; 0x30000; 0x10FFFF] do
                    check $"supplementary U+{point:X}" [|65; point; 66; point|]
                        (Char.ConvertFromUtf32 point) [1, 1; 3, 1] markers.Value
                check "mixed pattern" [|65; 0x1F600; 0x20000; 0x03A9; 65; 0x1F600; 0x20000; 0x03A9|]
                    ("A" + Char.ConvertFromUtf32 0x1F600 + Char.ConvertFromUtf32 0x20000 + "Ω") [0, 4; 4, 4] markers.Value
                check "supplementary failed prefix" [|0x1F600; 0x1F600; 66|]
                    (Char.ConvertFromUtf32 0x1F600 + "B") [1, 2] []
                check "supplementary low-part differs" [|0x1F601|] (Char.ConvertFromUtf32 0x1F600) [] markers.Value
                check "supplementary high-part differs" [|0x1F600|] (Char.ConvertFromUtf32 0x2F600) [] markers.Value
                check "no truncated BMP alias" [|0x20041|] "A" [] markers.Value
                check "non-glyph interrupts Unicode" [|0x1F600; -2; 65|] (Char.ConvertFromUtf32 0x1F600 + "A") [] markers.Value
                check "high surrogate cannot match half a glyph" [|0x1F600|] (utf16 [0xD83D]) [] markers.Value
                check "low surrogate cannot match half a glyph" [|0x1F600|] (utf16 [0xDE00]) [] markers.Value
                check "two surrogate glyphs are not one code point" [|0xD83D; 0xDE00|] (Char.ConvertFromUtf32 0x1F600) [] markers.Value
                check "literal high surrogate" [|0xD83D; 65|] (utf16 [0xD83D]) [0, 1] markers.Value
                check "literal low surrogate" [|0xDE00; 65|] (utf16 [0xDE00]) [0, 1] markers.Value
                check "unpaired high before BMP" [|0xD83D; 65|] (utf16 [0xD83D; 65]) [0, 2] markers.Value
                check "unpaired high at end" [|65; 0xD83D|] (utf16 [65; 0xD83D]) [0, 2] markers.Value
                check "reversed surrogates" [|0xDE00; 0xD83D|] (utf16 [0xDE00; 0xD83D]) [0, 2] markers.Value
                check "two highs then a valid pair" [|0xD83D; 0x1F600|] (utf16 [0xD83D; 0xD83D; 0xDE00]) [0, 2] markers.Value
            ]
            for empty in [false; true] do
                testCase $"empty pattern rejected before callback/input empty={empty}" <| fun _ ->
                    let source = input (if empty then [] else [shape 65 0])
                    let mutable calls = 0
                    let rep _ =
                        calls <- calls + 1
                        // Also bounds failure of the original implementation when used as a control.
                        failwith "an empty pattern must not invoke the callback"
                    let mutable caught = false
                    try ShapeList.replaceString "" rep source |> ignore
                    with :? ArgumentException as e ->
                        caught <- true
                        Expect.equal e.ParamName "str" "empty-pattern parameter"
                    Expect.isTrue caught "empty pattern rejected"
                    Expect.equal calls 0 "no callback for an empty pattern"

            testCase "replacement glyphs are not searched again" <| fun _ ->
                let replacement = [shape 65 101; shape 66 102]
                let mutable calls = 0
                let source = input [shape 65 0; shape 66 1; shape 65 2; shape 66 3]
                let result = ShapeList.replaceString "AB" (fun _ ->
                    calls <- calls + 1
                    if calls > 2 then failwith "replacement was searched again"
                    replacement) source
                Expect.equal calls 2 "one callback per original match"
                equalShapes (replacement @ replacement) result.concreteShapes

            for prefixCount in [0; 1; 2] do
                testCase $"final replacement tail reuse/prefix={prefixCount}" <| fun _ ->
                    let prefix = List.init prefixCount (shape 67)
                    let replacement = markers.Value
                    let snapshot = List.toArray replacement
                    let source = input (prefix @ [shape 65 0])
                    let result = ShapeList.replaceString "A" (fun _ -> replacement) source
                    equalShapes (prefix @ replacement) result.concreteShapes
                    same replacement (List.skip prefixCount result.concreteShapes) "final replacement is shared, not copied"
                    equalShapes (List.ofArray snapshot) replacement

            for failAt in [1; 2; 3] do
                testCase $"callback exception at match {failAt}" <| fun _ ->
                    let error = InvalidOperationException "callback sentinel"
                    let source = input [for i in 0 .. 7 -> shape 65 i]
                    let snapshot = source.concreteShapes |> List.toArray
                    let mutable calls = 0
                    let mutable observed : exn = null
                    try
                        ShapeList.replaceString "AA" (fun _ ->
                            calls <- calls + 1
                            if calls = failAt then raise error
                            markers.Value) source |> ignore
                    with e -> observed <- e
                    same error observed "original callback exception propagates"
                    Expect.equal calls failAt "callbacks stop at the failure"
                    equalShapes (Array.toList snapshot) source.concreteShapes

            for style in [RenderStyle.Normal; RenderStyle.NoBoundary; RenderStyle.Billboard] do
                for flip in [false; true] do
                    for pattern in ["A"; "Z"] do
                        testCase $"metadata/{style}/{flip}/{pattern}" <| fun _ ->
                            let source = { input [shape 65 0] with renderStyle = style; flipViewDependent = flip }
                            let result = ShapeList.replaceString pattern (fun _ -> []) source
                            equalMetadata source result

            for seed in 0 .. 9 do
                testCase $"original BMP equivalence/seed={seed}" <| fun _ ->
                    let random = Random(941 + seed)
                    let alphabet = [|65; 66; 67; 0xE9; 0x3A9; 0x4E2D; 0; 0xFFFF|]
                    for _ in 1 .. 100 do
                        let source =
                            input [
                                for i in 0 .. random.Next(51) - 1 ->
                                    shape (if random.Next(8) = 0 then -2 else alphabet.[random.Next alphabet.Length]) i
                            ]
                        let pattern = String(Array.init (1 + random.Next(7)) (fun _ -> char alphabet.[random.Next alphabet.Length]))
                        let run replace =
                            let calls = ResizeArray<Box2d>()
                            let result = replace pattern (fun box ->
                                calls.Add box
                                match calls.Count % 3 with
                                | 0 -> []
                                | 1 -> [shape -2 (300 + calls.Count)]
                                | _ -> [shape 65 (400 + calls.Count); shape -2 (500 + calls.Count)]) source
                            result, List.ofSeq calls
                        let expected, before = run legacy
                        let actual, after = run ShapeList.replaceString
                        equalShapes expected.concreteShapes actual.concreteShapes
                        Expect.equal after before "original callback bounds/order"
                        equalMetadata expected actual

            for pattern in ["Z"; "AAA"] do
                testCase $"allocation bounded by the output list/{pattern}" <| fun _ ->
                    let source = input (List.replicate 4098 (shape 65 0))
                    let replacement = [markers.Value.Head]
                    let expected = if pattern = "Z" then source.concreteShapes else List.replicate 1366 replacement.Head
                    let bytes action =
                        let mutable result = ShapeList.empty
                        for _ in 1 .. 10 do result <- action()
                        let start = GC.GetAllocatedBytesForCurrentThread()
                        for _ in 1 .. 10 do result <- action()
                        let allocated = (GC.GetAllocatedBytesForCurrentThread() - start) / 10L
                        GC.KeepAlive result
                        allocated
                    let outputCost = bytes (fun () -> { source with concreteShapes = List.map id expected })
                    let actual = bytes (fun () -> ShapeList.replaceString pattern (fun _ -> replacement) source)
                    Expect.isLessThanOrEqual actual (outputCost + 256L) "one output list, no per-shape strings or second output copy"

            testList "stack safety" [
                for count in [100000; 250000] do
                    testCase $"no matches/{count}" <| fun _ ->
                        let primitive = shape 65 0
                        let source = input (List.replicate count primitive)
                        let result = ShapeList.replaceString "Z" (fun _ -> failwith "unexpected match") source
                        Expect.equal result.concreteShapes.Length count "all unmatched shapes retained"
                        for s in result.concreteShapes do same primitive.shape s.shape "unmatched geometry"
                        equalMetadata source result
                for remove in [false; true] do
                    testCase $"repeated matches/remove={remove}" <| fun _ ->
                        let count = 100000
                        let source = input (List.replicate count (shape 65 0))
                        let mutable calls = 0
                        let result = ShapeList.replaceString "AA" (fun _ ->
                            calls <- calls + 1
                            if remove then [] else markers.Value) source
                        Expect.equal calls (count / 2) "all matches visited"
                        Expect.equal result.concreteShapes.Length (if remove then 0 else count) "replacement count"
                        if not remove then
                            result.concreteShapes |> List.iteri (fun i s -> Expect.equal s.z markers.Value.[i % 2].z "replacement order")
                testCase "long supplementary pattern" <| fun _ ->
                    let count = 100000
                    let text = String.Concat(Array.create count (Char.ConvertFromUtf32 0x1F600))
                    let source = input (List.replicate count (shape 0x1F600 0))
                    let mutable calls = 0
                    let result = ShapeList.replaceString text (fun _ -> calls <- calls + 1; []) source
                    Expect.equal calls 1 "one full long-pattern match"
                    Expect.isEmpty result.concreteShapes "matched the complete pattern"
                testCase "long failed prefix" <| fun _ ->
                    let count = 100000
                    let text = "A" + String('B', count) + "C"
                    let source = input (shape 65 0 :: List.replicate count (shape 66 1))
                    let result = ShapeList.replaceString text (fun _ -> failwith "incomplete prefix") source
                    Expect.equal result.concreteShapes.Length (count + 1) "failed prefix retains every shape"
                testCase "long replacement list" <| fun _ ->
                    let count = 200000
                    let primitive = rectangle.Value
                    let replacement = List.init count (fun i -> { primitive with z = i })
                    let result = ShapeList.replaceString "A" (fun _ -> replacement) (input [shape 65 0])
                    Expect.equal result.concreteShapes.Length count "replacement size"
                    result.concreteShapes |> List.iteri (fun i s -> Expect.equal s.z i "replacement order")
            ]
        ]
