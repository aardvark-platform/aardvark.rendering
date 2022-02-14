namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open System.Runtime.CompilerServices

[<AutoOpen>]
module ImageDownloadExtensions =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Image =

        let downloadLevel (offset : V3i) (size : V3i) (src : ImageSubresource) (dst : NativeTensor4<'T>) (device : Device) =
            let format = src.Image.Format
            let srcPixFormat = PixFormat(VkFormat.expectedType format, VkFormat.toColFormat format)

            let dst, srcOffset =
                if src.Image.IsCubeOr2D then
                    dst.SubTensor4(V4l.Zero, V4l(V3l size, dst.SW)).MirrorY(),
                    V3i(offset.X, src.Size.Y - offset.Y - int dst.SY, offset.Z) // flip y-offset
                else
                    dst, offset

            let temp = device.CreateTensorImage(V3i dst.Size, srcPixFormat, VkFormat.isSrgb src.Image.Format)

            let resolve() =
                let srcImg = src.Image

                let usage = VkImageUsageFlags.TransferDstBit ||| VkImageUsageFlags.TransferSrcBit
                let resolved = Image.create srcImg.Size srcImg.MipMapLevels srcImg.Layers 1 srcImg.Dimension format usage device

                let cmd =
                    let layout = srcImg.Layout

                    command {
                        do! Command.TransformLayout(srcImg, VkImageLayout.TransferSrcOptimal)
                        do! Command.TransformLayout(resolved, VkImageLayout.TransferDstOptimal)
                        do! Command.ResolveMultisamples(src, resolved.[src.Aspect, src.Level, src.Slice])
                        do! Command.TransformLayout(srcImg, layout)
                    }

                resolved.[src.Aspect, src.Level, src.Slice], cmd

            let srcResolved, cmdResolve =
                if src.Image.Samples > 1 then
                    resolve()
                else
                    src, Command.Nop

            try
                let cmd =
                    let layout = srcResolved.Image.Layout

                    command {
                        do! cmdResolve
                        do! Command.TransformLayout(srcResolved.Image, VkImageLayout.TransferSrcOptimal)
                        do! Command.Copy(srcResolved, srcOffset, temp, size)
                        do! Command.TransformLayout(srcResolved.Image, layout)
                    }

                device.GraphicsFamily.RunSynchronously(cmd)
                temp.Read(dst.Format, dst)
            finally
                temp.Dispose()
                if srcResolved <> src then
                    srcResolved.Image.Dispose()


    [<AbstractClass; Sealed; Extension>]
    type ContextImageDownloadExtensions private() =

        [<Extension>]
        static member inline DownloadLevel(this : Device, src : ImageSubresource, dst : NativeTensor4<'T>, offset : V3i, size : V3i) =
            this |> Image.downloadLevel offset size src dst