open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application


module Shader =
    open FShade

    type UniformScope with
        member x.Alpha : float32 = uniform?Alpha

    let alpha (v : Effects.Vertex) =
        fragment {
            let c = v.c.XYZ
            return V4f(c, uniform.Alpha)
        }

[<EntryPoint>]
let main argv =
    // first we need to initialize Aardvark's core components

    Aardvark.Init()

    let win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            samples 4
        }

    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let sbox = Box3d(-0.3*V3d.III, 0.3*V3d.III)
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red


    let alpha = cval 0.5

    let sw = System.Diagnostics.Stopwatch.StartNew()
    let off = win.Time |> AVal.map (fun _ -> Trafo3d.Translation(V3d.IOO * 3.0 * sin (0.6 * sw.Elapsed.TotalSeconds)))

    let rainbow =
        let img = PixImage<byte>(Col.Format.RGBA, 256, 256)
        img.GetMatrix<C4b>().SetByCoord (fun (c : V2l) ->
            HSVf(float32 c.X / 256.0f, 1.0f, 0.5f).ToC3f().ToC4b()
        ) |> ignore
        PixTexture2d(img, TextureParams.WantMipMaps) :> ITexture 
    
    let radialRings =
        let img = PixImage<byte>(Col.Format.RGBA, 256, 256)
        img.GetMatrix<C4b>().SetByCoord (fun (c : V2l) ->
            let o = c - V2l(128, 128)
            C4f(sqr (sin (float32 o.Length / 32.0f)), 0.0f, 0.0f, 1.0f).ToC4b()
        ) |> ignore
        PixTexture2d(img, TextureParams.WantMipMaps) :> ITexture 
    
    let sg =
        // thankfully aardvark defines a primitive box
        Sg.ofList [
            Sg.box (AVal.constant color) (AVal.constant box)
            |> Sg.diffuseTexture DefaultTextures.checkerboard

            Sg.sphere 5 (AVal.constant color) (AVal.constant 0.7)
            |> Sg.diffuseTexture' rainbow

            Sg.cone' 16 C4b.Red 0.1 0.2
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Yellow
                do! Shader.alpha
            }

            Sg.box (AVal.constant color) (AVal.constant sbox)
            |> Sg.trafo off
            |> Sg.diffuseTexture' radialRings
        ]
        |> Sg.uniform "Alpha" alpha
        |> Sg.transparent
        // apply a shader ...
        // * transforming all vertices
        // * looking up the DiffuseTexture
        // * applying a simple lighting to the geometry (headlight)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.diffuseTexture
            do! Shader.alpha
        }


    win.Keyboard.DownWithRepeats.Values.Add(fun k ->
        match k with
        | Keys.Up -> transact (fun () -> alpha.Value <- min 1.0 (alpha.Value + 0.1))
        | Keys.Down -> transact (fun () -> alpha.Value <- max 0.0 (alpha.Value - 0.1))
        | _ -> ()
    )

    // show the scene in a simple window
    win.Scene <- sg
    win.Run()

    0
