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
            let offset = -10.0
            let step = 20.0 / float (s - 1)
            [|
                for y in 0 .. s-1 do
                    for x in 0 .. s-1 do
                        yield V3d(offset + float x * step, offset + float y * step, 0.0)
            |]

        let texcoords =
            let offset = 0.0
            let step = 1.0 / float (s - 1)
            [|
                for y in 0 .. s-1 do
                    for x in 0 .. s-1 do
                        yield V2d(offset + float x * step, offset + float y * step)
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
                    DefaultSemantic.DiffuseColorCoordinates, texcoords :> Array
                ]
        )


    [<ReflectedDefinition>]
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
                filter Filter.Anisotropic
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let heightScale = 5.0
        let pixelSize = 30.0

        let planeSize = 10.0
        let texelSize = 16.0

        [<AutoOpen>]
        module Tools =
            let dxTessCoord (c : V2d) =
                let inner = getInnerTessLevel 0
                let si = 1.0 / inner
                let tessLevel = 
                    if c.Y >= 1.0 - si then getOuterTessLevel 3
                    elif c.Y <= si then getOuterTessLevel 1
                    else inner

                V2d(1.0 / tessLevel, 0.0)
                
            let dyTessCoord (c : V2d) =
                let inner = getInnerTessLevel 1
                let si = 1.0 / inner
                let tessLevel = 
                    if c.X >= 1.0 - si then getOuterTessLevel 2
                    elif c.X <= si then getOuterTessLevel 0
                    else inner
            
                V2d(0.0, 1.0 / tessLevel)


            let interpolate2 (c : V2d) (p0 : V2d) (p1 : V2d) (p2 : V2d) (p3 : V2d) =
                let x = c.X //clamp 0.0 1.0 c.X
                let y = c.Y //clamp 0.0 1.0 c.Y
                let a = p0 * (1.0 - x) + p1 * x
                let b = p2 * (1.0 - x) + p3 * x
                a * (1.0 - y) + b * y

            let interpolate3 (c : V2d) (p0 : V3d) (p1 : V3d) (p2 : V3d) (p3 : V3d) =
                let x = c.X //clamp 0.0 1.0 c.X
                let y = c.Y //clamp 0.0 1.0 c.Y
                let a = p0 * (1.0 - x) + p1 * x
                let b = p2 * (1.0 - x) + p3 * x
                a * (1.0 - y) + b * y

            let interpolate4 (c : V2d) (p0 : V4d) (p1 : V4d) (p2 : V4d) (p3 : V4d) =
                let x = c.X //clamp 0.0 1.0 c.X
                let y = c.Y //clamp 0.0 1.0 c.Y
                let a = p0 * (1.0 - x) + p1 * x
                let b = p2 * (1.0 - x) + p3 * x
                a * (1.0 - y) + b * y



            let gradient2 (c : V2d) (p0 : V2d) (p1 : V2d) (p2 : V2d) (p3 : V2d) =
                let dcx = dxTessCoord c
                let dcy = dyTessCoord c

                let v00 = interpolate2 c p0 p1 p2 p3
                let vp0 = interpolate2 (c + 0.5 * dcx) p0 p1 p2 p3
                let vn0 = interpolate2 (c - 0.5 * dcx) p0 p1 p2 p3
                let v0p = interpolate2 (c + 0.5 * dcy) p0 p1 p2 p3
                let v0n = interpolate2 (c - 0.5 * dcy) p0 p1 p2 p3
                let dx = (vp0 - vn0) / 2.0
                let dy = (v0p - v0n) / 2.0

                v00, dx, dy

            let gradient3 (c : V2d) (p0 : V3d) (p1 : V3d) (p2 : V3d) (p3 : V3d) =
                let dcx = dxTessCoord c
                let dcy = dyTessCoord c

                let v00 = interpolate3 c p0 p1 p2 p3
                let vp0 = interpolate3 (c + 0.5 * dcx) p0 p1 p2 p3
                let vn0 = interpolate3 (c - 0.5 * dcx) p0 p1 p2 p3
                let v0p = interpolate3 (c + 0.5 * dcy) p0 p1 p2 p3
                let v0n = interpolate3 (c - 0.5 * dcy) p0 p1 p2 p3
                let dx = (vp0 - vn0) / 2.0
                let dy = (v0p - v0n) / 2.0

                v00, dx, dy

            let gradient4 (c : V2d) (p0 : V4d) (p1 : V4d) (p2 : V4d) (p3 : V4d) =
                let dcx = dxTessCoord c
                let dcy = dyTessCoord c

                let v00 = interpolate4 c p0 p1 p2 p3
                let vp0 = interpolate4 (c + 0.5 * dcx) p0 p1 p2 p3
                let vn0 = interpolate4 (c - 0.5 * dcx) p0 p1 p2 p3
                let v0p = interpolate4 (c + 0.5 * dcy) p0 p1 p2 p3
                let v0n = interpolate4 (c - 0.5 * dcy) p0 p1 p2 p3
                let dx = (vp0 - vn0) / 2.0 
                let dy = (v0p - v0n) / 2.0 

                v00, dx, dy



        [<ReflectedDefinition>]
        let sampleHeight (world : V4d)  =
            let off = 1.0 / V2d heightSampler.Size
            let tc = V2d(0.5, 0.5) + world.XY / planeSize

