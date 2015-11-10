namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations

type Framebuffer(ctx : Context, signature : IFramebufferSignature, create : Aardvark.Rendering.GL.ContextHandle -> int, destroy : int -> unit, 
                 bindings : list<int * Symbol * IFramebufferOutput>, depth : Option<IFramebufferOutput>) =
    inherit UnsharedObject(ctx, create, destroy)

    let mutable bindings = bindings
    let mutable depth = depth

    let resolution() =
        match depth with
            | Some d -> d.Size
            | _ ->
                let res = 
                    bindings |> Seq.tryPick (fun (_,_,b) ->
                        Some b.Size
                    )
                match res with
                    | Some s -> s
                    | None -> V2i.II //failwith "could not determine framebuffer-size."

    let mutable size = resolution()

    let mutable outputBySem = 
        let bindings = (bindings |> List.map (fun (_,s,o) -> (s,o)))
        let depth = match depth with | Some d -> [DefaultSemantic.Depth, d] | _ -> []
        List.append bindings depth |> Map.ofList

    member x.Size 
        with get() = size
        and set v = size <- v

    member x.Update(create : Aardvark.Rendering.GL.ContextHandle -> int, b : list<int * Symbol * IFramebufferOutput>, d : Option<IFramebufferOutput>) =
        base.Update(create)
        bindings <- b
        depth <- d
        let bindings = (bindings |> List.map (fun (_,s,o) -> (s,o)))
        let depth = match depth with | Some d -> [DefaultSemantic.Depth, d] | _ -> []
        outputBySem <- List.append bindings depth |> Map.ofList

    interface IFramebuffer with
        member x.Signature = signature
        member x.Size = x.Size
        member x.GetHandle caller = x.Handle :> obj
        member x.Attachments = outputBySem
        member x.Dispose() = base.DestroyHandles()

//    static member Default (ctx : Context, size : V2i, samples : int) =
//        let bindings = [0,DefaultSemantic.Colors,Texture(ctx, 0, TextureDimension.Texture2D, 1, samples, V3i(size.X, size.Y, 1), 1, ChannelType.RGBA8).Output 0 :> IFramebufferOutput]
//        let depth = Renderbuffer(ctx, 0, V2i.II, RenderbufferFormat.Depth24Stencil8, 1) :> IFramebufferOutput
//        new Framebuffer(ctx, (fun _ -> 0), (fun _ -> ()), bindings, Some depth)

[<AutoOpen>]
module FramebufferExtensions =

    let private init (bindings : list<int * Symbol * IFramebufferOutput>) (depth : Option<IFramebufferOutput>) (c : Aardvark.Rendering.GL.ContextHandle) : int =
        let handle = GL.GenFramebuffer()
        GL.Check "could not create framebuffer"

        let mutable oldFbo = 0
        GL.GetInteger(GetPName.FramebufferBinding, &oldFbo)

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, handle)
        GL.Check "could not bind framebuffer"

        let attach (o : IFramebufferOutput) (attachment) =
            match o with

                | :? Renderbuffer as o ->
                    GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, attachment, RenderbufferTarget.Renderbuffer, o.Handle)
                    GL.Check "could not attach renderbuffer"


                | :? BackendTextureOutputView as r ->
                    let { texture = o; level = level; slice = slice } = r
                    let o = unbox<Texture> o

                    match o.Dimension with
                        | TextureDimension.TextureCube ->
                            let (_,target) = TextureExtensions.cubeSides.[slice]
                            if o.Count > 1 then
                                failwith "cubemaparray currently not implemented"
                            else
                                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, target, o.Handle, level)
                            GL.Check "could not attach texture"
                        | _ ->
                            if o.Count > 1 then
                                GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, attachment, o.Handle, level, slice)
                            else
                                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, TextureTarget.Texture2D, o.Handle, level)
                            GL.Check "could not attach texture"

        
                | _ ->
                    failwith "unsupported view"

        // attach all colors
        for (i,s,o) in bindings do
            let attachment = int FramebufferAttachment.ColorAttachment0 + i |> unbox<FramebufferAttachment>
            attach o attachment

        // attach depth
        match depth with
            | Some o ->
                attach o FramebufferAttachment.DepthAttachment
            | None ->
                ()

        // check framebuffer
        let status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
        GL.Check "could not get framebuffer status"

        if status <> FramebufferErrorCode.FramebufferComplete then
            raise <| OpenGLException(ErrorCode.InvalidFramebufferOperation, sprintf "framebuffer incomplete: %A" status)

        // unbind
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, oldFbo)
        GL.Check "could not unbind framebuffer"

        handle

    let private destroy (handle : int) =
        GL.DeleteFramebuffer handle
        GL.Check "could not delete framebuffer"

    type Context with

        member x.CreateFramebuffer (signature : IFramebufferSignature, bindings : list<int * Symbol * IFramebufferOutput>, depth : Option<IFramebufferOutput>) =
            let init = init bindings depth

            new Framebuffer(x, signature, init, destroy, bindings, depth)

        member x.Delete(f : Framebuffer) =
            f.DestroyHandles()

        member x.Update (f : Framebuffer, bindings : list<int * Symbol * IFramebufferOutput>, depth : Option<IFramebufferOutput>) =
            let init = init bindings depth
            f.Update(init, bindings, depth)