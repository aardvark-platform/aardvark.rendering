namespace Aardvark.Rendering.GL

open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

type Renderbuffer =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Size : V2i
        val mutable public Format : TextureFormat
        val mutable public Samples : int
        val mutable public SizeInBytes : int64

        interface IFramebufferOutput with
            member x.Runtime = x.Context.Runtime :> ITextureRuntime
            member x.Format = x.Format
            member x.Size = x.Size
            member x.Samples = x.Samples

        interface IRenderbuffer with
            member x.Handle = x.Handle :> obj

        new (ctx : Context, handle : int, size : V2i, format : TextureFormat, samples : int, sizeInBytes : int64) =
            { Context = ctx; Handle = handle; Size = size; Format = format; Samples = samples; SizeInBytes = sizeInBytes }
    end


[<AutoOpen>]
module RenderbufferExtensions =

    module private ResourceCounts =
        let addRenderbuffer (ctx:Context) size =
            Interlocked.Increment(&ctx.MemoryUsage.RenderBufferCount) |> ignore
            Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,size) |> ignore

        let removeRenderbuffer(ctx:Context) size =
            Interlocked.Decrement(&ctx.MemoryUsage.RenderBufferCount)  |> ignore
            Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,-size) |> ignore

        let resizeRenderbuffer(ctx:Context) oldSize newSize =
            Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,newSize - oldSize) |> ignore


    let private updateRenderbuffer (cxt : Context) (handle : int) (size : V2i) (format : TextureFormat) (samples : int) =
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, handle)
        GL.Check "could not bind renderbuffer"

        let samples =
            if samples > 1 then
                let counts = cxt.GetFormatSamples(ImageTarget.Renderbuffer, format)
                if counts.Contains samples then samples
                else
                    let max = Set.maxElement counts
                    Log.warn "[GL] cannot create %A render buffer with %d samples (using %d instead)" format samples max
                    max
            else
                1

        if samples > 1 then
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, unbox format, size.X, size.Y)
        else
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, unbox format, size.X, size.Y)
        GL.Check "could not set renderbuffer storage"

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

                let sizeInBytes = (int64 size.X * int64 size.Y * int64 format.PixelSizeInBits) / 8L
                ResourceCounts.addRenderbuffer x sizeInBytes
                Renderbuffer(x, handle, size, format, samples, sizeInBytes)
            )

        member x.Update(r : Renderbuffer, size : V2i, format : TextureFormat, ?samples : int) =
            let samples = defaultArg samples 1

            if r.Size <> size || r.Format <> format || r.Samples <> samples then
                using x.ResourceLock (fun _ ->
                    let samples = updateRenderbuffer x r.Handle size format samples
                    let sizeInBytes = (int64 size.X * int64 size.Y * int64 format.PixelSizeInBits) / 8L

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