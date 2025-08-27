open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

// This example illustrates how to render to a mipmapped cubemap

module Shaders =

    open FShade

    let cubeSampler =
        samplerCube {
            texture uniform.DiffuseColorTexture
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let cubeTexture (levels : int) (v : Effects.Vertex) =
        fragment {
            let dist = Vec.distance v.wp.XYZ uniform.CameraLocation
            let lod = float32 (levels - 1) * dist / 24.0f
            return cubeSampler.SampleLevel(Vec.normalize v.n, lod)
        }


[<EntryPoint>]
let main argv =

    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    use win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug true
            samples 8
        }

    let runtime = win.Runtime

    use signature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, TextureFormat.Rgba8
        ]

    let size = AVal.init 1024
    let levels = 4

    let cubeColors =
        let colors =
            [| C4f.Red; C4f.Green; C4f.Blue; C4f.Yellow; C4f.Magenta; C4f.Cyan |]

        CubeMap.init levels (fun face level ->
            colors.[(int face + level) % colors.Length]
        )

    let tasks =
        CubeMap.init levels (fun face level ->
            Sg.quad
            |> Sg.shader {
                do! DefaultSurfaces.constantColor cubeColors.[face, level]
            }
            |> Sg.compile runtime signature
        )

    let cubemap =
        tasks |> RenderTask.renderToColorCubeMip size

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let sg =
        Sg.box (AVal.constant color) (AVal.constant box)
        |> Sg.diffuseTexture cubemap
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! Shaders.cubeTexture levels
        }

    // show the window
    win.Scene <- sg
    win.Run()

    tasks |> CubeMap.iter Disposable.dispose

    0
