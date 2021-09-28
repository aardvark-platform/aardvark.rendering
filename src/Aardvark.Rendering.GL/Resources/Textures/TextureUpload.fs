namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open Aardvark.Rendering.GL
open System
open System.Runtime.CompilerServices

#nowarn "9"

[<AutoOpen>]
module internal TextureUploadImplementation =

    [<AutoOpen>]
    module private Utils =

        type NativeTensor4<'T when 'T : unmanaged> with
            member x.Format =
                match x.Size.W with
                | 1L -> Col.Format.Gray
                | 2L -> Col.Format.GrayAlpha
                | 3L -> Col.Format.RGB
                | _  -> Col.Format.RGBA

        let getFormatAndType (internalFormat : TextureFormat) (format : Col.Format) (typ : Type) =
            match PixelFormat.ofColFormat internalFormat.IsIntegerFormat format, PixelType.ofType typ with
            | Some pf, Some pt -> pf, pt
            | _ ->
                failwithf "[GL] Pixel format %A and type %A not supported" format typ

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Texture =

        let uploadPixelData (texture : Texture) (generateMipmap : bool) (level : int) (offset : V3i) (data : PixelData) =
            let target = texture |> TextureTarget.ofTexture
            let targetSlice = target |> TextureTarget.toSliceTarget offset.Z

            let dataInfo =
                match data with
                | PixelData.General d ->
                    let elementSize = PixelType.size d.Type
                    let channels = PixelFormat.channels d.Format

                    let alignedLineSize =
                        let lineSize = nativeint d.Size.X * nativeint elementSize * nativeint channels
                        let align = nativeint texture.Context.UnpackAlignment
                        let mask = align - 1n

                        if lineSize % align = 0n then lineSize
                        else (lineSize + mask) &&& ~~~mask

                    let sizeInBytes = alignedLineSize * nativeint d.Size.Y * nativeint d.Size.Z

                    PixelDataInfo.General {
                        Channels        = channels
                        ElementSize     = elementSize
                        AlignedLineSize = alignedLineSize
                        SizeInBytes     = sizeInBytes
                    }

                | PixelData.Compressed d ->
                    PixelDataInfo.Compressed { SizeInBytes = d.SizeInBytes }

            GL.BindTexture(target, texture.Handle)
            GL.Check "could not bind texture"

            let pbo = PixelUnpackBuffer.create BufferUsageHint.StaticDraw dataInfo.SizeInBytes
            let dst = pbo |> PixelUnpackBuffer.map BufferAccess.WriteOnly

            (dataInfo, data) ||> PixelData.copyTo dst

            let pixels = pbo |> PixelUnpackBuffer.unmap

            match texture.Dimension, texture.IsArray with
            | TextureDimension.Texture1D, false ->
                match data with
                | PixelData.General d ->
                    GL.TexSubImage1D(target, level, offset.X, d.Size.X, d.Format, d.Type, pixels)
                | PixelData.Compressed d ->
                    GL.CompressedTexSubImage1D(target, level, offset.X, d.Size.X,
                                               unbox texture.Format, int d.SizeInBytes, pixels)

            | TextureDimension.Texture1D, true
            | TextureDimension.Texture2D, false
            | TextureDimension.TextureCube, false ->
                match data with
                | PixelData.General d ->
                    GL.TexSubImage2D(targetSlice, level, offset.X, offset.Y, d.Size.X, d.Size.Y, d.Format, d.Type, pixels)
                | PixelData.Compressed d ->
                    GL.CompressedTexSubImage2D(target, level, offset.X, offset.Y, d.Size.X, d.Size.Y,
                                               unbox texture.Format, int d.SizeInBytes, pixels)

            | TextureDimension.Texture2D, true
            | TextureDimension.Texture3D, false
            | TextureDimension.TextureCube, true ->
                match data with
                | PixelData.General d ->
                    GL.TexSubImage3D(target, level, offset.X, offset.Y, offset.Z, d.Size.X, d.Size.Y, d.Size.Z, d.Format, d.Type, pixels)
                | PixelData.Compressed d ->
                    GL.CompressedTexSubImage3D(target, level, offset.X, offset.Y, offset.Z,
                                               d.Size.X, d.Size.Y, d.Size.Z, unbox texture.Format, int d.SizeInBytes, pixels)

            | d, a ->
                failwithf "unsupported texture data (%A, isArray = %b)" d a

            GL.Check (sprintf "could not upload texture data")

            pbo |> PixelUnpackBuffer.free

            if generateMipmap then
                GL.GenerateMipmap(unbox target)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(target, 0)
            GL.Check "could not unbind texture"

        let uploadNativeTensor4<'T when 'T : unmanaged> (texture : Texture) (flipY : bool) (generateMipmap : bool)
                                                        (level : int) (offset : V3i) (size : V3i) (src : NativeTensor4<'T>) =

            let pixelFormat, pixelType =
                getFormatAndType texture.Format src.Format typeof<'T>

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (dst : nativeint) =
                let dstTensor =
                    let dy = int64 alignedLineSize / int64 elementSize
                    let info =
                        Tensor4Info(
                            0L,
                            V4l(int64 size.X, int64 size.Y, int64 size.Z, src.SW),
                            V4l(int64 channels, dy, dy * int64 size.Y, 1L)
                        )
                    NativeTensor4<'T>(NativePtr.ofNativeInt dst, info)

                let src = src.SubTensor4(V4i.Zero, V4i(size, channels))
                NativeTensor4.copy src (if flipY then dstTensor.MirrorY() else dstTensor)

            let pixelData =
                PixelData.General {
                    Size   = size
                    Type   = pixelType
                    Format = pixelFormat
                    CopyTo = copy
                }

            uploadPixelData texture generateMipmap level offset pixelData


        let uploadNativeTexture (texture : Texture) (baseLevel : int) (baseSlice : int) (data : INativeTexture) =
            let pixelFormat, pixelType = TextureFormat.toFormatAndType data.Format
            let isCompressed = TextureFormat.isCompressed data.Format

            let levelCount =
                if data.WantMipMaps then data.MipMapLevels else 1

            let generateMipmap =
                data.WantMipMaps && levelCount < texture.MipMapLevels

            for slice = 0 to data.Count - 1 do
                let offset =
                    match texture.Dimension with
                    | TextureDimension.Texture1D -> V3i(0, baseSlice + slice, 0)
                    | TextureDimension.Texture2D
                    | TextureDimension.TextureCube -> V3i(0, 0, baseSlice + slice)
                    | _ -> V3i.Zero

                for level = 0 to levelCount - 1 do
                    let subdata = data.[slice, level]

                    let copy (dst : nativeint) =
                        subdata.Use (fun src ->
                            Marshal.Copy(src, dst, subdata.SizeInBytes)
                        )

                    let pixelData =
                        if isCompressed then
                            PixelData.Compressed {
                                Size        = subdata.Size
                                SizeInBytes = nativeint subdata.SizeInBytes
                                CopyTo      = copy
                            }

                        else
                            PixelData.General {
                                Size   = subdata.Size
                                Type   = pixelType
                                Format = pixelFormat
                                CopyTo = fun _ _ _ _ -> copy
                            }

                    let generateMipmap = generateMipmap && level = levelCount - 1
                    uploadPixelData texture generateMipmap (baseLevel + level) offset pixelData


        let uploadPixImage (texture : Texture) (flipY : bool) (generateMipmap : bool)
                           (level : int) (slice : int) (offset : V2i) (image : PixImage) =

            let pixelFormat, pixelType =
                getFormatAndType texture.Format image.Format image.PixFormat.Type

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (dst : nativeint) =
                let dstInfo =
                    let viSize = V3l(int64 image.Size.X, int64 image.Size.Y, int64 channels)
                    if flipY then
                        VolumeInfo(
                            int64 alignedLineSize * (int64 image.Size.Y - 1L),
                            viSize,
                            V3l(int64 channels * int64 elementSize, int64 -alignedLineSize, int64 elementSize)
                        )
                    else
                        VolumeInfo(
                            0L,
                            viSize,
                            V3l(int64 channels * int64 elementSize, int64 alignedLineSize, int64 elementSize)
                        )

                TextureCopyUtils.Copy(image, dst, dstInfo)

            let offset = V3i(offset, slice)
            let data =
                PixelData.General {
                    Size   = V3i(image.Size, 1)
                    Type   = pixelType
                    Format = pixelFormat
                    CopyTo = copy
                }

            uploadPixelData texture generateMipmap level offset data


        let private uploadPixImageMipMapInternal (texture : Texture) (flipY : bool) (wantMipmap : bool) (generateMipmap : bool)
                                                 (baseLevel : int) (slice : int) (offset : V2i) (images : PixImageMipMap) =
            let levelCount =
                if wantMipmap then images.LevelCount else 1

            for i = 0 to levelCount - 1 do
                let img = images.ImageArray.[i]
                let mip = generateMipmap && i = levelCount - 1
                uploadPixImage texture flipY mip (baseLevel + i) slice offset img


        let uploadPixImageMipMap (texture : Texture) (flipY : bool) (wantMipmap : bool)
                                 (baseLevel : int) (slice : int) (offset : V2i) (images : PixImageMipMap) =
            let generateMipmap =
                wantMipmap && images.LevelCount < texture.MipMapLevels

            uploadPixImageMipMapInternal texture flipY wantMipmap generateMipmap baseLevel slice offset images


        let uploadPixImageCube (texture : Texture) (flipY : bool) (wantMipmap : bool)
                               (baseLevel : int) (slice : int) (offset : V2i) (data : PixImageCube) =
            let generateMipmap =
                wantMipmap && data.MipMapArray |> Array.forany (fun i -> i.LevelCount < texture.MipMapLevels)

            for i = 0 to 5 do
                let data = data.[unbox<CubeSide> i]
                let generateMipMap = generateMipmap && i = 5
                uploadPixImageMipMapInternal texture flipY wantMipmap generateMipMap baseLevel (slice + i) offset data


        let uploadPixVolume (texture : Texture) (flipY : bool) (generateMipMap : bool)
                            (level : int) (offset : V3i) (volume : PixVolume) =

            let pixelFormat, pixelType =
                getFormatAndType texture.Format volume.Format volume.PixFormat.Type

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (sizeInBytes : nativeint) (dst : nativeint) =
                let dstInfo =
                    let rowPixels = int64 alignedLineSize / int64 elementSize
                    let tiSize = V4l(volume.SizeL, int64 channels)

                    if flipY then
                        Tensor4Info(
                            rowPixels * (tiSize.Y - 1L),
                            tiSize,
                            V4l(int64 channels, -rowPixels, rowPixels * tiSize.Y, 1L)
                        )
                    else
                        Tensor4Info(
                            0L,
                            tiSize,
                            V4l(int64 channels, rowPixels, rowPixels * tiSize.Y, 1L)
                        )

                TextureCopyUtils.Copy(volume, dst, dstInfo)

            let data =
                PixelData.General {
                    Size   = volume.Size
                    Type   = pixelType
                    Format = pixelFormat
                    CopyTo = copy
                }

            uploadPixelData texture generateMipMap level offset data


[<AutoOpen>]
module internal TextureCompressedFileLoadExtensions =
    open DevILSharp

    module private Devil =

        let compressedFormat =
            LookupTable.lookupTable' [
                (ChannelFormat.RGB, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedRgbS3tcDxt1Ext)
                (ChannelFormat.RGBA, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt5, PixelInternalFormat.CompressedRgbaS3tcDxt5Ext)
                (ChannelFormat.RGB, ChannelType.UnsignedByte, true), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedSrgbS3tcDxt1Ext)
                (ChannelFormat.RGBA, ChannelType.UnsignedByte, true), (CompressedDataFormat.Dxt5, PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext)

                (ChannelFormat.BGR, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedRgbS3tcDxt1Ext)
                (ChannelFormat.BGRA, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt5, PixelInternalFormat.CompressedRgbaS3tcDxt5Ext)
                (ChannelFormat.BGR, ChannelType.UnsignedByte, true), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedSrgbS3tcDxt1Ext)
                (ChannelFormat.BGRA, ChannelType.UnsignedByte, true), (CompressedDataFormat.Dxt5, PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext)

                (ChannelFormat.Luminance, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedRedRgtc1)
            ]

        let private devilLock =
            let fi = typeof<PixImageDevil>.GetField("s_devilLock", Reflection.BindingFlags.Static ||| Reflection.BindingFlags.NonPublic)
            fi.GetValue(null)

        let perform (f : unit -> 'T) =
            lock devilLock (fun _ ->
                PixImageDevil.InitDevil()
                f()
            )

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Texture =

        let tryLoadCompressedFromFile (ctx : Context) (config : TextureParams) (path : string) =
            Devil.perform (fun () ->
                let img = IL.GenImage()

                try
                    IL.BindImage(img)
                    IL.LoadImage(path) |> IL.check "could not load image"

                    let channelType = IL.GetDataType()
                    let channelFormat = IL.GetInteger(IntName.ImageFormat) |> unbox<ChannelFormat>

                    match Devil.compressedFormat (channelFormat, channelType, config.wantSrgb) with
                    | Some (format, internalFormat) ->
                        ILU.FlipImage() |> IL.check "could not flip image"

                        let size = V2i(IL.GetInteger(IntName.ImageWidth), IL.GetInteger(IntName.ImageWidth))
                        let channels = IL.GetInteger(IntName.ImageChannels)
                        let sizeInBytes = IL.GetDXTCData(0n, 0, format)

                        if sizeInBytes = 0 then
                            Log.warn "Cannot load compressed data from '%s'" path
                            None

                        else
                            Log.line "compression: %.2f%%" (100.0 * float sizeInBytes / float (size.X * size.Y * channels))

                            let copy (dst : nativeint) =
                                IL.GetDXTCData(dst, sizeInBytes, format) |> ignore

                            let data =
                                PixelData.Compressed {
                                    Size        = V3i(size, 1)
                                    SizeInBytes = nativeint sizeInBytes
                                    CopyTo      = copy
                                }

                            let levels = if config.wantMipMaps then Fun.MipmapLevels(size) else 1
                            let texture = ctx.CreateTexture2D(size, levels, unbox internalFormat, 1)
                            data |> Texture.uploadPixelData texture config.wantMipMaps 0 V3i.Zero

                            IL.BindImage(0)

                            Some texture

                    | _ ->
                        None

                finally
                    IL.DeleteImage(img)
            )


[<AutoOpen>]
module ContextTextureUploadExtensions =

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

    let private useTemporaryAndBlit (dst : Texture) (level : int) (slice : int) (offset : V2i) (size : V2i) (f : Texture -> unit) =
        let context = dst.Context
        let temp = context.CreateTexture2D(size, 1, dst.Format, 1)
        try
            f temp
            context.Blit(temp, 0, 0, V2i.Zero, size, dst, level, slice, offset, size, true)
        finally
            context.Delete(temp)

    [<Extension; AbstractClass; Sealed>]
    type ContextTextureUploadExtensions =

        [<Extension>]
        static member CreateTexture(this : Context, data : ITexture) =
            using this.ResourceLock (fun _ ->
                match data with
                | FileTexture(info, file) ->
                    if isNull file then
                        this.CreateTexture <| NullTexture()
                    else
                        let compressed =
                            if info.wantCompressed then
                                file |> Texture.tryLoadCompressedFromFile this info
                            else
                                None

                        match compressed with
                        | Some t -> t
                        | _ ->
                            let pi = PixImage.Create(file)
                            let mm = PixImageMipMap [|pi|]
                            this.CreateTexture <| PixTexture2d(mm, info)

                | PixTexture2D(info, data) ->
                    let texture = this.CreateTexture2D(data.[0].Size, data.PixFormat, info, 1)
                    Texture.uploadPixImageMipMap texture true info.wantMipMaps 0 0 V2i.Zero data
                    texture

                | PixTextureCube(info, data) ->
                    let img = data.[CubeSide.NegativeX]
                    let texture = this.CreateTextureCube(img.[0].Size.X, img.PixFormat, info)
                    Texture.uploadPixImageCube texture true info.wantMipMaps 0 0 V2i.Zero data
                    texture

                | PixTexture3D(info, data) ->
                    let texture = this.CreateTexture3D(data.Size, data.PixFormat, info)
                    Texture.uploadPixVolume texture true info.wantMipMaps 0 V3i.Zero data
                    texture

                | :? NullTexture ->
                    Texture(this, 0, TextureDimension.Texture2D, 1, 1, V3i(-1,-1,-1), None, TextureFormat.Rgba8, 0L, false)

                | :? Texture as o ->
                    o

                | :? INativeTexture as data ->
                    let slices = if data.Count > 1 then data.Count else 0
                    let levels = if data.WantMipMaps then Fun.MipmapLevels(data.[0, 0].Size) else 1
                    let texture = this.CreateTexture(data.[0, 0].Size, data.Dimension, data.Format, slices, levels, 1)
                    Texture.uploadNativeTexture texture 0 0 data
                    texture

                | _ ->
                    failwith "unsupported texture data"
            )

        [<Extension>]
        static member Upload(this : Context, texture : Texture, level : int,
                             offset : V3i, size : V3i, source : NativeTensor4<'T>) =
            using this.ResourceLock (fun _ ->
                let levelSize = texture.GetSize level
                let offset = V3i(offset.X, levelSize.Y - offset.Y - size.Y, offset.Z) // flip y-offset

                // Multisampled texture requires blit
                if texture.IsMultisampled then
                    useTemporaryAndBlit texture level offset.Z offset.XY size.XY (fun temp ->
                        Texture.uploadNativeTensor4 temp true false 0 offset size source
                    )

                // Upload directly
                else
                    Texture.uploadNativeTensor4 texture true false level offset size source
            )

        [<Extension>]
        static member Upload(this : Context, texture : Texture, level : int, size : V3i, source : NativeTensor4<'T>) =
            this.Upload(texture, level, V3i.Zero, size, source)

        [<Extension>]
        static member Upload(this : Context, texture : Texture, level : int, slice : int, offset : V2i, source : PixImage) =
            using this.ResourceLock (fun _ ->
                let levelSize = texture.GetSize level
                let offset = V2i(offset.X, levelSize.Y - offset.Y - source.Size.Y) // flip y-offset

                // Multisampled texture requires blit
                if texture.IsMultisampled then
                    useTemporaryAndBlit texture level slice offset source.Size (fun temp ->
                        Texture.uploadPixImage temp true false 0 0 V2i.Zero source
                    )

                // Upload directly
                else
                    Texture.uploadPixImage texture true false level slice offset source
            )

        [<Extension>]
        static member Upload(this : Context, texture : Texture, level : int, slice : int, source : PixImage) =
            this.Upload(texture, level, slice, V2i.Zero, source)

        [<Extension>]
        static member Upload(this : Context, t : Texture, level : int, source : PixImage) =
            this.Upload(t, level, 0, source)