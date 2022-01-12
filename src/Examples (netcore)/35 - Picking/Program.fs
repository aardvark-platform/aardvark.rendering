open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open System

(*

This example demonstrates how to implement picking by rendering object IDs and other attributes into a texture, which is used for subsequent lookups.

*)

module DefaultSemantic =
    let PickData = Sym.ofString "PickData"
    let CustomId = Sym.ofString "CustomId"

module RenderPass =
    let pickingInfo = RenderPass.after "pickingInfo" RenderPassOrder.Arbitrary RenderPass.main

module OctNormal =

    // Encode normals as a V2d by mapping to an octahedron
    // and projecting onto the z=0 plane (Meyer et al. 2010)
    [<ReflectedDefinition>]
    let private signNonZero (v : V2d) =
        V2d (
            (if v.X >= 0.0 then 1.0 else - 1.0),
            (if v.Y >= 0.0 then 1.0 else - 1.0)
        )

    [<ReflectedDefinition>]
    let encode (n : V3d) =
        let p = n.XY * (1.0 / (abs n.X + abs n.Y + abs n.Z))
        if n.Z <= 0.0 then (1.0 - abs p.YX) * signNonZero p else p

    [<ReflectedDefinition>]
    let decode (e : V2d) =
        let z = 1.0 - abs e.X - abs e.Y

        Vec.normalize (
            if z < 0.0 then
                V3d((1.0 - abs e.YX) * signNonZero e.XY, z)
            else
                V3d(e.XY, z)
        )

