open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Text
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open System

// This example illustrates how to implement reversed depth for better depth precision (including infinite far plane).
// For an in-depth explanation see: https://developer.nvidia.com/content/depth-precision-visualized
//
// Controls:
//   Enter - toggle reversed depth
//   Space - toggle infinite far plane
//   +/-   - adjust near plane
//   R     - toggle rotation
//
// To use reverse depth to its fullest extent, you need to do this:
//
// 1) Set NDC depth range to [0, 1] (via RuntimeConfig.DepthRange).
//    For the default [-1, 1] range, reverse depth also results in better precision but
//    the improvement is not as drastic as with [0, 1].
//    Note: For the GL backend OpenGL >= 4.5 is required to use the [0, 1] range.
//
// 2) Use a floating point depth format such as TextureFormat.DepthComponent32f.
//    If stenciling is required use TextureFormat.Depth32fStencil8. Note that this
//    format is actually a 64bit format.
//
// 3) Use DepthTest.GreaterOrEqual instead of DepthTest.LessOrEqual.
//
// 4) Clear the depth buffer to 0.0 instead of 1.0.
//
// 5) Adjust the projection matrix by swapping the near and far plane.
//    Alternatively, use the reversed Trafo3d projection variants, which also support infinite far planes.

module Semantic =

    module Literal =

        [<Literal>]
        let Rotation = "Rotation"

        [<Literal>]
        let RotationSpeed = "RotationSpeed"

    let Rotation      = TypedSymbol<float> Literal.Rotation
    let RotationSpeed = Sym.ofString Literal.RotationSpeed