//            let c = tc * V2d heightSampler.Size
//            let c = V2d (V2i (c / pixelSize)) * pixelSize + V2d(pixelSize / 2.0, pixelSize / 2.0)
//            let tc = c / V2d heightSampler.Size

            let h = heightSampler.SampleLevel(tc, 0.0).X * 1.5
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
                let p0 = sampleHeight m.P2.wp
                let p1 = sampleHeight m.P0.wp
                let p2 = sampleHeight m.P1.wp
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


                let t0 = ll0 / texelSize 
                let t1 = ll1 / texelSize 
                let t2 = ll2 / texelSize 
                let t3 = ll3 / texelSize 

                let i0 = 0.5 * (t1 + t3)
                let i1 = 0.5 * (t0 + t2)

                //let l = 10.0
                //return { innerLevel = [| l; l |]; outerLevel = [| l; l; l; l |]} 
                return { innerLevel = [|i0; i1|]; outerLevel = [| t0; t1; t2; t3 |]}  
            }


        let plane2d (p0 : V2d) (p1 : V2d) =
            let d = p1 - p0 |> Vec.normalize
            let n = V2d(-d.Y, d.X)
            V3d(n.X, n.Y, -Vec.dot n p0)

        let sampleRegion (c : V2d) (p0 : V2d) (p1 : V2d) (p2 : V2d) (p3 : V2d) =
            let dcx = dxTessCoord c
            let dcy = dyTessCoord c

            let t00 = interpolate2 (c - 0.5 * dcx - 0.5 * dcy) p0 p1 p2 p3 
            let t01 = interpolate2 (c - 0.5 * dcx + 0.5 * dcy) p0 p1 p2 p3 
            let t10 = interpolate2 (c + 0.5 * dcx - 0.5 * dcy) p0 p1 p2 p3 
            let t11 = interpolate2 (c + 0.5 * dcx + 0.5 * dcy) p0 p1 p2 p3 


            let textureSize = V2d heightSampler.Size
            let p00 = t00 * textureSize + V2d(0.5, 0.5)
            let p01 = t01 * textureSize + V2d(0.5, 0.5)
            let p10 = t10 * textureSize + V2d(0.5, 0.5)
            let p11 = t11 * textureSize + V2d(0.5, 0.5)

            let min =
                V2d(
                    min (min p00.X p01.X) (min p10.X p11.X),
                    min (min p00.Y p01.Y) (min p10.Y p11.Y)
                )

            let max =
                V2d(
                    max (max p00.X p01.X) (max p10.X p11.X),
                    max (max p00.Y p01.Y) (max p10.Y p11.Y)
                )

            let sizeF = max - min
            let sx = (int (ceil sizeF.X)) |> clamp 1 16
            let sy = (int (ceil sizeF.X)) |> clamp 1 16
            let step = (max - min) / V2d(sx,sy)

            let plane0 = plane2d p00 p10
            let plane1 = plane2d p10 p11
            let plane2 = plane2d p11 p01
            let plane3 = plane2d p01 p00

            let mutable sum = 0.0
            let mutable cnt = 0
            
            let imin = V2i (min - V2d(0.5, 0.5))
            for x in -1 .. sx + 1 do
                for y in -1 .. sy + 1 do
                    let c = imin + V2i(x,y)
                    let cc = V3d(float c.X + 0.5, float c.Y + 0.5, 1.0)

                    let inside =
                        Vec.dot plane0 cc >= -0.5 &&
                        Vec.dot plane1 cc >= -0.5 &&
                        Vec.dot plane2 cc >= -0.5 &&
                        Vec.dot plane3 cc >= -0.5 

                    if inside then
                        sum <- sum + heightSampler.[c].X * 1.5
                        cnt <- cnt + 1

            if cnt = 0 then 0.0
            else sum / float cnt


        let pgmTessEval (m : Patch4<Vertex>) =
            tessEval {
                let c = m.TessCoord.XY
                //let dcx = dxTessCoord c
                //let dcy = dyTessCoord c
                //let h = sampleRegion c m.P0.tc m.P1.tc m.P2.tc m.P3.tc
                let wp = interpolate4 c m.P0.wp m.P1.wp m.P2.wp m.P3.wp
                let tc = interpolate2 c m.P0.tc m.P1.tc m.P2.tc m.P3.tc
                let h = heightSampler.SampleLevel(tc, 0.0).X * 1.5

                let wp = wp + V4d(0.0, 0.0, h, 0.0)




                return {
                    pos = uniform.ViewProjTrafo * wp
                    wp = wp
                    dir = V3d.OOI
                    n = V3d.OOI
                    tc = tc
                }
            }

        let pgmFragment (v : Vertex) =
            fragment {
                let tc = v.tc //0.5 * (v.tc + V2d.II)
                if tc.X > 1.0 || tc.Y > 1.0 || tc.X < 0.0 || tc.Y < 0.0 then
                    discard()

                return { v with n = sampleNormal v.wp }

            }




        let sampleHeight2 (world : V4d) (tc : V2d) =
            let h = heightSampler.SampleLevel(tc, 0.0).X * heightScale
            let wp = world + V4d(0.0, 0.0, h, 0.0)
            wp

        let isVisible (world : V4d) =
            let v = uniform.ViewProjTrafo * world
            let ss = v.XYZ / v.W
            ss.X >= -1.0 && ss.Y >= -1.0 && ss.X <= 1.0 && ss.Y <= 1.0

        type Coord = { value : V2d; offscreen : bool }


        let getMaxInDirection (dir : V3d) (min : V3d) (max : V3d) =
            V3d(
                (if dir.X > 0.0 then max.X else min.X),
                (if dir.Y > 0.0 then max.Y else min.Y),
                (if dir.Z > 0.0 then max.Z else min.Z)
            )

        let outsidePlane (p : V4d) (min : V3d) (max : V3d) =
            let n = p.XYZ
            let md = getMaxInDirection n min max
            let h = Vec.dot md n + p.W 
            h < 0.0

        let boxOutsideFrustum (min : V3d) (max : V3d) (frustum : M44d) =
            let r0 = frustum.R0
            let r1 = frustum.R1
            let r2 = frustum.R2
            let r3 = frustum.R3

            outsidePlane (r3 + r0) min max || 
            outsidePlane (r3 - r0) min max ||
            outsidePlane (r3 + r1) min max || 
            outsidePlane (r3 - r1) min max ||
            outsidePlane (r3 + r2) min max || 
            outsidePlane (r3 - r2) min max


             

        let project (world : V4d) =
            let visible = isVisible world || isVisible (world + V4d(0.0, 0.0, heightScale, 0.0))
            let v = uniform.ViewProjTrafo * world
            let ss = v.XYZ / v.W
            let c = V2d uniform.ViewportSize * (V2d(ss.X * 0.5 + 0.5, 0.5 - ss.Y * 0.5))
            c

        let bounds (p0 : V4d) (p1 : V4d) (p2 : V4d) (p3 : V4d) =
            let min = 
                V3d(
                    min (min p0.X p1.X) (min p2.X p3.X),
                    min (min p0.Y p1.Y) (min p2.Y p3.Y),
                    0.0
                )

            let max = 
                V3d(
                    max (max p0.X p1.X) (max p2.X p3.X),
                    max (max p0.Y p1.Y) (max p2.Y p3.Y),
                    heightScale
                )

            min, max

        let hTessControl (m : Patch4<Vertex>) =
            tessControl {
                let p0 = project m.P2.wp
                let p1 = project m.P0.wp
                let p2 = project m.P1.wp
                let p3 = project m.P3.wp

                let l0 = p1 - p0
                let l1 = p2 - p1
                let l2 = p3 - p2
                let l3 = p0 - p3

                let ll0 = l0.Length
                let ll1 = l1.Length
                let ll2 = l2.Length
                let ll3 = l3.Length


                let min, max = bounds m.P0.wp m.P1.wp m.P2.wp m.P3.wp
                let offscreen = boxOutsideFrustum min max uniform.ViewProjTrafo

                let t0 = if offscreen then 0.0 else ll0 / pixelSize |> clamp 1.0 128.0
                let t1 = if offscreen then 0.0 else ll1 / pixelSize |> clamp 1.0 128.0
                let t2 = if offscreen then 0.0 else ll2 / pixelSize |> clamp 1.0 128.0
                let t3 = if offscreen then 0.0 else ll3 / pixelSize |> clamp 1.0 128.0

                let avg = (t0 + t1 + t2 + t3) / 4.0

                let i0 = 0.5 * (t1 + t3)
                let i1 = 0.5 * (t0 + t2)

                //return { innerLevel = [|1.0; 1.0|]; outerLevel = [| 1.0; 1.0; 1.0; 1.0 |]} 
                return { innerLevel = [|i0; i1|]; outerLevel = [| t0; t1; t2; t3 |]}  
            }





        let hTessEval (m : Patch4<Vertex>) =
            tessEval {
                let c = m.TessCoord.XY


                let wp = interpolate4 c m.P0.wp m.P1.wp m.P2.wp m.P3.wp
                let tc, dtx, dty = gradient2 c m.P0.tc m.P1.tc m.P2.tc m.P3.tc

                let h = heightSampler.SampleGrad(tc, dtx, dty).X * heightScale
                let wp = wp + V4d(0.0, 0.0, h, 0.0)

                return {
                    Vertex.pos = uniform.ViewProjTrafo * wp
                    Vertex.wp = wp
                    Vertex.dir = V3d.OOI
                    Vertex.n = V3d.OOI
                    Vertex.tc = tc
                }
            }



        let hGeometry (m : Triangle<Vertex>) =
            triangle {
                let p0 = m.P0.wp
                let p1 = m.P1.wp
                let p2 = m.P2.wp
                let tc0 = m.P0.tc
                let tc1 = m.P1.tc
                let tc2 = m.P2.tc


                let u = 1.0 * (tc1 - tc0)
                let v = 1.0 * (tc2 - tc0)

                
                let h0 = heightSampler.SampleGrad(tc0, u, v).X * heightScale
                let h1 = heightSampler.SampleGrad(tc1, u, v).X * heightScale
                let h2 = heightSampler.SampleGrad(tc2, u, v).X * heightScale

                let wp0 = p0 + V4d(0.0, 0.0, h0, 0.0)
                let wp1 = p1 + V4d(0.0, 0.0, h1, 0.0)
                let wp2 = p2 + V4d(0.0, 0.0, h2, 0.0)
                yield { m.P0 with pos = uniform.ViewProjTrafo * wp0; wp = wp0 }
                yield { m.P1 with pos = uniform.ViewProjTrafo * wp1; wp = wp1 }
                yield { m.P2 with pos = uniform.ViewProjTrafo * wp2; wp = wp2 }
            }

        let hNormal (m : Vertex) =
            fragment {
                let dx = ddx m.tc
                let dy = ddy m.tc

                let off = 4.0

                let h = heightSampler.SampleGrad(m.tc, dx, dy).X
                let hx = heightSampler.SampleGrad(m.tc + off * dx, dx, dy).X
                let hy = heightSampler.SampleGrad(m.tc + off * dy, dx, dy).X

                let px = off * ddx m.wp.XY
                let py = off * ddy m.wp.XY
                let n = Vec.cross (V3d(px.X, px.Y, hx - h)) (V3d(py.X, py.Y, hy - h)) |> Vec.normalize


                return { m with n = n }
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

        
        tessGrid 128
            |> Sg.ofIndexedGeometry
            |> Sg.effect [ 
                Shader.pgmVertex |> toEffect
                //Shader.pgmHeight |> toEffect
                Shader.pgmTessControl |> toEffect
                Shader.pgmTessEval |> toEffect
                Shader.pgmFragment |> toEffect
                DefaultSurfaces.diffuseTexture |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect 
               ]
            |> Sg.fillMode mode
            |> Sg.uniform "HeightFieldTexture" tex
            |> Sg.diffuseTexture color


    [<Demo("Height")>]
    let run2() =
        let h = PixImage.Create @"C:\Aardwork\ps_height_1k.png"
        let tex = PixTexture2d(PixImageMipMap [|h|], { TextureParams.empty with wantMipMaps = true }) :> ITexture |> Mod.constant
        let color = FileTexture(@"C:\Aardwork\ps_texture_1k.png",  { TextureParams.empty with wantMipMaps = true }) :> ITexture |> Mod.constant
        
        let mode = Mod.init FillMode.Fill

        App.Keyboard.KeyDown(Keys.X).Values.Add(fun _ ->
            transact (fun () ->
                match mode.Value with
                    | FillMode.Fill -> mode.Value <- FillMode.Line
                    | _ -> mode.Value <- FillMode.Fill
            )
        )

        
        tessGrid 128
            |> Sg.ofIndexedGeometry
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect 
                Shader.hTessControl |> toEffect
                Shader.hTessEval |> toEffect
                //Shader.hGeometry |> toEffect
                DefaultSurfaces.constantColor C4f.White |> toEffect
                DefaultSurfaces.diffuseTexture |> toEffect
                //Shader.hNormal |> toEffect
                //DefaultSurfaces.simpleLighting |> toEffect 
               ]
            |> Sg.fillMode mode
            |> Sg.uniform "HeightFieldTexture" tex
            |> Sg.uniform "ViewportSize" App.Size
            |> Sg.diffuseTexture color



