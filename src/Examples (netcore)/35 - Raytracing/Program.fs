open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open FSharp.Data.Adaptive

module Semantic =
    let DefaultHitGroup = Sym.ofString "DefaultHitGroup"

module Effect =
    open FShade

    [<AutoOpen>]
    module private Shaders =

        type UniformScope with
            member x.OutputBuffer : Image2d<Formats.rgba32f> = uniform?OutputBuffer

        let private mainScene =
            scene {
                accelerationStructure uniform?MainScene
            }

        let missSolidColor =
            miss {
                return V3d(0.55, 0.53, 0.66)
            }

        let chitSolidColor =
            closesthit {
                return V3d(0.7, 0.94, 0.61)
            }

        let rgenMain (input : RayGenerationInput) =
            raygen {
                let pixelCenter = V2d input.work.id.XY + 0.5
                let inUV = pixelCenter / V2d input.work.size.XY
                let d = inUV * 2.0 - 1.0

                let origin = uniform.ViewTrafoInv * V4d.WAxis
                let target = uniform.ProjTrafoInv * V4d(d, 1.0, 1.0)
                let direction = uniform.ViewTrafoInv * V4d(target.XYZ.Normalized, 0.0)

                let color = mainScene.TraceRay<V3d>(origin.XYZ, direction.XYZ)
                uniform.OutputBuffer.[input.work.id.XY] <- V4d(color, 1.0)
            }

    let private hitgroupMain =
        hitgroup {
            closesthit chitSolidColor
        }

    let main() =
        raytracing {
            raygen rgenMain
            miss missSolidColor
            hitgroup (Semantic.DefaultHitGroup, hitgroupMain)
        }


[<EntryPoint>]
let main argv =
    Aardvark.Init()

    use app = new VulkanApplication(debug = Vulkan.DebugConfig.TraceHandles)
    let runtime = app.Runtime :> IRuntime
    let samples = 8

    use win = app.CreateGameWindow(samples = samples)
    //win.RenderAsFastAsPossible <- true

    let cameraView =
        let initialView = CameraView.LookAt(V3d(10.0,10.0,10.0), V3d.Zero, V3d.OOI)
        DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let viewTrafo =
        cameraView |> AVal.map CameraView.viewTrafo

    let projTrafo =
        win.Sizes
        |> AVal.map (fun s ->
            Frustum.perspective 60.0 0.1 150.0 (float s.X / float s.Y)
            |> Frustum.projTrafo
        )

    let traceTexture =
        runtime.CreateTexture2D(TextureFormat.Rgba32f, 1, win.Sizes)

    let geometry =
        let vertices = {
                Buffer = ArrayBuffer([| V3f.XAxis; V3f.YAxis; V3f.ZAxis |])
                Count  = 3u
                Offset = 0UL
                Stride = uint64 sizeof<V3f>
            }

        let data =
            GeometryData.Triangles (vertices, None, Trafo3d.Identity)

        Geometry(data, 1u, GeometryFlags.Opaque)
        |> Array.singleton

    use accelerationStructure =
        runtime.CreateAccelerationStructure(geometry, AccelerationStructureUsage.Static)

    use uniforms =
        Map.ofList [
            "OutputBuffer", traceTexture |> AdaptiveResource.mapNonAdaptive (fun t -> t :> ITexture) :> IAdaptiveValue
            "ViewTrafo", viewTrafo :> IAdaptiveValue
            "ProjTrafo", projTrafo :> IAdaptiveValue
        ]
        |> UniformProvider.ofMap

    let scene =
        let obj =
            TraceObject.create' accelerationStructure
            |> TraceObject.hitGroup' Semantic.DefaultHitGroup

        AMap.single obj 0

    let pipeline =
        {
            Effect            = Effect.main()
            Scenes            = Map.ofList [Sym.ofString "MainScene", scene]
            Uniforms          = uniforms
            MaxRecursionDepth = AVal.constant 2048
        }

    use traceTask = runtime.CompileTraceToTexture(pipeline, traceTexture)

    use fullscreenTask =
        let sg =
            Sg.fullScreenQuad
            |> Sg.diffuseTexture traceTexture
            |> Sg.shader {
                do! DefaultSurfaces.diffuseTexture
            }

        RenderTask.ofList [
            runtime.CompileClear(win.FramebufferSignature, C4f.PaleGreen)
            runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    use renderTask =
        RenderTask.custom (fun (t, rt, fbo, q) ->
            q.Begin()
            traceTask.Run(t, q)
            fullscreenTask.Run(t, rt, fbo, q)
            q.End()
        )

    win.RenderTask <- renderTask
    win.Run()

    0