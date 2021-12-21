open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text

[<EntryPoint>]
let main argv =

    Aardvark.Init()

    // uncomment/comment to switch between the backends
    use app = new VulkanApplication(debug = true)
    //use app = new OpenGlApplication()
    let runtime = app.Runtime :> IRuntime
    runtime.ShaderCachePath <- None

    // create a game window (better for measuring fps)
    use win = app.CreateGameWindow(samples = 8)

    use sig1 =
        runtime.CreateFramebufferSignature([
            DefaultSemantic.Colors, TextureFormat.Rgba8
            DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
        ])

    use sig2 =
        runtime.CreateFramebufferSignature([
            DefaultSemantic.Colors, TextureFormat.Rgba8
            DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
        ])

    let effect =
        effect {
            do! DefaultSurfaces.trafo |> toEffect
            do! DefaultSurfaces.diffuseTexture |> toEffect
        }

    Log.warn "Preparing #1"
    let s1 = runtime.PrepareEffect(sig1, effect)
    Log.warn "Preparing #2"
    let s2 = runtime.PrepareEffect(sig2, effect)

    runtime.DeleteSurface(s1)
    runtime.DeleteSurface(s2)

    0
