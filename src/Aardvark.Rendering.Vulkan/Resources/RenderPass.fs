namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"


type RenderPass =
    class
        inherit Resource<VkRenderPass>
        val public Samples : int
        val internal Attachments : Symbol[]
        val public ColorAttachments : Map<int, AttachmentSignature>
        val public DepthStencilAttachment : Option<VkFormat>
        val public LayerCount : int
        val public PerLayerUniforms : Set<string>

        member x.Runtime =
            x.Device.Runtime

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyRenderPass(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkRenderPass.Null

        interface IFramebufferSignature with
            member x.Runtime = x.Runtime :> IFramebufferRuntime
            member x.Samples = x.Samples
            member x.ColorAttachments = x.ColorAttachments
            member x.DepthStencilAttachment = x.DepthStencilAttachment |> Option.map VkFormat.toTextureFormat

            member x.LayerCount = x.LayerCount
            member x.PerLayerUniforms = x.PerLayerUniforms

        new(device : Device, handle : VkRenderPass,
            colors : Map<int, AttachmentSignature>, depthStencil : Option<VkFormat>,
            samples : int, layers : int, perLayer : Set<string>) =

            let attachments =
                let colors = colors |> Map.toArray |> Array.map (snd >> AttachmentSignature.name)
                match depthStencil with
                | Some _ -> Array.append colors [| DefaultSemantic.DepthStencil |]
                | _ -> colors

            { inherit Resource<_>(device, handle);
                Samples = samples;
                Attachments = attachments;
                ColorAttachments = colors;
                DepthStencilAttachment = depthStencil;
                LayerCount = layers;
                PerLayerUniforms = perLayer
            }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderPass =

    [<AutoOpen>]
    module private Utilities =

        let rec tryFindFormat (device : Device) (fmt : VkFormat) =
            match device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, fmt) with
            | VkFormatFeatureFlags.None ->
                match fmt.NextBetter with
                | Some better -> tryFindFormat device better
                | None -> None
            | _ ->
                Some fmt

        let getLoadStoreOp flag =
            if flag then
                VkAttachmentLoadOp.Load, VkAttachmentStoreOp.Store
            else
                VkAttachmentLoadOp.DontCare, VkAttachmentStoreOp.DontCare

    module private VkAttachmentReference =
        let unused = VkAttachmentReference(VkAttachmentUnused, VkImageLayout.Undefined)

    let create (colorAttachments : Map<int, AttachmentSignature>) (depthStencilAttachment : Option<TextureFormat>)
               (samples : int) (layers : int) (perLayer : Set<string>) (device : Device) =
        native {
            let limits =
                let fbo = device.PhysicalDevice.Limits.Framebuffer

                let counts = [
                        if not colorAttachments.IsEmpty then
                            fbo.ColorSampleCounts

                        match depthStencilAttachment with
                        | Some fmt ->
                            if fmt.HasDepth then
                                fbo.DepthSampleCounts

                            if fmt.HasStencil then
                                fbo.StencilSampleCounts

                        | _ -> ()
                    ]

                if counts.IsEmpty then fbo.NoAttachmentsSampleCounts
                else Set.intersectMany counts

            let samples =
                if limits.Contains samples then samples
                else
                    let max = Set.maxElement limits
                    Log.warn "[Vulkan] cannot create render pass with %d samples (using %d instead)" samples max
                    max

            let colors =
                colorAttachments
                |> Map.toArray
                |> Array.mapi (fun index (slot, att) ->
                    let description =
                        VkAttachmentDescription(
                            VkAttachmentDescriptionFlags.None,
                            VkFormat.ofTextureFormat att.Format,
                            unbox<VkSampleCountFlags> samples,
                            VkAttachmentLoadOp.Load, VkAttachmentStoreOp.Store,
                            VkAttachmentLoadOp.DontCare, VkAttachmentStoreOp.DontCare,
                            VkImageLayout.ColorAttachmentOptimal,
                            VkImageLayout.ColorAttachmentOptimal
                        )

                    let reference =
                        VkAttachmentReference(uint32 index, VkImageLayout.ColorAttachmentOptimal)

                    slot, (description, reference)
                )

            let depth =
                depthStencilAttachment |> Option.map (fun fmt ->
                    let format =
                        match tryFindFormat device <| VkFormat.ofTextureFormat fmt with
                        | Some fmt -> fmt
                        | None -> failf "could not get supported format for %A" fmt

                    let depthLoadOp, depthStoreOp = getLoadStoreOp <| VkFormat.hasDepth format
                    let stencilLoadOp, stencilStoreOp = getLoadStoreOp <| VkFormat.hasStencil format

                    let description =
                        VkAttachmentDescription(
                            VkAttachmentDescriptionFlags.None,
                            format,
                            unbox<VkSampleCountFlags> samples,
                            depthLoadOp, depthStoreOp,
                            stencilLoadOp, stencilStoreOp,
                            VkImageLayout.DepthStencilAttachmentOptimal,
                            VkImageLayout.DepthStencilAttachmentOptimal
                        )

                    let reference =
                        VkAttachmentReference(uint32 colors.Length, VkImageLayout.DepthStencilAttachmentOptimal)

                    description, reference
                )

            let depthStencilFormat =
                depth |> Option.map (fun (desc, _) -> desc.format)

            let colorReferences =
                let count =
                    match colors with
                    | [||] -> 0
                    | _ -> (colors |> Array.last |> fst) + 1

                Array.init count (fun i ->
                    let reference =
                        colors |> Array.tryPick (fun (slot, (_, ref)) ->
                            if slot = i then Some ref else None
                        )

                    reference |> Option.defaultValue VkAttachmentReference.unused
                )

            let! pDepthReference =
                depth
                |> Option.map snd
                |> Option.defaultValue VkAttachmentReference.unused

            let! pColorReferences = colorReferences
            let! pSubpassDescription =
                VkSubpassDescription(
                    VkSubpassDescriptionFlags.None,
                    VkPipelineBindPoint.Graphics,

                    //inputs
                    0u, NativePtr.zero,

                    //attachments
                    uint32 colorReferences.Length, pColorReferences,

                    //resolve
                    NativePtr.zero,

                    //depth
                    pDepthReference,

                    //preserve
                    0u, NativePtr.zero
                )

            let attachmentDescriptions =
                let colorDescriptions = colors |> Array.map (snd >> fst)

                match depth with
                | Some (d, _) -> Array.append colorDescriptions [|d|]
                | None -> colorDescriptions

            let! pAttachmentDescriptions = attachmentDescriptions
            let! pInfo =
                VkRenderPassCreateInfo(
                    VkRenderPassCreateFlags.None,
                    uint32 attachmentDescriptions.Length, pAttachmentDescriptions,
                    1u, pSubpassDescription,
                    0u, NativePtr.zero
                )

            let! pHandle = VkRenderPass.Null
            VkRaw.vkCreateRenderPass(device.Handle, pInfo, NativePtr.zero, pHandle) |> check "vkCreateRenderPass"

            return new RenderPass(device, !!pHandle, colorAttachments, depthStencilFormat, samples, layers, perLayer)
        }

    let internal validateCompability (fbo : IFramebuffer) (pass : RenderPass) =
        if not <| pass.IsCompatibleWith fbo.Signature then
            failf "render task signatures need to be strictly compatible (i.e. equivalent) with the framebuffer signature\ntask signature:\n%A\n\nframebuffer signature:\n%A" pass.Layout fbo.Signature.Layout

[<AbstractClass; Sealed; Extension>]
type ContextRenderPassExtensions private() =
    [<Extension>]
    static member inline CreateRenderPass(this : Device, color : Map<int, AttachmentSignature>, depth : Option<TextureFormat>,
                                          samples : int, layers : int, perLayer : Set<string>) =
        this |> RenderPass.create color depth samples layers perLayer

    [<Extension>]
    static member inline CreateRenderPass(this : Device, color : List<AttachmentSignature>, depth : Option<TextureFormat>,
                                          samples : int, layers : int, perLayer : Set<string>) =
        this.CreateRenderPass(color |> List.indexed |> Map.ofList, depth, samples, layers, perLayer)