namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.ReflectionHelpers
open System.Runtime.InteropServices

#nowarn "9"

[<AbstractClass>]
type TensorImage(buffer : Buffer, info : Tensor4Info, format : PixFormat, imageFormat : VkFormat) =
    inherit ImageBuffer(buffer, V3i info.Size.XYZ, V2i info.Size.XY, VkFormat.toTextureFormat imageFormat)

    member x.Buffer = buffer
    member x.Channels = int info.Size.W
    member x.Size = V3i info.Size.XYZ
    member x.PixFormat = format
    member x.Format = format.Format
    member x.ImageFormat = imageFormat

    abstract member Write<'x when 'x : unmanaged> : NativeMatrix<'x> -> unit
    abstract member Write<'x when 'x : unmanaged> : Col.Format * NativeVolume<'x> -> unit
    abstract member Write<'x when 'x : unmanaged> : Col.Format * NativeTensor4<'x> -> unit

    abstract member Read<'x when 'x : unmanaged> : NativeMatrix<'x> -> unit
    abstract member Read<'x when 'x : unmanaged> : Col.Format * NativeVolume<'x> -> unit
    abstract member Read<'x when 'x : unmanaged> : Col.Format * NativeTensor4<'x> -> unit

    abstract member Write : data : nativeint * rowSize : nativeint * format : Col.Format * trafo : ImageTrafo -> unit
    abstract member Read : data : nativeint * rowSize : nativeint * format : Col.Format * trafo : ImageTrafo -> unit

    member x.Write(img : PixImage, mirrorY : bool) =
        img.Visit {
            new PixVisitors.PixImageVisitor() with
                override __.VisitUnit(img : PixImage<'T>) =
                    NativeVolume.using img.Volume (fun src ->
                        x.Write(img.Format, if mirrorY then src.MirrorY() else src)
                    )
        } |> ignore

    member x.Read(img : PixImage, mirrorY : bool) =
        img.Visit {
            new PixVisitors.PixImageVisitor() with
                override __.VisitUnit(img : PixImage<'a>) =
                    NativeVolume.using img.Volume (fun dst ->
                        x.Read(img.Format, if mirrorY then dst.MirrorY() else dst)
                    )
        } |> ignore

    member x.Write(img : PixVolume, mirrorY : bool) =
        img.Visit {
            new PixVisitors.PixVolumeVisitor() with
                override __.VisitUnit(img : PixVolume<'a>) =
                    NativeTensor4.using img.Tensor4 (fun src ->
                        x.Write(img.Format, if mirrorY then src.MirrorY() else src)
                    )
        } |> ignore

    member x.Read(img : PixVolume, mirrorY : bool) =
        img.Visit {
            new PixVisitors.PixVolumeVisitor() with
                override __.VisitUnit(img : PixVolume<'a>) =
                    NativeTensor4.using img.Tensor4 (fun dst ->
                        x.Read(img.Format, if mirrorY then dst.MirrorY() else dst)
                    )
        } |> ignore

type TensorImage<'a when 'a : unmanaged> private(buffer : Buffer, info : Tensor4Info, format : Col.Format, imageFormat : VkFormat) =
    inherit TensorImage(buffer, info, PixFormat(typeof<'a>, format), imageFormat)

    static let sa = sizeof<'a> |> int64

    static let rgbFormats =
        HashSet.ofList [
            Col.Format.RGB
            Col.Format.RGBA
            Col.Format.RGBP
        ]

    static let bgrFormats =
        HashSet.ofList [
            Col.Format.BGR
            Col.Format.BGRA
            Col.Format.BGRP
        ]

    static let reverseRGB (srcFormat : Col.Format) (dstFormat : Col.Format) =
        (rgbFormats.Contains srcFormat && bgrFormats.Contains dstFormat) ||
        (rgbFormats.Contains dstFormat && bgrFormats.Contains srcFormat)


    let tensor = DeviceTensor4<'a>(buffer.Memory, info)

    static let defaultValue =
        match typeof<'a> with
        | TypeMeta.Patterns.UInt8   -> 255uy |> unbox<'a>
        | TypeMeta.Patterns.Int8    -> 0y |> unbox<'a>
        | TypeMeta.Patterns.UInt16  -> UInt16.MaxValue |> unbox<'a>
        | TypeMeta.Patterns.Int16   -> 0s |> unbox<'a>
        | TypeMeta.Patterns.UInt32  -> UInt32.MaxValue |> unbox<'a>
        | TypeMeta.Patterns.Int32   -> 0 |> unbox<'a>
        | TypeMeta.Patterns.UInt64  -> UInt64.MaxValue |> unbox<'a>
        | TypeMeta.Patterns.Int64   -> 0L |> unbox<'a>
        | TypeMeta.Patterns.Float32 -> 1.0f |> unbox<'a>
        | TypeMeta.Patterns.Float64 -> 1.0 |> unbox<'a>
        | _ -> failf "unsupported channel-type: %A" typeof<'a>

    static let copy (src : NativeTensor4<'a>) (srcFormat : Col.Format) (dst : NativeTensor4<'a>) (dstFormat : Col.Format) =
        let channels = min src.SW dst.SW

        let mutable src = src
        let mutable dst = dst

        if src.Size.XYZ <> dst.Size.XYZ then
            let s = V3l(min src.SX dst.SX, min src.SY dst.SY, min src.SZ dst.SZ)
            src <- src.SubTensor4(V4l.Zero, V4l(s, src.SW))
            dst <- dst.SubTensor4(V4l.Zero, V4l(s, dst.SW))

        if reverseRGB srcFormat dstFormat then
            let src3 = src.[*,*,*,0..2].MirrorW()
            let dst3 = dst.[*,*,*,0..2]
            NativeTensor4.copy src3 dst3

            if channels > 3L then
                 NativeTensor4.copy src.[*,*,*,3..] dst.[*,*,*,3..]

            if dst.SW > channels then
                NativeTensor4.set defaultValue dst.[*,*,*,channels..]
        else
            // copy all available channels
            NativeTensor4.copy src dst.[*,*,*,0L..channels-1L]

            // set the missing channels to default
            if dst.SW > channels then
                NativeTensor4.set defaultValue dst.[*,*,*,channels..]

    override x.Write(data : nativeint, rowSize : nativeint, format : Col.Format, trafo : ImageTrafo) =
        let rowSize = int64 rowSize
        let channels = format.ChannelCount()

        if rowSize % sa <> 0L then failf "non-aligned row-size"
        let dy = rowSize / sa

        let srcInfo =
            VolumeInfo(
                0L,
                V3l(info.SX, info.SY, int64 channels),
                V3l(int64 channels, dy, 1L)
            )

        let srcInfo =
            srcInfo.Transformed(trafo)

        let src = NativeVolume<'a>(NativePtr.ofNativeInt data, srcInfo)
        x.Write(format, src)

    override x.Read(data : nativeint, rowSize : nativeint, format : Col.Format, trafo : ImageTrafo) =
        let rowSize = int64 rowSize
        let channels = format.ChannelCount()

        if rowSize % sa <> 0L then failf "non-aligned row-size"
        let dy = rowSize / sa

        let dstInfo =
            VolumeInfo(
                0L,
                V3l(info.SY, info.SY, int64 channels),
                V3l(int64 channels, dy, 1L)
            )

        let dstInfo =
            dstInfo.Transformed(trafo)

        let dst = NativeVolume<'a>(NativePtr.ofNativeInt data, dstInfo)
        x.Read(format, dst)

    override x.Write(matrix : NativeMatrix<'x>) : unit =
        if typeof<'a> = typeof<'x> then
            let src = unbox<NativeMatrix<'a>> matrix
            let dst = tensor.[*,*,*,0].[*,*,0]
            dst.CopyFrom src

            let rest = tensor.[*,*,1..,*]
            rest.Set defaultValue

            let rest = tensor.[*,*,0,1..]
            rest.Set defaultValue
        else
            failf "mismatching types in upload"

    override x.Write(fmt : Col.Format, volume : NativeVolume<'x>) =
        if typeof<'a> = typeof<'x> then
            let src = unbox<NativeVolume<'a>> volume

            let srcTensor = src.ToXYWTensor4'()
            tensor.Mapped (fun dst ->
                copy srcTensor fmt dst format
            )
        else
            failf "mismatching types in upload"

    override x.Write(fmt : Col.Format, t : NativeTensor4<'x>) =
        if typeof<'a> = typeof<'x> then
            let src = unbox<NativeTensor4<'a>> t
            tensor.Mapped (fun dst ->
                copy src fmt dst format
            )

        else
            failf "mismatching types in upload"

    override x.Read(dst : NativeMatrix<'x>) : unit =
        if typeof<'a> = typeof<'x> then
            let dst = unbox<NativeMatrix<'a>> dst
            let src = tensor.[*,*,*,0].[*,*,0]
            src.CopyTo dst
        else
            failf "mismatching types in download"

    override x.Read(fmt : Col.Format, dst : NativeVolume<'x>) : unit =
        if typeof<'a> = typeof<'x> then
            let dst = unbox<NativeVolume<'a>> dst
            let dstTensor = dst.ToXYWTensor4'()
            tensor.Mapped (fun src ->
                copy src format dstTensor fmt
            )

        else
            failf "mismatching types in download"

    override x.Read(fmt : Col.Format, dst : NativeTensor4<'x>) : unit =
        if typeof<'a> = typeof<'x> then
            let dst = unbox<NativeTensor4<'a>> dst
            tensor.Mapped (fun src ->
                copy src format dst fmt
            )
        else
            failf "mismatching types in download"

    member x.Buffer = buffer
    member x.Size = V3i info.Size.XYZ
    member x.Format = format
    member x.Channels = int info.SW

    member x.Tensor4 =
        tensor

    member x.Volume =
        if info.SZ <> 1L then failf "3d image cannot be interpreted as 2d image"
        else tensor.[*,*,0,*]

    member x.Matrix =
        if info.SZ <> 1L then failf "3d image cannot be interpreted as 2d matrix"
        elif info.SW <> 1L then failf "2d image with more than one channel cannot be interpreted as 2d matrix"
        else tensor.[*,*,*,0].[*,*,0]

    member x.Vector =
        if info.SZ <> 1L then failf "3d image cannot be interpreted as vector"
        elif info.SY <> 1L then failf "2d image cannot be interpreted as vector"
        elif info.SW <> 1L then failf "1d image with more than one channel cannot be interpreted as vector"
        else tensor.[*,*,*,0].[*,0,0]

    internal new(buffer : Buffer, size : V3i, format : Col.Format, imageFormat : VkFormat) =
        let channels = format.ChannelCount()
        let s = V4l(size.X, size.Y, size.Z, channels)
        let info =
            Tensor4Info(
                0L,
                s,
                V4l(s.W, s.W * s.X, s.W * s.X * s.Y, 1L)
            )
        new TensorImage<'a>(buffer, info, format, imageFormat)

type TensorImageMipMap(images : TensorImage[]) =
    member x.LevelCount = images.Length
    member x.ImageArray = images
    member x.Format = images.[0].PixFormat

    member x.Dispose() =
        images |> Array.iter Disposable.dispose

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type TensorImageCube(faces : TensorImageMipMap[]) =
    do assert(faces.Length = 6)
    member x.MipMapArray = faces

    member x.Dispose() =
        faces |> Array.iter Disposable.dispose

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TensorImage =
    let create<'a when 'a : unmanaged> (size : V3i) (format : Col.Format) (srgb : bool) (memory : IDeviceMemory) : TensorImage<'a> =
        let imageFormat = memory.Device.GetSupportedFormat(VkImageTiling.Optimal, PixFormat(typeof<'a>, format), { TextureParams.empty with wantSrgb = srgb })
        let format = PixFormat(VkFormat.expectedType imageFormat, VkFormat.toColFormat imageFormat)

        if format.Type <> typeof<'a> then
            failf "device does not support images of type %s" typeof<'a>.PrettyName

        let channels = format.ChannelCount
        let sizeInBytes = uint64 size.X * uint64 size.Y * uint64 size.Z * uint64 channels * uint64 sizeof<'a>
        let buffer = memory |> Buffer.create (VkBufferUsageFlags.TransferDstBit ||| VkBufferUsageFlags.TransferSrcBit) sizeInBytes
        new TensorImage<'a>(buffer, size, format.Format, imageFormat)

    let inline private erase (creator : V3i -> Col.Format -> bool -> IDeviceMemory -> TensorImage<'a>) (size : V3i) (format : Col.Format) (tp : bool) (memory : IDeviceMemory) = creator size format tp memory :> TensorImage

    let private creators =
        Dictionary.ofList [
            typeof<uint8>, erase create<uint8>
            typeof<int8>, erase create<int8>
            typeof<uint16>, erase create<uint16>
            typeof<int16>, erase create<int16>
            typeof<uint32>, erase create<uint32>
            typeof<int32>, erase create<int32>
            typeof<uint64>, erase create<uint64>
            typeof<int64>, erase create<int64>
            typeof<float16>, erase create<float16>
            typeof<float32>, erase create<float32>
            typeof<float>, erase create<float>
            // TODO: any others?
        ]

    let createUntyped (size : V3i) (format : PixFormat) (srgb : bool) (memory : IDeviceMemory) =
        creators.[format.Type] size format.Format srgb memory

    let ofPixImage (img : PixImage) (srgb : bool) (memory : IDeviceMemory) =
        let dst = createUntyped (V3i(img.Size.X, img.Size.Y, 1)) img.PixFormat srgb memory
        dst.Write(img, true)
        dst

    let ofPixVolume (img : PixVolume) (srgb : bool) (memory : IDeviceMemory) =
        let dst = createUntyped (V3i(img.Size.X, img.Size.Y, 1)) img.PixFormat srgb memory
        dst.Write(img, false)
        dst


[<AbstractClass; Sealed; Extension>]
type DeviceTensorExtensions private() =

    [<Extension>]
    static member inline CreateTensorImage<'a when 'a : unmanaged>(memory : IDeviceMemory, size : V3i, format : Col.Format, srgb : bool) : TensorImage<'a> =
        TensorImage.create size format srgb memory

    [<Extension>]
    static member inline CreateTensorImage(memory : IDeviceMemory, size : V3i, format : PixFormat, srgb : bool) : TensorImage =
        TensorImage.createUntyped size format srgb memory

    [<Extension>]
    static member inline CreateTensorImage2D(memory : IDeviceMemory, data : PixImage, srgb : bool) : TensorImage =
        memory |> TensorImage.ofPixImage data srgb

    [<Extension>]
    static member inline CreateTensorImage2D(memory : IDeviceMemory, data : PixImageMipMap, levels : int, srgb : bool) : TensorImageMipMap =
        new TensorImageMipMap(
            data.ImageArray |> Array.take levels |> Array.map (fun l -> TensorImage.ofPixImage l srgb memory)
        )

    [<Extension>]
    static member inline CreateTensorImage2D(memory : IDeviceMemory, data : PixImageMipMap, srgb : bool) =
        memory.CreateTensorImage2D(data, data.LevelCount, srgb)

    [<Extension>]
    static member inline CreateTensorImageCube(memory : IDeviceMemory, data : PixCube, levels : int, srgb : bool) : TensorImageCube =
        new TensorImageCube(
            data.MipMapArray |> Array.map (fun face ->
                memory.CreateTensorImage2D(face, levels, srgb)
            )
        )

    [<Extension>]
    static member inline CreateTensorImageCube(memory : IDeviceMemory, data : PixCube, srgb : bool) =
        memory.CreateTensorImageCube(data, data.MipMapArray.[0].LevelCount, srgb)

    [<Extension>]
    static member inline CreateTensorImageVolume(memory : IDeviceMemory, data : PixVolume, srgb : bool) : TensorImage =
        memory |> TensorImage.ofPixVolume data srgb


[<AutoOpen>]
module TensorImageCommandExtensions =

    type Command with

        // upload
        static member Copy(src : TensorImage, dst : ImageSubresource, dstOffset : V3i, size : V3i) =
            if dst.Aspect <> TextureAspect.Color then
                failf "[TensorImage] cannot copy to aspect %A" dst.Aspect

            if dstOffset.AnySmaller 0 || dst.Size.AnySmaller(dstOffset + size) then
                failf "[TensorImage] target region out of bounds"

            if src.Size.AnySmaller size then
                failf "[TensorImage] insufficient size %A" src.Size

            let dstElementType = VkFormat.expectedType dst.Image.Format
            let dstSizeInBytes = VkFormat.sizeInBytes dst.Image.Format

            if isNull dstElementType || dstSizeInBytes < 0 then
                failf "[TensorImage] format %A has no CPU representation" dst.Image.Format

            let dstChannels = dstSizeInBytes / Marshal.SizeOf dstElementType
            if dstChannels <> src.Channels then
                failf "[TensorImage] got '%d * %s' but expected '%d * %s'" src.Channels src.PixFormat.Type.PrettyName dstChannels dstElementType.PrettyName

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let copy =
                        VkBufferImageCopy(
                            0UL,
                            0u,
                            0u,
                            dst.VkImageSubresourceLayers,
                            VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    copy |> NativePtr.pin (fun pCopy ->
                        VkRaw.vkCmdCopyBufferToImage(cmd.Handle, src.Buffer.Handle, dst.Image.Handle, dst.Image.Layout, 1u, pCopy)
                    )

                    cmd.AddResource src.Buffer
                    cmd.AddResource dst.Image
            }

        static member Copy(src : TensorImage, dst : ImageSubresource) =
            if src.Size <> dst.Size then failf "[TensorImage] mismatching sizes in copy %A vs %A" src.Size dst.Size
            Command.Copy(src, dst, V3i.Zero, src.Size)

        // download
        static member Copy(src : ImageSubresource, srcOffset : V3i, dst : TensorImage, size : V3i) =
            if src.Aspect <> TextureAspect.Color then
                failf "[TensorImage] cannot copy from aspect %A" src.Aspect

            if srcOffset.AnySmaller 0 || src.Size.AnySmaller(srcOffset + size) then
                failf "[TensorImage] source region out of bounds"

            if dst.Size.AnySmaller size then
                failf "[TensorImage] insufficient size %A" src.Size

            let srcElementType = VkFormat.expectedType src.Image.Format
            let srcSizeInBytes = VkFormat.sizeInBytes src.Image.Format

            if isNull srcElementType || srcSizeInBytes < 0 then
                failf "[TensorImage] format %A has no CPU representation" src.Image.Format

            let srcChannels = srcSizeInBytes / Marshal.SizeOf srcElementType
            if srcChannels <> dst.Channels then
                failf "[TensorImage] got '%d * %s' but expected '%d * %s'" srcChannels srcElementType.PrettyName dst.Channels dst.PixFormat.Type.PrettyName

            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    let copy =
                        VkBufferImageCopy(
                            0UL,
                            0u,
                            0u,
                            src.VkImageSubresourceLayers,
                            VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                            VkExtent3D(size.X, size.Y, size.Z)
                        )

                    cmd.AppendCommand()
                    copy |> NativePtr.pin (fun pCopy ->
                        VkRaw.vkCmdCopyImageToBuffer(cmd.Handle, src.Image.Handle, src.Image.Layout, dst.Buffer.Handle, 1u, pCopy)
                    )

                    cmd.AddResource src.Image
                    cmd.AddResource dst.Buffer
            }

        static member Copy(src : ImageSubresource, dst : TensorImage) =
            if src.Size <> dst.Size then failf "[TensorImage] mismatching sizes in copy %A vs %A" src.Size dst.Size
            Command.Copy(src, V3i.Zero, dst, src.Size)


    type CopyCommand with

        // upload
        static member Copy(src : TensorImage, dst : ImageSubresource, dstOffset : V3i, size : V3i) =
            CopyCommand.Copy(
                src.Buffer.Handle,
                dst.Image.Handle,
                dst.Image.Layout,
                dst.Image.Format,
                VkBufferImageCopy(
                    0UL, 0u, 0u,
                    dst.VkImageSubresourceLayers,
                    VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                    VkExtent3D(size.X, size.Y, size.Z)
                )
            )

        static member Copy(src : TensorImage, dst : ImageSubresource) =
            CopyCommand.Copy(src, dst, V3i.Zero, src.Size)


        // download
        static member Copy(src : ImageSubresource, srcOffset : V3i, dst : TensorImage, size : V3i) =
            CopyCommand.Copy(
                src.Image.Handle,
                src.Image.Layout,
                dst.Buffer.Handle,
                src.Image.Format,
                VkBufferImageCopy(
                    0UL, 0u, 0u,
                    src.VkImageSubresourceLayers,
                    VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                    VkExtent3D(size.X, size.Y, size.Z)
                )
            )

        static member Copy(src : ImageSubresource, dst : TensorImage) =
            CopyCommand.Copy(src, V3i.Zero, dst, src.Size)


module private ``Tensor Image must compile`` =

       let createImage (memory : IDeviceMemory) =
           use img = memory.CreateTensorImage<byte>(V3i.III, Col.Format.RGBA, false)

           let v = img.Vector
           let m = img.Matrix
           let v = img.Volume
           let t = img.Tensor4

           ()

       let testVec (v : DeviceVector<int>) =
           let a = v.[1L..]
           let b = v.[..10L]

           let x = v.[1..]
           let y = v.[..10]

           ()

       let testMat (m : DeviceMatrix<int>) =
           let a = m.[1L,*]
           let b = m.[*,2L]
           let c = m.[1L.., 1L..]

           let x = m.[1,*]
           let y = m.[*,2]
           let z = m.[1.., 1..]

           ()

       let testVol (v : DeviceVolume<int>) =
           let a = v.[1L,*,*]
           let b = v.[*,2L,*]
           let c = v.[*,*,3L]

           let a = v.[1L,1L,*]
           let b = v.[1L,*,1L]
           let c = v.[*,1L,1L]

           let a = v.[1L..,1L..,1L..]



           let a = v.[1,*,*]
           let b = v.[*,2,*]
           let c = v.[*,*,3]

           let a = v.[1,1,*]
           let b = v.[1,*,1]
           let c = v.[*,1,1]

           let a = v.[1..,1..,1..]

           ()


       let testTensor4 (t : DeviceTensor4<int>) =
           let a = t.[1L,*,*,*]
           let b = t.[*,2L,*,*]
           let c = t.[*,*,3L,*]
           let d = t.[*,*,*,4L]


           // t.[1L,*,*,4L]
           // t.[1L,2L,*,4L]

           let a = t.[1L..,1L..,1L..,1L..]

           let a = t.[1,*,*,*]
           let b = t.[*,2,*,*]
           let c = t.[*,*,3,*]
           let d = t.[*,*,*,4]

           let a = t.[1..,1..,1..,1..]


           ()