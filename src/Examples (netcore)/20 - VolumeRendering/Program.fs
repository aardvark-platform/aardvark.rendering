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
            position : V4d

            [<PointSize>]
            pointSize : float
        }

    let estimateNormal (pos : V3d) (level : int) : V3d =
        let size = volumeTexture.Size 
        let level = float level

        let h = (1.0 / V3d size)

        let sxp = volumeTexture.SampleLevel(pos + V3d(h.X, 0.0, 0.0), level).X
        let sxn = volumeTexture.SampleLevel(pos + V3d(-h.X, 0.0, 0.0), level).X
        let syp = volumeTexture.SampleLevel(pos + V3d(0.0, h.Y, 0.0), level).X
        let syn = volumeTexture.SampleLevel(pos + V3d(0.0, -h.Y, 0.0), level).X
        let szp = volumeTexture.SampleLevel(pos + V3d(0.0, 0.0, h.Z), level).X
        let szn = volumeTexture.SampleLevel(pos + V3d(0.0, 0.0, -h.Z), level).X

        let s1 = V3d(sxn, syn, szn)
        let s2 = V3d(sxp, syp, szp)
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
            let coord = V3d(x,y,z) / V3d size

            let d = volumeTexture.SampleLevel(coord, 0.0).X
            let n = estimateNormal coord 0
            let gmag2 = Vec.length n 

            let wp = V4d(d * 2.0 - 1.0, gmag2 * 0.7 * 2.0 - 1.0, 1.0, 1.0) 

            return 
                { v with
                    position = wp
                    pointSize = 2.0
                } 
        }
    
    let vis (v : Effects.Vertex) =
        fragment {
            let c = v.c.X
            if c >= 1.0 then return V4d.IIII * c * 0.002 // Fun.Log2  c * 0.04
            else return V4d.Zero
        }

    let fragment (v : Vertex) =
        fragment { 
            return V4d.IIII
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

    let images = files |> Array.map (fun p -> PixImage.Create(p).ToPixImage<byte>(Col.Format.Gray))

    let s2d = images.[0].Size
    let volume = PixVolume<byte>(s2d.X, s2d.Y, files.Length, 1)
    for layer in 0 .. images.Length - 1 do
        volume.Tensor4.SubImageAtZ(int64 layer).Set(images.[layer].Volume) |> ignore


    let texture = PixTexture3d(volume, false) :> ITexture
    let texture = win.Runtime.PrepareTexture(texture) :> ITexture |> AVal.constant


    let fvc = int64 volume.Size.X * int64 volume.Size.Y * int64 volume.Size.Z
    let factor = fvc / (256L * 256L * 256L)

    let drawCall = { DrawCallInfo.empty with FaceVertexCount = (fvc / factor |> int); InstanceCount = 1 }

    let signature =
        win.Runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, RenderbufferFormat.R32f
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
