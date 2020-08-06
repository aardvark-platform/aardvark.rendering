open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

module Shader = 
    open FShade
    open Aardvark.Rendering.Effects

    let private colorSampler =
        sampler2d {
            texture uniform?Colors
            filter Filter.MinMagLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let private posSampler =
        sampler2d {
            texture uniform?WPos
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    type Fragment = { [<Color>] color: V4d;  [<Semantic("WPos")>] wpos : V4d }

    let pass0 (v : Vertex) =
        fragment {
            return { color = v.c; wpos = v.wp }
        }

    let composite (v : Vertex) =
        fragment {
            let mode : int = uniform?Mode
            if mode = 0 then
                return colorSampler.Sample(v.tc)
            elif mode = 1 then
                return posSampler.SampleLevel(v.tc,0.0)
            else return V4d.IOOI
        }

[<EntryPoint>]
let main argv = 
    
    Aardvark.Init()
    
    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }

    // define a dynamic transformation depending on the window's time
    // This time is a special value that can be used for animations which
    // will be evaluated when rendering the scene
    let dynamicTrafo =
        let startTime = System.DateTime.Now
        win.Time |> AVal.map (fun t ->
            let t = (t - startTime).TotalSeconds
            Trafo3d.RotationZ (0.5 * t)
        )

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red
    let size = V2i(512,512) |> AVal.constant

    let signature = 
        win.Runtime.CreateFramebufferSignature(
            [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                Sym.ofString "WPos"  , RenderbufferFormat.Rgba32f
                DefaultSemantic.Depth,  RenderbufferFormat.Depth24Stencil8
            ])

    let pass0 = 
        // create a red box with a simple shader
        Sg.box (AVal.constant color) (AVal.constant box)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! Shader.pass0
            }

            // apply the dynamic transformation to the box
            |> Sg.trafo dynamicTrafo
            |> Sg.viewTrafo (win.View |> AVal.map (Array.item 0))
            |> Sg.projTrafo (win.Proj |> AVal.map (Array.item 0))
            |> Sg.compile win.Runtime signature
            |> RenderTask.renderSemantics (
                    Set.ofList [
                        DefaultSemantic.Depth
                        DefaultSemantic.Colors
                        Sym.ofString "WPos"]
               ) size

    let mode = AVal.init 0

    win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ -> 
        transact (fun _ -> mode.Value <- (mode.Value + 1) % 2)
        printfn "%A" mode.Value
    )
    
    let finalComposite = 
        Sg.fullScreenQuad
        |> Sg.shader {
             do! Shader.composite
          }
        |> Sg.uniform "Mode" mode
        |> Sg.texture (Sym.ofString "Colors") (Map.find DefaultSemantic.Colors pass0)
        |> Sg.texture (Sym.ofString "WPos") (Map.find (Sym.ofString "WPos") pass0)

    win.Scene <- finalComposite
    win.Run()

    0
