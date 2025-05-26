namespace Aardvark.Rendering.GL

open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module internal RenderbufferResourceCounts =

    module ResourceCounts =

        let addRenderbuffer (ctx:Context) size =
            Interlocked.Increment(&ctx.MemoryUsage.RenderBufferCount) |> ignore
            Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,size) |> ignore

        let removeRenderbuffer(ctx:Context) size =
            Interlocked.Decrement(&ctx.MemoryUsage.RenderBufferCount)  |> ignore
            Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,-size) |> ignore

        let resizeRenderbuffer(ctx:Context) oldSize newSize =
            Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,newSize - oldSize) |> ignore

type Renderbuffer =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Size : V2i
        val mutable public Format : TextureFormat
        val mutable public Samples : int
        val mutable public SizeInBytes : int64
        val mutable private name : string

        member x.Name
            with get() = x.name
            and set name =
                x.name <- name
                x.Context.SetObjectLabel(ObjectLabelIdentifier.Renderbuffer, x.Handle, name)

        abstract member Destroy : unit -> unit
        default x.Destroy() =
            GL.DeleteRenderbuffer(x.Handle)
            ResourceCounts.removeRenderbuffer x.Context x.SizeInBytes
            GL.Check "could not delete renderbuffer"

        member x.Dispose() =
            using x.Context.ResourceLock (fun _ ->
                x.Destroy()
                x.Handle <- 0
            )

        interface IFramebufferOutput with
            member x.Runtime = x.Context.Runtime :> ITextureRuntime
            member x.Format = x.Format
            member x.Size = x.Size
            member x.Samples = x.Samples

        interface IRenderbuffer with
            member x.Handle = uint64 x.Handle
            member x.Name with get() = x.Name and set name = x.Name <- name
            member x.Dispose() = x.Dispose()

        new (ctx : Context, handle : int, size : V2i, format : TextureFormat, samples : int, sizeInBytes : int64) =
            { Context = ctx; Handle = handle; Size = size; Format = format; Samples = samples; SizeInBytes = sizeInBytes; name = null }

        new (ctx : Context, handle : int, size : V2i, format : TextureFormat, samples : int) =
            let sizeInBytes = ResourceCounts.texSizeInBytes TextureDimension.Texture2D size.XYI format samples 1 1
            new Renderbuffer(ctx, handle, size, format, samples, sizeInBytes)
    end


[<AutoOpen>]
module RenderbufferExtensions =

    let private updateRenderbuffer (ctx : Context) (handle : int) (size : V2i) (format : TextureFormat) (samples : int) =
        if Vec.anyGreater size ctx.MaxRenderbufferSize then
            failf $"cannot create renderbuffer with size {size} (maximum is {ctx.MaxRenderbufferSize})"

        let samples =
            Image.validateSampleCount ctx ImageTarget.Renderbuffer format samples

        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, handle)
        GL.Check "could not bind renderbuffer"

        if samples > 1 then
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, TextureFormat.toRenderbufferStorage format, size.X, size.Y)
        else
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, TextureFormat.toRenderbufferStorage format, size.X, size.Y)

        GL.Check $"failed to allocate renderbuffer storage (format = {format}, size = {size}, samples = {samples})"

        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
        GL.Check "could not unbind renderbuffer"

        samples

    type Context with
        member x.CreateRenderbuffer (size : V2i, format : TextureFormat, ?samples : int) =
            let samples = defaultArg samples 1

            using x.ResourceLock (fun _ ->
                let handle = GL.GenRenderbuffer()
                GL.Check "could not create renderbuffer"

                let samples = updateRenderbuffer x handle size format samples

                let rb = new Renderbuffer(x, handle, size, format, samples)
                ResourceCounts.addRenderbuffer x rb.SizeInBytes
                rb
            )

        member x.Update(r : Renderbuffer, size : V2i, format : TextureFormat, ?samples : int) =
            let samples = defaultArg samples 1

            if r.Size <> size || r.Format <> format || r.Samples <> samples then
                using x.ResourceLock (fun _ ->
                    let samples = updateRenderbuffer x r.Handle size format samples
                    let sizeInBytes = ResourceCounts.texSizeInBytes TextureDimension.Texture2D size.XYI format samples 1 1

                    ResourceCounts.resizeRenderbuffer x r.SizeInBytes sizeInBytes
                    r.SizeInBytes <- sizeInBytes
                    r.Size <- size
                    r.Format <- format
                    r.Samples <- samples
                )

        member x.Delete(r : Renderbuffer) =
            using x.ResourceLock (fun _ ->
                GL.DeleteRenderbuffer(r.Handle)
                ResourceCounts.removeRenderbuffer x r.SizeInBytes
                GL.Check "could not delete renderbuffer"
            )