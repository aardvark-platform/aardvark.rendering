namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"

type Framebuffer =
    class
        inherit Resource<VkFramebuffer>
        val public Size : V2i
        val public RenderPass : RenderPass
        val public Attachments : Map<Symbol, ImageView>

        override x.Destroy() =
            if x.Device.Handle <> 0n && x.Handle.IsValid then
                VkRaw.vkDestroyFramebuffer(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkFramebuffer.Null
            
            x.Attachments |> Map.iter (fun _ v -> v.Dispose())

        interface IFramebuffer with
            member x.Signature = x.RenderPass :> IFramebufferSignature
            member x.Handle = x.Handle.Handle
            member x.Size = x.Size
            member x.Attachments = x.Attachments |> Map.map (fun _ v -> v :> IFramebufferOutput)

        new(device : Device, handle : VkFramebuffer, pass : RenderPass, size : V2i, att : Map<Symbol, ImageView>) =
            { inherit Resource<_>(device, handle);
                RenderPass = pass;
                Size = size;
                Attachments = att;
            }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Framebuffer =

    let create (pass : RenderPass) (views : Map<Symbol, ImageView>) (device : Device) =
        if Map.isEmpty views then
            failf "cannot create empty framebuffer"

        let mutable minSize = V2i(Int32.MaxValue, Int32.MaxValue)

        let attachments =
            pass.Attachments
            |> Array.map (fun sem ->
                match Map.tryFind sem views with
                | Some view -> 
                    let s = Fun.MipmapLevelSize(view.Image.Size.XY, view.MipLevelRange.Max)
                    let l = 1 + view.ArrayRange.Max - view.ArrayRange.Min

                    if l < pass.LayerCount then
                        failf "framebuffer attachment %A does not have enough layers for render pass (attachment has %d, render pass requires %d)" sem l pass.LayerCount

                    minSize <- min minSize s
                    sem, view
                | _ -> failf "missing framebuffer attachment %A" sem
            )

        let attachmentMap =
            attachments |> Map.ofArray

        let attachmentHandles =
            attachments |> Array.map (snd >> _.Handle)

        native {
            let! pAttachments = attachmentHandles
            let! pInfo =
                VkFramebufferCreateInfo(
                    VkFramebufferCreateFlags.None,
                    pass.Handle,
                    uint32 attachments.Length, pAttachments,
                    uint32 minSize.X,
                    uint32 minSize.Y,
                    uint32 pass.LayerCount
                )

            let! pHandle = VkFramebuffer.Null
            VkRaw.vkCreateFramebuffer(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create framebuffer"

            let handle = NativePtr.read pHandle
            return new Framebuffer(device, handle, pass, minSize, attachmentMap)
        }

[<AbstractClass; Sealed; Extension>]
type ContextFramebufferExtensions private() =
    [<Extension>]
    static member inline CreateFramebuffer(this : Device, pass : RenderPass, attachments : Map<Symbol, ImageView>) =
        this |> Framebuffer.create pass attachments