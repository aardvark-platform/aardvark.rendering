open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

module Shaders =

    open FShade

    let cubeSampler =
        samplerCubeArray {
            texture uniform.DiffuseColorTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let cubeTexture (levels : int) (v : Effects.Vertex) =
        fragment {
            let dist = Vec.distance v.wp.XYZ uniform.CameraLocation
            let lod = float (levels - 1) * dist / 24.0
            return cubeSampler.SampleLevel(Vec.normalize v.wp.XYZ, 0, lod)
        }

module PixImage =

    let toTexture (wantMipMaps : bool) (img : #PixImage) =
        PixTexture2d(PixImageMipMap [| img :> PixImage |], wantMipMaps) :> ITexture

    let solid (size : V2i) (color : C4b) =
        let pi = PixImage<byte>(Col.Format.RGBA, size)
        pi.GetMatrix<C4b>().SetByCoord(fun _ -> color) |> ignore
        pi

    let checkerboard (color : C4b) =
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 16L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                color
        ) |> ignore
        pi

    let resized (size : V2i) (img : PixImage<byte>) =
        let result = PixImage<byte>(img.Format, size)

        for c in 0L .. img.Volume.Size.Z - 1L do
            let src = img.Volume.SubXYMatrixWindow(c)
            let dst = result.Volume.SubXYMatrixWindow(c)
            dst.SetScaledCubic(src)

        result

[<EntryPoint>]
let main argv = 
    
    Aardvark.Init()

   // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    use win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 8
        }

    let runtime = win.Runtime

    let colors = [|
            C4b.BurlyWood
            C4b.Crimson
            C4b.DarkOrange
            C4b.ForestGreen
            C4b.AliceBlue
            C4b.Indigo

            C4b.Cornsilk
            C4b.Cyan
            C4b.FireBrick
            C4b.Coral
            C4b.Chocolate
            C4b.LawnGreen
        |]

    let count = 2
    let levels = 4
    let size = V2i(1024)

    let data =
        Array.init count (fun index ->
            CubeMap.init levels (fun side level ->
                let data = PixImage.checkerboard colors.[index * 6 + int side]
                let size = size / (1 <<< level)
                data |> PixImage.resized size
            )
        )

    let format = TextureFormat.ofPixFormat data.[0].[CubeSide.PositiveX].PixFormat TextureParams.empty

    let cubeArray =
        runtime.CreateTextureCubeArray(size.X, format, levels = levels, count = count)

    data |> Array.iteri (fun index mipmaps ->
        mipmaps |> CubeMap.iteri (fun side level img ->
            runtime.Upload(cubeArray, level, index * 6 + int side, img)
        )
    )

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let sg =
        Sg.box (AVal.constant color) (AVal.constant box)
        |> Sg.diffuseTexture' cubeArray
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! Shaders.cubeTexture levels
        }

    // show the window
    win.Scene <- sg
    win.Run(preventDisposal = true)

    runtime.DeleteTexture(cubeArray)

    0
