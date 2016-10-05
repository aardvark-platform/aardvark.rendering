namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module PGM =
    
    let grid (s : int) =
        let s = max 2 (s + 1)
        let positions =
            let offset = -1.0
            let step = 2.0 / float (s - 1)
            [|
                for y in 0 .. s-1 do
                    for x in 0 .. s-1 do
                        yield V3d(offset + float x * step, offset + float y * step, 0.0)
            |]

        let indices =
            [|
                for y in 1 .. s-1 do
                    for x in 1 .. s-1 do
                        let p00 = s * (y - 1) + (x - 1)
                        let p10 = s * (y - 1) + (x + 0)
                        let p01 = s * (y + 0) + (x - 1)
                        let p11 = s * (y + 0) + (x - 0)


                        yield! [p00; p10; p11; p00; p11; p01]
            |]

        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = (indices :> Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, positions :> Array
                ]
        )

    let tessGrid (s : int) =
        let s = max 2 (s + 1)
        let positions =
            let offset = -1.0
            let step = 2.0 / float (s - 1)
            [|
                for y in 0 .. s-1 do
                    for x in 0 .. s-1 do
                        yield V3d(offset + float x * step, offset + float y * step, 0.0)
            |]

        let indices =
            [|
                for y in 1 .. s-1 do
                    for x in 1 .. s-1 do
                        let p00 = s * (y - 1) + (x - 1)
                        let p10 = s * (y - 1) + (x + 0)
                        let p01 = s * (y + 0) + (x - 1)
                        let p11 = s * (y + 0) + (x - 0)


                        yield! [p00; p10; p01; p11]
            |]

        IndexedGeometry(
            Mode = IndexedGeometryMode.QuadList,
            IndexArray = (indices :> Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, positions :> Array
                ]
        )


    module Shader =
        open FShade

        type RayDirectionAttribute() = inherit SemanticAttribute("RayDirection")

        type Vertex =
            {
                [<Position>]        pos : V4d
                [<WorldPosition>]   wp : V4d
                [<Normal>]          n : V3d
                [<TexCoord>]        tc : V2d
                [<RayDirection>]    dir : V3d
            }


        [<ReflectedDefinition>]
        let intersectGroundPlane (origin : V3d) (dir : V3d) =
            // origin.Z + t * dir.Z = 0

            if abs dir.Z < 0.01 then
                V3d(1000.0, 1000.0, 1000.0)
            else
                let t = -origin.Z / dir.Z
                if t >= 0.0 then
                    origin + t * dir
                else
                    V3d(1000.0, 1000.0, 1000.0)




        let pgmVertex (v : Vertex) =
            vertex {
                let size = 10.0
                let world = uniform.ViewProjTrafoInv * v.pos
                let near = world.XYZ / world.W
                let dir = near - uniform.CameraLocation |> Vec.normalize

                let w = intersectGroundPlane uniform.CameraLocation dir

                let tc = V2d(0.5, 0.5) + w.XY / size

                let valid = 
                    if tc.X < 0.0 || tc.Y < 0.0 || tc.X > 1.0 || tc.Y > 1.0 then false
                    else true


                return { 
                    pos = if valid then v.pos else V4d(v.pos.X, v.pos.Y, 10.0, 1.0)
                    n = V3d.OOI
                    wp = V4d(w, 1.0)
                    tc = V2d(0.5, 0.5) + w.XY / size
                    dir = dir
                }


            }

        let heightSampler =
            sampler2d {
                texture uniform?HeightFieldTexture
                filter Filter.MinMagLinear
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let planeSize = 10.0
        let heightScale = 1.0
        let pixelSize = 5.0

        [<ReflectedDefinition>]
        let sampleHeight (world : V4d) =
            let off = 1.0 / V2d heightSampler.Size
            let tc = V2d(0.5, 0.5) + world.XY / planeSize

//            let c = tc * V2d heightSampler.Size
//            let c = V2d (V2i (c / pixelSize)) * pixelSize + V2d(pixelSize / 2.0, pixelSize / 2.0)
//            let tc = c / V2d heightSampler.Size

            let h = heightSampler.SampleLevel(tc, 0.0).X * heightScale
            let wp = world + V4d(0.0, 0.0, h, 0.0)
            wp

        [<ReflectedDefinition>]
        let sampleNormal (world : V4d) =
            let off = 1.0 / V2d heightSampler.Size
            let tc = V2d(0.5, 0.5) + world.XY / planeSize
            let h = heightSampler.SampleLevel(tc, 0.0).X * heightScale
            let hx = heightSampler.SampleLevel(tc + V2d(off.X, 0.0), 0.0).X * heightScale
            let hy = heightSampler.SampleLevel(tc + V2d(0.0, off.Y), 0.0).X * heightScale

            let p = V3d(0.0,0.0, h)
            let px = V3d(off.X, 0.0, hx)
            let py = V3d(0.0, off.Y, hy)

            Vec.cross (px - p) (py - p) |> Vec.normalize


        let pgmHeight (v : Vertex) =
            vertex {
                let wp = sampleHeight v.wp
                let n = sampleNormal v.wp
                return 
                    { v with
                        wp = wp
                        n = n
                        pos = uniform.ViewProjTrafo * wp
                    }
            }

        let pgmTessControl (m : Patch4<Vertex>) =
            tessControl {
                let p0 = sampleHeight m.P0.wp
                let p1 = sampleHeight m.P1.wp
                let p2 = sampleHeight m.P2.wp
                let p3 = sampleHeight m.P3.wp

                let size = V2d heightSampler.Size
                let l0 = size * (p1.XY - p0.XY)
                let l1 = size * (p2.XY - p1.XY)
                let l2 = size * (p3.XY - p2.XY)
                let l3 = size * (p0.XY - p3.XY)


                let ll0 = l0.Length
                let ll1 = l1.Length
                let ll2 = l2.Length
                let ll3 = l3.Length


                let t0 = ll0 / pixelSize |> int |> float
                let t1 = ll1 / pixelSize |> int |> float
                let t2 = ll2 / pixelSize |> int |> float
                let t3 = ll3 / pixelSize |> int |> float

                let avg = (t0 + t1 + t2 + t3) / 4.0

                //return { innerLevel = [|1.0; 1.0|]; outerLevel = [| 1.0; 1.0; 1.0; 1.0 |]} 
                return { innerLevel = [|avg; avg|]; outerLevel = [| t0; t1; t2; t3 |]}  
            }

        let pgmTessEval (m : Patch4<Vertex>) =
            tessEval {
                let c = m.TessCoord.XY

                let p0 = m.P0.wp * (1.0 - c.X) + m.P1.wp * c.X
                let p1 = m.P2.wp * (1.0 - c.X) + m.P3.wp * c.X
                let wp = p0 * (1.0 - c.Y) + p1 * c.Y

                let wp = sampleHeight wp

                return {
                    pos = uniform.ViewProjTrafo * wp
                    wp = wp
                    dir = V3d.OOI
                    n = V3d.OOI
                    tc = V2d(0.5, 0.5) + wp.XY / planeSize
                }
            }

        let pgmFragment (v : Vertex) =
            fragment {
                let tc = v.tc //0.5 * (v.tc + V2d.II)
                if tc.X > 1.0 || tc.Y > 1.0 || tc.X < 0.0 || tc.Y < 0.0 then
                    discard()

                return { v with n = sampleNormal v.wp }

            }

    [<Demo("PGM")>]
    let run() =
        let h = PixImage.Create @"C:\Users\Schorsch\Desktop\ps_height_1k.png"
        let tex = PixTexture2d(PixImageMipMap [|h|], { TextureParams.empty with wantMipMaps = true }) :> ITexture |> Mod.constant
        let color = FileTexture(@"C:\Users\Schorsch\Desktop\ps_texture_1k.png",  { TextureParams.empty with wantMipMaps = true }) :> ITexture |> Mod.constant
        
        let mode = Mod.init FillMode.Fill

        App.Keyboard.KeyDown(Keys.X).Values.Add(fun _ ->
            transact (fun () ->
                match mode.Value with
                    | FillMode.Fill -> mode.Value <- FillMode.Line
                    | _ -> mode.Value <- FillMode.Fill
            )
        )

        
        tessGrid 64
            |> Sg.ofIndexedGeometry
            |> Sg.effect [ 
                Shader.pgmVertex |> toEffect
                Shader.pgmHeight |> toEffect
                Shader.pgmTessControl |> toEffect
                Shader.pgmTessEval |> toEffect
                Shader.pgmFragment |> toEffect
                DefaultSurfaces.diffuseTexture |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect 
               ]
            |> Sg.fillMode mode
            |> Sg.uniform "HeightFieldTexture" tex
            |> Sg.diffuseTexture color




