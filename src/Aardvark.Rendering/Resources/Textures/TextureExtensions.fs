namespace Aardvark.Rendering

open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base

module PixVisitors =

    [<AbstractClass>]
    type PixImageVisitor<'T>() =
        static let table =
            LookupTable.lookupTable [
                typeof<int8>,    (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<int8>(unbox img))
                typeof<uint8>,   (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<uint8>(unbox img))
                typeof<int16>,   (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<int16>(unbox img))
                typeof<uint16>,  (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<uint16>(unbox img))
                typeof<int32>,   (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<int32>(unbox img))
                typeof<uint32>,  (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<uint32>(unbox img))
                typeof<int64>,   (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<int64>(unbox img))
                typeof<uint64>,  (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<uint64>(unbox img))
                typeof<float16>, (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<float16>(unbox img))
                typeof<float32>, (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<float32>(unbox img))
                typeof<float>,   (fun (self : PixImageVisitor<'T>, img : PixImage) -> self.Visit<float>(unbox img))
            ]

        abstract member Visit<'TData when 'TData : unmanaged> : PixImage<'TData> -> 'T

        interface IPixImageVisitor<'T> with
            member x.Visit<'TData>(img : PixImage<'TData>) =
                table (typeof<'TData>) (x, img)

    [<AbstractClass>]
    type PixImageVisitor() =
        inherit PixImageVisitor<int>()

        abstract member VisitUnit<'TData when 'TData : unmanaged> : PixImage<'TData> -> unit

        override x.Visit(pi : PixImage<'TData>) = x.VisitUnit(pi); 0

    [<AbstractClass>]
    type PixVolumeVisitor<'T>() =
        static let table =
            LookupTable.lookupTable [
                typeof<int8>,    (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<int8>(unbox img))
                typeof<uint8>,   (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<uint8>(unbox img))
                typeof<int16>,   (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<int16>(unbox img))
                typeof<uint16>,  (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<uint16>(unbox img))
                typeof<int32>,   (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<int32>(unbox img))
                typeof<uint32>,  (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<uint32>(unbox img))
                typeof<int64>,   (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<int64>(unbox img))
                typeof<uint64>,  (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<uint64>(unbox img))
                typeof<float16>, (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<float16>(unbox img))
                typeof<float32>, (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<float32>(unbox img))
                typeof<float>,   (fun (self : PixVolumeVisitor<'T>, img : PixVolume) -> self.Visit<float>(unbox img))
            ]

        abstract member Visit<'TData when 'TData : unmanaged> : PixVolume<'TData> -> 'T

        interface IPixVolumeVisitor<'T> with
            member x.Visit<'TData>(img : PixVolume<'TData>) =
                table (typeof<'TData>) (x, img)

    [<AbstractClass>]
    type PixVolumeVisitor() =
        inherit PixVolumeVisitor<int>()

        abstract member VisitUnit<'TData when 'TData : unmanaged> : PixVolume<'TData> -> unit

        override x.Visit(pv : PixVolume<'TData>) = x.VisitUnit(pv); 0

[<AutoOpen>]
module PixExtensions =
    type PixImage with
        member x.VisitUnit<'T>(visitor : IPixImageVisitor<'T>) =
            x.Visit(visitor) |> ignore

[<AutoOpen>]
module TensorExtensions =

    type VolumeInfo with
        member x.ToXYWTensor4'() =
            Tensor4Info(
                x.Origin,
                V4l(x.SX, x.SY, 1L, x.SZ),
                V4l(x.DX, x.DY, x.DY * x.SY, x.DZ)
            )

    type NativeVolume<'T when 'T : unmanaged> with
        member x.ToXYWTensor4'() =
            NativeTensor4<'T>(
                x.Pointer,
                x.Info.ToXYWTensor4'()
            )

    type NativeTensor4<'T when 'T : unmanaged> with
        member x.Format =
            match x.Size.W with
            | 1L -> Col.Format.Gray
            | 2L -> Col.Format.GrayAlpha
            | 3L -> Col.Format.RGB
            | _  -> Col.Format.RGBA

[<AutoOpen>]
module ITextureRuntimeFSharpExtensions =

    // These extensions use SRTPs so MUST NOT be exposed to C#
    type ITextureRuntime with

        // ================================================================================================================
        // Clear
        // ================================================================================================================

        /// Clears the texture with the given color.
        member inline x.Clear(texture : IBackendTexture, color : ^Color) =
            let values = color |> (fun c -> clear { color c })
            x.Clear(texture, values)

        /// Clears the texture with the given depth value.
        member inline x.ClearDepth(texture : IBackendTexture, depth : ^Depth) =
            let values = depth |> (fun d -> clear { depth d })
            x.Clear(texture, values)

        /// Clears the texture with the given stencil value.
        member inline x.ClearStencil(texture : IBackendTexture, stencil : ^Stencil) =
            let values = stencil |> (fun s -> clear { stencil s })
            x.Clear(texture, values)

        /// Clears the texture with the given depth and stencil values.
        member inline x.ClearDepthStencil(texture : IBackendTexture, depth : ^Depth, stencil : ^Stencil) =
            let values = (depth, stencil) ||> (fun d s -> clear { depth d; stencil s })
            x.Clear(texture, values)


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeExtensions private() =

    static let levelRegion (texture : IBackendTexture) (level : int) (region : Box2i) =
        if region.IsEmpty || region.IsInfinite then
            Box2i.FromMinAndSize(V2i.Zero, texture.GetSize(level).XY)
        else
            region

    // ================================================================================================================
    // Create textures
    // ================================================================================================================

    ///<summary>Creates a 1D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    [<Extension>]
    static member CreateTexture1D(this : ITextureRuntime, size : int, format : TextureFormat,
                                  [<Optional; DefaultParameterValue(1)>] levels : int) =
        this.CreateTexture(V3i(size, 1, 1), TextureDimension.Texture1D, format, levels = levels, samples = 1)

    ///<summary>Creates a 1D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture1DArray(this : ITextureRuntime, size : int, format : TextureFormat,
                                       [<Optional; DefaultParameterValue(1)>] levels : int,
                                       count : int) =
        this.CreateTextureArray(V3i(size, 1, 1), TextureDimension.Texture1D, format, levels = levels, samples = 1, count = count)

    ///<summary>Creates a 2D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="samples">the number of samples. Default is 1.</param>
    [<Extension>]
    static member CreateTexture2D(this : ITextureRuntime, size : V2i, format : TextureFormat,
                                  [<Optional; DefaultParameterValue(1)>] levels : int,
                                  [<Optional; DefaultParameterValue(1)>] samples : int) =
        this.CreateTexture(V3i(size, 1), TextureDimension.Texture2D, format, levels = levels, samples = samples)

    ///<summary>Creates a 2D texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="samples">the number of samples. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTexture2DArray(this : ITextureRuntime, size : V2i, format : TextureFormat,
                                       [<Optional; DefaultParameterValue(1)>] levels : int,
                                       [<Optional; DefaultParameterValue(1)>] samples : int,
                                       count : int) =
        this.CreateTextureArray(V3i(size, 1), TextureDimension.Texture2D, format, levels = levels, samples = samples, count = count)

    ///<summary>Creates a 3D texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    [<Extension>]
    static member CreateTexture3D(this : ITextureRuntime, size : V3i, format : TextureFormat,
                                  [<Optional; DefaultParameterValue(1)>] levels : int) =
        this.CreateTexture(size, TextureDimension.Texture3D, format, levels = levels, samples = 1)

    ///<summary>Creates a cube texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    [<Extension>]
    static member CreateTextureCube(this : ITextureRuntime, size : int, format : TextureFormat,
                                    [<Optional; DefaultParameterValue(1)>] levels : int) =
        this.CreateTexture(V3i(size, size, 1), TextureDimension.TextureCube, format, levels = levels, samples = 1)

    ///<summary>Creates a cube texture array.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="size">The size of the texture.</param>
    ///<param name="format">The desired texture format.</param>
    ///<param name="levels">The number of mip levels. Default is 1.</param>
    ///<param name="count">The number of texture slices.</param>
    [<Extension>]
    static member CreateTextureCubeArray(this : ITextureRuntime, size : int, format : TextureFormat,
                                       [<Optional; DefaultParameterValue(1)>] levels : int,
                                       count : int) =
        this.CreateTextureArray(V3i(size, size, 1), TextureDimension.TextureCube, format, levels = levels, samples = 1, count = count)


    // ================================================================================================================
    // Upload 2D
    // ================================================================================================================

    ///<summary>Uploads data from a PixImage to the given texture sub resource.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V2i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V2i.Zero for the source size. Default is V2i.Zero.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : ITextureSubResource, source : PixImage,
                         [<Optional; DefaultParameterValue(V2i())>] offset : V2i,
                         [<Optional; DefaultParameterValue(V2i())>] size : V2i) =
        let size =
            if size = V2i.Zero then
                source.Size
            else
                size

        source.Visit
            { new PixVisitors.PixImageVisitor() with
                member x.VisitUnit(img : PixImage<'T>) =
                    NativeVolume.using img.Volume (fun src ->
                        this.Upload(texture, src.ToXYWTensor4'(), img.Format, offset.XYO, size.XYI)
                    )
            } |> ignore


    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="level">The texture level to update. Default is 0.</param>
    ///<param name="slice">The texture slice to update. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V2i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V2i.Zero for the source size. Default is V2i.Zero.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : IBackendTexture, source : PixImage,
                         [<Optional; DefaultParameterValue(0)>] level : int,
                         [<Optional; DefaultParameterValue(0)>] slice : int,
                         [<Optional; DefaultParameterValue(V2i())>] offset : V2i,
                         [<Optional; DefaultParameterValue(V2i())>] size : V2i) =
        this.Upload(texture.[TextureAspect.Color, level, slice], source, offset, size)

    // ================================================================================================================
    // Upload 3D
    // ================================================================================================================

    ///<summary>Uploads data from a PixVolume to the given texture sub resource.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixVolume containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V3i.Zero for the source size. Default is V3i.Zero.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : ITextureSubResource, source : PixVolume,
                         [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                         [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        let size =
            if size = V3i.Zero then
                source.Size
            else
                size

        source.Visit
            { new PixVisitors.PixVolumeVisitor() with
                member x.VisitUnit(img : PixVolume<'T>) =
                    NativeTensor4.using img.Tensor4 (fun src ->
                        this.Upload(texture, src, img.Format, offset, size)
                    )
            } |> ignore

    ///<summary>Uploads data from a PixVolume to the given texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixVolume containing the data to upload.</param>
    ///<param name="level">The texture level to update. Default is 0.</param>
    ///<param name="slice">The texture slice to update. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V3i.Zero for the source size. Default is V3i.Zero.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : IBackendTexture, source : PixVolume,
                         [<Optional; DefaultParameterValue(0)>] level : int,
                         [<Optional; DefaultParameterValue(0)>] slice : int,
                         [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                         [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        this.Upload(texture.[TextureAspect.Color, level, slice], source, offset, size)


    // ================================================================================================================
    // Download 2D
    // ================================================================================================================

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V2i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V2i.Zero for the target size. Default is V2i.Zero.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : ITextureSubResource, target : PixImage,
                           [<Optional; DefaultParameterValue(V2i())>] offset : V2i,
                           [<Optional; DefaultParameterValue(V2i())>] size : V2i) =
        let size =
            if size = V2i.Zero then
                target.Size
            else
                size

        target.Visit
            { new PixVisitors.PixImageVisitor() with
                member x.VisitUnit(img : PixImage<'T>) =
                    NativeVolume.using img.Volume (fun dst ->
                        this.Download(texture, dst.ToXYWTensor4'(), img.Format, offset.XYO, size.XYI)
                    )
            } |> ignore

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V2i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V2i.Zero for the target size. Default is V2i.Zero.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, target : PixImage,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(V2i())>] offset : V2i,
                           [<Optional; DefaultParameterValue(V2i())>] size : V2i) =
        this.Download(texture.[TextureAspect.Color, level, slice], target, offset, size)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="region">The (half-open) region of the texture to copy, Box2i.Infinite or an empty Box2i if the whole texture is to be copied. Default is an empty Box2i.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, format : PixFormat,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(Box2i())>] region : Box2i) =
        let region = region |> levelRegion texture level
        let pi = PixImage.Create(format, int64 region.SizeX, int64 region.SizeY)
        this.Download(texture, pi, level, slice, region.Min, region.Size)
        pi

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="region">The (half-open) region of the texture to copy, Box2i.Infinite or an empty Box2i if the whole texture is to be copied. Default is an empty Box2i.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(Box2i())>] region : Box2i) =
        let format = TextureFormat.toDownloadFormat texture.Format
        this.Download(texture, format, level, slice, region)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="region">The (half-open) region of the texture to copy, Box2i.Infinite or an empty Box2i if the whole texture is to be copied. Default is an empty Box2i.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : ITextureRuntime, texture : IBackendTexture,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(0)>] slice : int,
                                [<Optional; DefaultParameterValue(Box2i())>] region : Box2i) =
        let region = region |> levelRegion texture level
        let matrix = Matrix<float32>(region.Size)
        this.DownloadDepth(texture, matrix, level, slice, region.Min)
        matrix

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="region">The (half-open) region of the texture to copy, Box2i.Infinite or an empty Box2i if the whole texture is to be copied. Default is an empty Box2i.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : ITextureRuntime, texture : IBackendTexture,
                                  [<Optional; DefaultParameterValue(0)>] level : int,
                                  [<Optional; DefaultParameterValue(0)>] slice : int,
                                  [<Optional; DefaultParameterValue(Box2i())>] region : Box2i) =
        let region = region |> levelRegion texture level
        let matrix = Matrix<int>(region.Size)
        this.DownloadStencil(texture, matrix, level, slice, region.Min)
        matrix


    // ================================================================================================================
    // Download 3D
    // ================================================================================================================

    ///<summary>Downloads data from the given texture to a PixVolume.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V3i.Zero for the target size. Default is V3i.Zero.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : ITextureSubResource, target : PixVolume,
                           [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                           [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        let size =
            if size = V3i.Zero then
                target.Size
            else
                size

        target.Visit
            { new PixVisitors.PixVolumeVisitor() with
                member x.VisitUnit(img : PixVolume<'T>) =
                    NativeTensor4.using img.Tensor4 (fun dst ->
                        this.Download(texture, dst, img.Format, offset, size)
                    )
            } |> ignore

    ///<summary>Downloads data from the given texture to a PixVolume.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V3i.Zero for the target size. Default is V3i.Zero.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, target : PixVolume,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                           [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        this.Download(texture.[TextureAspect.Color, level, slice], target, offset, size)


    // ================================================================================================================
    // Copies
    // ================================================================================================================

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : IFramebufferOutput, srcOffset : V3i, dst : IFramebufferOutput, dstOffset : V3i, size : V2i) =
        this.Copy(src, srcOffset, dst, dstOffset, V3i(size, 1))

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : IFramebufferOutput, srcOffset : V2i, dst : IFramebufferOutput, dstOffset : V2i, size : V2i) =
        this.Copy(src, V3i(srcOffset, 0), dst, V3i(dstOffset, 0), V3i(size, 1))

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : IFramebufferOutput, dst : IFramebufferOutput, size : V3i) =
        this.Copy(src, V3i.Zero, dst, V3i.Zero, size)

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : IFramebufferOutput, dst : IFramebufferOutput, size : V2i) =
        this.Copy(src, V3i.Zero, dst, V3i.Zero, V3i(size, 1))

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : IFramebufferOutput, dst : IFramebufferOutput) =
        let size =
            match src with
                | :? ITextureLevel as l -> l.Size
                | _ -> V3i(src.Size, 1)

        this.Copy(src, V3i.Zero, dst, V3i.Zero, size)

    [<Extension>]
    static member CopyTo(src : IFramebufferOutput, dst : IFramebufferOutput) =
        src.Runtime.Copy(src, dst)


