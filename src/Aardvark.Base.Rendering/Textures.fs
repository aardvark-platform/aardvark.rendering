namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices

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


type TextureParams =
    {
        wantMipMaps : bool
        wantSrgb : bool
        wantCompressed : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextureParams =
    
    let empty = { wantMipMaps = false; wantSrgb = false; wantCompressed = false}
    let compressed = { wantMipMaps = false; wantSrgb = false; wantCompressed = true }
    let mipmapped = { wantMipMaps = true; wantSrgb = false; wantCompressed = false }
    let mipmappedCompressed = { wantMipMaps = true; wantSrgb = false; wantCompressed = true }

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