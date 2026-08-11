namespace Aardvark.Rendering.Tests

open System
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.Base.Geometry
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
open Expecto
open FShade

module ``SceneGraph Tests`` =

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

    let checkCompleteness =
        test "General.Check Completeness" {
            IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
            let sgTypes = Introspection.GetAllClassesImplementingInterface(typeof<ISg>)

            let sgModule = typeof<Sg.Set>.DeclaringType

            for att, expected in semantics do
                for t in sgTypes do
                    if t.DeclaringType = sgModule then
                        let hasRule = Ag.hasSynRule t expected att
                        Expect.isTrue hasRule <| sprintf "no semantic %A for type %s" att (prettyName t)
        }


    let private testOnActivation (countAfterPrepare : int) (countAfterDispose : int) (sgWithActivator : (ISg -> ISg) -> ISg) =
        use app = TestApplication.create TestBackend.Vulkan
        let runtime = app.Runtime

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
        app.Complete()

    let onActivationMultiRenderObject =
        test "Sg.OnActivation multiple RenderObjects" {

            let sg (withCountingActivator : ISg -> ISg) =
                Sg.ofList [
                    Sg.quad |> Sg.shader { do! DefaultSurfaces.constantColor C4f.Blue }
                    Sg.quad |> Sg.shader { do! DefaultSurfaces.constantColor C4f.Red }
                ]
                |> withCountingActivator

            sg |> testOnActivation 1 0
        }

    let delayModifySurface =
        test "Sg.Delay modify surface" {
            use app = TestApplication.create TestBackend.Vulkan
            let runtime = app.Runtime

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

            app.Complete()
        }

    let modelTrafo =
        test "Sg.ModelTrafo" {
            IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
            Aardvark.Init()

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
        }

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

        let renderNode =
            test "Bounding Box.RenderNode" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

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
            }

        let renderObjectsNode =
            test "Bounding Box.RenderObjectsNode" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

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
            }

        let private commands<'TCommand> (name: string) (toSg: 'TCommand -> ISg)
                                        (clearCmd: C4b -> 'TCommand) (ifThenElseCmd: aval<bool> * 'TCommand * 'TCommand -> 'TCommand)
                                        (orderedCmd: alist<'TCommand> -> 'TCommand) (unorderedCmd: list<ISg> -> 'TCommand) =
            test $"Bounding Box.{name}" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

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
            }

        let renderCommands =
            commands "RenderCommands"
                Sg.execute
                RenderCommand.Clear
                RenderCommand.IfThenElse
                RenderCommand.Ordered
                RenderCommand.Unordered

        let runtimeCommands =
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

            commands "RuntimeCommands"
                toSg
                clearCmd
                RuntimeCommand.IfThenElse
                RuntimeCommand.Ordered
                unorderedCmd

    module Picking =

        type private CountingBuffer(data : Array) =
            let buffer = ArrayBuffer data
            let mutable downloadCount = 0

            member _.DownloadCount = Volatile.Read &downloadCount

            interface INativeBuffer with
                member _.SizeInBytes = buffer.SizeInBytes

                member _.Use action =
                    Interlocked.Increment &downloadCount |> ignore
                    buffer.Use action

        type private FailOnceBuffer(data : Array) =
            let buffer = ArrayBuffer data
            let mutable attemptCount = 0

            member _.AttemptCount = Volatile.Read &attemptCount

            interface INativeBuffer with
                member _.SizeInBytes = buffer.SizeInBytes

                member _.Use action =
                    let count = Interlocked.Increment &attemptCount
                    if count = 1 then
                        raise <| InvalidOperationException("Expected first download failure")
                    buffer.Use action

        type private EphemeralReferences =
            {
                call      : WeakReference
                positions : WeakReference
                index     : WeakReference option
            }

        let private createPickTree (call : aval<DrawCallInfo>) (positions : BufferView) (index : BufferView option) =
            let sg =
                Sg.RenderNode(call, IndexedGeometryMode.TriangleList) :> ISg
                |> Sg.vertexBuffer DefaultSemantic.Positions positions

            let sg =
                match index with
                | Some index -> sg |> Sg.indexBuffer index
                | None -> sg

            sg |> Sg.requirePicking |> PickTree.ofSg

        let private intersects (target : V3d) (tree : PickTree) =
            let ray = Ray3d(V3d.Zero, target.Normalized)
            tree.IntersectV ray |> AVal.force |> ValueOption.isSome

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private createEphemeralPickScene(indexed : bool) =
            let call = AVal.constant <| DrawCallInfo(3)
            let positionBuffer = CountingBuffer([| V3f(-1, -1, -2); V3f(1, -1, -2); V3f(0, 1, -2) |])
            let positions = BufferView(AVal.constant (positionBuffer :> IBuffer), typeof<V3f>)

            let indexBuffer =
                if indexed then Some <| CountingBuffer([| 0; 1; 2 |])
                else None

            let index =
                indexBuffer |> Option.map (fun buffer ->
                    BufferView(AVal.constant (buffer :> IBuffer), typeof<int>)
                )

            let tree = createPickTree call positions index
            let references =
                {
                    call = WeakReference(call :> obj)
                    positions = WeakReference(positionBuffer :> obj)
                    index = indexBuffer |> Option.map (fun buffer -> WeakReference(buffer :> obj))
                }

            Expect.equal positionBuffer.DownloadCount 1 "Expected the ephemeral positions to be downloaded"
            indexBuffer |> Option.iter (fun buffer ->
                Expect.equal buffer.DownloadCount 1 "Expected the ephemeral indices to be downloaded"
            )

            GC.KeepAlive tree
            GC.KeepAlive call
            GC.KeepAlive positionBuffer
            GC.KeepAlive indexBuffer
            references

        let private forceFullCollection() =
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true)
            GC.WaitForPendingFinalizers()
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true)

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

        let private testDescription (name: string) (triangleList: bool) (indexed: bool) =
            let modeDesc = if triangleList then "triangle list" else "triangle strip"
            let indexedDesc = if indexed then "indexed" else "non-indexed"
            $"Picking.{name} ({modeDesc}, {indexedDesc})"

        let renderNode (triangleList: bool) (indexed: bool) =
            test (testDescription "RenderNode" triangleList indexed) {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

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
            }

        let renderCommands  (triangleList: bool) (indexed: bool) =
            test (testDescription "RenderCommands" triangleList indexed)  {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

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
            }

        let cacheReusesEquivalentLiveGeometry =
            test "Picking.Cache reuses equivalent live geometry" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

                let call = AVal.constant <| DrawCallInfo(3)
                let positionBuffer = CountingBuffer([| V3f(-1, -1, -2); V3f(1, -1, -2); V3f(0, 1, -2) |])
                let indexBuffer = CountingBuffer([| 0; 1; 2 |])
                let positions = AVal.constant (positionBuffer :> IBuffer)
                let indices = AVal.constant (indexBuffer :> IBuffer)
                let first =
                    createPickTree call
                        (BufferView(positions, typeof<V3f>))
                        (Some <| BufferView(indices, typeof<int>))
                let second =
                    createPickTree call
                        (BufferView(positions, typeof<V3f>))
                        (Some <| BufferView(indices, typeof<int>))

                Expect.equal positionBuffer.DownloadCount 1 "Expected equivalent live positions to be downloaded once"
                Expect.equal indexBuffer.DownloadCount 1 "Expected equivalent live indices to be downloaded once"
                GC.KeepAlive first
                GC.KeepAlive second
            }

        let cacheRemainsReusableAfterContention =
            test "Picking.Cache remains reusable after contention" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

                let call = AVal.constant <| DrawCallInfo(3)
                let positionBuffer = CountingBuffer([| V3f(-1, -1, -2); V3f(1, -1, -2); V3f(0, 1, -2) |])
                let indexBuffer = CountingBuffer([| 0; 1; 2 |])
                let positions = AVal.constant (positionBuffer :> IBuffer)
                let indices = AVal.constant (indexBuffer :> IBuffer)
                use start = new ManualResetEventSlim(false)

                let create() =
                    createPickTree call
                        (BufferView(positions, typeof<V3f>))
                        (Some <| BufferView(indices, typeof<int>))

                let tasks =
                    Array.init 32 (fun _ ->
                        Task.Run(fun () ->
                            start.Wait()
                            create()
                        )
                    )

                start.Set()
                let trees = tasks |> Array.map _.Result
                let positionDownloads = positionBuffer.DownloadCount
                let indexDownloads = indexBuffer.DownloadCount
                let final = create()

                Expect.equal positionBuffer.DownloadCount positionDownloads "A lookup after contention reconstructed positions"
                Expect.equal indexBuffer.DownloadCount indexDownloads "A lookup after contention reconstructed indices"
                GC.KeepAlive trees
                GC.KeepAlive final
            }

        let cacheKeepsDistinctGeometrySeparate =
            test "Picking.Cache keeps distinct geometry separate" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

                let call = AVal.constant <| DrawCallInfo(3)
                let leftBuffer = CountingBuffer([| V3f(-5, -1, -4); V3f(-3, -1, -4); V3f(-4, 1, -4) |])
                let rightBuffer = CountingBuffer([| V3f(3, -1, -4); V3f(5, -1, -4); V3f(4, 1, -4) |])
                let left =
                    createPickTree call (BufferView(AVal.constant (leftBuffer :> IBuffer), typeof<V3f>)) None
                let right =
                    createPickTree call (BufferView(AVal.constant (rightBuffer :> IBuffer), typeof<V3f>)) None

                Expect.isTrue (intersects (V3d(-4, 0, -4)) left) "Expected the left geometry to be pickable"
                Expect.isFalse (intersects (V3d(4, 0, -4)) left) "Left geometry aliased the right geometry"
                Expect.isTrue (intersects (V3d(4, 0, -4)) right) "Expected the right geometry to be pickable"
                Expect.isFalse (intersects (V3d(-4, 0, -4)) right) "Right geometry aliased the left geometry"
                Expect.equal leftBuffer.DownloadCount 1 "Expected the left geometry to be downloaded once"
                Expect.equal rightBuffer.DownloadCount 1 "Expected the right geometry to be downloaded once"
            }

        let cachePreservesAdaptiveDrawCalls =
            test "Picking.Cache preserves adaptive draw-call propagation" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

                let call = AVal.init <| DrawCallInfo(3)
                let positionBuffer =
                    CountingBuffer(
                        [|
                            V3f(-5, -1, -4); V3f(-3, -1, -4); V3f(-4, 1, -4)
                            V3f(3, -1, -4); V3f(5, -1, -4); V3f(4, 1, -4)
                        |]
                    )
                let positions = BufferView(AVal.constant (positionBuffer :> IBuffer), typeof<V3f>)
                let tree = createPickTree call positions None
                let target = V3d(4, 0, -4)

                Expect.isFalse (intersects target tree) "The second triangle must initially be outside the draw call"
                transact (fun _ -> call.Value <- DrawCallInfo(6))
                Expect.isTrue (intersects target tree) "The cached pickable did not observe the draw-call update"
                Expect.equal positionBuffer.DownloadCount 2 "Expected geometry to be downloaded again after the draw-call update"
            }

        let cacheRecoversFromDownloadFailure =
            test "Picking.Cache recovers from a transient download failure" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

                let call = AVal.constant <| DrawCallInfo(3)
                let positionBuffer = FailOnceBuffer([| V3f(-1, -1, -2); V3f(1, -1, -2); V3f(0, 1, -2) |])
                let positions = BufferView(AVal.constant (positionBuffer :> IBuffer), typeof<V3f>)

                Expect.throwsT<InvalidOperationException>
                    (fun () -> createPickTree call positions None |> ignore)
                    "Expected the first geometry download to fail"

                let tree = createPickTree call positions None
                Expect.isTrue (intersects (V3d(0, 0, -2)) tree) "Expected construction to succeed after the transient failure"
                Expect.equal positionBuffer.AttemptCount 2 "Expected exactly one failed and one successful download"
            }

        let cacheDoesNotRetainScenes =
            test "Picking.Cache releases indexed and non-indexed scenes" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                Aardvark.Init()

                let references =
                    Array.append
                        (Array.init 32 (fun _ -> createEphemeralPickScene false))
                        (Array.init 32 (fun _ -> createEphemeralPickScene true))

                let isAlive references =
                    references.call.IsAlive || references.positions.IsAlive ||
                    (references.index |> Option.exists _.IsAlive)

                for _ = 1 to 10 do
                    if references |> Array.exists isAlive then
                        forceFullCollection()

                let retainedCalls = references |> Array.sumBy (fun r -> if r.call.IsAlive then 1 else 0)
                let retainedPositions = references |> Array.sumBy (fun r -> if r.positions.IsAlive then 1 else 0)
                let retainedIndices =
                    references |> Array.sumBy (fun r ->
                        match r.index with
                        | Some index when index.IsAlive -> 1
                        | _ -> 0
                    )

                Expect.equal retainedCalls 0 "Picking cache retained ephemeral draw calls"
                Expect.equal retainedPositions 0 "Picking cache retained ephemeral position buffers"
                Expect.equal retainedIndices 0 "Picking cache retained ephemeral index buffers"
            }

    [<Tests>]
    let tests =
        testList "SceneGraph" [
            checkCompleteness
            onActivationMultiRenderObject
            delayModifySurface
            modelTrafo
            BoundingBox.renderNode
            BoundingBox.renderObjectsNode
            BoundingBox.renderCommands
            BoundingBox.runtimeCommands
            Picking.renderNode false false
            Picking.renderNode false true
            Picking.renderNode true false
            Picking.renderNode true true
            Picking.renderCommands false false
            Picking.renderCommands false true
            Picking.renderCommands true false
            Picking.renderCommands true true
            Picking.cacheReusesEquivalentLiveGeometry
            Picking.cacheRemainsReusableAfterContention
            Picking.cacheKeepsDistinctGeometrySeparate
            Picking.cachePreservesAdaptiveDrawCalls
            Picking.cacheRecoversFromDownloadFailure
            Picking.cacheDoesNotRetainScenes
        ]
