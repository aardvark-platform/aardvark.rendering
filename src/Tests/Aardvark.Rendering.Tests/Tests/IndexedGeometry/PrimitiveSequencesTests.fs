namespace Aardvark.Rendering.Tests.IndexedGeometry

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open System
open System.Collections
open System.Collections.Generic
open System.Reflection
open Expecto

module PrimitiveSequences =

    module P = IndexedGeometryPrimitives

    type private Source<'T>(values : int -> 'T[], singleUse : bool, fault : string, at : int) =
        let error = InvalidOperationException("source failure")
        let mutable starts, moves, reads, disposals = 0, 0, 0, 0
        member _.Counts = starts, moves, reads, disposals
        member _.Error = error
        interface IEnumerable<'T> with
            member _.GetEnumerator() =
                starts <- starts + 1
                if fault = "start" then raise error
                if singleUse && starts > 1 then failwith "source enumerated twice"
                let data = values starts
                let mutable index = -1
                let current() =
                    reads <- reads + 1
                    if fault = "current" && index = at then raise error
                    data.[index]
                { new IEnumerator<'T> with
                    member _.Current = current()
                  interface IEnumerator with
                    member _.Current = box (current())
                    member _.MoveNext() =
                        moves <- moves + 1
                        index <- index + 1
                        if fault = "move" && index = at then raise error
                        index < data.Length
                    member _.Reset() = raise (NotSupportedException())
                  interface IDisposable with
                    member _.Dispose() =
                        disposals <- disposals + 1
                        if fault = "dispose" then raise error }
        interface IEnumerable with
            member x.GetEnumerator() = (x :> IEnumerable<'T>).GetEnumerator() :> IEnumerator

    let private equalFloat (actual : float32) (expected : float32) =
        if Single.IsNaN expected then Expect.isTrue (Single.IsNaN actual) "NaN preserved"
        else Expect.equal (BitConverter.SingleToInt32Bits actual) (BitConverter.SingleToInt32Bits expected) "float bits"

    let private equalVector (actual : V3f) (expected : V3f) =
        equalFloat actual.X expected.X
        equalFloat actual.Y expected.Y
        equalFloat actual.Z expected.Z

    let private geometry mode (expected : (V3f * C4b * V3f)[]) (actual : IndexedGeometry) =
        Expect.equal actual.Mode mode "topology"
        Expect.isNull actual.IndexArray "non-indexed"
        Expect.isTrue actual.IsValid "valid attribute lengths"
        Expect.equal actual.IndexedAttributes.Count 3 "only positions, colors and normals"
        Expect.equal actual.VertexCount expected.Length "vertex count"
        Expect.equal actual.FaceVertexCount expected.Length "face vertex count"
        let positions = actual.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
        let colors = actual.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[]
        let normals = actual.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]
        Expect.equal positions.Length expected.Length "position length"
        Expect.equal colors.Length expected.Length "color length"
        Expect.equal normals.Length expected.Length "normal length"
        for i in 0 .. expected.Length - 1 do
            let p, c, n = expected.[i]
            equalVector positions.[i] p
            Expect.equal colors.[i] c "color including alpha"
            equalVector normals.[i] n

    let private lineExpected (input : (Line3d * C4b)[]) =
        [| for l, c in input do
                yield V3f l.P0, c, V3f.OOI
                yield V3f l.P1, c, V3f.OOI |]

    // Derive winding and normals from the corners, independently of the builders.
    let private triangleExpected wire (input : (Triangle3d * C4b)[]) =
        [| for t, c in input do
                let corners = [|t.P0; t.P1; t.P2|]
                let n = V3f ((t.P1 - t.P0).Cross(t.P2 - t.P0).Normalized)
                for i in 0 .. 2 do
                    yield V3f corners.[i], c, n
                    if wire then yield V3f corners.[(i + 1) % 3], c, n |]

    let private uniform = C4b(17, 69, 201, 123)
    let private point (r : Random) = V3d(r.NextDouble() * 2e5 - 1e5, r.NextDouble() * 4.0 - 2.0, r.NextDouble() * 1e-3)
    let private color (r : Random) = C4b(r.Next(256), r.Next(256), r.Next(256), r.Next(256))
    let private lines n =
        let r = Random(917)
        Array.init n (fun _ -> Line3d(point r, point r), color r)
    let private triangles n =
        let r = Random(151)
        Array.init n (fun i ->
            let a, b, c = point r, point r, point r
            // Repeated and collinear corners retain the existing NaN-normal behavior.
            let t = if i % 7 = 0 then Triangle3d(a, a, a) elif i % 7 = 1 then Triangle3d(a, b, a) else Triangle3d(a, b, c)
            t, color r)

    module private Cases =

        let private withContext context action =
            try action ()
            with error -> raise (Exception($"{context}: {error.Message}", error))

        let private inputRepresentations (fixture : int -> 'T[]) expected mode (build : seq<'T> -> IndexedGeometry) () =
            for kind in ["array"; "list"; "lazy"] do
                for count in [0; 1; 3; 257] do
                    withContext $"kind={kind}, count={count}" (fun () ->
                        let values = fixture count
                        let snapshot = Array.copy values
                        let input =
                            match kind with
                            | "array" -> values :> seq<_>
                            | "list" -> Array.toList values :> seq<_>
                            | _ -> seq { yield! values }
                        let result = build input
                        geometry mode (expected snapshot) result
                        Expect.equal values snapshot "source unmodified"
                        // Returned attributes do not alias the source array or each other across calls.
                        let again = build input
                        if count > 0 then
                            let positions = result.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
                            positions.[0] <- V3f(99.0f)
                            Expect.equal values snapshot "output mutation does not affect input"
                            geometry mode (expected snapshot) again
                    )

        let private singleUse (fixture : int -> 'T[]) expected mode (build : seq<'T> -> IndexedGeometry) () =
            for count in [0; 1; 5] do
                let values = fixture count
                let source = Source((fun _ -> values), true, "", -1)
                geometry mode (expected values) (build source)
                Expect.equal source.Counts (1, count + 1, count, 1) "one eager pass, one current read per item, one disposal"

        let private queueDraining (fixture : int -> 'T[]) expected mode (build : seq<'T> -> IndexedGeometry) () =
            let values = fixture 5
            let queue = Queue<_>(values)
            let mutable disposals = 0
            let input = seq { try while queue.Count > 0 do yield queue.Dequeue() finally disposals <- disposals + 1 }
            geometry mode (expected values) (build input)
            Expect.equal queue.Count 0 "eagerly consumed"
            Expect.equal disposals 1 "disposed once"

        let private changingSource (fixture : int -> 'T[]) expected mode (build : seq<'T> -> IndexedGeometry) () =
            let values = fixture 5
            let other = Array.rev values
            let source = Source((fun generation -> if generation = 1 then values else other), false, "", -1)
            geometry mode (expected values) (build source)
            Expect.equal source.Counts (1, 6, 5, 1) "only the first generation is read"

        let private sourceFailures (fixture : int -> 'T[]) (build : seq<'T> -> IndexedGeometry) () =
            let failures = ["start", [0]; "move", [0; 2; 5]; "current", [0; 2]; "dispose", [0]]
            for fault, indices in failures do
                for at in indices do
                    withContext $"fault={fault}, index={at}" (fun () ->
                        let source = Source((fun _ -> fixture 5), false, fault, at)
                        let thrown = try build source |> ignore; None with error -> Some error
                        Expect.isTrue (thrown |> Option.exists (fun error -> obj.ReferenceEquals(error, source.Error))) "original source exception"
                        let starts, _, _, disposed = source.Counts
                        Expect.equal starts 1 "no retry"
                        Expect.equal disposed (if fault = "start" then 0 else 1) "dispose acquired enumerator on failure"
                    )

        let private randomized (fixture : int -> 'T[]) expected mode (build : seq<'T> -> IndexedGeometry) () =
            let random = Random(1447)
            for _ in 1 .. 100 do
                let values = fixture (random.Next(0, 70)) |> Array.sortBy (fun _ -> random.Next())
                geometry mode (expected values) (build (seq { yield! values }))

        let variants name (fixture : int -> 'T[]) expected mode (build : seq<'T> -> IndexedGeometry) =
            [
                $"{name}.input representations", inputRepresentations fixture expected mode build
                $"{name}.single use and exact counts", singleUse fixture expected mode build
                $"{name}.queue-draining source", queueDraining fixture expected mode build
                $"{name}.changing source cannot mix attribute generations", changingSource fixture expected mode build
                $"{name}.source failures", sourceFailures fixture build
                $"{name}.deterministic randomized inputs", randomized fixture expected mode build
            ]

        let lineInput n = lines n |> Array.map fst
        let lineUniform xs = xs |> Array.map (fun line -> line, uniform) |> lineExpected
        let linePrime xs = P.Line.lines' xs uniform
        let linePrimeAlias xs = P.lines' xs uniform
        let triangleInput n = triangles n |> Array.map fst
        let triangleUniform wire xs = xs |> Array.map (fun triangle -> triangle, uniform) |> triangleExpected wire
        let solidUniform xs = P.Triangle.solidTrianglesWithColor xs uniform
        let wireUniform xs = P.Triangle.wireframeTrianglesWithColor xs uniform
        let aliasUniform xs = P.triangles' xs uniform

        let singleLineBuilders () =
            for line, color in lines 5 do
                geometry IndexedGeometryMode.LineList (lineExpected [|line, color|]) (P.Line.line line color)
                geometry IndexedGeometryMode.LineList (lineExpected [|line, color|]) (P.line line color)

        let lineEndpointConversion () =
            let values =
                [| Line3d(V3d.Zero, V3d.Zero), C4b.Black
                   Line3d(V3d(-0.0, 16777217.0, 1e150), V3d(0.0, -16777217.0, -1e150)), uniform |]
            geometry IndexedGeometryMode.LineList (lineExpected values) (P.Line.lines values)

        let triangleNormalConversion () =
            let origin = V3d(1e10)
            let values = [|Triangle3d(origin, origin + V3d.XAxis, origin + V3d.YAxis), uniform|]
            let result = P.Triangle.solidTrianglesWithColors values
            geometry IndexedGeometryMode.TriangleList (triangleExpected false values) result
            for normal in result.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[] do
                equalVector normal V3f.OOI

        let allocationBounds () =
            let builders =
                [
                    "lines", 2, (fun n -> let data = lines n in fun () -> P.Line.lines data)
                    "solid triangles", 3, (fun n -> let data = triangles n in fun () -> P.Triangle.solidTrianglesWithColors data)
                    "wire triangles", 6, (fun n -> let data = triangles n in fun () -> P.Triangle.wireframeTrianglesWithColors data)
                ]

            for name, vertexFactor, create in builders do
                withContext $"builder={name}" (fun () ->
                    let count = 2048
                    let build = create count
                    for _ in 1 .. 8 do GC.KeepAlive(build())
                    let before = GC.GetAllocatedBytesForCurrentThread()
                    for _ in 1 .. 4 do GC.KeepAlive(build())
                    let bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 4L
                    // 12 B positions + 4 B colors + 12 B normals, plus generous fixed geometry overhead.
                    Expect.isLessThan bytes (int64 (count * vertexFactor * 28 + 8192)) "no source copy or per-primitive temporary arrays"
                )

        let extremeTriangleConversion () =
            for scale in [1e-150; 1e150; Double.PositiveInfinity; Double.NaN] do
                let values = [|Triangle3d(V3d.Zero, V3d(scale, 0.0, 0.0), V3d(0.0, scale, 0.0)), uniform|]
                geometry IndexedGeometryMode.TriangleList (triangleExpected false values) (P.Triangle.solidTrianglesWithColors values)
                geometry IndexedGeometryMode.LineList (triangleExpected true values) (P.Triangle.wireframeTrianglesWithColors values)

        let checkedOutputCounts () =
            let typ = typeof<ISg>.Assembly.GetType("Aardvark.SceneGraph.IndexedGeometryPrimitives", true)
            let method = typ.GetMethod("vertexCount", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            Expect.isNotNull method "shared checked output count"
            for factor in [2; 3; 6] do
                for count in [0; 1; Int32.MaxValue / factor] do
                    Expect.equal (method.Invoke(null, [|box count; box factor|]) :?> int) (count * factor) "representable count"
                let thrown = try method.Invoke(null, [|box (Int32.MaxValue / factor + 1); box factor|]) |> ignore; None with error -> Some error
                Expect.isTrue (thrown |> Option.exists (fun error -> match error with :? TargetInvocationException as target -> target.InnerException :? OverflowException | _ -> false)) "overflow is rejected"

    let tests (target: TestTarget) =
        [
            yield! Cases.variants "lines" lines lineExpected IndexedGeometryMode.LineList P.Line.lines
            yield! Cases.variants "lines alias" lines lineExpected IndexedGeometryMode.LineList P.lines
            yield! Cases.variants "lines'" Cases.lineInput Cases.lineUniform IndexedGeometryMode.LineList Cases.linePrime
            yield! Cases.variants "lines' alias" Cases.lineInput Cases.lineUniform IndexedGeometryMode.LineList Cases.linePrimeAlias

            yield! Cases.variants "solid colors" triangles (triangleExpected false) IndexedGeometryMode.TriangleList P.Triangle.solidTrianglesWithColors
            yield! Cases.variants "wire colors" triangles (triangleExpected true) IndexedGeometryMode.LineList P.Triangle.wireframeTrianglesWithColors
            yield! Cases.variants "triangles alias" triangles (triangleExpected false) IndexedGeometryMode.TriangleList P.triangles
            yield! Cases.variants "solid uniform" Cases.triangleInput (Cases.triangleUniform false) IndexedGeometryMode.TriangleList Cases.solidUniform
            yield! Cases.variants "wire uniform" Cases.triangleInput (Cases.triangleUniform true) IndexedGeometryMode.LineList Cases.wireUniform
            yield! Cases.variants "triangles' alias" Cases.triangleInput (Cases.triangleUniform false) IndexedGeometryMode.TriangleList Cases.aliasUniform

            "single line builders", Cases.singleLineBuilders
            "line endpoint conversion and zero-length lines", Cases.lineEndpointConversion
            "triangle normals precede float position conversion", Cases.triangleNormalConversion
            "array allocation bounds", Cases.allocationBounds
            "extreme and non-finite triangle conversion", Cases.extremeTriangleConversion
            "checked output count boundaries without huge allocations", Cases.checkedOutputCounts
        ]
        |> prepareCasesCpu "Primitives sequences" target
