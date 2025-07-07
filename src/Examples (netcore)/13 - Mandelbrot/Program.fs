open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

module Shader =
    open FShade 

    type Vertex =
        {
            [<Position>] pos    : V4f
            [<TexCoord; Interpolation(InterpolationMode.Sample)>] tc     : V2f
            [<SamplePosition>] s     : V2f
        }

    type UniformScope with
        member x.Iterations : int = x?Mandelbrot?Iterations
        member x.Scale : float32 = x?Mandelbrot?Scale
        member x.Center : V2f = x?Mandelbrot?Center

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
            let aspect = float32 size.X / float32 size.Y
            let iter = uniform.Iterations

            let c = V2f(aspect * (2.0f * v.tc.X - 1.0f), (2.0f * v.tc.Y - 1.0f)) * scale - center

            let mutable cont = true
            let mutable z = c
            let mutable i = 0
            while i < iter && cont do
                let x = (z.X * z.X - z.Y * z.Y) + c.X
                let y = (z.Y * z.X + z.X * z.Y) + c.Y
                
                if (x * x + y * y) > 4.0f then
                    cont <- false
                else
                    z <- V2f(x,y)
                    i <- i + 1

            let coord = if i = iter then 0.0f else float32 i / 100.0f
            let color = transfer.SampleLevel(V2f(coord, 0.0f), 0.0f)

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
