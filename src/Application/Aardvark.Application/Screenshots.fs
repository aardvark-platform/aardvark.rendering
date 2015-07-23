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

        //use lock = runtime.ContextLock
        use color = runtime.CreateRenderbuffer(~~size, ~~RenderbufferFormat.Rgba8, ~~samples)
        use depth = runtime.CreateRenderbuffer(~~size, ~~RenderbufferFormat.DepthComponent32, ~~samples)
        use clear = runtime.CompileClear(~~C4f.Black, ~~1.0)

        use fbo = 
            runtime.CreateFramebuffer(
                Map.ofList [
                    DefaultSemantic.Colors, ~~(color :> IFramebufferOutput)
                    DefaultSemantic.Depth, ~~(depth :> IFramebufferOutput)
                ]
            )

        clear.Run(fbo) |> ignore
        task.Run(fbo) |> ignore


        use colorTexture = runtime.CreateTexture(~~size, ~~PixFormat.ByteRGBA, ~~1, ~~1)
        runtime.ResolveMultisamples(color, colorTexture, ImageTrafo.MirrorY)

        let images = colorTexture.Download(0)
        images.[0] 

    let takeMS (samples : int) (target : IRenderTarget) =
        async {
            let img = renderToImage samples (Mod.force target.Sizes) target.RenderTask
            return img
        }

    let take (target : IRenderTarget) =
        async {
            let img = renderToImage (Mod.force target.Samples) (Mod.force target.Sizes) target.RenderTask
            return img
        }


[<AbstractClass; Sealed; Extension>]
type RenderTargetExtensions private() =
    
    [<Extension>]
    static member Capture(this : IRenderTarget, samples : int) =
        Screenshot.renderToImage samples (Mod.force this.Sizes) this.RenderTask

    [<Extension>]
    static member Capture(this : IRenderTarget) =
        RenderTargetExtensions.Capture(this, Mod.force this.Samples)

    [<Extension>]
    static member CaptureAsync(this : IRenderTarget, samples : int) =
        Task.Factory.StartNew (fun () ->
            RenderTargetExtensions.Capture(this, samples)
        )

    [<Extension>]
    static member CaptureAsync(this : IRenderTarget) =
        RenderTargetExtensions.CaptureAsync(this, Mod.force this.Samples)