namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto

module Culling =

    module private Shader =
        open FShade

        module Raytracing =

            type UniformScope with
                member x.OutputBuffer : Image2d<Formats.rgba8> = uniform?OutputBuffer

            let private mainScene =
                scene { accelerationStructure uniform?MainScene }

            let raygenMain (flags : RayFlags) (input : RayGenerationInput) =
                raygen {
                    let uv = (V2f input.work.id + 0.5f) / V2f input.work.size.XY
                    let ndc = uv * 2.0f - 1.0f

                    let origin = V4f.WAxis
                    let target = V4f(ndc, 1.0f, 1.0f)
                    let direction = V4f(target.XYZ.Normalized, 0.0f)
                    let result = mainScene.TraceRay<V3f>(origin.XYZ, direction.XYZ, flags = flags)

                    uniform.OutputBuffer.[input.work.id.XY] <- V4f(result, 1.0f)
                }

            let missMain (input : RayMissInput) =
                miss {
                    return V3f(0.5f, 0.5f, 0.5f)
                }

            let hitRed (input : RayHitInput<V3f>) =
                closestHit {
                    return V3f(1, 0, 0)
                }

            let hitFrontFacingRed (input : RayHitInput<V3f>) =
                closestHit {
                    return
                        match input.hit.kind with
                        | RayHitKind.FrontFacingTriangle -> V3f(1, 0, 0)
                        | RayHitKind.BackFacingTriangle -> V3f(0, 1, 0)
                        | _ -> V3f.One
                }

        type Vertex =
            { [<FrontFacing>] isFrontFace : bool }

        let frontFacing (front : V3f) (back : V3f) (v : Vertex) =
            fragment {
                return if v.isFrontFace then front else back
            }

    module private Effect =
        open FShade

        let constantRed = toEffect <| DefaultSurfaces.constantColor C4f.Red
        let frontFacingRed = toEffect <| Shader.frontFacing V3f.IOO V3f.OOO

        let raytracing (flags : RayFlags) =
            let hitgroupMain = hitgroup { closestHit Shader.Raytracing.hitRed }
            let hitgroupFrontFacing = hitgroup { closestHit Shader.Raytracing.hitFrontFacingRed }

            raytracingEffect {
                raygen (Shader.Raytracing.raygenMain flags)
                miss Shader.Raytracing.missMain
                hitgroup "Main" hitgroupMain
                hitgroup "FrontFacing" hitgroupFrontFacing
            }

    module FullscreenQuad =

        let private makeSg (mode : IndexedGeometryMode) (positions : V3f[]) =
            DrawCallInfo(faceVertexCount = positions.Length)
            |> Sg.render mode
            |> Sg.vertexAttribute' DefaultSemantic.Positions positions

        let alternating =
            [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
            |> makeSg IndexedGeometryMode.TriangleList

        let ccw =
            [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
            |> makeSg IndexedGeometryMode.TriangleStrip

        let cw =
            [| V3f(-1,-1,0); V3f(-1,1,0); V3f(1,-1,0); V3f(1,1,0) |]
            |> makeSg IndexedGeometryMode.TriangleStrip

        let private makeTraceGeometry (vertices: V3f[]) (indices: int[]) =
            let mesh = TriangleMesh(vertices, indices)
            TraceGeometry.Triangles [| mesh |]

        let alternatingTrace =
            makeTraceGeometry
                [| V3f(-1,-1,1); V3f(1,-1,1); V3f(-1,1,1); V3f(1,1,1) |]
                [| 0; 1; 2; 1; 2; 3|]

        let ccwTrace =
            makeTraceGeometry
                [| V3f(-1,-1,1); V3f(1,-1,1); V3f(-1,1,1); V3f(1,1,1) |]
                [| 0; 1; 2; 2; 1; 3|]

        let cwTrace =
            makeTraceGeometry
                [| V3f(-1,-1,1); V3f(-1,1,1); V3f(1,-1,1); V3f(1,1,1) |]
                [| 0; 1; 2; 2; 1; 3|]

    module Cases =
        open FShade

        let private renderToPix (f : PixImage<uint8> -> unit) (sg : ISg) (runtime : IRuntime) =
            let size = AVal.constant <| V2i(256)

            use signature =
                runtime.CreateFramebufferSignature([
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ])

            use task =
                sg |> Sg.compile runtime signature

            let output = task |> RenderTask.renderToColor size
            output.Acquire()

            try
                f <| output.GetValue().Download().AsPixImage<uint8>()
            finally
                output.Release()

        let private renderToRed : ISg -> IRuntime -> unit =
            Sg.effect [Effect.constantRed]
            >> renderToPix (
                PixImage.isColor [| 255uy; 0uy; 0uy |]
            )

        let defaultNoCulling : IRuntime -> unit =
            FullscreenQuad.alternating
            |> renderToRed

        let defaultFrontFaceCCW : IRuntime -> unit =
            FullscreenQuad.ccw
            |> Sg.cullMode' CullMode.Back
            |> renderToRed

        let private renderToRedWithCulling (cull : CullMode) (frontFace : WindingOrder) =
            Sg.cullMode' cull
            >> Sg.frontFacing' frontFace
            >> renderToRed

        let backFaceCullingFrontFaceCCW : IRuntime -> unit =
            FullscreenQuad.ccw
            |> renderToRedWithCulling CullMode.Back WindingOrder.CounterClockwise

        let backFaceCullingFrontFaceCW : IRuntime -> unit =
            FullscreenQuad.cw
            |> renderToRedWithCulling CullMode.Back WindingOrder.Clockwise

        let frontFaceCullingFrontFaceCCW : IRuntime -> unit =
            FullscreenQuad.cw
            |> renderToRedWithCulling CullMode.Front WindingOrder.CounterClockwise

        let frontFaceCullingFrontFaceCW : IRuntime -> unit =
            FullscreenQuad.ccw
            |> renderToRedWithCulling CullMode.Front WindingOrder.Clockwise

        let shaderFrontFacing : IRuntime -> unit =
            FullscreenQuad.ccw
            |> Sg.cullMode' CullMode.Back
            |> Sg.frontFacing' WindingOrder.CounterClockwise
            |> Sg.effect [Effect.frontFacingRed]
            |> renderToPix (
                PixImage.isColor [| 255uy; 0uy; 0uy |]
            )

        let private traceToPix (expected : uint8[]) (flags : RayFlags)
                               (hitGroup : string) (frontFace : WindingOrder option)
                               (geometry : TraceGeometry) (runtime : IRuntime) =
            if not runtime.SupportsRaytracing then
                Tests.skiptest "Runtime does not support raytracing"

            use accel = runtime.CreateAccelerationStructure(geometry)

            let instance =
                TraceInstance.ofAccelerationStructure accel
                |> TraceInstance.hitGroup hitGroup
                |> match frontFace with Some ff -> TraceInstance.frontFace ff | _ -> id

            let pipeline =
                let scene = RaytracingScene.ofList [instance]

                { Effect            = Effect.raytracing flags
                  Scenes            = Map.ofList [Sym.ofString "MainScene", scene]
                  Uniforms          = UniformProvider.Empty
                  MaxRecursionDepth = AVal.constant 2048 }

            let output =
                runtime.TraceTo2D(V2i(256), TextureFormat.Rgba8, "OutputBuffer", pipeline)

            output.Acquire()

            try
                let result = output.GetValue().Download().AsPixImage<uint8>()
                result |> PixImage.isColor expected
            finally
                output.Release()

        let private traceToRed : RayFlags -> string -> WindingOrder option -> TraceGeometry -> IRuntime -> unit =
            traceToPix [| 255uy; 0uy; 0uy |]

        let raytracingDefaultNoCulling : IRuntime -> unit =
            FullscreenQuad.alternatingTrace
            |> traceToRed RayFlags.CullBackFacingTriangles "Main" None

        let raytracingBackFaceCullingFrontFaceCCW : IRuntime -> unit =
            FullscreenQuad.ccwTrace
            |> traceToRed RayFlags.CullBackFacingTriangles "Main" (Some WindingOrder.CounterClockwise)

        let raytracingBackFaceCullingFrontFaceCW : IRuntime -> unit =
            FullscreenQuad.cwTrace
            |> traceToRed RayFlags.CullBackFacingTriangles "Main" (Some WindingOrder.Clockwise)

        let raytracingFrontFaceCullingFrontFaceCCW : IRuntime -> unit =
            FullscreenQuad.cwTrace
            |> traceToRed RayFlags.CullFrontFacingTriangles "Main" (Some WindingOrder.CounterClockwise)

        let raytracingFrontFaceCullingFrontFaceCW : IRuntime -> unit =
            FullscreenQuad.ccwTrace
            |> traceToRed RayFlags.CullFrontFacingTriangles "Main" (Some WindingOrder.Clockwise)

        let raytracingShaderFrontFacing : IRuntime -> unit =
            FullscreenQuad.ccwTrace
            |> traceToRed RayFlags.None "FrontFacing" None

        let raytracingShaderBackFacing : IRuntime -> unit =
            FullscreenQuad.cwTrace
            |> traceToPix [| 0uy; 255uy; 0uy |] RayFlags.None "FrontFacing" None

    let tests (backend : Backend) =
        [
            "Default no culling",                     Cases.defaultNoCulling
            "Default front face CCW",                 Cases.defaultFrontFaceCCW

            "Back face culling with front face CCW",  Cases.backFaceCullingFrontFaceCCW
            "Back face culling with front face CW",   Cases.backFaceCullingFrontFaceCW

            "Front face culling with front face CCW", Cases.frontFaceCullingFrontFaceCCW
            "Front face culling with front face CW",  Cases.frontFaceCullingFrontFaceCW

            "Shader front facing",                    Cases.shaderFrontFacing

            if backend = Backend.Vulkan then
                "Raytracing default no culling",                     Cases.raytracingDefaultNoCulling

                "Raytracing back face culling with front face CCW",  Cases.raytracingBackFaceCullingFrontFaceCCW
                "Raytracing back face culling with front face CW",   Cases.raytracingBackFaceCullingFrontFaceCW

                "Raytracing front face culling with front face CCW", Cases.raytracingFrontFaceCullingFrontFaceCCW
                "Raytracing front face culling with front face CW",  Cases.raytracingFrontFaceCullingFrontFaceCW

                "Raytracing shader front facing",                    Cases.raytracingShaderFrontFacing
                "Raytracing shader back facing",                     Cases.raytracingShaderBackFacing
        ]
        |> prepareCases backend "Culling"