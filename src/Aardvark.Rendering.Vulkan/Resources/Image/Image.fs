namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"

// ===========================================================================================
// Format Conversions
// ===========================================================================================

[<AutoOpen>]
module ``Image Format Extensions`` =

    module internal VkFormat =
        let supportsLinearFiltering (device : Device) (format : VkFormat) =
            let features = device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, format)
            features.HasFlag VkFormatFeatureFlags.SampledImageFilterLinearBit

        let supportsMipmapGeneration (device : Device) (format : VkFormat) =
            let features = device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, format)
            features.HasFlag (
                VkFormatFeatureFlags.BlitSrcBit |||
                VkFormatFeatureFlags.BlitDstBit
            )

    module internal TextureFormat =
        let supportsMipmapGeneration (device : Device) (format : TextureFormat) =
            format |> VkFormat.ofTextureFormat |> VkFormat.supportsMipmapGeneration device

    module VkImageType =
        let ofTextureDimension =
            LookupTable.lookupTable [
                TextureDimension.Texture1D, VkImageType.D1d
                TextureDimension.Texture2D, VkImageType.D2d
                TextureDimension.Texture3D, VkImageType.D3d
                TextureDimension.TextureCube, VkImageType.D2d
            ]

    module VkIndexType =
        let ofType =
            LookupTable.lookupTable [
                typeof<int16>, VkIndexType.Uint16
                typeof<uint16>, VkIndexType.Uint16
                typeof<int32>, VkIndexType.Uint32
                typeof<uint32>, VkIndexType.Uint32
            ]

    type Device with

        member x.GetSupportedFormat(tiling : VkImageTiling, fmt : PixFormat, t : TextureParams) =
            let retry f = x.GetSupportedFormat(tiling, PixFormat(fmt.Type, f), t)

            match fmt.Format with
            | Col.Format.BGR    -> retry Col.Format.RGB
            | Col.Format.BGRA   -> retry Col.Format.RGBA
            | Col.Format.BGRP   -> retry Col.Format.RGBP
            | _ ->
                let test = VkFormat.ofPixFormat fmt t
                let features = x.PhysicalDevice.GetFormatFeatures(tiling, test)

                if features <> VkFormatFeatureFlags.None then
                    test
                else
                    match fmt.Format with
                    | Col.Format.BW         -> retry Col.Format.Gray
                    | Col.Format.Alpha      -> retry Col.Format.Gray
                    | Col.Format.Gray       -> retry Col.Format.NormalUV
                    | Col.Format.NormalUV   -> retry Col.Format.RGB
                    | Col.Format.GrayAlpha  -> retry Col.Format.RGB
                    | Col.Format.RGB        -> retry Col.Format.RGBA
                    | _ ->
                        failf "bad format"


// ===========================================================================================
// Image Resource Type
// ===========================================================================================

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkImageAspectFlags =
    let ofTextureAspect (a : TextureAspect) =
        a |> int |> unbox<VkImageAspectFlags>

    let toTextureAspect (a : VkImageAspectFlags) =
        a |> int |> unbox<TextureAspect>

[<RequireQualifiedAccess>]
type ImageExportMode =
    | None
    | Export of preferArray: bool

