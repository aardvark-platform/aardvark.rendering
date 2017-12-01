namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices

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
    abstract member Dimension : TextureDimension
    abstract member Format : TextureFormat
    abstract member Samples : int
    abstract member Count : int
    abstract member MipMapLevels : int
    abstract member Size : V3i
    abstract member Handle : obj

type IRenderbuffer =
    inherit IFramebufferOutput
    abstract member Handle : obj


[<AutoOpen>]
module private TextureRanges =
    
    type ITextureRange =
        abstract member Texture : IBackendTexture
        abstract member Aspect : TextureAspect
        abstract member Levels : Range1i
        abstract member Slices : Range1i

    type ITextureSlice =
        inherit ITextureRange
        abstract member Slice : int

    type ITextureLevel =
        inherit ITextureRange
        abstract member Level : int
        abstract member Size : V3i
        
    type ISubTexture =
        inherit ITextureSlice
        inherit ITextureLevel

    [<AutoOpen>]
    module Extensions = 
        type private TextureRange(aspect : TextureAspect, tex : IBackendTexture, levels : Range1i, slices : Range1i) =
            interface ITextureRange with
                member x.Texture = tex
                member x.Aspect = aspect
                member x.Levels = levels
                member x.Slices = slices

        type private TextureLevel(aspect : TextureAspect, tex : IBackendTexture, level : int, slices : Range1i) =
            interface ITextureRange with
                member x.Texture = tex
                member x.Aspect = aspect
                member x.Levels = Range1i(level, level)
                member x.Slices = slices

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
            interface ITextureRange with
                member x.Texture = tex
                member x.Aspect = aspect
                member x.Levels = Range1i(level, level)
                member x.Slices = Range1i(slice, slice)

            interface ISubTexture with
                member x.Slice = slice
                member x.Level = level
                member x.Size = 
                    let v = tex.Size / (1 <<< level)
                    V3i(max 1 v.X, max 1 v.Y, max 1 v.Z)


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
                with get(aspect : TextureAspect, level : int, slice : int) = SubTexture(aspect, x, level, slice) :> ISubTexture

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
                with get(level : int, slice : int) = SubTexture(x.Aspect, x.Texture, x.Levels.Min + level, x.Slices.Min + slice) :> ISubTexture

            member x.Item
                with get(level : int) = TextureLevel(x.Aspect, x.Texture, x.Levels.Min + level, x.Slices) :> ITextureLevel
 
        type ITextureLevel with
            member x.GetSlice(minSlice : Option<int>, maxSlice : Option<int>) =
                let slice = x.Slices.SubRange(minSlice, maxSlice)
                TextureLevel(x.Aspect, x.Texture, x.Level, slice) :> ITextureRange
            
            member x.Item
                with get(slice : int) = SubTexture(x.Aspect, x.Texture,x.Level, x.Slices.Min + slice) :> ISubTexture

        type ITextureSlice with
            member x.GetSlice(minLevel : Option<int>, maxLevel : Option<int>) =
                let levels = x.Levels.SubRange(minLevel, maxLevel)
                TextureSlice(x.Aspect, x.Texture, levels, x.Slice) :> ITextureRange
            
            member x.Item
                with get(level : int) = SubTexture(x.Aspect, x.Texture, x.Levels.Min + level, x.Slice) :> ISubTexture
          
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