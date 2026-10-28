namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Expecto

module TextureClear =

    module private Cases =

        let private doubleInputs =
            let channels =
                [| float Int32.MinValue; float (Int32.MinValue + 1); -16777219.0; -16777217.0
                   -2.0000001; -2.0; Math.BitIncrement(-2.0)
                   Math.BitDecrement(-1.0); -1.0; Math.BitIncrement(-1.0); -0.25; -0.0
                   0.0; 0.25; Math.BitDecrement(1.0); 1.0; Math.BitIncrement(1.0)
                   Math.BitDecrement(2.0); 2.0; Math.BitIncrement(2.0)
                   16777215.0; 16777216.0; 16777217.0; 16777219.0; float Int32.MaxValue |]
            [ for i in 0 .. channels.Length - 1 do
                yield V4d(channels.[i], channels.[(i + 5) % channels.Length], channels.[(i + 11) % channels.Length], channels.[(i + 17) % channels.Length])
              for channel in 0 .. 3 do
                for value in channels do
                    let mutable v = V4d(3.75, -4.75, 5.25, 6.5)
                    v.[channel] <- value
                    yield v ]

        let private checkColor context (integer : V4i) (floating : V4f) (actual : ClearColor) =
            Expect.equal actual.Integer integer (context + ": integer channels")
            for channel in 0 .. 3 do
                Expect.equal (BitConverter.SingleToInt32Bits actual.Float.[channel]) (BitConverter.SingleToInt32Bits floating.[channel]) $"{context}: float channel={channel}"

        let doublePrecision (_ : IRuntime) =
            for value in doubleInputs do
                let rgb = C3d(value.X, value.Y, value.Z)
                checkColor $"C3d={rgb}" (V4i(V4d rgb)) (V4f rgb) (ClearColor rgb)
            for value in doubleInputs do
                let rgba = C4d(value.X, value.Y, value.Z, value.W)
                checkColor $"C4d={rgba}" (V4i value) (V4f rgba) (ClearColor rgba)

        let inline private delegated context (value : ^T) integer floating =
            let semantic = Sym.ofString "DoubleColor"
            checkColor (context + ": conversion helper") integer floating (ClearValues.Conversion.toColor value)
            let routes =
                [ "ofColor", ClearValues.ofColor value
                  "color helper", ClearValues.empty |> ClearValues.color value
                  "attachment helper", ClearValues.empty |> ClearValues.colorAttachment semantic value
                  "colors helper", ClearValues.empty |> ClearValues.colors (Map.ofList [semantic, value])
                  "default builder", clear { color value }
                  "attachment builder", clear { color semantic value }
                  "map builder", clear { colors (Map.ofList [semantic, value]) }
                  "sequence builder", clear { colors [semantic, value] } ]
            for route, values in routes do
                let label = $"{context}, route={route}"
                let actual = ClearValues.tryGetColor semantic values
                Expect.isSome actual (label + ": effective color")
                checkColor label integer floating actual.Value
                Expect.isNone values.Depth (label + ": no depth")
                Expect.isNone values.Stencil (label + ": no stencil")

        let doubleDelegation (_ : IRuntime) =
            for value in doubleInputs do
                let rgb = C3d(value.X, value.Y, value.Z)
                let rgba = C4d(value.X, value.Y, value.Z, value.W)
                let i3, f3 = V4i(V4d rgb), V4f rgb
                let i4, f4 = V4i value, V4f rgba
                checkColor $"C3d implicit={rgb}" i3 f3 (ClearColor.op_Implicit rgb)
                checkColor $"C4d implicit={rgba}" i4 f4 (ClearColor.op_Implicit rgba)
                delegated $"C3d={rgb}" rgb i3 f3
                delegated $"C4d={rgba}" rgba i4 f4

        let constructorControls (_ : IRuntime) =
            for value in [-17.75; -1.0; -0.0; 0.0; 0.25; 0.5; 1.0; 17.75] do
                let rgb, rgba = C3d(value), C4d(value, -value, 0.5, 0.25)
                checkColor $"ordinary C3d={rgb}" (V4i(V4f rgb)) (V4f rgb) (ClearColor rgb)
                checkColor $"ordinary C4d={rgba}" (V4i(V4f rgba)) (V4f rgba) (ClearColor rgba)
            let d2, d3, d4 = V2d(16777217.0, -1.99999999), V3d(16777219.0, -16777217.0, 0.99999999), V4d(3.75, -4.75, 16777217.0, 2147483647.0)
            let f2, f3, f4 = V2f(2.75f, -3.75f), V3f(2.75f, -3.75f, 4.5f), V4f(2.75f, -3.75f, 4.5f, 0.5f)
            let c3, c4 = C3f(2.75f, -3.75f, 4.5f), C4f(2.75f, -3.75f, 4.5f, 0.5f)
            let b3, b4 = C3b(17uy, 31uy, 63uy), C4b(17uy, 31uy, 63uy, 127uy)
            let us3, us4 = C3us(17us, 1023us, 65535us), C4us(17us, 1023us, 65535us, 123us)
            let ui3, ui4 = C3ui(17u, 31u, UInt32.MaxValue), C4ui(17u, 31u, UInt32.MaxValue, 127u)
            let i2, i3, i4 = V2i(-1, 2), V3i(-1, 2, 3), V4i(-1, 2, 3, Int32.MaxValue)
            let u2, u3, u4 = V2ui(1u, 2u), V3ui(1u, 2u, 3u), V4ui(1u, 2u, 3u, UInt32.MaxValue)
            let controls =
                [ "V2d", ClearColor d2, V4i d2, V4f d2
                  "V3d", ClearColor d3, V4i d3, V4f d3
                  "V4d", ClearColor d4, V4i d4, V4f d4
                  "V2f", ClearColor f2, V4i f2, V4f f2
                  "V3f", ClearColor f3, V4i f3, V4f f3
                  "V4f", ClearColor f4, V4i f4, V4f f4
                  "C3f", ClearColor c3, V4i(V4f c3), V4f c3
                  "C4f", ClearColor c4, V4i(V4f c4), V4f c4
                  "C3b", ClearColor b3, V4i b3, V4f(C4f b3)
                  "C4b", ClearColor b4, V4i b4, V4f(C4f b4)
                  "C3us", ClearColor us3, V4i us3, V4f(C4f us3)
                  "C4us", ClearColor us4, V4i us4, V4f(C4f us4)
                  "C3ui", ClearColor ui3, V4i(17, 31, -1, -1), V4f(C4f ui3)
                  "C4ui", ClearColor ui4, V4i(17, 31, -1, 127), V4f(C4f ui4)
                  "V2i", ClearColor i2, V4i i2, V4f i2
                  "V3i", ClearColor i3, V4i i3, V4f i3
                  "V4i", ClearColor i4, i4, V4f i4
                  "V2ui", ClearColor u2, V4i u2, V4f u2
                  "V3ui", ClearColor u3, V4i u3, V4f u3
                  "V4ui", ClearColor u4, V4i u4, V4f u4 ]
            for label, actual, integer, floating in controls do
                checkColor label integer floating actual
                checkColor (label + ": copy constructor") integer floating (ClearColor actual)

        let private gpuDoubleInputs =
            [ V4d(0.99999999, -0.99999999, 16777217.0, -16777217.0)
              V4d(1.99999999, -1.99999999, float Int32.MaxValue, float (Int32.MinValue + 1))
              V4d(2.00000001, -2.00000001, 123.75, -123.75)
              V4d(16777219.0, -16777219.0, 0.25, float Int32.MaxValue) ]

        let private checkImage context (integer : V4i) (image : PixImage) =
            try image.AsPixImage<int>() |> PixImage.isColor (integer.ToArray())
            with error -> raise (Exception(context + ": integer readback", error))

        let inline private createAndClearColor (runtime : IRuntime) (format : TextureFormat) (color : ^T) =
            let t = runtime.CreateTexture2D(V2i(256), format)

            try
                runtime.Clear(t, color)
                t.Download()
            finally
                runtime.DeleteTexture(t)

        let doubleTexture (runtime : IRuntime) =
            for value in gpuDoubleInputs do
                let rgb = C3d(value.X, value.Y, value.Z)
                let rgba = C4d(value.X, value.Y, value.Z, value.W)
                createAndClearColor runtime TextureFormat.Rgba32i rgb |> checkImage $"texture C3d={rgb}" (V4i(V4d rgb))
                createAndClearColor runtime TextureFormat.Rgba32i rgba |> checkImage $"texture C4d={rgba}" (V4i value)

        let rgba8 (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba8 <| C3f(0.5)
            data.AsPixImage<uint8>() |> PixImage.isColorWithDist 1L (C4b(127uy).ToArray())

        let rgba32i (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32i <| V4i(-1)
            data.AsPixImage<int>() |> PixImage.isColor (V4i(-1).ToArray())

        let rgba32ui (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32ui <| V4i(-1)
            data.AsPixImage<uint>() |> PixImage.isColor [| UInt32.MaxValue; UInt32.MaxValue; UInt32.MaxValue; UInt32.MaxValue |]

        let rgba16ui (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba16ui <| V4i(-1)
            data.AsPixImage<uint16>() |> PixImage.isColor [| UInt16.MaxValue; UInt16.MaxValue; UInt16.MaxValue; UInt16.MaxValue |]

        let rgba32f (runtime : IRuntime) =
            let data = createAndClearColor runtime TextureFormat.Rgba32f <| V4i(-1)
            data.AsPixImage<float32>() |> PixImage.isColor (V4f(-1).ToArray())

        let private clearDepthStencilInternal (format : TextureFormat)
                                              (initialDepth : float) (initialStencil : int)
                                              (clearDepth : Option<float>) (clearStencil : Option<int>)
                                              (runtime : IRuntime) =
            let t = runtime.CreateTexture2D(V2i(256), format)

            try
                runtime.ClearDepthStencil(t, initialDepth, initialStencil)
                clearDepth |> Option.iter (fun d -> runtime.ClearDepth(t, d))
                clearStencil |> Option.iter (fun s -> runtime.ClearStencil(t, s))

                if format.HasDepth then
                    let depth = t.DownloadDepth()
                    let test = clearDepth |> Option.defaultValue initialDepth
                    depth.Data |> Array.iter (fun x -> Expect.floatClose Accuracy.medium (float x) test "Depth data mismatch")

                if format.HasStencil then
                    let stencil = t.DownloadStencil()
                    let test = clearStencil |> Option.defaultValue initialStencil
                    stencil.Data |> Array.iter (fun x -> Expect.equal x test "Stencil data mismatch")
            finally
                runtime.DeleteTexture(t)

        let depth                   = clearDepthStencilInternal TextureFormat.DepthComponent32f 0.5 3 None None
        let depthStencil            = clearDepthStencilInternal TextureFormat.Depth24Stencil8 0.5 3 None None
        let depthStencilOnlyDepth   = clearDepthStencilInternal TextureFormat.Depth24Stencil8 1.0 5 (Some 0.5) None
        let depthStencilOnlyStencil = clearDepthStencilInternal TextureFormat.Depth24Stencil8 1.0 5 None (Some 3)

        let private createAndTestFramebuffer (runtime : IRuntime) (formats : Map<Symbol, TextureFormat>)
                                             (test : IFramebuffer -> Map<Symbol, IBackendTexture> -> unit) =
            let signature =
                runtime.CreateFramebufferSignature(formats)

            let textures =
                formats |> Map.map (fun _ fmt -> runtime.CreateTexture2D(V2i(256), fmt))

            let fbo =
                let attachments = textures |> Map.map (fun _ tex -> tex.GetOutputView())
                runtime.CreateFramebuffer(signature, attachments)

            try
                test fbo textures
            finally
                fbo.Dispose()
                signature.Dispose()
                textures |> Map.iter (fun _ t -> runtime.DeleteTexture t)

        let private clearFramebufferDouble (clearFramebuffer : IRuntime -> IFramebuffer -> ClearValues -> unit) (runtime : IRuntime) =
            let named = Sym.ofString "DoubleColor"
            let formats = Map.ofList [DefaultSemantic.Colors, TextureFormat.Rgba32i; named, TextureFormat.Rgba32i]
            createAndTestFramebuffer runtime formats (fun fbo textures ->
                for value in gpuDoubleInputs do
                    let rgb = C3d(value.Z, value.X, value.Y)
                    let rgba = C4d(value.X, value.Y, value.Z, value.W)
                    let values = clear { color rgb; color named rgba }
                    clearFramebuffer runtime fbo values
                    textures.[DefaultSemantic.Colors].Download() |> checkImage $"framebuffer C3d={rgb}" (V4i(V4d rgb))
                    textures.[named].Download() |> checkImage $"framebuffer C4d={rgba}" (V4i value)
            )

        let private clearFramebufferMixed (clearFramebuffer : IRuntime -> IFramebuffer -> ClearValues -> unit) (runtime : IRuntime) =
            let c0 = Sym.ofString "c0"
            let c1 = Sym.ofString "c1"
            let c2 = Sym.ofString "c2"
            let c3 = Sym.ofString "c3"
            let c4 = Sym.ofString "c4"

            let formats =
                Map.ofList [
                    c0, TextureFormat.Rgba32f
                    c1, TextureFormat.Rgba32i
                    c2, TextureFormat.Rgba32ui
                    c3, TextureFormat.Rgba8
                    c4, TextureFormat.Rgba16i
                    DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                ]

            createAndTestFramebuffer runtime formats (fun fbo textures ->
                let clear =
                    clear {
                        color C3b.AliceBlue
                        color c0 C3us.Aquamarine
                        color c1 (V4i(-1))
                        color c3 (C3f(0.5))
                        depth 0.5
                        stencil 4
                    }

                clearFramebuffer runtime fbo clear

                do
                    let c = C3us.Aquamarine |> C4f
                    let pi = textures.[c0].Download().AsPixImage<float32>()
                    pi |> PixImage.isColor (c.ToArray())

                do
                    let pi = textures.[c1].Download().AsPixImage<int>()
                    pi |> PixImage.isColor (V4i(-1).ToArray())

                do
                    let c = C3b.AliceBlue |> V4i
                    let pi = textures.[c2].Download().AsPixImage<uint>()
                    pi |> PixImage.isColor (c.ToArray() |> Array.map uint32)

                do
                    let c = C4b(127uy)
                    let pi = textures.[c3].Download().AsPixImage<uint8>()
                    pi |> PixImage.isColorWithDist 1L (c.ToArray())

                do
                    let c = C3b.AliceBlue |> V4i
                    let pi = textures.[c4].Download().AsPixImage<int16>()
                    pi |> PixImage.isColor (c.ToArray() |> Array.map int16)

                do
                    let depth = textures.[DefaultSemantic.DepthStencil].DownloadDepth()
                    depth.Data |> Array.iter (fun x -> Expect.floatClose Accuracy.medium (float x) 0.5 "Depth data mismatch")

                do
                    let stencil = textures.[DefaultSemantic.DepthStencil].DownloadStencil()
                    stencil.Data |> Array.iter (fun x -> Expect.equal x 4 "Stencil data mismatch")
            )


        let private clearFramebufferDepthStencilInternal (format : TextureFormat)
                                                         (initialDepth : float) (initialStencil : int)
                                                         (clearDepth : Option<float>) (clearStencil : Option<int>)
                                                         (clearFramebuffer : IRuntime -> IFramebuffer -> ClearValues -> unit)
                                                         (runtime : IRuntime) =
            let formats =
                Map.ofList [
                    DefaultSemantic.DepthStencil, format
                ]

            createAndTestFramebuffer runtime formats (fun fbo textures ->
                let clearValues =
                    clear { depth initialDepth; stencil initialStencil }

                clearFramebuffer runtime fbo clearValues

                let clearValues =
                    { ClearValues.empty with
                        Depth   = clearDepth |> Option.map float32
                        Stencil = clearStencil |> Option.map uint32 }

                clearFramebuffer runtime fbo clearValues

                do if format.HasDepth then
                    let depth = textures.[DefaultSemantic.DepthStencil].DownloadDepth()
                    let test = clearDepth |> Option.defaultValue initialDepth
                    depth.Data |> Array.iter (fun x -> Expect.floatClose Accuracy.medium (float x) test "Depth data mismatch")

                do if format.HasStencil then
                    let stencil = textures.[DefaultSemantic.DepthStencil].DownloadStencil()
                    let test = clearStencil |> Option.defaultValue initialStencil
                    stencil.Data |> Array.iter (fun x -> Expect.equal x test "Stencil data mismatch")
            )

        let clearFramebufferDepth                   = clearFramebufferDepthStencilInternal TextureFormat.DepthComponent32f 0.5 3 None None
        let clearFramebufferDepthStencil            = clearFramebufferDepthStencilInternal TextureFormat.Depth24Stencil8 0.5 3 None None
        let clearFramebufferDepthStencilOnlyDepth   = clearFramebufferDepthStencilInternal TextureFormat.Depth24Stencil8 1.0 5 (Some 0.5) None
        let clearFramebufferDepthStencilOnlyStencil = clearFramebufferDepthStencilInternal TextureFormat.Depth24Stencil8 1.0 5 None (Some 3)


        let private framebufferDirectClear (test : (IRuntime -> IFramebuffer -> ClearValues -> unit) -> IRuntime -> unit) =
            test (fun runtime fbo clear -> runtime.Clear(fbo, clear))

        let private framebufferCompileClear (test : (IRuntime -> IFramebuffer -> ClearValues -> unit) -> IRuntime -> unit) =
            test (fun runtime fbo clear ->
                use task = runtime.CompileClear(fbo.Signature, clear)
                task.Run(RenderToken.Empty, fbo)
            )

        let private framebufferRenderCommand (test : (IRuntime -> IFramebuffer -> ClearValues -> unit) -> IRuntime  -> unit) =
            test (fun runtime fbo clear ->
                use task =
                    Sg.execute (RenderCommand.Clear clear)
                    |> Sg.compile runtime fbo.Signature

                task.Run(RenderToken.Empty, fbo)
            )

        let doubleFramebuffer                               = framebufferDirectClear clearFramebufferDouble
        let doubleFramebufferCompiled                       = framebufferCompileClear clearFramebufferDouble

        let framebufferMixed                                = framebufferDirectClear clearFramebufferMixed
        let framebufferMixedCompileClear                    = framebufferCompileClear clearFramebufferMixed
        let framebufferMixedRenderCommand                   = framebufferRenderCommand clearFramebufferMixed

        let framebufferDepth                                = framebufferDirectClear clearFramebufferDepth
        let framebufferDepthCompileClear                    = framebufferCompileClear clearFramebufferDepth
        let framebufferDepthRenderCommand                   = framebufferRenderCommand clearFramebufferDepth

        let framebufferDepthStencil                         = framebufferDirectClear clearFramebufferDepthStencil
        let framebufferDepthStencilCompileClear             = framebufferCompileClear clearFramebufferDepthStencil
        let framebufferDepthStencilRenderCommand            = framebufferRenderCommand clearFramebufferDepthStencil

        let framebufferDepthStencilOnlyDepth                = framebufferDirectClear clearFramebufferDepthStencilOnlyDepth
        let framebufferDepthStencilOnlyDepthCompileClear    = framebufferCompileClear clearFramebufferDepthStencilOnlyDepth
        let framebufferDepthStencilOnlyDepthRenderCommand   = framebufferRenderCommand clearFramebufferDepthStencilOnlyDepth

        let framebufferDepthStencilOnlyStencil              = framebufferDirectClear clearFramebufferDepthStencilOnlyStencil
        let framebufferDepthStencilOnlyStencilCompileClear  = framebufferCompileClear clearFramebufferDepthStencilOnlyStencil
        let framebufferDepthStencilOnlyStencilRenderCommand = framebufferRenderCommand clearFramebufferDepthStencilOnlyStencil

    let tests (target: TestTarget) =
        [
          if target = TestTarget.Cpu then
            "Double color integer precision",               Cases.doublePrecision
            "Double color conversion delegation",           Cases.doubleDelegation
            "Ordinary and other clear color constructors",  Cases.constructorControls

          if target.IsGpu then
            "Double color texture precision",               Cases.doubleTexture
            "Double color framebuffer precision",           Cases.doubleFramebuffer
            "Double color compiled framebuffer precision",  Cases.doubleFramebufferCompiled

            "Color rgba8",                              Cases.rgba8
            "Color rgba32i",                            Cases.rgba32i
            "Color rgba16ui",                           Cases.rgba16ui
            "Color rgba32ui",                           Cases.rgba32ui
            "Color rgba32f",                            Cases.rgba32f
            "Depth",                                    Cases.depth
            "Depth-stencil",                            Cases.depthStencil
            "Depth-only",                               Cases.depthStencilOnlyDepth
            "Stencil-only",                             Cases.depthStencilOnlyStencil

            "Framebuffer",                              Cases.framebufferMixed
            "Framebuffer compiled",                     Cases.framebufferMixedCompileClear
            "Framebuffer render command",               Cases.framebufferMixedRenderCommand

            "Framebuffer depth",                        Cases.framebufferDepth
            "Framebuffer depth compiled",               Cases.framebufferDepthCompileClear
            "Framebuffer depth render command",         Cases.framebufferDepthRenderCommand

            "Framebuffer depth-stencil",                Cases.framebufferDepthStencil
            "Framebuffer depth-stencil compiled",       Cases.framebufferDepthStencilCompileClear
            "Framebuffer depth-stencil render command", Cases.framebufferDepthStencilRenderCommand

            "Framebuffer depth-only",                   Cases.framebufferDepthStencilOnlyDepth
            "Framebuffer depth-only, compiled",         Cases.framebufferDepthStencilOnlyDepthCompileClear
            "Framebuffer depth-only, render command",   Cases.framebufferDepthStencilOnlyDepthRenderCommand

            "Framebuffer stencil-only",                 Cases.framebufferDepthStencilOnlyStencil
            "Framebuffer stencil-only, compiled",       Cases.framebufferDepthStencilOnlyStencilCompileClear
            "Framebuffer stencil-only, render command", Cases.framebufferDepthStencilOnlyStencilRenderCommand
        ]
        |> prepareCases "Clear" target