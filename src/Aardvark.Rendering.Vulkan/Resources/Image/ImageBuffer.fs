namespace Aardvark.Rendering.Vulkan

open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

type ImageBuffer(buffer : Buffer, size : V3i, pitch : V2i, format : TextureFormat) =
    inherit BufferDecorator(buffer)

    member x.ImageSize = size
    member x.ImagePitch = pitch
    member x.TextureFormat = format

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal ImageBuffer =

    [<Literal>]
    let internal defaultUsage = VkBufferUsageFlags.TransferSrcBit

    let create (format : TextureFormat) (size : V3i) (pitch : V2i) (sizeInBytes : uint64) (usage : VkBufferUsageFlags) (memory : IDeviceMemory) =
        let buffer = memory |> Buffer.create usage sizeInBytes
        new ImageBuffer(buffer, size, pitch, format)

[<AbstractClass; Sealed>]
type ImageBufferExtensions private() =

    [<Extension>]
    static member inline internal CreateImageBuffer(memory : IDeviceMemory, format : TextureFormat, size : V3i, pitch : V2i, sizeInBytes : uint64,
                                                    [<Optional; DefaultParameterValue(ImageBuffer.defaultUsage)>] usage : VkBufferUsageFlags)  =
        memory |> ImageBuffer.create format size pitch sizeInBytes usage

[<AutoOpen>]
module internal ImageBufferCommandExtensions =

    type Command with

        static member Copy(src : ImageBuffer, dst : ImageSubresource, dstOffset : V3i) =
            Command.Copy(src, 0L, src.ImagePitch, dst, dstOffset, src.ImageSize)


    type CopyCommand with

        static member Copy(src : ImageBuffer, dst : ImageSubresource, dstOffset : V3i) =
            CopyCommand.BufferToImageCmd(
                src.Handle,
                dst.Image.Handle,
                dst.Image.Layout,
                VkBufferImageCopy(
                    0UL, uint32 src.ImagePitch.X, uint32 src.ImagePitch.Y,
                    dst.VkImageSubresourceLayers,
                    VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                    VkExtent3D(src.ImageSize.X, src.ImageSize.Y, src.ImageSize.Z)
                ),
                src.Size
            )