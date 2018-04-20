﻿namespace Aardvark.Application


open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices
open System.Threading.Tasks

type RunningMean(maxCount : int) =
    let values = Array.zeroCreate maxCount
    let mutable index = 0
    let mutable count = 0
    let mutable sum = 0.0

    member x.Add(v : float) =
        let newSum = 
            if count < maxCount then 
                count <- count + 1
                sum + v
            else 
                sum + v - values.[index]

        sum <- newSum
        values.[index] <- v
        index <- (index + 1) % maxCount
              
    member x.Count = count

    member x.Average =
        if count = 0 then 0.0
        else sum / float count  

module Screenshot =


    let renderToImage (samples : int) (size : V2i) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature.Value

        let (_,color) = signature.ColorAttachments |> Map.find 0

        let depth = 
            match signature.DepthAttachment with
                | Some depth -> depth.format
                | None -> RenderbufferFormat.DepthComponent32

        //use lock = runtime.ContextLock
        let color = runtime.CreateRenderbuffer(size, color.format, samples)
        let depth = runtime.CreateRenderbuffer(size, depth, samples)
        use clear = runtime.CompileClear(signature, ~~C4f(0.0f,0.0f,0.0f,0.0f), ~~1.0)

        use fbo = 
            runtime.CreateFramebuffer(
                signature,
                Map.ofList [
                    DefaultSemantic.Colors, (color :> IFramebufferOutput)
                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
                ]
            ) 
        let desc = OutputDescription.ofFramebuffer fbo

        clear.Run(AdaptiveToken.Top, RenderToken.Empty, desc) |> ignore
        task.Run(AdaptiveToken.Top, RenderToken.Empty, desc) |> ignore


        let colorTexture = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1)
        runtime.ResolveMultisamples(color, colorTexture, ImageTrafo.Rot0)

        runtime.Download(colorTexture, PixFormat.ByteRGBA)

    let takeMS (samples : int) (target : IRenderTarget) =
        async {
            let img = renderToImage samples (Mod.force target.Sizes) target.RenderTask
            return img
        }

    let take (target : IRenderTarget) =
        async {
            let img = renderToImage target.Samples (Mod.force target.Sizes) target.RenderTask
            return img
        }


[<AbstractClass; Sealed; Extension>]
type RenderTargetExtensions private() =
    
    [<Extension>]
    static member Capture(this : IRenderTarget, samples : int) =
        Screenshot.renderToImage samples (Mod.force this.Sizes) this.RenderTask

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