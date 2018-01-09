namespace Examples


open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO


module Jpeg =

    let run() =
        //use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication(true)

        
        use app = new Aardvark.Application.WinForms.OpenGlApplication(true)

        Aardvark.Rendering.GL.Config.CheckErrors <- true
        let runtime = app.Runtime

        use comp = new JpegCompressor(runtime)

        let outputPath = @"C:\Users\steinlechner\Desktop\test.jpg"
        let diffPath = @"C:\Users\steinlechner\Desktop\diff.png"

        // load the input
        let input = PixImage.Create @"vs.png"
        let tex = runtime.PrepareTexture(PixTexture2d(PixImageMipMap [| input |], TextureParams.empty))


        // compress the image
        use comp = comp.NewInstance(input.Size, Quantization.photoshop80)
        let data = comp.Compress(tex.[TextureAspect.Color, 0, 0])
        File.WriteAllBytes(outputPath, data)
        
//        for i in 1 .. 10 do
//            comp.Compress(tex) |> ignore
//            comp.Encode(tex).Run()
//            comp.Download() |> ignore
//
//        let sw = System.Diagnostics.Stopwatch.StartNew()
//        let mutable sum = 0uy
//        for i in 1 .. 1000 do
//            comp.Encode(tex).Run() |> ignore
//        sw.Stop()
//        Log.line "encode took: %A" (sw.MicroTime / 1000)
//        
//        let sw = System.Diagnostics.Stopwatch.StartNew()
//        let mutable sum = 0uy
//        for i in 1 .. 1000 do
//            comp.Download() |> ignore
//        sw.Stop()
//        Log.line "download took: %A" (sw.MicroTime / 1000)
//
//        let sw = System.Diagnostics.Stopwatch.StartNew()
//        let mutable sum = 0uy
//        for i in 1 .. 1000 do
//            comp.Compress(tex) |> ignore
//        sw.Stop()
//        Log.line "compress took: %A" (sw.MicroTime / 1000)

        // diff with input
        let result = PixImage.Create outputPath
        let input = input.ToPixImage<byte>(Col.Format.RGBA).GetMatrix<C4b>()
        let result = result.ToPixImage<byte>(Col.Format.RGBA).GetMatrix<C4b>()
        let outputImage = PixImage<byte>(Col.Format.RGBA, input.Size)
        let output = outputImage.GetMatrix<C4b>()
        output.SetMap2(input, result, fun (ic : C4b) (rc : C4b) ->
            let i = ic.ToC3f().ToV4f()
            let r = rc.ToC3f().ToV4f()

            let d = (i - r).NormMax
            if d > 0.03f then
                HSVf((d - 0.03f) * 10.0f, 1.0f, 1.0f).ToC3f().ToC4b()
            else
                C4f(ic.ToC3f().ToV3f() * 0.5f).ToC4b()

        ) |> ignore
        outputImage.SaveAsImage diffPath


        runtime.DeleteTexture tex