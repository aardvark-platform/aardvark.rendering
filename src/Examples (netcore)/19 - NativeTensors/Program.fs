open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application


// This example demonstrates the use of NativeVolume with image data.
// NativeTensors (Vector/Matrix/Volume/Tensor4) are quite similar to 
// the managed tensors from Aardvark.Base but operate on native memory
// allowing their implementations to be significantly faster.
//
// For interopability the API provides functions (using) which temporarily
// pin managed tensors as native tensors.
//
// Note that not all convenience functions (such as view-tensors [e.g. volume.GetMatrix<C3b>()]) 
// are implemented yet but nonetheless the library is quite powerful.


// Creates Images of an optical illusion that tricks the mind into
// seeing more different colors (4) than are actually present in the image (3).
let howManyColorsIllusion (size : int) =

    let scale = 1024.0 / float size
    let delta = 0.5 * float (size - 1)

    let pixImage = PixImage<byte>(Col.Format.RGB, V2i(size, size))
    let colorMatrix = pixImage.GetMatrix<C3b>()
    
    let orange      = C3b(255, 150, 0)
    let magenta     = C3b(255, 0, 255)
    let bluegreen   = C3b(0, 255, 150)

    let pixelFun (x : int64) (y : int64) =
        let xd = scale * (float x - delta)
        let yd = scale * (float y - delta)
        let r = sqrt (xd * xd + yd * yd)
        let phi = atan2 yd xd
        let lp1 = phi / Constant.PiTimesFour
        let lp2 = phi / Constant.Pi
        let lr = log r / Constant.E
        let p1 = Fun.Frac (0.05 + 4.0 * (lr - lp1))
        let p2 = Fun.Frac (96.0 * (lr + lp2))
        if p2 < 0.5 then
            if p1 >= 0.0 && p1 < 0.25 then bluegreen
            else orange
        else
            if p1 >= 0.5 && p1 < 0.75 then bluegreen
            else magenta

    colorMatrix.SetByCoord pixelFun |> ignore
    pixImage

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    // let's create an input image containing a simple illusion
    let inputImage = 
        howManyColorsIllusion 1331

    // since we'd like to process the image, we create an output image
    // with a different size
    let resultImage =
        PixImage<byte>(Col.Format.RGB, V2i(1024, 768))
    

    // first we need to pin both images in order to use them as NativeVolumes
    NativeVolume.using inputImage.Volume (fun vInput ->
        NativeVolume.using resultImage.Volume (fun vOutput ->

            // blit the input-image to the output
            NativeVolume.blit vInput vOutput

            // set a rectangular region to black
            vOutput.[10 .. 100, 10 .. 100, *] <- 0uy
            
            // set a rectangular region to red
            vOutput.[924 .. , 668 .., 0] <- 255uy
            vOutput.[924 .. , 668 .., 1..] <- 0uy

            // replicate a mirrored region
            NativeVolume.copy 
                (vOutput.[462 .. 562, 334 .. 434, *].MirrorY())
                (vOutput.[101 .. 201, 101 .. 201, *])

            // blit the center-region to a different one with half size
            NativeVolume.blit 
                (vOutput.[462 .. 562, 334 .. 434, *].MirrorY())
                (vOutput.[101 .. 150, 101 .. 150, *])

            // set a region to a circular gradient
            vOutput.[300 .. 400, 300 .. 400, *].SetByCoord (fun (c : V3d) ->
                let d = Vec.length (c.XY - V2d.Half) * 2.0
                Fun.Lerp(d / Constant.Sqrt2, 0uy, 255uy)
            )

        
        )
    )

    // finally we can create a texture using our modified image and show it
    let texture =
        PixTexture2d(
            PixImageMipMap [| resultImage :> PixImage |],
            TextureParams.empty
        )
        
    // show the scene in a simple window
    show {
        backend Backend.Vulkan
        display Display.Mono
        debug false
        samples 8
        scene (
            Sg.fullScreenQuad
                |> Sg.diffuseTexture (AVal.constant (texture :> ITexture))
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
        )
    }

    0
