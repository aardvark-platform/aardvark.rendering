namespace Aardvark.Rendering.GL

open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module private FramebufferMemoryUsage =
    let addPhysicalFbo (ctx:Context) =
        Interlocked.Increment(&ctx.MemoryUsage.PhysicalFramebufferCount) |> ignore
    let removePhysicalFbo (ctx:Context) =
        Interlocked.Decrement(&ctx.MemoryUsage.PhysicalFramebufferCount) |> ignore
    let addVirtualFbo (ctx:Context) =
        Interlocked.Increment(&ctx.MemoryUsage.VirtualFramebufferCount) |> ignore
    let removeVirtualFbo (ctx:Context) =
        Interlocked.Decrement(&ctx.MemoryUsage.VirtualFramebufferCount) |> ignore

type Framebuffer(ctx : Context, signature : IFramebufferSignature, create : ContextHandle -> int, destroy : int -> unit, 
                 bindings : list<int * Symbol * IFramebufferOutput>, depthStencil : Option<IFramebufferOutput>) =
    inherit UnsharedObject(ctx, (fun h -> addPhysicalFbo ctx; create h), (fun h -> removePhysicalFbo ctx; destroy h))

    let mutable bindings = bindings
    let mutable depthStencil = depthStencil

    let resolution() =
        match depthStencil with
        | Some d -> d.Size
        | _ ->
            match bindings |> List.tryHead with
            | Some (_, _, b) -> b.Size
            | _ -> V2i.II

    let mutable size = resolution()

    let mutable outputBySem = 
        let bindings = (bindings |> List.map (fun (_,s,o) -> (s,o)))
        let depthStencil = match depthStencil with | Some d -> [DefaultSemantic.DepthStencil, d] | _ -> []
        depthStencil |> List.append bindings |> Map.ofList

    member x.Size 
        with get() = size
        and set v = size <- v

    member x.Update(create : ContextHandle -> int,
                    b : list<int * Symbol * IFramebufferOutput>,
                    ds : Option<IFramebufferOutput>) =
        base.Update(create)
        bindings <- b
        depthStencil <- ds
        let bindings = (bindings |> List.map (fun (_,s,o) -> (s,o)))
        let depthStencil = match depthStencil with | Some d -> [DefaultSemantic.DepthStencil, d] | _ -> []
        outputBySem <- depthStencil |> List.append bindings |> Map.ofList

    member x.Attachments = outputBySem
    member x.Signature = signature

    member x.Dispose() =
        removeVirtualFbo ctx
        x.DestroyHandles()

    interface IFramebuffer with
        member x.Signature = signature
        member x.Size = x.Size
        member x.Handle = uint64 x.Handle
        member x.Attachments = outputBySem
        member x.Dispose() = x.Dispose()

//    static member Default (ctx : Context, size : V2i, samples : int) =
//        let bindings = [0,DefaultSemantic.Colors,Texture(ctx, 0, TextureDimension.Texture2D, 1, samples, V3i(size.X, size.Y, 1), 1, ChannelType.RGBA8).Output 0 :> IFramebufferOutput]
//        let depth = Renderbuffer(ctx, 0, V2i.II, TextureFormat.Depth24Stencil8, 1) :> IFramebufferOutput
//        new Framebuffer(ctx, (fun _ -> 0), (fun _ -> ()), bindings, Some depth)

[<AutoOpen>]
module FramebufferExtensions =

    let private destroy (handle : int) =
        GL.DeleteFramebuffer handle
        GL.Check "could not delete framebuffer"

    let private init (signature : IFramebufferSignature) (bindings : list<int * Symbol * IFramebufferOutput>) (depthStencil : Option<IFramebufferOutput>)
                     (context : ContextHandle) : int =

        let mutable oldFbo = 0
        GL.GetInteger(GetPName.DrawFramebufferBinding, &oldFbo)

        let handle = GL.GenFramebuffer()
        GL.Check "could not create framebuffer"

        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, handle)
        GL.Check "could not bind framebuffer"

        let attach (semantic : Symbol) (attachment : FramebufferAttachment) (output : IFramebufferOutput) =
            let validateLayerCount (count : int) =
                if count < signature.LayerCount then
                    failf "framebuffer attachment %A does not have enough layers for signature (attachment has %d, signature requires %d)" semantic count signature.LayerCount

            match output with
            | :? Renderbuffer as o ->
                validateLayerCount 1
                GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, attachment, RenderbufferTarget.Renderbuffer, o.Handle)
                GL.Check "could not attach renderbuffer"

            | :? ITextureLevel as r ->
                let o = unbox<Texture> r.Texture

                let baseSlice = r.Slices.Min
                let slices = 1 + r.Slices.Max - baseSlice
                let level = r.Level

                validateLayerCount slices

                if slices > 1 then
                    if baseSlice <> 0 || slices <> (if o.Dimension = TextureDimension.TextureCube then 6 * o.Count else o.Count) then // TODO: Is it possible to bind a cubemap array as texture layers?
                        failf "sub-layers not supported atm."
  
                    GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, attachment, o.Handle, level)
                    GL.Check "could not attach texture"

                else
                    match o.Dimension with
                    | TextureDimension.TextureCube ->
                        let (_,target) = TextureTarget.cubeSides.[baseSlice]
                        if o.IsArray then
                            failf "cubemaparray currently not implemented"
                        else
                            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, attachment, target, o.Handle, level)
                        GL.Check "could not attach texture"
                    | _ ->
                        if o.IsArray then
                            GL.FramebufferTextureLayer(FramebufferTarget.DrawFramebuffer, attachment, o.Handle, level, baseSlice)
                        else
                            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, attachment, (if o.IsMultisampled then TextureTarget.Texture2DMultisample else TextureTarget.Texture2D), o.Handle, level)
                        GL.Check "could not attach texture"

            | v ->
                failf "unsupported view: %A" v

        // attach all colors
        for (i, s, o) in bindings do
            let attachment = int FramebufferAttachment.ColorAttachment0 + i |> unbox<FramebufferAttachment>
            o |> attach s attachment

        // attach depth-stencil
        match depthStencil with
        | Some o ->
            let attachment =
                if o.Format.IsDepthStencil then
                    FramebufferAttachment.DepthStencilAttachment
                elif o.Format.IsDepth then
                    FramebufferAttachment.DepthAttachment
                else
                    FramebufferAttachment.StencilAttachment

            o |> attach DefaultSemantic.DepthStencil attachment
        | None ->
            ()

        // check framebuffer
        let status = GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer)
        GL.Check "could not get framebuffer status"

        // unbind
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, oldFbo)
        GL.Check "could not unbind framebuffer"

        if status <> FramebufferErrorCode.FramebufferComplete then
            // cleanup and raise exception
            destroy handle
            raise <| OpenGLException(ErrorCode.InvalidFramebufferOperation, sprintf "framebuffer incomplete: %A" status)

        handle


    type Context with

        member x.CreateFramebuffer (signature : IFramebufferSignature, bindings : list<int * Symbol * IFramebufferOutput>, depthStencil : Option<IFramebufferOutput>) =
            let init = init signature bindings depthStencil
            addVirtualFbo x
            new Framebuffer(x, signature, init, destroy, bindings, depthStencil)

        member x.Delete(f : Framebuffer) =
            removeVirtualFbo x
            f.DestroyHandles()

        member x.Update (f : Framebuffer, bindings : list<int * Symbol * IFramebufferOutput>, depthStencil : Option<IFramebufferOutput>) =
            let init = init f.Signature bindings depthStencil
            f.Update(init, bindings, depthStencil)