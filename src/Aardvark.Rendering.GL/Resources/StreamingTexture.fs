namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL


type private PixelBuffer = { handle : int; mutable sizeInBytes : int64 }

type StreamingTexture(ctx : Context, handle : int, mipMap : bool) =
    inherit AdaptiveObject()

    let expectedLevels (size : V2i) = 
        if mipMap then Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int
        else 1

    let texture = Texture(ctx, handle, TextureDimension.Texture2D, expectedLevels V2i.II, 1, V3i(1,1,0), 1, TextureFormat.R11fG11fB10f)
    let mutable size = V2i.Zero
    let mutable format = PixFormat()
    let mutable formatSize = 0
    let mutable pixelBuffers : PixelBuffer[] = null
    let mutable currentPixelBuffer = 0

    let createPBOs() =
        if pixelBuffers = null then
            using ctx.ResourceLock (fun _ ->
                pixelBuffers <- Array.init 3 (fun _ ->
                    let handle = GL.GenBuffer()
                    { handle = handle; sizeInBytes = 0L }
                )
            )

    let swapPBOs() =
        createPBOs()
        let res = pixelBuffers.[currentPixelBuffer]
        currentPixelBuffer <- (currentPixelBuffer + 1) % pixelBuffers.Length
        res

    let lastPBO() =
        createPBOs()
        pixelBuffers.[(currentPixelBuffer + pixelBuffers.Length - 1) % pixelBuffers.Length]

    let uploadUsingPBO (pbo : PixelBuffer) (fmt : PixFormat) (s : V2i) (data : nativeint) =
        match toPixelType fmt.Type, toPixelFormat fmt.Format with
            | Some pixelType, Some pixelFormat ->
                if fmt <> format then formatSize <- fmt.ChannelCount * Marshal.SizeOf fmt.Type
                let bufferSize = s.X * s.Y * formatSize |> int64

                let bufferResized = bufferSize <> pbo.sizeInBytes
                let textureResized = size <> s || fmt <> format
                size <- s
                format <- fmt  
                pbo.sizeInBytes <- bufferSize
                         
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo.handle)
                GL.Check "could not bind buffer"

                if bufferResized then
                    GL.BufferData(BufferTarget.PixelUnpackBuffer, nativeint bufferSize, data, BufferUsageHint.StreamDraw)
                    GL.Check "could not upload buffer"
                else
                    let ptr = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, nativeint bufferSize, BufferAccessMask.MapWriteBit ||| BufferAccessMask.MapInvalidateBufferBit)
                    GL.Check "could not map buffer"

                    data.CopyTo(ptr, int bufferSize)

                    GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore
                    GL.Check "could not unmap buffer"


                GL.BindTexture(TextureTarget.Texture2D, handle)
                GL.Check "could not bind texture"

                if textureResized then
                    let tfmt = TextureFormat.ofPixFormat fmt TextureParams.empty
                    GL.TexImage2D(TextureTarget.Texture2D, 0, unbox tfmt, s.X, s.Y, 0, pixelFormat, pixelType, 0n)
                    GL.Check "could not copy PBO to texture"

                    texture.Format <- tfmt
                    texture.Size <- V3i(size.X, size.Y, 1)
                    texture.MipMapLevels <- expectedLevels size
                else
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, s.X, s.Y, pixelFormat, pixelType, 0n)
                    GL.Check "could not copy PBO to texture"

                if mipMap then
                    GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)

                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.Check "could not unbind texture"

                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                GL.Check "could not unbind buffer"

            | _ ->
                failwithf "unsupported format: %A" fmt

    static let mark (self : StreamingTexture) =
        let action = 
            lock self (fun () ->
                if not self.OutOfDate then
                    match Transaction.Running with
                        | Some t -> 
                            t.Enqueue(self)
                            id
                        | _ ->
                            let t = Transaction()
                            t.Enqueue(self)
                            fun () -> t.Commit()
                else id
            )
        action()


    member x.Context = ctx
    member x.Handle = handle
    member x.MipMaps = mipMap
    member x.Texture = texture

    member x.GetValue(caller) =
        x.EvaluateAlways caller (fun () -> texture :> ITexture)

    interface IMod with
        member x.IsConstant = false
        member x.GetValue(caller) = x.GetValue(caller) :> obj

    interface IMod<ITexture> with
        member x.GetValue(caller) = x.GetValue(caller)

    interface IStreamingTexture with
        member x.Update(fmt, size, data) = x.Update(fmt, size, data)
        member x.ReadPixel(pos) = x.ReadPixel pos

    member x.Update(f : PixFormat, s : V2i, data : nativeint) =
        let pbo = swapPBOs()
        lock pbo (fun () ->
            using ctx.ResourceLock (fun _ ->
                uploadUsingPBO pbo f s data
            )
        )
        mark x

    member x.ReadPixel(pos : V2i) =
        let pbo = lastPBO()
        lock pbo (fun () ->
            using ctx.ResourceLock (fun _ ->
                let offset = formatSize * (pos.X + pos.Y * size.X)
            
                if offset >= 0 && offset < int pbo.sizeInBytes then
                    let data : byte[] = Array.zeroCreate 4
                    let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            
                    try
                        GL.BindBuffer(BufferTarget.CopyReadBuffer, pbo.handle)
                        GL.Check "could not bind PBO"

                        GL.GetBufferSubData(BufferTarget.CopyReadBuffer, nativeint offset, 4n, gc.AddrOfPinnedObject())
                        GL.Check "could not read pixel from PBO"

                        GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
                        GL.Check "could not unbind PBO"

                        C4b(data.[0], data.[1], data.[2], data.[3])
                    finally
                        gc.Free()
                else
                    C4b(0,0,0,0)
            )
        )

    member x.Release() =
        size <- V2i.Zero
        format <- PixFormat()
        formatSize <- 0
        currentPixelBuffer <- 0

        if pixelBuffers <> null then
            for p in pixelBuffers do
                GL.DeleteBuffer(p.handle)

            pixelBuffers <- null


[<AutoOpen>]
module StreamingTextureExtensions =

    type Context with
        member x.CreateStreamingTexture(mipMaps : bool) =
            using x.ResourceLock (fun _ ->
                let handle = GL.GenTexture()
                GL.Check "could not create streaming texture"

                StreamingTexture(x, handle, mipMaps)
            )

        member x.CreateStreamingTexture() =
            x.CreateStreamingTexture(false)

        member x.Delete(t : StreamingTexture) =
            using x.ResourceLock (fun _ ->
                t.Release()

                GL.DeleteTexture(t.Handle)
                GL.Check "could not delete streaming texture"
            )

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StreamingTexture =
    let create (ctx : Context) (mipMaps : bool) =
        ctx.CreateStreamingTexture(mipMaps)

    let update (format : PixFormat) (size : V2i) (data : nativeint) (t : StreamingTexture) =
        t.Update(format, size, data)

    let readPixel (pos : V2i) (t : StreamingTexture) =
        t.ReadPixel(pos)

    let delete (t : StreamingTexture) =
        t.Context.Delete t
//
//    let read (format : PixFormat) (level : int) (tex : StreamingTexture) : PixImage =
//        let arr = tex.Context.Download(tex.Texture, format, level)
//        arr.[0]