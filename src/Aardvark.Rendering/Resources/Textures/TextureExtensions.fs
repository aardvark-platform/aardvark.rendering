namespace Aardvark.Rendering

open System
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


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeExtensions private() =

    static let levelSize (level : int) (s : V2i) =
        V2i(max 1 (s.X / (1 <<< level)), max 1 (s.Y / (1 <<< level)))

    static let levelRegion (texture : IBackendTexture) (level : int) (region : Box2i) =
        if region = Box2i.Infinite then
            Box2i.FromMinAndSize(V2i.Zero, texture.Size.XY |> levelSize level)
        else
            region

    // PixVolume
    [<Extension>]
    static member Copy(this : ITextureRuntime, img : PixVolume, dst : ITextureSubResource, dstOffset : V3i, size : V3i) =
        img.Visit
            { new PixVolumeVisitor<int>() with
                member x.Visit(img : PixVolume<'a>, _) =
                    NativeTensor4.using img.Tensor4 (fun pImg ->
                        this.Copy(pImg, img.Format, dst, dstOffset, size)
                    )
                    0
            } |> ignore

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : ITextureSubResource, srcOffset : V3i, dst : PixVolume, size : V3i) =
        dst.Visit
            { new PixVolumeVisitor<int>() with
                member x.Visit(dst : PixVolume<'a>, _) =
                    NativeTensor4.using dst.Tensor4 (fun pImg ->
                        this.Copy(src, srcOffset, pImg, dst.Format, size)
                    )
                    0
            } |> ignore

    [<Extension>]
    static member Copy(this : ITextureRuntime, img : PixVolume, dst : ITextureSubResource) =
        ITextureRuntimeExtensions.Copy(this, img, dst, V3i.Zero, img.Size)

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : ITextureSubResource, dst : PixVolume) =
        ITextureRuntimeExtensions.Copy(this, src, V3i.Zero, dst, dst.Size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V3i>, maxC : Option<V3i>, value : PixVolume) =
        let minC = defaultArg minC V3i.Zero
        let maxC = defaultArg maxC (this.Size - V3i.III)
        let size = V3i.III + maxC - minC
        let imgSize = value.Size
        let size = V3i(min size.X imgSize.X, min size.Y imgSize.Y, min size.Z imgSize.Z)
        ITextureRuntimeExtensions.Copy(this.Texture.Runtime, value, this, minC, size)

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
        ITextureRuntimeExtensions.Copy(this.Texture.Runtime, value, this, minC, size)

    // PixImage
    [<Extension>]
    static member Copy(this : ITextureRuntime, img : PixImage, dst : ITextureSubResource, dstOffset : V3i, size : V2i) =
        img.Visit
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

                        this.Copy(tensor4, img.Format, dst, dstOffset, V3i(size, 1))
                    )
                    0
            } |> ignore

    [<Extension>]
    static member Copy(this : ITextureRuntime, img : PixImage, dst : ITextureSubResource, dstOffset : V2i, size : V2i) =
        ITextureRuntimeExtensions.Copy(this, img, dst, V3i(dstOffset, 0), size)

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : ITextureSubResource, srcOffset : V3i, dst : PixImage, size : V2i) =
        dst.Visit
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

                        this.Copy(src, srcOffset, tensor4, dst.Format, V3i(size,1))
                    )
                    0
            } |> ignore

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : ITextureSubResource, srcOffset : V2i, dst : PixImage, size : V2i) =
        ITextureRuntimeExtensions.Copy(this, src, V3i(srcOffset, 0), dst, size)

    [<Extension>]
    static member Copy(this : ITextureRuntime, img : PixImage, dst : ITextureSubResource) =
        ITextureRuntimeExtensions.Copy(this, img, dst, V3i.Zero, img.Size)

    [<Extension>]
    static member Copy(this : ITextureRuntime, src : ITextureSubResource, dst : PixImage) =
        ITextureRuntimeExtensions.Copy(this, src, V3i.Zero, dst, dst.Size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V2i>, maxC : Option<V2i>, z : int, value : PixImage) =
        let minC = defaultArg minC V2i.Zero
        let maxC = defaultArg maxC (this.Size.XY - V2i.II)
        let size = V2i.II + maxC - minC
        let imgSize = value.Size
        let size = V2i(min size.X imgSize.X, min size.Y imgSize.Y)
        ITextureRuntimeExtensions.Copy(this.Texture.Runtime, value, this, V3i(minC, z), size)

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
        ITextureRuntimeExtensions.Copy(this.Texture.Runtime, value, this, V3i(minC, z), size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, value : PixImage) =
        ITextureRuntimeExtensions.SetSlice(this, minX, maxX, minY, maxY, 0, value)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V2i>, maxC : Option<V2i>, value : PixImage) =
        ITextureRuntimeExtensions.SetSlice(this, minC, maxC, 0, value)

    // Copies
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

    // CopyTo
    [<Extension>]
    static member CopyTo(src : ITextureSubResource, dst : PixImage) =
        src.Texture.Runtime.Copy(src, dst)

    [<Extension>]
    static member CopyTo(src : ITextureSubResource, dst : PixVolume) =
        src.Texture.Runtime.Copy(src, dst)

    [<Extension>]
    static member CopyTo(src : IFramebufferOutput, dst : IFramebufferOutput) =
        src.Runtime.Copy(src, dst)

    // Download

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, target : PixImage) =
        this.Download(texture, level, slice, V2i.Zero, target)

    ///<summary>Downloads color data from the given texture to a PixVolume.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, target : PixVolume) =
        this.Download(texture, level, slice, V3i.Zero, target)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, region : Box2i, format : PixFormat) =
        let region = region |> levelRegion texture level
        let pi = PixImage.Create(format, int64 region.SizeX, int64 region.SizeY)
        this.Download(texture, level, slice, region.Min, pi)
        pi

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, format : PixFormat) =
        this.Download(texture, level, slice, Box2i.Infinite, format)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, level : int, format : PixFormat) =
        this.Download(texture, level, 0, Box2i.Infinite, format)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, format : PixFormat) =
        this.Download(texture, 0, 0, Box2i.Infinite, format)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, region : Box2i) =
        let pixFormat = TextureFormat.toDownloadFormat texture.Format
        this.Download(texture, level, slice, region, pixFormat)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int) =
        this.Download(texture, level, slice, Box2i.Infinite)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture, level : int) =
        this.Download(texture, level, 0, Box2i.Infinite)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : ITextureRuntime, texture : IBackendTexture) =
        this.Download(texture, 0, 0, Box2i.Infinite)

    // DownloadDepth

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    [<Extension>]
    static member DownloadDepth(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, target : Matrix<float32>) =
        this.DownloadDepth(texture, level, slice, V2i.Zero, target)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, region : Box2i) =
        let region = region |> levelRegion texture level
        let matrix = Matrix<float32>(region.Size)
        this.DownloadDepth(texture, level, slice, region.Min, matrix)
        matrix

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int) =
        this.DownloadDepth(texture, level, slice, Box2i.Infinite)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : ITextureRuntime, texture : IBackendTexture, level : int) =
        this.DownloadDepth(texture, level, 0, Box2i.Infinite)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : ITextureRuntime, texture : IBackendTexture) =
        this.DownloadDepth(texture, 0, 0, Box2i.Infinite)

    // DownloadStencil

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    [<Extension>]
    static member DownloadStencil(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, target : Matrix<int>) =
        this.DownloadStencil(texture, level, slice, V2i.Zero, target)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, region : Box2i) =
        let region = region |> levelRegion texture level
        let matrix = Matrix<int>(region.Size)
        this.DownloadStencil(texture, level, slice, region.Min, matrix)
        matrix

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int) =
        this.DownloadStencil(texture, level, slice, Box2i.Infinite)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<param name="level">The texture level to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : ITextureRuntime, texture : IBackendTexture, level : int) =
        this.DownloadStencil(texture, level, 0, Box2i.Infinite)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : ITextureRuntime, texture : IBackendTexture) =
        this.DownloadStencil(texture, 0, 0, Box2i.Infinite)

    // Upload

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="level">The texture level to update.</param>
    ///<param name="slice">The texture slice to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : IBackendTexture, level : int, slice : int, source : PixImage) =
        this.Upload(texture, level, slice, V2i.Zero, source)

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="level">The texture level to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : IBackendTexture, level : int, source : PixImage) =
        this.Upload(texture, level, 0, V2i.Zero, source)

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The runtime.</param>
    ///<param name="texture">The texture to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : ITextureRuntime, texture : IBackendTexture, source : PixImage) =
        this.Upload(texture, 0, 0, V2i.Zero, source)


