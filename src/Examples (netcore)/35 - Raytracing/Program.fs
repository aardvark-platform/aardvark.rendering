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
                let color = mainScene.TraceRay<V3d>(V3d(V2d input.work.id.XY / 512.0, 0.0), V3d.ZAxis)
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
    win.RenderAsFastAsPossible <- true

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