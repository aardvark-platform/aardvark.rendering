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


    let private updateRenderbuffer (handle : int) (size : V2i) (format : TextureFormat) (samples : int) =
        let format = unbox<RenderbufferStorage> format

        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, handle)
        GL.Check "could not bind renderbuffer"

        match samples with
            | 1 ->
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, format, size.X, size.Y)
            | sam ->
                GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, sam, format, size.X, size.Y)
        GL.Check "could not set renderbuffer storage"

        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
        GL.Check "could not unbind renderbuffer"

    type Context with
        member x.CreateRenderbuffer (size : V2i, format : TextureFormat, ?samples : int) =
            let samples = defaultArg samples 1

            using x.ResourceLock (fun _ ->

                let handle = GL.GenRenderbuffer()
                GL.Check "could not create renderbuffer"

                updateRenderbuffer handle size format samples

                let sizeInBytes = (int64 size.X * int64 size.Y * int64 format.PixelSizeInBits) / 8L
                ResourceCounts.addRenderbuffer x sizeInBytes
                Renderbuffer(x, handle, size, format, samples, sizeInBytes)
            )

        member x.Update(r : Renderbuffer, size : V2i, format : TextureFormat, ?samples : int) =
            let samples = defaultArg samples 1

            if r.Size <> size || r.Format <> format || r.Samples <> samples then
                using x.ResourceLock (fun _ ->
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, r.Handle)
                    GL.Check "could not bind renderbuffer"
                    let storageFormat = unbox<RenderbufferStorage> format
                    match samples with
                        | 1 ->
                            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, storageFormat, size.X, size.Y)
                        | sam ->
                            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, sam, storageFormat, size.X, size.Y)
                    GL.Check "could not set renderbuffer storage"

                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
                    GL.Check "could not unbind renderbuffer"

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