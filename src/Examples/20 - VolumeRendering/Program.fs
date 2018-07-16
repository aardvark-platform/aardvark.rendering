open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open System.IO

[<ReflectedDefinition>]
module Shader =
    open FShade

    let volumeTexture =
        sampler3d {
            texture uniform?VolumeTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
            addressW WrapMode.Clamp
        }

    let pickRay (p : V2d) =
        let pn = uniform.ViewProjTrafoInv * V4d(p.X, p.Y, 0.0, 1.0)
        let nearPlanePoint = pn.XYZ / pn.W
        Vec.normalize nearPlanePoint

    type Vertex =
        {
            [<Position>]
            pos : V4d

            [<Semantic("RayDirection")>]
            dir : V3d

            [<Semantic("CubeCoord")>]
            cubeCoord : V3d

        }

    let vertex (v : Vertex) =
        vertex {
            let cameraInModel = uniform.ModelTrafoInv.TransformPos uniform.CameraLocation
            let wp = uniform.ModelTrafo * v.pos
            return {
                pos = uniform.ViewProjTrafo * wp
                dir = v.pos.XYZ - cameraInModel
                cubeCoord = v.pos.XYZ
            }
        }

    let fragment (v : Vertex) =
        fragment {
            let size = volumeTexture.Size
            let mutable color = V3d.Zero
                
            let mutable sampleLocation = v.cubeCoord

            let steps = 100

            let dir = -Vec.normalize v.dir / float steps

            do
                while sampleLocation.X >= 0.0 && sampleLocation.X <= 1.0 && sampleLocation.Y >= 0.0 && sampleLocation.Y <= 1.0 && sampleLocation.Z >= 0.0 && sampleLocation.Z <= 1.0 do
                    color <- color + V3d.III * volumeTexture.SampleLevel(sampleLocation, 0.0).X
                    sampleLocation <- sampleLocation + dir

            return V4d(2.0 * color / float steps, 1.0)
        }

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let folder = @"D:\volumes\hechtkopfsalamander male"
    let files = Directory.GetFiles folder

    let images = files |> Array.map (fun p -> PixImage.Create(p).ToPixImage<byte>(Col.Format.Gray))

    let s2d = images.[0].Size
    let volume = PixVolume<byte>(s2d.X, s2d.Y, files.Length, 1)
    for layer in 0 .. images.Length - 1 do
        volume.Tensor4.SubImageAtZ(int64 layer).Set(images.[layer].Volume) |> ignore


    let texture = PixTexture3d(volume, false) :> ITexture |> Mod.constant

    let size = V3d volume.Size / float volume.Size.NormMax

    let sg = 
        Sg.box' C4b.Red (Box3d(-size, size))
        |> Sg.uniform "VolumeTexture" texture
        |> Sg.shader {
            do! Shader.vertex
            do! Shader.fragment
            }
        |> Sg.cullMode (Mod.constant CullMode.CounterClockwise)




    

    // show the scene in a simple window
    show {
        backend Backend.Vulkan
        display Display.Mono
        debug true
        verbosity DebugVerbosity.Warning
        samples 8
        scene sg
    }

    0
