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

type StreamingTextureOld(ctx : Context, handle : int, mipMap : bool) =
    inherit AdaptiveObject()

    let expectedLevels (size : V2i) = 
        if mipMap then Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int
        else 1

    let texture = Texture(ctx, handle, TextureDimension.Texture2D, expectedLevels V2i.II, 1, V3i(1,1,0), 1, TextureFormat.R11fG11fB10f, 0L, false)
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

    static let mark (self : StreamingTextureOld) =
        let action = 
            lock self (fun () ->
                if not self.OutOfDate then
                    match Transaction.Running with
                        | Some t -> 
                            t.Enqueue(self)
                            id
                        | _ ->
                            let t = new Transaction()
                            t.Enqueue(self)
                            fun () -> t.Commit()
                else id
            )
        async { action() }


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
        member x.UpdateAsync(fmt, size, data) = x.Update(fmt, size, data); new Transaction()
        member x.ReadPixel(pos) = x.ReadPixel(pos).ToC4f()

    member x.Update(f : PixFormat, s : V2i, data : nativeint) =
        let pbo = swapPBOs()
        lock pbo (fun () ->
            using ctx.ResourceLock (fun _ ->
                uploadUsingPBO pbo f s data
            )
        )
        mark x |> Async.RunSynchronously

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

type StreamingTexture(ctx : Context, mipMap : bool) =
    inherit Mod.AbstractMod<ITexture>()

    let expectedLevels (size : V2i) = 
        if mipMap then Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int
        else 1

    let mutable pbo = 0
    let mutable pboSize = 0n
   

    let mutable texA = 
        use t = ctx.ResourceLock
        let handle = GL.GenTexture()
        Texture(ctx, handle, TextureDimension.Texture2D, 1, 1, V3i.Zero, 1, TextureFormat.Rgba8, 0L, false)

    let mutable texB = 
        use t = ctx.ResourceLock
        let handle = GL.GenTexture()
        Texture(ctx, handle, TextureDimension.Texture2D, 1, 1, V3i.Zero, 1, TextureFormat.Rgba8, 0L, false)

    let mutable texC = 
        use t = ctx.ResourceLock
        let handle = GL.GenTexture()
        Texture(ctx, handle, TextureDimension.Texture2D, 1, 1, V3i.Zero, 1, TextureFormat.Rgba8, 0L, false)

    let mutable fenceAB = 0n
    let mutable fenceC = 0n

    let mutable currentFormat = PixFormat(typeof<obj>, Col.Format.RGBA)
    let mutable currentSize = -V2i.II

    let mutable textureFormat = TextureFormat.Alpha
    let mutable pixelType = PixelType.UnsignedByte
    let mutable pixelFormat = PixelFormat.Alpha
    let mutable channels = 0
    let mutable channelSize = 0

    let mutable mipMapLevels = 0
    let mutable bufferSize = 0n

    let swapLock = obj()

    let upload (f : PixFormat) (size : V2i) (data : nativeint) =
        use token = ctx.ResourceLock

        // update format depenent things
        if f <> currentFormat then
            currentFormat <- f
            textureFormat <- TextureFormat.ofPixFormat f TextureParams.empty
            let pf, pt = TextureFormat.toFormatAndType textureFormat
            pixelType <- toPixelType f.Type |> Option.get
            pixelFormat <- toPixelFormat f.Format |> Option.get
            channels <- PixelFormat.channels pf
            channelSize <- PixelType.size pt

        // update size depenent things
        if currentSize <> size then
            currentSize <- size
            mipMapLevels <- expectedLevels size
            bufferSize <- size.X * size.Y * channels * channelSize |> nativeint

        // create the pbo if necessary
        if pbo = 0 then pbo <- GL.GenBuffer()
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
        GL.Check "could not bind PBO"

        if fenceAB <> 0n then
            GL.ClientWaitSync(fenceAB, ClientWaitSyncFlags.SyncFlushCommandsBit, ~~~0L) |> ignore
            GL.DeleteSync(fenceAB)
            fenceAB <- 0n

        // update its size if necessary
        if pboSize <> bufferSize then
            pboSize <- bufferSize
            GL.BufferData(BufferTarget.PixelUnpackBuffer, bufferSize, 0n, BufferUsageHint.DynamicDraw)
            GL.Check "could not resize PBO"

        // upload the data to the pbo
        let ptr = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n , bufferSize, BufferAccessMask.MapWriteBit)
        if ptr = 0n then failwithf "[GL] could not map PBO"
        Marshal.Copy(data, ptr, bufferSize)
        if not (GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer)) then
            failwithf "[GL] could not unmap PBO"


        // copy pbo to texture
        GL.BindTexture(TextureTarget.Texture2D, texA.Handle)
        GL.Check "could not bind texture"


        if texA.Size2D = size && texA.Format = textureFormat && texA.MipMapLevels = mipMapLevels then
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, size.X, size.Y, pixelFormat, pixelType, 0n)
            GL.Check "could not update texture"
        else
            GL.TexImage2D(TextureTarget.Texture2D, 0, unbox (int textureFormat), size.X, size.Y, 0, pixelFormat, pixelType, 0n)
            GL.Check "could not update texture"
            texA.Size <- V3i(size.X, size.Y, 1)
            texA.Format <- textureFormat
            texA.MipMapLevels <- mipMapLevels
            texA.ImmutableFormat <- false
            texA.Count <- 1
            texA.Dimension <- TextureDimension.Texture2D
            texA.Multisamples <- 1

            let newSize = if mipMap then (int64 bufferSize * 4L) / 3L else int64 bufferSize
            updateTexture ctx texA.SizeInBytes newSize
            texA.SizeInBytes <- newSize


        if mipMapLevels > 1 then 
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
            GL.Check "could not generate mipmaps"
        GL.BindTexture(TextureTarget.Texture2D, 0)
        GL.Check "could not unbind texture"

        // unbind the pbo
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
        GL.Check "could not unbind PBO"
        let fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None)
        fenceAB <- fence

        lock swapLock (fun () -> 
            Fun.Swap(&texA, &texB)
        )

    member x.Context = ctx

    member x.UpdateAsync(f : PixFormat, size : V2i, data : nativeint) =
        lock x (fun () -> upload f size data)
        let t = new Transaction()
        t.Enqueue x
        t

    member x.Update(f : PixFormat, size : V2i, data : nativeint) =
        lock x (fun () -> upload f size data)
        
        transact (fun () -> x.MarkOutdated())

    member x.ReadPixel(pos : V2i) =
        lock x (fun () ->
            if pbo = 0 || pos.X < 0 || pos.Y < 0 || pos.X >= currentSize.X || pos.Y >= currentSize.Y then
                C4f(0.0f, 0.0f, 0.0f, 0.0f)
            else
                use token = ctx.ResourceLock

                let pixelSize = channels * channelSize
                let offset = pixelSize * (pos.X + pos.Y * currentSize.X)
            
                let data : byte[] = Array.zeroCreate pixelSize
                let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            
                try
                    GL.BindBuffer(BufferTarget.CopyReadBuffer, pbo)
                    GL.Check "could not bind PBO"

                    GL.GetBufferSubData(BufferTarget.CopyReadBuffer, nativeint offset, nativeint pixelSize, gc.AddrOfPinnedObject())
                    GL.Check "could not read pixel from PBO"

                    GL.BindBuffer(BufferTarget.CopyReadBuffer, 0)
                    GL.Check "could not unbind PBO"
                finally
                    gc.Free()

                match pixelType with
                    | PixelType.UnsignedByte ->
                        match pixelFormat with
                            | PixelFormat.Rgba              -> C4b(data.[0], data.[1], data.[2], data.[3]).ToC4f()
                            | PixelFormat.Bgra              -> C4b(data.[2], data.[1], data.[0], data.[3]).ToC4f()
                            | PixelFormat.Rgb               -> C4b(data.[0], data.[1], data.[2], 255uy).ToC4f()
                            | PixelFormat.Bgr               -> C4b(data.[2], data.[1], data.[0], 255uy).ToC4f()
                            | PixelFormat.Luminance         -> C4b(data.[0], data.[0], data.[0], 255uy).ToC4f()
                            | PixelFormat.Alpha             -> C4b(0uy, 0uy, 0uy, data.[0]).ToC4f()
                            | PixelFormat.Red               -> C4b(data.[0], 0uy, 0uy, 255uy).ToC4f()
                            | PixelFormat.Green             -> C4b(0uy, data.[0], 0uy, 255uy).ToC4f()
                            | PixelFormat.Blue              -> C4b(0uy, 0uy, data.[0], 255uy).ToC4f()
                            | PixelFormat.LuminanceAlpha    -> C4b(data.[0], data.[0], data.[0], data.[1]).ToC4f()
                            | _                             -> failwithf "[GL] unsupported format %A" pixelFormat

                    | PixelType.UnsignedShort ->
                        let data = data.UnsafeCoerce<uint16>()
                        match pixelFormat with
                            | PixelFormat.Rgba              -> C4us(data.[0], data.[1], data.[2], data.[3]).ToC4f()
                            | PixelFormat.Bgra              -> C4us(data.[2], data.[1], data.[0], data.[3]).ToC4f()
                            | PixelFormat.Rgb               -> C4us(data.[0], data.[1], data.[2], 65535us).ToC4f()
                            | PixelFormat.Bgr               -> C4us(data.[2], data.[1], data.[0], 65535us).ToC4f()
                            | PixelFormat.Luminance         -> C4us(data.[0], data.[0], data.[0], 65535us).ToC4f()
                            | PixelFormat.Alpha             -> C4us(0us, 0us, 0us, data.[0]).ToC4f()
                            | PixelFormat.Red               -> C4us(data.[0], 0us, 0us, 65535us).ToC4f()
                            | PixelFormat.Green             -> C4us(0us, data.[0], 0us, 65535us).ToC4f()
                            | PixelFormat.Blue              -> C4us(0us, 0us, data.[0], 65535us).ToC4f()
                            | PixelFormat.LuminanceAlpha    -> C4us(data.[0], data.[0], data.[0], data.[1]).ToC4f()
                            | _                             -> failwithf "[GL] unsupported format %A" pixelFormat



                    | _ ->
                        failwithf "[GL] unsupported type %A" pixelType
        )

    member x.Dispose() =
        use token = ctx.ResourceLock
        ctx.Delete(texA)
        ctx.Delete(texB)
        ctx.Delete(texC)
        if pbo <> 0 then
            pbo <- 0
            GL.DeleteBuffer(pbo)

        pboSize <- 0n
        currentFormat <- PixFormat(typeof<obj>, Col.Format.RGBA)
        currentSize <- -V2i.II
        textureFormat <- TextureFormat.Alpha
        pixelType <- PixelType.UnsignedByte
        pixelFormat <- PixelFormat.Alpha
        channels <- 0
        channelSize <- 0
        mipMapLevels <- 0
        bufferSize <- 0n



    override x.Compute() =
        lock swapLock (fun () -> 
            Fun.Swap(&texB, &texC)
            Fun.Swap(&fenceAB, &fenceC)
        )
        use t = ctx.ResourceLock
        GL.ClientWaitSync(fenceC, ClientWaitSyncFlags.SyncFlushCommandsBit, ~~~0L) |> ignore
        GL.DeleteSync(fenceC)
        fenceC <- 0n

        texC :> ITexture

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IStreamingTexture with
        member x.Update(f,s,d) = x.Update(f,s,d)
        member x.UpdateAsync(f,s,d) = x.UpdateAsync(f,s,d)
        member x.ReadPixel(pos) = x.ReadPixel(pos)
    



[<AutoOpen>]
module StreamingTextureExtensions =

    type Context with
        member x.CreateStreamingTexture(mipMaps : bool) =
            using x.ResourceLock (fun _ ->
                new StreamingTexture(x, mipMaps)
            )

        member x.CreateStreamingTexture() =
            x.CreateStreamingTexture(false)

        member x.Delete(t : StreamingTexture) =
            t.Dispose()

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