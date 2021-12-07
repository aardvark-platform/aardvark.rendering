namespace Aardvark.Rendering

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base

[<AutoOpen>]
module private PixVisitors =
    [<AbstractClass>]
    type PixImageVisitor<'r>() =
        static let table =
            LookupTable.lookupTable [
                typeof<int8>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<int8>(unbox img, 127y))
                typeof<uint8>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<uint8>(unbox img, 255uy))
                typeof<int16>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<int16>(unbox img, Int16.MaxValue))
                typeof<uint16>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<uint16>(unbox img, UInt16.MaxValue))
                typeof<int32>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<int32>(unbox img, Int32.MaxValue))
                typeof<uint32>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<uint32>(unbox img, UInt32.MaxValue))
                typeof<int64>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<int64>(unbox img, Int64.MaxValue))
                typeof<uint64>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<uint64>(unbox img, UInt64.MaxValue))
                typeof<float16>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<float16>(unbox img, float16(Float32 = 1.0f)))
                typeof<float32>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<float32>(unbox img, 1.0f))
                typeof<float>, (fun (self : PixImageVisitor<'r>, img : PixImage) -> self.Visit<float>(unbox img, 1.0))
            ]
        abstract member Visit<'a when 'a : unmanaged> : PixImage<'a> * 'a -> 'r

        interface IPixImageVisitor<'r> with
            member x.Visit<'a>(img : PixImage<'a>) =
                table (typeof<'a>) (x, img)

    [<AbstractClass>]
    type PixVolumeVisitor<'r>() =
        static let table =
            LookupTable.lookupTable [
                typeof<int8>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<int8>(unbox img, 127y))
                typeof<uint8>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<uint8>(unbox img, 255uy))
                typeof<int16>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<int16>(unbox img, Int16.MaxValue))
                typeof<uint16>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<uint16>(unbox img, UInt16.MaxValue))
                typeof<int32>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<int32>(unbox img, Int32.MaxValue))
                typeof<uint32>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<uint32>(unbox img, UInt32.MaxValue))
                typeof<int64>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<int64>(unbox img, Int64.MaxValue))
                typeof<uint64>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<uint64>(unbox img, UInt64.MaxValue))
                typeof<float16>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<float16>(unbox img, float16(Float32 = 1.0f)))
                typeof<float32>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<float32>(unbox img, 1.0f))
                typeof<float>, (fun (self : PixVolumeVisitor<'r>, img : PixVolume) -> self.Visit<float>(unbox img, 1.0))
            ]

        abstract member Visit<'a when 'a : unmanaged> : PixVolume<'a> * 'a -> 'r

        interface IPixVolumeVisitor<'r> with
            member x.Visit<'a>(img : PixVolume<'a>) =
                table (typeof<'a>) (x, img)

[<AutoOpen>]
module TensorExtensions =

    type NativeTensor4<'T when 'T : unmanaged> with
        member x.Format =
            match x.Size.W with
            | 1L -> Col.Format.Gray
            | 2L -> Col.Format.GrayAlpha
            | 3L -> Col.Format.RGB
            | _  -> Col.Format.RGBA

[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeExtensions private() =

    static let levelRegion (texture : IBackendTexture) (level : int) (region : Box2i) =
        if region = Box2i.Infinite then
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
    // PixVolume
    // ================================================================================================================

    ///<summary>Uploads data from a PixVolume to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixVolume containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="size">The size of the texture region to update.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : ITextureSubResource, source : PixVolume, offset : V3i, size : V3i) =
        source.Visit
            { new PixVolumeVisitor<int>() with
                member x.Visit(img : PixVolume<'a>, _) =
                    NativeTensor4.using img.Tensor4 (fun pImg ->
                        this.Upload(texture, pImg, offset, size)
                    )
                    0
            } |> ignore

    ///<summary>Uploads data from a PixVolume to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixVolume containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : ITextureSubResource, source : PixVolume) =
        this.Upload(texture, source, V3i.Zero, source.Size)

    ///<summary>Uploads data from a PixVolume to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixVolume containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="size">The size of the texture region to update.</param>
    [<Extension>]
    static member Upload(texture : ITextureSubResource, source : PixVolume, offset : V3i, size : V3i) =
        texture.Runtime.Upload(texture, source, offset, size)

    ///<summary>Uploads data from a PixVolume to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixVolume containing the data to upload.</param>
    [<Extension>]
    static member Upload(texture : ITextureSubResource, source : PixVolume) =
        texture.Texture.Runtime.Upload(texture, source)

    ///<summary>Downloads data from the given texture to a PixVolume.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download.</param>
    ///<param name="size">The size of the texture region to download.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : ITextureSubResource, target : PixVolume, offset : V3i, size : V3i) =
        target.Visit
            { new PixVolumeVisitor<int>() with
                member x.Visit(dst : PixVolume<'a>, _) =
                    NativeTensor4.using dst.Tensor4 (fun pImg ->
                        this.Download(texture, pImg, offset, size)
                    )
                    0
            } |> ignore

    ///<summary>Downloads data from the given texture to a PixVolume.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : ITextureSubResource, target : PixVolume) =
        this.Download(texture, target, V3i.Zero, target.Size)

    ///<summary>Downloads data from the given texture to a PixVolume.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download.</param>
    ///<param name="size">The size of the texture region to download.</param>
    [<Extension>]
    static member Download(texture : ITextureSubResource, target : PixVolume, offset : V3i, size : V3i) =
        texture.Texture.Runtime.Download(texture, target, offset, size)

    ///<summary>Downloads data from the given texture to a PixVolume.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    [<Extension>]
    static member Download(texture : ITextureSubResource, target : PixVolume) =
        texture.Texture.Runtime.Download(texture, target)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V3i>, maxC : Option<V3i>, value : PixVolume) =
        let minC = defaultArg minC V3i.Zero
        let maxC = defaultArg maxC (this.Size - V3i.III)
        let size = V3i.III + maxC - minC
        let imgSize = value.Size
        let size = V3i(min size.X imgSize.X, min size.Y imgSize.Y, min size.Z imgSize.Z)
        this.Texture.Runtime.Upload(this, value, minC, size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, minZ : Option<int>, maxZ : Option<int>, value : PixVolume) =
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


    // ================================================================================================================
    // PixImage
    // ================================================================================================================

    ///<summary>Uploads data from a PixImage to the given texture sub resource.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="size">The size of the texture region to update.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : ITextureSubResource, source : PixImage, offset : V3i, size : V2i) =
        source.Visit
            { new PixImageVisitor<int>() with
                member x.Visit(img : PixImage<'a>, _) =
                    NativeVolume.using img.Volume (fun pImg ->
                        let info = pImg.Info

                        let tensor4 =
                            NativeTensor4<'a>(
                                pImg.Pointer,
                                Tensor4Info(
                                    info.Origin,
                                    V4l(info.SX, info.SY, 1L, info.SZ),
                                    V4l(info.DX, info.DY, info.DY * info.SY, info.DZ)
                                )
                            )

                        this.Upload(texture, tensor4, offset, V3i(size, 1))
                    )
                    0
            } |> ignore


    ///<summary>Uploads data from a PixImage to the given texture sub resource.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="size">The size of the texture region to update.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : ITextureSubResource, source : PixImage, offset : V2i, size : V2i) =
        this.Upload(texture, source, V3i(offset, 0), size)
    
    ///<summary>Uploads data from a PixImage to the given texture sub resource.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : ITextureSubResource, source : PixImage) =
        this.Upload(texture, source, V3i.Zero, source.Size)

    ///<summary>Uploads data from a PixImage to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="size">The size of the texture region to update.</param>
    [<Extension>]
    static member Upload(texture : ITextureSubResource, source : PixImage, offset : V3i, size : V2i) =
        texture.Texture.Runtime.Upload(texture, source, offset, size)

    ///<summary>Uploads data from a PixImage to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="size">The size of the texture region to update.</param>
    [<Extension>]
    static member Upload(texture : ITextureSubResource, source : PixImage, offset : V2i, size : V2i) =
        texture.Texture.Runtime.Upload(texture, source, offset, size)

    ///<summary>Uploads data from a PixImage to the given texture sub resource.</summary>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(texture : ITextureSubResource, source : PixImage) =
        texture.Texture.Runtime.Upload(texture, source)

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download.</param>
    ///<param name="size">The size of the texture region to download.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : ITextureSubResource, target : PixImage, offset : V3i, size : V2i) =
        target.Visit
            { new PixImageVisitor<int>() with
                member x.Visit(dst : PixImage<'a>, _) =
                    NativeVolume.using dst.Volume (fun pImg ->
                        let info = pImg.Info

                        let tensor4 =
                            NativeTensor4<'a>(
                                pImg.Pointer,
                                Tensor4Info(
                                    info.Origin,
                                    V4l(info.SX, info.SY, 1L, info.SZ),
                                    V4l(info.DX, info.DY, info.DY * info.SY, info.DZ)
                                )
                            )

                        this.Download(texture, tensor4, offset, V3i(size,1))
                    )
                    0
            } |> ignore

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download.</param>
    ///<param name="size">The size of the texture region to download.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : ITextureSubResource, target : PixImage, offset : V2i, size : V2i) =
        this.Download(texture, target, V3i(offset, 0), size)

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : ITextureSubResource, target : PixImage) =
        this.Download(texture, target, V3i.Zero, target.Size)

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download.</param>
    ///<param name="size">The size of the texture region to download.</param>
    [<Extension>]
    static member Download(texture : ITextureSubResource, target : PixImage, offset : V3i, size : V2i) =
        texture.Texture.Runtime.Download(texture, target, offset, size)

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="offset">The minimum coordinate to download.</param>
    ///<param name="size">The size of the texture region to download.</param>
    [<Extension>]
    static member Download(texture : ITextureSubResource, target : PixImage, offset : V2i, size : V2i) =
        texture.Texture.Runtime.Download(texture, target, offset, size)

    ///<summary>Downloads data from the given texture to a PixImage.</summary>
    ///<param name="texture">The texture to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    [<Extension>]
    static member Download(texture : ITextureSubResource, target : PixImage) =
        texture.Texture.Runtime.Download(texture, target)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V2i>, maxC : Option<V2i>, z : int, value : PixImage) =
        let minC = defaultArg minC V2i.Zero
        let maxC = defaultArg maxC (this.Size.XY - V2i.II)
        let size = V2i.II + maxC - minC
        let imgSize = value.Size
        let size = V2i(min size.X imgSize.X, min size.Y imgSize.Y)
        this.Texture.Runtime.Upload(this, value, V3i(minC, z), size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, z : int, value : PixImage) =
        let minX = defaultArg minX 0
        let maxX = defaultArg maxX (this.Size.X - 1)
        let minY = defaultArg minY 0
        let maxY = defaultArg maxY (this.Size.Y - 1)
        let minC = V2i(minX, minY)
        let maxC = V2i(maxX, maxY)

        let size = V2i.II + maxC - minC
        let imgSize = value.Size
        let size = V2i(min size.X imgSize.X, min size.Y imgSize.Y)
        this.Texture.Runtime.Upload(this, value, V3i(minC, z), size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, value : PixImage) =
        this.SetSlice(minX, maxX, minY, maxY, 0, value)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V2i>, maxC : Option<V2i>, value : PixImage) =
        this.SetSlice(minC, maxC, 0, value)


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

    // ================================================================================================================
    // Download
    // ================================================================================================================

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, format : PixFormat, region : Box2i,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int) =
        let region = region |> levelRegion texture level
        let pi = PixImage.Create(format, int64 region.SizeX, int64 region.SizeY)
        this.Download(texture, pi, level, slice, region.Min)
        pi

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, format : PixFormat,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Download(texture, format, Box2i.Infinite, level, slice)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, region : Box2i,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int) =
        let format = TextureFormat.toDownloadFormat texture.Format
        this.Download(texture, format, region, level, slice)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Download(texture, Box2i.Infinite, level, slice)


    // ================================================================================================================
    // Download depth
    // ================================================================================================================

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : ITextureRuntime, texture : IBackendTexture, region : Box2i,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(0)>] slice : int) =
        let region = region |> levelRegion texture level
        let matrix = Matrix<float32>(region.Size)
        this.DownloadDepth(texture, matrix, level, slice, region.Min)
        matrix

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : ITextureRuntime, texture : IBackendTexture,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.DownloadDepth(texture, Box2i.Infinite, level, slice)

    // ================================================================================================================
    // Download stencil
    // ================================================================================================================


    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : ITextureRuntime, texture : IBackendTexture, region : Box2i,
                                  [<Optional; DefaultParameterValue(0)>] level : int,
                                  [<Optional; DefaultParameterValue(0)>] slice : int) =
        let region = region |> levelRegion texture level
        let matrix = Matrix<int>(region.Size)
        this.DownloadStencil(texture, matrix, level, slice, region.Min)
        matrix

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : ITextureRuntime, texture : IBackendTexture,
                                  [<Optional; DefaultParameterValue(0)>] level : int,
                                  [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.DownloadStencil(texture, Box2i.Infinite, level, slice)


    // ================================================================================================================
    // Clear
    // ================================================================================================================

    /// Clears the given texture with the given color.
    [<Extension>]
    static member inline ClearColor(this : ITextureRuntime, texture : IBackendTexture, color : ^Color) =
        this.ClearColor(texture, ClearColor.create color)

    /// Clears the given texture with the given depth and stencil values.
    [<Extension>]
    static member inline ClearDepthStencil(this : ITextureRuntime, texture : IBackendTexture, depth: Option< ^Depth>, stencil : Option< ^Stencil>) =
        let depth = depth |> Option.map ClearDepth.create
        let stencil = stencil |> Option.map ClearStencil.create
        this.ClearDepthStencil(texture, depth, stencil)

    /// Clears the texture with the given clear values.
    [<Extension>]
    static member Clear(this : ITextureRuntime, texture : IBackendTexture, values : ClearValues) =
        if texture.Format.HasDepth || texture.Format.HasStencil then
            this.ClearDepthStencil(texture, values.Depth, values.Stencil)
        else
            match values.Colors.Default with
            | Some color -> this.ClearColor(texture, color)
            | _ -> ()


[<AbstractClass; Sealed; Extension>]
type IBackendTextureExtensions private() =

    // ================================================================================================================
    // Download
    // ================================================================================================================

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V2i.Zero.</param>
    [<Extension>]
    static member Download(this : IBackendTexture, target : PixImage,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(V2i())>] offset : V2i) =
        this.Runtime.Download(this, target, level, slice, offset)

    ///<summary>Downloads color data from the given texture to a PixVolume.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    [<Extension>]
    static member Download(this : IBackendTexture, target : PixVolume,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int,
                           [<Optional; DefaultParameterValue(V3i())>] offset : V3i) =
        this.Runtime.Download(this, target, level, slice, offset)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, format : PixFormat, region : Box2i,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Runtime.Download(this, format, region, level, slice)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, format : PixFormat,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Runtime.Download(this, format, level, slice)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, region : Box2i,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Runtime.Download(this, region, level, slice)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture,
                           [<Optional; DefaultParameterValue(0)>] level : int,
                           [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Runtime.Download(this, level, slice)


    // ================================================================================================================
    // Download depth
    // ================================================================================================================

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V2i.Zero.</param>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture, target : Matrix<float32>,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(0)>] slice : int,
                                [<Optional; DefaultParameterValue(V2i())>] offset : V2i) =
        this.Runtime.DownloadDepth(this, target, level, slice, offset)


    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture, region : Box2i,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Runtime.DownloadDepth(this, region, level, slice)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture,
                                [<Optional; DefaultParameterValue(0)>] level : int,
                                [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Runtime.DownloadDepth(this, level, slice)


    // ================================================================================================================
    // Download stencil
    // ================================================================================================================

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to download. Default is V2i.Zero.</param>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture,  target : Matrix<int>,
                                  [<Optional; DefaultParameterValue(0)>] level : int,
                                  [<Optional; DefaultParameterValue(0)>] slice : int,
                                  [<Optional; DefaultParameterValue(V2i())>] offset : V2i) =
        this.Runtime.DownloadStencil(this, target, level, slice, offset)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="level">The texture level to download. Default is 0.</param>
    ///<param name="slice">The texture slice to download. Default is 0.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture, region : Box2i,
                                  [<Optional; DefaultParameterValue(0)>] level : int,
                                  [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Runtime.DownloadStencil(this, region, level, slice)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture,
                                  [<Optional; DefaultParameterValue(0)>] level : int,
                                  [<Optional; DefaultParameterValue(0)>] slice : int) =
        this.Runtime.DownloadStencil(this, level, slice)


    // ================================================================================================================
    // Upload
    // ================================================================================================================

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    ///<param name="level">The texture level to update. Default is 0.</param>
    ///<param name="slice">The texture slice to update. Default is 0.</param>
    ///<param name="offset">The minimum coordinate to update. Default is V2i.Zero. </param>
    [<Extension>]
    static member Upload(this : IBackendTexture, source : PixImage,
                         [<Optional; DefaultParameterValue(0)>] level : int,
                         [<Optional; DefaultParameterValue(0)>] slice : int,
                         [<Optional; DefaultParameterValue(V2i())>] offset : V2i) =
        this.Runtime.Upload(this, source, level, slice, offset)


    // ================================================================================================================
    // Output view
    // ================================================================================================================

    /// <summary>
    /// Creates an output view of the texture with the given level and slice.
    /// In case the texture is an array or a cube and slice is negative, all items or faces are selected as texture layers.
    /// </summary>
    [<Extension>]
    static member GetOutputView(this : IBackendTexture, level : int, slice : int) =
        let aspect = TextureAspect.ofTextureFormat this.Format
        if slice < 0 then
            this.[aspect, level] :> IFramebufferOutput
        else
            this.[aspect, level, slice] :> IFramebufferOutput

    /// <summary>
    /// Creates an output view of the texture with the given level.
    /// In case the texture is an array or a cube, all items or faces are selected as texture layers.
    /// </summary>
    [<Extension>]
    static member GetOutputView(this : IBackendTexture, level : int) =
        let aspect = TextureAspect.ofTextureFormat this.Format
        this.[aspect, level] :> IFramebufferOutput

    /// <summary>
    /// Creates an output view of the first level of the texture.
    /// In case the texture is an array or a cube, all items or faces are selected as texture layers.
    /// </summary>
    [<Extension>]
    static member GetOutputView(this : IBackendTexture) =
        let aspect = TextureAspect.ofTextureFormat this.Format
        this.[aspect, 0] :> IFramebufferOutput