open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

module Shader =
    open FShade 

    type Vertex =
        {
            [<Position>] pos    : V4d
            [<TexCoord; Interpolation(InterpolationMode.Sample)>] tc     : V2d
            [<SamplePosition>] s     : V2d
        }

    type UniformScope with
        member x.Iterations : int = x?Mandelbrot?Iterations
        member x.Scale : float = x?Mandelbrot?Scale
        member x.Center : V2d = x?Mandelbrot?Center

    let transfer =
        sampler2d {
            texture uniform?TransferFunction
            filter Filter.MinMagLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }


    let mandelbrot (v : Vertex) =
        fragment {
            let scale = uniform.Scale
            let center = uniform.Center
            let size = uniform.ViewportSize
            let aspect = float size.X / float size.Y
            let iter = uniform.Iterations

            let c = V2d(aspect * (2.0 * v.tc.X - 1.0), (2.0 * v.tc.Y - 1.0)) * scale - center

            let mutable cont = true
            let mutable z = c
            let mutable i = 0
            while i < iter && cont do
                let x = (z.X * z.X - z.Y * z.Y) + c.X
                let y = (z.Y * z.X + z.X * z.Y) + c.Y
                
                if (x * x + y * y) > 4.0 then
                    cont <- false
                else
                    z <- V2d(x,y)
                    i <- i + 1

            let coord = if i = iter then 0.0 else float i / 100.0
            let color = transfer.SampleLevel(V2d(coord, 0.0), 0.0)

            return color


        }


[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()


    let window =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }

    let texture = 
        let path = Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "data"; "transfer.png"]
        FileTexture(path, TextureParams.empty) :> ITexture

    let scale =
        let sw = System.Diagnostics.Stopwatch()
        window.Time |> AVal.map (fun _ -> 
            if not sw.IsRunning then sw.Start()

            exp (-0.15 * (sw.Elapsed.TotalSeconds % 40.0))
        )

    let center = V2d(0.743643887037158704752191506114774, -0.131825904205311970493132056385139) //* 0.5 + V2d(0.5, 0.5)

    let sg = 
        Sg.fullScreenQuad
            |> Sg.uniform "Iterations" (AVal.constant 1000)
            |> Sg.uniform "Scale" scale //(AVal.constant 2.2)
            |> Sg.uniform "Center" (AVal.constant <| center)
            |> Sg.uniform "TransferFunction" (AVal.constant texture)

            |> Sg.shader {
                do! Shader.mandelbrot
            }
    
    window.Scene <- sg

    // show the scene in a simple window
    window.Run()
    0
