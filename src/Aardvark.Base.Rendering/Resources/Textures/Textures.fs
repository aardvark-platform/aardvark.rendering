namespace Aardvark.Base

open System
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

type TextureAspect =
    | Color
    | Depth
    | Stencil

[<AllowNullLiteral>]
type ITexture =
    abstract member WantMipMaps : bool

type ISparseTexture<'a when 'a : unmanaged> =
    inherit IDisposable

    [<CLIEvent>]
    abstract member OnSwap : IEvent<EventHandler, EventArgs>

    abstract member UsedMemory : Mem
    abstract member AllocatedMemory : Mem

    abstract member Size : V3i
    abstract member MipMapLevels : int
    abstract member Count : int
    abstract member Format : Col.Format
    abstract member Texture : aval<ITexture>
    abstract member GetBrickCount : level : int -> V3i

    abstract member SparseLevels : int
    abstract member BrickSize : V3i
    abstract member UploadBrick : level : int * slice : int * index : V3i * data : NativeTensor4<'a> -> IDisposable

type INativeTextureData =
    abstract member Size : V3i
    abstract member SizeInBytes : int64
    abstract member Use : (nativeint -> 'a) -> 'a

[<AllowNullLiteral>]
type INativeTexture =
    inherit ITexture
    abstract member Dimension : TextureDimension
    abstract member Format : TextureFormat
    abstract member MipMapLevels : int
    abstract member Count : int
    abstract member Item : slice : int * level : int -> INativeTextureData with get

type IBackendTexture =
    inherit ITexture
    abstract member Runtime : ITextureRuntime
    abstract member Dimension : TextureDimension
    abstract member Format : TextureFormat
    abstract member Samples : int
    abstract member Count : int
    abstract member MipMapLevels : int
    abstract member Size : V3i
    abstract member Handle : obj

and IFramebufferOutput =
    abstract member Runtime : ITextureRuntime
    abstract member Format : RenderbufferFormat
    abstract member Samples : int
    abstract member Size : V2i

and IBackendTextureOutputView =
    inherit IFramebufferOutput
    abstract member texture : IBackendTexture
    abstract member level : int
    abstract member slices : Range1i

and ITextureRange =
    abstract member Texture : IBackendTexture
    abstract member Aspect : TextureAspect
    abstract member Levels : Range1i
    abstract member Slices : Range1i

and ITextureSlice =
    inherit ITextureRange
    abstract member Slice : int

and ITextureLevel =
    inherit ITextureRange
    inherit IFramebufferOutput
    abstract member Level : int
    abstract member Size : V3i

and ITextureSubResource =
    inherit ITextureSlice
    inherit ITextureLevel

and IRenderbuffer =
    inherit IFramebufferOutput
    abstract member Handle : obj

and ITextureRuntime =
    inherit IBufferRuntime

    abstract member CreateTexture : size : V3i * dim : TextureDimension * format : TextureFormat * slices : int * levels : int * samples : int -> IBackendTexture
    abstract member PrepareTexture : ITexture -> IBackendTexture

    abstract member Copy : src : NativeTensor4<'a> * srcFormat : Col.Format * dst : ITextureSubResource * dstOffset : V3i * size : V3i -> unit
    abstract member Copy : src : ITextureSubResource * srcOffset : V3i * dst : NativeTensor4<'a> * dstFormat : Col.Format * size : V3i -> unit
    abstract member Copy : src : IFramebufferOutput * srcOffset : V3i * dst : IFramebufferOutput * dstOffset : V3i * size : V3i -> unit

    abstract member DeleteTexture : IBackendTexture -> unit
    abstract member CreateRenderbuffer : size : V2i * format : RenderbufferFormat * samples : int -> IRenderbuffer

    abstract member GenerateMipMaps : IBackendTexture -> unit
    abstract member ResolveMultisamples : src : IFramebufferOutput * target : IBackendTexture * imgTrafo : ImageTrafo -> unit
    abstract member Upload : texture : IBackendTexture * level : int * slice : int * source : PixImage -> unit
    abstract member Download : texture : IBackendTexture * level : int * slice : int * target : PixImage -> unit
    abstract member Download : texture : IBackendTexture * level : int * slice : int * target : PixVolume -> unit
    abstract member DownloadStencil : texture : IBackendTexture * level : int * slice : int * target : Matrix<int> -> unit
    abstract member DownloadDepth : texture : IBackendTexture * level : int * slice : int * target : Matrix<float32> -> unit
    abstract member Copy : src : IBackendTexture * srcBaseSlice : int * srcBaseLevel : int * dst : IBackendTexture * dstBaseSlice : int * dstBaseLevel : int * slices : int * levels : int -> unit

    abstract member CreateTexture : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture
    abstract member CreateTextureArray : size : V2i * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture
    abstract member CreateTextureCube : size : int * format : TextureFormat * levels : int * samples : int -> IBackendTexture
    abstract member CreateTextureCubeArray : size : int * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture

    abstract member ClearColor : texture : IBackendTexture * color : C4f -> unit
    abstract member ClearDepthStencil : texture : IBackendTexture * depth : Option<float> * stencil : Option<int> -> unit

    abstract member CreateTextureView : texture : IBackendTexture * levels : Range1i * slices : Range1i * isArray : bool -> IBackendTexture

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
        ITextureRuntimeExtensions.Copy(src.Texture.Runtime, src, dst)

    [<Extension>]
    static member CopyTo(src : ITextureSubResource, dst : PixVolume) =
        ITextureRuntimeExtensions.Copy(src.Texture.Runtime, src, dst)

    [<Extension>]
    static member CopyTo(src : IFramebufferOutput, dst : IFramebufferOutput) =
        ITextureRuntimeExtensions.Copy(src.Runtime, src, dst)
