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

type Renderbuffer =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Size : V2i
        val mutable public Format : RenderbufferFormat
        val mutable public Samples : int
        val mutable public SizeInBytes : int64

        interface IContextChild with
            member x.Context = x.Context
            member x.Handle = x.Handle

        interface IFramebufferOutput with
            member x.Format = x.Format
            member x.Size = x.Size
            member x.Samples = x.Samples

        interface IRenderbuffer with
            member x.Runtime = x.Context.Runtime :> ITextureRuntime
            member x.Handle = x.Handle :> obj

        new (ctx : Context, handle : int, size : V2i, format : RenderbufferFormat, samples : int, sizeInBytes : int64) =
            { Context = ctx; Handle = handle; Size = size; Format = format; Samples = samples; SizeInBytes = sizeInBytes }
    end


[<AutoOpen>]
module RenderbufferExtensions =


    let private addRenderbuffer (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.RenderBufferCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,size) |> ignore

    let private removeRenderbuffer(ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.RenderBufferCount)  |> ignore
        Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,-size) |> ignore

    let private resizeRenderbuffer(ctx:Context) oldSize newSize =
        Interlocked.Add(&ctx.MemoryUsage.RenderBufferMemory,newSize - oldSize) |> ignore


    let private lookup (name : string) (l : list<'a * 'b>) =
        let d = Dict.empty
        for (k,v) in l do
            match d.TryGetValue k with
                | (true, vo) -> printfn "duplicated entry in %s: %A (%A, %A)" name k vo v
                | _ -> ()
            d.[k] <- v

        fun a ->
            match d.TryGetValue a with
                | (true, b) -> b
                | _ -> failwithf "unknown %s: %A" name a  

    let private storageFormat =
        lookup "RenderBufferFormat" [
            RenderbufferFormat.DepthComponent, RenderbufferStorage.DepthComponent
            RenderbufferFormat.R3G3B2, RenderbufferStorage.R3G3B2
            RenderbufferFormat.Rgb4, RenderbufferStorage.Rgb4
            RenderbufferFormat.Rgb5, RenderbufferStorage.Rgb5
            RenderbufferFormat.Rgb8, RenderbufferStorage.Rgb8
            RenderbufferFormat.Rgb10, RenderbufferStorage.Rgb10
            RenderbufferFormat.Rgb12, RenderbufferStorage.Rgb12
            RenderbufferFormat.Rgb16, RenderbufferStorage.Rgb16
            RenderbufferFormat.Rgba2, RenderbufferStorage.Rgba2
            RenderbufferFormat.Rgba4, RenderbufferStorage.Rgba4
            RenderbufferFormat.Rgba8, RenderbufferStorage.Rgba8
            RenderbufferFormat.Rgb10A2, RenderbufferStorage.Rgb10A2
            RenderbufferFormat.Rgba12, RenderbufferStorage.Rgba12
            RenderbufferFormat.Rgba16, RenderbufferStorage.Rgba16
            RenderbufferFormat.DepthComponent16, RenderbufferStorage.DepthComponent16
            RenderbufferFormat.DepthComponent24, RenderbufferStorage.DepthComponent24
            RenderbufferFormat.DepthComponent32, RenderbufferStorage.DepthComponent32
            RenderbufferFormat.R8, RenderbufferStorage.R8
            RenderbufferFormat.R16, RenderbufferStorage.R16
            RenderbufferFormat.Rg8, RenderbufferStorage.Rg8
            RenderbufferFormat.Rg16, RenderbufferStorage.Rg16
            RenderbufferFormat.R16f, RenderbufferStorage.R16f
            RenderbufferFormat.R32f, RenderbufferStorage.R32f
            RenderbufferFormat.Rg16f, RenderbufferStorage.Rg16f
            RenderbufferFormat.Rg32f, RenderbufferStorage.Rg32f
            RenderbufferFormat.R8i, RenderbufferStorage.R8i
            RenderbufferFormat.R8ui, RenderbufferStorage.R8ui
            RenderbufferFormat.R16i, RenderbufferStorage.R16i
            RenderbufferFormat.R16ui, RenderbufferStorage.R16ui
            RenderbufferFormat.R32i, RenderbufferStorage.R32i
            RenderbufferFormat.R32ui, RenderbufferStorage.R32ui
            RenderbufferFormat.Rg8i, RenderbufferStorage.Rg8i
            RenderbufferFormat.Rg8ui, RenderbufferStorage.Rg8ui
            RenderbufferFormat.Rg16i, RenderbufferStorage.Rg16i
            RenderbufferFormat.Rg16ui, RenderbufferStorage.Rg16ui
            RenderbufferFormat.Rg32i, RenderbufferStorage.Rg32i
            RenderbufferFormat.Rg32ui, RenderbufferStorage.Rg32ui
            RenderbufferFormat.DepthStencil, RenderbufferStorage.DepthStencil
            RenderbufferFormat.Rgba32f, RenderbufferStorage.Rgba32f
            RenderbufferFormat.Rgb32f, RenderbufferStorage.Rgb32f
            RenderbufferFormat.Rgba16f, RenderbufferStorage.Rgba16f
            RenderbufferFormat.Rgb16f, RenderbufferStorage.Rgb16f
            RenderbufferFormat.Depth24Stencil8, RenderbufferStorage.Depth24Stencil8
            RenderbufferFormat.R11fG11fB10f, RenderbufferStorage.R11fG11fB10f
            RenderbufferFormat.Rgb9E5, RenderbufferStorage.Rgb9E5
            RenderbufferFormat.Srgb8, RenderbufferStorage.Srgb8
            RenderbufferFormat.Srgb8Alpha8, RenderbufferStorage.Srgb8Alpha8
            RenderbufferFormat.DepthComponent32f, RenderbufferStorage.DepthComponent32f
            RenderbufferFormat.Depth32fStencil8, RenderbufferStorage.Depth32fStencil8
            RenderbufferFormat.StencilIndex1, RenderbufferStorage.StencilIndex1
            RenderbufferFormat.StencilIndex4, RenderbufferStorage.StencilIndex4
            RenderbufferFormat.StencilIndex8, RenderbufferStorage.StencilIndex8
            RenderbufferFormat.StencilIndex16, RenderbufferStorage.StencilIndex16
            RenderbufferFormat.Rgba32ui, RenderbufferStorage.Rgba32ui
            RenderbufferFormat.Rgb32ui, RenderbufferStorage.Rgb32ui
            RenderbufferFormat.Rgba16ui, RenderbufferStorage.Rgba16ui
            RenderbufferFormat.Rgb16ui, RenderbufferStorage.Rgb16ui
            RenderbufferFormat.Rgba8ui, RenderbufferStorage.Rgba8ui
            RenderbufferFormat.Rgb8ui, RenderbufferStorage.Rgb8ui
            RenderbufferFormat.Rgba32i, RenderbufferStorage.Rgba32i
            RenderbufferFormat.Rgb32i, RenderbufferStorage.Rgb32i
            RenderbufferFormat.Rgba16i, RenderbufferStorage.Rgba16i
            RenderbufferFormat.Rgb16i, RenderbufferStorage.Rgb16i
            RenderbufferFormat.Rgba8i, RenderbufferStorage.Rgba8i
            RenderbufferFormat.Rgb8i, RenderbufferStorage.Rgb8i
            RenderbufferFormat.Rgb10A2ui, RenderbufferStorage.Rgb10A2ui
        ]



    let private updateRenderbuffer (handle : int) (size : V2i) (format : RenderbufferStorage) (samples : int) =
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
        member x.CreateRenderbuffer (size : V2i, format : RenderbufferFormat, ?samples : int) =
            let samples = defaultArg samples 1

            using x.ResourceLock (fun _ ->

                let handle = GL.GenRenderbuffer()
                GL.Check "could not create renderbuffer"

                let storageFormat = storageFormat format
                updateRenderbuffer handle size storageFormat samples
                
                let sizeInBytes = (int64 size.X * int64 size.Y * int64 (RenderbufferStorage.getSizeInBits storageFormat)) / 8L
                addRenderbuffer x sizeInBytes
                Renderbuffer(x, handle, size, format, samples, sizeInBytes)
            )