[<AbstractClass; Sealed; Extension>]
type IBackendTextureExtensions private() =

    // Download

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, slice : int, offset : V2i, target : PixImage) =
        this.Runtime.Download(this, level, slice, offset, target)

    ///<summary>Downloads color data from the given texture to a PixVolume.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, slice : int, offset : V3i, target : PixVolume) =
        this.Runtime.Download(this, level, slice, offset, target)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="target">The PixImage to copy the data to.</param>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, slice : int, target : PixImage) =
        this.Runtime.Download(this, level, slice, target)

    ///<summary>Downloads color data from the given texture to a PixVolume.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="target">The PixVolume to copy the data to.</param>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, slice : int, target : PixVolume) =
        this.Runtime.Download(this, level, slice, target)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, slice : int, region : Box2i, format : PixFormat) =
        this.Runtime.Download(this, level, slice, region, format)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, slice : int, format : PixFormat) =
        this.Runtime.Download(this, level, slice, format)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, format : PixFormat) =
        this.Runtime.Download(this, level, format)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="format">The format of the PixImage.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, format : PixFormat) =
        this.Runtime.Download(this, format)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, slice : int, region : Box2i) =
        this.Runtime.Download(this, level, slice, region)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int, slice : int) =
        this.Runtime.Download(this, level, slice)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture, level : int) =
        this.Runtime.Download(this, level)

    ///<summary>Downloads color data from the given texture to a PixImage.</summary>
    ///<param name="this">The texture.</param>
    ///<returns>A PixImage containing the downloaded data.</returns>
    [<Extension>]
    static member Download(this : IBackendTexture) =
        this.Runtime.Download(this)

    // DownloadDepth

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<float32>) =
        this.Runtime.DownloadDepth(this, level, slice, offset, target)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture, level : int, slice : int, target : Matrix<float32>) =
        this.Runtime.DownloadDepth(this, level, slice, V2i.Zero, target)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture, level : int, slice : int, region : Box2i) =
        this.Runtime.DownloadDepth(this, level, slice, region)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture, level : int, slice : int) =
        this.Runtime.DownloadDepth(this, level, slice)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture, level : int) =
        this.Runtime.DownloadDepth(this, level)

    ///<summary>Downloads depth data from the given texture to a float matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadDepth(this : IBackendTexture) =
        this.Runtime.DownloadDepth(this)

    // DownloadStencil

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture, level : int, slice : int, offset : V2i, target : Matrix<int>) =
        this.Runtime.DownloadStencil(this, level, slice, offset, target)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="target">The matrix to copy the data to.</param>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture, level : int, slice : int, target : Matrix<int>) =
        this.Runtime.DownloadStencil(this, level, slice, target)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<param name="region">The (half-open) region of the texture to copy, or Box2i.Infinite if the whole texture is to be copied.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture, level : int, slice : int, region : Box2i) =
        this.Runtime.DownloadStencil(this, level, slice, region)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<param name="slice">The texture slice to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture, level : int, slice : int) =
        this.Runtime.DownloadStencil(this, level, slice)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to download.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture, level : int) =
        this.Runtime.DownloadStencil(this, level)

    ///<summary>Downloads stencil data from the given texture to an integer matrix.</summary>
    ///<param name="this">The texture.</param>
    ///<returns>A matrix containing the downloaded data.</returns>
    [<Extension>]
    static member DownloadStencil(this : IBackendTexture) =
        this.Runtime.DownloadStencil(this)

    // Upload

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to update.</param>
    ///<param name="slice">The texture slice to update.</param>
    ///<param name="offset">The minimum coordinate to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : IBackendTexture, level : int, slice : int, offset : V2i, source : PixImage) =
        this.Runtime.Upload(this, level, slice, offset, source)

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to update.</param>
    ///<param name="slice">The texture slice to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : IBackendTexture, level : int, slice : int, source : PixImage) =
        this.Runtime.Upload(this, level, slice, V2i.Zero, source)

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="level">The texture level to update.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : IBackendTexture, level : int, source : PixImage) =
        this.Runtime.Upload(this, level, 0, V2i.Zero, source)

    ///<summary>Uploads data from a PixImage to the given texture.</summary>
    ///<param name="this">The texture.</param>
    ///<param name="source">The PixImage containing the data to upload.</param>
    [<Extension>]
    static member Upload(this : IBackendTexture, source : PixImage) =
        this.Runtime.Upload(this, 0, 0, V2i.Zero, source)

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