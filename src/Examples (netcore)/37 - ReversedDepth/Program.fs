open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.ImGui
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System

// This example illustrates how to implement reversed depth for better depth precision (including infinite far plane).
// For an in-depth explanation see: https://developer.nvidia.com/content/depth-precision-visualized
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

        let [<Literal>] Rotation      = "Rotation"
        let [<Literal>] RotationSpeed = "RotationSpeed"
        let [<Literal>] CubeColor     = "CubeColor"
        let [<Literal>] ErrorColor    = "ErrorColor"

    let Rotation      = TypedSymbol<float> Literal.Rotation
    let RotationSpeed = Sym.ofString Literal.RotationSpeed
    let CubeColor     = TypedSymbol<C3b> Literal.CubeColor
    let ErrorColor    = TypedSymbol<C3b> Literal.ErrorColor

module Shader =
    open FShade

    type RotationSpeedAttribute() =
        inherit SemanticAttribute(Semantic.Literal.RotationSpeed)

    type UniformScope with
        member x.Rotation   : float32 = x?Rotation
        member x.CubeColor  : V4f     = x?CubeColor
        member x.ErrorColor : V4f     = x?ErrorColor

    type Vertex = {
        [<Position>]      pos : V4f
        [<Color>]           c : V4f
        [<Normal>]          n : V3f
        [<RotationSpeed>] spd : float32
        [<InstanceId>]     id : int
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

    let cubeColor (v : Vertex) =
        vertex {
            let c = if v.id % 2 = 0 then uniform.CubeColor else uniform.ErrorColor
            return { v with c = c }
        }

[<EntryPoint>]
let main _argv =

    Aardvark.Init()

    let nearPlane = AVal.init 0.01
    let farPlane = AVal.init 2000.0
    let infiniteFarPlane = AVal.init false
    let reversedDepth = AVal.init false
    let rotateCubes = AVal.init false
    let errorColor = AVal.init <| C3b(205, 65, 50)
    let fov = AVal.init <| radians 70.0

    // For best results, use a float-based format.
    // Note: For stenciling, we need Depth32fStencil8 which is a 64bit format.
    let format = TextureFormat.DepthComponent32f

    // Make sure to use [0, 1] NDC depth range
    let depthRange = DepthRange.ZeroToOne
    Vulkan.RuntimeConfig.DepthRange <- depthRange
    GL.RuntimeConfig.DepthRange <- depthRange

    use win =
        window {
            display Display.Mono
            samples 8
            backend Backend.Vulkan
            debug true
            showHelp false
        }

    use gui = win.Control.InitializeImGui()

    gui.Render <- fun () ->
        if ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize) then
            let depthRange =
                let n, f = if depthRange = DepthRange.ZeroToOne then 0, 1 else -1, 1
                if reversedDepth.Value then $"[{f}, {n}]" else $"[{n}, {f}]"

            ImGui.AlignTextToFramePadding()
            ImGui.Text($"Depth range: {depthRange}")

            ImGui.SameLine()
            ImGui.Checkbox("Reversed", reversedDepth)

            let mutable nearPlaneValue = nearPlane.Value
            if ImGui.InputDouble("Near plane", &nearPlaneValue, 0.001, "%.3f") then
                nearPlane.Value <- nearPlaneValue |> clamp 0.001 0.999

            ImGui.BeginDisabled infiniteFarPlane.Value
            let mutable farPlaneValue = farPlane.Value
            if ImGui.InputDouble("Far plane", &farPlaneValue, 10.0, "%.2f") then
                farPlane.Value <- farPlaneValue |> clamp 10.0 9999.0
            ImGui.EndDisabled()

            ImGui.SameLine()
            ImGui.Checkbox("Infinite", infiniteFarPlane)

            let mutable fovDegrees = int <| degrees fov.Value
            if ImGui.SliderInt("Field of view", &fovDegrees, 5, 120, "%d\u00B0") then
                fov.Value <- radians <| float fovDegrees

            ImGui.Checkbox("Animate cubes", rotateCubes)
            ImGui.ColorEdit3("Error color", errorColor, ImGuiColorEditFlags.NoInputs)
        ImGui.End()

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
            let! infiniteFar = infiniteFarPlane
            let! fov = fov

            let far = if infiniteFar then infinity else far
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
        let totalCount = count.X * count.Y
        let margin = 2.5
        let offset = (V2d (count - 1) * margin) * 0.5

        let geometry =
            IndexedGeometryPrimitives.Box.solidBox (Box3d(V3d(-0.5), V3d(0.5))) C4b.Black

        let drawCall =
            DrawCallInfo(
                FaceVertexCount = geometry.FaceVertexCount,
                InstanceCount   = 2 * totalCount,
                FirstInstance   = 0,
                BaseVertex      = 0
            )

        let rotationSpeeds =
            Array.init totalCount (fun _ ->
                let spd = float32 (1 + rnd.UniformInt 3) * (if rnd.UniformInt 2 = 0 then -1.0f else 1.0f)
                [| spd; spd |]
            )
            |> Array.concat

        let trafos =
            Array.init totalCount (fun i ->
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

        Sg.render geometry.Mode drawCall
        |> Sg.indexArray geometry.IndexArray
        |> Sg.vertexArray DefaultSemantic.Positions geometry.IndexedAttributes.[DefaultSemantic.Positions]
        |> Sg.vertexArray DefaultSemantic.Normals geometry.IndexedAttributes.[DefaultSemantic.Normals]
        |> Sg.instanceAttribute' DefaultSemantic.InstanceTrafo instanceTrafo
        |> Sg.instanceAttribute' DefaultSemantic.InstanceTrafoInv instanceTrafoInv
        |> Sg.instanceArray Semantic.RotationSpeed rotationSpeeds
        |> Sg.shader {
            do! Shader.rotate
            do! Shader.cubeColor
            do! DefaultSurfaces.instanceTrafo
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.uniform Semantic.Rotation rotation
        |> Sg.uniform' Semantic.CubeColor (C3b(6, 146, 188))
        |> Sg.uniform Semantic.ErrorColor errorColor
        |> Sg.viewTrafo viewTrafo
        |> Sg.projTrafo projTrafo
        |> Sg.depthTest depthTest
        |> Sg.cullMode' CullMode.Back
        |> Sg.compile win.Runtime signature

    let output =
        task |> RenderTask.renderToColorWithAdaptiveClear win.Sizes clearValues

    let scene =
        let resolved =
            Sg.fullScreenQuad
            |> Sg.diffuseTexture output
            |> Sg.shader {
                do! Shader.resolve signature.Samples
            }

        RenderCommand.Ordered [
            RenderCommand.Render resolved
            RenderCommand.Render gui
        ]
        |> Sg.execute

    win.Scene <- scene
    win.Run()

    0