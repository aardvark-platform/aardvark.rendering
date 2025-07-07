open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
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

    let pickRay (p : V2f) =
        let pn = uniform.ViewProjTrafoInv * V4f(p.X, p.Y, 0.0f, 1.0f)
        let nearPlanePoint = pn.XYZ / pn.W
        Vec.normalize nearPlanePoint

    type Vertex =
        {
            [<Position>]
            pos : V4f

            [<Semantic("RayDirection")>]
            dir : V3f

            [<Semantic("CubeCoord")>]
            cubeCoord : V3f

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
            let mutable color = V3f.Zero
                
            let mutable sampleLocation = v.cubeCoord

            let steps = 100

            let dir = -Vec.normalize v.dir / float32 steps

            do
                while sampleLocation.X >= 0.0f && sampleLocation.X <= 1.0f && sampleLocation.Y >= 0.0f && sampleLocation.Y <= 1.0f && sampleLocation.Z >= 0.0f && sampleLocation.Z <= 1.0f do
                    color <- color + V3f.III * volumeTexture.SampleLevel(sampleLocation, 0.0f).X
                    sampleLocation <- sampleLocation + dir

            return V4f(2.0f * color / float32 steps, 1.0f)
        }

[<ReflectedDefinition>]
module Scatter =
    open Aardvark.Rendering
    open Aardvark.Rendering.Effects
    open FShade
    
    let volumeTexture =
        sampler3d {
            texture uniform?VolumeTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
            addressW WrapMode.Clamp
        }

    type Vertex =
        {
            [<VertexId>]
            id : int

            [<Position>]
            position : V4f

            [<PointSize>]
            pointSize : float32
        }

    let estimateNormal (pos : V3f) (level : int) : V3f =
        let size = volumeTexture.Size 
        let level = float32 level

        let h = (1.0f / V3f size)

        let sxp = volumeTexture.SampleLevel(pos + V3f(h.X, 0.0f, 0.0f), level).X
        let sxn = volumeTexture.SampleLevel(pos + V3f(-h.X, 0.0f, 0.0f), level).X
        let syp = volumeTexture.SampleLevel(pos + V3f(0.0f, h.Y, 0.0f), level).X
        let syn = volumeTexture.SampleLevel(pos + V3f(0.0f, -h.Y, 0.0f), level).X
        let szp = volumeTexture.SampleLevel(pos + V3f(0.0f, 0.0f, h.Z), level).X
        let szn = volumeTexture.SampleLevel(pos + V3f(0.0f, 0.0f, -h.Z), level).X

        let s1 = V3f(sxn, syn, szn)
        let s2 = V3f(sxp, syp, szp)
        let n =  (s2 - s1)
        -n
   

    let scatter (v : Vertex) =
        vertex {
            let size = volumeTexture.Size
            let factor : int = uniform?Factor
            let id = v.id * factor
            let z = id / (size.X * size.Y)
            let id = id - z * (size.X * size.Y)
            let x = id % size.X
            let y = id / size.Y
            let coord = V3f(x,y,z) / V3f size

            let d = volumeTexture.SampleLevel(coord, 0.0f).X
            let n = estimateNormal coord 0
            let gmag2 = Vec.length n 

            let wp = V4f(d * 2.0f - 1.0f, gmag2 * 0.7f * 2.0f - 1.0f, 1.0f, 1.0f)

            return 
                { v with
                    position = wp
                    pointSize = 2.0f
                } 
        }
    
    let vis (v : Effects.Vertex) =
        fragment {
            let c = v.c.X
            if c >= 1.0f then return V4f.IIII * c * 0.002f // Fun.Log2  c * 0.04
            else return V4f.Zero
        }

    let fragment (v : Vertex) =
        fragment { 
            return V4f.IIII
        }


[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()

    // show the scene in a simple window
    let win = window {
        backend Backend.GL
        display Display.Mono
        debug false
        samples 1
    }

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let folder = @"C:\Users\Schorsch\Development\WorkDirectory\hechtkopfsalamander male - Copy"
    let files = Directory.GetFiles folder

    let images = files |> Array.map (fun p -> PixImage.Load(p).ToPixImage<byte>(Col.Format.Gray))

    let s2d = images.[0].Size
    let volume = PixVolume<byte>(s2d.X, s2d.Y, files.Length, 1)
    for layer in 0 .. images.Length - 1 do
        volume.Tensor4.SubImageAtZ(int64 layer).Set(images.[layer].Volume) |> ignore


    let texture = PixTexture3d(volume, false) :> ITexture
    let texture = win.Runtime.PrepareTexture(texture) :> ITexture |> AVal.constant


    let fvc = int64 volume.Size.X * int64 volume.Size.Y * int64 volume.Size.Z
    let factor = fvc / (256L * 256L * 256L)

    let drawCall = DrawCallInfo(FaceVertexCount = (fvc / factor |> int), InstanceCount = 1)

    let signature =
        win.Runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, TextureFormat.R32f
        ]

    let scatterTexture = win.Runtime.CreateTexture2D(V2i(256,1024), TextureFormat.R32f, 1, 1)

    let fbo = 
        win.Runtime.CreateFramebuffer(
            signature, 
            Map.ofList [
                DefaultSemantic.Colors, scatterTexture.GetOutputView()
            ]
        )


    let size = V3d volume.Size / float volume.Size.NormMax

    let sg = 
        Sg.box' C4b.Red (Box3d(-size, size))
        |> Sg.uniform "VolumeTexture" texture
        |> Sg.shader {
            do! Shader.vertex
            do! Shader.fragment
            }
        |> Sg.cullMode (AVal.constant CullMode.Back)

    win.Scene <- sg
    win.Run()

    0
