namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering

#nowarn "9"
#nowarn "51"

[<Flags>]
type ClearMask =
    | None      = 0x0000000
    | Depth     = 0x0000001
    | Color     = 0x0000002
    | Stencil   = 0x0000004

type AttachmentDescription = { format : VkFormat; samples : int; clearMask : ClearMask }


module Formats =
    
    let toRenderbufferFormat =
        lookupTable [
            VkFormat.B8g8r8a8Unorm, RenderbufferFormat.Rgba8
            VkFormat.R8g8b8a8Unorm, RenderbufferFormat.Rgba8
            VkFormat.D24UnormS8Uint, RenderbufferFormat.Depth24Stencil8
            VkFormat.D32Sfloat, RenderbufferFormat.DepthComponent32f
        ]


type RenderPass =
    class
        val mutable public Runtime : IRuntime
        val mutable public Device : Device
        val mutable public Handle : VkRenderPass
        val mutable public ColorAttachments : (Symbol * AttachmentDescription)[]
        val mutable public DepthAttachment : Option<AttachmentDescription>
        
        interface IFramebufferSignature with
            member x.Runtime = x.Runtime
            member x.ColorAttachments = 
                x.ColorAttachments 
                    |> Array.mapi (fun i (sem, att) ->
                        i, (sem, { AttachmentSignature.format = Formats.toRenderbufferFormat att.format; AttachmentSignature.samples = att.samples })
                    )
                    |> Map.ofArray

            member x.DepthAttachment =
                match x.DepthAttachment with
                    | Some att ->
                        { AttachmentSignature.format = Formats.toRenderbufferFormat att.format; AttachmentSignature.samples = att.samples } |> Some
                    | _ ->
                        None

            member x.StencilAttachment =
                match x.DepthAttachment with
                    | Some att ->
                        { AttachmentSignature.format = Formats.toRenderbufferFormat att.format; AttachmentSignature.samples = att.samples } |> Some
                    | _ ->
                        None

            member x.IsAssignableFrom(other : IFramebufferSignature) = true


        member x.HasDepth = x.DepthAttachment.IsSome

        new(runtime, dev, handle, color, depth) = { Runtime = runtime; Device = dev; Handle = handle; ColorAttachments = color; DepthAttachment = depth }
    end

[<AbstractClass; Sealed; Extension>]
type RenderPassExtensions private() =
    
    [<Extension>]
    static member CreateRenderPass(this : Device, colorAttachments : (Symbol * AttachmentDescription)[], ?depthAttachment : AttachmentDescription) =

        // create a subpass description
        let colorAttachmentReferences =
            Array.init colorAttachments.Length (fun i ->
                VkAttachmentReference(
                    uint32 i,
                    VkImageLayout.ColorAttachmentOptimal
                )
            )

        let mutable depthAttachmentReference =
            match depthAttachment with
                | Some att ->
                    VkAttachmentReference(
                        uint32 colorAttachments.Length,
                        VkImageLayout.DepthStencilAttachmentOptimal
                    )
                | None ->
                    VkAttachmentReference(
                        ~~~0u, // VK_ATTACHMENT_UNUSED
                        VkImageLayout.Undefined
                    )

        let pColorAttachmentReferences = NativePtr.pushStackArray colorAttachmentReferences

        let mutable subpassDescription =
            VkSubpassDescription(
                VkSubpassDescriptionFlags.MinValue,
                VkPipelineBindPoint.Graphics,
                0u,
                NativePtr.zero,
                uint32 colorAttachmentReferences.Length,
                pColorAttachmentReferences,
                NativePtr.zero,
                &&depthAttachmentReference, 
                0u, 
                NativePtr.zero
            )


        // create the attachment descriptions
        let colorAttachmentDescriptions =
            colorAttachments |> Array.map (fun (sem,a) ->
                    
                let loadOp =
                    if (a.clearMask &&& ClearMask.Color) <> ClearMask.None then VkAttachmentLoadOp.Clear
                    else VkAttachmentLoadOp.Load

                VkAttachmentDescription(
                    VkAttachmentDescriptionFlags.None,
                    a.format,
                    unbox<VkSampleCountFlags> a.samples,

                    loadOp,
                    VkAttachmentStoreOp.Store,
                    VkAttachmentLoadOp.DontCare,
                    VkAttachmentStoreOp.DontCare,

                    VkImageLayout.General,
                    VkImageLayout.General
                )
            ) 

        let depthAttachmentDescription =
            match depthAttachment with
                | Some a ->

                    let hasStencil =
                        match a.format with
                            | VkFormat.D16UnormS8Uint -> true
                            | VkFormat.D24UnormS8Uint -> true
                            | VkFormat.X8D24UnormPack32 -> true
                            | VkFormat.D32SfloatS8Uint -> true
                            | _ -> false

                    let loadOp =
                        if (a.clearMask &&& ClearMask.Depth) <> ClearMask.None then VkAttachmentLoadOp.Clear
                        else VkAttachmentLoadOp.Load

                    let stencilLoadOp =
                        if hasStencil then
                            if (a.clearMask &&& ClearMask.Stencil) <> ClearMask.None then VkAttachmentLoadOp.Clear
                            else VkAttachmentLoadOp.Load
                        else
                            VkAttachmentLoadOp.DontCare

                    let stencilStoreOp =
                        if hasStencil then VkAttachmentStoreOp.Store
                        else VkAttachmentStoreOp.DontCare

                    let desc = 
                        VkAttachmentDescription(
                            VkAttachmentDescriptionFlags.None,
                            a.format,
                            unbox<VkSampleCountFlags> a.samples,

                            loadOp,
                            VkAttachmentStoreOp.Store,
                            stencilLoadOp,
                            stencilStoreOp,

                            VkImageLayout.General,
                            VkImageLayout.General
                        )

                    Some desc

                | _ -> None

        let attachmentDescriptions =
            match depthAttachmentDescription with
                | Some d -> Array.append colorAttachmentDescriptions [|d|]
                | None -> colorAttachmentDescriptions

        let pAttachmentDescriptions = NativePtr.pushStackArray attachmentDescriptions

        // finally create a renderpass
        let mutable renderPassCreateInfo =
            VkRenderPassCreateInfo(
                VkStructureType.RenderPassCreateInfo,
                0n, VkRenderPassCreateFlags.MinValue,
                uint32 attachmentDescriptions.Length, pAttachmentDescriptions,
                1u,
                &&subpassDescription,
                0u,
                NativePtr.zero
            )


        let mutable renderPass = VkRenderPass.Null
        VkRaw.vkCreateRenderPass(this.Handle, &&renderPassCreateInfo, NativePtr.zero, &&renderPass) |> check "vkCreateRenderPass"

        RenderPass(Unchecked.defaultof<IRuntime>, this, renderPass, colorAttachments, depthAttachment)

    [<Extension>]
    static member Delete(this : Device, p : RenderPass) =
        if p.Handle.IsNull then
            VkRaw.vkDestroyRenderPass(this.Handle, p.Handle, NativePtr.zero)
            p.Handle <- VkRenderPass.Null
            p.ColorAttachments <- [||]
            p.DepthAttachment <- None