type Image =
    class
        inherit Resource<VkImage>

        val public Size : V3i
        val public MipMapLevels : int
        val public Layers : int
        val public Samples : int
        val public Dimension : TextureDimension
        val public Format : VkFormat
        val public Memory : DevicePtr
        val public PeerHandles : VkImage[]
        val public SamplerLayout : VkImageLayout

        // ISSUE: This is not safe, generally it's not possible to track the layout
        val mutable public Layout : VkImageLayout

        override x.Destroy() =
            if x.Device.Handle <> 0n && x.Handle.IsValid then
                VkRaw.vkDestroyImage(x.Device.Handle, x.Handle, NativePtr.zero)
                for i = 0 to x.PeerHandles.Length - 1 do
                    VkRaw.vkDestroyImage(x.Device.Handle, x.PeerHandles.[i], NativePtr.zero)
                    x.PeerHandles.[i] <- VkImage.Null
                x.Memory.Dispose()
                x.Handle <- VkImage.Null                                        

        member x.Count =
            match x.Dimension with
            | TextureDimension.TextureCube -> x.Layers / 6
            | _ -> x.Layers

        interface ITexture with
            member x.WantMipMaps = x.MipMapLevels > 1

        interface IBackendTexture with
            member x.Runtime = x.Device.Runtime :> ITextureRuntime
            member x.Handle = x.Handle :> obj
            member x.Count = x.Count
            member x.Dimension = x.Dimension
            member x.Format = VkFormat.toTextureFormat x.Format
            member x.MipMapLevels = x.MipMapLevels
            member x.Samples = x.Samples
            member x.Size = x.Size

        interface IRenderbuffer with
            member x.Runtime = x.Device.Runtime :> ITextureRuntime
            member x.Size = x.Size.XY
            member x.Samples = x.Samples
            member x.Format = VkFormat.toTextureFormat x.Format
            member x.Handle = x.Handle :> obj

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        member x.IsNull = x.Handle.IsNull

        member x.Item with get(aspect : TextureAspect) = ImageSubresourceRange(x, aspect, 0, x.MipMapLevels, 0, x.Layers)
        member x.Item with get(aspect : TextureAspect, level : int) = ImageSubresourceLayers(x, aspect, level, 0, x.Layers)
        member x.Item with get(aspect : TextureAspect, level : int, slice : int) = ImageSubresource(x, aspect, level, slice)

        member x.GetSlice(aspect : TextureAspect, minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            x.[aspect].GetSlice(minLevel, maxLevel, minSlice, maxSlice)

        member x.GetSlice(aspect : TextureAspect, minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            x.[aspect].GetSlice(minLevel, maxLevel, slice)

        member x.GetSlice(aspect : TextureAspect, level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            x.[aspect].GetSlice(level, minSlice, maxSlice)

        override x.ToString() =
            sprintf "0x%08X" x.Handle.Handle

        new(device, handle, size, levels, layers, samples, dimension, format, memory, layout,
            [<Optional; DefaultParameterValue(VkImageLayout.ShaderReadOnlyOptimal)>] samplerLayout,
            [<Optional; DefaultParameterValue(null : VkImage[])>] peerHandles) =
            {
                inherit Resource<_>(device, handle);
                Size = size
                MipMapLevels = levels
                Layers = layers
                Samples = samples
                Dimension = dimension
                Format = format
                Memory = memory
                Layout = layout
                SamplerLayout = samplerLayout
                PeerHandles = if isNull peerHandles then [||] else peerHandles
            }
    end

and internal ExportedImage =
    class
        inherit Image
        val public PreferArray : bool

        member x.ExternalMemory =
            { Block  = x.Memory.Memory.ExternalBlock
              Offset = x.Memory.Offset
              Size   = x.Memory.Size }

        member x.IsArray =
            x.Count > 1 || x.PreferArray

        interface IExportedBackendTexture with
            member x.IsArray = x.IsArray
            member x.Memory = x.ExternalMemory

        new(device, handle, size, levels, layers, samples, dimension, format, preferArray, memory, layout,
            [<Optional; DefaultParameterValue(VkImageLayout.ShaderReadOnlyOptimal)>] samplerLayout : VkImageLayout,
            [<Optional; DefaultParameterValue(null : VkImage[])>] peerHandles : VkImage[]) =
            {
                inherit Image(device, handle, size, levels, layers, samples, dimension, format, memory, layout, samplerLayout, peerHandles)
                PreferArray = preferArray
            }

    end

and ImageSubresourceRange(image : Image, aspect : TextureAspect, baseLevel : int, levelCount : int, baseSlice : int, sliceCount : int) =
    let maxSize =
        if baseLevel = 0 then
            image.Size
        else
            let imageSize = image.Size
            let divisor = 1 <<< baseLevel
            V3i(
                max 1 (imageSize.X / divisor),
                max 1 (imageSize.Y / divisor),
                max 1 (imageSize.Z / divisor)
            )

    let flags =
        VkImageAspectFlags.ofTextureAspect aspect

    member x.VkImageAspectFlags = flags

    member x.VkImageSubresourceRange =
        VkImageSubresourceRange(
            flags,
            uint32 baseLevel,
            uint32 levelCount,
            uint32 baseSlice,
            uint32 sliceCount
        )

    member x.Aspect = aspect
    member x.Image = image

    member x.BaseLevel = baseLevel
    member x.LevelCount = levelCount
    member x.BaseSlice = baseSlice
    member x.SliceCount = sliceCount

    member x.MaxSize = maxSize

    member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
        let levelTop = levelCount - 1
        let sliceTop = sliceCount - 1
        let minLevel = defaultArg minLevel 0            |> clamp 0 levelTop
        let maxLevel = defaultArg maxLevel levelTop     |> clamp minLevel levelTop
        let minSlice = defaultArg minSlice 0            |> clamp 0 sliceTop
        let maxSlice = defaultArg maxSlice sliceTop     |> clamp minSlice sliceTop
        ImageSubresourceRange(image, aspect, baseLevel + minLevel, 1 + maxLevel - minLevel, baseSlice + minSlice, 1 + maxSlice - minSlice)

    member x.GetSlice(level : int, minSlice : Option<int>, maxSlice : Option<int>) =
        let sliceTop = sliceCount - 1
        let minSlice = defaultArg minSlice 0            |> clamp 0 sliceTop
        let maxSlice = defaultArg maxSlice sliceTop     |> clamp minSlice sliceTop
        ImageSubresourceLayers(image, aspect, baseLevel + level, baseSlice + minSlice, 1 + maxSlice - minSlice)

    member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
        let levelTop = levelCount - 1
        let minLevel = defaultArg minLevel 0            |> clamp 0 levelTop
        let maxLevel = defaultArg maxLevel levelTop     |> clamp minLevel levelTop
        ImageSubresourceLevels(image, aspect, baseLevel + minLevel, 1 + maxLevel - minLevel, baseSlice + slice)

    member x.Item with get(level : int, slice : int) = ImageSubresource(image, aspect, baseLevel + level, baseSlice + slice)

    override x.ToString() =
        let minLevel = baseLevel
        let maxLevel = minLevel + levelCount - 1
        let minSlice = baseSlice
        let maxSlice = minSlice + sliceCount - 1
        match (minLevel = maxLevel), (minSlice = maxSlice) with
            | true,     true    -> sprintf "%A[%A, %d, %d]" image aspect minLevel minSlice
            | true,     false   -> sprintf "%A[%A, %d, %d..%d]" image aspect minLevel minSlice maxSlice
            | false,    true    -> sprintf "%A[%A, %d..%d, %d]" image aspect minLevel maxLevel minSlice
            | false,    false   -> sprintf "%A[%A, %d..%d, %d..%d]" image aspect minLevel maxLevel minSlice maxSlice


and ImageSubresourceLayers(image : Image, aspect : TextureAspect, level : int, baseSlice : int, sliceCount : int) =
    inherit ImageSubresourceRange(image, aspect, level, 1, baseSlice, sliceCount)
    member x.Level = level
    member x.Size = x.MaxSize
    member x.Item with get (slice : int) = x.[0, slice]
    member x.GetSlice (minSlice : Option<int>, maxSlice : Option<int>) = x.GetSlice(0, minSlice, maxSlice)
    member x.VkImageSubresourceLayers = VkImageSubresourceLayers(x.VkImageAspectFlags, uint32 level, uint32 baseSlice, uint32 sliceCount)

and ImageSubresourceLevels(image : Image, aspect : TextureAspect, baseLevel : int, levelCount : int, slice : int) =
    inherit ImageSubresourceRange(image, aspect, baseLevel, levelCount, slice, 1)
    member x.Slice = slice
    member x.Item with get (i : int) = x.[i, 0]
    member x.GetSlice (minLevel : Option<int>, maxLevel : Option<int>) = x.GetSlice(minLevel, maxLevel, 0)

and ImageSubresource(image : Image, aspect : TextureAspect, level : int, slice : int) =
    inherit ImageSubresourceLayers(image, aspect, level, slice, 1)
    member x.Slice = slice
    member x.VkImageSubresource = VkImageSubresource(x.VkImageAspectFlags, uint32 level, uint32 slice)

type ImageProperties =
    { Dimension      : TextureDimension
      Format         : TextureFormat
      IsMultisampled : bool }

[<AutoOpen>]
module internal ImagePropertiesExtensions =

    type FShade.GLSL.GLSLSamplerType with
        member x.Properties =
            let format =
                if x.isShadow then TextureFormat.Depth24Stencil8
                elif x.IsInteger then TextureFormat.Rgba8i
                else TextureFormat.Rgba8

            { Dimension      = x.dimension.TextureDimension
              Format         = format
              IsMultisampled = x.isMS }

    type FShade.GLSL.GLSLImageType with
        member x.Properties =
            let format =
                match x.format with
                | Some fmt -> fmt.TextureFormat
                | _ -> if x.IsInteger then TextureFormat.Rgba8i else TextureFormat.Rgba8

            { Dimension      = x.dimension.TextureDimension
              Format         = format
              IsMultisampled = x.isMS }

[<AutoOpen>]
module DeviceTensorCommandExtensions =

    type CopyCommand with


        static member Copy(src : ImageSubresourceLayers, srcOffset : V3i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            CopyCommand.ImageToImageCmd(
                src.Image.Handle,
                VkImageLayout.TransferSrcOptimal,
                dst.Image.Handle,
                VkImageLayout.TransferDstOptimal,
                VkImageCopy(
                    src.VkImageSubresourceLayers,
                    VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                    dst.VkImageSubresourceLayers,
                    VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                    VkExtent3D(size.X, size.Y, size.Z)
                ),
                0L
            )

        static member Copy(src : ImageSubresourceLayers, dst : ImageSubresourceLayers) =
            CopyCommand.Copy(src, V3i.Zero, dst, V3i.Zero, src.Size)

        static member TransformLayout(img : ImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout) =
            CopyCommand.TransformLayout(img.Image.Handle, img.VkImageSubresourceRange, srcLayout, dstLayout)

        static member Release(img : ImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout, dstQueueFamily : DeviceQueueFamily) =
            CopyCommand.Release(img.Image.Handle, img.VkImageSubresourceRange, srcLayout, dstLayout, dstQueueFamily.Index)

        static member Release(img : ImageSubresourceRange, layout : VkImageLayout, dstQueueFamily : DeviceQueueFamily) =
            CopyCommand.Release(img.Image.Handle, img.VkImageSubresourceRange, layout, layout, dstQueueFamily.Index)

        static member SyncImage(img : ImageSubresourceRange, layout : VkImageLayout) =
            CopyCommand.SyncImage(img.Image.Handle, img.VkImageSubresourceRange, layout)


// ===========================================================================================
// Image Command Extensions
// ===========================================================================================
[<AutoOpen>]
module ``Image Command Extensions`` =

    open KHRSwapchain
    open Vulkan11

    type CommandBuffer with

        member internal cmd.ImageBarrier(img : ImageSubresourceRange,
                                         srcLayout : VkImageLayout, dstLayout : VkImageLayout,
                                         srcStage : VkPipelineStageFlags, srcAccess : VkAccessFlags,
                                         dstStage : VkPipelineStageFlags, dstAccess : VkAccessFlags,
                                         srcQueue : uint32, dstQueue : uint32)  =

            let srcStage, srcAccess = (srcStage, srcAccess) ||> filterSrcStageAndAccess cmd.QueueFamily.Stages
            let dstStage, dstAccess = (dstStage, dstAccess) ||> filterDstStageAndAccess cmd.QueueFamily.Stages

            let imageMemoryBarrier =
                VkImageMemoryBarrier(
                    srcAccess, dstAccess,
                    srcLayout, dstLayout,
                    srcQueue, dstQueue,
                    img.Image.Handle,
                    img.VkImageSubresourceRange
                )

            imageMemoryBarrier |> pin (fun pBarrier ->
                VkRaw.vkCmdPipelineBarrier(
                    cmd.Handle,
                    srcStage, dstStage,
                    VkDependencyFlags.None,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero,
                    1u, pBarrier
                )
            )

    type Command with

        static member ImageBarrier(img : ImageSubresourceRange,
                                   srcLayout : VkImageLayout, dstLayout : VkImageLayout,
                                   srcStage : VkPipelineStageFlags, srcAccess : VkAccessFlags,
                                   dstStage : VkPipelineStageFlags, dstAccess : VkAccessFlags) =

            if img.Image.IsNull || dstLayout = VkImageLayout.Undefined || dstLayout = VkImageLayout.Preinitialized then
                Command.Nop
            else
                { new Command() with
                    member x.Compatible = QueueFlags.All
                    member x.Enqueue (cmd : CommandBuffer) =
                        cmd.AppendCommand()

                        cmd.ImageBarrier(
                            img, srcLayout, dstLayout,
                            srcStage, srcAccess, dstStage, dstAccess,
                            VkQueueFamilyIgnored, VkQueueFamilyIgnored
                        )

                        img.Image.Layout <- dstLayout

                        [img.Image]
                }

        static member Acquire(src : ImageSubresourceRange, srcLayout : VkImageLayout, dstLayout : VkImageLayout, srcQueue : DeviceQueueFamily) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =
                    cmd.AppendCommand()

                    cmd.ImageBarrier(
                        src, srcLayout, dstLayout,
                        VkPipelineStageFlags.TopOfPipeBit, VkAccessFlags.None,
                        VkImageLayout.toDstStageFlags dstLayout,
                        VkImageLayout.toDstAccessFlags dstLayout,
                        uint32 srcQueue.Index,
                        uint32 cmd.QueueFamily.Index
                    )

                    [src.Image]
            }

        static member Acquire(src : ImageSubresourceRange, layout : VkImageLayout, srcQueue : DeviceQueueFamily) =
            Command.Acquire(src, layout, layout, srcQueue)

        static member Copy(src : ImageSubresourceLayers, srcOffset : V3i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            if src.SliceCount <> dst.SliceCount then
                failf "cannot copy image: { srcSlices = %A, dstSlices = %A }" src.SliceCount dst.SliceCount

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let copy =
                        VkImageCopy(
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    copy |> pin (fun pCopy ->
                        VkRaw.vkCmdCopyImage(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Image.Handle, dst.Image.Layout, 1u, pCopy)
                    )

                    [src.Image; dst.Image]
            }

        static member Copy(src : ImageSubresourceLayers, dst : ImageSubresourceLayers) =
            let srcSize = src.Size
            let dstSize = dst.Size

            if srcSize <> dstSize then
                failf "cannot copy image: { srcSize = %A; dstSize = %A }" srcSize dstSize

            Command.Copy(src, V3i.Zero, dst, V3i.Zero, srcSize)

        static member Copy(src : ImageSubresourceRange, dst : ImageSubresourceRange) =
            if src.LevelCount <> dst.LevelCount then
                failf "cannot copy image: { srcLevels = %A; dstLevels = %A }" src.LevelCount dst.LevelCount

            if src.SliceCount <> dst.SliceCount then
                failf "cannot copy image: { srcSlices = %A; dstSlices = %A }" src.SliceCount dst.SliceCount

            if src.MaxSize <> dst.MaxSize then
                failf "cannot copy image: { srcSize = %A; dstSize = %A }" src.LevelCount dst.LevelCount

            command {
                for i in 0 .. src.LevelCount-1 do
                    do! Command.Copy(src.[i, *], dst.[i, *])
            }


        static member Copy(src : Buffer, srcOffset : int64, srcStride : V2i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let copy =
                        VkBufferImageCopy(
                            uint64 srcOffset,
                            uint32 srcStride.X,
                            uint32 srcStride.Y,
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    copy |> pin (fun pCopy ->
                        VkRaw.vkCmdCopyBufferToImage(cmd.Handle, src.Handle, dst.Image.Handle, dst.Image.Layout, 1u, pCopy)
                    )

                    [src; dst.Image]
            }

        static member Copy(src : ImageSubresourceLayers, srcOffset : V3i, dst : Buffer, dstOffset : int64, dstStride : V2i, size : V3i) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let copy =
                        VkBufferImageCopy(
                            uint64 dstOffset,
                            uint32 dstStride.X,
                            uint32 dstStride.Y,
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    copy |> pin (fun pCopy ->
                        VkRaw.vkCmdCopyImageToBuffer(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Handle, 1u, pCopy)
                    )

                    [src.Image; dst]
            }

        static member ResolveMultisamples(src : ImageSubresourceLayers, srcLayout : VkImageLayout, srcOffset : V3i,
                                          dst : ImageSubresourceLayers, dstLayout : VkImageLayout, dstOffset : V3i,
                                          size : V3i) =

            if src.SliceCount <> dst.SliceCount then
                failf "cannot resolve image: { srcSlices = %A; dstSlices = %A }" src.SliceCount dst.SliceCount

            if dst.Aspect <> TextureAspect.Color then
                failf "cannot automatically resolve non-color textures"

            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue (cmd : CommandBuffer) =
                    let resolve =
                        VkImageResolve(
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    resolve |> pin (fun pResolve ->
                        VkRaw.vkCmdResolveImage(cmd.Handle, src.Image.Handle, srcLayout, dst.Image.Handle, dstLayout, 1u, pResolve)
                    )

                    [src.Image; dst.Image]
            }

        static member ResolveMultisamples(src : ImageSubresourceLayers, srcOffset : V3i, dst : ImageSubresourceLayers, dstOffset : V3i, size : V3i) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue (cmd : CommandBuffer) =
                    Command.ResolveMultisamples(src, src.Image.Layout, srcOffset, dst, dst.Image.Layout, dstOffset, size).Enqueue(cmd)
            }

        static member ResolveMultisamples(src : ImageSubresourceLayers, srcLayout : VkImageLayout,
                                          dst : ImageSubresourceLayers, dstLayout : VkImageLayout) =
            if src.Size <> dst.Size then
                failf "cannot copy image: { srcSize = %A; dstSize = %A }" src.LevelCount dst.LevelCount

            Command.ResolveMultisamples(src, srcLayout, V3i.Zero, dst, dstLayout, V3i.Zero, src.Size)

        static member ResolveMultisamples(src : ImageSubresourceLayers, dst : ImageSubresourceLayers) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue (cmd : CommandBuffer) =
                    Command.ResolveMultisamples(src, src.Image.Layout, dst, dst.Image.Layout).Enqueue(cmd)
            }

        static member Blit(src : ImageSubresourceLayers, srcLayout : VkImageLayout, srcRange : Box3i, dst : ImageSubresourceLayers, dstLayout : VkImageLayout, dstRange : Box3i, filter : VkFilter) =
            { new Command() with
                member x.Compatible = QueueFlags.Graphics
                member x.Enqueue cmd =

                    let mutable srcOffsets = VkOffset3D_2()

                    let inline extend (b : Box3i) =
                        let mutable min = b.Min
                        let mutable max = b.Max

                        if max.X >= min.X then max.X <- 1 + max.X
                        else min.X <- min.X + 1

                        if max.Y >= min.Y then max.Y <- 1 + max.Y
                        else min.Y <- min.Y + 1

                        if max.Z >= min.Z then max.Z <- 1 + max.Z
                        else min.Z <- min.Z + 1

                        Box3i(min, max)

                    let srcRange = extend srcRange
                    let dstRange = extend dstRange

                    srcOffsets.[0] <- VkOffset3D(srcRange.Min.X, srcRange.Min.Y, srcRange.Min.Z)
                    srcOffsets.[1] <- VkOffset3D(srcRange.Max.X, srcRange.Max.Y, srcRange.Max.Z)

                    let mutable dstOffsets = VkOffset3D_2()
                    dstOffsets.[0] <- VkOffset3D(dstRange.Min.X, dstRange.Min.Y, dstRange.Min.Z)
                    dstOffsets.[1] <- VkOffset3D(dstRange.Max.X, dstRange.Max.Y, dstRange.Max.Z)


                    let blit =
                        VkImageBlit(
                            src.VkImageSubresourceLayers,
                            srcOffsets,
                            dst.VkImageSubresourceLayers,
                            dstOffsets
                        )

                    cmd.AppendCommand()
                    blit |> pin (fun pBlit ->
                        VkRaw.vkCmdBlitImage(cmd.Handle, src.Image.Handle, srcLayout, dst.Image.Handle, dstLayout, 1u, pBlit, filter)
                    )

                    [src.Image; dst.Image]
            }

        static member Blit(src : ImageSubresourceLayers, srcLayout : VkImageLayout, dst : ImageSubresourceLayers, dstLayout : VkImageLayout, dstRange : Box3i, filter : VkFilter) =
            Command.Blit(src, srcLayout, Box3i(V3i.Zero, src.Size - V3i.III), dst, dstLayout, dstRange, filter)

        static member Blit(src : ImageSubresourceLayers, srcLayout : VkImageLayout, srcRange : Box3i, dst : ImageSubresourceLayers, dstLayout : VkImageLayout, filter : VkFilter) =
            Command.Blit(src, srcLayout, srcRange, dst, dstLayout, Box3i(V3i.Zero, dst.Size - V3i.III), filter)

        static member Blit(src : ImageSubresourceLayers, srcLayout : VkImageLayout, dst : ImageSubresourceLayers, dstLayout : VkImageLayout, filter : VkFilter) =
            Command.Blit(src, srcLayout, Box3i(V3i.Zero, src.Size - V3i.III), dst, dstLayout, Box3i(V3i.Zero, dst.Size - V3i.III), filter)

        static member Sync(img : ImageSubresourceRange, layout : VkImageLayout, srcAccess : VkAccessFlags, dstAccess : VkAccessFlags) =
            Command.ImageBarrier(
                img, layout, layout,
                VkImageLayout.toSrcStageFlags layout, srcAccess,
                VkImageLayout.toDstStageFlags layout, dstAccess
            )

        static member Sync(img : ImageSubresourceRange, layout : VkImageLayout, srcStage : VkPipelineStageFlags, srcAccess : VkAccessFlags) =
            Command.ImageBarrier(
                img, layout, layout,
                srcStage, srcAccess,
                VkImageLayout.toDstStageFlags layout,
                VkImageLayout.toDstAccessFlags layout
            )

        static member GenerateMipMaps (img : ImageSubresourceRange, [<Optional; DefaultParameterValue(0)>] baseLevel : int) =
            if img.Image.IsNull || baseLevel + 1 >= img.LevelCount then
                Command.Nop
            else
                if not <| VkFormat.supportsMipmapGeneration img.Image.Device img.Image.Format then
                    raise <| ArgumentException($"[Vk] Format {img.Image.Format} does not support mipmap generation.")

                let filter =
                    if VkFormat.supportsLinearFiltering img.Image.Device img.Image.Format then
                        VkFilter.Linear
                    else
                        Log.warn "[Vk] Format %A does not support linear filtering, using nearest filtering for mipmap generation" img.Image.Format
                        VkFilter.Nearest

                { new Command() with
                    member x.Compatible = QueueFlags.Graphics
                    member x.Enqueue cmd =
                        let oldLayout = img.Image.Layout
                        cmd.Enqueue <| Command.TransformLayout(img.[baseLevel,*], oldLayout, VkImageLayout.TransferSrcOptimal)

                        for l = baseLevel + 1 to img.LevelCount - 1 do
                            cmd.Enqueue <| Command.TransformLayout(img.[l,*], oldLayout, VkImageLayout.TransferDstOptimal)
                            cmd.Enqueue <| Command.Blit(img.[l - 1, *], VkImageLayout.TransferSrcOptimal, img.[l, *], VkImageLayout.TransferDstOptimal, filter)
                            cmd.Enqueue <| Command.TransformLayout(img.[l,*], VkImageLayout.TransferDstOptimal, VkImageLayout.TransferSrcOptimal)

                        cmd.Enqueue <| Command.TransformLayout(img.[baseLevel .. img.LevelCount - 1,*], VkImageLayout.TransferSrcOptimal, oldLayout)

                        [img.Image]
                }

        static member TransformLayout(img : ImageSubresourceRange, source : VkImageLayout, target : VkImageLayout) =
            Command.ImageBarrier(
                img, source, target,
                VkImageLayout.toSrcStageFlags source,
                VkImageLayout.toSrcAccessFlags source,
                VkImageLayout.toDstStageFlags target,
                VkImageLayout.toDstAccessFlags target
            )

        static member TransformLayout(img : Image, levels : Range1i, slices : Range1i, target : VkImageLayout) =
            if img.IsNull || target = VkImageLayout.Undefined || target = VkImageLayout.Preinitialized then
                Command.Nop
            else
                { new Command() with
                    member x.Compatible = QueueFlags.All
                    member x.Enqueue (cmd : CommandBuffer) =
                        if img.Layout = target then
                            []
                        else
                            let source = img.Layout
                            let aspect = VkFormat.toAspect img.Format
                            Command.TransformLayout(img.[unbox (int aspect), levels.Min .. levels.Max, slices.Min .. slices.Max], source, target).Enqueue(cmd)
                }

        static member TransformLayout(img : Image, level : int, slice : int, target : VkImageLayout) =
            Command.TransformLayout(img, Range1i(level), Range1i(slice), target)

        static member TransformLayout(img : Image, target : VkImageLayout) =
            Command.TransformLayout(img, Range1i(0, img.MipMapLevels - 1), Range1i(0, img.Layers - 1), target)

        static member SyncPeers (img : ImageSubresourceLayers, ranges : array<Range1i * Box3i>) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue(cmd) =

                    let mutable totalSize = 0L

                    if img.Image.PeerHandles.Length > 0 then
                        cmd.AppendCommand()

                        let baseImage = img.Image
                        let deviceIndices = baseImage.Device.AllIndicesArr


                        for di in deviceIndices do
                            VkRaw.vkCmdSetDeviceMask(cmd.Handle, 1u <<< int di)

                            let srcSlices, srcRange = ranges.[int di]

                            for ci in 0 .. baseImage.PeerHandles.Length - 1 do
                                let srcSub = img.[srcSlices.Min .. srcSlices.Max].VkImageSubresourceLayers
                                let copy =
                                    VkImageCopy(
                                        srcSub,
                                        VkOffset3D(srcRange.Min.X, srcRange.Min.Y, srcRange.Min.Z),
                                        srcSub,
                                        VkOffset3D(srcRange.Min.X, srcRange.Min.Y, srcRange.Min.Z),
                                        VkExtent3D(1+srcRange.SizeX, 1+srcRange.SizeY, 1+srcRange.SizeZ)
                                    )

                                if di = 0u then
                                    let imgSize = int64 copy.extent.width * int64 copy.extent.height * int64 copy.extent.depth * 4L
                                    totalSize <- totalSize + imgSize * int64 copy.srcSubresource.layerCount

                                copy |> pin (fun pCopy ->
                                    VkRaw.vkCmdCopyImage(
                                        cmd.Handle,
                                        baseImage.Handle, VkImageLayout.TransferSrcOptimal,
                                        baseImage.PeerHandles.[ci], VkImageLayout.TransferDstOptimal,
                                        1u, pCopy
                                    )
                                )

                        VkRaw.vkCmdSetDeviceMask(cmd.Handle, baseImage.Device.AllMask)

                        let mem =
                            VkMemoryBarrier(
                                VkAccessFlags.TransferWriteBit,
                                VkAccessFlags.TransferReadBit ||| VkAccessFlags.TransferWriteBit
                            )
                        mem |> pin (fun pMem ->
                            VkRaw.vkCmdPipelineBarrier(
                                cmd.Handle,
                                VkPipelineStageFlags.TransferBit,
                                VkPipelineStageFlags.TransferBit,
                                VkDependencyFlags.DeviceGroupBit,
                                1u, pMem,
                                0u, NativePtr.zero,
                                0u, NativePtr.zero
                            )
                        )

                    [img.Image]
            }

        static member SyncPeersDefault(img : Image, dstLayout : VkImageLayout) =
            if img.PeerHandles.Length > 0 then
                let device = img.Device
                let arrayRange = Range1i(0, img.Layers - 1)
                let ranges =
                    let range =
                        {
                            frMin = V2i.Zero;
                            frMax = img.Size.XY - V2i.II
                            frLayers = arrayRange
                        }
                    range.Split(int device.AllCount)

                command {
                    do! Command.TransformLayout(img, VkImageLayout.TransferSrcOptimal)

                    let aspect = (img :> IBackendTexture).Format.Aspect

                    let subResource = img.[aspect, 0]
                    let ranges =
                        ranges |> Array.map (fun { frMin = min; frMax = max; frLayers = layers} ->
                            layers, Box3i(V3i(min,0), V3i(max, 0))
                        )

                    do! Command.SyncPeers(subResource, ranges)
                    do! Command.TransformLayout(img, dstLayout)
                }
            else
                Command.nop

    // We hide these methods since they use ClearColor, ClearDepth and ClearStencil, which have
    // implicit conversion operators for better C# interop. They would take precedence over the SRTP variants defined below, resulting in warnings.
    // We also cannot move the implementation to the SRTP variants directly, since they make use of private functions.
    module ``Internal Clear Commands`` =

        type Command with

            static member ClearColorImpl(img : ImageSubresourceRange, color : ClearColor) =
                if img.Image.IsNull then
                    Command.Nop
                else
                    if img.Aspect <> TextureAspect.Color then
                        failf "cannot clear image with aspect %A using color" img.Aspect

                    { new Command() with
                        member x.Compatible = QueueFlags.Graphics ||| QueueFlags.Compute
                        member x.Enqueue cmd =
                            let originalLayout = img.Image.Layout

                            cmd.Enqueue (Command.TransformLayout(img.Image, VkImageLayout.TransferDstOptimal))

                            let clearValue =
                                if img.Image.Format |> VkFormat.toTextureFormat |> TextureFormat.isIntegerFormat then
                                    VkClearColorValue(int32 = color.Integer)
                                else
                                    VkClearColorValue(float32 = color.Float)

                            let range = img.VkImageSubresourceRange
                            clearValue |> pin (fun pClear ->
                                range |> pin (fun pRange ->
                                    cmd.AppendCommand()
                                    VkRaw.vkCmdClearColorImage(cmd.Handle, img.Image.Handle, VkImageLayout.TransferDstOptimal, pClear, 1u, pRange)
                                )
                            )
                            cmd.Enqueue (Command.TransformLayout(img.Image, originalLayout))

                            [img.Image]
                    }

            static member ClearDepthStencilImpl(img : ImageSubresourceRange, depth : ClearDepth, stencil : ClearStencil) =
                if img.Image.IsNull then
                    Command.Nop
                else
                    if not (img.Aspect.HasFlag(TextureAspect.Depth) || img.Aspect.HasFlag(TextureAspect.Stencil)) then
                        failf "cannot clear image with aspect %A using depth/stencil" img.Aspect

                    { new Command() with
                        member x.Compatible = QueueFlags.Graphics
                        member x.Enqueue cmd =
                            let originalLayout = img.Image.Layout
                            cmd.Enqueue (Command.TransformLayout(img.Image, VkImageLayout.TransferDstOptimal))

                            let mutable clearValue = VkClearDepthStencilValue(depth.Value, stencil.Value)
                            let mutable range = img.VkImageSubresourceRange
                            clearValue |> pin (fun pClear ->
                                range |> pin (fun pRange ->
                                    cmd.AppendCommand()
                                    VkRaw.vkCmdClearDepthStencilImage(cmd.Handle, img.Image.Handle, VkImageLayout.TransferDstOptimal, pClear, 1u, pRange)
                                )
                            )
                            cmd.Enqueue (Command.TransformLayout(img.Image, originalLayout))

                            [img.Image]
                    }

    open ``Internal Clear Commands``

    type Command with

        static member inline ClearColor(img : ImageSubresourceRange, color : ^Color) =
            Command.ClearColorImpl(img, ClearColor.create color)

        static member inline ClearDepthStencil(img : ImageSubresourceRange, depth : ^Depth, stencil : ^Stencil) =
            Command.ClearDepthStencilImpl(img, ClearDepth.create depth, ClearStencil.create stencil)


[<AutoOpen>]
module private ImageExtensions =

    type Image with
        member x.IsCubeOr2D =
            match x.Dimension with
            | TextureDimension.Texture2D | TextureDimension.TextureCube -> true
            | _ -> false

// ===========================================================================================
// Image functions
// ===========================================================================================
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Image =
    open System.Collections.Concurrent
    open KHRBindMemory2
    open Vulkan11

    [<Literal>]
    let internal defaultUsage =
        VkImageUsageFlags.TransferSrcBit |||
        VkImageUsageFlags.TransferDstBit |||
        VkImageUsageFlags.SampledBit |||
        VkImageUsageFlags.StorageBit

    let allocLinear (size : V2i) (fmt : VkFormat) (usage : VkImageUsageFlags) (device : Device) =
        let features = device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Linear, fmt)
        let usage = usage |> VkImageUsageFlags.filterSupported features

        let info =
            VkImageCreateInfo(
                VkImageCreateFlags.None,
                VkImageType.D2d,
                fmt,
                VkExtent3D(uint32 size.X, uint32 size.Y, 1u),
                1u,
                1u,
                VkSampleCountFlags.D1Bit,
                VkImageTiling.Linear,
                usage,
                VkSharingMode.Exclusive,
                0u, NativePtr.zero,
                VkImageLayout.Preinitialized
            )

        let mutable handle =
            temporary (fun pHandle ->
                info |> pin (fun pInfo ->
                    VkRaw.vkCreateImage(device.Handle, pInfo, NativePtr.zero, pHandle)
                        |> check "could not create image"
                    NativePtr.read pHandle
                )
            )

        let reqs =
            temporary (fun ptr ->
                VkRaw.vkGetImageMemoryRequirements(device.Handle, handle, ptr)
                NativePtr.read ptr
            )
        let memalign = int64 reqs.alignment |> Alignment.next device.BufferImageGranularity
        let memsize = int64 reqs.size |> Alignment.next device.BufferImageGranularity

        if device.HostMemory.Mask &&& reqs.memoryTypeBits = 0u then
            VkRaw.vkDestroyImage(device.Handle, handle, NativePtr.zero)
            failf "cannot create linear image in host-memory"

        let ptr = device.HostMemory.Alloc(memalign, memsize)

        VkRaw.vkBindImageMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
            |> check "could not bind image memory"

        new Image(device, handle, V3i(size, 1), 1, 1, 1, TextureDimension.Texture2D, fmt, ptr, VkImageLayout.Preinitialized)


    let rec alloc (size : V3i) (mipMapLevels : int) (count : int) (samples : int)
                  (dim : TextureDimension) (fmt : VkFormat) (usage : VkImageUsageFlags) 
                  (export : ImageExportMode) (device : Device) =

        let features = device.PhysicalDevice.GetFormatFeatures(VkImageTiling.Optimal, fmt)

        if features = VkFormatFeatureFlags.None then
            match fmt.NextBetter with
            | Some fmt -> alloc size mipMapLevels count samples dim fmt usage export device
            | None -> failf "bad image format %A" fmt
        else
            let mayHavePeers =
                device.IsDeviceGroup &&
                (
                    (usage &&& VkImageUsageFlags.ColorAttachmentBit <> VkImageUsageFlags.None) ||
                    (usage &&& VkImageUsageFlags.DepthStencilAttachmentBit <> VkImageUsageFlags.None) ||
                    (usage &&& VkImageUsageFlags.StorageBit <> VkImageUsageFlags.None)
                )

            let flags =
                if dim = TextureDimension.TextureCube then VkImageCreateFlags.CubeCompatibleBit
                else VkImageCreateFlags.None

            let flags =
                if mayHavePeers then VkImageCreateFlags.AliasBit ||| flags
                else flags

            let layers =
                match dim with
                | TextureDimension.TextureCube -> 6 * (count |> max 1)
                | _ -> count |> max 1

            let size =
                match dim with
                | TextureDimension.Texture1D -> V3i(size.X, 1, 1)
                | TextureDimension.Texture2D -> V3i(size.X, size.Y, 1)
                | TextureDimension.TextureCube -> V3i(size.X, size.X, 1)
                | _ -> size

            let typ =
                VkImageType.ofTextureDimension dim

            let usage =
                usage |> VkImageUsageFlags.filterSupported features

            let properties =
                device.PhysicalDevice.GetImageFormatProperties(fmt, typ, VkImageTiling.Optimal, usage, flags)

            let maxExtent = V3l.OfExtent properties.maxExtent

            if Vec.anyGreater (V3l size) maxExtent then
                failf "cannot create %A image with size %A (maximum is %A)" fmt size maxExtent

            if uint32 layers > properties.maxArrayLayers then
                failf "cannot create %A image with %d layers (maximum is %d)" fmt layers properties.maxArrayLayers

            if uint32 mipMapLevels > properties.maxMipLevels then
                failf "cannot create %A image with %d mip-map levels (maximum is %d)" fmt mipMapLevels properties.maxMipLevels

            let samples =
                let counts = VkSampleCountFlags.toSet properties.sampleCounts
                if counts.Contains samples then samples
                else
                    let max = Set.maxElement counts
                    Log.warn "[Vulkan] cannot create %A image with %d samples (using %d instead)" fmt samples max
                    max

            native {
                let! pNext = 
                    VkExternalMemoryImageCreateInfo(
                        if export <> ImageExportMode.None then 
                            VkExternalMemoryHandleTypeFlags.OpaqueFdBit ||| VkExternalMemoryHandleTypeFlags.OpaqueWin32Bit
                        else 
                            VkExternalMemoryHandleTypeFlags.None)

                let! pInfo = 
                    VkImageCreateInfo(
                        NativePtr.toNativeInt pNext,
                        flags,
                        typ,
                        fmt,
                        VkExtent3D(uint32 size.X, uint32 size.Y, uint32 size.Z),
                        uint32 mipMapLevels,
                        uint32 layers,
                        unbox<VkSampleCountFlags> samples,
                        VkImageTiling.Optimal,
                        usage,
                        VkSharingMode.Exclusive,
                        0u, NativePtr.zero,
                        VkImageLayout.Undefined
                    )

                let handle =
                    temporary (fun pHandle ->
                        VkRaw.vkCreateImage(device.Handle, pInfo, NativePtr.zero, pHandle)
                            |> check "could not create image"
                        NativePtr.read pHandle
                    )

                let reqs =
                    temporary (fun ptr ->
                        VkRaw.vkGetImageMemoryRequirements(device.Handle, handle,ptr)
                        NativePtr.read ptr
                    )

                let memalign = int64 reqs.alignment |> Alignment.next device.BufferImageGranularity
                let memsize = int64 reqs.size |> Alignment.next device.BufferImageGranularity
                let ptr = device.Alloc(VkMemoryRequirements(uint64 memsize, uint64 memalign, reqs.memoryTypeBits), true, export <> ImageExportMode.None)

                if mayHavePeers then
                    let indices = device.AllIndicesArr
                    let handles = Array.zeroCreate indices.Length
                    handles.[0] <- handle
                    for i in 1 .. indices.Length - 1 do
                        let handle =
                            temporary (fun pHandle ->    
                                VkRaw.vkCreateImage(device.Handle, pInfo, NativePtr.zero, pHandle)
                                    |> check "could not create image"
                                NativePtr.read pHandle
                            )
                        handles.[i] <- handle

                    for off in 0 .. indices.Length - 1 do
                        let deviceIndices =
                            Array.init indices.Length (fun i ->
                                indices.[(i+off) % indices.Length] |> uint32
                            )

                        native {
                            let! pDeviceIndices = deviceIndices
                            let groupInfo =
                                VkBindImageMemoryDeviceGroupInfo(
                                    uint32 deviceIndices.Length, pDeviceIndices,
                                    0u, NativePtr.zero
                                )
                            let! pGroup = groupInfo
                            let! pInfo =
                                VkBindImageMemoryInfo(
                                    NativePtr.toNativeInt pGroup,
                                    handles.[off],
                                    ptr.Memory.Handle,
                                    uint64 ptr.Offset
                                )
                            VkRaw.vkBindImageMemory2(device.Handle, 1u, pInfo)
                                |> check "could not bind image memory"
                        }


                    let peerHandles = Array.skip 1 handles
                    let result = new Image(device, handles.[0], size, mipMapLevels, layers, samples, dim, fmt, ptr, VkImageLayout.Undefined, peerHandles = peerHandles)

                    device.perform {
                        for i in 1 .. handles.Length - 1 do
                            let img = new Image(device, handles.[i], size, mipMapLevels, layers, samples, dim, fmt, ptr, VkImageLayout.Undefined)
                            do! Command.TransformLayout(img, VkImageLayout.TransferDstOptimal)
                    }

                    return result
                else
                    VkRaw.vkBindImageMemory(device.Handle, handle, ptr.Memory.Handle, uint64 ptr.Offset)
                        |> check "could not bind image memory"

                    match export with
                    | ImageExportMode.Export preferArray ->
                        return new ExportedImage(
                            device, handle, size, mipMapLevels, layers, samples, dim, fmt, preferArray, ptr, VkImageLayout.Undefined
                        )

                    | _ ->
                        return new Image(device, handle, size, mipMapLevels, layers, samples, dim, fmt, ptr, VkImageLayout.Undefined)
                }

    let create (size : V3i) (mipMapLevels : int) (count : int) (samples : int) (dim : TextureDimension) (fmt : VkFormat) (usage : VkImageUsageFlags)
               (export : ImageExportMode) (device : Device) =
        alloc size mipMapLevels count samples dim fmt usage export device

    /// Returns an uninitialized image with size 8 in each dimension, used as NullTexture counter-part
    let internal empty =
        let store = ConcurrentDictionary<ImageProperties, Image>()

        fun (properties : ImageProperties) (device : Device) ->
            let image =
                store.GetOrAdd(properties, fun _ ->
                    let size = V3i 8
                    let format = VkFormat.ofTextureFormat properties.Format
                    let samples = if properties.IsMultisampled then 2 else 1
                    let image = device |> create size 1 1 samples properties.Dimension format defaultUsage ImageExportMode.None

                    device.perform {
                        do! Command.TransformLayout(image, VkImageLayout.ShaderReadOnlyOptimal)
                    }

                    device.OnDispose.Add(fun _ ->
                        store.TryRemove properties |> ignore
                        image.Dispose()
                    )

                    image
                )

            image.AddReference()
            image


[<AbstractClass; Sealed; Extension>]
type ContextImageExtensions private() =

    [<Extension>]
    static member inline CreateImage(this : Device,
                                     size : V3i, mipMapLevels : int, count : int, samples : int, dim : TextureDimension, fmt : VkFormat, usage : VkImageUsageFlags,
                                     export : ImageExportMode) =
        this |> Image.create size mipMapLevels count samples dim fmt usage export

    [<Extension>]
    static member inline CreateImage(this : Device,
                                     size : V3i, mipMapLevels : int, count : int, samples : int, dim : TextureDimension, fmt : VkFormat, usage : VkImageUsageFlags,
                                     [<Optional; DefaultParameterValue(false)>] export : bool) =
        let export = if export then ImageExportMode.Export false else ImageExportMode.None
        this.CreateImage(size, mipMapLevels, count, samples, dim, fmt, usage, export)

    [<Extension>]
    static member inline CreateImage(this : Device,
                                     size : V3i, mipMapLevels : int, count : int, samples : int, dim : TextureDimension, fmt : TextureFormat, usage : VkImageUsageFlags,
                                     export : ImageExportMode) =
        let fmt = VkFormat.ofTextureFormat fmt
        this.CreateImage(size, mipMapLevels, count, samples, dim, fmt, usage, export)

    [<Extension>]
    static member inline CreateImage(this : Device,
                                     size : V3i, mipMapLevels : int, count : int, samples : int, dim : TextureDimension, fmt : TextureFormat, usage : VkImageUsageFlags,
                                     [<Optional; DefaultParameterValue(false)>] export : bool) =
        let fmt = VkFormat.ofTextureFormat fmt
        this.CreateImage(size, mipMapLevels, count, samples, dim, fmt, usage, export)


[<AutoOpen>]
module private ImageRanges =

    module ImageSubresource =
        let ofTextureSubResource (src : ITextureSubResource) =
            let srcImage = src.Texture |> unbox<Image>
            srcImage.[src.Aspect, src.Level, src.Slice]

    module ImageSubresourceLayers =
        let ofFramebufferOutput (src : IFramebufferOutput) =
            match src with
            | :? Image as img ->
                if VkFormat.hasDepth img.Format then
                    img.[TextureAspect.Depth, 0, *]
                else
                    img.[TextureAspect.Color, 0, *]

            | :? ITextureLevel as src ->
                let srcImage = src.Texture |> unbox<Image>
                srcImage.[src.Aspect, src.Level, src.Slices.Min .. src.Slices.Max]

            | _ ->
                failf "unexpected IFramebufferOutput: %A" src