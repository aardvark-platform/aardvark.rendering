namespace Aardvark.Rendering.GL

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

type StreamingTextureOld(ctx : Context, mipMap : bool) =
    inherit AVal.AbstractVal<ITexture>()

    let expectedLevels (size : V2i) = 
        if mipMap then Fun.MipmapLevels(size)
        else 1

    let mutable pbo = 0
    let mutable pboSize = 0n
   

    let mutable texA = 
        use t = ctx.ResourceLock
        let handle = GL.GenTexture()
        new Texture(ctx, handle, TextureDimension.Texture2D, 1, 1, V3i.Zero, None, TextureFormat.Rgba8, 0L)

    let mutable texB = 
        use t = ctx.ResourceLock
        let handle = GL.GenTexture()
        new Texture(ctx, handle, TextureDimension.Texture2D, 1, 1, V3i.Zero, None, TextureFormat.Rgba8, 0L)

    let mutable texC = 
        use t = ctx.ResourceLock
        let handle = GL.GenTexture()
        new Texture(ctx, handle, TextureDimension.Texture2D, 1, 1, V3i.Zero, None, TextureFormat.Rgba8, 0L)

    let mutable fenceAB = 0n
    let mutable fenceC = 0n

    let mutable currentFormat = PixFormat(typeof<obj>, Col.Format.RGBA)
    let mutable currentSize = -V2i.II

    let mutable textureFormat = TextureFormat.R8
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
            pixelType <- PixelType.ofType f.Type
            pixelFormat <- PixelFormat.ofColFormat textureFormat.IsIntegerFormat f.Format
            channels <- PixelFormat.channels pixelFormat
            channelSize <- PixelType.size pixelType

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
            GL.TexImage2D(TextureTarget.Texture2D, 0, TextureFormat.toPixelInternalFormat textureFormat, size.X, size.Y, 0, pixelFormat, pixelType, 0n)
            GL.Check "could not update texture"
            texA.Size <- V3i(size.X, size.Y, 1)
            texA.Format <- textureFormat
            texA.MipMapLevels <- mipMapLevels
            texA.Count <- 1
            texA.Dimension <- TextureDimension.Texture2D
            texA.Multisamples <- 1

            let newSize = if mipMap then (int64 bufferSize * 4L) / 3L else int64 bufferSize
            ResourceCounts.updateTexture ctx texA.SizeInBytes newSize
            texA.SizeInBytes <- newSize


        if mipMapLevels > 1 then 
            ctx.CheckMipmapGenerationSupport(ImageTarget.Texture2D, textureFormat)
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
        textureFormat <- TextureFormat.R8
        pixelType <- PixelType.UnsignedByte
        pixelFormat <- PixelFormat.Alpha
        channels <- 0
        channelSize <- 0
        mipMapLevels <- 0
        bufferSize <- 0n



    override x.Compute(token) =
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
module private PixelBufferExtensions =
    
    type PixelBuffer =
        class
            val mutable public Context : Context
            val mutable public Handle : int
            val mutable public Size : nativeint
            val mutable public ImageSize : V2i
    
            val mutable public PixelType : PixelType
            val mutable public PixelFormat : PixelFormat
            val mutable public InternalFormat : PixelInternalFormat
    
            val mutable public Sync : nativeint
    
            new(ctx : Context, handle : int, size : nativeint, s : V2i, pt, pf, pif) =
                { Context = ctx; Handle = handle; Size = size; ImageSize = s; PixelType = pt; PixelFormat = pf; InternalFormat = pif; Sync = 0n }
        end

    type Context with
        member x.CreatePixelBuffer(imageSize : V2i, size : nativeint, pixelType : PixelType, pixelFormat : PixelFormat, ifmt : TextureFormat) =
            use t = x.ResourceLock
            let handle = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, handle)
            GL.Check "could not bind buffer"
            GL.BufferData(BufferTarget.PixelUnpackBuffer, size, 0n, BufferUsageHint.StreamDraw)
            GL.Check "could not set buffer data"
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.Check "could not unbind buffer"

            PixelBuffer(x, handle, size, imageSize, pixelType, pixelFormat, TextureFormat.toPixelInternalFormat ifmt)
        
        member x.Update(pbo : PixelBuffer, imageSize : V2i, size : nativeint, pixelType : PixelType, pixelFormat : PixelFormat, ifmt : TextureFormat) =
            if pbo.Handle > 0 then
                if size <> pbo.Size then
                    use t = x.ResourceLock
                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo.Handle)
                    GL.Check "could not bind buffer"
                    GL.BufferData(BufferTarget.PixelUnpackBuffer, size, 0n, BufferUsageHint.StreamDraw)
                    GL.Check "could not set buffer data"
                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                    GL.Check "could not unbind buffer"
                    pbo.Size <- size

                pbo.ImageSize <- imageSize
                pbo.PixelFormat <- pixelFormat
                pbo.PixelType <- pixelType
                pbo.InternalFormat <- TextureFormat.toPixelInternalFormat ifmt
            else
                let res = x.CreatePixelBuffer(imageSize, size, pixelType, pixelFormat, ifmt)
                pbo.Handle <- res.Handle
                pbo.ImageSize <- imageSize
                pbo.Size <- size
                pbo.Context <- x
                pbo.PixelType <- res.PixelType
                pbo.PixelFormat <- res.PixelFormat
                pbo.InternalFormat <- res.InternalFormat

        member x.Delete(pbo : PixelBuffer) =
            if pbo.Handle > 0 then
                use t = x.ResourceLock
                GL.DeleteBuffer(pbo.Handle)
                pbo.Handle <- 0
                pbo.Size <- 0n
                pbo.Handle <- 0
                pbo.ImageSize <- V2i.Zero


