open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System.Threading
open Offler
open System

module Shader =
    open FShade

    let diffuseSampler =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.Anisotropic
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let diffuseTexture (v : Effects.Vertex) =
        fragment {
            let level = diffuseSampler.QueryLod v.tc
            let texColor = diffuseSampler.Sample(v.tc)
            return texColor//V4d(V3d((level.Y + 1.0) / 4.0), 1.0)
        }

[<EntryPoint>]
let main argv = 
    
    Offler.Init()
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

    let foci = List.init 6 (fun _ -> cval false)
    let focus = List.existsA (fun a -> a :> aval<_>) foci
    

    let mutable last = CameraView.lookAt (V3d(2,-4,2)) V3d.Zero V3d.OOI
    let camera = 
        focus |> AVal.bind (fun focus ->
            if focus then 
                AVal.constant last
            else
                last
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                |> AVal.map (fun a -> last <- a; a)
        )


    let sw = System.Diagnostics.Stopwatch.StartNew()
        
    let time =
        focus |> AVal.bind (function
            | true -> 
                if sw.IsRunning then sw.Stop()
                AVal.constant sw.MicroTime
            | false -> 
                if not sw.IsRunning then sw.Start()
                win.Time |> AVal.map (fun _ -> sw.MicroTime)
        )

    let anim = time |> AVal.map (fun t -> Trafo3d.RotationZ(0.5 * t.TotalSeconds))

    let urls =
        [
            "https://amazon.com", Trafo3d.Scale(-1.0, 1.0, 1.0) * Trafo3d.Translation(V3d.OOI) * Trafo3d.RotationX(-Constant.PiHalf)
            "https://youtube.com", Trafo3d.Scale(-1.0, 1.0, 1.0) * Trafo3d.Translation(V3d.OOI) * Trafo3d.RotationX(-Constant.PiHalf) * Trafo3d.RotationZ(-Constant.PiHalf)

            "https://de.wikipedia.org", Trafo3d.Scale(-1.0, 1.0, 1.0) * Trafo3d.Translation(V3d.OOI) * Trafo3d.RotationX(-Constant.PiHalf) * Trafo3d.RotationZ(Constant.PiHalf)
            "https://google.com", Trafo3d.Scale(-1.0, 1.0, 1.0) * Trafo3d.Translation(V3d.OOI) * Trafo3d.RotationX(-Constant.PiHalf) * Trafo3d.RotationZ(Constant.Pi)

            "https://stackoverflow.com", Trafo3d.Translation(V3d.OOI)
            "https://xkcd.com", Trafo3d.Translation(-V3d.OOI)

            //"https://", Trafo3d.Scale(-1.0, 1.0, 1.0) * Trafo3d.Translation(V3d.OOI) * Trafo3d.RotationX(-Constant.PiHalf) * Trafo3d.RotationZ(-Constant.PiHalf)
            //"https://news.orf.at"
            //"https://news.orf.at"
            //"https://news.orf.at"
            //"https://news.orf.at"
        ]



    let sg =
        Sg.ofList [
            for (url, trafo), focus in List.zip urls foci do
                Sg.browser {
                    Browser.url url
                    Browser.keyboard win.Keyboard
                    Browser.mouse win.Mouse
                    Browser.focus focus
                    Browser.mipMaps true
                    Browser.size (1024,1024)
                }
                |> Sg.transform trafo
        ]
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! Shader.diffuseTexture

        }
        |> Sg.trafo anim
        |> Sg.viewTrafo (AVal.map CameraView.viewTrafo camera)


  
    win.Scene <- sg
    win.Run()

    0
