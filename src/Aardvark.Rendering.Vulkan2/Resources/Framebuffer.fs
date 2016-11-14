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

type Framebuffer =
    class
        inherit Resource<VkFramebuffer>
        val mutable public Size : V2i
        val mutable public RenderPass : RenderPass
        val mutable public Attachments : Map<Symbol, ImageView>

        interface IFramebuffer with
            member x.Signature = x.RenderPass :> IFramebufferSignature
            member x.Dispose() = ()
            member x.GetHandle _ = x.Handle :> obj
            member x.Size = x.Size
            member x.Attachments = x.Attachments |> Map.map (fun _ v -> v :> IFramebufferOutput)

        new(device : Device, handle : VkFramebuffer, pass : RenderPass, size : V2i, att : Map<Symbol, ImageView>) = { inherit Resource<_>(device, handle); RenderPass = pass; Size = size; Attachments = att }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Framebuffer =
    
    let create (pass : RenderPass) (views : Map<Symbol, ImageView>) (device : Device) =
        if Map.isEmpty views then
            failf "cannot create empty framebuffer"

        let attachmentSems =
            let colors = pass.ColorAttachments |> Map.map (fun k (sem,_) -> sem)
            match pass.DepthStencilAttachment with
                | Some att ->
                    colors |> Map.add pass.ColorAttachmentCount DefaultSemantic.Depth
                | None ->
                    colors

        let mutable minSize = V2i(Int32.MaxValue, Int32.MaxValue)
        let attachments = 
            attachmentSems
                |> Map.toArray
                |> Array.map (fun (idx, sem) ->
                    match Map.tryFind sem views with
                        | Some view -> 
                            let s = view.Image.Size.XY
                            minSize <- V2i(min minSize.X s.X, min minSize.Y s.Y)
                            view.Handle
                        | _ -> failf "missing framebuffer attachment %A/%A" idx sem
                )

        attachments |> NativePtr.withA (fun pAttachments ->
            let mutable info =
                VkFramebufferCreateInfo(
                    VkStructureType.FramebufferCreateInfo, 0n,
                    VkFramebufferCreateFlags.MinValue,
                    pass.Handle,
                    uint32 attachments.Length, pAttachments,
                    uint32 minSize.X,
                    uint32 minSize.Y,
                    1u
                )

            let mutable handle = VkFramebuffer.Null
            VkRaw.vkCreateFramebuffer(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create framebuffer"

            let real =
                let sems = attachmentSems |> Map.toSeq |> Seq.map snd |> Set.ofSeq
                views |> Map.filter (fun k _ -> Set.contains k sems)

            new Framebuffer(device, handle, pass, minSize, real)
        )

    let delete (fbo : Framebuffer) (device : Device) =
        if fbo.Handle.IsValid then
            VkRaw.vkDestroyFramebuffer(device.Handle, fbo.Handle, NativePtr.zero)
            fbo.Handle <- VkFramebuffer.Null


[<AbstractClass; Sealed; Extension>]
type ContextFramebufferExtensions private() =
    [<Extension>]
    static member inline CreateFramebuffer(this : Device, pass : RenderPass, attachments : Map<Symbol, ImageView>) =
        this |> Framebuffer.create pass attachments
        
    [<Extension>]
    static member inline Delete(this : Device, framebuffer : Framebuffer) =
        this |> Framebuffer.delete framebuffer
        