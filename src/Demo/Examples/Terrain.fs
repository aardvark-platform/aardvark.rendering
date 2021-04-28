namespace Examples


open System
open System.IO
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open FSharp.Data.Adaptive.Operators
open Aardvark.Rendering
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO


module Terrain =
    [<Literal>]
    let HeightMap = "HeightMap"



    let grid (count : V2i) =
        let delta = 1.0 / V2d count

        let pos = Array.zeroCreate ((count.X + 1) * (count.Y + 1))
        let tc = Array.zeroCreate pos.Length
        let n = Array.zeroCreate pos.Length
        let t = Array.zeroCreate pos.Length
        let b = Array.zeroCreate pos.Length


        let mutable index = 0
        for y in 0 .. count.Y do
            for x in 0 .. count.X do
                let uv = delta * V2d(x,y)
                let v = V3d(uv, 0.0)
                pos.[index] <- V3f v
                tc.[index] <- V2f uv
                n.[index] <- V3f.OOI
                t.[index] <- V3f.IOO
                b.[index] <- V3f.OIO
                index <- index + 1


        let quads = count.X * count.Y
        let indices = Array.zeroCreate (3 * 2 * quads)
        let mutable oi = 0

        let getId (v : V2i) = v.X + v.Y * (count.X + 1)

        for y in 1 .. count.Y do
            for x in 1 .. count.X do
                let p = V2i(x,y)
                let i00 = getId (p - V2i.II)
                let i10 = getId (p - V2i.OI)
                let i01 = getId (p - V2i.IO)
                let i11 = getId p

                indices.[oi + 0] <- i00
                indices.[oi + 1] <- i10
                indices.[oi + 2] <- i01

                indices.[oi + 3] <- i10
                indices.[oi + 4] <- i01
                indices.[oi + 5] <- i11

                oi <- oi + 6


        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = indices,
            IndexedAttributes = 
                SymDict.ofList [
                    DefaultSemantic.Positions, pos :> System.Array
                    DefaultSemantic.DiffuseColorCoordinates, tc :> System.Array
                    DefaultSemantic.Normals, n :> System.Array
                    DefaultSemantic.DiffuseColorUTangents, t :> System.Array
                    DefaultSemantic.DiffuseColorVTangents, b :> System.Array
                ]
        )


    module Shader =
        open FShade

        let height =
            sampler2d {
                texture uniform?HeightMap
                filter Filter.MinMagMipLinear
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        type UniformScope with
            member x.CellSize : V2d = x?Height?CellSize

        type Vertex =
            {
                [<Position>]        pos : V4d
                [<Semantic("OffsetPos")>]        hpos : V4d
                [<WorldPosition>]   wp : V4d
                [<Normal>]          n : V3d
                [<BiNormal>]        b : V3d
                [<Tangent>]         t : V3d
                [<TexCoord>]        tc : V2d
            }

        let heightVertex (v : Vertex) =
            vertex {
                let s = uniform.CellSize

                let ddx = V2d(s.X, 0.0)
                let ddy = V2d(0.0, s.Y)

                let h0 = height.SampleGrad(v.tc, ddx, ddy).X
                let hr = height.SampleGrad(v.tc + V2d(0.5 * s.X, 0.0), ddx, ddy).X
                let hu = height.SampleGrad(v.tc + V2d(0.0, 0.5 * s.Y), ddx, ddy).X
    
                let pr = v.pos.XYZ + V3d(0.5 * s.X, 0.0, hr)
                let pu = v.pos.XYZ + V3d(0.0, 0.5 * s.Y, hu)
                let p0 = v.pos.XYZ + V3d(0.0, 0.0, h0)

                let t = (pr - p0) |> Vec.normalize
                let b = (pu - p0) |> Vec.normalize
                let n = Vec.cross t b |> Vec.normalize

                return { v with hpos = V4d(p0, 1.0); n = n; t = t; b = b }
            }

        [<ReflectedDefinition>]
        let inViewVolume (v : V3d) =
            v.X >= -1.0 && v.Y >= -1.0 && v.Z >= -1.0 && v.X <= 1.0 && v.Y <= 1.0 && v.Z <= 1.0

//        let tessFactor (p0 : V4d) (p1 : V4d) 
//            let wp0 = uniform.ModelTrafo * p0
//            let wp1 = uniform.ModelTrafo * p1
//
//            let c = 0.5 * (wp0 + wp1)
//            let cp = uniform.ViewProjTrafo * c
//            let cp = cp.XYZ / cp.W
//
//
//
//
//
//            let len = uniform.ModelTrafo.TransformDir (p1.XYZ - p.XYZ) |> Vec.length
//
//            let pp0 = uniform.ModelViewProjTrafo * p0
//            let pp1 = uniform.ModelViewProjTrafo * p1
//            let pp0 = pp0.XYZ / pp0.W
//            let pp1 = pp1.XYZ / pp1.W
//
//            let c = 0.5 * (p0 + p1)



        let heightTess (t : Triangle<Vertex>) =
            tessellation {
                let vs = V2d uniform.ViewportSize 
                let p0 = uniform.ModelViewProjTrafo * t.P0.hpos
                let p1 = uniform.ModelViewProjTrafo * t.P1.hpos
                let p2 = uniform.ModelViewProjTrafo * t.P2.hpos

                let pp0 = 0.5 * p0.XYZ / p0.W
                let pp1 = 0.5 * p1.XYZ / p1.W
                let pp2 = 0.5 * p2.XYZ / p2.W


                let mutable t01 = -1.0
                let mutable t12 = -1.0
                let mutable t20 = -1.0

                if inViewVolume pp0 || inViewVolume pp1 || inViewVolume pp2 then
                    let e01 = vs * (pp1.XY - pp0.XY) |> Vec.length
                    let e12 = vs * (pp2.XY - pp1.XY) |> Vec.length
                    let e20 = vs * (pp0.XY - pp2.XY) |> Vec.length

                    t01 <- 0.1 * e01 |> clamp 1.0 64.0
                    t12 <- 0.1 * e12 |> clamp 1.0 64.0
                    t20 <- 0.1 * e20 |> clamp 1.0 64.0

                let avg = (t01 + t12 + t20) / 3.0
                let! coord = tessellateTriangle avg (t12, t20, t01)

                let p = coord.X * t.P0.pos + coord.Y * t.P1.pos + coord.Z * t.P2.pos
                let tc = p.XY


                let s = V2d(0.01, 0.01)
                let ddx = V2d(s.X, 0.0)
                let ddy = V2d(0.0, s.Y)

                let h0 = height.SampleGrad(tc, ddx, ddy).X
                let hr = height.SampleGrad(tc + V2d(0.5 * s.X, 0.0), ddx, ddy).X
                let hu = height.SampleGrad(tc + V2d(0.0, 0.5 * s.Y), ddx, ddy).X
    
                let pr = p.XYZ + V3d(0.5 * s.X, 0.0, hr)
                let pu = p.XYZ + V3d(0.0, 0.5 * s.Y, hu)
                let p0 = p.XYZ + V3d(0.0, 0.0, h0)

                let t = (pr - p0) |> Vec.normalize
                let b = (pu - p0) |> Vec.normalize
                let n = Vec.cross t b |> Vec.normalize
                
                return { 
                    pos = uniform.ModelViewProjTrafo * V4d(p0, 1.0)
                    hpos = p
                    wp = uniform.ModelTrafo * V4d(p0, 1.0)
                    n = uniform.ModelTrafoInv.TransposedTransformDir n
                    t = uniform.ModelTrafo.TransformDir t
                    b = uniform.ModelTrafo.TransformDir b 
                    tc = tc
                }
            }


    let run() =
        FShade.EffectDebugger.attach()


        let bounds = AVal.init (Box3d(V3d(-10.0, -10.0, 0.0), V3d(10.0, 10.0, 3.0)))

        let gridCount = V2i(40, 40)
        let cellSize = 1.0 / V2d gridCount
        let sg = 
            grid gridCount
                |> Sg.ofIndexedGeometry
                |> Sg.trafo (bounds |> AVal.map (fun b -> Trafo3d.Scale(b.Size) * Trafo3d.Translation(b.Min)))

                |> Sg.fileTexture (Symbol.Create HeightMap) @"C:\Users\schorsch\Desktop\ps_height_4k.png" true
                |> Sg.fileTexture DefaultSemantic.DiffuseColorTexture @"C:\Users\schorsch\Desktop\ps_texture_4k.png" true

                |> Sg.uniform "CellSize" (AVal.constant cellSize)

                |> Sg.shader {
                    do! Shader.heightVertex
                    do! Shader.heightTess
//                    do! DefaultSurfaces.trafo
//                    do! DefaultSurfaces.constantColor C4f.Red
                    do! DefaultSurfaces.diffuseTexture
                    do! DefaultSurfaces.simpleLighting
                }


        show {
            display Display.Mono
            samples 8
            backend Backend.Vulkan
            debug true
            scene sg
        }
