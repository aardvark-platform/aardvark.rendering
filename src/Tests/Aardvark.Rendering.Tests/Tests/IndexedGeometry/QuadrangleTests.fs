namespace Aardvark.Rendering.Tests.IndexedGeometry

open System
open System.Collections
open System.Collections.Generic
open System.Reflection
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Expecto

module Quadrangles =

    module private Cases =

        type private Corner = V3d * C4b * V3d
        type private Quad = Corner * Corner * Corner * Corner

        let private corners (a, b, c, d) = [|a; b; c; d|]
        let private positions ((p0, _, _), (p1, _, _), (p2, _, _), (p3, _, _)) = p0, p1, p2, p3
        let private uniform (q : Quad) = let (_, color, normal), _, _, _ = q in positions q, normal, color
        let private plain (q : Quad) = let (_, color, _), _, _, _ = q in positions q, color
        let private builders : (string * bool * int * (seq<Quad> -> IndexedGeometry)) list =
            [ "solid corner attributes", false, 0, IndexedGeometryPrimitives.Quad.solidQuadranglesWithColorsAndNormals
              "wire corner attributes",  true,  0, IndexedGeometryPrimitives.Quad.wireframeQuadranglesWithColorsAndNormals
              "solid uniform normal",    false, 1, (Seq.map uniform >> IndexedGeometryPrimitives.Quad.solidQuadrangles)
              "wire uniform normal",     true,  1, (Seq.map uniform >> IndexedGeometryPrimitives.Quad.wireframeQuadrangles)
              "solid default normal",    false, 2, (Seq.map plain >> IndexedGeometryPrimitives.Quad.solidQuadrangles')
              "wire default normal",     true,  2, (Seq.map plain >> IndexedGeometryPrimitives.Quad.wireframeQuadrangles') ]

        let private fixture count : Quad[] =
            let corner i k =
                let x = float (i * 4 + k)
                let p = V3d(x + 0.125, -2.0 * x - 0.25, x / 7.0)
                let n = V3d(-x - 0.5, x / 3.0, 2.0 + x)
                p, C4b((17 + i + k) % 256, (53 + 7 * k) % 256, (201 + i) % 256, (11 + 31 * k) % 256), n
            Array.init count (fun i -> corner i 0, corner i 1, corner i 2, corner i 3)

        let private expected style (values : Quad[]) =
            [| for q in values do
                   let (_, color, normal), _, _, _ = q
                   for p, c, n in corners q do
                       let convert (v : V3d) = V3f(float32 v.X, float32 v.Y, float32 v.Z)
                       yield convert p, (if style = 0 then c else color),
                             (if style = 2 then V3f.OOI else convert (if style = 0 then n else normal)) |]

        let private vector context (actual : V3f) (expected : V3f) =
            for axis, a, e in ["X", actual.X, expected.X; "Y", actual.Y, expected.Y; "Z", actual.Z, expected.Z] do
                if Single.IsNaN e then Expect.isTrue (Single.IsNaN a) (context + ": " + axis + " NaN")
                else Expect.equal (BitConverter.SingleToInt32Bits a) (BitConverter.SingleToInt32Bits e) (context + ": " + axis + " bits")

        let private geometry context wire attributes (g : IndexedGeometry) =
            let count = Array.length attributes
            let pattern = if wire then [|0; 1; 1; 2; 2; 3; 3; 0|] else [|0; 1; 2; 0; 2; 3|]
            let indices = [|for i = 0 to count / 4 - 1 do for index in pattern do yield 4 * i + index|]
            Expect.equal g.Mode (if wire then IndexedGeometryMode.LineList else IndexedGeometryMode.TriangleList) (context + ": topology")
            Expect.isTrue g.IsIndexed (context + ": indexed even when empty")
            Expect.isTrue g.IsValid (context + ": valid attributes")
            Expect.isNull g.SingleAttributes (context + ": no single attributes")
            Expect.equal g.VertexCount count (context + ": vertex count")
            Expect.equal g.FaceVertexCount indices.Length (context + ": face vertex count")
            Expect.equal (g.IndexArray :?> int[]) indices (context + ": exact winding and edge order")
            Expect.equal g.IndexedAttributes.Count 3 (context + ": only positions, colors and normals")
            let p = g.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
            let c = g.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[]
            let n = g.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]
            Expect.equal (p.Length, c.Length, n.Length) (count, count, count) (context + ": exact attribute lengths")
            for i = 0 to count - 1 do
                let ep, ec, en = attributes.[i]
                let context = sprintf "%s, vertex=%d" context i
                vector (context + ": position") p.[i] ep
                Expect.equal c.[i] ec (context + ": color including alpha")
                vector (context + ": unnormalized normal") n.[i] en

        type private Source<'T>(values : int -> 'T[], fault : string, at : int, onDispose : unit -> unit) =
            let error = InvalidOperationException("quadrangle source")
            let mutable starts, moves, reads, disposals = 0, 0, 0, 0
            member _.Counts = starts, moves, reads, disposals
            member _.Error = error
            interface IEnumerable<'T> with
                member _.GetEnumerator() =
                    starts <- starts + 1
                    if fault = "start" then raise error
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
                            onDispose()
                            if fault = "dispose" then raise error }
            interface IEnumerable with
                member x.GetEnumerator() = (x :> IEnumerable<'T>).GetEnumerator() :> IEnumerator

        let batchLayouts() =
            for name, wire, style, build in builders do
                for count in [0; 1; 2; 7; 257] do
                    for kind in ["array"; "list"; "lazy"] do
                        let values = fixture count
                        let source = match kind with "array" -> values :> seq<_> | "list" -> List.ofArray values :> seq<_> | _ -> seq { yield! values }
                        geometry (sprintf "%s, count=%d, kind=%s" name count kind) wire (expected style values) (build source)

        let singleAliases() =
            let values = Array.append (fixture 5)
                            [| (V3d(-0.0, 16777217.0, 1e150), C4b.Red, V3d(1e-200, -0.0, Double.NaN)),
                               (V3d(Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity), C4b.Green, V3d(3.0, 4.0, 7.0)),
                               (V3d.Zero, C4b.Blue, V3d.Zero), (V3d.One, C4b.White, V3d(1e150)) |]
            for q in values do
                let a, b, c, d = q
                let p0, p1, p2, p3 = positions q
                let (_, color, normal), _, _, _ = q
                let pc (p, color, _) = p, color
                let results =
                    [ "solid corner attributes", false, 0, IndexedGeometryPrimitives.Quad.solidQuadrangleWithColorsAndNormals a b c d
                      "wire corner attributes", true, 0, IndexedGeometryPrimitives.Quad.wireframeQuadrangleWithColorsAndNormals a b c d
                      "solid uniform normal", false, 1, IndexedGeometryPrimitives.Quad.solidQuadrangle p0 p1 p2 p3 color normal
                      "wire uniform normal", true, 1, IndexedGeometryPrimitives.Quad.wireframeQuadrangle p0 p1 p2 p3 color normal
                      "solid default normal", false, 2, IndexedGeometryPrimitives.Quad.solidQuadrangle' p0 p1 p2 p3 color
                      "wire default normal", true, 2, IndexedGeometryPrimitives.Quad.wireframeQuadrangle' p0 p1 p2 p3 color
                      "quad alias", false, 0, IndexedGeometryPrimitives.quad (pc a) (pc b) (pc c) (pc d)
                      "quad prime wire alias", true, 2, IndexedGeometryPrimitives.quad' p0 p1 p2 p3 color ]
                for name, wire, style, result in results do
                    let attributes = expected style [|q|]
                    let attributes = if name = "quad alias" then attributes |> Array.map (fun (p, c, _) -> p, c, V3f.OOI) else attributes
                    geometry (sprintf "%s, input=%A" name q) wire attributes result

        let snapshotsAndIsolation() =
            for name, wire, style, build in builders do
                let values = fixture 7
                let snapshot = Array.copy values
                let first, second = build values, build values
                for semantic in [DefaultSemantic.Positions; DefaultSemantic.Colors; DefaultSemantic.Normals] do
                    Expect.isFalse (obj.ReferenceEquals(first.IndexedAttributes.[semantic], second.IndexedAttributes.[semantic])) (name + ": independent outputs")
                Expect.isFalse (obj.ReferenceEquals(first.IndexArray, second.IndexArray)) (name + ": independent index storage")
                (first.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]).[0] <- V3f(99.0f)
                (first.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[]).[0] <- C4b.Black
                (first.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]).[0] <- V3f(77.0f)
                (first.IndexArray :?> int[]).[0] <- -1
                Expect.equal values snapshot (name + ": output writes leave input unchanged")
                values.[0] <- values.[6]
                geometry (name + ": source writes leave output unchanged") wire (expected style snapshot) second
                let changing = Source((fun generation -> if generation = 1 then snapshot else Array.rev snapshot), "", -1, ignore)
                geometry (name + ": single snapshot generation") wire (expected style snapshot) (build changing)
                Expect.equal changing.Counts (1, 8, 7, 1) (name + ": one enumeration")
                let source = Array.copy snapshot
                let onDispose() = for i = 0 to source.Length - 1 do source.[i] <- snapshot.[0]
                let eager = Source((fun _ -> source), "", -1, onDispose)
                geometry (name + ": snapshot survives disposal mutation") wire (expected style snapshot) (build eager)
                Expect.equal eager.Counts (1, 8, 7, 1) (name + ": snapshot acquired before output filling")

        let enumerationAndFailures() =
            for name, wire, style, build in builders do
                for count in [0; 1; 5] do
                    let values = fixture count
                    let source = Source((fun _ -> values), "", -1, ignore)
                    geometry (sprintf "%s, count=%d" name count) wire (expected style values) (build source)
                    Expect.equal source.Counts (1, count + 1, count, 1) (name + ": one eager enumeration and disposal")
                    let queue = Queue<_>(values)
                    let mutable disposed = 0
                    let drained = seq { try while queue.Count > 0 do yield queue.Dequeue() finally disposed <- disposed + 1 }
                    geometry (name + ": destructive source") wire (expected style values) (build drained)
                    Expect.equal (queue.Count, disposed) (0, 1) (name + ": consumed and disposed before return")
                for fault, indices in ["start", [0]; "move", [0; 2; 5]; "current", [0; 2; 4]; "dispose", [0]] do
                    for at in indices do
                        let source = Source((fun _ -> fixture 5), fault, at, ignore)
                        let actual = try build source |> ignore; None with error -> Some error
                        let context = sprintf "%s, fault=%s, index=%d" name fault at
                        Expect.isTrue (actual |> Option.exists (fun e -> obj.ReferenceEquals(e, source.Error))) (context + ": original exception identity")
                        let starts, _, _, disposed = source.Counts
                        Expect.equal (starts, disposed) (1, if fault = "start" then 0 else 1) (context + ": no retry and acquired enumerator disposed once")
                Expect.throwsT<ArgumentNullException> (fun () -> build null |> ignore) (name + ": null source")

        let allocationBounds() =
            let count = 2048
            let values = fixture count
            let uniform = Array.map uniform values
            let plain = Array.map plain values
            let jobs =
                [ "solid corner attributes", false, 0, (fun () -> IndexedGeometryPrimitives.Quad.solidQuadranglesWithColorsAndNormals values)
                  "wire corner attributes", true, 0, (fun () -> IndexedGeometryPrimitives.Quad.wireframeQuadranglesWithColorsAndNormals values)
                  "solid uniform normal", false, 448, (fun () -> IndexedGeometryPrimitives.Quad.solidQuadrangles uniform)
                  "wire uniform normal", true, 448, (fun () -> IndexedGeometryPrimitives.Quad.wireframeQuadrangles uniform)
                  "solid default normal", false, 512, (fun () -> IndexedGeometryPrimitives.Quad.solidQuadrangles' plain)
                  "wire default normal", true, 512, (fun () -> IndexedGeometryPrimitives.Quad.wireframeQuadrangles' plain) ]
            for name, wire, wrapperBytes, build in jobs do
                for _ = 1 to 8 do GC.KeepAlive(build())
                let before = GC.GetAllocatedBytesForCurrentThread()
                for _ = 1 to 4 do GC.KeepAlive(build())
                let bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 4L
                // Final attributes/indices and the retained snapshot; uniform wrappers
                // keep their existing corner tuples, with generous fixed overhead.
                let bound = int64 count * int64 (112 + (if wire then 32 else 24) + 8 + wrapperBytes) + 8192L
                Expect.isLessThan bytes bound (sprintf "%s: no per-quad temporary arrays (bytes=%d, bound=%d)" name bytes bound)

        let checkedLengths() =
            let t = typeof<ISg>.Assembly.GetType("Aardvark.SceneGraph.IndexedGeometryPrimitives", true)
            let method = t.GetMethod("vertexCount", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            Expect.isNotNull method "shared checked output length"
            for factor in [4; 6; 8] do
                for count in [0; 1; Int32.MaxValue / factor] do
                    Expect.equal (method.Invoke(null, [|box count; box factor|]) :?> int) (count * factor) (sprintf "count=%d, factor=%d" count factor)
                let error = try method.Invoke(null, [|box (Int32.MaxValue / factor + 1); box factor|]) |> ignore; None with e -> Some e
                Expect.isTrue (error |> Option.exists (function :? TargetInvocationException as e -> e.InnerException :? OverflowException | _ -> false))
                    (sprintf "factor=%d: oversized output rejected without allocation" factor)

    let tests (target : TestTarget) =
        [
            "Batch layouts and input representations", Cases.batchLayouts
            "Single quadrangles and aliases",          Cases.singleAliases
            "Snapshots and storage isolation",         Cases.snapshotsAndIsolation
            "Enumeration ownership and failures",      Cases.enumerationAndFailures
            "Linear allocation bounds",                Cases.allocationBounds
            "Checked output length boundaries",        Cases.checkedLengths
        ]
        |> prepareCasesCpu "Quadrangles" target
