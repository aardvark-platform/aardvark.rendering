open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Rendering.Text

// This example illustrates and tests new pipeline states.

module Semantic =
    let Color0 = Sym.ofString "Color0"
    let Color1 = Sym.ofString "Color1"
    let Color2 = Sym.ofString "Color2"
    let Color3 = Sym.ofString "Color3"
    let All = Set.ofList [Color0; Color1; Color2; Color3 ]

    let Color0Texture = Sym.ofString "Color0Texture"
    let Color1Texture = Sym.ofString "Color1Texture"
    let Color2Texture = Sym.ofString "Color2Texture"
    let Color3Texture = Sym.ofString "Color3Texture"

module Shaders =
    open FShade

    type Fragment = {
        [<Color>] color : V4d
        [<FragCoord>] coord : V4d
        [<SampleId>] sample : int
    }

    let quadrupleOutput (f : Fragment) =
        fragment {
            return {| Color0 = f.color
                      Color1 = f.color
                      Color2 = f.color
                      Color3 = f.color |}
        }

    let private color0Sampler =
        sampler2dMS {
            texture uniform?Color0Texture
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let private color1Sampler =
        sampler2dMS {
            texture uniform?Color1Texture
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let private color2Sampler =
        sampler2dMS {
            texture uniform?Color2Texture
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let private color3Sampler =
        sampler2dMS {
            texture uniform?Color3Texture
            filter Filter.MinMagPoint
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
        }

    let blit (f : Fragment) =
        fragment {
            let coord = V2i f.coord.XY
            let center = uniform.ViewportSize / 2

            if coord.X < center.X then
                if coord.Y < center.Y then
                    return color0Sampler.Read(coord, f.sample)
                else
                    return color2Sampler.Read(V2i(coord.X, coord.Y - center.Y), f.sample)
            else
                if coord.Y < center.Y then
                    return color1Sampler.Read(V2i(coord.X - center.X, coord.Y), f.sample)
                else
                    return color3Sampler.Read(V2i(coord.X - center.X, coord.Y - center.Y), f.sample)
        }

[<EntryPoint>]
let main argv =

    Aardvark.Init()

    // uncomment/comment to switch between the backends
    use app = new VulkanApplication(debug = true)
    //use app = new OpenGlApplication()
    let runtime = app.Runtime :> IRuntime

    // create a game window (better for measuring fps)
    use win = app.CreateGameWindow(samples = 8)

    let colorMasks = [|
        ColorMask.All
        ColorMask.None
        ColorMask.Red
        ColorMask.Green
        ColorMask.Blue
        ColorMask.Red ||| ColorMask.Green
        ColorMask.Red ||| ColorMask.Blue
        ColorMask.Blue ||| ColorMask.Green
    |]

    let colorMaskIndices =
        AVal.init <| Map.ofList [
            Semantic.Color0, 0
            Semantic.Color1, 2
            Semantic.Color2, 3
            Semantic.Color3, 4
        ]

    let multisample =
        AVal.init true

    let conservativeRaster =
        AVal.init false

    win.Keyboard.KeyDown(Keys.V).Values.Add(
        let rng = RandomSystem()

        fun _ ->
            transact (fun () ->
                colorMaskIndices.Value <-
                    colorMaskIndices.Value
                    |> Map.map (fun _ _ ->
                        rng.UniformInt colorMasks.Length
                    )
            )
    )

    win.Keyboard.KeyDown(Keys.B).Values.Add(
        fun _ -> transact (fun _ ->
            multisample.Value <- not multisample.Value
            Log.line "Multisample: %A" multisample.Value
        )
    )

    win.Keyboard.KeyDown(Keys.N).Values.Add(
        fun _ -> transact (fun _ ->
            conservativeRaster.Value <- not conservativeRaster.Value
            Log.line "Conservative raster: %A" conservativeRaster.Value
        )
    )

    let quadFront, quadBack =

        let drawCall =
            DrawCallInfo(
                FaceVertexCount = 4,
                InstanceCount = 1
            )

        let posFront = [| V3f(-0.4,-0.4,0.0); V3f(0.4,-0.4,0.0); V3f(-0.4,0.4,0.0); V3f(0.4,0.4,0.0) |]
        let posBack  = [| V3f(-0.4,-0.4,0.0); V3f(-0.4,0.4,0.0); V3f(0.4,-0.4,0.0); V3f(0.4,0.4,0.0) |]

        drawCall
        |> Sg.render IndexedGeometryMode.TriangleStrip
        |> Sg.vertexAttribute DefaultSemantic.Positions (~~posFront),

        drawCall
        |> Sg.render IndexedGeometryMode.TriangleStrip
        |> Sg.vertexAttribute DefaultSemantic.Positions (~~posBack)

    let quadsSg =
        let pass0 = RenderPass.main
        let pass1 = RenderPass.after "maskPass" RenderPassOrder.Arbitrary pass0

        let trafoFront, trafoBack =
            let startTime = System.DateTime.Now

            win.Time |> AVal.map (fun t ->
                let t = (t - startTime).TotalSeconds
                Trafo3d.RotationZ (0.5 * t)
            ),

            win.Time |> AVal.map (fun t ->
                let t = (t - startTime).TotalSeconds
                Trafo3d.RotationZ (-0.5 * t)
            )

        let sg =
            [
                quadFront |> Sg.trafo trafoFront
                quadBack  |> Sg.trafo trafoBack
            ]
            |> Sg.ofList
            |> Sg.depthTest' DepthTest.None
            |> Sg.multisample multisample
            |> Sg.conservativeRaster conservativeRaster
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.White
                do! Shaders.quadrupleOutput
            }

        let maskSg =
            sg
            |> Sg.pass pass0
            |> Sg.colorOutput' Set.empty
            |> Sg.stencilModeFront' { StencilMode.None with Pass = StencilOperation.IncrementWrap }
            |> Sg.stencilModeBack' { StencilMode.None with Pass = StencilOperation.DecrementWrap }

        let mainSg =
            sg
            |> Sg.pass pass1
            |> Sg.stencilMode' { StencilMode.None with Comparison = ComparisonFunction.Equal }

        [ maskSg; mainSg ] |> Sg.ofList

    let signature =
        runtime.CreateFramebufferSignature(8, [
            Semantic.Color0, TextureFormat.Rgba8
            Semantic.Color1, TextureFormat.Rgba8
            Semantic.Color2, TextureFormat.Rgba8
            Semantic.Color3, TextureFormat.Rgba8
            DefaultSemantic.Stencil, TextureFormat.StencilIndex8
        ])

    use task =
        quadsSg
        |> Sg.colorMasks (colorMaskIndices |> AVal.map (Map.map (fun _ i -> colorMasks.[i])))
        |> Sg.compile runtime signature

    let output =
        task |> RenderTask.renderSemantics Semantic.All (win.Sizes |> AVal.map (fun s -> s / 2))

    use finalTask =
        let sg =
            Sg.fullScreenQuad
            |> Sg.texture Semantic.Color0Texture output.[Semantic.Color0]
            |> Sg.texture Semantic.Color1Texture output.[Semantic.Color1]
            |> Sg.texture Semantic.Color2Texture output.[Semantic.Color2]
            |> Sg.texture Semantic.Color3Texture output.[Semantic.Color3]
            |> Sg.depthTest' DepthTest.None
            |> Sg.shader {
                do! Shaders.blit
            }

        RenderTask.ofList [
            runtime.CompileClear(win.FramebufferSignature, C4f.DarkSlateGray)
            runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    // show the window
    win.RenderTask <- finalTask
    win.Run()

    runtime.DeleteFramebufferSignature(signature)

    0
