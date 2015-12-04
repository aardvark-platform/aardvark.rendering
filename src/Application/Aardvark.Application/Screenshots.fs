namespace Aardvark.Application


open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices
open System.Threading.Tasks

module Screenshot =


    let renderToImage (samples : int) (size : V2i) (task : IRenderTask) =
        let runtime = task.Runtime.Value

        let signature = task.FramebufferSignature

        let (_,color) = signature.ColorAttachments |> Map.find 0

        let depth = 
            match signature.DepthStencilAttachment with
                | Some depth -> depth.format
                | None -> RenderbufferFormat.DepthComponent32

        //use lock = runtime.ContextLock
        let color = runtime.CreateRenderbuffer(size, color.format, samples)
        let depth = runtime.CreateRenderbuffer(size, depth, samples)
        use clear = runtime.CompileClear(task.FramebufferSignature, ~~C4f(0.0f,0.0f,0.0f,0.0f), ~~1.0)

        use fbo = 
            runtime.CreateFramebuffer(
                task.FramebufferSignature,
                Map.ofList [
                    DefaultSemantic.Colors, (color :> IFramebufferOutput)
                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
                ]
            )

        clear.Run(null, fbo) |> ignore
        task.Run(null, fbo) |> ignore


        let colorTexture = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)
        runtime.ResolveMultisamples(color, colorTexture, ImageTrafo.MirrorY)

        runtime.Download(colorTexture, PixFormat.ByteBGRA)

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