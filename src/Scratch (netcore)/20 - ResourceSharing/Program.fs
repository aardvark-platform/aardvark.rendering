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
        [<TexCoord>] tc : V2d
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

    [<ReflectedDefinition>]
    let toFragCoords (size : V2i) (tc : V2d) =
        V2i(tc * V2d size)

    let resolve (samples : int) (f : Fragment) =
        fragment {
            if samples > 1 then
                let mutable result = V4d.Zero

                for i = 0 to samples - 1 do
                    result <- result + diffuseSamplerMS.Read(f.tc |> toFragCoords diffuseSamplerMS.Size, i)

                return result / float samples
            else
                return diffuseSampler.Read(f.tc |> toFragCoords diffuseSampler.Size, 0)
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

type SharedAdaptiveBuffer<'T>(runtime : Runtime, data : aval<'T[]>) =
    inherit AdaptiveResource<IBackendBuffer>()

    let mutable handle : Option<IBackendBuffer * 'T[]> = None

    member private x.CreateHandle(data : 'T[]) =
        let buf = runtime.PrepareBuffer(ArrayBuffer data, export = true)
        handle <- Some (buf, data)
        buf

    override x.Create() = ()
    override x.Destroy() =
        match handle with
        | Some (h, _) ->
            h.Dispose()
            handle <- None
        | None ->
            ()

    override x.Compute(token : AdaptiveToken, t : RenderToken) =
        let data = data.GetValue token

        match handle with
        | Some (h, d) when Object.ReferenceEquals(d, data) ->
            h
        | Some (h, _) ->
            h.Dispose()
            x.CreateHandle(data)
        | None ->
            x.CreateHandle(data)


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

    use vulkanFullscreenQuadPositionBuffer =
        vulkan.Runtime.PrepareBuffer(
            ArrayBuffer(
                [| V3f(-1.0, -1.0, 1.0); V3f(1.0, -1.0, 1.0)
                   V3f(-1.0, 1.0, 1.0); V3f(1.0, 1.0, 1.0) |]
            ), export = true
        )

    let vulkanFullscreenQuadTexCoordBuffer =
        let data =
            let start = DateTime.Now

            win.Time |> AVal.map (fun t ->
                let elapsed = (start - t).TotalSeconds
                let s1 = (sin elapsed + 1.0)
                let s2 = (cos elapsed + 1.0)

                [| V2f(0.0 + s1, 0.0); V2f(1.0 - s2, 0.0)
                   V2f(0.0 + s2, 1.0); V2f(1.0 - s1, 1.0) |]
            )

        SharedAdaptiveBuffer(vulkan.Runtime, data)

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

        let fullscreenQuad =
            let drawCall =
                DrawCallInfo(
                    FaceVertexCount = 4,
                    InstanceCount = 1
                )

            let positions = Aardvark.Rendering.BufferView(vulkanFullscreenQuadPositionBuffer, typeof<V3f>)
            let texcoords = Aardvark.Rendering.BufferView(vulkanFullscreenQuadTexCoordBuffer, typeof<V2f>)

            drawCall
            |> Sg.render IndexedGeometryMode.TriangleStrip
            |> Sg.vertexBuffer DefaultSemantic.Positions positions
            |> Sg.vertexBuffer DefaultSemantic.DiffuseColorCoordinates texcoords

        fullscreenQuad
        |> Sg.diffuseTexture vulkanRenderedTexture
        |> Sg.shader {
            do! Shader.resolve vulkanFramebufferSignature.Samples
        }

    // show the window
    win.Scene <- sg
    win.Run()

    0
