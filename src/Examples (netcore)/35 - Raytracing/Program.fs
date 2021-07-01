open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Application
open Aardvark.Application.Slim
open FSharp.Data.Adaptive

module Semantic =
    let MissShadow = Sym.ofString "MissShadow"
    let HitGroupModel = Sym.ofString "HitGroupModel"
    let HitGroupFloor = Sym.ofString "HitGroupFloor"
    let HitGroupSphere = Sym.ofString "HitGroupSphere"

module Effect =
    open FShade

    [<AutoOpen>]
    module private Shaders =

        type UniformScope with
            member x.OutputBuffer : Image2d<Formats.rgba32f> = uniform?OutputBuffer

            // Model
            member x.ModelNormals : V4d[] = uniform?StorageBuffer?ModelNormals

            // Floor
            member x.FloorIndices : int[] = uniform?StorageBuffer?FloorIndices
            member x.FloorTextureCoords : V2d[] = uniform?StorageBuffer?FloorTextureCoords

            // Sphere
            member x.SphereCenters : V3d[] = uniform?StorageBuffer?SphereCenters

        let private mainScene =
            scene {
                accelerationStructure uniform?MainScene
            }

        let private textureFloor =
            sampler2d {
                texture uniform?TextureFloor
                filter Filter.MinMagMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let rgenMain (input : RayGenerationInput) =
            raygen {
                let pixelCenter = V2d input.work.id.XY + 0.5
                let inUV = pixelCenter / V2d input.work.size.XY
                let d = inUV * 2.0 - 1.0

                let origin = uniform.ViewTrafoInv * V4d.WAxis
                let target = uniform.ProjTrafoInv * V4d(d, 1.0, 1.0)
                let direction = uniform.ViewTrafoInv * V4d(target.XYZ.Normalized, 0.0)

                let color = mainScene.TraceRay<V3d>(origin.XYZ, direction.XYZ, flags = RayFlags.CullBackFacingTriangles)
                uniform.OutputBuffer.[input.work.id.XY] <- V4d(color, 1.0)
            }

        let missSolidColor (color : C3d) =
            let color = V3d color

            miss {
                return color
            }

        let missShadow =
            miss { return false }

        [<ReflectedDefinition>]
        let fromBarycentric (v0 : V3d) (v1 : V3d) (v2 : V3d) (coords : V2d) =
            let barycentricCoords = V3d(1.0 - coords.X - coords.Y, coords.X, coords.Y)
            v0 * barycentricCoords.X + v1 * barycentricCoords.Y + v2 * barycentricCoords.Z

        [<ReflectedDefinition>]
        let fromBarycentric2d (v0 : V2d) (v1 : V2d) (v2 : V2d) (coords : V2d) =
            let barycentricCoords = V3d(1.0 - coords.X - coords.Y, coords.X, coords.Y)
            v0 * barycentricCoords.X + v1 * barycentricCoords.Y + v2 * barycentricCoords.Z

        [<ReflectedDefinition>]
        let computeLighting (color : V3d) (light : V3d) (normal : V3d) (position : V3d) =
            let L = light - position |> Vec.normalize
            let NdotL = Vec.dot normal L |> max 0.0

            let ambient = 0.3
            let diffuse = ambient + NdotL

            color * diffuse

        let chitModel (color : C3d) (input : RayHitInput) =
            let color = V3d color

            closesthit {
                let id = input.geometry.primitiveId
                let indices = V3i(3 * id, 3 * id + 1, 3 * id + 2)

                let normal =
                    let n0 = uniform.ModelNormals.[indices.X].XYZ
                    let n1 = uniform.ModelNormals.[indices.Y].XYZ
                    let n2 = uniform.ModelNormals.[indices.Z].XYZ
                    input.hit.attribute |> fromBarycentric n0 n1 n2 |> Vec.normalize

                let position =
                    input.ray.origin + input.hit.t * input.ray.direction

                let shadowed =
                    let direction = Vec.normalize (uniform.LightLocation - position)
                    let flags = RayFlags.SkipClosestHitShader ||| RayFlags.TerminateOnFirstHit ||| RayFlags.Opaque ||| RayFlags.CullFrontFacingTriangles
                    mainScene.TraceRay<bool>(position, direction, payload = true, miss = "MissShadow", flags = flags)

                let diffuse = computeLighting color uniform.LightLocation normal position
                if shadowed then
                    return diffuse * 0.3
                else
                    return diffuse
            }

        let chitFloor (input : RayHitInput) =
            closesthit {
                let id = input.geometry.primitiveId
                let indices = V3i(uniform.FloorIndices.[3 * id], uniform.FloorIndices.[3 * id + 1], uniform.FloorIndices.[3 * id + 2])

                let position =
                    input.ray.origin + input.hit.t * input.ray.direction

                let texCoords =
                    let uv0 = uniform.FloorTextureCoords.[indices.X]
                    let uv1 = uniform.FloorTextureCoords.[indices.Y]
                    let uv2 = uniform.FloorTextureCoords.[indices.Z]
                    input.hit.attribute |> fromBarycentric2d uv0 uv1 uv2

                let shadowed =
                    let direction = Vec.normalize (uniform.LightLocation - position)
                    let flags = RayFlags.SkipClosestHitShader ||| RayFlags.TerminateOnFirstHit ||| RayFlags.Opaque ||| RayFlags.CullFrontFacingTriangles
                    mainScene.TraceRay<bool>(position, direction, payload = true, miss = "MissShadow", flags = flags)

                let color = textureFloor.Sample(texCoords).XYZ
                let diffuse = computeLighting color uniform.LightLocation V3d.ZAxis position

                if shadowed then
                    return diffuse * 0.3
                else
                    return diffuse
            }

        let chitSphere (input : RayHitInput) =
            closesthit {
                let position = input.ray.origin + input.hit.t * input.ray.direction
                let normal = Vec.normalize (position - uniform.SphereCenters.[input.geometry.instanceCustomIndex])

                let shadowed =
                    let direction = Vec.normalize (uniform.LightLocation - position)
                    let flags = RayFlags.SkipClosestHitShader ||| RayFlags.TerminateOnFirstHit ||| RayFlags.Opaque ||| RayFlags.CullFrontFacingTriangles
                    mainScene.TraceRay<bool>(position, direction, payload = true, miss = "MissShadow", flags = flags, minT = 0.1)

                let color = V3d.One
                let diffuse = computeLighting color uniform.LightLocation normal position

                if shadowed then
                    return diffuse * 0.3
                else
                    return diffuse
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
                    Intersection.Report(t) |> ignore
            }

    let private hitgroupSphere =
        hitgroup {
            closesthit chitSphere
            intersection (intersectionSphere V3d.Zero 0.5)
        }

    let private hitgroupModel =
        hitgroup {
            closesthit (chitModel C3d.BurlyWood)
        }

    let private hitgroupFloor =
        hitgroup {
            closesthit chitFloor
        }


    let main() =
        raytracing {
            raygen rgenMain
            miss (missSolidColor C3d.Lavender)
            miss (Semantic.MissShadow, missShadow)
            hitgroup (Semantic.HitGroupModel, hitgroupModel)
            hitgroup (Semantic.HitGroupFloor, hitgroupFloor)
            hitgroup (Semantic.HitGroupSphere, hitgroupSphere)
        }


[<EntryPoint>]
let main argv =
    Aardvark.Init()

    use app = new VulkanApplication(debug = true)
    let runtime = app.Runtime :> IRuntime
    let samples = 8

    use win = app.CreateGameWindow(samples = samples)

    let cameraView =
        let initialView = CameraView.LookAt(V3d.One * 10.0, V3d.Zero, V3d.OOI)
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

    let model, modelNormals =
        let scene = Loader.Assimp.load (Path.combine [__SOURCE_DIRECTORY__; "..";"..";"..";"data";"lucy.obj"])
        let geometry = scene.meshes.[0].geometry

        let normals =
            geometry.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]
            |> Array.map V4f

        geometry |> TraceGeometry.ofIndexedGeometry GeometryFlags.Opaque Trafo3d.Identity,
        AVal.constant <| ArrayBuffer(normals)

    let floor, floorIndices, floorTextureCoords =
        let positions = [| V3f(-0.5f, -0.5f, 0.0f); V3f(-0.5f, 0.5f, 0.0f); V3f(0.5f, -0.5f, 0.0f); V3f(0.5f, 0.5f, 0.0f); |]
        let indices = [| 0; 1; 2; 3 |]
        let uv = positions |> Array.map (fun p -> p.XY + 0.5f)

        let attributes =
            SymDict.ofList [
                DefaultSemantic.Positions, positions :> System.Array
            ]

        let trafo = Trafo3d.Scale(48.0)
        let geometry =
            IndexedGeometry(IndexedGeometryMode.TriangleStrip, indices, attributes, SymDict.empty)
            |> IndexedGeometry.toNonStripped

        geometry |> TraceGeometry.ofIndexedGeometry GeometryFlags.Opaque trafo,
        AVal.constant <| ArrayBuffer(geometry.IndexArray),
        AVal.constant <| ArrayBuffer(uv)


    let sphere =
        TraceGeometry.ofCenterAndRadius GeometryFlags.Opaque V3d.Zero 0.5

    let sphereCenter =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            V3d(Rot2d(t * -Constant.PiHalf) * V2d(-6.0, 0.0), 2.0)
        )

    let sphereCenter2 =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            V3d(Rot2d(t * Constant.PiHalf) * V2d(-12.0, 0.0), 3.5)
        )

    let sphereCenters =
        (sphereCenter, sphereCenter2) ||> AVal.map2 (fun c1 c2 ->
            ArrayBuffer([| V4f c1; V4f c2|])
        )

    use accelerationStructureModel =
        runtime.CreateAccelerationStructure(
            model, AccelerationStructureUsage.Static
        )

    use accelerationStructureFloor =
        runtime.CreateAccelerationStructure(
            floor, AccelerationStructureUsage.Static
        )

    use accelerationStructureSphere =
        runtime.CreateAccelerationStructure(
            sphere, AccelerationStructureUsage.Static
        )

    let lightLocation =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            V3d(Rot2d(t * Constant.PiQuarter) * V2d(8.0, 0.0), 16.0)
        )

    use uniforms =
        Map.ofList [
            "OutputBuffer", traceTexture |> AdaptiveResource.mapNonAdaptive (fun t -> t :> ITexture) :> IAdaptiveValue
            "ViewTrafo", viewTrafo :> IAdaptiveValue
            "ProjTrafo", projTrafo :> IAdaptiveValue
            "ModelNormals", modelNormals :> IAdaptiveValue
            "FloorIndices", floorIndices :> IAdaptiveValue
            "FloorTextureCoords", floorTextureCoords :> IAdaptiveValue
            "TextureFloor", DefaultTextures.checkerboard :> IAdaptiveValue
            "LightLocation", lightLocation :> IAdaptiveValue
            "SphereCenters", sphereCenters :> IAdaptiveValue
        ]
        |> UniformProvider.ofMap

    let staticObjects =
        let objModel =
            traceobject {
                geometry accelerationStructureModel
                hitgroup Semantic.HitGroupModel
                culling (CullMode.Enabled WindingOrder.CounterClockwise)
            }

        let objFloor =
            traceobject {
                geometry accelerationStructureFloor
                hitgroup Semantic.HitGroupFloor
                culling (CullMode.Enabled WindingOrder.CounterClockwise)
            }

        let objSphere =
            traceobject {
                geometry accelerationStructureSphere
                hitgroup Semantic.HitGroupSphere
                transform (sphereCenter |> AVal.map Trafo3d.Translation)
                customIndex 0
            }

        let objSphere2 =
            traceobject {
                geometry accelerationStructureSphere
                hitgroup Semantic.HitGroupSphere
                transform (sphereCenter2 |> AVal.map Trafo3d.Translation)
                customIndex 1
            }

        ASet.ofList [objModel; objSphere2; objFloor; objSphere]

    let dynamicObjects =
        cset<TraceObject>()

    let objects =
        ASet.union staticObjects dynamicObjects
        |> ASet.compact

    let scene =
        { Objects = objects; Usage = AccelerationStructureUsage.Static }

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