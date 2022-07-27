open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open System

module Shader =
    open FShade

    type Fragment = {
        [<FragCoord>]    coord : V4d
    }

    [<AutoOpen>]
    module private Samplers =

        let diffuseSampler =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

        let diffuseSamplerMS =
            sampler2dMS {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

    let resolve (samples : int) (f : Fragment) =
        fragment {
            if samples > 1 then
                let mutable result = V4d.Zero

                for i = 0 to samples - 1 do
                    result <- result + diffuseSamplerMS.Read(V2i f.coord.XY, i)

                return result / float samples
            else
                return diffuseSampler.Read(V2i f.coord.XY, 0)
        }

type SharedAdaptiveTexture(runtime : Runtime, size : aval<V2i>, format : TextureFormat, samples : int) =
    inherit AdaptiveResource<IBackendTexture>()

    let mutable handle : Option<IBackendTexture * V2i> = None

    member private x.CreateHandle(size : V2i) =
        let tex = runtime.CreateTexture(size.XYI, TextureDimension.Texture2D, format, 1, samples, true)
        handle <- Some (tex, size)
        tex

    override x.Create() = ()
    override x.Destroy() =
        match handle with
        | Some (h, _) ->
            runtime.DeleteTexture h
            handle <- None
        | None ->
            ()

    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let size = size.GetValue token

        match handle with
        | Some (h, s) when size = s ->
            h

        | Some (h, _) ->
            runtime.DeleteTexture h
            x.CreateHandle(size)

        | None ->
            x.CreateHandle(size)

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    use vulkan = new HeadlessVulkanApplication(debug = true)

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 8
        }

    use vulkanFramebufferSignature =
        vulkan.Runtime.CreateFramebufferSignature(
            Map.ofList [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
            ],
            samples = 4
        )

    let vulkanFramebuffer =
        let colorBuffer =
            SharedAdaptiveTexture(vulkan.Runtime, win.Sizes, TextureFormat.Rgba8, vulkanFramebufferSignature.Samples)
                |> AdaptiveResource.map (fun t -> t.GetOutputView())

        let depthBuffer =
            vulkan.Runtime.CreateRenderbuffer(win.Sizes, TextureFormat.Depth24Stencil8, vulkanFramebufferSignature.Samples)
                |> AdaptiveResource.mapNonAdaptive (fun rb -> rb :> IFramebufferOutput)

        vulkan.Runtime.CreateFramebuffer(vulkanFramebufferSignature, Map.ofList [
            DefaultSemantic.Colors, colorBuffer
            DefaultSemantic.DepthStencil, depthBuffer
        ])


    use vulkanTask =
        let box = Box3d(-V3d.III, V3d.III)
        let color = C4b.Red

        let trafo =
            let start = DateTime.Now

            win.Time |> AVal.map (fun t ->
                let elapsed = (start - t).TotalSeconds
                Trafo3d.RotationY(elapsed)
            )

        Sg.box (AVal.constant color) (AVal.constant box)
            |> Sg.trafo trafo
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }
            |> Sg.viewTrafo (win.View |> AVal.mapNonAdaptive Array.head)
            |> Sg.projTrafo (win.Proj |> AVal.mapNonAdaptive Array.head)
            |> Sg.compile vulkan.Runtime vulkanFramebufferSignature

    let vulkanRenderedTexture =
        let clear = clear { color C4b.Black; depth 1.0 }
        vulkanTask.RenderTo(vulkanFramebuffer, clear).GetOutputTexture(DefaultSemantic.Colors)

    let sg =
        Sg.fullScreenQuad
        |> Sg.diffuseTexture vulkanRenderedTexture
        |> Sg.shader {
            do! Shader.resolve vulkanFramebufferSignature.Samples
        }

    // show the window
    win.Scene <- sg
    win.Run()

    0
