namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
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
    abstract member Texture : IMod<ITexture>
    abstract member GetBrickCount : level : int -> V3i

    abstract member SparseLevels : int
    abstract member BrickSize : V3i
    abstract member UploadBrick : level : int * slice : int * index : V3i * data : NativeTensor4<'a> -> IDisposable


 
type IFramebufferOutput =
    abstract member Format : RenderbufferFormat
    abstract member Samples : int
    abstract member Size : V2i

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
    abstract member Runtime : ITextureRuntime
    abstract member Handle : obj




and ITextureRuntime =
    inherit IBufferRuntime

    abstract member CreateTexture : size : V3i * dim : TextureDimension * format : TextureFormat * slices : int * levels : int * samples : int -> IBackendTexture
    abstract member PrepareTexture : ITexture -> IBackendTexture


//    abstract member Copy : src : ITextureLevel * srcOffset : V3i * dst : ITextureLevel * dstOffset : V3i * size : V3i -> unit
//    abstract member Blit : src : ITextureLevel * srcRange : Box3i * dst : ITextureLevel * dstRange : Box3i * linear : bool -> unit
    abstract member Copy : src : NativeTensor4<'a> * srcFormat : Col.Format * dst : ITextureSubResource * dstOffset : V3i * size : V3i -> unit
    abstract member Copy : src : ITextureSubResource * srcOffset : V3i * dst : NativeTensor4<'a> * dstFormat : Col.Format * size : V3i -> unit
    //    member x.Copy<'a when 'a : unmanaged>(src : ITextureSubResource, srcOffset : V3i, dst : NativeTensor4<'a>, fmt : Col.Format, size : V3i) =




    abstract member DeleteTexture : IBackendTexture -> unit
    abstract member CreateRenderbuffer : size : V2i * format : RenderbufferFormat * samples : int -> IRenderbuffer

    abstract member GenerateMipMaps : IBackendTexture -> unit
    abstract member ResolveMultisamples : src : IFramebufferOutput * target : IBackendTexture * imgTrafo : ImageTrafo -> unit
    abstract member Upload : texture : IBackendTexture * level : int * slice : int * source : PixImage -> unit
    abstract member Download : texture : IBackendTexture * level : int * slice : int * target : PixImage -> unit
    abstract member DownloadStencil : texture : IBackendTexture * level : int * slice : int * target : Matrix<int> -> unit
    abstract member DownloadDepth : texture : IBackendTexture * level : int * slice : int * target : Matrix<float32> -> unit
    abstract member Copy : src : IBackendTexture * srcBaseSlice : int * srcBaseLevel : int * dst : IBackendTexture * dstBaseSlice : int * dstBaseLevel : int * slices : int * levels : int -> unit


    abstract member CreateTexture : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture
    abstract member CreateTextureArray : size : V2i * format : TextureFormat * levels : int * samples : int * count : int -> IBackendTexture
    abstract member CreateTextureCube : size : V2i * format : TextureFormat * levels : int * samples : int -> IBackendTexture



[<AbstractClass>]
type private PixImageVisitor<'r>() =
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