//        member x.CreateRenderbuffer (size : V2i, format : ChannelType, ?samples : int) =
//            match samples with
//                | Some s -> x.CreateRenderbuffer(size, toRenderbufferFormat format, s)
//                | None -> x.CreateRenderbuffer(size, toRenderbufferFormat format)

        member x.Update(r : Renderbuffer, size : V2i, format : RenderbufferFormat, ?samples : int) =
            let samples = defaultArg samples 1

            if r.Size <> size || r.Format <> format || r.Samples <> samples then
                using x.ResourceLock (fun _ ->
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, r.Handle)
                    GL.Check "could not bind renderbuffer"
                    let storageFormat = storageFormat format
                    match samples with
                        | 1 ->
                            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, storageFormat, size.X, size.Y)
                        | sam ->
                            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, sam, storageFormat, size.X, size.Y)
                    GL.Check "could not set renderbuffer storage"

                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)
                    GL.Check "could not unbind renderbuffer"

                    let sizeInBytes = (int64 size.X * int64 size.Y * int64 (RenderbufferStorage.getSizeInBits storageFormat)) / 8L 

                    resizeRenderbuffer x r.SizeInBytes sizeInBytes
                    r.SizeInBytes <- sizeInBytes
                    r.Size <- size
                    r.Format <- format
                    r.Samples <- samples
                )

        member x.Delete(r : Renderbuffer) =
            using x.ResourceLock (fun _ ->
                GL.DeleteRenderbuffer(r.Handle)
                removeRenderbuffer x r.SizeInBytes
                GL.Check "could not delete renderbuffer"
            )