module Shader =
    open FShade

    type CustomIdAttribute() =
        inherit SemanticAttribute("CustomId")

    type Fragment = {
        [<Position>]     pos   : V4d
        [<Normal>]       n     : V3d
        [<FragCoord>]    coord : V4d
        [<CustomId; Interpolation(InterpolationMode.Flat)>] id : int
    }

    [<AutoOpen>]
    module private Samplers =

        let diffuseSampler =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

        let diffuseSamplerMS =
            sampler2dMS {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

    // R = id (encoded as float, since we use an RGBA32f texture)
    // G = NDC depth
    // B, A = normal encoded using oct mapping
    let picking (f : Fragment) =
        fragment {
            let id = float <| Fun.FloatFromBits(f.id)
            let d = f.pos.Z / f.pos.W
            let n = OctNormal.encode f.n
            return {| PickData = V4d(id, d, n) |}
        }

    let withoutPicking (f : Fragment) =
        fragment {
            return {| PickData = V4d.Zero |}
        }

    let resolveSingle (samples : int) (f : Fragment) =
        fragment {
            if samples > 1 then
                return diffuseSamplerMS.Read(V2i f.coord.XY, 0)
            else
                return diffuseSampler.Read(V2i f.coord.XY, 0)
        }

    let resolve (samples : int) (f : Fragment) =
        fragment {
            if samples > 1 then
                let mutable result = V4d.Zero

                for i = 0 to samples - 1 do
                    result <- result + diffuseSamplerMS.Read(V2i f.coord.XY, i)

                return result / float samples
            else
                return diffuseSampler.Read(V2i f.coord.XY, 0)
        }

type Scene =
    {
        CubeSg     : ISg
        CubeIds    : int[]
        CubeTrafos : aval<Trafo3d[]>
        ViewTrafo  : aval<Trafo3d>
        ProjTrafo  : aval<Trafo3d>
    }

module Scene =

    // Define how many cubes we want
    let private size = V2i(32, 32)
    let private count = size.X * size.Y
    let private margin = 3.0

    let private trafos (rnd : RandomSystem) (time : aval<DateTime>) =
        let offset = Trafo3d.Translation(V3d(-0.5))

        // Layout cubes in a static XY grid
        let positions =
            Array.init count (fun index ->
                let x = index % size.Y
                let y = index / size.Y

                V3d(float x * margin, float y * margin, 0.0)
            )

        // Rotations are based on time
        let rotations =
            let factors = Array.init count (fun _ -> rnd.UniformDouble() * 2.0 - 1.0)
            let startTime = System.DateTime.Now

            time |> AVal.map (fun t ->
                let dt = (t - startTime).TotalSeconds
                factors |> Array.map (fun f -> dt * f)
            )

        rotations |> AVal.map (
            Array.mapi (fun i r ->
                offset * Trafo3d.RotationZ(r) * Trafo3d.Translation(positions.[i])
            )
        )

    // Generate some random IDs, don't care about collisions in this example
    let private customIds (rnd : RandomSystem) =
        Array.init count (fun _ -> rnd.UniformIntNonZero())

    // Random colors
    let private colors (rnd : RandomSystem) =
        Array.init count (fun _ -> rnd.UniformC3f() |> C4f)

    // Render the cubes using indirect multidraw
    let create (win : IRenderWindow) (rnd : RandomSystem) =
        let ids = customIds rnd
        let trafos = trafos rnd win.Time

        let geometry =
            IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black

        let indirectBuffer =
            Array.init count (fun i ->
                DrawCallInfo(
                    FaceVertexCount = geometry.FaceVertexCount,
                    InstanceCount = 1,
                    FirstInstance = i,
                    BaseVertex = 0
                )
            )
            |> IndirectBuffer.ofArray geometry.IsIndexed

        let makeBufferView (f : 'T -> 'U) (data : aval<'T[]>) =
            let buffer =
                data |> AVal.map (fun data ->
                    let array = data |> Array.map f
                    ArrayBuffer array :> IBuffer
                )

            BufferView(buffer, typeof<'U>)

        let instanceIndex =
            ids |> AVal.constant |> makeBufferView id

        let instanceColor =
            colors rnd |> AVal.constant |> makeBufferView id

        let instanceTrafo =
            trafos |> makeBufferView (Trafo.forward >> M44f)

        let instanceTrafoInv =
            trafos |> makeBufferView (Trafo.backward >> M44f)

        let sg =
            Sg.indirectDraw geometry.Mode ~~indirectBuffer
            |> Sg.indexArray geometry.IndexArray
            |> Sg.vertexArray DefaultSemantic.Positions geometry.IndexedAttributes.[DefaultSemantic.Positions]
            |> Sg.vertexArray DefaultSemantic.Normals geometry.IndexedAttributes.[DefaultSemantic.Normals]
            |> Sg.instanceBuffer DefaultSemantic.CustomId instanceIndex
            |> Sg.instanceBuffer DefaultSemantic.Colors instanceColor
            |> Sg.instanceBuffer DefaultSemantic.InstanceTrafo instanceTrafo
            |> Sg.instanceBuffer DefaultSemantic.InstanceTrafoInv instanceTrafoInv

        let projTrafo =
            win.Sizes |> AVal.map (fun s ->
                Frustum.perspective 60.0 0.1 500.0 (float s.X / float s.Y)
                |> Frustum.projTrafo
            )

        let viewTrafo =
            let center = (V2d size) * margin * 0.5
            let initialView = CameraView.LookAt(V3d(10.0,10.0,10.0), center.XYO, V3d.OOI)
            DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView
            |> AVal.map CameraView.viewTrafo

        { CubeSg = sg
          CubeIds = ids
          CubeTrafos = trafos
          ViewTrafo = viewTrafo
          ProjTrafo = projTrafo }

    // Computes the world position from the given normalized device coordinates.
    let getWorldPosition (ndc : V3d) (scene : Scene) =
        let projTrafo = scene.ProjTrafo.GetValue()
        let viewTrafo = scene.ViewTrafo.GetValue()

        let vp = ndc |> Mat.transformPosProj projTrafo.Backward
        vp |> Mat.transformPos viewTrafo.Backward

type PickingInfo =
    {
        ObjectIndex : int   // Index of the picked object
        Position : V3d      // Position of the picked point in local space
        Normal : V3d        // Normal at the picked point in local space
    }

module PickingInfo =

    // Gets the picking info from the given id, world space position and normal
    let fromWorldSpace (scene : Scene) (id : int) (position : V3d) (normal : V3d) =
        let index = scene.CubeIds |> Array.tryFindIndex ((=) id)

        match index with
        | Some idx ->
            let trafo = scene.CubeTrafos.GetValue().[idx]
            let position = position |> Mat.transformPos trafo.Backward
            let normal = normal |> Mat.transformDir trafo.Forward.Transposed

            Some {
                ObjectIndex = idx
                Position = position
                Normal = normal
            }

        | None ->
            None

[<EntryPoint>]
let main argv =

    Aardvark.Init()

    // uncomment/comment to switch between the backends
    //use app = new VulkanApplication(debug = true)
    use app = new OpenGlApplication(DebugLevel.Normal)
    let runtime = app.Runtime :> IRuntime
    let samples = 8

    use win = app.CreateGameWindow(samples = 1)
    let rnd = RandomSystem(0)

    let scene =
        Scene.create win rnd

    let pickingInfo =
        AVal.init None

    // First we render the scene into an offscreen buffer, which has the
    // default color and depth attachments, and additionally the pick buffer.
    use offscreenSignature =
        runtime.CreateFramebufferSignature([
            DefaultSemantic.Colors, TextureFormat.Rgba8
            DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            DefaultSemantic.PickData, TextureFormat.Rgba32f
        ], samples)

    use offscreenTask =
        let cubes =
            scene.CubeSg
            |> Sg.shader {
                do! DefaultSurfaces.instanceTrafo
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
                do! Shader.picking
            }

        // Visualize the picked point.
        // Note that we do not write into the pick buffer!
        let hit =
            let line =
                adaptive {
                    let! trafos = scene.CubeTrafos

                    match! pickingInfo with
                    | Some i ->
                        let trafo = trafos.[i.ObjectIndex].Forward
                        let p0 = i.Position |> Mat.transformPos trafo
                        let p1 = (i.Position + i.Normal * 0.25) |> Mat.transformPos trafo
                        return Line3d(p0, p1)
                    | None ->
                        return Line3d()
                }

            let sphere =
                Sg.sphere' 16 C4b.Red 0.02
                |> Sg.translation (line |> AVal.map (fun p -> p.P0))
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.vertexColor
                    do! Shader.withoutPicking           // Do not write picking data
                }

            let lines =
                Sg.lines ~~C4b.Red (line |> AVal.map Array.singleton)
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.thickLine
                    do! DefaultSurfaces.vertexColor
                    do! Shader.withoutPicking           // Do not write picking data
                }
                |> Sg.uniform' "LineWidth" 2.0

            Sg.ofList [sphere; lines]
            |> Sg.colorOutput' (Set.singleton DefaultSemantic.Colors)   // Only write to color buffer
            |> Sg.pass RenderPass.pickingInfo                           // Make sure to render after pickable objects
            |> Sg.onOff (pickingInfo |> AVal.map Option.isSome)

        Sg.ofList [cubes; hit]
        |> Sg.uniform' "LightLocation" (V3d(10.0, 5.0, 50.0))
        |> Sg.viewTrafo scene.ViewTrafo
        |> Sg.projTrafo scene.ProjTrafo
        |> Sg.compile runtime offscreenSignature

    let offscreenBuffer =
        let clear =
            clear {
                colors [
                    DefaultSemantic.Colors, C4f.AliceBlue
                    DefaultSemantic.PickData, C4f.Zero
                ]

                depth 1.0
            }

        let output = Set.ofList [ DefaultSemantic.Colors; DefaultSemantic.PickData ]
        offscreenTask |> RenderTask.renderSemanticsWithClear output win.Sizes clear

    // Before we can use the pick buffer for our lookups we first need to resolve it (in case it is multisampled)
    // However, using a normal resolve would average our samples resulting in possibly invalid IDs.
    // Therefore, we use a fragment shader to extract the first sample.
    use resolvePickSignature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, TextureFormat.Rgba32f
        ]

    use resolvePickTask =
        Sg.fullScreenQuad
        |> Sg.diffuseTexture offscreenBuffer.[DefaultSemantic.PickData]
        |> Sg.shader {
            do! Shader.resolveSingle samples
        }
        |> Sg.compile runtime resolvePickSignature

    let pickTexture =
        resolvePickTask |> RenderTask.renderToColor win.Sizes

    pickTexture.Acquire()

    // Finally we install a callback on mouse click events.
    // We download the resolved pick buffer and simply lookup the values in the PixImage.
    win.Mouse.DoubleClick.Values.Add(fun btn ->
        if btn.HasFlag(MouseButtons.Left) then
            let pos = win.Mouse.Position.GetValue().Position
            let tex = pickTexture.GetValue()

            // Check if in bounds
            if Vec.allGreaterOrEqual pos 0 && Vec.allSmaller pos tex.Size.XY then

                // Download pixel
                let pixel =
                    let data = tex.Download(0, 0, Box2i.FromMinAndSize(pos, V2i.II)).AsPixImage<float32>()
                    data.GetMatrix<C4f>().[0, 0]

                // Get id, depth and normal from pixel
                let id = Fun.FloatToBits pixel.R
                let depth = float pixel.G
                let normal = OctNormal.decode <| V2d(pixel.B, pixel.A)

                // Compute world space position
                let wp =
                    let uv = (V2d pos + 0.5) / V2d tex.Size.XY
                    let uv = V2d(uv.X, 1.0 - uv.Y)              // Flip Y
                    let ndc = V3d(uv * 2.0 - 1.0, depth)
                    scene |> Scene.getWorldPosition ndc

                // Update picking info
                match PickingInfo.fromWorldSpace scene id wp normal with
                | Some p ->
                    transact (fun _ ->
                        pickingInfo.Value <- Some p
                    )

                | None ->
                    Log.warn "No instance with index %d" id
    )

    // The color buffer is also resolved and blitted to the framebuffer.
    use task =
        Sg.fullScreenQuad
        |> Sg.diffuseTexture offscreenBuffer.[DefaultSemantic.Colors]
        |> Sg.shader {
            do! Shader.resolve samples
        }
        |> Sg.compile runtime win.FramebufferSignature

    win.RenderTask <- task
    win.Run()

    // Cleanup
    pickTexture.Release()

    0