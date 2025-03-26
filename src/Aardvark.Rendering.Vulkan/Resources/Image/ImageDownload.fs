namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AutoOpen>]
module ImageDownloadExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Image =

        [<AutoOpen>]
        module private DepthStencilUtilities =

            let (|D16UNorm|_|) = function
                | VkFormat.D16Unorm
                | VkFormat.D16UnormS8Uint -> Some 65535us      // Divide by 2^16 - 1
                | _ -> None

            let (|D24UNorm|_|) = function
                | VkFormat.D24UnormS8Uint
                | VkFormat.X8D24UnormPack32 -> Some 16777215u  // Divide by 2^24 - 1
                | _ -> None

            let depthTexelSize =
                LookupTable.lookup [
                    VkFormat.D16Unorm, 2
                    VkFormat.D16UnormS8Uint, 2
                    VkFormat.D24UnormS8Uint, 4
                    VkFormat.X8D24UnormPack32, 4
                    VkFormat.D32Sfloat, 4
                    VkFormat.D32SfloatS8Uint, 4
                ]

        [<AutoOpen>]
        module private CopyUtilities =

            let private iter2<'T, 'U when 'T : unmanaged and 'U : unmanaged>
                             (f : nativeptr<uint8> -> nativeptr<uint8> -> unit) (src : nativeint) (dst : NativeMatrix<'U>) =
                let src =
                    let sa = int64 sizeof<'T>
                    let info = MatrixInfo(0L, dst.Size, V2l(sa, dst.Size.X * sa))
                    src |> NativeMatrix.ofNativeInt<uint8> info

                let dst =
                    let sa = int64 sizeof<'U>
                    let info = MatrixInfo(dst.Origin * sa, dst.Size, dst.Delta * sa)
                    dst.Address |> NativeMatrix.ofNativeInt<uint8> info

                (src, dst) ||> NativeMatrix.iterPtr2 (fun _ -> f)

            let inline copy<'T, 'U when 'T : unmanaged and 'U : unmanaged> (src : nativeint) (dst : NativeMatrix<'U>) =
                (src, dst) ||> iter2<'T, 'U> (fun src dst ->
                    let src : nativeptr<'T> = NativePtr.cast src
                    let dst : nativeptr<'T> = NativePtr.cast dst
                    NativePtr.write dst (NativePtr.read src)
                )

            let inline copyUnsignedNormalized (maxValue : ^T) (src : nativeint) (dst : NativeMatrix<'U>) =
                (src, dst) ||> iter2< ^T, 'U> (fun src dst ->
                    let src : nativeptr< ^T> = NativePtr.cast src
                    let dst : nativeptr<float32> = NativePtr.cast dst
                    NativePtr.write dst (float32 ((NativePtr.read src) &&& maxValue) / float32 maxValue)
                )


        let private resolveAndCopy (copy : ImageSubresource -> Command) (src : ImageSubresource) (device : Device)=
            let format = src.Image.Format

            let resolve() =
                let srcImg = src.Image

                let usage = VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit
                let resolved = device.CreateImage(srcImg.Size, srcImg.MipMapLevels, srcImg.Layers, 1, srcImg.Dimension, format, usage)  
                               
                try
                    let cmd =
                        { new Command() with
                            member x.Compatible = QueueFlags.All
                            member x.Enqueue (cmd : CommandBuffer) =
                                let layout = srcImg.Layout
                                cmd.Enqueue <| Command.TransformLayout(srcImg, VkImageLayout.TransferSrcOptimal)
                                cmd.Enqueue <| Command.TransformLayout(resolved, VkImageLayout.TransferDstOptimal)
                                cmd.Enqueue <| Command.ResolveMultisamples(src, resolved.[src.Aspect, src.Level, src.Slice])
                                cmd.Enqueue <| Command.TransformLayout(srcImg, layout)

                                cmd.AddResource srcImg
                                cmd.AddResource resolved
                        }

                    resolved.[src.Aspect, src.Level, src.Slice], cmd

                with
                | exn -> resolved.Dispose(); raise exn

            let srcResolved, cmdResolve =
                if src.Image.Samples > 1 then
                    resolve()
                else
                    src, Command.Nop

            let layout = srcResolved.Image.Layout

            device.perform {
                try
                    do! cmdResolve
                    do! Command.TransformLayout(srcResolved.Image, VkImageLayout.TransferSrcOptimal)
                    do! copy srcResolved
                    do! Command.TransformLayout(srcResolved.Image, layout)

                finally
                    if srcResolved <> src then
                        srcResolved.Image.Dispose()
            }


        let private downloadLevelCompressed (mode : CompressionMode) (offset : V3i) (size : V3i)
                                            (src : ImageSubresource) (dst : NativeTensor4<'T>) (device : Device) =

            let blockSize = mode |> CompressionMode.blockSize

            let alignedOffset = (offset / blockSize) * blockSize

            let alignedBufferSize =
                let size = offset - alignedOffset + size
                let blocks = mode |> CompressionMode.numberOfBlocks size.XYI
                blocks * blockSize

            let alignedSize =
                min alignedBufferSize (src.Size - alignedOffset.XYO)

            let buffer =
                let sizeInBytes = mode |> CompressionMode.sizeInBytes alignedBufferSize
                device.ReadbackMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (uint64 sizeInBytes)

            try
                (src, device) ||> resolveAndCopy (fun src ->
                    Command.Copy(src, alignedOffset, buffer, 0L, alignedBufferSize.XY, alignedSize)
                )

                buffer.Memory.Mapped (fun src ->
                    let dstInfo = dst.Info.SubXYWVolume(0L)
                    let offset = offset - alignedOffset
                    BlockCompression.decode mode offset.XY size.XY src dst.Address dstInfo
                )

            finally
                buffer.Dispose()


        let private downloadLevelColor (offset : V3i) (size : V3i) (format : Col.Format)
                                       (src : ImageSubresource) (dst : NativeTensor4<'T>) (device : Device) =
            let imageFormat = src.Image.Format
            let srcPixFormat = PixFormat(VkFormat.expectedType imageFormat, VkFormat.toColFormat imageFormat)

            let temp = device.ReadbackMemory.CreateTensorImage(V3i dst.Size, srcPixFormat, VkFormat.isSrgb imageFormat)

            try
                (src, device) ||> resolveAndCopy (fun resolved ->
                    Command.Copy(resolved, offset, temp, size)
                )
                temp.Read(format, dst)

            finally
                temp.Dispose()


        let downloadLevel (offset : V3i) (size : V3i) (format : Col.Format)
                          (src : ImageSubresource) (dst : NativeTensor4<'T>) (device : Device) =
            let dst, offset =
                if src.Image.IsCubeOr2D then
                    dst.SubTensor4(V4l.Zero, V4l(V3l size, dst.SW)).MirrorY(),
                    V3i(offset.X, src.Size.Y - offset.Y - int dst.SY, offset.Z) // flip y-offset
                else
                    dst, offset

            let textureFormat = (src.Image :> IBackendTexture).Format

            match textureFormat.CompressionMode with
            | CompressionMode.None ->
                downloadLevelColor offset size format src dst device

            | mode ->
                downloadLevelCompressed mode offset size src dst device


        let downloadLevelDepthStencil (offset : V2i) (src : ImageSubresource) (dst : Matrix<'T>) (device : Device) =
            if src.Aspect <> TextureAspect.Depth && src.Aspect <> TextureAspect.Stencil then
                failf "cannot download level of subresource with aspect %A (must be Depth or Stencil)" src.Aspect

            let size = V2i dst.Size

            let offset =
                V2i(offset.X, src.Size.Y - offset.Y - size.Y) // flip y-offset

            let format = src.Image.Format
            let aspect = src.Aspect

            let texelSize =
                if aspect = TextureAspect.Stencil then 1
                else depthTexelSize src.Image.Format

            let buffer =
                let sizeInBytes = texelSize * size.X * size.Y
                device.ReadbackMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (uint64 sizeInBytes)

            try
                (src, device) ||> resolveAndCopy (fun src ->
                    Command.Copy(src, offset.XYO, buffer, 0L, V2i.Zero, size.XYI)
                )

                buffer.Memory.Mapped (fun src ->
                    NativeMatrix.using dst (fun dst ->
                        let dst = dst.MirrorY()

                        if aspect = TextureAspect.Depth then
                            match format with
                            | D16UNorm max -> copyUnsignedNormalized max src dst
                            | D24UNorm max -> copyUnsignedNormalized max src dst
                            | _ -> copy<float32, 'T> src dst
                        else
                            copy<uint8, 'T> src dst
                    )
                )

            finally
                buffer.Dispose()


    [<AbstractClass; Sealed>]
    type ContextImageDownloadExtensions private() =

        [<Extension>]
        static member inline DownloadLevel(this : Device, src : ImageSubresource, dst : NativeTensor4<'T>,
                                           format : Col.Format, offset : V3i, size : V3i) =
            this |> Image.downloadLevel offset size format src dst

        [<Extension>]
        static member DownloadDepth(this : Device, src : ImageSubresource, dst : Matrix<float32>, offset : V2i) =
            if not <| src.Aspect.HasFlag TextureAspect.Depth then
                failf "cannot download depth data from subresource with aspect %A" src.Aspect

            let src = src.Image.[TextureAspect.Depth, src.Level, src.Slice]
            this |> Image.downloadLevelDepthStencil offset src dst

        [<Extension>]
        static member DownloadStencil(this : Device, src : ImageSubresource, dst : Matrix<int>, offset : V2i) =
            if not <| src.Aspect.HasFlag TextureAspect.Stencil then
                failf "cannot download stencil data from subresource with aspect %A" src.Aspect

            let src = src.Image.[TextureAspect.Stencil, src.Level, src.Slice]
            this |> Image.downloadLevelDepthStencil offset src dst