type StreamingTexture(ctx : Context, mipMap : bool) =
    inherit AVal.AbstractVal<ITexture>()

    let expectedLevels (size : V2i) = 
        if mipMap then Fun.MipmapLevels(size)
        else 1


    let texture =
        use t = ctx.ResourceLock
        let handle = GL.GenTexture()
        new Texture(ctx, handle, TextureDimension.Texture2D, 1, 1, V3i.Zero, None, TextureFormat.Rgba8, 0L)

    let swapLock = obj()
    let mutable ping = ctx.CreatePixelBuffer(V2i.Zero, 0n, PixelType.UnsignedByte, PixelFormat.Bgra, TextureFormat.Rgba8)
    let mutable pong = ctx.CreatePixelBuffer(V2i.Zero, 0n, PixelType.UnsignedByte, PixelFormat.Bgra, TextureFormat.Rgba8)

    let mutable currentFormat = PixFormat(typeof<obj>, Col.Format.RGBA)
    let mutable currentSize = -V2i.II
    let mutable textureFormat = TextureFormat.R8
    let mutable pixelType = PixelType.UnsignedByte
    let mutable pixelFormat = PixelFormat.Alpha
    let mutable channels = 0
    let mutable channelSize = 0
    let mutable mipMapLevels = 0
    let mutable bufferSize = 0n

    let watch = System.Diagnostics.Stopwatch()
    let mutable iter = 0

    let upload (f : PixFormat) (size : V2i) (data : nativeint) =
        // update format depenent things
        if f <> currentFormat then
            currentFormat <- f
            textureFormat <- TextureFormat.ofPixFormat f TextureParams.empty

            pixelType <- PixelType.ofType f.Type
            pixelFormat <- PixelFormat.ofColFormat textureFormat.IsIntegerFormat f.Format
            channels <- PixelFormat.channels pixelFormat
            channelSize <- PixelType.size pixelType

        // update size depenent things
        if currentSize <> size then
            currentSize <- size
            mipMapLevels <- expectedLevels size
            bufferSize <- size.X * size.Y * channels * channelSize |> nativeint

        use t = ctx.ResourceLock

        if ping.Sync <> 0n then
            GL.DeleteSync(ping.Sync)

        ctx.Update(ping, size, bufferSize, pixelType, pixelFormat, textureFormat)

        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, ping.Handle)
        GL.Check "could not bind buffer"
        let target = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, bufferSize, BufferAccessMask.MapInvalidateBufferBit ||| BufferAccessMask.MapWriteBit)
        if target = 0n then failwithf "[GL] could not map PBO"

        Marshal.Copy(data, target, bufferSize)

        if not <| GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) then
            failwithf "[GL] could not unmap PBO"

        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
        GL.Check "could not unbind buffer"

        ping.Sync <- GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None)

        lock swapLock (fun () ->
            Fun.Swap(&ping, &pong)
        )

    member x.Context = ctx

    member x.UpdateAsync(f : PixFormat, size : V2i, data : nativeint) =
        upload f size data
        let t = new Transaction()
        t.Enqueue x
        t

    member x.Update(f : PixFormat, size : V2i, data : nativeint) =
        upload f size data
        transact (fun () -> x.MarkOutdated())

    member x.ReadPixel(pos : V2i) =
        lock swapLock (fun () ->
            let pbo = pong
            let size = pbo.ImageSize
            if pos.X < 0 || pos.Y < 0 || pos.X >= size.X || pos.Y >= size.Y then
                C4f(0.0f, 0.0f, 0.0f, 0.0f)
            else
                use token = ctx.ResourceLock

                let pixelSize = channels * channelSize
                let offset = pixelSize * (pos.X + pos.Y * currentSize.X)
            
                let data : byte[] = Array.zeroCreate pixelSize
                let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            
                try
                    GL.BindBuffer(BufferTarget.CopyReadBuffer, pbo.Handle)
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
        ctx.Delete(ping)
        ctx.Delete(pong)
        ctx.Delete(texture)
        currentFormat <- PixFormat(typeof<obj>, Col.Format.RGBA)
        currentSize <- -V2i.II
        textureFormat <- TextureFormat.R8
        pixelType <- PixelType.UnsignedByte
        pixelFormat <- PixelFormat.Alpha
        channels <- 0
        channelSize <- 0
        mipMapLevels <- 0
        bufferSize <- 0n
 

    override x.Compute(token) =
        use t = ctx.ResourceLock

        if iter = 60 then
            Log.warn "took: %A" (watch.MicroTime / iter)
            iter <- 0
            watch.Reset()

        watch.Start()
        iter <- iter + 1

        lock swapLock (fun () ->
            let pbo = pong

            if pbo.Sync <> 0n then
                GL.ClientWaitSync(pbo.Sync, ClientWaitSyncFlags.SyncFlushCommandsBit, ~~~0L) |> ignore
                GL.Check "could not wait on sync object"
                GL.DeleteSync(pbo.Sync)
                pbo.Sync <- 0n

            GL.BindTexture(TextureTarget.Texture2D, texture.Handle)
            GL.Check "could not bind texture"

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo.Handle)
            GL.Check "could not bind buffer"

            if pbo.ImageSize <> texture.Size2D then
                GL.TexImage2D(TextureTarget.Texture2D, 0, pbo.InternalFormat, pbo.ImageSize.X, pbo.ImageSize.Y, 0, pbo.PixelFormat, pbo.PixelType, 0n)
                texture.Size <- V3i(pbo.ImageSize.X, pbo.ImageSize.Y, 1)

            else
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, pbo.ImageSize.X, pbo.ImageSize.Y, pbo.PixelFormat, pbo.PixelType, 0n)
            GL.Check "could not set texture data"
            
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.Check "could not unbind buffer"

            if mipMap then
                ctx.CheckMipmapGenerationSupport(ImageTarget.Texture2D, textureFormat)
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "could not generate mip map"

            GL.BindTexture(TextureTarget.Texture2D, 0)
            GL.Check "could not unbind texture"

            GL.Sync()
        )

        
        watch.Stop()

        texture :> ITexture

    interface IStreamingTexture with
        member x.Update(f,s,d) = x.Update(f,s,d)
        member x.UpdateAsync(f,s,d) = x.UpdateAsync(f,s,d)
        member x.ReadPixel(pos) = x.ReadPixel(pos)
        member x.Dispose() = x.Dispose()



[<AutoOpen>]
module StreamingTextureExtensions =

    type Context with
        member x.CreateStreamingTexture(mipMaps : bool) =
            Operators.using x.ResourceLock (fun _ ->
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