namespace Aardvark.Rendering.Tests.Rendering.SceneGraph

open System
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
open Expecto

module BoundingBox =

    module private Cases =
        let private randomTrafo() =
            let s = (Rnd.v3d() + 0.1) * 2.0
            let t = (Rnd.v3d() - 0.5) * 10.0
            let r = (Rnd.v3d() - 0.5) * Constant.PiTimesFour
            Trafo3d.Scale s * Trafo3d.Translation t * Trafo3d.RotationEuler r

        let private drawLeaf (trafo: aval<Trafo3d>) (positions: V3f[]) =
            Sg.draw IndexedGeometryMode.TriangleList
            |> Sg.vertexArray DefaultSemantic.Positions positions
            |> Sg.trafo trafo

        let private getSingleRenderObject (sg: ISg) =
            let ro =
                sg.RenderObjects(Ag.Scope.Root).Content.GetValue()
                |> Seq.exactlyOne

            ro :?> RenderObject

        let private scopeWithModelTrafo (trafo: aval<Trafo3d>) =
            drawLeaf trafo [| V3f.Zero; V3f.XAxis; V3f.YAxis |]
            |> getSingleRenderObject
            |> _.AttributeScope

        let private adaptiveRenderObject (localTrafo: aval<Trafo3d>) (positions: aval<V3f[]>) (indices: aval<int[]>) =
            Sg.draw IndexedGeometryMode.TriangleList
            |> Sg.vertexAttribute DefaultSemantic.Positions positions
            |> Sg.index indices
            |> Sg.trafo localTrafo
            |> getSingleRenderObject

        let private transformedBox (positions: V3f[]) (indices: int[]) (localTrafo: Trafo3d) (outerTrafo: Trafo3d) =
            let trafo = localTrafo * outerTrafo
            indices
            |> Array.map (fun index -> positions.[index] |> V3d |> Mat.transformPos trafo.Forward)
            |> Box3d

        let private fixedScope (runtimeCommand: bool) (reverse: bool) =
            let path = if runtimeCommand then "RuntimeCommands" else "RenderObjectSet"
            let order = if reverse then "second-then-first" else "first-then-second"

            let positions =
                [| V3f(-1.0f, -2.0f, 0.5f)
                   V3f(2.0f, 0.5f, -1.0f)
                   V3f(0.25f, 4.0f, 3.0f) |]

            let localTrafo = Trafo3d.Scale(0.5, 2.0, 1.5) * Trafo3d.Translation(1.0, -3.0, 2.0)
            let firstOuter = Trafo3d.Translation(10.0, 2.0, -4.0)
            let secondOuter = Trafo3d.Translation(-7.0, 5.0, 9.0)

            let objects = (drawLeaf (AVal.constant localTrafo) positions).RenderObjects(Ag.Scope.Root)

            let shared =
                if runtimeCommand then
                    let command = RuntimeCommand.Render objects
                    let ro = CommandRenderObject(RenderPass.main, Ag.Scope.Root, command) :> IRenderObject
                    Sg.renderObjectSet (ASet.single ro)
                else
                    Sg.renderObjectSet objects

            let first = shared |> Sg.trafo' firstOuter
            let second = shared |> Sg.trafo' secondOuter

            let firstBox, secondBox =
                if reverse then
                    let secondBox = second.GlobalBoundingBox(Ag.Scope.Root) |> AVal.force
                    let firstBox = first.GlobalBoundingBox(Ag.Scope.Root) |> AVal.force
                    firstBox, secondBox
                else
                    let firstBox = first.GlobalBoundingBox(Ag.Scope.Root) |> AVal.force
                    let secondBox = second.GlobalBoundingBox(Ag.Scope.Root) |> AVal.force
                    firstBox, secondBox

            let all = Array.init positions.Length id
            let expectedFirst = transformedBox positions all localTrafo firstOuter
            let expectedSecond = transformedBox positions all localTrafo secondOuter
            Expect.approxEquals firstBox expectedFirst 0.001 $"{path}, {order}: First caller scope was ignored"
            Expect.approxEquals secondBox expectedSecond 0.001 $"{path}, {order}: Second caller scope reused the first bounds"

        let fixedRenderObjectScopes () =
            for reverse in [false; true] do fixedScope false reverse

        let fixedCommandScopes () =
            for reverse in [false; true] do fixedScope true reverse

        let scopeAdaptiveUpdates () =
            let positions =
                cval [| V3f(-2.0f, 1.0f, 0.0f); V3f(4.0f, -3.0f, 2.0f); V3f(0.5f, 5.0f, -1.0f); V3f(7.0f, 2.0f, 3.0f) |]
            let indices = cval [| 0; 2 |]
            let localTrafo = cval (Trafo3d.Translation(1.0, 2.0, 3.0))
            let firstOuter = cval Trafo3d.Identity
            let secondOuter = cval Trafo3d.Identity

            let ro = adaptiveRenderObject localTrafo positions indices
            let firstScope = scopeWithModelTrafo firstOuter
            let secondScope = scopeWithModelTrafo secondOuter
            let firstBox = ro.GetBoundingBox firstScope
            let firstBoxAgain = ro.GetBoundingBox firstScope
            let secondBox = ro.GetBoundingBox secondScope

            Expect.isTrue (Object.ReferenceEquals(firstBox, firstBoxAgain)) "Repeated scope lookup did not reuse the adaptive"
            Expect.isFalse (Object.ReferenceEquals(firstBox, secondBox)) "Distinct outer adaptives shared a cached bounding box"

            let check() =
                let expectedFirst = transformedBox positions.Value indices.Value localTrafo.Value firstOuter.Value
                let expectedSecond = transformedBox positions.Value indices.Value localTrafo.Value secondOuter.Value
                Expect.approxEquals (firstBox.GetValue()) expectedFirst 0.001 "First adaptive bounds are stale"
                Expect.approxEquals (secondBox.GetValue()) expectedSecond 0.001 "Second adaptive bounds are stale"

            check()

            transact (fun _ ->
                positions.Value <- [| V3f(-8.0f, 4.0f, 1.0f); V3f(2.0f, 9.0f, -5.0f); V3f(6.0f, -1.0f, 7.0f); V3f(3.0f, 3.0f, 3.0f) |]
            )
            check()

            transact (fun _ -> indices.Value <- [| 1; 3 |])
            check()

            transact (fun _ -> localTrafo.Value <- Trafo3d.Scale(2.0, 0.5, 1.5) * Trafo3d.Translation(-2.0, 1.0, 4.0))
            check()

            let secondBefore = secondBox.GetValue()
            transact (fun _ -> firstOuter.Value <- Trafo3d.Translation(20.0, -4.0, 2.0))
            check()
            Expect.approxEquals (secondBox.GetValue()) secondBefore 0.001 "Updating the first outer transform changed the second bounds"

            transact (fun _ -> secondOuter.Value <- Trafo3d.Translation(-11.0, 8.0, -3.0))
            check()

        let rootAndInvalid () =
            let positions = [| V3f(-1.0f); V3f(2.0f) |]
            let localTrafo = Trafo3d.Translation(3.0, 4.0, 5.0)
            let ro = drawLeaf (AVal.constant localTrafo) positions |> getSingleRenderObject
            let explicitRoot = ro.GetBoundingBox Ag.Scope.Root
            let implicitRoot = ro.GetBoundingBox()

            Expect.isTrue (Object.ReferenceEquals(explicitRoot, implicitRoot)) "No-argument lookup did not reuse the root entry"
            let expected = transformedBox positions [| 0; 1 |] localTrafo Trafo3d.Identity
            Expect.approxEquals (implicitRoot.GetValue()) expected 0.001 "Root bounds changed"

            let invalid =
                Sg.draw IndexedGeometryMode.TriangleList
                |> getSingleRenderObject
                |> _.GetBoundingBox()
                |> AVal.force

            Expect.equal invalid Box3d.Invalid "Missing positions no longer produce invalid bounds"

        let concurrentSameScope () =
            let positions = AVal.constant [| V3f.Zero; V3f.XAxis; V3f.YAxis |]
            let indices = AVal.constant [| 0; 1; 2 |]
            let ro = adaptiveRenderObject (AVal.constant Trafo3d.Identity) positions indices
            let scope = scopeWithModelTrafo (AVal.constant (Trafo3d.Translation(5.0, 6.0, 7.0)))
            use gate = new ManualResetEventSlim(false)

            let tasks =
                Array.init 64 (fun _ ->
                    Task.Run(fun () ->
                        if not (gate.Wait(TimeSpan.FromSeconds 10.0)) then failtest "Concurrent first lookup start timed out"
                        ro.GetBoundingBox scope
                    )
                )

            gate.Set()
            Expect.isTrue (Task.WaitAll(tasks |> Array.map (fun task -> task :> Task), TimeSpan.FromSeconds 30.0))
                "Concurrent first lookup did not finish"

            let first = tasks.[0].Result
            for task in tasks do
                Expect.isTrue (Object.ReferenceEquals(first, task.Result)) "Concurrent lookup published multiple adaptives"

        let concurrentWarmedScopes () =
            let positions = [| V3f(-1.0f, 2.0f, 0.5f); V3f(4.0f, -2.0f, 3.0f); V3f(0.0f, 5.0f, -1.0f) |]
            let indices = [| 0; 2 |]
            let local = Trafo3d.Scale(2.0, 0.5, 1.5) * Trafo3d.Translation(1.0, -3.0, 2.0)
            let ro = adaptiveRenderObject (AVal.constant local) (AVal.constant positions) (AVal.constant indices)
            let outer = [| Trafo3d.Translation(5.0, 6.0, 7.0); Trafo3d.Translation(-11.0, 2.0, 3.0); Trafo3d.Translation(20.0, -4.0, 5.0) |]
            let scopes = outer |> Array.map (AVal.constant >> scopeWithModelTrafo)
            let adaptives = scopes |> Array.map ro.GetBoundingBox
            let boxes = outer |> Array.map (transformedBox positions indices local)

            let check context index =
                let actual = ro.GetBoundingBox scopes.[index]
                Expect.isTrue (Object.ReferenceEquals(actual, adaptives.[index])) $"{context}, scope={index}: stable adaptive identity"
                Expect.approxEquals (AVal.force actual) boxes.[index] 0.001 $"{context}, scope={index}: correct bounds"

            for i in 0 .. scopes.Length - 1 do
                check "warm-up" i
                for j in 0 .. i - 1 do
                    Expect.isFalse (Object.ReferenceEquals(adaptives.[i], adaptives.[j])) $"scopes={i}/{j}: distinct entries"

            // Three callers cannot all fit in the two recent slots; rotating through them exercises Remember.
            use stop = new CancellationTokenSource(TimeSpan.FromSeconds 30.0)
            use start = new Barrier(4)
            let tasks =
                Array.init 4 (fun worker ->
                    Task.Factory.StartNew((fun () ->
                        start.SignalAndWait(stop.Token)
                        for iteration in 0 .. 4095 do
                            stop.Token.ThrowIfCancellationRequested()
                            check $"worker={worker}, iteration={iteration}" ((worker + iteration) % scopes.Length)
                    ), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                )
            Expect.isTrue (Task.WaitAll(tasks, TimeSpan.FromSeconds 40.0))
                "Concurrent warmed lookups did not finish"

            // Lost recent hits may persist after contention; fallback must still preserve bounds and identity.
            for iteration in 0 .. 95 do check $"after contention, iteration={iteration}" (iteration % scopes.Length)

        let renderNode () =
            let positions = [| V3f(-0.5f, -0.25f, 0.0f); V3f(0.0f, -10.0f, 5.0f); V3f(3.0f, 7.0f, -7.0f) |]

            let translation = V3d(40.0, 12.0, -23.0)
            let rotation = V3d(3.0, -2.0, 1.0)
            let scaling = V3d(0.5, 2.0, 3.0)
            let trafo = Trafo3d.Translation translation * Trafo3d.RotationEuler rotation * Trafo3d.Scale scaling

            let sg =
                Sg.draw IndexedGeometryMode.TriangleList
                |> Sg.vertexArray DefaultSemantic.Positions positions
                |> Sg.translation' translation
                |> Sg.rotation' rotation
                |> Sg.scaling' scaling

            let expected = positions |> Array.map (V3d >> Mat.transformPos trafo.Forward) |> Box3d
            let lbb = sg.LocalBoundingBox Ag.Scope.Root |> AVal.force
            let gbb = sg.GlobalBoundingBox Ag.Scope.Root |> AVal.force

            Expect.approxEquals lbb expected 0.001 "Invalid local bounding box"
            Expect.approxEquals gbb expected 0.001 "Invalid global bounding box"

        let renderObjectsNode () =
            let positions = [| V3f(-0.5f, -0.25f, 0.0f); V3f(0.0f, -10.0f, 5.0f); V3f(3.0f, 7.0f, -7.0f) |]
            let globalTrafo = AVal.init <| randomTrafo()

            let data =
                List.init 10 (fun _ ->
                    {| visible = AVal.init true
                       trafo   = AVal.init <| randomTrafo() |}
                )

            let sg =
                AList.ofList data
                |> AList.chooseA (fun data ->
                    data.visible |> AVal.map (fun visible ->
                        if visible then
                            Some <| drawLeaf data.trafo positions
                        else
                            None
                    )
                )
                |> AList.toASet
                |> ASet.collect _.RenderObjects(Ag.Scope.Root)
                |> Sg.renderObjectSet
                |> Sg.trafo globalTrafo

            let expected =
                AVal.custom (fun t ->
                    (Box3d.Invalid, data) ||> List.fold (fun result data ->
                        if data.visible.GetValue t then
                            let trafo = data.trafo.GetValue t * globalTrafo.GetValue t
                            let bb = positions |> Array.map (V3d >> Mat.transformPos trafo.Forward) |> Box3d
                            Box.Union(result, bb)
                        else
                            result
                    )
                )

            let actualLocal = sg.LocalBoundingBox Ag.Scope.Root
            let actualGlobal = sg.GlobalBoundingBox Ag.Scope.Root

            let randomize() =
                transact (fun _ ->
                    if Rnd.bool() then globalTrafo.Value <- randomTrafo()

                    for d in data do
                        d.visible.Value <- Rnd.bool()
                        if Rnd.bool() then d.trafo.Value <- randomTrafo()
                )

            let test() =
                let expected = expected.GetValue()
                let actualLocal = actualLocal.GetValue()
                let actualGlobal = actualGlobal.GetValue()
                Expect.approxEquals actualLocal expected 0.001 "Invalid local bounding box"
                Expect.approxEquals actualGlobal expected 0.001 "Invalid global bounding box"

            test()

            for _ = 1 to 100 do
                randomize()
                test()

            transact (fun _ -> for d in data do d.visible.Value <- false)
            test()

        let private commands<'TCommand> (toSg: 'TCommand -> ISg)
                                        (clearCmd: C4b -> 'TCommand) (ifThenElseCmd: aval<bool> * 'TCommand * 'TCommand -> 'TCommand)
                                        (orderedCmd: alist<'TCommand> -> 'TCommand) (unorderedCmd: list<ISg> -> 'TCommand) =
            let positions = [| V3f(-0.5f, -0.25f, 0.0f); V3f(0.0f, -10.0f, 5.0f); V3f(3.0f, 7.0f, -7.0f) |]
            let globalTrafo = AVal.init <| randomTrafo()

            let data =
                List.init 10 (fun _ ->
                    {| visible = AVal.init true
                       switch  = AVal.init true
                       t1      = AVal.init <| randomTrafo()
                       t2      = AVal.init <| randomTrafo() |}
                )

            let sg =
                orderedCmd (alist {
                    yield clearCmd C4b.Blue

                    for d in data do
                        match! d.visible with
                        | true ->
                            let l1 = unorderedCmd [drawLeaf d.t1 positions]
                            let l2 = unorderedCmd [drawLeaf d.t2 positions]
                            yield ifThenElseCmd(d.switch, l1, l2)

                        | _ -> ()
                })
                |> toSg
                |> Sg.trafo globalTrafo

            let expected =
                AVal.custom (fun t ->
                    (Box3d.Invalid, data) ||> List.fold (fun result data ->
                        if data.visible.GetValue t then
                            let localTrafo = if data.switch.GetValue t then data.t1 else data.t2
                            let trafo = localTrafo.GetValue t * globalTrafo.GetValue t
                            let bb = positions |> Array.map (V3d >> Mat.transformPos trafo.Forward) |> Box3d
                            Box.Union(result, bb)
                        else
                            result
                    )
                )

            let actualLocal = sg.LocalBoundingBox Ag.Scope.Root
            let actualGlobal = sg.GlobalBoundingBox Ag.Scope.Root

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
                let expected = expected.GetValue()
                let actualLocal = actualLocal.GetValue()
                let actualGlobal = actualGlobal.GetValue()
                Expect.approxEquals actualLocal expected 0.001 "Invalid local bounding box"
                Expect.approxEquals actualGlobal expected 0.001 "Invalid global bounding box"

            test()

            for _ = 1 to 100 do
                randomize()
                test()

            transact (fun _ -> for d in data do d.visible.Value <- false)
            test()

        let renderCommands () =
            commands
                Sg.execute
                RenderCommand.Clear
                RenderCommand.IfThenElse
                RenderCommand.Ordered
                RenderCommand.Unordered

        let runtimeCommands () =
            let toSg (cmd: RuntimeCommand) =
                let ro = CommandRenderObject(RenderPass.main, Ag.Scope.Root, cmd) :> IRenderObject
                ro |> ASet.single |> Sg.renderObjectSet

            let clearCmd (color: C4b) =
                let values = ClearValues.ofColor color
                RuntimeCommand.Clear (AVal.constant values)

            let unorderedCmd (sg: ISg list) =
                let ro =
                    sg
                    |> List.map _.RenderObjects(Ag.Scope.Root)
                    |> ASet.ofList
                    |> ASet.unionMany

                RuntimeCommand.Render ro

            commands
                toSg
                clearCmd
                RuntimeCommand.IfThenElse
                RuntimeCommand.Ordered
                unorderedCmd

    let tests (target: TestTarget) =
        [
            "RenderNode",        Cases.renderNode
            "RenderObjectsNode", Cases.renderObjectsNode
            "RenderCommands",    Cases.renderCommands
            "RuntimeCommands",   Cases.runtimeCommands

            "Scope-aware cache render-object sets",        Cases.fixedRenderObjectScopes
            "Scope-aware cache runtime commands",          Cases.fixedCommandScopes
            "Scope-aware cache adaptive inputs",           Cases.scopeAdaptiveUpdates
            "Scope-aware cache root and invalid bounds",   Cases.rootAndInvalid
            "Scope-aware cache concurrent first lookup",   Cases.concurrentSameScope
            "Scope-aware cache concurrent warmed lookup",  Cases.concurrentWarmedScopes
        ]
        |> prepareCasesCpu "SceneGraph.BoundingBox" target
