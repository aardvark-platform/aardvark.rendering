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

        val mutable public ColorAttachments : Map<int, Symbol * AttachmentSignature>
        val mutable public DepthStencilAttachment : Option<int * AttachmentSignature>
        val mutable public LayerCount : int
        val mutable public PerLayerUniforms : Set<string>

        // Use depth slot if either depth or combined depth-stencil attachment
        member x.DepthAttachment =
            x.DepthStencilAttachment
            |> Option.map snd
            |> Option.filter (AttachmentSignature.format >> RenderbufferFormat.hasDepth)

        // Use stencil slot only if pure stencil attachment
        member x.StencilAttachment =
            x.DepthStencilAttachment
            |> Option.map snd
            |> Option.filter (AttachmentSignature.format >> RenderbufferFormat.isStencil)

        member x.ColorAttachmentCount =
            Map.count x.ColorAttachments

        member x.AttachmentCount =
            match x.DepthStencilAttachment with
            | Some _ -> x.ColorAttachmentCount + 1
            | _ -> 0

        member x.Semantics =
            let add sym att set =
                if Option.isSome att then set |> Set.add sym else set

            x.ColorAttachments |> Map.toSeq |> Seq.map (snd >> fst) |> Set.ofSeq
            |> add DefaultSemantic.Depth x.DepthAttachment
            |> add DefaultSemantic.Stencil x.StencilAttachment

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyRenderPass(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkRenderPass.Null

        interface IFramebufferSignature with
            member x.Runtime = x.Device.Runtime :> IFramebufferRuntime
            member x.ColorAttachments = x.ColorAttachments
            member x.DepthAttachment = x.DepthAttachment
            member x.StencilAttachment = x.StencilAttachment

            member x.IsAssignableFrom (other : IFramebufferSignature) =
                match other with
                | :? RenderPass as other ->
                    x.Handle = other.Handle
                | _ ->
                    false

            member x.LayerCount = x.LayerCount
            member x.PerLayerUniforms = x.PerLayerUniforms

        new(device : Device, handle : VkRenderPass, colors : Map<int, Symbol * AttachmentSignature>, depthStencil : Option<int * AttachmentSignature>, layers : int, perLayer : Set<string>) =
            { inherit Resource<_>(device, handle); ColorAttachments = colors; DepthStencilAttachment = depthStencil; LayerCount = layers; PerLayerUniforms = perLayer }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderPass =

    let create (attachments : Map<Symbol, AttachmentSignature>) (layers : int) (perLayer : Set<string>) (device : Device) =
        let depth = attachments |> Map.tryFind DefaultSemantic.Depth
        let stencil = attachments |> Map.tryFind DefaultSemantic.Stencil

        let depthStencil =
            match depth.IsSome, stencil.IsSome with
            | false, false -> None
            | true,  false -> depth
            | false, true  -> stencil
            | true,  true  -> failwith "Vulkan backend does not support separate depth and stencil attachments."

        native {
            let attachments     = attachments |> Map.remove DefaultSemantic.Depth |> Map.remove DefaultSemantic.Stencil
            let colorAtt        = attachments |> Map.toSeq |> Seq.mapi (fun i (s,a) -> i,s,a) |> Seq.toArray
            let depthAtt        = depthStencil |> Option.map (fun s -> colorAtt.Length, s)

            let colorReferences =
                colorAtt |> Array.map (fun (i,s,a) -> VkAttachmentReference(uint32 i, VkImageLayout.ColorAttachmentOptimal))

            let! pDepthReference =
                match depthAtt with
                | Some (i, _) -> VkAttachmentReference(uint32 i, VkImageLayout.DepthStencilAttachmentOptimal)
                | _ -> VkAttachmentReference(~~~0u, VkImageLayout.DepthStencilAttachmentOptimal)

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

            let colorAttachmentDescriptions =
                colorAtt |> Array.map (fun (_, _, a) ->
                    let loadOp = VkAttachmentLoadOp.Load

                    VkAttachmentDescription(
                        VkAttachmentDescriptionFlags.None,
                        VkFormat.ofRenderbufferFormat a.format,
                        unbox<VkSampleCountFlags> a.samples,

                        loadOp,
                        VkAttachmentStoreOp.Store,
                        VkAttachmentLoadOp.DontCare,
                        VkAttachmentStoreOp.DontCare,

                        VkImageLayout.ColorAttachmentOptimal,
                        VkImageLayout.ColorAttachmentOptimal
                    )
                )

            let rec tryFindFormat (fmt : VkFormat) =
                match device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, fmt) with
                | VkFormatFeatureFlags.None ->
                    match fmt.NextBetter with
                    | Some better -> tryFindFormat better
                    | None -> None
                | _ ->
                    Some fmt

            let depthAttachmentDescription =
                match depthAtt with
                | Some (_, a) ->
                    let format = VkFormat.ofRenderbufferFormat a.format

                    let format =
                        match tryFindFormat format with
                        | Some fmt -> fmt
                        | None -> failf "could not get supported format for %A" format

                    let stencilLoadOp, stencilStoreOp =
                        if VkFormat.hasStencil format then
                            VkAttachmentLoadOp.Load, VkAttachmentStoreOp.Store
                        else
                            VkAttachmentLoadOp.DontCare, VkAttachmentStoreOp.DontCare

                    let desc =
                        VkAttachmentDescription(
                            VkAttachmentDescriptionFlags.None,
                            format,
                            unbox<VkSampleCountFlags> a.samples,

                            VkAttachmentLoadOp.Load,
                            VkAttachmentStoreOp.Store,
                            stencilLoadOp,
                            stencilStoreOp,

                            VkImageLayout.DepthStencilAttachmentOptimal,
                            VkImageLayout.DepthStencilAttachmentOptimal
                        )

                    Some desc

                | _ ->
                    None

            let attachmentDescriptions =
                match depthAttachmentDescription with
                | Some d -> Array.append colorAttachmentDescriptions [|d|]
                | None -> colorAttachmentDescriptions

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

            let colorMap = colorAtt |> Array.map (fun (i,s,v) -> i,(s,v)) |> Map.ofArray
            return new RenderPass(device, !!pHandle, colorMap, depthAtt, layers, perLayer)
        }

[<AbstractClass; Sealed; Extension>]
type ContextRenderPassExtensions private() =
    [<Extension>]
    static member inline CreateRenderPass(this : Device, attachments : Map<Symbol, AttachmentSignature>, layers : int, perLayer : Set<string>) =
        this |> RenderPass.create attachments layers perLayer