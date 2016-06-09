namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL
open Aardvark.Base.NativeTensors
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type Texture =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Dimension : TextureDimension
        val mutable public Multisamples : int
        val mutable public Size : V3i
        val mutable public Count : int
        val mutable public Format : TextureFormat
        val mutable public MipMapLevels : int
        val mutable public SizeInBytes : int64
        val mutable public ImmutableFormat : bool

        member x.IsMultisampled = x.Multisamples > 1
        member x.IsArray = x.Count > 1

        member x.Size1D = x.Size.X
        member x.Size2D = x.Size.XY
        member x.Size3D = x.Size

        interface IContextChild with
            member x.Context = x.Context
            member x.Handle = x.Handle

        interface IBackendTexture with
            member x.WantMipMaps = x.MipMapLevels > 1
            member x.Dimension = x.Dimension
            member x.MipMapLevels = x.MipMapLevels
            member x.Handle = x.Handle :> obj
            member x.Size = x.Size
            member x.Count = x.Count
            member x.Format = x.Format
            member x.Samples = x.Multisamples

        member x.GetSize (level : int)  =
            if level = 0 then x.Size2D
            else 
                let level = Fun.Clamp(level, 0, x.MipMapLevels-1)
                let factor = 1 <<< level
                x.Size2D / factor

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int, size : V3i, count : int, format : TextureFormat, sizeInBytes : int64, immutable : bool) =
            { Context = ctx; Handle = handle; Dimension = dimension; MipMapLevels = mipMapLevels; Multisamples = multisamples; Size = size; Count = count; Format = format; SizeInBytes = sizeInBytes; ImmutableFormat = immutable }

    end


