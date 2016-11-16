namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"


type RenderPass =
    class
        inherit Resource<VkRenderPass>

        val mutable public ColorAttachmentCount : int
        val mutable public ColorAttachments : Map<int, Symbol * AttachmentSignature>
        val mutable public DepthStencilAttachment : Option<AttachmentSignature>

        member x.AttachmentCount =
            match x.DepthStencilAttachment with
                | Some _ -> x.ColorAttachmentCount + 1
                | _ -> 0

        interface IFramebufferSignature with
            member x.Runtime = x.Device.Runtime
            member x.ColorAttachments = x.ColorAttachments
            member x.DepthAttachment = x.DepthStencilAttachment
            member x.StencilAttachment = x.DepthStencilAttachment
            member x.Images = 
                let res = x.ColorAttachments |> Map.map (fun k (v,_) -> v)
                match x.DepthStencilAttachment with
                    | Some d -> res |> Map.add x.ColorAttachmentCount DefaultSemantic.Depth
                    | _ -> res

            member x.IsAssignableFrom (other : IFramebufferSignature) =
                match other with
                    | :? RenderPass as other ->
                        x.Handle = other.Handle
                    | _ ->
                        false

        new(device : Device, handle : VkRenderPass, colorCount : int, colors : Map<int, Symbol * AttachmentSignature>, depth : Option<AttachmentSignature>) = 
            { inherit Resource<_>(device, handle); ColorAttachmentCount = colorCount; ColorAttachments = colors; DepthStencilAttachment = depth }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderPass =
    
    let create (attachments : Map<Symbol, AttachmentSignature>) (device : Device) =
        let depthAtt        = attachments |> Map.tryFind DefaultSemantic.Depth
        let attachments     = attachments |> Map.remove DefaultSemantic.Depth


        let colorAtt = attachments |> Map.toArray
        
        let colorReferences = 
            colorAtt |> Array.mapi (fun i a -> VkAttachmentReference(uint32 i, VkImageLayout.ColorAttachmentOptimal))

        let mutable depthReference =
            match depthAtt with
                | Some depth ->
                    VkAttachmentReference(uint32 colorReferences.Length, VkImageLayout.DepthStencilAttachmentOptimal)
                | _ ->
                    // VK_ATTACHMENT_UNUSED
                    VkAttachmentReference(~~~0u, VkImageLayout.DepthStencilAttachmentOptimal)

        let colorAttachmentDescriptions =
            colorAtt |> Array.map (fun (sem,a) ->
                    
                let loadOp = VkAttachmentLoadOp.Load

                VkAttachmentDescription(
                    VkAttachmentDescriptionFlags.None,
                    VkFormat.ofTextureFormat (unbox (int a.format)),
                    unbox<VkSampleCountFlags> a.samples,

                    loadOp,
                    VkAttachmentStoreOp.Store,
                    VkAttachmentLoadOp.DontCare,
                    VkAttachmentStoreOp.DontCare,

                    VkImageLayout.ColorAttachmentOptimal,
                    VkImageLayout.ColorAttachmentOptimal
                )
            ) 

        let depthAttachmentDescription =
            match depthAtt with
                | Some a ->
                    let format = VkFormat.ofTextureFormat (unbox (int a.format))
                    let hasStencil =
                        match format with
                            | VkFormat.D16UnormS8Uint -> true
                            | VkFormat.D24UnormS8Uint -> true
                            | VkFormat.X8D24UnormPack32 -> true
                            | VkFormat.D32SfloatS8Uint -> true
                            | _ -> false

                    let loadOp = VkAttachmentLoadOp.Load

                    let stencilLoadOp =
                        if hasStencil then VkAttachmentLoadOp.Load
                        else VkAttachmentLoadOp.DontCare

                    let stencilStoreOp =
                        if hasStencil then VkAttachmentStoreOp.Store
                        else VkAttachmentStoreOp.DontCare

                    let desc = 
                        VkAttachmentDescription(
                            VkAttachmentDescriptionFlags.None,
                            format,
                            unbox<VkSampleCountFlags> a.samples,

                            loadOp,
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

        attachmentDescriptions |> NativePtr.withA (fun pAttachmentDescriptions -> 
            colorReferences |> NativePtr.withA (fun pColorReferences ->
                let mutable subpassDescription =
                    VkSubpassDescription(
                        VkSubpassDescriptionFlags.MinValue,
                        VkPipelineBindPoint.Graphics,
                        0u, NativePtr.zero,

                        uint32 colorReferences.Length, pColorReferences,

                        NativePtr.zero,
                        &&depthReference, 

                        0u,  NativePtr.zero
                    )

                let mutable info =
                    VkRenderPassCreateInfo(
                        VkStructureType.RenderPassCreateInfo,
                        0n, VkRenderPassCreateFlags.MinValue,
                        uint32 attachmentDescriptions.Length, pAttachmentDescriptions,
                        1u,
                        &&subpassDescription,
                        0u,
                        NativePtr.zero
                    )

                let mutable handle = VkRenderPass.Null
                VkRaw.vkCreateRenderPass(device.Handle, &&info, NativePtr.zero, &&handle) |> check "vkCreateRenderPass"


                let colorMap = colorAtt |> Array.mapi (fun i v -> i,v) |> Map.ofArray

                RenderPass(device, handle, colorAtt.Length, colorMap, depthAtt)
            )
        )

    let delete (pass : RenderPass) (device : Device) =
        if pass.Handle.IsValid then
            VkRaw.vkDestroyRenderPass(device.Handle, pass.Handle, NativePtr.zero)
            pass.Handle <- VkRenderPass.Null         

[<AbstractClass; Sealed; Extension>]
type ContextRenderPassExtensions private() =
    [<Extension>]
    static member inline CreateRenderPass(this : Device, attachments : Map<Symbol, AttachmentSignature>) =
        this |> RenderPass.create attachments
        
    [<Extension>]
    static member inline Delete(this : Device, pass : RenderPass) =
        this |> RenderPass.delete pass