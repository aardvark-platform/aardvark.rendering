open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open FSharp.Data.Adaptive

module Semantic =
    let DefaultHitGroup = Sym.ofString "DefaultHitGroup"
    let SphereHitGroup = Sym.ofString "SphereHitGroup"
    let SphereHitGroup2 = Sym.ofString "SphereHitGroup2"

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

        let missSolidColor (color : C3d) =
            let color = V3d color

            miss {
                return color
            }

        let chitSolidColor (color : C3d) =
            let color = V3d color

            closesthit {
                return color
            }

        let intersectionSphere (center : V3d) (radius : float) (input : RayIntersectionInput) =
            intersection {
                let origin = input.objectSpace.rayOrigin - center
                let direction = input.objectSpace.rayDirection

                let a = Vec.dot direction direction
                let b = 2.0 * Vec.dot origin direction
                let c = (Vec.dot origin origin) - (radius * radius)

                let discriminant = b * b - 4.0 * a * c
                if discriminant >= 0.0 then
                    let t = (-b - sqrt discriminant) / (2.0 * a)
                    Intersection.Report(t, V2d.Zero) |> ignore
            }

    let private hitgroupMain =
        hitgroup {
            closesthit (chitSolidColor C3d.DodgerBlue)
        }

    let private hitgroupSphere =
        hitgroup {
            closesthit (chitSolidColor C3d.Salmon)
            intersection (intersectionSphere V3d.Zero 0.5)
        }

    let private hitgroupSphere2 =
        hitgroup {
            closesthit (chitSolidColor C3d.PaleGreen)
            intersection (intersectionSphere (V3d(0.0, 2.0, 0.0)) 0.5)
        }

    let main() =
        raytracing {
            raygen rgenMain
            miss (missSolidColor C3d.Lavender)
            hitgroup (Semantic.DefaultHitGroup, hitgroupMain)
            hitgroup (Semantic.SphereHitGroup, hitgroupSphere)
            hitgroup (Semantic.SphereHitGroup2, hitgroupSphere2)
        }


[<EntryPoint>]
let main argv =
    Aardvark.Init()

    use app = new VulkanApplication(debug = true)
    let runtime = app.Runtime :> IRuntime
    let samples = 8

    use win = app.CreateGameWindow(samples = samples)

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

    let sphere =
        TraceGeometry.ofCenterAndRadius GeometryFlags.Opaque V3d.Zero 0.5

    let sphere2 =
        TraceGeometry.ofCenterAndRadius GeometryFlags.Opaque (V3d(0.0, 2.0, 0.0)) 0.5

    let torus =
        IndexedGeometryPrimitives.Torus.solidTorus (Torus3d(V3d.Zero, V3d.YAxis, 1.0, 0.2)) C4b.White 128 64
        |> TraceGeometry.ofIndexedGeometry GeometryFlags.Opaque

    let torus2 =
        IndexedGeometryPrimitives.Torus.solidTorus (Torus3d(V3d.Zero, V3d.YAxis, 0.5, 0.1)) C4b.White 128 64
        |> TraceGeometry.ofIndexedGeometry GeometryFlags.Opaque

    use accelerationStructure =
        runtime.CreateAccelerationStructure(
            TraceGeometry.ofList [sphere; sphere2],
            AccelerationStructureUsage.Static
        )

    use uniforms =
        Map.ofList [
            "OutputBuffer", traceTexture |> AdaptiveResource.mapNonAdaptive (fun t -> t :> ITexture) :> IAdaptiveValue
            "ViewTrafo", viewTrafo :> IAdaptiveValue
            "ProjTrafo", projTrafo :> IAdaptiveValue
        ]
        |> UniformProvider.ofMap

    let scene =
        let obj =
            traceobject {
                geometry accelerationStructure
                hitgroups [Semantic.SphereHitGroup; Semantic.SphereHitGroup2]
            }

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