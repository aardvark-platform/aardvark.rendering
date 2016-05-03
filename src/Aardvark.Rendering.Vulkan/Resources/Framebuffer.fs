namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"


type Framebuffer =
    class
        val mutable public Context : Context
        val mutable public Handle : VkFramebuffer
        val mutable public RenderPass : RenderPass
        val mutable public Attachments : list<ImageView>
        val mutable public Size : V2i

        interface IFramebuffer with
            member x.Dispose() = ()
            member x.Attachments = Map.empty
            member x.GetHandle(caller) = x.Handle :> obj
            member x.Size = x.Size
            member x.Signature = x.RenderPass :> IFramebufferSignature

        new(ctx,h, rp, att, s) = { Context = ctx; Handle = h; RenderPass = rp; Attachments = att; Size = s }

    end

[<AbstractClass; Sealed; Extension>]
type FramebufferExtensions private() =

    [<Extension>]
    static member CreateFramebuffer(this : Context, pass : RenderPass, attachments : list<ImageView>, size : V2i) =
            
        let viewHandles = attachments |> List.map (fun v -> v.Handle)
        let views = NativePtr.pushStackArray viewHandles

   
        let mutable info =
            VkFramebufferCreateInfo(
                VkStructureType.FramebufferCreateInfo,
                0n, VkFramebufferCreateFlags.MinValue,
                pass.Handle,
                uint32 viewHandles.Length,
                views,
                uint32 size.X,
                uint32 size.Y,
                1u
            )

        let mutable fbo = VkFramebuffer.Null
        VkRaw.vkCreateFramebuffer(this.Device.Handle, &&info, NativePtr.zero, &&fbo) |> check "vkCreateFramebuffer"

        new Framebuffer(this, fbo, pass, attachments, size)

    [<Extension>]
    static member CreateFramebuffer(this : Context, pass : RenderPass, attachments : list<ImageView>) =
        let fst = List.head attachments
        let size = fst.Image.Size.XY
        FramebufferExtensions.CreateFramebuffer(
            this,
            pass,
            attachments,
            size
        )

    [<Extension>]
    static member Delete(this : Context, fbo : Framebuffer) =
        if fbo.Handle.IsValid then
            VkRaw.vkDestroyFramebuffer(this.Device.Handle, fbo.Handle, NativePtr.zero)
            fbo.Handle <- VkFramebuffer.Null
