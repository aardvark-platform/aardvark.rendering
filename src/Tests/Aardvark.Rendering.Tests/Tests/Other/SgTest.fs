namespace Aardvark.Rendering.Tests

open System
open System.Reflection
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
        ]