[<AutoOpen>]
module TextureExtensions =

    let private addTexture (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.TextureCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,size) |> ignore

    let private removeTexture (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.TextureCount)  |> ignore
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,-size) |> ignore

    let private updateTexture (ctx:Context) oldSize newSize =
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,newSize-oldSize) |> ignore


    // PositiveX = 0,
    // NegativeX = 1,
    // PositiveY = 2,
    // NegativeY = 3,
    // PositiveZ = 4,
    // NegativeZ = 5,
    // cubeSides are sorted like in their implementation (making some things easier)
    let cubeSides =
        [|
            CubeSide.PositiveX, TextureTarget.TextureCubeMapPositiveX
            CubeSide.NegativeX, TextureTarget.TextureCubeMapNegativeX

            CubeSide.PositiveY, TextureTarget.TextureCubeMapNegativeY
            CubeSide.NegativeY, TextureTarget.TextureCubeMapPositiveY
                
            CubeSide.PositiveZ, TextureTarget.TextureCubeMapPositiveZ
            CubeSide.NegativeZ, TextureTarget.TextureCubeMapNegativeZ
        |]

    [<AutoOpen>]
    module private Patterns =
        let (|FileTexture|_|) (t : ITexture) =
            match t with
                | :? FileTexture as t -> Some(FileTexture(t.TextureParams, t.FileName))
                | _ -> None

        let (|PixTextureCube|_|) (t : ITexture) =
            match t with
                | :? PixTextureCube as t -> Some(PixTextureCube(t.TextureParams, t.PixImageCube))
                | _ -> None

        let (|PixTexture2D|_|) (t : ITexture) =
            match t with
                | :? PixTexture2d as t -> Some(t.TextureParams, t.PixImageMipMap)
                | _ -> None

        let (|PixTexture3D|_|) (t : ITexture) =
            match t with
                | :? PixTexture3d as t -> Some(PixTexture3D(t.TextureParams, t.PixVolume))
                | _ -> None

    let private lookupTable (l : list<'a * 'b>) =
        let d = Dictionary()
        for (k,v) in l do

            match d.TryGetValue k with
                | (true, vo) -> failwithf "duplicated lookup-entry: %A (%A vs %A)" k vo v
                | _ -> ()

            d.[k] <- v

        fun (key : 'a) ->
            match d.TryGetValue key with
                | (true, v) -> Some v
                | _ -> None

    type Col.Format with
        static member Stencil = unbox<Col.Format> (Int32.MaxValue)

    let internal toPixelType =
        lookupTable [
            typeof<uint8>, PixelType.UnsignedByte
            typeof<int8>, PixelType.Byte
            typeof<uint16>, PixelType.UnsignedShort
            typeof<int16>, PixelType.Short
            typeof<uint32>, PixelType.UnsignedInt
            typeof<int32>, PixelType.Int
            typeof<float32>, PixelType.Float
            typeof<float16>, PixelType.HalfFloat


        ]

    let internal toPixelFormat =
        lookupTable [
        
            Col.Format.Alpha, PixelFormat.Alpha
            Col.Format.BW, PixelFormat.Red
            Col.Format.Gray, PixelFormat.Luminance
            Col.Format.GrayAlpha, PixelFormat.LuminanceAlpha
            Col.Format.RGB, PixelFormat.Rgb
            Col.Format.BGR, PixelFormat.Bgr
            Col.Format.RGBA, PixelFormat.Rgba
            Col.Format.BGRA, PixelFormat.Bgra
            Col.Format.RGBP, PixelFormat.Rgba
            Col.Format.NormalUV, PixelFormat.Rg
            Col.Format.Stencil, PixelFormat.StencilIndex
        ]

    let internal toUntypedPixelFormat =
        lookupTable [
            TextureFormat.DepthComponent16, PixelFormat.DepthComponent
            TextureFormat.Depth24Stencil8, PixelFormat.DepthComponent
            TextureFormat.DepthComponent32, PixelFormat.DepthComponent
            TextureFormat.DepthComponent32f, PixelFormat.DepthComponent

            TextureFormat.Rgba8, PixelFormat.Rgba
            TextureFormat.Rgba16, PixelFormat.Rgba
            TextureFormat.Rgba16f, PixelFormat.Rgba
            TextureFormat.Rgba32f, PixelFormat.Rgba

            TextureFormat.Rgb8, PixelFormat.Rgb
            TextureFormat.Rgb16, PixelFormat.Rgb
            TextureFormat.Rgb16f, PixelFormat.Rgb
            TextureFormat.Rgb32f, PixelFormat.Rgb

            TextureFormat.Rg8, PixelFormat.Rg
            TextureFormat.Rg16, PixelFormat.Rg
            TextureFormat.Rg16f, PixelFormat.Rg
            TextureFormat.Rg32f, PixelFormat.Rg

            TextureFormat.R8, PixelFormat.Red
            TextureFormat.R16, PixelFormat.Red
            TextureFormat.R16f, PixelFormat.Red
            TextureFormat.R32f, PixelFormat.Red
        ]

    let internal toChannelCount =
        lookupTable [
            Col.Format.Alpha, 1
            Col.Format.BW, 1
            Col.Format.Gray, 1
            Col.Format.GrayAlpha, 2
            Col.Format.RGB, 3
            Col.Format.BGR, 3
            Col.Format.RGBA, 4
            Col.Format.BGRA, 4
            Col.Format.RGBP, 4
            Col.Format.NormalUV, 2
            Col.Format.Stencil, 1
        ]


    [<AutoOpen>]
    module private Uploads =

        let getTextureTarget (texture : Texture) =
            match texture.Dimension, texture.IsArray, texture.IsMultisampled with

                | TextureDimension.Texture1D,      _,       true     -> failwith "Texture1D cannot be multisampled"
                | TextureDimension.Texture1D,      true,    _        -> TextureTarget.Texture1DArray
                | TextureDimension.Texture1D,      false,   _        -> TextureTarget.Texture1D
                                                   
                | TextureDimension.Texture2D,      false,   false    -> TextureTarget.Texture2D
                | TextureDimension.Texture2D,      true,    false    -> TextureTarget.Texture2DArray
                | TextureDimension.Texture2D,      false,   true     -> TextureTarget.Texture2DMultisample
                | TextureDimension.Texture2D,      true,    true     -> TextureTarget.Texture2DMultisampleArray
                                                   
                | TextureDimension.Texture3D,      false,   false    -> TextureTarget.Texture3D
                | TextureDimension.Texture3D,      _,       _        -> failwith "Texture3D cannot be multisampled or an array"
                                                  
                | TextureDimension.TextureCube,   false,    false    -> TextureTarget.TextureCubeMap
                | TextureDimension.TextureCube,   true,     false    -> TextureTarget.TextureCubeMapArray
                | TextureDimension.TextureCube,   _,        true     -> failwith "TextureCube cannot be multisampled"

                | _ -> failwithf "unknown texture dimension: %A" texture.Dimension


        let uploadTexture2DLevelInternal (target : TextureTarget) (t : Texture) (level : int) (image : PixImage) =
            // determine the input format and covert the image
            // to a supported format if necessary.
            let pixelType, pixelFormat, image =
                match toPixelType image.PixFormat.Type, toPixelFormat image.Format with
                    | Some t, Some f -> (t,f, image)
                    | _ ->
                        failwith "conversion not implemented"

            let elementSize = image.PixFormat.Type.GLSize
            let channelCount =
                match toChannelCount image.Format with
                    | Some c -> c
                    | _ -> image.PixFormat.ChannelCount

            let lineSize = image.Size.X * channelCount * elementSize
            let packAlign = t.Context.PackAlignment

            let alignedLineSize = (lineSize + (packAlign - 1)) &&& ~~~(packAlign - 1)
            let targetSize = alignedLineSize * image.Size.Y
            //let data = Marshal.AllocHGlobal(alignedLineSize * image.Size.Y)

            let b = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, b)
            GL.BufferStorage(BufferTarget.PixelUnpackBuffer, nativeint targetSize, 0n, BufferStorageFlags.MapWriteBit)

            let ptr = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, nativeint targetSize, BufferAccessMask.MapWriteBit)
            try
                let srcInfo = image.VolumeInfo
                let dy = int64 (alignedLineSize / elementSize)

                let dstInfo = 
                    VolumeInfo(
                        dy * (srcInfo.Size.Y-1L), 
                        srcInfo.Size, 
                        V3l(srcInfo.SZ, -dy, 1L)
                    )
                NativeVolume.copyImageToNative image ptr dstInfo
            finally
                GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore

            GL.TexSubImage2D(target, level, 0, 0, image.Size.X, image.Size.Y, pixelFormat, pixelType, 0n)
            GL.Check (sprintf "could not upload texture data for level %d" level)

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.DeleteBuffer(b)

        let private uploadTexture2DInternal (bindTarget : TextureTarget) (target : TextureTarget) (isTopLevel : bool) (t : Texture) (startLevel : int) (textureParams : TextureParams) (data : PixImageMipMap) =
            if data.LevelCount <= 0 then
                failwith "cannot upload texture having 0 levels"

            let size = data.[0].Size
            let expectedLevels = Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let uploadLevels = if textureParams.wantMipMaps then data.LevelCount else 1
            let generateMipMap = textureParams.wantMipMaps && data.LevelCount < expectedLevels
            // TODO: think about texture format here
            let newFormat = TextureFormat.ofPixFormat data.[0].PixFormat textureParams
            let formatChanged = t.Format <> newFormat
            t.Format <- newFormat

            let internalFormat = TextureFormat.ofPixFormat data.[0].PixFormat textureParams |> int |> unbox<PixelInternalFormat>
            let sizeChanged = size <> t.Size2D

            if sizeChanged then
                let sizeInBytes = int64 <| ((InternalFormat.getSizeInBits internalFormat) * size.X * size.Y) / 8
                let sizeInBytes =  if textureParams.wantMipMaps then (sizeInBytes * 3L) / 2L else sizeInBytes
                updateTexture t.Context t.SizeInBytes sizeInBytes
                t.SizeInBytes <- sizeInBytes

            GL.BindTexture(bindTarget, t.Handle)
            GL.Check "could not bind texture"

            if not generateMipMap then
                GL.TexParameterI(bindTarget, TextureParameterName.TextureMaxLevel, [|uploadLevels|])

            for l in 0..uploadLevels-1 do
                let level = data.[l]
                //let level = level.ToPixImage(Col.Format.RGBA)

                // determine the input format and covert the image
                // to a supported format if necessary.
                let pixelType, pixelFormat, image =
                    match toPixelType level.PixFormat.Type, toPixelFormat level.Format with
                        | Some t, Some f -> (t,f, level)
                        | _ ->
                            failwith "conversion not implemented"

                let elementSize = image.PixFormat.Type.GLSize
                let channelCount =
                    match toChannelCount image.Format with
                        | Some c -> c
                        | _ -> image.PixFormat.ChannelCount
                let lineSize = image.Size.X * channelCount * elementSize
                let packAlign = t.Context.PackAlignment

                let alignedLineSize = (lineSize + (packAlign - 1)) &&& ~~~(packAlign - 1)
                let targetSize = alignedLineSize * image.Size.Y
                //let data = Marshal.AllocHGlobal(alignedLineSize * image.Size.Y)

                let b = GL.GenBuffer()
                GL.Check "could not create pixel buffer"
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, b)
                GL.Check "could not bind pixel buffer"
                GL.BufferStorage(BufferTarget.PixelUnpackBuffer, nativeint targetSize, 0n, BufferStorageFlags.MapWriteBit)
                GL.Check "could not allocate pixel buffer"
            
                let ptr = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, nativeint targetSize, BufferAccessMask.MapWriteBit)
                GL.Check "could not map pixel buffer"
                try
                    let srcInfo = image.VolumeInfo

                    let dy = int64 (alignedLineSize / elementSize)
                    let dstInfo = 
                        VolumeInfo(
                            dy * (srcInfo.Size.Y-1L),
                            srcInfo.Size, 
                            V3l(srcInfo.SZ,-dy, 1L)
                        )


                    NativeVolume.copyImageToNative image ptr dstInfo
                finally
                    GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore
                    GL.Check "could not unmap pixel buffer"
                if sizeChanged || formatChanged then
                    GL.TexImage2D(target, startLevel + l, internalFormat, image.Size.X, image.Size.Y, 0, pixelFormat, pixelType, 0n)
                else
                    GL.TexSubImage2D(target, startLevel + l, 0, 0, image.Size.X, image.Size.Y, pixelFormat, pixelType, 0n)
                GL.Check (sprintf "could not upload texture data for level %d" l)

                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                GL.Check "could not unbind pixel buffer"

                GL.DeleteBuffer(b)
                GL.Check "could not delete pixel buffer"



            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap && isTopLevel then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(bindTarget, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here
            if isTopLevel then
                t.Size <- V3i(size.X, size.Y, 0)
                t.Multisamples <- 1
                t.Count <- 1
                t.Dimension <- TextureDimension.Texture2D
                //t.ChannelType <- ChannelType.fromGlFormat internalFormat

            generateMipMap

        let uploadTexture2D (t : Texture) (textureParams : TextureParams) (data : PixImageMipMap) =
            uploadTexture2DInternal TextureTarget.Texture2D TextureTarget.Texture2D true t 0 textureParams data |> ignore

        let uploadTextureCube (t : Texture) (textureParams : TextureParams) (data : PixImageCube) =
            for (s,_) in cubeSides do
                if data.[s].LevelCount <= 0 then
                    failwith "cannot upload texture having 0 levels"

            let mutable generateMipMaps = false
            let size = data.[CubeSide.NegativeX].[0].Size

            let mutable minLevels = Int32.MaxValue
            for (side, target) in cubeSides do
                let data = data.[side]
                
                minLevels <- min minLevels data.LevelCount
                let generate = uploadTexture2DInternal TextureTarget.TextureCubeMap target false t 0 textureParams data

                if generate && textureParams.wantMipMaps then
                    generateMipMaps <- true

            let realSize = t.SizeInBytes * 6L
            updateTexture t.Context t.SizeInBytes realSize
            t.SizeInBytes <- realSize

            let levels =
                if generateMipMaps then
                    GL.BindTexture(TextureTarget.TextureCubeMap, t.Handle)
                    GL.Check "could not bind texture"

                    GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap)
                    GL.Check "failed to generate mipmaps"

                    GL.BindTexture(TextureTarget.TextureCubeMap, 0)
                    GL.Check "could not unbind texture"

                    GL.GetTexParameterI(TextureTarget.TextureCubeMap, GetTextureParameter.TextureMaxLevel, &minLevels)
                    minLevels + 1
                else
                    minLevels
                
            t.MipMapLevels <- levels
            t.Size <- V3i(size.X, size.Y, 0)
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.TextureCube



        let uploadTexture2DBitmap (t : Texture) (mipMaps : bool) (bmp : BitmapTexture) =
            let size = V2i(bmp.Bitmap.Width, bmp.Bitmap.Height)
            let expectedLevels = Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let uploadLevels = 1
            let generateMipMap = mipMaps
            let internalFormat = PixelInternalFormat.CompressedRgba
            let sizeChanged = size <> t.Size2D
            GL.BindTexture(TextureTarget.Texture2D, t.Handle)
            GL.Check "could not bind texture"

            // determine the input format and covert the image
            // to a supported format if necessary.
            let pixelType, pixelFormat =
                PixelType.UnsignedByte, PixelFormat.Bgra


            let elementSize = 1
            let lineSize = size.X * 4 * elementSize
            let packAlign = t.Context.PackAlignment

            let alignedLineSize = (lineSize + (packAlign - 1)) &&& ~~~(packAlign - 1)
            let targetSize = alignedLineSize * size.Y



            let locked = bmp.Bitmap.LockBits(Drawing.Rectangle(0,0,bmp.Bitmap.Width, bmp.Bitmap.Height), Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppArgb)          
            let srcInfo = VolumeInfo(V3l(size.X, size.Y, 4), V3l(4, size.X * 4, 1))
            let src : NativeVolume<byte> = locked.Scan0 |> NativeVolume.ofNativeInt srcInfo
  
  
            let b = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, b)
            GL.BufferStorage(BufferTarget.PixelUnpackBuffer, nativeint targetSize, 0n, BufferStorageFlags.MapWriteBit)

            let ptr = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, nativeint targetSize, BufferAccessMask.MapWriteBit)
            try
                let dy = int64 (alignedLineSize / elementSize)

                let dstInfo = 
                    VolumeInfo(
                        dy * (srcInfo.Size.Y-1L), 
                        srcInfo.Size, 
                        V3l(srcInfo.SZ, -dy, 1L)
                    )
                let dst = ptr |> NativeVolume.ofNativeInt dstInfo

                NativeVolume.iter2 src dst (fun s d -> NativePtr.write d (NativePtr.read s))
            finally
                GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore

            if sizeChanged then
                GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, size.X, size.Y, 0, pixelFormat, pixelType, 0n)
            else
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, size.X, size.Y, pixelFormat, pixelType, 0n)
            GL.Check "could not upload texture data"

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.DeleteBuffer(b)


            bmp.Bitmap.UnlockBits(locked)

            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(TextureTarget.Texture2D, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here

            t.Size <- V3i(size.X, size.Y, 0)
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.Texture2D
            t.Format <- unbox internalFormat

        let uploadTexture3D (t : Texture) (textureParams : TextureParams) (data : PixVolume) =
            let size = data.Size
            let expectedLevels = Fun.Min(size.X, size.Y, size.Z) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let generateMipMap = textureParams.wantMipMaps
            let newFormat = TextureFormat.ofPixFormat data.PixFormat textureParams
            let formatChanged = t.Format <> newFormat
            let sizeChanged = size <> t.Size3D

            GL.BindTexture(TextureTarget.Texture3D, t.Handle)
            GL.Check "could not bind texture"

            // determine the input format and covert the image
            // to a supported format if necessary.
            let pixelType, pixelFormat, image =
                match toPixelType data.PixFormat.Type, toPixelFormat data.Format with
                    | Some t, Some f -> (t,f, data)
                    | _ ->
                        failwith "conversion not implemented"

            // since OpenGL cannot upload image-regions we
            // need to ensure that the image has a canonical layout. 
            // TODO: Check id this is no "real" copy when already canonical
            let image = image.CopyToPixVolumeWithCanonicalDenseLayout()


            let gc = GCHandle.Alloc(image.Array, GCHandleType.Pinned)

            // if the size did not change it is more efficient
            // to use glTexSubImage
            if sizeChanged || formatChanged then
                let sizeInBytes = int64 <| ((InternalFormat.getSizeInBits (unbox (int newFormat))) * size.X * size.Y) / 8
                updateTexture t.Context t.SizeInBytes sizeInBytes
                t.SizeInBytes <- sizeInBytes

                GL.TexImage3D(TextureTarget.Texture3D, 0, unbox (int newFormat), size.X, size.Y, size.Z, 0, pixelFormat, pixelType, gc.AddrOfPinnedObject())
            else
                GL.TexSubImage3D(TextureTarget.Texture3D, 0, 0, 0, 0, size.X, size.Y, size.Z, pixelFormat, pixelType, gc.AddrOfPinnedObject())
            GL.Check "could not upload texture data"

            gc.Free()

            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture3D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(TextureTarget.Texture3D, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here
            t.Size <- size
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.Texture3D
            t.Format <- newFormat


        let downloadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (level : int) (image : PixImage) =
            let format = image.PixFormat
            GL.BindTexture(target, t.Handle)
            GL.Check "could not bind texture"

            let pixelType, pixelFormat =
                match toPixelType format.Type, toPixelFormat image.Format with
                    | Some t, Some f -> (t,f)
                    | _ ->
                        failwith "conversion not implemented"


            let elementSize = image.PixFormat.Type.GLSize
            let channelCount =
                match toChannelCount image.Format with
                    | Some c -> c
                    | _ -> image.PixFormat.ChannelCount

            let lineSize = image.Size.X * channelCount * elementSize
            let packAlign = t.Context.PackAlignment

            let alignedLineSize = (lineSize + (packAlign - 1)) &&& ~~~(packAlign - 1)
            let targetSize = alignedLineSize * image.Size.Y

            let b = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelPackBuffer, b)
            GL.Check "could not bind buffer"
            GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint targetSize, 0n, BufferStorageFlags.MapReadBit)
            GL.Check "could not set buffer storage"
            GL.GetTexImage(target, level, pixelFormat, pixelType, 0n)
            GL.Check "could not get texture image"

            let src = GL.MapBufferRange(BufferTarget.PixelPackBuffer, 0n, nativeint targetSize, BufferAccessMask.MapReadBit)
            GL.Check "could not map buffer"
            try
                let dstInfo = image.VolumeInfo
                let dy = int64(alignedLineSize / elementSize)
                let srcInfo = 
                    VolumeInfo(
                        dy * (dstInfo.Size.Y - 1L), 
                        dstInfo.Size, 
                        V3l(dstInfo.SZ, -dy, 1L)
                    )

                NativeVolume.copyNativeToImage src srcInfo image

            finally
                GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
                GL.Check "could not unmap buffer"

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
            GL.Check "could not unbind buffer"
            GL.DeleteBuffer(b)
            GL.Check "could not delete buffer"

            GL.BindTexture(target, 0)
            GL.Check "could not unbind texture"

          
        let downloadTexture2D (t : Texture) (level : int) (image : PixImage) =
            downloadTexture2DInternal TextureTarget.Texture2D true t level image

        let downloadTextureCube (t : Texture) (level : int) (side : CubeSide) (image : PixImage) =
            let target = cubeSides.[int side] |> snd
            downloadTexture2DInternal target false t level image

    let texSizeInBytes (size : V3i, t : TextureFormat, samples : int) =
        let pixelCount = (int64 size.X) * (int64 size.Y) * (int64 size.Z) * (int64 samples)
        pixelCount * (int64 (InternalFormat.getSizeInBits (unbox (int t)))) / 8L

    type Context with
        member x.CreateTexture1D(size : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i.Zero, 1, t, 0L, false)
                x.UpdateTexture1D(tex, size, mipMapLevels, t)

                tex
            )

        member x.CreateTexture2D(size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"
                
                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i.Zero, 1, t, 0L, false)

                x.UpdateTexture2D(tex, size, mipMapLevels, t, samples)

                tex
            )

        member x.CreateTexture3D(size : V3i, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture3D, mipMapLevels, 1, V3i.Zero, 1, t, 0L, false)
                x.UpdateTexture3D(tex, size, mipMapLevels, t)

                tex
            )

        member x.CreateTextureCube(size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size.X, size.Y, 0), 1, t, 0L, false)
                x.UpdateTextureCube(tex, size, mipMapLevels, t, samples)

                tex
            )

        member x.CreateTexture1DArray(size : int, count : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i.Zero, 1, t, 0L, false)
                x.UpdateTexture1DArray(tex, size, count, mipMapLevels, t)

                tex
            ) 

        member x.CreateTexture2DArray(size : V2i, count : int, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"
                
                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i.Zero, 1, t, 0L, false)

                x.UpdateTexture2DArray(tex, size, count, mipMapLevels, t, samples)

                tex
            )
            
        member x.UpdateTexture1D(tex : Texture, size : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture1D, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = texSizeInBytes(V3i(size, 1, 1), t, 1)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes
  
                GL.TexStorage1D(TextureTarget1d.Texture1D, mipMapLevels, unbox (int t), size)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture1D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture1D
                tex.Size <- V3i(size, 0, 0)
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture2D(tex : Texture, size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                let target =
                    if samples = 1 then TextureTarget.Texture2D
                    else TextureTarget.Texture2DMultisample

                GL.BindTexture(target, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = texSizeInBytes(size.XYI, t, samples)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                if samples = 1 then
                    GL.TexStorage2D(TextureTarget2d.Texture2D, mipMapLevels, unbox (int t), size.X, size.Y)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage2DMultisample(TextureTargetMultisample2d.Texture2DMultisample, samples, unbox (int t), size.X, size.Y, false)

                GL.Check "could not allocate texture"

                GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipMapLevels)
                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)


                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture2D
                tex.Multisamples <- samples
                tex.Count <- 1
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture3D(tex : Texture, size : V3i, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture3D, tex.Handle)
                GL.Check "could not bind texture"

                let ifmt = unbox (int t) 

                let sizeInBytes = texSizeInBytes(size, t, 1)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                GL.TexStorage3D(TextureTarget3d.Texture3D, mipMapLevels, ifmt, size.X, size.Y, size.Z)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture3D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture3D
                tex.Count <- 1
                tex.Multisamples <- 1
                tex.Size <- size
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTextureCube(tex : Texture, size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.TextureCubeMap, tex.Handle)
                GL.Check "could not bind texture"

                if samples = 1 then
                    GL.TexStorage2D(TextureTarget2d.TextureCubeMap, mipMapLevels, unbox (int t), size.X, size.Y)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    // TODO: verify that this works!!
                    for f in 0..5 do
                        let target = int TextureTarget.TextureCubeMapPositiveX + f
                        GL.TexImage2DMultisample(unbox target, samples, unbox (int t), size.X, size.Y, true)

                GL.BindTexture(TextureTarget.TextureCubeMap, 0)
                GL.Check "could not unbind texture"

                let sizeInBytes = texSizeInBytes(size.XYI, t, samples)
                let sizeInBytes = sizeInBytes * 6L
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.TextureCube
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.Count <- 1
                tex.Multisamples <- samples
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture2DArray(tex : Texture, size : V2i, count : int, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                let target =
                    if samples = 1 then TextureTarget.Texture2DArray
                    else TextureTarget.Texture2DMultisampleArray

                GL.BindTexture(target, tex.Handle)
                GL.Check "could not bind texture"


                let sizeInBytes = texSizeInBytes(size.XYI, t, samples) * (int64 count)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes // TODO check multisampling

                if samples = 1 then
                    GL.TexStorage3D(TextureTarget3d.Texture2DArray, mipMapLevels, unbox (int t), size.X, size.Y, count)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage3DMultisample(TextureTargetMultisample3d.Texture2DMultisampleArray, samples, unbox (int t), size.X, size.Y, count, true)
  
                GL.Check "could not allocate texture"

                GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipMapLevels)
                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)


                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture2D
                tex.Count <- count
                tex.Multisamples <- samples
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture1DArray(tex : Texture, size : int, count : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture1DArray, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = texSizeInBytes(V3i(size, 1, 1), t, 1) * (int64 count)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes
  
                GL.TexStorage2D(TextureTarget2d.Texture1DArray, mipMapLevels, unbox (int t), size, count)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture1DArray, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture1D
                tex.Count <- count
                tex.Multisamples <- 1
                tex.Size <- V3i(size, 0, 0)
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.CreateTextureView(orig : Texture, levels : Range1i, slices : Range1i) =
            using x.ResourceLock (fun _ ->
                if not orig.ImmutableFormat then
                    failwithf "cannot create texture-views for mutable textures"

                let handle = GL.GenTexture()
                GL.Check "could not create texture"

                let dim =
                    match orig.Dimension, orig.Count with
                        | TextureDimension.TextureCube, 1 -> 
                            if slices.Min <> slices.Max then failwithf "cannot take multiple slices from CubeMap"
                            TextureDimension.Texture2D
                        | d,_ -> d

                let tex = Texture(x, handle, dim, 1 + levels.Max - levels.Min, orig.Multisamples, orig.Size, 1 + slices.Max - slices.Min, orig.Format, 0L, true)
                let target = getTextureTarget tex
                  
                GL.TextureView(
                    handle,
                    target,
                    orig.Handle,
                    unbox (int orig.Format),
                    levels.Min, 1 + levels.Max - levels.Min,
                    slices.Min, 1
                )
                GL.Check "could not create texture view"

                tex
            )


        member x.CreateTexture(data : ITexture) =
            using x.ResourceLock (fun _ ->
                let newTexture () = // not all cases need new textures
                    let h = GL.GenTexture()
                    GL.Check "could not create texture"
                    addTexture x 0L
                    Texture(x, h, TextureDimension.Texture2D, 1, 1, V3i(-1,-1,-1), 1, TextureFormat.Rgba8, 0L, false)

                match data with

                    | :? BitmapTexture as bmp ->
                        let t = newTexture ()
                        uploadTexture2DBitmap t true bmp
                        t

                    | FileTexture(info, file) ->
                        let t = newTexture ()
                        if file = null then 
                            t
                        else
                            let pi = PixImage.Create(file, PixLoadOptions.UseDevil)
                            let mm = PixImageMipMap [|pi|]
                            uploadTexture2D t info mm |> ignore
                            t

                    | PixTexture2D(wantMipMaps, data) -> 
                        let t = newTexture ()
                        uploadTexture2D t wantMipMaps data |> ignore
                        t

                    | PixTextureCube(info, data) ->
                        let t = newTexture () 
                        uploadTextureCube t info data
                        t

                    | PixTexture3D(info, data) ->
                        let t = newTexture ()
                        uploadTexture3D t info data
                        t

                    | :? NullTexture ->
                        Texture(x, 0, TextureDimension.Texture2D, 1, 1, V3i(-1,-1,-1), 1, TextureFormat.Rgba8, 0L, false)

                    | :? Texture as o ->
                        o

                    | _ ->
                        failwith "unsupported texture data"

            )

        member x.Upload(t : Texture, data : ITexture) =
            using x.ResourceLock (fun _ ->
                match data with
                    | :? BitmapTexture as bmp ->
                        uploadTexture2DBitmap t true bmp

                    | PixTexture2D(wantMipMaps, data) -> 
                        uploadTexture2D t wantMipMaps data |> ignore

                    | PixTextureCube(info, data) -> 
                        uploadTextureCube t info data

                    | PixTexture3D(info, image) ->
                        uploadTexture3D t info image

                    | FileTexture(info, file) ->
                        let pi = PixImage.Create(file, PixLoadOptions.UseDevil)
                        let mm = PixImageMipMap [|pi|]
                        uploadTexture2D t info mm |> ignore

                    | :? NullTexture -> failwith "cannot update texture with null texture"

                    | :? Texture as o ->
                        if t.Handle <> o.Handle then
                            failwith "cannot upload to framebuffer-texture"

                    | _ ->
                        failwith "unsupported texture data"
            )


            
        member x.Delete(t : Texture) =
            using x.ResourceLock (fun _ ->
                removeTexture x t.SizeInBytes
                GL.DeleteTexture(t.Handle)
                GL.Check "could not delete texture"
            )
            
        member x.Blit(src : Texture, srcLevel : int, srcSlice : int, srcRegion : Box2i, dst : Texture, dstLevel : int, dstSlice : int, dstRegion : Box2i, linear : bool) =
            using x.ResourceLock (fun _ ->
                let fSrc = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"
                let fDst = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                GL.Check "could not bind framebuffer"
                if src.Count > 1 then GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel, srcSlice)
                else GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel)
                GL.Check "could not attach texture to framebuffer"

                let srcCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                if srcCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create input framebuffer: %A" srcCheck

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fDst)
                GL.Check "could not bind framebuffer"

                if src.Count > 1 then GL.FramebufferTextureLayer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, dst.Handle, dstLevel, dstSlice)
                else GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, dst.Handle, dstLevel)
                GL.Check "could not attach texture to framebuffer"

                let dstCheck = GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer)
                if dstCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create output framebuffer: %A" dstCheck

                GL.BlitFramebuffer(
                    srcRegion.Min.X, srcRegion.Min.Y,
                    srcRegion.Max.X, srcRegion.Max.Y,

                    dstRegion.Min.X, dstRegion.Min.Y,
                    dstRegion.Max.X, dstRegion.Max.Y,

                    ClearBufferMask.ColorBufferBit,
                    (if linear then BlitFramebufferFilter.Linear else BlitFramebufferFilter.Nearest)
                )
                GL.Check "could blit framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.Check "could unbind framebuffer"

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.Check "could unbind framebuffer"

                GL.DeleteFramebuffer(fSrc)
                GL.Check "could delete framebuffer"

                GL.DeleteFramebuffer(fDst)
                GL.Check "could delete framebuffer"

            )

        member x.Copy(src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i, dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i, size : V2i) =
            using x.ResourceLock (fun _ ->
                let fSrc = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                GL.Check "could not bind framebuffer"

                if src.Count > 1 then
                    GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel, srcSlice)
                else
                    GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel)
                GL.Check "could not attach texture to framebuffer"

                let srcCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                if srcCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create input framebuffer: %A" srcCheck

                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.Check "could not set readbuffer"


                let bindTarget = getTextureTarget dst
                GL.BindTexture(bindTarget, dst.Handle)
                GL.Check "could not bind texture"

                let copyTarget =
                    match dst.Dimension with
                        | TextureDimension.TextureCube -> snd cubeSides.[dstSlice]
                        | _ -> bindTarget

                GL.CopyTexSubImage2D(
                    copyTarget,
                    dstLevel,
                    dstOffset.X, dstOffset.Y,
                    srcOffset.X, srcOffset.Y,
                    size.X, size.Y
                )
                GL.Check "could not copy texture"

                GL.ReadBuffer(ReadBufferMode.None)
                GL.Check "could not unset readbuffer"

                GL.BindTexture(bindTarget, 0)
                GL.Check "could not unbind texture"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.Check "could not unbind framebuffer"

                GL.DeleteFramebuffer(fSrc)
                GL.Check "could not delete framebuffer"

            )


        member x.Upload(t : Texture, level : int, slice : int, offset : V2i, source : PixImage) =
            using x.ResourceLock (fun _ ->
                let levelSize = t.Size2D / (1 <<< level)
                if offset = V2i.Zero && source.Size = levelSize then
                    x.Upload(t, level, slice, source)
                else
                    let temp = x.CreateTexture2D(source.Size, 1, t.Format, 1)
                    x.Upload(temp, 0, 0, source)
                    x.Copy(temp, 0, 0, V2i.Zero, t, level, slice, offset, source.Size)
                    x.Delete(temp)
            )

        member x.Upload(t : Texture, level : int, slice : int, source : PixImage) =
            using x.ResourceLock (fun _ ->
                match t.Dimension with
                    | TextureDimension.Texture2D -> 
                        let target = getTextureTarget t
                        GL.BindTexture(target, t.Handle)
                        GL.Check "could not bind texture"

                        uploadTexture2DLevelInternal target t level source

                        GL.BindTexture(target, 0)
                        GL.Check "could not unbind texture"

                    | TextureDimension.TextureCube ->
                        let target = getTextureTarget t
                        GL.BindTexture(target, t.Handle)
                        GL.Check "could not bind texture"

                        let target = snd cubeSides.[slice]
                        uploadTexture2DLevelInternal target t level source

                        GL.BindTexture(target, 0)
                        GL.Check "could not unbind texture"
                    | _ ->  
                        failwithf "cannot upload textures of kind: %A" t.Dimension
            )

        member x.Upload(t : Texture, level : int, source : PixImage) =
            x.Upload(t, level, 0, source)



        member x.Download(t : Texture, level : int, slice : int, offset : V2i, target : PixImage) =
            using x.ResourceLock (fun _ ->
                let levelSize = t.Size2D / (1 <<< level)
                if offset = V2i.Zero && target.Size = levelSize then
                    x.Download(t, level, slice, target)
                else
                    let temp = x.CreateTexture2D(target.Size, 1, t.Format, 1)
                    
                    if target.Size = levelSize then
                        x.Copy(t, level, slice, offset, temp, 0, 0, V2i.Zero, target.Size)
                    else
                        x.Blit(t, level, slice, Box2i.FromMinAndSize(offset, levelSize), temp, 0, 0, Box2i(V2i.Zero, target.Size), true)
                    
                    x.Download(temp, 0, 0, target)
                    x.Delete(temp)
            )

        member x.Download(t : Texture, level : int, slice : int, target : PixImage) =
            using x.ResourceLock (fun _ ->
                match t.Dimension with
                    | TextureDimension.Texture2D -> 
                        downloadTexture2D t level target

                    | TextureDimension.TextureCube ->
                        downloadTextureCube t level (unbox slice) target

                    | _ ->  
                        failwithf "cannot download textures of kind: %A" t.Dimension
            )

        member x.DownloadStencil(t : Texture, level : int, slice : int, target : Matrix<int>) =
            using x.ResourceLock (fun _ ->
                match t.Dimension with
                    | TextureDimension.Texture2D -> 
                        
                        //let downloadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (level : int) (image : PixImage)
                        
                        let img : PixImage<int> = PixImage<int>()
                        img.Volume <- target.AsVolume()
                        img.Format <- Col.Format.Stencil
                        downloadTexture2DInternal TextureTarget.Texture2D true t level img

                    | _ ->  
                        failwithf "cannot download stecil-texture of kind: %A" t.Dimension
            )

        member x.Download(t : Texture, level : int, slice : int) : PixImage =
            let fmt = TextureFormat.toDownloadFormat t.Format
            let levelSize = t.Size2D / (1 <<< level)
            let img = PixImage.Create(fmt, int64 levelSize.X, int64 levelSize.Y)
            x.Download(t, level, slice, img)
            img

        member x.Download(t : Texture, level : int) : PixImage =
            x.Download(t, level, 0)

        member x.Download(t : Texture) : PixImage =
            x.Download(t, 0, 0)

        




    module ExecutionContext =

        let internal getTextureTarget (texture : Texture) = Uploads.getTextureTarget texture

        let bindTexture (unit : int) (texture : Texture) =
            seq {
                yield Instruction.ActiveTexture(int TextureUnit.Texture0 + unit)
                
                let target = getTextureTarget texture
                yield Instruction.BindTexture (int target) texture.Handle
            }            

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Texture =

    let empty =
        Texture(null,0,TextureDimension.Texture2D,0,0,V3i.Zero,0,TextureFormat.Rgba8,0L, false)

    let create1D (c : Context) (size : int) (mipLevels : int) (format : TextureFormat) =
        c.CreateTexture1D(size, mipLevels, format)

    let create2D (c : Context) (size : V2i) (mipLevels : int) (format : TextureFormat) (samples : int) =
        c.CreateTexture2D(size, mipLevels, format, samples)

    let createCube (c : Context) (size : V2i) (mipLevels : int) (format : TextureFormat) (samples : int) =
        c.CreateTextureCube(size, mipLevels, format, samples)

    let create3D (c : Context) (size : V3i) (mipLevels : int) (format : TextureFormat)  =
        c.CreateTexture3D(size, mipLevels, format)

    let delete (tex : Texture) =
        tex.Context.Delete(tex)

    let write (data : ITexture) (tex : Texture) =
        tex.Context.Upload(tex, data)

    let read (format : PixFormat) (level : int) (tex : Texture) : PixImage[] =
        let size = V2i(max 1 (tex.Size.X / (1 <<< level)), max 1 (tex.Size.Y / (1 <<< level)))

        let pi = PixImage.Create(format, int64 size.Y, int64 size.Y)
        tex.Context.Download(tex, level, 0, pi)
        [|pi|]