[<AbstractClass; Sealed; Extension>]
type ITextureSubResourceExtensions private() =

    // ================================================================================================================
    // Upload 2D
    // ================================================================================================================

    ///<summary>Uploads data from a PixImage to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V2i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V2i.Zero for the source size. Default is V2i.Zero.</param>
    [<Extension>]
    static member Upload(texture : ITextureSubResource, source : PixImage,
                         [<Optional; DefaultParameterValue(V2i())>] offset : V2i,
                         [<Optional; DefaultParameterValue(V2i())>] size : V2i) =
        texture.Runtime.Upload(texture, source, offset, size)

    // ================================================================================================================
    // Upload 3D
    // ================================================================================================================

    ///<summary>Uploads data from a PixVolume to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixVolume containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V3i.Zero for the source size. Default is V3i.Zero.</param>
    [<Extension>]
    static member Upload(texture : ITextureSubResource, source : PixVolume,
                         [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                         [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        texture.Runtime.Upload(texture, source, offset, size)

    // ================================================================================================================
    // Download 2D
    // ================================================================================================================

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V2i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V2i.Zero for the target size. Default is V2i.Zero.</param>
    [<Extension>]
    static member Download(texture : ITextureSubResource, target : PixImage,
                           [<Optional; DefaultParameterValue(V2i())>] offset : V2i,
                           [<Optional; DefaultParameterValue(V2i())>] size : V2i) =
        texture.Runtime.Download(texture, target, offset, size)

    // ================================================================================================================
    // Download 3D
    // ================================================================================================================

    ///<summary>Downloads data from the given texture to a PixVolume.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V3i.Zero for the target size. Default is V3i.Zero.</param>
    [<Extension>]
    static member Download(texture : ITextureSubResource, target : PixVolume,
                           [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                           [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        texture.Runtime.Download(texture, target, offset, size)

    // ================================================================================================================
    // Set slice 2D
    // ================================================================================================================

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V2i>, maxC : Option<V2i>, value : PixImage) =
        let minC = defaultArg minC V2i.Zero
        let maxC = defaultArg maxC (this.Size.XY - V2i.II)
        let size = V2i.II + maxC - minC
        let imgSize = value.Size
        let size = V2i(min size.X imgSize.X, min size.Y imgSize.Y)
        this.Texture.Runtime.Upload(this, value, minC, size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, value : PixImage) =
        let minX = defaultArg minX 0
        let maxX = defaultArg maxX (this.Size.X - 1)
        let minY = defaultArg minY 0
        let maxY = defaultArg maxY (this.Size.Y - 1)
        let minC = V2i(minX, minY)
        let maxC = V2i(maxX, maxY)

        let size = V2i.II + maxC - minC
        let imgSize = value.Size
        let size = V2i(min size.X imgSize.X, min size.Y imgSize.Y)
        this.Texture.Runtime.Upload(this, value, minC, size)

    // ================================================================================================================
    // Set slice 3D
    // ================================================================================================================

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V3i>, maxC : Option<V3i>, value : PixVolume) =
        let minC = defaultArg minC V3i.Zero
        let maxC = defaultArg maxC (this.Size - V3i.III)
        let size = V3i.III + maxC - minC
        let imgSize = value.Size
        let size = V3i(min size.X imgSize.X, min size.Y imgSize.Y, min size.Z imgSize.Z)
        this.Texture.Runtime.Upload(this, value, minC, size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource,
                           minX : Option<int>, maxX : Option<int>,
                           minY : Option<int>, maxY : Option<int>,
                           minZ : Option<int>, maxZ : Option<int>,
                           value : PixVolume) =
        let minX = defaultArg minX 0
        let maxX = defaultArg maxX (this.Size.X - 1)
        let minY = defaultArg minY 0
        let maxY = defaultArg maxY (this.Size.Y - 1)
        let minZ = defaultArg minZ 0
        let maxZ = defaultArg maxZ (this.Size.Z - 1)
        let minC = V3i(minX, minY, minZ)
        let maxC = V3i(maxX, maxY, maxZ)
        let size = V3i.III + maxC - minC
        let imgSize = value.Size
        let size = V3i(min size.X imgSize.X, min size.Y imgSize.Y, min size.Z imgSize.Z)
        this.Texture.Runtime.Upload(this, value, minC, size)


[<AbstractClass; Sealed; Extension>]
type IBackendTextureExtensions private() =

    // ================================================================================================================
    // Upload 2D
    // ================================================================================================================

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="level">The texture level to update. Default is 0.</param>
    ///<param name="slice">The texture slice to update. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V2i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V2i.Zero for the source size. Default is V2i.Zero.</param>
    [<Extension>]
    static member Upload(texture : IBackendTexture, source : PixImage,
                         [<Optional; DefaultParameterValue(0)>] level : int,
                         [<Optional; DefaultParameterValue(0)>] slice : int,
                         [<Optional; DefaultParameterValue(V2i())>] offset : V2i,
                         [<Optional; DefaultParameterValue(V2i())>] size : V2i) =
        texture.Runtime.Upload(texture, source, level, slice, offset, size)

    // ================================================================================================================
    // Upload 3D
    // ================================================================================================================

    ///<summary>Uploads data from a PixVolume to the given texture.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixVolume containing the data to upload.</param>
    ///<param name="level">The texture level to update. Default is 0.</param>
    ///<param name="slice">The texture slice to update. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to update or V3i.Zero for the source size. Default is V3i.Zero.</param>
    [<Extension>]
    static member Upload(texture : IBackendTexture, source : PixVolume,
                         [<Optional; DefaultParameterValue(0)>] level : int,
                         [<Optional; DefaultParameterValue(0)>] slice : int,
                         [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                         [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        texture.Runtime.Upload(texture, source, level, slice, offset, size)

    // ================================================================================================================
    // Download 2D
    // ================================================================================================================

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V2i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V2i.Zero for the target size. Default is V2i.Zero.</param>
    [<Extension>]
    static member Download(texture : IBackendTexture, target : PixImage,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(V2i())>] offset : V2i,
                           [<Optional; DefaultParameterValue(V2i())>] size : V2i) =
        texture.Runtime.Download(texture, target, level, slice, offset, size)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="region">The (half-open) region of the texture to copy, Box2i.Infinite or an empty Box2i if the whole texture is to be copied. Default is an empty Box2i.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(texture : IBackendTexture, format : PixFormat,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(Box2i())>] region : Box2i) =
        texture.Runtime.Download(texture, format, level, slice, region)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="region">The (half-open) region of the texture to copy, Box2i.Infinite or an empty Box2i if the whole texture is to be copied. Default is an empty Box2i.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(texture : IBackendTexture,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(Box2i())>] region : Box2i) =
        texture.Runtime.Download(texture, level, slice, region)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="region">The (half-open) region of the texture to copy, Box2i.Infinite or an empty Box2i if the whole texture is to be copied. Default is an empty Box2i.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(texture : IBackendTexture,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(0)>] slice : int,
                                [<Optional; DefaultParameterValue(Box2i())>] region : Box2i) =
        texture.Runtime.DownloadDepth(texture, level, slice, region)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="region">The (half-open) region of the texture to copy, Box2i.Infinite or an empty Box2i if the whole texture is to be copied. Default is an empty Box2i.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(texture : IBackendTexture,
                                  [<Optional; DefaultParameterValue(0)>] level : int,
                                  [<Optional; DefaultParameterValue(0)>] slice : int,
                                  [<Optional; DefaultParameterValue(Box2i())>] region : Box2i) =
        texture.Runtime.DownloadStencil(texture, level, slice, region)


    // ================================================================================================================
    // Download 3D
    // ================================================================================================================

    ///<summary>Downloads data from the given texture to a PixVolume.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V3i.Zero for the target size. Default is V3i.Zero.</param>
    [<Extension>]
    static member Download(texture : IBackendTexture, target : PixVolume,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(V3i())>] offset : V3i,
                           [<Optional; DefaultParameterValue(V3i())>] size : V3i) =
        texture.Runtime.Download(texture, target, level, slice, offset, size)

    // ================================================================================================================
    // Output view
    // ================================================================================================================

    ///<summary>
    /// Creates an output view of the texture with the given level and slice.
    /// In case the texture is an array or a cube and slice is negative, all items or faces are selected as texture layers.
    ///</summary>
    ///<param name="texture">The texture.</param>
    ///<param name="aspect">The aspect of the texture.</param>
    ///<param name="level">The level for the output view. Default is 0.</param>
    ///<param name="slice">The slice for the output view or -1 for all slices. Default is -1.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V3i.Zero for the target size. Default is V3i.Zero.</param>
    [<Extension>]
    static member GetOutputView(texture : IBackendTexture, aspect : TextureAspect,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(-1)>] slice : int) =
        if slice < 0 then
            texture.[aspect, level] :> IFramebufferOutput
        else
            texture.[aspect, level, slice] :> IFramebufferOutput

    ///<summary>
    /// Creates an output view of the texture with the given level and slice.
    /// In case the texture is an array or a cube and slice is negative, all items or faces are selected as texture layers.
    /// If the texture format is a depth-stencil format, the depth aspect is selected.
    ///</summary>
    ///<param name="texture">The texture.</param>
    ///<param name="aspect">The aspect of the texture.</param>
    ///<param name="level">The level for the output view. Default is 0.</param>
    ///<param name="slice">The slice for the output view or -1 for all slices. Default is -1.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V3i.Zero.</param>
    ///<param name="size">The size of the texture region to download or V3i.Zero for the target size. Default is V3i.Zero.</param>
    [<Extension>]
    static member GetOutputView(texture : IBackendTexture,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(-1)>] slice : int) =
        let aspect =
            match texture.Format.Aspect with
            | TextureAspect.DepthStencil -> TextureAspect.Depth
            | asp -> asp

        texture.GetOutputView(aspect, level, slice)