module Shader =
    open FShade

    type RotationSpeedAttribute() =
        inherit SemanticAttribute(Semantic.Literal.RotationSpeed)

    type UniformScope with
        member x.Rotation : float32 = x?Rotation

    type Vertex = {
        [<Position>]      pos : V4f
        [<Normal>]          n : V3f
        [<RotationSpeed>] spd : float32
    }

    type Fragment = {
        [<FragCoord>] coord : V4f
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

    let resolve (samples : int) (f : Fragment) =
        fragment {
            if samples > 1 then
                let mutable result = V4f.Zero

                for i = 0 to samples - 1 do
                    result <- result + diffuseSamplerMS.Read(V2i f.coord.XY, i)

                return result / float32 samples
            else
                return diffuseSampler.Read(V2i f.coord.XY, 0)
        }

    let rotate (v : Vertex) =
        vertex {
            let angle = uniform.Rotation * v.spd * ConstantF.PiTimesTwo
            let c, s = cos angle, sin angle

            let prot =
                V2f(
                    v.pos.X * c - v.pos.Y * s,
                    v.pos.X * s + v.pos.Y * c
                )

            let nrot =
                V2f(
                    v.n.X * c - v.n.Y * s,
                    v.n.X * s + v.n.Y * c
                )

            return { v with pos = V4f(prot, v.pos.ZW)
                            n   = V3f(nrot, v.n.Z) }
        }

[<EntryPoint>]
let main argv =

    Aardvark.Init()

    let useVulkan = false

    let finiteFarPlane = 2000.0
    let nearPlane = AVal.init 0.01
    let farPlane = AVal.init finiteFarPlane
    let reversedDepth = AVal.init false
    let rotateCubes = AVal.init false
    let fov = radians 70.0

    // For best results, use a float-based format.
    // Note: For stenciling, we need Depth32fStencil8 which is a 64bit format.
    let format = TextureFormat.DepthComponent32f

    // Make sure to use [0, 1] NDC depth range
    let depthRange = DepthRange.ZeroToOne

    use app : Application =
        if useVulkan then
            Vulkan.RuntimeConfig.DepthRange <- depthRange
            new VulkanApplication(debug = DebugLevel.Minimal)
        else
            GL.RuntimeConfig.DepthRange <- depthRange
            new OpenGlApplication(debug = DebugLevel.Minimal)

    use win = app.CreateGameWindow()

    // Controls
    win.Keyboard.Down.Values.Add(function
        | Keys.Enter ->
            transact (fun _ ->
                reversedDepth.Value <- not reversedDepth.Value
            )

        | Keys.Space ->
            transact (fun _ ->
                farPlane.Value <-
                    if isFinite farPlane.Value then
                        infinity
                    else
                        finiteFarPlane
            )

        | Keys.R ->
            transact (fun _ ->
                rotateCubes.Value <- not rotateCubes.Value
            )

        | _ -> ()
    )

    win.Keyboard.DownWithRepeats.Values.Add(function
        | Keys.OemPlus ->
            transact (fun _ -> nearPlane.Value <- nearPlane.Value + 0.001)

        | Keys.OemMinus ->
            transact (fun _ -> nearPlane.Value <- max 0.001 (nearPlane.Value - 0.001))

        | _ -> ()
    )

    // Set depth test to >= for reverse depth
    let depthTest =
        reversedDepth |> AVal.map (fun reversed ->
            if reversed then
                DepthTest.GreaterOrEqual
            else
                DepthTest.LessOrEqual
        )

    // Clear depth to 0.0 for reverse depth
    let clearValues =
        reversedDepth |> AVal.map (fun reversed ->
            ClearValues.ofColor C4b.SlateGray
            |> ClearValues.depth (if reversed then 0.0 else 1.0)
        )

    let cameraView =
        let initialView = CameraView.LookAt(V3d(40), V3d(0, 0, 10), V3d.OOI)
        DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let viewTrafo =
        cameraView |> AVal.map CameraView.viewTrafo

    let projTrafo =
        adaptive {
            let! size = win.Sizes
            let! reversed = reversedDepth
            let! near = nearPlane
            let! far = farPlane

            let aspect = float size.X / float size.Y

            if depthRange = DepthRange.ZeroToOne then
                if reversed then
                    return Trafo3d.PerspectiveProjectionReversedRH(fov, aspect, near, far)
                else
                    return Trafo3d.PerspectiveProjectionRH(fov, aspect, near, far)
            else
                if reversed then
                    return Trafo3d.PerspectiveProjectionReversedGL(fov, aspect, near, far)
                else
                    return Trafo3d.PerspectiveProjectionGL(fov, aspect, near, far)
        }

    use signature =
        win.Runtime.CreateFramebufferSignature([
            DefaultSemantic.Colors, TextureFormat.Rgba8
            DefaultSemantic.DepthStencil, format
        ], samples = 8)

    use task =
        let rnd = RandomSystem(0)
        let count = V2i(400, 400)
        let margin = 2.5
        let offset = (V2d (count - 1) * margin) * 0.5

        let geometry =
            IndexedGeometryPrimitives.Box.solidBox (Box3d(V3d(-0.5), V3d(0.5))) C4b.Black

        let indirectBuffer =
            DrawCallInfo(
                FaceVertexCount = geometry.FaceVertexCount,
                InstanceCount = 2 * count.X * count.Y,
                FirstInstance = 0,
                BaseVertex = 0
            )
            |> Array.singleton
            |> IndirectBuffer.ofArray

        let colors =
            [| C4b(6, 146, 188); C4b(205, 65, 50) |]
            |> List.replicate (count.X * count.Y)
            |> Array.concat

        let rotationSpeeds =
            List.init (count.X * count.Y) (fun _ ->
                let spd = float32 (1 + rnd.UniformInt 3) * (if rnd.UniformInt 2 = 0 then -1.0f else 1.0f)
                [| spd; spd |]
            )
            |> Array.concat

        let trafos =
            List.init (count.X * count.Y) (fun i ->
                let x = i % count.X
                let y = i / count.X

                let rotation = rnd.UniformDouble() * Constant.PiTimesTwo
                let position = V3d(x, y, 0) * margin - offset.XYO
                let outer = Trafo3d.RotationZ rotation * Trafo3d.Translation position
                let inner = Trafo3d.Scale 0.9 * outer

                [| outer; inner |]
            )
            |> Array.concat

        let instanceTrafo =
            trafos |> Array.map (Trafo.forward >> M44f)

        let instanceTrafoInv =
            trafos |> Array.map (Trafo.backward >> M44f)

        let rotation =
            let start = DateTime.Now

            let rotation =
                win.Time |> AVal.map (fun t ->
                    let dt = t - start
                    (dt.TotalSeconds * 0.25) % 1.0
                )

            adaptive {
                let! enabled = rotateCubes
                if enabled then
                    return! rotation
                else
                    return 0.0
            }

        let scene =
            Sg.indirectDraw geometry.Mode ~~indirectBuffer
            |> Sg.indexArray geometry.IndexArray
            |> Sg.vertexArray DefaultSemantic.Positions geometry.IndexedAttributes.[DefaultSemantic.Positions]
            |> Sg.vertexArray DefaultSemantic.Normals geometry.IndexedAttributes.[DefaultSemantic.Normals]
            |> Sg.instanceAttribute' DefaultSemantic.Colors colors
            |> Sg.instanceAttribute' DefaultSemantic.InstanceTrafo instanceTrafo
            |> Sg.instanceAttribute' DefaultSemantic.InstanceTrafoInv instanceTrafoInv
            |> Sg.instanceArray Semantic.RotationSpeed rotationSpeeds
            |> Sg.shader {
                do! Shader.rotate
                do! DefaultSurfaces.instanceTrafo
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }
            |> Sg.uniform Semantic.Rotation rotation
            |> Sg.viewTrafo viewTrafo
            |> Sg.projTrafo projTrafo
            |> Sg.depthTest depthTest
            |> Sg.cullMode' CullMode.Back

        let overlay =
            let str =
                adaptive {
                    let! reversed = reversedDepth
                    let! near = nearPlane
                    let! far = farPlane

                    let range =
                        if depthRange = DepthRange.ZeroToOne then "[0, 1]"
                        else "[-1, 1]"

                    return sprintf "Depth range: %s %s\n" range (if reversed then "reversed" else "") +
                           sprintf "Near plane: %.3f\n" near +
                           sprintf "Far plane: %g" far
                }

            let trafo =
                win.Sizes |> AVal.map (fun size ->
                    let border = V2d(25.0, 15.0) / V2d size
                    let pixels = 50.0 / float size.Y
                    Trafo3d.Scale(pixels) *
                    Trafo3d.Scale(float size.Y / float size.X, 1.0, 1.0) *
                    Trafo3d.Translation(-1.0 + border.X, 1.0 - border.Y - pixels, 0.0)
                )

            Sg.text DefaultFonts.Hack.Regular C4b.White str
            |> Sg.trafo trafo

        let overlayFormat =
            let str = sprintf "Format: %A" format

            let trafo =
                win.Sizes |> AVal.map (fun size ->
                    let border = V2d(25.0, 20.0) / V2d size
                    let pixels = 50.0 / float size.Y
                    Trafo3d.Scale(pixels) *
                    Trafo3d.Scale(float size.Y / float size.X, 1.0, 1.0) *
                    Trafo3d.Translation(-1.0 + border.X, -1.0 + border.Y, 0.0)
                )

            Sg.text DefaultFonts.Hack.Regular C4b.White (AVal.constant str)
            |> Sg.trafo trafo

        Sg.ofList [scene; overlay; overlayFormat]
        |> Sg.compile win.Runtime signature

    let output =
        task |> RenderTask.renderToColorWithAdaptiveClear win.Sizes clearValues

    use fullscreenTask =
        Sg.fullScreenQuad
        |> Sg.diffuseTexture output
        |> Sg.shader {
            do! Shader.resolve signature.Samples
        }
        |> Sg.compile win.Runtime win.FramebufferSignature

    win.RenderTask <- fullscreenTask
    win.Run()

    0