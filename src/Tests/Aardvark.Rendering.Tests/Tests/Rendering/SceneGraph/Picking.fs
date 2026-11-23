namespace Aardvark.Rendering.Tests.Rendering.SceneGraph

open System
open System.Threading
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
open Expecto

module Picking =

    module private Cases =
        type private CountingBuffer(data : Array) =
            let buffer = ArrayBuffer data
            let mutable downloadCount = 0

            member _.DownloadCount = Volatile.Read &downloadCount

            interface INativeBuffer with
                member _.SizeInBytes = buffer.SizeInBytes

                member _.Use action =
                    Interlocked.Increment &downloadCount |> ignore
                    buffer.Use action

        let private createPickTree (call : DrawCallInfo) (positions : IBuffer) (index : IBuffer option) =
            let positions = BufferView(AVal.constant positions, typeof<V3f>)
            let sg =
                Sg.RenderNode(call, IndexedGeometryMode.TriangleList) :> ISg
                |> Sg.vertexBuffer DefaultSemantic.Positions positions

            let sg =
                match index with
                | Some index ->
                    sg |> Sg.indexBuffer (BufferView(AVal.constant index, typeof<int>))
                | None ->
                    sg

            sg |> Sg.requirePicking |> PickTree.ofSg

        let private randomTrafo() =
            let s = (Rnd.v3d() + 0.1) * 2.0
            let t = (Rnd.v3d() - 0.5) * 10.0
            let r = (Rnd.v3d() - 0.5) * Constant.PiTimesFour
            Trafo3d.Scale s * Trafo3d.Translation t * Trafo3d.RotationEuler r

        let private quad = Quad3d(V3d(-1,-1,0), V3d(1,-1,0), V3d(1,1,0), V3d(-1,1,0))

        let private drawQuad (triangleList: bool) (indexed: bool) =
            let geometry =
                let ig = IndexedGeometry()
                ig.Mode <- IndexedGeometryMode.TriangleStrip
                ig.IndexedAttributes <- SymbolDict()
                ig.IndexedAttributes.[DefaultSemantic.Positions] <- [| V3f quad.P3; V3f quad.P2; V3f quad.P0; V3f quad.P1 |]

                ig
                |> if triangleList then _.ToNonStripped() else id
                |> if indexed then _.ToIndexed() else id

            Sg.ofIndexedGeometry geometry

        let private modeName (mode : IndexedGeometryMode) =
            match mode with
            | IndexedGeometryMode.TriangleList -> "triangle list"
            | IndexedGeometryMode.TriangleStrip -> "triangle strip"
            | IndexedGeometryMode.TriangleAdjacencyList -> "triangle adjacency list"
            | _ -> string mode

        let private primitive (mode : IndexedGeometryMode) (center : V3d) =
            let p0 = center + V3d(-1.0, -1.0, 0.0)
            let p1 = center + V3d(1.0, -1.0, 0.0)
            let p2 = center + V3d(0.0, 1.0, 0.0)

            match mode with
            | IndexedGeometryMode.TriangleAdjacencyList ->
                [| p0; center + V3d(0.0, -2.0, 0.0)
                   p1; center + V3d(2.0, 0.0, 0.0)
                   p2; center + V3d(-2.0, 0.0, 0.0) |]
            | _ ->
                [| p0; p1; p2 |]

        let private pickTree (mode : IndexedGeometryMode) (positions : V3d[]) (indices : int[] option) (call : aval<DrawCallInfo>) =
            let mutable sg = Sg.RenderNode(call, mode) :> ISg
            sg <- sg |> Sg.vertexAttribute' DefaultSemantic.Positions positions

            match indices with
            | Some index -> sg <- sg |> Sg.index' index
            | None -> ()

            sg |> Sg.requirePicking |> PickTree.ofSg

        let private intersects (center : V3d) (tree : PickTree) =
            let ray = Ray3d(center + 10.0 * V3d.ZAxis, -V3d.ZAxis)
            tree.Intersect(ray) |> AVal.force |> Option.isSome

        let drawCallRanges () =
            let modes =
                [| IndexedGeometryMode.TriangleList
                   IndexedGeometryMode.TriangleStrip
                   IndexedGeometryMode.TriangleAdjacencyList |]

            let targetCenter = V3d.Zero
            let excludedCenter = V3d(-20.0, 0.0, 0.0)
            let wrongBaseCenter = V3d(20.0, 0.0, 0.0)

            for mode in modes do
                let target = primitive mode targetCenter
                let excluded = primitive mode excludedCenter
                let count = target.Length

                let nonIndexedCall =
                    DrawCallInfo(
                        FaceVertexCount = count,
                        InstanceCount = 1,
                        FirstIndex = count,
                        BaseVertex = 123
                    )

                let nonIndexedTree =
                    pickTree mode (Array.append excluded target) None (AVal.constant nonIndexedCall)

                let description = $"{modeName mode}, non-indexed"
                Expect.isTrue (intersects targetCenter nonIndexedTree) $"Selected geometry was not pickable ({description})"
                Expect.isFalse (intersects excludedCenter nonIndexedTree) $"Geometry before FirstIndex was pickable ({description})"

                let wrongBase = primitive mode wrongBaseCenter
                let baseVertex = wrongBase.Length + 1
                let positions = Array.concat [ wrongBase; [| V3d(100.0) |]; target; excluded ]
                let excludedIndex = [| count .. 2 * count - 1 |]
                let selectedIndex = [| 0 .. count - 1 |]
                let indices = Array.append excludedIndex selectedIndex

                let indexedCall =
                    DrawCallInfo(
                        FaceVertexCount = count,
                        InstanceCount = 1,
                        FirstIndex = count,
                        BaseVertex = baseVertex
                    )

                let indexedTree = pickTree mode positions (Some indices) (AVal.constant indexedCall)
                let description = $"{modeName mode}, indexed"
                Expect.isTrue (intersects targetCenter indexedTree) $"Selected base-vertex geometry was not pickable ({description})"
                Expect.isFalse (intersects excludedCenter indexedTree) $"Geometry before FirstIndex was pickable ({description})"
                Expect.isFalse (intersects wrongBaseCenter indexedTree) $"BaseVertex was ignored ({description})"

            let target = primitive IndexedGeometryMode.TriangleList targetCenter
            let excluded = primitive IndexedGeometryMode.TriangleList excludedCenter
            let backing =
                Array.append excluded target
                |> Array.collect (fun p -> [| p; V3d(100.0) |])

            let view = BufferView(backing, stride = 2 * sizeof<V3d>)
            let call = DrawCallInfo(FaceVertexCount = 3, InstanceCount = 1, FirstIndex = 3)
            let tree =
                Sg.render IndexedGeometryMode.TriangleList call
                |> Sg.vertexBuffer DefaultSemantic.Positions view
                |> Sg.requirePicking
                |> PickTree.ofSg

            Expect.isTrue (intersects targetCenter tree) "Selected geometry in a strided buffer view was not pickable"
            Expect.isFalse (intersects excludedCenter tree) "Excluded geometry in a strided buffer view was pickable"

        let automaticFaceVertexCount () =
            let positions = primitive IndexedGeometryMode.TriangleList V3d.Zero
            let indices = [| 0; 1; 2 |]
            let call =
                DrawCallInfo(
                    FaceVertexCount = -1,
                    InstanceCount = 1,
                    BaseVertex = Int32.MaxValue
                )

            let tree = pickTree IndexedGeometryMode.TriangleList positions (Some indices) (AVal.constant call)
            Expect.isTrue (intersects V3d.Zero tree) "Automatic FaceVertexCount did not use the scoped count and reset BaseVertex"

        let adaptiveDrawCallRange () =
            let left = V3d(-3.0, 0.0, 0.0)
            let right = V3d(3.0, 0.0, 0.0)
            let positions = Array.append (primitive IndexedGeometryMode.TriangleList left) (primitive IndexedGeometryMode.TriangleList right)

            for indexed in [| false; true |] do
                let call =
                    AVal.init <| DrawCallInfo(
                        FaceVertexCount = 3,
                        InstanceCount = 1,
                        FirstIndex = 0
                    )

                let indices =
                    if indexed then Some [| 0; 1; 2; 0; 1; 2 |]
                    else None

                let tree = pickTree IndexedGeometryMode.TriangleList positions indices call
                Expect.isTrue (intersects left tree) $"Initial draw range was not pickable (indexed = {indexed})"
                Expect.isFalse (intersects right tree) $"Geometry outside the initial draw range was pickable (indexed = {indexed})"

                transact (fun _ ->
                    call.Value <- DrawCallInfo(
                        FaceVertexCount = 3,
                        InstanceCount = 1,
                        FirstIndex = 3,
                        BaseVertex = if indexed then 3 else 0
                    )
                )
                tree.Update()

                Expect.isFalse (intersects left tree) $"Stale geometry remained pickable after a range update (indexed = {indexed})"
                Expect.isTrue (intersects right tree) $"Updated draw range was not pickable (indexed = {indexed})"

        let emptyAndUnderfilledDrawCalls () =
            let modes =
                [| IndexedGeometryMode.TriangleList
                   IndexedGeometryMode.TriangleStrip
                   IndexedGeometryMode.TriangleAdjacencyList |]

            let positions = primitive IndexedGeometryMode.TriangleAdjacencyList V3d.Zero
            let indices = [| 0 .. positions.Length - 1 |]

            for mode in modes do
                for indexed in [| false; true |] do
                    let call =
                        DrawCallInfo(
                            FaceVertexCount = 0,
                            InstanceCount = 1,
                            FirstIndex = Int32.MaxValue,
                            BaseVertex = Int32.MaxValue
                        )
                    let index = if indexed then Some indices else None
                    let tree = pickTree mode positions index (AVal.constant call)
                    Expect.isFalse (intersects V3d.Zero tree) $"Zero-count {modeName mode} draw was pickable (indexed = {indexed})"

            for indexed in [| false; true |] do
                for count in [| 1; 2 |] do
                    let call = DrawCallInfo(FaceVertexCount = count, InstanceCount = 1)
                    let index = if indexed then Some indices else None
                    let tree = pickTree IndexedGeometryMode.TriangleStrip positions index (AVal.constant call)
                    Expect.isFalse (intersects V3d.Zero tree) $"Underfilled triangle strip was pickable (indexed = {indexed}, count = {count})"

        let drawCallRangeOverflow () =
            let positions = primitive IndexedGeometryMode.TriangleList V3d.Zero

            let nonIndexedCall =
                DrawCallInfo(
                    FaceVertexCount = 2,
                    InstanceCount = 1,
                    FirstIndex = Int32.MaxValue
                )

            Expect.throwsT<ArgumentOutOfRangeException>
                (fun _ -> pickTree IndexedGeometryMode.TriangleList positions None (AVal.constant nonIndexedCall) |> ignore)
                "Overflowing non-indexed range was not rejected"

            let indexedCall =
                DrawCallInfo(
                    FaceVertexCount = 3,
                    InstanceCount = 1,
                    BaseVertex = Int32.MaxValue
                )

            Expect.throwsT<ArgumentOutOfRangeException>
                (fun _ -> pickTree IndexedGeometryMode.TriangleList positions (Some [| 1; 1; 1 |]) (AVal.constant indexedCall) |> ignore)
                "Overflowing base-vertex range was not rejected"

            let excessiveSpanCall = DrawCallInfo(FaceVertexCount = 3, InstanceCount = 1)
            Expect.throwsT<ArgumentOutOfRangeException>
                (fun _ ->
                    pickTree
                        IndexedGeometryMode.TriangleList
                        positions
                        (Some [| 0; Int32.MaxValue; 0 |])
                        (AVal.constant excessiveSpanCall)
                    |> ignore
                )
                "Unrepresentable indexed vertex span was not rejected"

        let renderNode (triangleList: bool) (indexed: bool) () =
            let translation = V3d(40.0, 12.0, -23.0)
            let rotation = V3d(3.0, -2.0, 1.0)
            let scaling = V3d(0.5, 2.0, 3.0)
            let trafo = Trafo3d.Translation translation * Trafo3d.RotationEuler rotation * Trafo3d.Scale scaling

            let pickTree =
                drawQuad triangleList indexed
                |> Sg.translation' translation
                |> Sg.rotation' rotation
                |> Sg.scaling' scaling
                |> Sg.requirePicking
                |> PickTree.ofSg

            let ray =
                let target = trafo.TransformPos((quad.P0 + quad.P2 + quad.P3) / 3.0)
                Ray3d(V3d.Zero, target.Normalized)

            let expected =
                let quad = quad.Transformed trafo.Forward
                let mutable t = 0.0
                quad.Intersects(ray, 0.0, infinity, &t) |> flip Expect.isTrue "No hit"
                t

            let result =
                let pick = pickTree.IntersectV ray |> AVal.force
                pick |> ValueOption.get |> RayHit.t

            Expect.approxEquals result expected 0.001 "Invalid intersection"

        let renderCommands  (triangleList: bool) (indexed: bool) () =
            let globalTrafo = AVal.init <| randomTrafo()

            let data =
                List.init 10 (fun _ ->
                    {| visible = AVal.init true
                       switch  = AVal.init true
                       t1      = AVal.init <| randomTrafo()
                       t2      = AVal.init <| randomTrafo() |}
                )

            let pickTree =
                RenderCommand.Ordered (alist {
                    yield RenderCommand.Clear C4b.Blue

                    for d in data do
                        match! d.visible with
                        | true ->
                            let l1 = RenderCommand.Unordered [drawQuad triangleList indexed |> Sg.trafo d.t1]
                            let l2 = RenderCommand.Unordered [drawQuad triangleList indexed |> Sg.trafo d.t2]
                            yield RenderCommand.IfThenElse(d.switch, l1, l2)

                        | _ -> ()
                })
                |> Sg.execute
                |> Sg.trafo globalTrafo
                |> Sg.requirePicking
                |> PickTree.ofSg

            let getExpected (ray: Ray3d) =
                let hits =
                    data |> List.choose (fun data ->
                        if data.visible.GetValue() then
                            let localTrafo = if data.switch.GetValue() then data.t1 else data.t2
                            let trafo = localTrafo.GetValue() * globalTrafo.GetValue()
                            let quad = quad.Transformed trafo.Forward
                            let mutable t = 0.0
                            if quad.Intersects(ray, 0.0, infinity, &t) then
                                Some t
                            else
                                None
                        else
                            None
                    )

                match hits with
                | [] -> None
                | _ -> Some <| List.min hits

            let getResult (ray: Ray3d) =
                let pick = pickTree.Intersect ray |> AVal.force
                pick |> Option.map RayHit.t

            let randomize() =
                transact (fun _ ->
                    if Rnd.bool() then globalTrafo.Value <- randomTrafo()

                    for d in data do
                        d.visible.Value <- Rnd.bool()
                        d.switch.Value <- Rnd.bool()
                        if Rnd.bool() then
                            d.t1.Value <- randomTrafo()
                            d.t2.Value <- randomTrafo()
                )

            let test() =
                for d in data do
                    let ray =
                        let localTrafo = if d.switch.GetValue() then d.t1 else d.t2
                        let trafo = localTrafo.GetValue() * globalTrafo.GetValue()
                        let target = trafo.TransformPos((quad.P0 + quad.P2 + quad.P3) / 3.0)
                        let dir = if target.Length > 0.1 then target.Normalized else V3d.ZAxis
                        Ray3d(V3d.Zero, dir.Normalized)

                    match getResult ray, getExpected ray with
                    | Some result, Some expected -> Expect.approxEquals result expected 0.001 "Invalid intersection"
                    | Some result, _ -> failwithf "Expected no hit but got %A" result
                    | _, Some expected -> failwithf "Expected hit %A but got none" expected
                    | _ -> ()

            test()

            for _ = 1 to 100 do
                randomize()
                test()

            transact (fun _ -> for d in data do d.visible.Value <- false)
            test()

        let cacheReusesEquivalentLeavesAtScale () =
            let positions =
                CountingBuffer([| V3f(-1, -1, -2); V3f(1, -1, -2); V3f(0, 1, -2) |])
            let indices = CountingBuffer([| 0; 1; 2 |])

            let trees =
                Array.init 256 (fun _ ->
                    createPickTree (DrawCallInfo(3)) (positions :> IBuffer) (Some (indices :> IBuffer))
                )

            Expect.equal positions.DownloadCount 1 "Equivalent leaves downloaded shared positions more than once"
            Expect.equal indices.DownloadCount 1 "Equivalent leaves downloaded shared indices more than once"
            Expect.isTrue (intersects (V3d(0, 0, -2)) trees.[0]) "Expected shared geometry to remain pickable"
            GC.KeepAlive trees

        let cacheDoesNotAliasDifferentGeometry () =
            let leftPositions =
                CountingBuffer([| V3f(-5, -1, -4); V3f(-3, -1, -4); V3f(-4, 1, -4) |])
            let rightPositions =
                CountingBuffer([| V3f(3, -1, -4); V3f(5, -1, -4); V3f(4, 1, -4) |])

            let left =
                Array.init 32 (fun _ ->
                    createPickTree (DrawCallInfo(3)) (leftPositions :> IBuffer) None
                )
            let right =
                Array.init 32 (fun _ ->
                    createPickTree (DrawCallInfo(3)) (rightPositions :> IBuffer) None
                )

            Expect.equal leftPositions.DownloadCount 1 "Equivalent left leaves did not share their geometry"
            Expect.equal rightPositions.DownloadCount 1 "Equivalent right leaves did not share their geometry"
            Expect.isTrue (intersects (V3d(-4, 0, -4)) left.[0]) "Expected the left geometry to be pickable"
            Expect.isFalse (intersects (V3d(4, 0, -4)) left.[0]) "Left geometry aliased the right geometry"
            Expect.isTrue (intersects (V3d(4, 0, -4)) right.[0]) "Expected the right geometry to be pickable"
            Expect.isFalse (intersects (V3d(-4, 0, -4)) right.[0]) "Right geometry aliased the left geometry"
            GC.KeepAlive left
            GC.KeepAlive right

        let cacheDoesNotAliasDifferentDrawRanges () =
            let positions =
                CountingBuffer(
                    [|
                        V3f(-5, -1, -4); V3f(-3, -1, -4); V3f(-4, 1, -4)
                        V3f(3, -1, -4); V3f(5, -1, -4); V3f(4, 1, -4)
                    |]
                )

            let short =
                Array.init 32 (fun _ ->
                    createPickTree (DrawCallInfo(3)) (positions :> IBuffer) None
                )
            let long =
                Array.init 32 (fun _ ->
                    createPickTree (DrawCallInfo(6)) (positions :> IBuffer) None
                )

            Expect.equal positions.DownloadCount 2 "Each distinct draw range should download shared positions once"
            Expect.isFalse (intersects (V3d(4, 0, -4)) short.[0]) "The short draw range included the second triangle"
            Expect.isTrue (intersects (V3d(4, 0, -4)) long.[0]) "The long draw range omitted the second triangle"
            GC.KeepAlive short
            GC.KeepAlive long

    let tests (target: TestTarget) =
        [
            "DrawCallInfo subranges",                    Cases.drawCallRanges
            "Automatic FaceVertexCount",                 Cases.automaticFaceVertexCount
            "Adaptive DrawCallInfo range",               Cases.adaptiveDrawCallRange
            "Empty and underfilled DrawCallInfo ranges", Cases.emptyAndUnderfilledDrawCalls
            "DrawCallInfo range overflow",               Cases.drawCallRangeOverflow

            "RenderNode (triangle strip, non-indexed)", Cases.renderNode false false
            "RenderNode (triangle strip, indexed)",     Cases.renderNode false true
            "RenderNode (triangle list, non-indexed)",  Cases.renderNode true false
            "RenderNode (triangle list, indexed)",      Cases.renderNode true true

            "RenderCommands (triangle strip, non-indexed)", Cases.renderCommands false false
            "RenderCommands (triangle strip, indexed)",     Cases.renderCommands false true
            "RenderCommands (triangle list, non-indexed)",  Cases.renderCommands true false
            "RenderCommands (triangle list, indexed)",      Cases.renderCommands true true

            "Cache reuses independently constructed equivalent leaves", Cases.cacheReusesEquivalentLeavesAtScale
            "Cache does not alias different geometry",                  Cases.cacheDoesNotAliasDifferentGeometry
            "Cache does not alias different draw ranges",               Cases.cacheDoesNotAliasDifferentDrawRanges
        ]
        |> prepareCasesCpu "SceneGraph.Picking" target
