namespace Aardvark.Rendering.Tests.Rendering

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
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
open FShade

module SceneGraph =

    [<AutoOpen>]
    module private Utilities =
        let semantics =
            [
                "RenderObjects",        typeof<aset<IRenderObject>>
                "GlobalBoundingBox",    typeof<aval<Box3d>>
                "LocalBoundingBox",     typeof<aval<Box3d>>
            ]

        let genericNameRx = Regex @"(?<name>.*?)´[0-9]+"
        let cleanName (name : string) =
            let m = genericNameRx.Match name
            if m.Success then m.Groups.["name"].Value
            else name

        let intrisicNames =
            Dict.ofList [
                typeof<byte>, "byte"
                typeof<int8>, "int8"
                typeof<uint16>, "uint16"
                typeof<int16>, "int16"
                typeof<int>, "int"
                typeof<uint32>, "uint32"
                typeof<int64>, "int64"
                typeof<uint64>, "uint64"
                typeof<obj>, "obj"
            ]

        let rec prettyName (t : Type) =
            match intrisicNames.TryGetValue t with
                | true, n -> n
                | _ ->
                    if t.IsArray then
                        sprintf "%s[]" (t.GetElementType() |> prettyName)
                    elif t.IsGenericType then
                        let args = t.GetGenericArguments() |> Seq.map prettyName |> String.concat ","
                        sprintf "%s<%s>" (cleanName t.Name) args
                    else
                        cleanName t.Name

    module General =
        let checkCompleteness _ =
            let sgTypes = Introspection.GetAllClassesImplementingInterface(typeof<ISg>)

            let sgModule = typeof<Sg.Set>.DeclaringType

            for att, expected in semantics do
                for t in sgTypes do
                    if t.DeclaringType = sgModule then
                        let hasRule = Ag.hasSynRule t expected att
                        Expect.isTrue hasRule <| sprintf "no semantic %A for type %s" att (prettyName t)

    module Sg =
        let private testOnActivation (runtime: IRuntime) (countAfterPrepare : int) (countAfterDispose : int) (sgWithActivator : (ISg -> ISg) -> ISg) =
            use signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ]

            let mutable count = 0

            let sg =
                sgWithActivator (
                    Sg.onActivation (fun _ ->
                        count <- count + 1
                        { new IDisposable with member x.Dispose() = count <- count - 1 }
                    )
                )

            let prepared =
                sg.RenderObjects(Ag.Scope.Root)
                |> ASet.toAVal
                |> AVal.force
                |> HashSet.toList
                |> List.map (fun ro -> runtime.PrepareRenderObject(signature, ro))

            try
                Expect.equal count countAfterPrepare "unexpected count after preparing"
            with e ->
                prepared |> List.iter Disposable.dispose
                raise e

            prepared |> List.iter Disposable.dispose
            Expect.equal count countAfterDispose "unexpected count after disposing"

        let onActivationMultiRenderObject (runtime: IRuntime) =
            let sg (withCountingActivator : ISg -> ISg) =
                Sg.ofList [
                    Sg.quad |> Sg.shader { do! DefaultSurfaces.constantColor C4f.Blue }
                    Sg.quad |> Sg.shader { do! DefaultSurfaces.constantColor C4f.Red }
                ]
                |> withCountingActivator

            sg |> testOnActivation runtime 1 0

        let delayModifySurface (runtime: IRuntime) =
            use signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ]

            let effectAddBlue =
                let shader (v : Effects.Vertex) =
                    fragment {
                        return v.c + V4f.OOIO
                    }

                Effect.ofFunction shader

            use task =
                Sg.delay (fun scope ->
                    Sg.fullScreenQuad
                    |> Sg.effect [
                        match scope.Surface with
                        | Surface.Effect effect -> yield effect
                        | _ -> ()

                        yield effectAddBlue
                    ]
                )
                |> Sg.shader {
                    do! DefaultSurfaces.constantColor C4f.Red
                }
                |> Sg.compile runtime signature

            let size = AVal.constant <| V2i(128)
            let buffer = task |> RenderTask.renderToColor size
            buffer.Acquire()

            try
                let color = buffer.GetValue().Download().AsPixImage<uint8>()
                color |> PixImage.isColor [| 255uy; 0uy; 255uy; 255uy |]

            finally
                buffer.Release()

        let modelTrafo _ =
            let translation = V3d(40.0, 12.0, -23.0)
            let rotation = V3d(3.0, -2.0, 1.0)
            let scaling = V3d(0.5, 2.0, 3.0)

            let sg =
                Sg.fullScreenQuad
                |> Sg.translation' translation
                |> Sg.rotation' rotation
                |> Sg.scaling' scaling

            let ro =
                sg.RenderObjects(Ag.Scope.Root).Content.GetValue()
                |> Seq.head

            let result = ro.AttributeScope.ModelTrafo |> AVal.force
            let expected = Trafo3d.Translation translation * Trafo3d.RotationEuler rotation * Trafo3d.Scale scaling

            Expect.approxEquals result.Forward expected.Forward 0.001 "Invalid model trafo"

    module BoundingBox =

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

        let fixedRenderObjectScopes _ =
            for reverse in [false; true] do fixedScope false reverse

        let fixedCommandScopes _ =
            for reverse in [false; true] do fixedScope true reverse

        let scopeAdaptiveUpdates _ =
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

        let rootAndInvalid _ =
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

        let concurrentSameScope _ =
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

        let concurrentWarmedScopes _ =
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

        let renderNode _ =
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

        let renderObjectsNode _ =
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

        let renderCommands _ =
            commands
                Sg.execute
                RenderCommand.Clear
                RenderCommand.IfThenElse
                RenderCommand.Ordered
                RenderCommand.Unordered

        let runtimeCommands _ =
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

    module Picking =

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

        let renderNode (triangleList: bool) (indexed: bool) _ =
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

        let renderCommands  (triangleList: bool) (indexed: bool) _ =
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

    module Caching =
        module C = SgFSharpHelpers.Caching

        let private same a b message = Expect.isTrue (obj.ReferenceEquals(a, b)) message
        let private different a b message = Expect.isFalse (obj.ReferenceEquals(a, b)) message
        let private semantic = Symbol.Create "RawTransform"
        let private geometry =
            IndexedGeometry(Mode = IndexedGeometryMode.PointList,
                            IndexedAttributes = SymDict.ofList [DefaultSemantic.Positions, [|V3f.Zero|] :> Array])

        let private trafos count =
            let random = Random(673 + count)
            Array.init count (fun i ->
                Trafo3d.Scale(V3d(0.5 + random.NextDouble(), 1.5 + random.NextDouble(), 2.0 + float i)) *
                Trafo3d.RotationEuler(V3d(random.NextDouble(), random.NextDouble(), random.NextDouble())) *
                Trafo3d.Translation(V3d(float i, -3.0 * float i, 1e4 + float i)))

        let private source constant values : aval<_> = if constant then AVal.constant values else cval values :> aval<_>
        let private rawInstance (value : aval<Trafo3d[]>) =
            let sg = Sg.empty |> Sg.instanceAttribute semantic value :?> Sg.InstanceAttributeApplicator
            sg.Values.[semantic]
        let private instanced value = Sg.instancedGeometry value geometry :?> Sg.InstanceAttributeApplicator
        let private pair (sg : Sg.InstanceAttributeApplicator) =
            sg.Values.[DefaultSemantic.InstanceTrafo], sg.Values.[DefaultSemantic.InstanceTrafoInv]

        let private data<'T> (view : BufferView) =
            Expect.equal view.ElementType typeof<'T> "element type"
            Expect.equal view.Offset 0 "offset"
            Expect.equal view.Stride 0 "packed stride"
            Expect.isTrue view.Normalized "normalization flag"
            Expect.isFalse view.IsSingleValue "array buffer"
            let buffer = AVal.force view.Buffer :?> ArrayBuffer
            Expect.equal buffer.ElementType typeof<'T> "buffer element type"
            buffer.Data :?> 'T[]

        let private check (values : Trafo3d[]) raw (forward, backward) =
            same (data<Trafo3d> raw) values "ordinary buffer retains the input array"
            Expect.equal (data<M44f> forward) (values |> Array.map (fun t -> M44f t.Forward)) "forward conversion"
            Expect.equal (data<M44f> backward) (values |> Array.map (fun t -> M44f t.Backward)) "inverse conversion"
            different forward backward "separate forward/inverse views"
            if values.Length > 0 then
                different (data<M44f> forward) (data<M44f> backward) "separate matrix arrays"

        let rec private drawCall (sg : ISg) =
            match sg with
            | :? Sg.RenderNode as node -> AVal.force node.DrawCallInfo
            | :? IApplicator as node -> drawCall (AVal.force node.Child)
            | _ -> failwith "unexpected instance geometry structure"

        let private concurrently count (work : int -> 'T) =
            use start = new Barrier(count)
            let tasks =
                Array.init count (fun i ->
                    Task.Factory.StartNew<'T>((fun () ->
                        if not (start.SignalAndWait(TimeSpan.FromSeconds 10.0)) then failwith "concurrent start timed out"
                        work i), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            Task.WhenAll(tasks).GetAwaiter().GetResult()

        // The cache factory inspects IsConstant. A gate here can detect serialization of
        // unrelated factories without forcing an adaptive value or relying on timing ratios.
        type private FactoryProbe<'T>(value : 'T, visit : unit -> unit) =
            inherit AdaptiveObject()
            interface IAdaptiveValue<'T> with
                member _.IsConstant = visit(); false
                member _.ContentType = typeof<'T>
                member _.GetValue _ = value
                member _.GetValueUntyped _ = box value
                member x.Accept visitor = visitor.Visit (x :> aval<'T>)

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private unreachable constant representation force =
            let values = trafos 3
            let value = source constant values
            let refs = ResizeArray<WeakReference>()
            let add (x : obj) = refs.Add(WeakReference x)
            add values
            add value
            if representation <> "pair" then
                let view = C.bufferOfArray value
                add view
                add view.Buffer
                if force then add (AVal.force view.Buffer)
            if representation <> "array" then
                let views = C.buffersOfTrafos value
                let f, b = views
                add views
                for view in [f; b] do
                    add view
                    add view.Buffer
                    if force then add (AVal.force view.Buffer)
            refs.ToArray()

        let private ordinaryContents (values : 'T[]) =
            for constant in [false; true] do
                let value = source constant values
                let view = C.bufferOfArray value
                same values (data<'T> view) "typed buffer preserves its source array"
                same view (C.bufferOfArray value) "typed cache reuse"
                different view (C.bufferOfArray (source constant values)) "identity, not array equality"

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private weakViews (value : aval<Trafo3d[]>) representation =
            if representation = "array" then WeakReference(C.bufferOfArray value)
            else WeakReference(C.buffersOfTrafos value)

        let private collect() =
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()

        let typedIntArray _      = ordinaryContents [|1; 2; 3|]
        let typedVecArray _      = ordinaryContents [|V3f.Zero; V3f.IOO|]
        let typedColArray _      = ordinaryContents [|C4b.Red; C4b.Blue|]
        let typedMatArray _      = ordinaryContents [|M44f.Identity|]
        let typedRefArray _      = ordinaryContents [|"a"; "b"|]
        let typedEmptyRefArray _ = ordinaryContents Array.empty<string>

        let publicSharing _ =
            for constant in [false; true] do
                for rawFirst in [false; true] do
                    for count in [0; 1; 7] do
                        let values = trafos count
                        let snapshot = Array.copy values
                        let value = source constant values
                        let raw, sg =
                            if rawFirst then let raw = rawInstance value in raw, instanced value
                            else let sg = instanced value in rawInstance value, sg
                        let forward, backward = pair sg
                        check values raw (forward, backward)
                        same raw (rawInstance value) "ordinary view reused"
                        let nextForward, nextBackward = pair (instanced value)
                        same forward nextForward "forward view reused"
                        same backward nextBackward "inverse view reused"
                        Expect.equal (drawCall sg).InstanceCount count "instance count"
                        Expect.equal values snapshot "source unchanged"

        let vertexInstanceIndexReuse _ =
            for constant in [false; true] do
                let values = [|2; 0; 1|]
                let value = source constant values
                let vertex = Sg.empty |> Sg.vertexAttribute DefaultSemantic.Positions value :?> Sg.VertexAttributeApplicator
                let instance = Sg.empty |> Sg.instanceAttribute DefaultSemantic.Colors value :?> Sg.InstanceAttributeApplicator
                let index = Sg.empty |> Sg.index value :?> Sg.VertexIndexApplicator
                let view = vertex.Values.[DefaultSemantic.Positions]
                same view instance.Values.[DefaultSemantic.Colors] "reuse across semantics and attribute rates"
                same view index.Value "reuse for indices"
                same view (C.bufferOfArray value) "public and helper lookups agree"
                same values (data<int> view) "ordinary contents unchanged"

        let distinctEqualSources _ =
            for constant in [false; true] do
                for representation in ["array"; "pair"] do
                    let values = trafos 2
                    let a, b = source constant values, source constant values
                    different a b "distinct source objects"
                    if representation = "array" then
                        let x, y = C.bufferOfArray a, C.bufferOfArray b
                        different x y "distinct source identities do not share views"
                        same x (C.bufferOfArray a) "same source does share"
                    else
                        let x, y = C.buffersOfTrafos a, C.buffersOfTrafos b
                        different x y "distinct source identities do not share pairs"
                        different (fst x) (fst y) "distinct forward views"
                        different (snd x) (snd y) "distinct inverse views"
                        same x (C.buffersOfTrafos a) "published pair reused"

        let adaptiveContents _ =
            for rawFirst in [false; true] do
                let value = cval (trafos 3)
                let raw, sg =
                    if rawFirst then let raw = rawInstance value in raw, instanced value
                    else let sg = instanced value in rawInstance value, sg
                let views = pair sg
                for frame, count in List.indexed [3; 3; 1; 0; 7; 2; 0; 5] do
                    let values = trafos count |> Array.map (fun t -> t * Trafo3d.Translation(float frame, 0.0, 0.0))
                    let snapshot = Array.copy values
                    transact (fun () -> value.Value <- values)
                    Expect.equal value.Value values "adaptive contents (structurally equal assignments may be suppressed)"
                    check value.Value raw views
                    Expect.equal (drawCall sg).InstanceCount count "adaptive instance count"
                    same raw (C.bufferOfArray value) "updates do not replace ordinary view"
                    let f, b = C.buffersOfTrafos value
                    same f (fst views) "updates do not replace forward view"
                    same b (snd views) "updates do not replace inverse view"
                    Expect.equal values snapshot "updating and converting do not mutate input"

        let deferredEval _ =
            for constant in [false; true] do
                for representation in ["array"; "pair"] do
                    let mutable reads = 0
                    let read() = reads <- reads + 1; trafos 2
                    let value = if constant then AVal.delay read else AVal.custom (fun _ -> read())
                    let views = if representation = "array" then [C.bufferOfArray value] else let f, b = C.buffersOfTrafos value in [f; b]
                    Expect.equal reads 0 "lookup does not evaluate the source"
                    for view in views do AVal.force view.Buffer |> ignore
                    Expect.equal reads 1 "derived buffers share the adaptive source evaluation"
                    for view in views do AVal.force view.Buffer |> ignore
                    Expect.equal reads 1 "unchanged reads remain cached"

        let concurrentColdPublication _ =
            for constant in [false; true] do
                for representation in ["array"; "pair"; "mixed"] do
                    for _ in 1 .. 24 do
                        let value = source constant (trafos 3)
                        let results = concurrently 8 (fun i ->
                            if representation = "array" || (representation = "mixed" && i % 2 = 0) then
                                Choice1Of2(C.bufferOfArray value)
                            else Choice2Of2(C.buffersOfTrafos value))
                        for result in results do
                            match result with
                            | Choice1Of2 view -> same view (C.bufferOfArray value) "one published ordinary view"
                            | Choice2Of2 views -> same views (C.buffersOfTrafos value) "one published pair"

        let unrelatedColdKeys _ =
            for representation in ["array"; "pair"; "mixed"] do
                use entered = new CountdownEvent(8)
                use release = new ManualResetEventSlim(false)
                let tasks = Array.init 8 (fun i ->
                    let mutable first = 1
                    let value = FactoryProbe(trafos 1, fun () ->
                        if Interlocked.Exchange(&first, 0) = 1 then entered.Signal() |> ignore
                        if not (release.Wait(TimeSpan.FromSeconds 10.0)) then failwith "factory release timed out")
                    Task.Factory.StartNew((fun () ->
                        if representation = "array" || (representation = "mixed" && i % 2 = 0) then C.bufferOfArray value |> ignore
                        else C.buffersOfTrafos value |> ignore), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
                let allEntered = entered.Wait(TimeSpan.FromSeconds 5.0)
                release.Set()
                Task.WhenAll(tasks).GetAwaiter().GetResult()
                Expect.isTrue allEntered "unrelated factories can all enter before any completes"

        let factoryExceptions _ =
            for representation in ["array"; "pair"] do
                let error = InvalidOperationException("factory probe")
                let mutable fail = true
                let value = FactoryProbe(trafos 1, fun () -> if fail then raise error)
                let lookup() = if representation = "array" then box (C.bufferOfArray value) else box (C.buffersOfTrafos value)
                let thrown = try lookup() |> ignore; None with e -> Some e
                Expect.isTrue (thrown |> Option.exists (fun e -> obj.ReferenceEquals(e, error))) "original failure"
                fail <- false
                let result = lookup()
                same result (lookup()) "retry publishes a reusable result"

        let warmedLookupAlloc _ =
            for constant in [false; true] do
                for representation in ["array"; "pair"] do
                    let value = source constant (trafos 1)
                    let run = if representation = "array" then fun () -> GC.KeepAlive(C.bufferOfArray value) else fun () -> GC.KeepAlive(C.buffersOfTrafos value)
                    for _ in 1 .. 100 do run()
                    let before = GC.GetAllocatedBytesForCurrentThread()
                    for _ in 1 .. 10000 do run()
                    let allocated = GC.GetAllocatedBytesForCurrentThread() - before
                    Expect.equal allocated 0L "zero allocation on warm cache hits"

        let liveSources _ =
            for representation in ["array"; "pair"] do
                let value = cval (trafos 1)
                let views = weakViews value representation
                for _ in 1 .. 3 do collect()
                Expect.isTrue views.IsAlive "values stay published while the key is alive"
                let actual = if representation = "array" then box (C.bufferOfArray value) else box (C.buffersOfTrafos value)
                same views.Target actual "live-key identity survives GC"
                GC.KeepAlive value

        let unreachableSources _ =
            for constant in [false; true] do
                for representation in ["array"; "pair"; "mixed"] do
                    for force in [false; true] do
                        let live = cval [|17|]
                        let liveView = C.bufferOfArray live
                        let refs = unreachable constant representation force
                        for _ in 1 .. 5 do collect()
                        Expect.isTrue (refs |> Array.forall (fun r -> not r.IsAlive)) "weak cache does not retain source/view cycles"
                        same liveView (C.bufferOfArray live) "unrelated live entry remains usable"
                        GC.KeepAlive live

    let tests (target: TestTarget) =
        [
            if target = TestTarget.Cpu then
                "General.Check Completeness", General.checkCompleteness

                "Sg.ModelTrafo", Sg.modelTrafo

                "Bounding Box.RenderNode",        BoundingBox.renderNode
                "Bounding Box.RenderObjectsNode", BoundingBox.renderObjectsNode
                "Bounding Box.RenderCommands",    BoundingBox.renderCommands
                "Bounding Box.RuntimeCommands",   BoundingBox.runtimeCommands

                "Bounding Box.Scope-aware cache render-object sets",        BoundingBox.fixedRenderObjectScopes
                "Bounding Box.Scope-aware cache runtime commands",          BoundingBox.fixedCommandScopes
                "Bounding Box.Scope-aware cache adaptive inputs",           BoundingBox.scopeAdaptiveUpdates
                "Bounding Box.Scope-aware cache root and invalid bounds",   BoundingBox.rootAndInvalid
                "Bounding Box.Scope-aware cache concurrent first lookup",   BoundingBox.concurrentSameScope
                "Bounding Box.Scope-aware cache concurrent warmed lookup",  BoundingBox.concurrentWarmedScopes

                "Picking.RenderNode (triangle strip, non-indexed)", Picking.renderNode false false
                "Picking.RenderNode (triangle strip, indexed)",     Picking.renderNode false true
                "Picking.RenderNode (triangle list, non-indexed)",  Picking.renderNode true false
                "Picking.RenderNode (triangle list, indexed)",      Picking.renderNode true true

                "Picking.RenderCommands (triangle strip, non-indexed)", Picking.renderCommands false false
                "Picking.RenderCommands (triangle strip, indexed)",     Picking.renderCommands false true
                "Picking.RenderCommands (triangle list, non-indexed)",  Picking.renderCommands true false
                "Picking.RenderCommands (triangle list, indexed)",      Picking.renderCommands true true

                "Buffer cache.typed int array",                         Caching.typedIntArray
                "Buffer cache.typed vector array",                      Caching.typedVecArray
                "Buffer cache.typed color array",                       Caching.typedColArray
                "Buffer cache.typed matrix array",                      Caching.typedMatArray
                "Buffer cache.typed reference array",                   Caching.typedRefArray
                "Buffer cache.typed empty reference array",             Caching.typedEmptyRefArray
                "Buffer cache.public sharing",                          Caching.publicSharing
                "Buffer cache.vertex/instance/index reuse",             Caching.vertexInstanceIndexReuse
                "Buffer cache.distinct equal sources",                  Caching.distinctEqualSources
                "Buffer cache.adaptive contents and resizing",          Caching.adaptiveContents
                "Buffer cache.deferred evaluation",                     Caching.deferredEval
                "Buffer cache.concurrent cold publication",             Caching.concurrentColdPublication
                "Buffer cache.unrelated cold keys are not serialized",  Caching.unrelatedColdKeys
                "Buffer cache.factory exceptions allow retry",          Caching.factoryExceptions
                "Buffer cache.warmed lookup allocation",                Caching.warmedLookupAlloc
                "Buffer cache.live source retains its published views", Caching.liveSources
                "Buffer cache.unreachable source and views collected",  Caching.unreachableSources

            elif target.IsGpu then
                "Sg.OnActivation multiple RenderObjects", Sg.onActivationMultiRenderObject
                "Sg.Delay modify surface",                Sg.delayModifySurface
        ]
        |> prepareCases "SceneGraph" target