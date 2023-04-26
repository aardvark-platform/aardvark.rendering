﻿namespace Aardvark.Application

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Runtime.CompilerServices
open System.Threading.Tasks

module Screenshot =


    let renderToImage (samples : int) (size : V2i) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature.Value

        let (_, color) = signature.ColorAttachments |> Map.toArray |> Array.head

        let depth =
            signature.DepthStencilAttachment |> Option.defaultValue TextureFormat.DepthComponent32

        //use lock = runtime.ContextLock
        let color = runtime.CreateRenderbuffer(size, color.Format, samples)
        let depth = runtime.CreateRenderbuffer(size, depth, samples)
        use clear = runtime.CompileClear(signature, ~~C4f(0.0f,0.0f,0.0f,0.0f), ~~1.0)

        use fbo = 
            runtime.CreateFramebuffer(
                signature,
                Map.ofList [
                    DefaultSemantic.Colors, (color :> IFramebufferOutput)
                    DefaultSemantic.DepthStencil, (depth :> IFramebufferOutput)
                ]
            ) 
        let desc = OutputDescription.ofFramebuffer fbo

        clear.Run(AdaptiveToken.Top, RenderToken.Empty, desc) |> ignore
        task.Run(AdaptiveToken.Top, RenderToken.Empty, desc) |> ignore


        let colorTexture = runtime.CreateTexture2D(size, TextureFormat.Rgba8, 1, 1)
        runtime.ResolveMultisamples(color, colorTexture)

        runtime.Download(colorTexture, PixFormat.ByteRGBA)

    let takeMS (samples : int) (target : IRenderTarget) =
        async {
            let img = renderToImage samples (AVal.force target.Sizes) target.RenderTask
            return img
        }

    let take (target : IRenderTarget) =
        async {
            let img = renderToImage target.Samples (AVal.force target.Sizes) target.RenderTask
            return img
        }


[<AbstractClass; Sealed; Extension>]
type RenderTargetExtensions private() =
    
    [<Extension>]
    static member Capture(this : IRenderTarget, samples : int) =
        Screenshot.renderToImage samples (AVal.force this.Sizes) this.RenderTask

    [<Extension>]
    static member Capture(this : IRenderTarget) =
        RenderTargetExtensions.Capture(this, this.Samples)

    [<Extension>]
    static member CaptureAsync(this : IRenderTarget, samples : int) =
        Task.Factory.StartNew (fun () ->
            RenderTargetExtensions.Capture(this, samples)
        )

    [<Extension>]
    static member CaptureAsync(this : IRenderTarget) =
        RenderTargetExtensions.CaptureAsync(this, this.Samples)

    [<Extension>]
    static member InstallCapture(this : IRenderControl, k : Keys) =
        this.Keyboard.KeyDown(k).Values.Add(fun () ->
            let take =
                async {
                    do! Async.SwitchToThreadPool()

                    Log.line "saving screenshot"
                    let! shot = RenderTargetExtensions.CaptureAsync(this) |> Async.AwaitTask
                    Aardvark.Rendering.Screenshot.SaveAndUpload(shot, true)
                }

            Async.Start take
        )