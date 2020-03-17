open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open Aardvark.Application

module Shader =
    open FShade

    let texCoordColor(v : Effects.Vertex) =
        fragment {
            return V4d(v.tc, 1.0, 1.0)
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

    let pass0 = RenderPass.main
    let pass1 = RenderPass.after "pass1" RenderPassOrder.Arbitrary pass0
    let pass2 = RenderPass.after "pass2" RenderPassOrder.Arbitrary pass1

    let box =
        Sg.box' C4b.Red Box3d.Unit
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.simpleLighting
        }
        //|> Sg.writeBuffers (Some (Set.singleton DefaultSemantic.Colors))
        |> Sg.pass pass1


    let someText =
        Sg.text FontSquirrel.Hack.Regular C4b.Black (AVal.constant "Hi There!")
        |> Sg.transform (Trafo3d.RotationX Constant.PiHalf)
        |> Sg.depthTest (AVal.constant DepthTestMode.None)
        |> Sg.pass pass2

    let scene =
        Sg.fullScreenQuad
        |> Sg.shader {
            do! Shader.texCoordColor
        }
        |> Sg.pass pass0
        |> Sg.writeBuffers (Some (Set.singleton DefaultSemantic.Colors))
        |> Sg.andAlso someText
        |> Sg.andAlso box


    // show the window
    win.Scene <- scene
    win.Run()

    0
