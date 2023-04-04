open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.SceneGraph.Raytracing
open Aardvark.Application
open Aardvark.Application.Slim
open FSharp.Data.Adaptive

[<AutoOpen>]
module Semantics =
    module HitGroup =
        let Model = Sym.ofString "HitGroupModel"
        let Floor = Sym.ofString "HitGroupFloor"
        let Sphere = Sym.ofString "HitGroupSphere"

    module MissShader =
        let Shadow = Sym.ofString "MissShadow"

    module InstanceAttribute =
        let ModelMatrix = Sym.ofString "ModelMatrix"
        let NormalMatrix = Sym.ofString "NormalMatrix"

    module FaceAttribute =
        let Colors = Sym.ofString "FaceColors"

    module GeometryAttribute =
        let Colors = Sym.ofString "Colors"

module Effect =
    open FShade

    [<AutoOpen>]
    module private Shaders =

        type UniformScope with
            member x.OutputBuffer : Image2d<Formats.rgba32f> = uniform?OutputBuffer
            member x.RecursionDepth : int = uniform?RecursionDepth

            member x.Positions      : V3d[]                 = uniform?StorageBuffer?Positions
            member x.Normals        : V3d[]                 = uniform?StorageBuffer?Normals
            member x.Indices        : int[]                 = uniform?StorageBuffer?Indices
            member x.TextureCoords  : V2d[]                 = uniform?StorageBuffer?TextureCoords
            member x.ModelMatrices  : M44d[]                = uniform?StorageBuffer?ModelMatrices
            member x.NormalMatrices : M33d[]                = uniform?StorageBuffer?NormalMatrices
            member x.FaceColors     : V3d[]                 = uniform?StorageBuffer?FaceColors
            member x.Colors         : V3d[]                 = uniform?StorageBuffer?Colors

            member x.SphereOffsets  : V3d[]                 = uniform?StorageBuffer?SphereOffsets

        type Payload =
            {
                recursionDepth : int
                color : V3d
            }

        let private mainScene =
            scene {
                accelerationStructure uniform?MainScene
            }

        let private textureFloor =
            sampler2d {
                texture uniform?TextureFloor
                filter Filter.MinMagPointMipLinear
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        [<ReflectedDefinition>]
        let trace (origin : V3d) (offset : V2d) (input : RayGenerationInput) =
            let pixelCenter = V2d input.work.id.XY + offset
            let inUV = pixelCenter / V2d input.work.size.XY
            let d = inUV * 2.0 - 1.0

            let target = uniform.ProjTrafoInv * V4d(d, 1.0, 1.0)
            let direction = uniform.ViewTrafoInv * V4d(target.XYZ.Normalized, 0.0)

            let payload = { recursionDepth = 0; color = V3d.Zero }
            let result = mainScene.TraceRay<Payload>(origin.XYZ, direction.XYZ, payload, flags = RayFlags.CullBackFacingTriangles)
            result.color

        let rgenMain (input : RayGenerationInput) =
            raygen {
                let origin = uniform.ViewTrafoInv * V4d.WAxis

                let c1 = input |> trace origin.XYZ (V2d(0.25, 0.25))
                let c2 = input |> trace origin.XYZ (V2d(0.75, 0.25))
                let c3 = input |> trace origin.XYZ (V2d(0.25, 0.75))
                let c4 = input |> trace origin.XYZ (V2d(0.75, 0.75))
                let final = (c1 + c2 + c3 + c4) / 4.0

                uniform.OutputBuffer.[input.work.id.XY] <- V4d(final, 1.0)
            }

        let missSky (input : RayMissInput) =
            let top = V3d(0.25, 0.5, 1.0)
            let bottom = V3d C3d.SlateGray

            miss {
                let color =
                    if input.ray.direction.Z > 0.0 then
                        input.ray.direction.Z |> lerp bottom top
                    else
                        bottom

                return { color = color; recursionDepth = 0 }
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
        let getIndices (info : TraceGeometryInfo) (input : RayHitInput<'T, 'U>) =
            let firstIndex = info.FirstIndex + 3 * input.geometry.primitiveId
            let baseVertex = info.BaseVertex

            V3i(uniform.Indices.[firstIndex],
                uniform.Indices.[firstIndex + 1],
                uniform.Indices.[firstIndex + 2]) + baseVertex

        [<ReflectedDefinition>]
        let getPosition (indices : V3i) (input : RayHitInput<'T, V2d>) =
            let p0 = uniform.Positions.[indices.X]
            let p1 = uniform.Positions.[indices.Y]
            let p2 = uniform.Positions.[indices.Z]
            input.hit.attribute |> fromBarycentric p0 p1 p2

        [<ReflectedDefinition>]
        let getNormal (indices : V3i) (input : RayHitInput<'T, V2d>) =
            let n0 = uniform.Normals.[indices.X]
            let n1 = uniform.Normals.[indices.Y]
            let n2 = uniform.Normals.[indices.Z]
            input.hit.attribute |> fromBarycentric n0 n1 n2

        [<ReflectedDefinition>]
        let getTextureCoords (indices : V3i) (input : RayHitInput<'T, V2d>) =
            let uv0 = uniform.TextureCoords.[indices.X]
            let uv1 = uniform.TextureCoords.[indices.Y]
            let uv2 = uniform.TextureCoords.[indices.Z]
            input.hit.attribute |> fromBarycentric2d uv0 uv1 uv2

        [<ReflectedDefinition>]
        let diffuseLighting (normal : V3d) (position : V3d) =
            let L = uniform.LightLocation - position |> Vec.normalize
            let NdotL = Vec.dot normal L |> max 0.0

            let ambient = 0.3
            ambient + NdotL

        [<ReflectedDefinition>]
        let specularLighting (shininess : float) (normal : V3d) (position : V3d) =
            let L = uniform.LightLocation - position |> Vec.normalize
            let V = uniform.CameraLocation - position |> Vec.normalize
            let R = Vec.reflect normal -L
            let VdotR = Vec.dot V R |> max 0.0
            pow VdotR shininess

        [<ReflectedDefinition>]
        let reflection (depth : int) (direction : V3d) (normal : V3d) (position : V3d) =
            if depth < uniform.RecursionDepth then
                let direction = Vec.reflect normal direction
                let payload = { recursionDepth = depth + 1; color = V3d.Zero }
                let result = mainScene.TraceRay(position, direction, payload, flags = RayFlags.CullBackFacingTriangles)
                result.color
            else
                V3d.Zero

        [<ReflectedDefinition>]
        let lightingWithShadow (mask : int32) (reflectiveness : float) (specularAmount : float) (shininess : float)
                               (position : V3d) (normal : V3d) (input : RayHitInput<Payload>) =

            let shadowed =
                let direction = Vec.normalize (uniform.LightLocation - position)
                let flags = RayFlags.SkipClosestHitShader ||| RayFlags.TerminateOnFirstHit ||| RayFlags.Opaque ||| RayFlags.CullFrontFacingTriangles
                mainScene.TraceRay<bool>(position, direction, payload = true, miss = "MissShadow", flags = flags, minT = 0.01, cullMask = mask)

            let diffuse = diffuseLighting normal position

            let result =
                if reflectiveness > 0.0 then
                    let reflection = reflection input.payload.recursionDepth input.ray.direction normal position
                    reflectiveness |> lerp (V3d(diffuse)) reflection
                else
                    V3d(diffuse)

            if shadowed then
                0.3 * result
            else
                let specular = specularLighting shininess normal position
                result + specularAmount * V3d(specular)

        let chitFaceColor (input : RayHitInput<Payload>) =
            closestHit {
                let info = TraceGeometryInfo.ofRayHit input
                let indices = getIndices info input

                let position =
                    let p = getPosition indices input
                    let m = uniform.ModelMatrices.[info.InstanceAttributeIndex]
                    m.TransformPos p

                let normal =
                    let n = getNormal indices input
                    let m = uniform.NormalMatrices.[info.InstanceAttributeIndex]
                    (m * n) |> Vec.normalize

                let color = uniform.FaceColors.[info.BasePrimitive + input.geometry.primitiveId]
                let diffuse = lightingWithShadow 0xFF 0.0 1.0 16.0 position normal input
                return { color = color * diffuse; recursionDepth = 0 }
            }

        let chitTextured (input : RayHitInput<Payload>) =
            closestHit {
                let info = TraceGeometryInfo.ofRayHit input
                let indices = getIndices info input

                let position =
                    let p = getPosition indices input
                    let m = uniform.ModelMatrices.[info.InstanceAttributeIndex]
                    m.TransformPos p

                let texCoords =
                    getTextureCoords indices input

                let color = textureFloor.Sample(texCoords).XYZ
                let diffuse = lightingWithShadow 0xFF 0.3 0.5 28.0 position V3d.ZAxis input
                return { color = color * diffuse; recursionDepth = 0 }
            }

        let chitSphere (input : RayHitInput<Payload>) =
            closestHit {
                let info = TraceGeometryInfo.ofRayHit input
                let position = input.ray.origin + input.hit.t * input.ray.direction
                let center = input.objectSpace.objectToWorld.TransformPos uniform.SphereOffsets.[input.geometry.geometryIndex]
                let normal = Vec.normalize (position - center)

                let color = uniform.Colors.[info.GeometryAttributeIndex]
                let diffuse = lightingWithShadow 0x7F 0.8 1.0 28.0 position normal input
                return { color = color * diffuse; recursionDepth = 0 }
            }

        let intersectionSphere (radius : float) (input : RayIntersectionInput) =
            intersection {
                let origin = input.objectSpace.rayOrigin - uniform.SphereOffsets.[input.geometry.geometryIndex]
                let direction = input.objectSpace.rayDirection

                let a = Vec.dot direction direction
                let b = 2.0 * Vec.dot origin direction
                let c = (Vec.dot origin origin) - (radius * radius)

                let discriminant = b * b - 4.0 * a * c
                if discriminant >= 0.0 then
                    let t = (-b - sqrt discriminant) / (2.0 * a)
                    Intersection.Report(t) |> ignore
            }


    let private hitGroupModel =
        hitgroup {
            closestHit chitFaceColor
        }

    let private hitGroupFloor =
        hitgroup {
            closestHit chitTextured
        }

    let private hitGroupSphere =
        hitgroup {
            closestHit chitSphere
            intersection (intersectionSphere 0.2)
        }

    let main =
        raytracingEffect {
            raygen rgenMain
            miss missSky
            miss (MissShader.Shadow, missShadow)
            hitgroup (HitGroup.Model, hitGroupModel)
            hitgroup (HitGroup.Floor, hitGroupFloor)
            hitgroup (HitGroup.Sphere, hitGroupSphere)
        }

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    let rnd = RandomSystem()

    use win =
        window {
            display Display.Mono
            samples 8
            backend Backend.Vulkan
            debug true
        }

    let runtime = win.Runtime

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

    use geometryPool =
        let signature =
            let vertexAttributes =
                Map.ofList [
                    DefaultSemantic.Positions, typeof<V4f>
                    DefaultSemantic.Normals, typeof<V4f>
                    DefaultSemantic.DiffuseColorCoordinates, typeof<V2f>
                ]

            let instanceAttributes =
                Map.ofList [
                    InstanceAttribute.ModelMatrix, typeof<M44f>
                    InstanceAttribute.NormalMatrix, typeof<M34f>
                ]

            let faceAttributes =
                Map.ofList [
                    FaceAttribute.Colors, typeof<V4f>
                ]

            let geometryAttributes =
                Map.ofList [
                    GeometryAttribute.Colors, typeof<V4f>
                ]

            { IndexType              = IndexType.UInt32
              VertexAttributeTypes   = vertexAttributes
              FaceAttributeTypes     = faceAttributes
              InstanceAttributeTypes = instanceAttributes
              GeometryAttributeTypes = geometryAttributes }

        new ManagedTracePool(runtime, signature)

    let model =
        let scene = Loader.Assimp.load (Path.combine [__SOURCE_DIRECTORY__; "..";"..";"..";"data";"aardvark";"aardvark.obj"])

        let trafo =
            Trafo3d.Scale(5.0, 5.0, -5.0) * Trafo3d.Translation(0.0, 0.0, 1.5)

        let colors =
            let rnd = RandomSystem()

            Array.init scene.meshes.[0].geometry.FaceCount (fun _ ->
                rnd.UniformC3d()
            )
            |> BufferView.ofArray

        let instanceAttr =
            let normalMatrix =
                trafo.Backward.Transposed.UpperLeftM33()

            Map.ofList [
                InstanceAttribute.ModelMatrix, AVal.constant trafo.Forward :> IAdaptiveValue
                InstanceAttribute.NormalMatrix, AVal.constant normalMatrix :> IAdaptiveValue
            ]

        scene.meshes.[0].geometry
        |> TraceObject.ofIndexedGeometry GeometryFlags.Opaque trafo
        |> TraceObject.instanceAttributes instanceAttr
        |> TraceObject.faceAttribute (FaceAttribute.Colors, colors)
        |> TraceObject.hitGroup HitGroup.Model
        |> TraceObject.frontFace WindingOrder.Clockwise

    let floor =
        let positions = [| V3f(-0.5f, -0.5f, 0.0f); V3f(-0.5f, 0.5f, 0.0f); V3f(0.5f, -0.5f, 0.0f); V3f(0.5f, 0.5f, 0.0f); |]
        let indices = [| 0; 1; 2; 3 |]
        let uv = positions |> Array.map (fun p -> p.XY + 0.5f)

        let vertexAttr =
            SymDict.ofList [
                DefaultSemantic.Positions, positions :> System.Array
                DefaultSemantic.DiffuseColorCoordinates, uv :> System.Array
            ]

        let trafo = Trafo3d.Scale(48.0)

        let instanceAttr =
            let normalMatrix =
                trafo.Backward.Transposed.UpperLeftM33()

            Map.ofList [
                InstanceAttribute.ModelMatrix, AVal.constant trafo.Forward :> IAdaptiveValue
                InstanceAttribute.NormalMatrix, AVal.constant normalMatrix :> IAdaptiveValue
            ]

        let geom =
            IndexedGeometry(IndexedGeometryMode.TriangleStrip, indices, vertexAttr, SymDict.empty)

        geom
        |> TraceObject.ofIndexedGeometry GeometryFlags.Opaque trafo
        |> TraceObject.instanceAttributes instanceAttr
        |> TraceObject.hitGroup HitGroup.Floor
        |> TraceObject.frontFace WindingOrder.CounterClockwise

    let sphereOffsets =
        let o1 = V3d(0.0, 0.0, 0.5)
        let o2 = V3d(0.0, 0.0, -0.5)
        let o3 = V3d(0.5, 0.0, 0.0)
        let o4 = V3d(-0.5, 0.0, 0.0)
        let o5 = V3d(0.0, 0.5, 0.0)
        let o6 = V3d(0.0, -0.5, 0.0)

        [| o1; o2; o3; o4; o5; o6 |]

    let indices =
        geometryPool.IndexBuffer

    let positions =
        geometryPool.GetVertexAttribute DefaultSemantic.Positions

    let textureCoordinates =
        geometryPool.GetVertexAttribute DefaultSemantic.DiffuseColorCoordinates

    let normals =
        geometryPool.GetVertexAttribute DefaultSemantic.Normals

    let modelMatrices =
        geometryPool.GetInstanceAttribute InstanceAttribute.ModelMatrix

    let normalMatrices =
        geometryPool.GetInstanceAttribute InstanceAttribute.NormalMatrix

    let faceColors =
        geometryPool.GetFaceAttribute FaceAttribute.Colors

    let colors =
        geometryPool.GetGeometryAttribute GeometryAttribute.Colors

    let geometryInfos =
        geometryPool.GeometryBuffer

    let lightLocation =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            V3d(Rot2d(t * Constant.PiQuarter) * V2d(8.0, 0.0), 16.0)
        )

    let cameraLocation =
        cameraView |> AVal.map CameraView.location

    let uniforms =
        uniformMap {
            buffer  DefaultSemantic.TraceGeometryBuffer geometryInfos
            value   "RecursionDepth"                    4
            value   "ViewTrafo"                         viewTrafo
            value   "ProjTrafo"                         projTrafo
            value   "CameraLocation"                    cameraLocation
            buffer  "Positions"                         positions
            buffer  "Normals"                           normals
            buffer  "Indices"                           indices
            buffer  "TextureCoords"                     textureCoordinates
            buffer  "ModelMatrices"                     modelMatrices
            buffer  "NormalMatrices"                    normalMatrices
            buffer  "FaceColors"                        faceColors
            buffer  "Colors"                            colors
            buffer  "SphereOffsets"                     (sphereOffsets |> Array.map V4f)
            texture "TextureFloor"                      DefaultTextures.checkerboard
            value   "LightLocation"                     lightLocation
        }

    let staticObjects =
        ASet.ofList [model; floor]

    let sphereObjects =
        cset<TraceObject>()

    let createSphere =

        let colors = [|
            C3d.BlueViolet
            C3d.DodgerBlue
            C3d.HoneyDew
            C3d.BlanchedAlmond
            C3d.LimeGreen
            C3d.MistyRose
        |]

        fun () ->
            let colors =
                let p = rnd.CreatePermutationArray(colors.Length)
                colors |> Array.permute (fun i -> p.[i])

            let aabbs =
                sphereOffsets |> Array.map (fun offset ->
                    BoundingBoxes.ofCenterAndRadius offset 0.2
                    |> BoundingBoxes.flags GeometryFlags.Opaque
                )
                |> TraceGeometry.AABBs

            let trafo =
                let startTime = System.DateTime.Now

                let position = rnd.UniformV3d(Box3d(V3d(-10.0, -10.0, 2.0), V3d(10.0)))
                let rotation = ((rnd.UniformV3d()) * 2.0 - 1.0) * 2.0 * Constant.Pi

                win.Time |> AVal.map (fun t ->
                    let t = (t - startTime).TotalSeconds
                    Trafo3d.RotationEuler(t * rotation) * Trafo3d.Translation(position)
                )

            TraceObject.ofGeometry aabbs
            |> TraceObject.geometryAttribute (GeometryAttribute.Colors, colors)
            |> TraceObject.hitGroups (HitGroup.Sphere |> List.replicate 6)
            |> TraceObject.transform trafo
            |> TraceObject.mask 0x80

    let scene =
        ASet.union staticObjects sphereObjects
        |> RaytracingSceneDescription.ofPool geometryPool

    let pipeline =
        {
            Effect            = Effect.main
            Scenes            = Map.ofList [Sym.ofString "MainScene", scene]
            Uniforms          = uniforms
            MaxRecursionDepth = AVal.constant 2048
        }

    let traceOutput =
        runtime.TraceTo2D(win.Sizes, TextureFormat.Rgba8, "OutputBuffer", pipeline)

    let sg =
        Sg.fullScreenQuad
        |> Sg.diffuseTexture traceOutput
        |> Sg.shader {
            do! DefaultSurfaces.diffuseTexture
        }

    win.Keyboard.KeyDown(Keys.Enter).Values.Add(fun _ ->
        transact (fun () ->
            let s = createSphere()
            sphereObjects.Value <- sphereObjects.Value |> HashSet.add s
        )
    )

    win.Keyboard.KeyDown(Keys.Delete).Values.Add(fun _ ->
        transact (fun () ->
            let list = sphereObjects.Value |> HashSet.toList
            let idx = rnd.UniformInt(list.Length)
            let set = list |> List.indexed |> List.filter (fst >> (<>) idx) |> List.map snd |> HashSet.ofList
            sphereObjects.Value <- set
        )
    )

    win.Scene <- sg
    win.Run(preventDisposal = true)

    0