[<AbstractClass; Sealed; Extension>]
type ITextureRuntimeExtensions private() =
//
//    [<Extension>]
//    static member CreateTexture(this : ITextureRuntime, size : V2i, format : TextureFormat, levels : int, samples : int) =
//        this.CreateTexture(V3i(size, 1), TextureDimension.Texture2D, format, 1, levels, samples)
//
//    [<Extension;>]
//    static member CreateTextureArray(this : ITextureRuntime, size : V2i, format : TextureFormat, levels : int, samples : int, count : int) =
//        this.CreateTexture(V3i(size, 1), TextureDimension.Texture2D, format, count, levels, samples)
//
//    [<Extension>]
//    static member CreateTextureCube(this : ITextureRuntime, size : V2i, format : TextureFormat, levels : int, samples : int) =
//        this.CreateTexture(V3i(size, 1), TextureDimension.TextureCube, format, 1, levels, samples)
//

    


    [<Extension>]
    static member Copy<'a when 'a : unmanaged>(this : ITextureRuntime, img : PixImage<'a>, dst : ITextureSubResource, dstOffset : V2i, size : V2i) =
        NativeVolume.using img.Volume (fun pImg ->
            let info = pImg.Info
            
            let tensor4 = 
                NativeTensor4<'a>(
                    pImg.Pointer, 
                    Tensor4Info(
                        0L,
                        V4l(info.SX, info.SY, 1L, info.SZ),
                        V4l(info.DX, info.DY, info.DY * info.SY, info.DZ)
                    )
                )

            this.Copy(tensor4, img.Format, dst, V3i(dstOffset, 0), V3i(size, 1))
        )

    [<Extension>]
    static member Copy<'a when 'a : unmanaged>(this : ITextureRuntime, src : ITextureSubResource, srcOffset : V2i, dst : PixImage<'a>, size : V2i) =
        NativeVolume.using dst.Volume (fun pImg ->
            let info = pImg.Info
            
            let tensor4 = 
                NativeTensor4<'a>(
                    pImg.Pointer, 
                    Tensor4Info(
                        0L,
                        V4l(info.SX, info.SY, 1L, info.SZ),
                        V4l(info.DX, info.DY, info.DY * info.SY, info.DZ)
                    )
                )

            this.Copy(src, V3i(srcOffset,0), tensor4, dst.Format, V3i(size,1))
        )
        
    [<Extension>]
    static member Copy<'a when 'a : unmanaged>(this : ITextureRuntime, img : PixImage<'a>, dst : ITextureSubResource) =
        ITextureRuntimeExtensions.Copy(this, img, dst, V2i.Zero, img.Size)
        
    [<Extension>]
    static member Copy<'a when 'a : unmanaged>(this : ITextureRuntime, src : ITextureSubResource, dst : PixImage<'a>) =
        ITextureRuntimeExtensions.Copy(this, src, V2i.Zero, dst, dst.Size)
        
    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minC : Option<V2i>, maxC : Option<V2i>, value : PixImage<'a>) =
        let minC = defaultArg minC V2i.Zero
        let maxC = defaultArg maxC (this.Size.XY - V2i.II)
        let size = V2i.II + maxC - minC
        let imgSize = value.Size
        let size = V2i(min size.X imgSize.X, min size.Y imgSize.Y)
        this.Texture.Runtime.Copy(value, this, minC, size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, value : PixImage<'a>) =
        let minX = defaultArg minX 0
        let maxX = defaultArg maxX (this.Size.X - 1)
        let minY = defaultArg minY 0
        let maxY = defaultArg maxY (this.Size.Y - 1)
        let minC = V2i(minX, minY)
        let maxC = V2i(maxX, maxY)

        let size = V2i.II + maxC - minC
        let imgSize = value.Size
        let size = V2i(min size.X imgSize.X, min size.Y imgSize.Y)
        this.Texture.Runtime.Copy(value, this, minC, size)

    [<Extension>]
    static member SetSlice(this : ITextureSubResource, minX : Option<int>, maxX : Option<int>, minY : Option<int>, maxY : Option<int>, value : PixImage) =
        value.Visit {
            new PixImageVisitor<int>() with
                member x.Visit (img : PixImage<'a>, def : 'a) =
                    ITextureRuntimeExtensions.SetSlice(this, minX, maxX, minY, maxY, img)
                    1
        } |> ignore



[<AutoOpen>]
module Extensions = 
    type private TextureRange(aspect : TextureAspect, tex : IBackendTexture, levels : Range1i, slices : Range1i) =
        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = levels
            member x.Slices = slices

    type private TextureLevel(aspect : TextureAspect, tex : IBackendTexture, level : int, slices : Range1i) =
        member x.Size = 
            let v = tex.Size / (1 <<< level)
            V3i(max 1 v.X, max 1 v.Y, max 1 v.Z)

        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = Range1i(level, level)
            member x.Slices = slices

        interface IFramebufferOutput with
            member x.Size = x.Size.XY
            member x.Format = unbox (int tex.Format)
            member x.Samples = tex.Samples

        interface ITextureLevel with
            member x.Level = level
            member x.Size = 
                let v = tex.Size / (1 <<< level)
                V3i(max 1 v.X, max 1 v.Y, max 1 v.Z)

    type private TextureSlice(aspect : TextureAspect, tex : IBackendTexture, levels : Range1i, slice : int) =
        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = levels
            member x.Slices = Range1i(slice, slice)

        interface ITextureSlice with
            member x.Slice = slice

    type private SubTexture(aspect : TextureAspect, tex : IBackendTexture, level : int, slice : int) =

        member x.Size = 
            let v = tex.Size / (1 <<< level)
            V3i(max 1 v.X, max 1 v.Y, max 1 v.Z)

        interface ITextureRange with
            member x.Texture = tex
            member x.Aspect = aspect
            member x.Levels = Range1i(level, level)
            member x.Slices = Range1i(slice, slice)

        interface ITextureSubResource with
            member x.Slice = slice
            member x.Level = level
            member x.Size = x.Size

        interface IFramebufferOutput with
            member x.Format = unbox (int tex.Format)
            member x.Samples = tex.Samples
            member x.Size = x.Size.XY

    type Range1i with
        member x.SubRange(min : Option<int>, max : Option<int>) =
            let cnt = 1 + x.Max - x.Min
            let min = defaultArg min 0
            let max = defaultArg max (cnt - 1)
            Range1i(x.Min + min, x.Min + max)

    type IBackendTexture with
        
        member x.GetSlice(aspect : TextureAspect, minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = Range1i(defaultArg minLevel 0, defaultArg maxLevel (x.MipMapLevels - 1))
            let slice = Range1i(defaultArg minSlice 0, defaultArg maxSlice (x.Count - 1))
            TextureRange(aspect, x, level, slice) :> ITextureRange

        member x.GetSlice(aspect : TextureAspect, minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            let level = Range1i(defaultArg minLevel 0, defaultArg maxLevel (x.MipMapLevels - 1))
            TextureSlice(aspect, x, level, slice) :> ITextureSlice

        member x.GetSlice(aspect : TextureAspect, level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            let slice = Range1i(defaultArg minSlice 0, defaultArg maxSlice (x.Count - 1))
            TextureLevel(aspect, x, level, slice) :> ITextureLevel

        member x.Item
            with get(aspect : TextureAspect, level : int, slice : int) = SubTexture(aspect, x, level, slice) :> ITextureSubResource

        member x.Item
            with get(aspect : TextureAspect, level : int) = TextureLevel(aspect, x, level, Range1i(0, x.Count - 1)) :> ITextureLevel

        member x.Item
            with get(aspect : TextureAspect) = TextureRange(aspect, x, Range1i(0, x.MipMapLevels - 1), Range1i(0, x.Count - 1)) :> ITextureRange

    type ITextureRange with
        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = x.Levels.SubRange(minLevel, maxLevel)
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureRange(x.Aspect, x.Texture, level, slice) :> ITextureRange
            
        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>, slice : int) =
            let level = x.Levels.SubRange(minLevel, maxLevel)
            let slice = x.Slices.Min + slice
            TextureSlice(x.Aspect, x.Texture, level, slice) :> ITextureSlice    

        member x.GetSlice(level : int, minSlice : Option<int>, maxSlice : Option<int>) =
            let level = x.Levels.Min + level
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureLevel(x.Aspect, x.Texture, level, slice) :> ITextureLevel
            
        member x.Item
            with get(level : int, slice : int) = SubTexture(x.Aspect, x.Texture, x.Levels.Min + level, x.Slices.Min + slice) :> ITextureSubResource

        member x.Item
            with get(level : int) = TextureLevel(x.Aspect, x.Texture, x.Levels.Min + level, x.Slices) :> ITextureLevel
 
    type ITextureLevel with
        member x.GetSlice(minSlice : Option<int>, maxSlice : Option<int>) =
            let slice = x.Slices.SubRange(minSlice, maxSlice)
            TextureLevel(x.Aspect, x.Texture, x.Level, slice) :> ITextureRange
            
        member x.Item
            with get(slice : int) = SubTexture(x.Aspect, x.Texture,x.Level, x.Slices.Min + slice) :> ITextureSubResource

    type ITextureSlice with
        member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>) =
            let levels = x.Levels.SubRange(minLevel, maxLevel)
            TextureSlice(x.Aspect, x.Texture, levels, x.Slice) :> ITextureRange
            
        member x.Item
            with get(level : int) = SubTexture(x.Aspect, x.Texture, x.Levels.Min + level, x.Slice) :> ITextureSubResource
          
[<AutoOpen>]
module private TextureRanges =

    let test (t : IBackendTexture) =
        
        let a = t.[Color,*,*]
        let a = t.[Color]

        let a = t.[Color, 1, *]
        let a = t.[Color, *, 1]
        let a = t.[Color, 3, 1]
        
        
        let b = a.[*,*]


        ()



type BitmapTexture(bmp : System.Drawing.Bitmap, textureParams : TextureParams) =
    [<Obsolete("use texture params instead")>]
    member x.WantMipMaps = textureParams.wantMipMaps
    member x.TextureParams = textureParams
    member x.Bitmap = bmp
    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(bmp.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
            | :? BitmapTexture as o ->
                bmp = o.Bitmap && textureParams = o.TextureParams
            | _ ->
                false
    new(bmp : System.Drawing.Bitmap, wantMipMaps : bool) = 
        BitmapTexture(bmp, { TextureParams.empty with wantMipMaps = wantMipMaps}  )

type FileTexture(fileName : string, textureParams : TextureParams) =
    do if System.IO.File.Exists fileName |> not then failwithf "File does not exist: %s" fileName

    member x.FileName = fileName
    [<Obsolete("use texture params instead")>]
    member x.WantMipMaps = textureParams.wantMipMaps
    member x.TextureParams = textureParams
    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(fileName.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
            | :? FileTexture as o ->
                fileName = o.FileName && textureParams = o.TextureParams
            | _ ->
                false

    new(fileName : string, wantMipMaps : bool) = 
        FileTexture(fileName, { TextureParams.compressed with wantMipMaps = wantMipMaps}  )

type NullTexture() =
    interface ITexture with 
        member x.WantMipMaps = false
    override x.GetHashCode() = 0
    override x.Equals o =
        match o with
            | :? NullTexture -> true
            | _ -> false

type PixTexture2d(data : PixImageMipMap, textureParams : TextureParams) =
    member x.PixImageMipMap = data
    [<Obsolete("use texture params instead")>]
    member x.WantMipMaps = textureParams.wantMipMaps
    member x.TextureParams = textureParams
    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
            | :? PixTexture2d as o ->
                data = o.PixImageMipMap && textureParams = o.TextureParams
            | _ ->
                false

    new(data : PixImageMipMap, wantMipMaps : bool) = 
        PixTexture2d(data, { TextureParams.empty with wantMipMaps = wantMipMaps}  )

type PixTextureCube(data : PixImageCube,  textureParams : TextureParams) =
    member x.PixImageCube = data
    [<Obsolete("use texture params instead")>]
    member x.WantMipMaps = textureParams.wantMipMaps
    member x.TextureParams = textureParams
    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
            | :? PixTextureCube as o ->
                data = o.PixImageCube && textureParams = o.TextureParams
            | _ ->
                false
    new(data : PixImageCube, wantMipMaps : bool) = 
        PixTextureCube(data, { TextureParams.empty with wantMipMaps = wantMipMaps}  )

type PixTexture3d(data : PixVolume, textureParams : TextureParams) =
    member x.PixVolume = data
    [<Obsolete("use texture params instead")>]
    member x.WantMipMaps = textureParams.wantMipMaps
    member x.TextureParams = textureParams
    interface ITexture with
        member x.WantMipMaps = textureParams.wantMipMaps

    override x.GetHashCode() =
        HashCode.Combine(data.GetHashCode(), textureParams.GetHashCode())

    override x.Equals o =
        match o with
            | :? PixTexture3d as o ->
                data = o.PixVolume && textureParams = o.TextureParams
            | _ ->
                false
    new(data : PixVolume, wantMipMaps : bool) = 
        PixTexture3d(data, { TextureParams.empty with wantMipMaps = wantMipMaps}  )

module DefaultTextures =

    let checkerboardPix = 
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 16L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                C4b.Gray
        ) |> ignore
        pi

    let checkerboard = 
        PixTexture2d(PixImageMipMap [| checkerboardPix :> PixImage |], true) :> ITexture |> Mod.constant