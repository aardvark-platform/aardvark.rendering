namespace Rendering.Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg

module Shaders =
    open FShade

    type Vertex = { [<TexCoord>] tc : V2d; [<Position>] p : V4d; [<WorldPosition>] wp : V4d }

    let input =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipPoint
        }

//    let cube =
//        SamplerCube(uniform?CubeMap, { SamplerState.Empty with Filter = Some Filter.MinMagLinear })

    // for a given filterSize and sigma calculate the weights CPU-side
    let filterSize = 15
    let sigma = 6.0

    let halfFilterSize = filterSize / 2
    let weights =
        let res = 
            Array.init filterSize (fun i ->
                let x = abs (i - halfFilterSize)
                exp (-float (x*x) / (2.0 * sigma * sigma))
            )

        // normalize the weights
        let sum = Array.sum res
        res |> Array.map (fun v -> v / sum)


    let gaussX (v : Vertex) =
        fragment {
            let mutable color = V4d.Zero
            let off = V2d(1.0 / float uniform.ViewportSize.X, 0.0)
            for x in -halfFilterSize..halfFilterSize do
                let w = weights.[x+halfFilterSize]
                color <- color + w * input.Sample(v.tc + (float x) * off)

            return V4d(color.XYZ, 1.0)
        }

    let gaussY (v : Vertex) =
        fragment {
            let mutable color = V4d.Zero
            let off = V2d(0.0, 1.0 / float uniform.ViewportSize.Y)
            for y in -halfFilterSize..halfFilterSize do
                let w = weights.[y+halfFilterSize]
                color <- color + w * input.Sample(v.tc + (float y) * off)

            return V4d(color.XYZ, 1.0)
        }


    let envMap = SamplerCube(ShaderTextureHandle("EnvironmentMap", uniform), { SamplerState.Empty with Filter = Some Filter.MinMagLinear; AddressU = Some WrapMode.Clamp; AddressV = Some WrapMode.Clamp; AddressW = Some WrapMode.Clamp })


 
    let environmentMap (v : Vertex) =
        fragment {
            let worldPos = uniform.ViewProjTrafoInv * V4d(2.0 * v.tc.X - 1.0, 1.0 - 2.0 * v.tc.Y, 0.5, 1.0)
            let worldDir = (worldPos.XYZ / worldPos.W) - uniform.CameraLocation
            let coord = Vec.normalize worldDir
            let c2 = V3d(coord.X, coord.Z, coord.Y)
            let tv = envMap.Sample(c2)

            return V4d(tv.XYZ, 1.0) //V4d(0.5 * (coord + V3d.III) + tv.XYZ, 1.0) //V4d((envMap.Sample(Vec.normalize worldDir).XYZ + V3d.III) * 0.5, 1.0)

        }


module HelloWorld =


    let run () =

        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
   
        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let quadSg =
            let quad =
                let index = [|0;1;2; 0;2;3|]
                let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]

                IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array], SymDict.empty)

            quad |> Sg.ofIndexedGeometry

        let sg =
            quadSg |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor C4f.White |> toEffect
                  ]
               |> Sg.viewTrafo (viewTrafo   |> Mod.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()
        0

    let envTest() =

        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
   
        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let faceFiles = 
            [|
                CubeSide.PositiveX, "forest_positive_x.jpg"
                CubeSide.NegativeX, "forest_negative_x.jpg"
                CubeSide.PositiveY, "forest_positive_y.jpg"
                CubeSide.NegativeY, "forest_negative_y.jpg"
                CubeSide.PositiveZ, "forest_positive_z.jpg"
                CubeSide.NegativeZ, "forest_negative_z.jpg"
            |]

        let envCube = 
            PixImageCube(
                faceFiles |> Array.map (fun (face, file) -> 
                    let path = Path.combine [ @"E:\Development\WorkDirectory\DataSVN"; file ]
                    let img = PixImage.Create path
                    PixImageMipMap img
                )
            )

        let fullscreenQuad =
            Sg.draw IndexedGeometryMode.TriangleStrip
                |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant [|V3f(-1.0,-1.0,0.0); V3f(1.0,-1.0,0.0); V3f(-1.0,1.0,0.0);V3f(1.0,1.0,0.0) |])
                |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant [|V2f.OO; V2f.IO; V2f.OI; V2f.II|])
                |> Sg.uniform "ViewportSize" win.Sizes

        let envSg =
            fullscreenQuad
                |> Sg.effect [toEffect Shaders.environmentMap]
                |> Sg.texture (Symbol.Create "EnvironmentMap") (PixTextureCube(envCube, true) :> ITexture |> Mod.constant)
                |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo)

        let task = app.Runtime.CompileRender(win.FramebufferSignature, envSg)

        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()
        0