namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open System
open System.Collections
open System.Collections.Generic
open System.Reflection
open Expecto

module ``IndexedGeometry Tests`` =

    module Clone =

        let clone (shallow: bool) =
            let name = if shallow then "shallow" else "deep"

            test $"Clone ({name})" {
                let g = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
                g.SingleAttributes <- SymDict.empty
                let g_pos = g.IndexedAttributes.[DefaultSemantic.Positions]
                let g_idx = g.IndexArray

                let gc = if shallow then g.Clone() else g.Clone(shallowCopy = false)
                let gc_pos = gc.IndexedAttributes.[DefaultSemantic.Positions]
                let gc_idx = gc.IndexArray

                Expect.isFalse (obj.ReferenceEquals(g.SingleAttributes, gc.SingleAttributes)) "Single attributes not copied"
                Expect.isFalse (obj.ReferenceEquals(g.IndexedAttributes, gc.IndexedAttributes)) "Indexed attributes not copied"

                let expect = if shallow then Expect.isTrue else Expect.isFalse
                expect (obj.ReferenceEquals(g_pos, gc_pos)) "Attribute array"
                expect (obj.ReferenceEquals(g_idx, gc_idx)) "Index array"

                Expect.isTrue gc.IsValid "Invalid"
            }

    module Union =

        let unionIndexed =
            test "Union (indexed)" {
                let a = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
                a.IndexArray <- a.IndexArray |> unbox<int32[]> |> Array.map int16
                let b = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
                b.IndexArray <- b.IndexArray |> unbox<int32[]> |> Array.map int16
                let c = IndexedGeometry.union a b

                let expected =
                    Array.concat [
                        a.IndexArray |> unbox<int16[]>
                        b.IndexArray |> unbox<int16[]> |> Array.map ((+) (int16 a.VertexCount))
                    ]

                Expect.equal (unbox<int16[]> c.IndexArray) expected "Unexpected indices"
                Expect.equal c.FaceVertexCount (a.FaceVertexCount + b.FaceVertexCount) "Unexpected face vertex count"
                Expect.isTrue c.IsValid "Invalid"
            }

        let unionNonIndexed =
            test "Union (non-indexed)" {
                let a = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toNonIndexed
                let b = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toNonIndexed
                let c = IndexedGeometry.union a b

                Expect.isNull c.IndexArray "Unexpected index array"
                Expect.equal c.FaceVertexCount (a.FaceVertexCount + b.FaceVertexCount) "Unexpected face vertex count"
                Expect.isTrue c.IsValid "Invalid"
            }

        let inline private unionNonIndexedAndIndexed (name: string) (mapIndex: int32 -> 'T) =
            test $"Union (non-indexed & {name}-indexed)" {
                let a = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toNonIndexed
                let b = IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black |> IndexedGeometry.toIndexed
                b.IndexArray <- b.IndexArray |> unbox<int32[]> |> Array.map mapIndex
                let c = IndexedGeometry.union a b

                let expected =
                    Array.concat [
                        Array.init a.FaceVertexCount (id >> mapIndex)
                        b.IndexArray |> unbox<'T[]> |> Array.map ((+) (mapIndex a.VertexCount))
                    ]

                Expect.equal (unbox<'T[]> c.IndexArray) expected "Unexpected indices"
                Expect.isTrue c.IsValid "Invalid"
            }

        let unionNonIndexedAndInt16 = unionNonIndexedAndIndexed "int16" int16
        let unionNonIndexedAndInt32 = unionNonIndexedAndIndexed "int32" int32

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

        let private variants name (fixture : int -> 'T[]) expected mode (build : seq<'T> -> IndexedGeometry) =
            testList name [
                for count in [0; 1; 3; 257] do
                    for kind in ["array"; "list"; "lazy"] do
                        testCase $"{kind}/{count}" <| fun _ ->
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
                for count in [0; 1; 5] do
                    testCase $"single use and exact counts/{count}" <| fun _ ->
                        let values = fixture count
                        let source = Source((fun _ -> values), true, "", -1)
                        geometry mode (expected values) (build source)
                        Expect.equal source.Counts (1, count + 1, count, 1) "one eager pass, one current read per item, one disposal"
                testCase "queue-draining source" <| fun _ ->
                    let values = fixture 5
                    let queue = Queue<_>(values)
                    let mutable disposals = 0
                    let input = seq { try while queue.Count > 0 do yield queue.Dequeue() finally disposals <- disposals + 1 }
                    geometry mode (expected values) (build input)
                    Expect.equal queue.Count 0 "eagerly consumed"
                    Expect.equal disposals 1 "disposed once"
                testCase "changing source cannot mix attribute generations" <| fun _ ->
                    let values = fixture 5
                    let other = Array.rev values
                    let source = Source((fun generation -> if generation = 1 then values else other), false, "", -1)
                    geometry mode (expected values) (build source)
                    Expect.equal source.Counts (1, 6, 5, 1) "only the first generation is read"
                for fault, at in ["start", 0; "move", 0; "move", 2; "move", 5; "current", 0; "current", 2; "dispose", 0] do
                    testCase $"{fault} failure/{at}" <| fun _ ->
                        let source = Source((fun _ -> fixture 5), false, fault, at)
                        let thrown = try build source |> ignore; None with e -> Some e
                        Expect.isTrue (thrown |> Option.exists (fun e -> obj.ReferenceEquals(e, source.Error))) "original source exception"
                        let starts, _, _, disposed = source.Counts
                        Expect.equal starts 1 "no retry"
                        Expect.equal disposed (if fault = "start" then 0 else 1) "dispose acquired enumerator on failure"
                testCase "deterministic randomized inputs" <| fun _ ->
                    let r = Random(1447)
                    for _ in 1 .. 100 do
                        let values = fixture (r.Next(0, 70)) |> Array.sortBy (fun _ -> r.Next())
                        geometry mode (expected values) (build (seq { yield! values }))
            ]

        let tests = [
            variants "lines" lines lineExpected IndexedGeometryMode.LineList P.Line.lines
            variants "lines alias" lines lineExpected IndexedGeometryMode.LineList P.lines
            let lineInput n = lines n |> Array.map fst
            let lineUniform xs = xs |> Array.map (fun l -> l, uniform) |> lineExpected
            variants "lines'" lineInput lineUniform IndexedGeometryMode.LineList (fun xs -> P.Line.lines' xs uniform)
            variants "lines' alias" lineInput lineUniform IndexedGeometryMode.LineList (fun xs -> P.lines' xs uniform)
            for name, wire, build in ["solid colors", false, P.Triangle.solidTrianglesWithColors; "wire colors", true, P.Triangle.wireframeTrianglesWithColors; "triangles alias", false, P.triangles] do
                variants name triangles (triangleExpected wire) (if wire then IndexedGeometryMode.LineList else IndexedGeometryMode.TriangleList) build
            for name, wire, build in ["solid uniform", false, P.Triangle.solidTrianglesWithColor; "wire uniform", true, P.Triangle.wireframeTrianglesWithColor; "triangles' alias", false, P.triangles'] do
                variants name (fun n -> triangles n |> Array.map fst) (fun ts -> ts |> Array.map (fun t -> t, uniform) |> triangleExpected wire)
                    (if wire then IndexedGeometryMode.LineList else IndexedGeometryMode.TriangleList) (fun ts -> build ts uniform)
            testCase "single line builders" <| fun _ ->
                for l, c in lines 5 do
                    geometry IndexedGeometryMode.LineList (lineExpected [|l, c|]) (P.Line.line l c)
                    geometry IndexedGeometryMode.LineList (lineExpected [|l, c|]) (P.line l c)
            testCase "line endpoint conversion and zero-length lines" <| fun _ ->
                let values =
                    [| Line3d(V3d.Zero, V3d.Zero), C4b.Black
                       Line3d(V3d(-0.0, 16777217.0, 1e150), V3d(0.0, -16777217.0, -1e150)), uniform |]
                geometry IndexedGeometryMode.LineList (lineExpected values) (P.Line.lines values)
            testCase "triangle normals precede float position conversion" <| fun _ ->
                let origin = V3d(1e10)
                let values = [|Triangle3d(origin, origin + V3d.XAxis, origin + V3d.YAxis), uniform|]
                let g = P.Triangle.solidTrianglesWithColors values
                geometry IndexedGeometryMode.TriangleList (triangleExpected false values) g
                for n in g.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[] do equalVector n V3f.OOI
            for name, vertexFactor, create in [
                "lines", 2, (fun n -> let data = lines n in fun () -> P.Line.lines data)
                "solid triangles", 3, (fun n -> let data = triangles n in fun () -> P.Triangle.solidTrianglesWithColors data)
                "wire triangles", 6, (fun n -> let data = triangles n in fun () -> P.Triangle.wireframeTrianglesWithColors data)
            ] do
                testCase $"array allocation bound/{name}" <| fun _ ->
                    let count = 2048
                    let build = create count
                    for _ in 1 .. 8 do GC.KeepAlive(build())
                    let before = GC.GetAllocatedBytesForCurrentThread()
                    for _ in 1 .. 4 do GC.KeepAlive(build())
                    let bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 4L
                    // 12 B positions + 4 B colors + 12 B normals, plus generous fixed geometry overhead.
                    Expect.isLessThan bytes (int64 (count * vertexFactor * 28 + 8192)) "no source copy or per-primitive temporary arrays"
            testCase "extreme and non-finite triangle conversion" <| fun _ ->
                for scale in [1e-150; 1e150; Double.PositiveInfinity; Double.NaN] do
                    let values = [|Triangle3d(V3d.Zero, V3d(scale, 0.0, 0.0), V3d(0.0, scale, 0.0)), uniform|]
                    geometry IndexedGeometryMode.TriangleList (triangleExpected false values) (P.Triangle.solidTrianglesWithColors values)
                    geometry IndexedGeometryMode.LineList (triangleExpected true values) (P.Triangle.wireframeTrianglesWithColors values)
            testCase "checked output count boundaries without huge allocations" <| fun _ ->
                let typ = typeof<ISg>.Assembly.GetType("Aardvark.SceneGraph.IndexedGeometryPrimitives", true)
                let method = typ.GetMethod("vertexCount", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
                Expect.isNotNull method "shared checked output count"
                for factor in [2; 3; 6] do
                    for count in [0; 1; Int32.MaxValue / factor] do
                        Expect.equal (method.Invoke(null, [|box count; box factor|]) :?> int) (count * factor) "representable count"
                    let thrown = try method.Invoke(null, [|box (Int32.MaxValue / factor + 1); box factor|]) |> ignore; None with e -> Some e
                    Expect.isTrue (thrown |> Option.exists (fun e -> match e with :? TargetInvocationException as t -> t.InnerException :? OverflowException | _ -> false)) "overflow is rejected"
        ]


    [<Tests>]
    let tests =
        testList "IndexedGeometry" [
            testList "Clone" [
                Clone.clone true
                Clone.clone false
            ]

            testList "Union" [
                Union.unionIndexed
                Union.unionNonIndexed
                Union.unionNonIndexedAndInt16
                Union.unionNonIndexedAndInt32
            ]

            testList "Primitive Sequences" PrimitiveSequences.tests
        ]