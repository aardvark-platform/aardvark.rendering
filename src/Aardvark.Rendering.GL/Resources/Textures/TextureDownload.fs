﻿namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open System.Runtime.CompilerServices
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module internal TextureDownloadImplementation =

    module Texture =

        let private readTextureLayer (texture : Texture) (level : int) (offset : V3i)
                                     (pixels : nativeint) (info : GeneralPixelDataInfo) (data : GeneralPixelData) =

            // Size of a slice in the output data
            let bytesPerSlice =
                info.ElementSize * info.Channels * data.Size.X * data.Size.Y

            let image = Image.Texture texture

            Image.readLayers image level offset.Z data.Size.Z (fun slice ->
                let dstOffset =
                    let index = slice - offset.Z
                    nativeint <| index * bytesPerSlice

                GL.ReadPixels(offset.X, offset.Y, data.Size.X, data.Size.Y, data.Format, data.Type, pixels + dstOffset)
                GL.Check "could not read pixels"
            )

        let downloadPixelData (texture : Texture) (level : int) (offset : V3i) (data : PixelData) =
            let target = texture |> TextureTarget.ofTexture
            let targetSlice = target |> TextureTarget.toSliceTarget offset.Z

            let info =
                data |> PixelData.getInfo texture.Context.PackAlignment

            GL.BindTexture(target, texture.Handle)
            GL.Check "could not bind texture"

            PixelBuffer.pack info.SizeInBytes (fun pbo ->
                let pixels = pbo.Pixels

                // In case we download the whole texture and it isn't arrayed, we can
                // avoid GL.GetTextureSubImage() which is not available on all systems! (e.g. MacOS)
                if offset = V3i.Zero && data.Size = texture.GetSize(level) && not texture.IsArray then
                    match data with
                    | PixelData.General d ->
                        GL.GetTexImage(targetSlice, level, d.Format, d.Type, pixels)
                    | PixelData.Compressed _ ->
                        GL.GetCompressedTexImage(targetSlice, level, pixels)

                else
                    if GL.ARB_get_texture_subimage then
                        let offset =
                            if texture.Dimension = TextureDimension.Texture1D then offset.XZO
                            else offset

                        match data with
                        | PixelData.General d ->
                            GL.Dispatch.GetTextureSubImage(texture.Handle, level, offset, d.Size, d.Format, d.Type, int info.SizeInBytes, pixels)
                        | PixelData.Compressed d ->
                            GL.Dispatch.GetCompressedTextureSubImage(texture.Handle, level, offset, d.Size, int info.SizeInBytes, pixels)

                    // We want to download a subregion but don't have GL_ARB_get_texture_subimage
                    // Use readPixels with FBO as fallback
                    else
                        match data, info with
                        | PixelData.General d, PixelDataInfo.General i ->
                            (i, d) ||> readTextureLayer texture level offset pixels

                        | _ ->
                            failwithf "[GL] Cannot download subwindow of compressed textures without glGetCompressedTextureSubImage (not available)"

                GL.Check "could not get texture image"

                pbo |> PixelBuffer.mapped BufferAccess.ReadOnly (fun src ->
                    (info, data) ||> PixelData.copy src
                )
            )

            GL.BindTexture(target, 0)
            GL.Check "could not unbind texture"

        let private downloadGeneralPixelData (texture : Texture) (level : int) (slice : int) (offset : V3i) (size : V3i)
                                             (pixelFormat : PixelFormat) (pixelType : PixelType)
                                             (copy : int -> int -> nativeint -> nativeint -> unit) =
            let offset =
                let flipped = texture.WindowOffset(level, offset, size)
                if texture.Dimension = TextureDimension.Texture3D then flipped
                else V3i(flipped.XY, slice)

            let pixelData =
                PixelData.General {
                    Size   = size
                    Type   = pixelType
                    Format = pixelFormat
                    Copy   = copy
                }

            downloadPixelData texture level offset pixelData


        let private downloadCompressedPixelData (texture : Texture) (level : int) (slice : int) (offset : V3i) (size : V3i)
                                                (mode : CompressionMode) (dst : nativeint) (dstInfo : Tensor4Info) =
            let offset =
                let flipped = texture.WindowOffset(level, offset, size)
                flipped.XY

            let blockSize = CompressionMode.blockSize mode

            let alignedOffset = (offset / blockSize) * blockSize

            let alignedSize =
                let size = offset - alignedOffset + size.XY
                let blocks = mode |> CompressionMode.numberOfBlocks size.XYI
                min (texture.GetSize(level) - alignedOffset.XYO) (blocks * blockSize)

            let sizeInBytes = mode |> CompressionMode.sizeInBytes alignedSize

            let copy (src : nativeint) =
                let dstInfo = dstInfo.MirrorY().SubXYWVolume(0L)
                let offset = offset.XY - alignedOffset
                BlockCompression.decode mode offset size.XY src dst dstInfo

            let pixelData =
                PixelData.Compressed {
                    Size = alignedSize
                    SizeInBytes = sizeInBytes
                    Copy = copy
                }

            downloadPixelData texture level (V3i(alignedOffset, slice)) pixelData


        let private downloadNative (texture : Texture) (level : int) (slice : int) (offset : V3i) (size : V3i)
                                   (startOffset : int) (maxElementSize : int) (pixelFormat : PixelFormat) (pixelType : PixelType)
                                   (dst : nativeint) (dstInfo : Tensor4Info) =
            match texture.Format.CompressionMode with
            | CompressionMode.None ->
                let bufferElementSize =
                    min maxElementSize pixelType.Size

                let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (src : nativeint) =
                    let srcInfo = Tensor4Info.deviceLayoutWithOffset texture.IsCubeOr2D startOffset elementSize alignedLineSize channels (V3l size)
                    NativeTensor4.copyBytesWithSize bufferElementSize src srcInfo dst dstInfo

                downloadGeneralPixelData texture level slice offset size pixelFormat pixelType copy

            | mode ->
                downloadCompressedPixelData texture level slice offset size mode dst dstInfo


        let downloadNativeTensor4<'T when 'T : unmanaged> (texture : Texture) (level : int) (slice : int)
                                                          (offset : V3i) (size : V3i)
                                                          (dstFormat : Col.Format) (dst : NativeTensor4<'T>) =
            let dstInfo = dst.Info.AsBytes<'T>()
            let pixelType = PixelType.ofType typeof<'T>
            let pixelFormat = PixelFormat.ofColFormat texture.Format.IsIntegerFormat dstFormat
            downloadNative texture level slice offset size 0 sizeof<'T> pixelFormat pixelType dst.Address dstInfo


        let downloadPixImage (texture : Texture) (level : int) (slice : int) (offset : V2i) (image : PixImage) =
            let size = V3i(image.Size, 1)
            let offset = V3i(offset, slice)
            let elementSize = image.ChannelSize
            let pixelType = PixelType.ofType image.PixFormat.Type
            let pixelFormat = PixelFormat.ofColFormat texture.Format.IsIntegerFormat image.Format

            image.Array |> NativeInt.pin (fun dst ->
                let dstInfo = image.VolumeInfo.ToXYWTensor4'().AsBytes(elementSize)
                downloadNative texture level slice offset size 0 elementSize pixelFormat pixelType dst dstInfo
            )


        let downloadPixVolume (texture : Texture) (level : int) (offset : V3i) (volume : PixVolume) =
            let elementSize = volume.ChannelSize
            let pixelType = PixelType.ofType volume.PixFormat.Type
            let pixelFormat = PixelFormat.ofColFormat texture.Format.IsIntegerFormat volume.Format

            volume.Array |> NativeInt.pin (fun dst ->
                let dstInfo = volume.Tensor4Info.AsBytes(elementSize)
                downloadNative texture level 0 offset volume.Size 0 elementSize pixelFormat pixelType dst dstInfo
            )


        let inline private copyUnsignedNormalizedDepth (matrix : Matrix<float32>) (shift : int) (maxValue : ^T)
                                                       (channels : int) (elementSize : int) (alignedLineSize : nativeint) (src : nativeint) =
            matrix.Data |> NativePtr.pinArr (fun dst ->
                let src =
                    let info = Tensor4Info.deviceLayout true elementSize alignedLineSize channels matrix.Size.XYI
                    src |> NativeTensor4.ofNativeInt<uint8> info

                let dst =
                    let info = matrix.AsVolume().Info.ToXYWTensor4'().AsBytes<float32>()
                    dst.Address |> NativeTensor4.ofNativeInt<uint8> info

                (src, dst) ||> NativeTensor4.iterPtr2 (fun _ src dst ->
                    let src : nativeptr<'T> = NativePtr.cast src
                    let dst : nativeptr<float32> = NativePtr.cast dst
                    NativePtr.write dst (float32 (((NativePtr.read src) >>> shift) &&& maxValue) / float32 maxValue)
                )
            )

        let private (|UnsignedNormalizedDepth|_|) = function
            | TextureFormat.DepthComponent16 ->
                // Divide by 2^16 - 1
                Some (PixelFormat.DepthComponent, PixelType.UnsignedShort, 0, 65535u, true) 

            | TextureFormat.DepthComponent24 ->
                // Divide by 2^32 - 1
                Some (PixelFormat.DepthComponent, PixelType.UnsignedInt, 0, 4294967295u, false)

            | TextureFormat.DepthComponent32 ->
                // Divide by 2^32 - 1
                Some (PixelFormat.DepthComponent, PixelType.UnsignedInt, 0, 4294967295u, false)

            | TextureFormat.Depth24Stencil8  ->
                // Shift right by 8 and divide by 2^24 - 1
                Some (PixelFormat.DepthStencil, PixelType.UnsignedInt248, 8, 16777215u, false)

            | _ -> None

        let private (|FloatingPointDepth|_|) = function
            | TextureFormat.DepthComponent32f -> Some (PixelFormat.DepthComponent, PixelType.Float)
            | TextureFormat.Depth32fStencil8  -> Some (PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev)
            | _ -> None

        let downloadDepth (texture : Texture) (level : int) (slice : int) (offset : V2i) (matrix : Matrix<float32>) =
            let size = V3i matrix.Size.XYI

            match texture.Format with
            | UnsignedNormalizedDepth (format, typ, shift, maxValue, is16bit) ->
                let copy =
                    if is16bit then
                        copyUnsignedNormalizedDepth matrix shift (uint16 maxValue)
                    else
                        copyUnsignedNormalizedDepth matrix shift (uint32 maxValue)

                let size = V3i(V2i matrix.Size, 1)
                let offset = V3i(offset, slice)
                downloadGeneralPixelData texture level slice offset size format typ copy

            | FloatingPointDepth (format, typ) ->
                matrix.Data |> NativePtr.pinArr (fun dst ->
                    let dstInfo = matrix.AsVolume().Info.ToXYWTensor4'().AsBytes<float32>()
                    downloadNative texture level slice offset.XYO size 0 sizeof<float32> format typ dst.Address dstInfo
                )

            | fmt -> failwithf "[GL] %A is not a supported depth format" fmt


        let private (|Stencil|_|) = function
            | TextureFormat.StencilIndex8    -> Some (PixelFormat.StencilIndex, PixelType.UnsignedByte, 0)
            | TextureFormat.Depth24Stencil8  -> Some (PixelFormat.DepthStencil, PixelType.UnsignedInt248, 0)
            | TextureFormat.Depth32fStencil8 -> Some (PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev, 4)
            | _ -> None

        let downloadStencil (texture : Texture) (level : int) (slice : int) (offset : V2i) (matrix : Matrix<int>) =
            let size = V3i matrix.Size.XYI

            match texture.Format with
            | Stencil(format, typ, byteOffset) ->
                matrix.Data |> NativePtr.pinArr (fun dst ->
                    let dstInfo = matrix.AsVolume().Info.ToXYWTensor4'().AsBytes<int>()
                    downloadNative texture level slice offset.XYO size byteOffset sizeof<uint8> format typ dst.Address dstInfo
                )

            | fmt -> failwithf "[GL] %A is not a supported stencil format" fmt

[<AutoOpen>]
module ContextTextureDownloadExtensions =

    let private download (texture : Texture) (level : int) (slice : int) (offset : V3i) (size : V3i)
                         (dl : int -> int -> V3i -> V3i -> Texture -> unit) =
        let context = texture.Context

        using context.ResourceLock (fun _ ->
            // Multisampled texture requires blit
            if texture.IsMultisampled then
                let temp = context.CreateTexture2D(size.XY, 1, texture.Format, 1)
                try
                    context.Blit(texture, level, slice, offset.XY, size.XY, temp, 0, 0, V2i.Zero, size.XY, false)
                    temp |> dl 0 0 V3i.Zero size
                finally
                    context.Delete(temp)

            // Download directly
            else
                texture |> dl level slice offset size
        )

    [<Extension; AbstractClass; Sealed>]
    type ContextTextureDownloadExtensions =

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int,
                               offset : V3i, size : V3i, target : NativeTensor4<'T>, format : Col.Format) =
            download texture level slice offset size (fun level slice offset size texture ->
                Texture.downloadNativeTensor4 texture level slice offset size format target
            )

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int,
                               size : V3i, target : NativeTensor4<'T>, format : Col.Format) =
            this.Download(texture, level, slice, V3i.Zero, size, target, format)

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int, offset : V2i, target : PixImage) =
            download texture level slice (V3i offset) (V3i target.Size) (fun level slice offset _ texture ->
                Texture.downloadPixImage texture level slice offset.XY target
            )

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int, target : PixImage) =
            this.Download(texture, level, slice, V2i.Zero, target)

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, slice : int) : PixImage =
            let fmt = TextureFormat.toDownloadFormat texture.Format
            let levelSize = texture.GetSize level
            let img = PixImage.Create(fmt, int64 levelSize.X, int64 levelSize.Y)
            this.Download(texture, level, slice, img)
            img

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int) : PixImage =
            this.Download(texture, level, 0)

        [<Extension>]
        static member Download(this : Context, texture : Texture) : PixImage =
            this.Download(texture, 0, 0)

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, offset : V3i, target : PixVolume) =
            download texture level 0 offset target.Size (fun level _ offset _ texture ->
                Texture.downloadPixVolume texture level offset target
            )

        [<Extension>]
        static member Download(this : Context, texture : Texture, level : int, target : PixVolume) =
            this.Download(texture, level, V3i.Zero, target)

        [<Extension>]
        static member Download(this : Context, texture : Texture, target : PixVolume) =
            this.Download(texture, 0, V3i.Zero, target)

        [<Extension>]
        static member DownloadStencil(this : Context, texture : Texture,
                                      level : int, slice : int, offset : V2i, size : V2i, target : Matrix<int>) =
            download texture level slice offset.XYO size.XYI (fun level slice offset _ texture ->
                Texture.downloadStencil texture level slice offset.XY target
            )

        [<Extension>]
        static member DownloadStencil(this : Context, texture : Texture,
                                      level : int, slice : int, offset : V2i, target : Matrix<int>) =
            this.DownloadStencil(texture, level, slice, offset, V2i target.Size, target)

        [<Extension>]
        static member DownloadStencil(this : Context, texture : Texture, level : int, slice : int, target : Matrix<int>) =
            this.DownloadStencil(texture, level, slice, V2i.Zero, target)

        [<Extension>]
        static member DownloadDepth(this : Context, texture : Texture,
                                    level : int, slice : int, offset : V2i, size : V2i, target : Matrix<float32>) =
            download texture level slice offset.XYO size.XYI (fun level slice offset _ texture ->
                Texture.downloadDepth texture level slice offset.XY target
            )

        [<Extension>]
        static member DownloadDepth(this : Context, texture : Texture,
                                    level : int, slice : int, offset : V2i, target : Matrix<float32>) =
           this.DownloadDepth(texture, level, slice, offset, V2i target.Size, target)

        [<Extension>]
        static member DownloadDepth(this : Context, texture : Texture, level : int, slice : int, target : Matrix<float32>) =
            this.DownloadDepth(texture, level, slice, V2i.Zero, target)