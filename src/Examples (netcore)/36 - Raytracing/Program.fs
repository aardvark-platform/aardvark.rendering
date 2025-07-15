open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Assimp
open Aardvark.SceneGraph.Raytracing
open Aardvark.Application
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
            member x.RecursionDepth : int    = uniform?RecursionDepth
            member x.Positions      : V3f[]  = uniform?StorageBuffer?Positions
            member x.Normals        : V3f[]  = uniform?StorageBuffer?Normals
            member x.TextureCoords  : V2f[]  = uniform?StorageBuffer?DiffuseColorCoordinates
            member x.NormalMatrices : M33f[] = uniform?StorageBuffer?NormalMatrix
            member x.FaceColors     : V3f[]  = uniform?StorageBuffer?FaceColors
            member x.Colors         : V3f[]  = uniform?StorageBuffer?Colors
            member x.SphereOffsets  : V3f[]  = uniform?StorageBuffer?SphereOffsets

        type Payload =
            {
                color       : V3f
                origin      : V3f
                direction   : V3f
                attenuation : float32
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
        let trace (origin : V3f) (offset : V2f) (input : RayGenerationInput) =
            let pixelCenter = V2f input.work.id.XY + offset
            let inUV = pixelCenter / V2f input.work.size.XY
            let d = inUV * 2.0f - 1.0f

            let target = uniform.ProjTrafoInv * V4f(d, 1.0f, 1.0f)
            let direction = uniform.ViewTrafoInv * V4f(target.XYZ.Normalized, 0.0f)

            let mutable depth = 0
            let mutable final = V3f.Zero

            let mutable payload =
                { color       = V3f.Zero
                  origin      = origin.XYZ
                  direction   = direction.XYZ
                  attenuation = 1.0f }

            while depth < uniform.RecursionDepth && payload.attenuation > 0.0f do
                let attenuation = payload.attenuation
                payload <- mainScene.TraceRay<Payload>(payload.origin, payload.direction, payload, flags = RayFlags.CullBackFacingTriangles)
                final <- final + payload.color * attenuation
                depth <- depth + 1

            final

        let rgenMain (input : RayGenerationInput) =
            raygen {
                let origin = uniform.ViewTrafoInv * V4f.WAxis

                let c1 = input |> trace origin.XYZ (V2f(0.25f, 0.25f))
                let c2 = input |> trace origin.XYZ (V2f(0.75f, 0.25f))
                let c3 = input |> trace origin.XYZ (V2f(0.25f, 0.75f))
                let c4 = input |> trace origin.XYZ (V2f(0.75f, 0.75f))
                let final = (c1 + c2 + c3 + c4) / 4.0f

                uniform.OutputBuffer.[input.work.id.XY] <- V4f(final, 1.0f)
            }

        let missSky (input : RayMissInput) =
            let top = V3f(0.25f, 0.5f, 1.0f)
            let bottom = V3f C3f.SlateGray

            miss {
                let color =
                    if input.ray.direction.Z > 0.0f then
                        input.ray.direction.Z |> lerp bottom top
                    else
                        bottom

                return {
                    color       = color
                    origin      = V3f.Zero
                    direction   = V3f.Zero
                    attenuation = 0.0f
                }
            }

        let missShadow =
            miss { return false }

        [<ReflectedDefinition>]
        let fromBarycentric (v0 : V3f) (v1 : V3f) (v2 : V3f) (coords : V2f) =
            let barycentricCoords = V3f(1.0f - coords.X - coords.Y, coords.X, coords.Y)
            v0 * barycentricCoords.X + v1 * barycentricCoords.Y + v2 * barycentricCoords.Z

        [<ReflectedDefinition>]
        let fromBarycentric2d (v0 : V2f) (v1 : V2f) (v2 : V2f) (coords : V2f) =
            let barycentricCoords = V3f(1.0f - coords.X - coords.Y, coords.X, coords.Y)
            v0 * barycentricCoords.X + v1 * barycentricCoords.Y + v2 * barycentricCoords.Z

        // If the position fetch feature is supported and enabled, we can get the object space triangle positions directly from the shader input.
        // Inline this function to eliminate the use of hit.positions if not enabled or supported.
        [<ReflectedDefinition; Inline>]
        let getPosition (positionFetch : bool) (indices : V3i) (input : RayHitInput<'T, V2f>) =
            if positionFetch then
                let p0 = input.hit.positions.[0]
                let p1 = input.hit.positions.[1]
                let p2 = input.hit.positions.[2]
                input.hit.attribute |> fromBarycentric p0 p1 p2
            else
                let p0 = uniform.Positions.[indices.X]
                let p1 = uniform.Positions.[indices.Y]
                let p2 = uniform.Positions.[indices.Z]
                input.hit.attribute |> fromBarycentric p0 p1 p2

        [<ReflectedDefinition>]
        let getNormal (indices : V3i) (input : RayHitInput<'T, V2f>) =
            let n0 = uniform.Normals.[indices.X]
            let n1 = uniform.Normals.[indices.Y]
            let n2 = uniform.Normals.[indices.Z]
            input.hit.attribute |> fromBarycentric n0 n1 n2

        [<ReflectedDefinition>]
        let getTextureCoords (indices : V3i) (input : RayHitInput<'T, V2f>) =
            let uv0 = uniform.TextureCoords.[indices.X]
            let uv1 = uniform.TextureCoords.[indices.Y]
            let uv2 = uniform.TextureCoords.[indices.Z]
            input.hit.attribute |> fromBarycentric2d uv0 uv1 uv2

        [<ReflectedDefinition>]
        let diffuseLighting (normal : V3f) (position : V3f) =
            let L = uniform.LightLocation - position |> Vec.normalize
            let NdotL = Vec.dot normal L |> max 0.0f

            let ambient = 0.1f
            ambient + 0.9f * NdotL

        [<ReflectedDefinition>]
        let specularLighting (shininess : float32) (normal : V3f) (position : V3f) =
            let L = uniform.LightLocation - position |> Vec.normalize
            let V = uniform.CameraLocation - position |> Vec.normalize
            let R = Vec.reflect normal -L
            let VdotR = Vec.dot V R |> max 0.0f
            0.8f * pow VdotR shininess

        [<ReflectedDefinition>]
        let lightingWithShadow (mask : int32) (diffuse : V3f) (reflectiveness : float32) (specularAmount : float32) (shininess : float32)
                               (position : V3f) (normal : V3f) (input : RayHitInput<Payload>) =

            let shadowed =
                let direction = Vec.normalize (uniform.LightLocation - position)
                let flags = RayFlags.SkipClosestHitShader ||| RayFlags.TerminateOnFirstHit ||| RayFlags.Opaque ||| RayFlags.CullFrontFacingTriangles
                mainScene.TraceRay<bool>(position, direction, payload = true, miss = "MissShadow", flags = flags, minT = 0.01f, cullMask = mask)

            let color =
                let diffuse = diffuse * diffuseLighting normal position

                if shadowed then
                    diffuse * 0.3f
                else
                    let specular = specularLighting shininess normal position
                    diffuse + specularAmount * V3f(specular)

            { color       = color
              origin      = position
              direction   = Vec.reflect normal input.ray.direction
              attenuation = input.payload.attenuation * reflectiveness }

        let chitFaceColor (positionFetch : bool) (input : RayHitInput<Payload>) =
            closestHit {
                let info = TraceGeometryInfo.ofRayHit input
                let indices = TraceGeometryInfo.getIndices info input

                let position =
                    let p = getPosition positionFetch indices input
                    input.objectSpace.objectToWorld.TransformPos p

                let normal =
                    let n = getNormal indices input
                    let m = uniform.NormalMatrices.[info.InstanceAttributeIndex]
                    (m * n) |> Vec.normalize

                let diffuse = uniform.FaceColors.[info.BasePrimitive + input.geometry.primitiveId]
                return lightingWithShadow 0xFF diffuse 0.0f 1.0f 16.0f position normal input
            }

        let chitTextured (positionFetch : bool) (input : RayHitInput<Payload>) =
            closestHit {
                let info = TraceGeometryInfo.ofRayHit input
                let indices = TraceGeometryInfo.getIndices info input

                let position =
                    let p = getPosition positionFetch indices input
                    input.objectSpace.objectToWorld.TransformPos p

                let texCoords =
                    getTextureCoords indices input

                let diffuse = textureFloor.Sample(texCoords).XYZ
                return lightingWithShadow 0xFF diffuse 0.3f 0.5f 28.0f position V3f.ZAxis input
            }

        let chitSphere (input : RayHitInput<Payload>) =
            closestHit {
                let info = TraceGeometryInfo.ofRayHit input
                let position = input.ray.origin + input.hit.t * input.ray.direction
                let center = input.objectSpace.objectToWorld.TransformPos uniform.SphereOffsets.[input.geometry.geometryIndex]
                let normal = Vec.normalize (position - center)

                let diffuse = uniform.Colors.[info.GeometryAttributeIndex]
                return lightingWithShadow 0x7F diffuse 0.4f 0.8f 28.0f position normal input
            }

        let intersectionSphere (radius : float32) (input : RayIntersectionInput) =
            intersection {
                let origin = input.objectSpace.rayOrigin - uniform.SphereOffsets.[input.geometry.geometryIndex]
                let direction = input.objectSpace.rayDirection

                let a = Vec.dot direction direction
                let b = 2.0f * Vec.dot origin direction
                let c = (Vec.dot origin origin) - (radius * radius)

                let discriminant = b * b - 4.0f * a * c
                if discriminant >= 0.0f then
                    let t = (-b - sqrt discriminant) / (2.0f * a)
                    Intersection.Report(t) |> ignore
            }


    let private hitGroupModel (positionFetch : bool) =
        hitgroup {
            closestHit (chitFaceColor positionFetch)
        }

    let private hitGroupFloor (positionFetch : bool) =
        hitgroup {
            closestHit (chitTextured positionFetch)
        }

    let private hitGroupSphere =
        hitgroup {
            closestHit chitSphere
            intersection (intersectionSphere 0.2f)
        }

    let main (positionFetch : bool) =
        raytracingEffect {
            raygen rgenMain
            miss missSky
            miss MissShader.Shadow missShadow
            hitgroup HitGroup.Model (hitGroupModel positionFetch)
            hitgroup HitGroup.Floor (hitGroupFloor positionFetch)
            hitgroup HitGroup.Sphere hitGroupSphere
        }

[<EntryPoint>]
let main _argv =
    Aardvark.Init()

    let rnd = RandomSystem()

    use win =
        window {
            display Display.Mono
            samples 8
            backend Backend.Vulkan
            debug true
        }

    let runtime = win.Runtime :?> Vulkan.Runtime
    let positionFetch = runtime.Device.EnabledFeatures.Raytracing.PositionFetch

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
                    if not positionFetch then DefaultSemantic.Positions, typeof<V4f>
                    DefaultSemantic.Normals, typeof<V4f>
                    DefaultSemantic.DiffuseColorCoordinates, typeof<V2f>
                ]

            let instanceAttributes =
                Map.ofList [
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

            { IndexType              = IndexType.Int32
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
                InstanceAttribute.NormalMatrix, AVal.constant normalMatrix :> IAdaptiveValue
            ]

        scene.meshes.[0].geometry
        |> TraceObject.ofIndexedGeometry GeometryFlags.Opaque Trafo3d.Identity
        |> TraceObject.transform trafo
        |> TraceObject.instanceAttributes instanceAttr
        |> TraceObject.faceAttribute (FaceAttribute.Colors, colors)
        |> TraceObject.hitGroup HitGroup.Model
        |> TraceObject.frontFace WindingOrder.CounterClockwise

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
                InstanceAttribute.NormalMatrix, AVal.constant normalMatrix :> IAdaptiveValue
            ]

        let geom =
            IndexedGeometry(IndexedGeometryMode.TriangleStrip, indices, vertexAttr, SymDict.empty)

        geom
        |> TraceObject.ofIndexedGeometry GeometryFlags.Opaque Trafo3d.Identity
        |> TraceObject.transform trafo
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

    let lightLocation =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            V3d(Rot2d(t * Constant.PiQuarter) * V2d(8.0, 0.0), 16.0)
        )

    let cameraLocation =
        cameraView |> AVal.map CameraView.location

    let uniforms =
        let custom =
            uniformMap {
                value   "RecursionDepth" 4
                value   "ViewTrafo"      viewTrafo
                value   "ProjTrafo"      projTrafo
                value   "CameraLocation" cameraLocation
                buffer  "SphereOffsets"  (sphereOffsets |> Array.map V4f)
                texture "TextureFloor"   DefaultTextures.checkerboard
                value   "LightLocation"  lightLocation
            }

        UniformProvider.union geometryPool.Uniforms custom

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
                    BoundingBoxes.FromCenterAndRadius(offset, 0.2, GeometryFlags.Opaque)
                )
                |> TraceGeometry.AABBs

            let trafo =
                let startTime = System.DateTime.Now

                let position = rnd.UniformV3d(Box3d(V3d(-10.0, -10.0, 2.0), V3d(10.0)))
                let rotation = (rnd.UniformV3d() * 2.0 - 1.0) * 2.0 * Constant.Pi

                win.Time |> AVal.map (fun t ->
                    let t = (t - startTime).TotalSeconds
                    Trafo3d.RotationEuler(t * rotation) * Trafo3d.Translation(position)
                )

            TraceObject.ofGeometry aabbs
            |> TraceObject.geometryAttribute (GeometryAttribute.Colors, colors)
            |> TraceObject.hitGroups (HitGroup.Sphere |> Array.replicate 6)
            |> TraceObject.transform trafo
            |> TraceObject.mask 0x80

    let scene =
        ASet.union staticObjects sphereObjects
        |> RaytracingSceneDescription.ofPool geometryPool

    let pipeline =
        {
            Effect            = Effect.main positionFetch
            Scenes            = Map.ofList [Sym.ofString "MainScene", scene]
            Uniforms          = uniforms
            MaxRecursionDepth = AVal.constant 1
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