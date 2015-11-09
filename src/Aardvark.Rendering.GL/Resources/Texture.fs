namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

type Texture =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Dimension : TextureDimension
        val mutable public Multisamples : int
        val mutable public Size : V3i
        val mutable public Count : int
        val mutable public Format : TextureFormat
        val mutable public MipMapLevels : int

        member x.IsMultisampled = x.Multisamples > 1
        member x.IsArray = x.Count > 1

        member x.Size1D = x.Size.X
        member x.Size2D = x.Size.XY
        member x.Size3D = x.Size

        interface IResource with
            member x.Context = x.Context
            member x.Handle = x.Handle

        interface IBackendTexture with
            member x.WantMipMaps = x.MipMapLevels > 1
            member x.Handle = x.Handle :> obj
            member x.Size = x.Size.XY
            member x.Format = x.Format
            member x.Samples = x.Multisamples

        member x.GetSize (level : int)  =
            if level = 0 then x.Size2D
            else 
                let level = Fun.Clamp(level, 0, x.MipMapLevels-1)
                let factor = 1 <<< level
                x.Size2D / factor

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int, size : V3i, count : int, format : TextureFormat) =
            { Context = ctx; Handle = handle; Dimension = dimension; MipMapLevels = mipMapLevels; Multisamples = multisamples; Size = size; Count = count; Format = format }

    end


[<AutoOpen>]
module TextureExtensions =


    // PositiveX = 0,
    // NegativeX = 1,
    // PositiveY = 2,
    // NegativeY = 3,
    // PositiveZ = 4,
    // NegativeZ = 5,
    // cubeSides are sorted like in their implementation (making some things easier)
    let cubeSides =
        [|
            CubeSide.PositiveX, TextureTarget.TextureCubeMapPositiveX
            CubeSide.NegativeX, TextureTarget.TextureCubeMapNegativeX

            CubeSide.PositiveY, TextureTarget.TextureCubeMapPositiveY
            CubeSide.NegativeY, TextureTarget.TextureCubeMapNegativeY
                
            CubeSide.PositiveZ, TextureTarget.TextureCubeMapPositiveZ
            CubeSide.NegativeZ, TextureTarget.TextureCubeMapNegativeZ
        |]

    [<AutoOpen>]
    module private Patterns =
        let (|FileTexture|_|) (t : ITexture) =
            match t with
                | :? FileTexture as t -> Some(FileTexture(t.TextureParams, t.FileName))
                | _ -> None

        let (|PixTextureCube|_|) (t : ITexture) =
            match t with
                | :? PixTextureCube as t -> Some(PixTextureCube(t.TextureParams, t.PixImageCube))
                | _ -> None

        let (|PixTexture2D|_|) (t : ITexture) =
            match t with
                | :? PixTexture2d as t -> Some(t.TextureParams, t.PixImageMipMap)
                | _ -> None

        let (|PixTexture3D|_|) (t : ITexture) =
            match t with
                | :? PixTexture3d as t -> Some(PixTexture3D(t.TextureParams, t.PixVolume))
                | _ -> None

    let private lookupTable (l : list<'a * 'b>) =
        let d = Dictionary()
        for (k,v) in l do

            match d.TryGetValue k with
                | (true, vo) -> failwithf "duplicated lookup-entry: %A (%A vs %A)" k vo v
                | _ -> ()

            d.[k] <- v

        fun (key : 'a) ->
            match d.TryGetValue key with
                | (true, v) -> Some v
                | _ -> None

    let internal toPixelType =
        lookupTable [
            typeof<uint8>, PixelType.UnsignedByte
            typeof<int8>, PixelType.Byte
            typeof<uint16>, PixelType.UnsignedShort
            typeof<int16>, PixelType.Short
            typeof<uint32>, PixelType.UnsignedInt
            typeof<int32>, PixelType.Int
            typeof<float32>, PixelType.Float

        ]

    let internal toPixelFormat =
        lookupTable [
        
            Col.Format.Alpha, PixelFormat.Alpha
            Col.Format.BW, PixelFormat.Red
            Col.Format.Gray, PixelFormat.Luminance
            Col.Format.GrayAlpha, PixelFormat.LuminanceAlpha
            Col.Format.RGB, PixelFormat.Rgb
            Col.Format.BGR, PixelFormat.Bgr
            Col.Format.RGBA, PixelFormat.Rgba
            Col.Format.BGRA, PixelFormat.Bgra
            Col.Format.RGBP, PixelFormat.Rgba
            Col.Format.NormalUV, PixelFormat.Rg
        ]

    let private tryGetSizedInternalFormat = 
        lookupTable [
            TextureFormat.R16, SizedInternalFormat.R16
            TextureFormat.R16f, SizedInternalFormat.R16f
            TextureFormat.R16i, SizedInternalFormat.R16i
            TextureFormat.R16ui, SizedInternalFormat.R16ui
            TextureFormat.R32f, SizedInternalFormat.R32f
            TextureFormat.R32i, SizedInternalFormat.R32i
            TextureFormat.R32ui, SizedInternalFormat.R32ui
            TextureFormat.R8, SizedInternalFormat.R8
            TextureFormat.R8i, SizedInternalFormat.R8i
            TextureFormat.R8ui, SizedInternalFormat.R8ui
            TextureFormat.Rg16, SizedInternalFormat.Rg16
            TextureFormat.Rg16i, SizedInternalFormat.Rg16i
            TextureFormat.Rg16ui, SizedInternalFormat.Rg16ui
            TextureFormat.Rg32f, SizedInternalFormat.Rg32f
            TextureFormat.Rg32i, SizedInternalFormat.Rg32i
            TextureFormat.Rg32ui, SizedInternalFormat.Rg32ui
            TextureFormat.Rg8, SizedInternalFormat.Rg8
            TextureFormat.Rg8i, SizedInternalFormat.Rg8i
            TextureFormat.Rg8ui, SizedInternalFormat.Rg8ui
            TextureFormat.Rgba16, SizedInternalFormat.Rgba16
            TextureFormat.Rgba16f, SizedInternalFormat.Rgba16f
            TextureFormat.Rgba16i, SizedInternalFormat.Rgba16i
            TextureFormat.Rgba16ui, SizedInternalFormat.Rgba16ui
            TextureFormat.Rgba32f, SizedInternalFormat.Rgba32f
            TextureFormat.Rgba32i, SizedInternalFormat.Rgba32i
            TextureFormat.Rgba32ui, SizedInternalFormat.Rgba32ui
            TextureFormat.Rgba8, SizedInternalFormat.Rgba8
            TextureFormat.Rgba8i, SizedInternalFormat.Rgba8i
            TextureFormat.Rgba8ui, SizedInternalFormat.Rgba8ui
        ]

    let private toSizedInternalFormat (fmt : TextureFormat) =
        match tryGetSizedInternalFormat fmt with
            | Some v -> v
            | _ -> failwithf "cannot get SizedInternalFormat for: %A" fmt


    [<AutoOpen>]
    module private Uploads =

        let private withAlignedPixImageContent (packAlign : int) (img : PixImage) (f : nativeint -> 'a) : 'a =
            let image = img.ToCanonicalDenseLayout()

            let lineSize = image.Size.X * image.PixFormat.ChannelCount * image.PixFormat.Type.GLSize
            let gc = GCHandle.Alloc(image.Data, GCHandleType.Pinned)

            let result = 
                if lineSize % packAlign <> 0 then
                    let adjustedLineSize = lineSize + (packAlign - lineSize % packAlign)

                    let data = Marshal.AllocHGlobal(image.Size.Y * adjustedLineSize)
                    let mutable src = gc.AddrOfPinnedObject()
                    let mutable aligned = data

                    for line in 0..image.Size.Y-1 do
                        Marshal.Copy(src, aligned, adjustedLineSize)
                        src <- src + nativeint lineSize
                        aligned <- aligned + nativeint adjustedLineSize
                    
                    try
                        f(data)
                    finally 
                        Marshal.FreeHGlobal(data)

                else
                    f(gc.AddrOfPinnedObject())

            gc.Free()

            result


        let private uploadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (textureParams : TextureParams) (data : PixImageMipMap) =
            if data.LevelCount <= 0 then
                failwith "cannot upload texture having 0 levels"

            let size = data.[0].Size
            let expectedLevels = Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let uploadLevels = if textureParams.wantMipMaps then data.LevelCount else 1
            let generateMipMap = textureParams.wantMipMaps && data.LevelCount < expectedLevels
            // TODO: think about texture format here
            let newFormat = TextureFormat.ofPixFormat data.[0].PixFormat textureParams
            let formatChanged = t.Format <> newFormat
            t.Format <- newFormat

            let internalFormat = TextureFormat.ofPixFormat data.[0].PixFormat textureParams |> unbox<PixelInternalFormat>
            let sizeChanged = size <> t.Size2D

            GL.BindTexture(target, t.Handle)
            GL.Check "could not bind texture"

            for l in 0..uploadLevels-1 do
                let level = data.[0]
                //let level = level.ToPixImage(Col.Format.RGBA)

                // determine the input format and covert the image
                // to a supported format if necessary.
                let pixelType, pixelFormat, image =
                    match toPixelType level.PixFormat.Type, toPixelFormat level.Format with
                        | Some t, Some f -> (t,f, level)
                        | _ ->
                            failwith "conversion not implemented"

                // since OpenGL cannot upload image-regions we
                // need to ensure that the image has a canonical layout. 
                // TODO: Check id this is no "real" copy when already canonical
                let image = image.ToCanonicalDenseLayout()


                let lineSize = image.Size.X * image.PixFormat.ChannelCount * image.PixFormat.Type.GLSize
                let packAlign = t.Context.PackAlignment

                withAlignedPixImageContent packAlign image (fun ptr ->
                    if sizeChanged || formatChanged then
                        GL.TexImage2D(target, l, internalFormat, image.Size.X, image.Size.Y, 0, pixelFormat, pixelType, ptr)
                    else
                        GL.TexSubImage2D(target, l, 0, 0, image.Size.X, image.Size.Y, pixelFormat, pixelType, ptr)
                    GL.Check (sprintf "could not upload texture data for level %d" l)
                )


            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap && isTopLevel then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(target, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here
            if isTopLevel then
                t.Size <- V3i(size.X, size.Y, 0)
                t.Multisamples <- 1
                t.Count <- 1
                t.Dimension <- TextureDimension.Texture2D
                //t.ChannelType <- ChannelType.fromGlFormat internalFormat

            generateMipMap

        let uploadTexture2DBitmap (t : Texture) (mipMaps : bool) (bmp : BitmapTexture) =

            let size = V2i(bmp.Bitmap.Width, bmp.Bitmap.Height)
            let expectedLevels = Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let uploadLevels = 1
            let generateMipMap = mipMaps
            let internalFormat = PixelInternalFormat.CompressedRgba
            let sizeChanged = size <> t.Size2D

            GL.BindTexture(TextureTarget.Texture2D, t.Handle)
            GL.Check "could not bind texture"

            // determine the input format and covert the image
            // to a supported format if necessary.
            let pixelType, pixelFormat =
                PixelType.UnsignedByte, PixelFormat.Bgra

            bmp.Bitmap.RotateFlip(Drawing.RotateFlipType.RotateNoneFlipY)
            let locked = bmp.Bitmap.LockBits(Drawing.Rectangle(0,0,bmp.Bitmap.Width, bmp.Bitmap.Height), Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppArgb)
            // if the size did not change it is more efficient
            // to use glTexSubImage
            if sizeChanged then
                GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, size.X, size.Y, 0, pixelFormat, pixelType, locked.Scan0)
            else
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, size.X, size.Y, pixelFormat, pixelType, locked.Scan0)
            GL.Check (sprintf "could not upload texture data for level %d" 0)

            bmp.Bitmap.UnlockBits(locked)
            bmp.Bitmap.RotateFlip(Drawing.RotateFlipType.RotateNoneFlipY)

            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(TextureTarget.Texture2D, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here

            t.Size <- V3i(size.X, size.Y, 0)
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.Texture2D
            t.Format <- unbox internalFormat

        let uploadTexture2D (t : Texture) (textureParams : TextureParams) (data : PixImageMipMap) =
            uploadTexture2DInternal TextureTarget.Texture2D true t textureParams data |> ignore

        let uploadTextureCube (t : Texture) (textureParams : TextureParams) (data : PixImageCube) =
            for (s,_) in cubeSides do
                if data.[s].LevelCount <= 0 then
                    failwith "cannot upload texture having 0 levels"

            let mutable generateMipMaps = false
            let size = data.[CubeSide.NegativeX].[0].Size

            for (side, target) in cubeSides do
                let data = data.[side]
                let generate = uploadTexture2DInternal target false t textureParams data

                if generate && textureParams.wantMipMaps then
                    generateMipMaps <- true

            if generateMipMaps then
                GL.BindTexture(TextureTarget.TextureCubeMap, t.Handle)
                GL.Check "could not bind texture"

                GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap)
                GL.Check "failed to generate mipmaps"

                GL.BindTexture(TextureTarget.TextureCubeMap, 0)
                GL.Check "could not unbind texture"

            t.Size <- V3i(size.X, size.Y, 0)
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.TextureCube

        let uploadTexture3D (t : Texture) (textureParams : TextureParams) (data : PixVolume) =
            let size = data.Size
            let expectedLevels = Fun.Min(size.X, size.Y, size.Z) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let generateMipMap = textureParams.wantMipMaps
            let newFormat = TextureFormat.ofPixFormat data.PixFormat textureParams
            let formatChanged = t.Format <> newFormat
            let sizeChanged = size <> t.Size3D

            GL.BindTexture(TextureTarget.Texture3D, t.Handle)
            GL.Check "could not bind texture"

            // determine the input format and covert the image
            // to a supported format if necessary.
            let pixelType, pixelFormat, image =
                match toPixelType data.PixFormat.Type, toPixelFormat data.Format with
                    | Some t, Some f -> (t,f, data)
                    | _ ->
                        failwith "conversion not implemented"

            // since OpenGL cannot upload image-regions we
            // need to ensure that the image has a canonical layout. 
            // TODO: Check id this is no "real" copy when already canonical
            let image = image.CopyToPixVolumeWithCanonicalDenseLayout()


            let gc = GCHandle.Alloc(image.Array, GCHandleType.Pinned)

            // if the size did not change it is more efficient
            // to use glTexSubImage
            if sizeChanged || formatChanged then
                GL.TexImage3D(TextureTarget.Texture3D, 0, unbox newFormat, size.X, size.Y, size.Z, 0, pixelFormat, pixelType, gc.AddrOfPinnedObject())
            else
                GL.TexSubImage3D(TextureTarget.Texture3D, 0, 0, 0, 0, size.X, size.Y, size.Z, pixelFormat, pixelType, gc.AddrOfPinnedObject())
            GL.Check "could not upload texture data"

            gc.Free()

            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture3D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(TextureTarget.Texture3D, 0)
            GL.Check "could not bind texture"

            // since some attributes of the texture
            // may have changed we mutate them here
            t.Size <- size
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.Texture3D
            t.Format <- unbox newFormat

        let downloadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (level : int) (image : PixImage) =
            if level <> 0 then
                failwith "downloads of mipmap-levels currently not implemented"

            let format =  image.PixFormat
            let levelSize = t.Size2D

            GL.BindTexture(target, t.Handle)
            GL.Check "could not bind texture"

            let pixelType, pixelFormat =
                match toPixelType format.Type, toPixelFormat format.Format with
                    | Some t, Some f -> (t,f)
                    | _ ->
                        failwith "conversion not implemented"

            let gc = GCHandle.Alloc(image.Data, GCHandleType.Pinned)

            OpenTK.Graphics.OpenGL4.GL.GetTexImage(target, level, pixelFormat, pixelType, gc.AddrOfPinnedObject())
            GL.Check "could not download image"

            gc.Free()
            

        let downloadTexture2D (t : Texture) (level : int) (image : PixImage) =
            downloadTexture2DInternal TextureTarget.Texture2D true t level image

        let downloadTextureCube (t : Texture) (level : int) (side : CubeSide) (image : PixImage) =
            let target = cubeSides.[int side] |> snd
            downloadTexture2DInternal target false t level image

    type Context with
        member x.CreateTexture1D(size : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i(size,0,0), 1, t)
                x.UpdateTexture1D(tex, size, mipMapLevels, t)

                tex
            )

        member x.CreateTexture2D(size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"
                
                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i(size.X,size.Y,0), 1, t)

                x.UpdateTexture2D(tex, size, mipMapLevels, t, samples)

                tex
            )

        member x.CreateTexture3D(size : V3i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                let tex = Texture(x, h, TextureDimension.Texture3D, mipMapLevels, 1, size, 1, t)
                x.UpdateTexture3D(tex, size, mipMapLevels, t, samples)

                tex
            )

        member x.CreateTextureCube(size : V2i, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                let tex = Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size.X, size.Y, 0), 1, t)
                x.UpdateTextureCube(tex, size, mipMapLevels, t)

                tex
            )

        member x.UpdateTexture1D(tex : Texture, size : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                GL.BindTexture(TextureTarget.Texture1D, tex.Handle)
                GL.Check "could not bind texture"

                match tryGetSizedInternalFormat t with
                    | Some ifmt -> 
                        GL.TexStorage1D(TextureTarget1d.Texture1D, mipMapLevels, ifmt, size)
                    | _ ->
                        GL.TexImage1D(TextureTarget.Texture1D, 0, unbox t, size, 0, PixelFormat.Red, PixelType.Byte, 0n)

                GL.Check "could allocate texture"

                GL.BindTexture(TextureTarget.Texture1D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture1D
                tex.Size <- V3i(size, 0, 0)
                tex.Format <- t
            )

        member x.UpdateTexture2D(tex : Texture, size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                GL.BindTexture(TextureTarget.Texture2D, tex.Handle)
                GL.Check "could not bind texture"

                match tryGetSizedInternalFormat t with
                    | Some ifmt ->
                        if samples = 1 then
                            GL.TexStorage2D(TextureTarget2d.Texture2D, mipMapLevels, ifmt, size.X, size.Y)
                        else
                            if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                            GL.TexStorage2DMultisample(TextureTargetMultisample2d.Texture2DMultisample, samples, ifmt, size.X, size.Y, false)
                
                    | None ->
                        if samples = 1 then
                            GL.TexImage2D(TextureTarget.Texture2D, 0, unbox t, size.X, size.Y, 0, PixelFormat.DepthComponent, PixelType.Byte, 0n)
                        else
                            GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, samples, unbox t, size.X, size.Y, false)
                
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.DepthTextureMode, int All.Intensity)
         
                GL.Check "could allocate texture"

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, mipMapLevels)
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0)


                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture2D
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.Format <- t
            )

        member x.UpdateTexture3D(tex : Texture, size : V3i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                GL.BindTexture(TextureTarget.Texture3D, tex.Handle)
                GL.Check "could not bind texture"

                let ifmt = toSizedInternalFormat t
                if samples = 1 then
                    GL.TexStorage3D(TextureTarget3d.Texture3D, mipMapLevels, ifmt, size.X, size.Y, size.Z)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage3DMultisample(TextureTargetMultisample3d.Texture2DMultisampleArray, samples, ifmt, size.X, size.Y, size.Z, false)
                GL.Check "could allocate texture"


                GL.BindTexture(TextureTarget.Texture3D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture3D
                tex.Size <- size
                tex.Format <- t
            )

        member x.UpdateTextureCube(tex : Texture, size : V2i, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                for (_,target) in cubeSides do
                    GL.BindTexture(target, tex.Handle)
                    GL.Check "could not bind texture"

                    let target2d = target |> int |> unbox<TextureTarget2d>
                    let ifmt = toSizedInternalFormat t
                    GL.TexStorage2D(target2d, mipMapLevels, ifmt, size.X, size.Y)
                    GL.Check "could allocate texture"

                    GL.BindTexture(TextureTarget.Texture2D, 0)
                    GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.TextureCube
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.Format <- t
            )



        member x.CreateTexture(data : ITexture) =
            using x.ResourceLock (fun _ ->
                let newTexture () = // not all cases need new textures
                    let h = GL.GenTexture()
                    GL.Check "could not create texture"
                    Texture(x, h, TextureDimension.Texture2D, 1, 1, V3i(-1,-1,-1), 1, TextureFormat.Rgba8)

                match data with

                    | :? BitmapTexture as bmp ->
                        let t = newTexture ()
                        uploadTexture2DBitmap t true bmp
                        t

                    | FileTexture(info, file) ->
                        let t = newTexture ()
                        if file = null then 
                            t
                        else
                            let pi = PixImage.Create(file, PixLoadOptions.UseDevil)
                            let mm = PixImageMipMap [|pi|]
                            uploadTexture2D t info mm |> ignore
                            t

                    | PixTexture2D(wantMipMaps, data) -> 
                        let t = newTexture ()
                        uploadTexture2D t wantMipMaps data |> ignore
                        t

                    | PixTextureCube(info, data) ->
                        let t = newTexture () 
                        uploadTextureCube t info data
                        t

                    | PixTexture3D(info, data) ->
                        let t = newTexture ()
                        uploadTexture3D t info data
                        t

                    | :? NullTexture ->
                        Texture(x, 0, TextureDimension.Texture2D, 1, 1, V3i(-1,-1,-1), 1, TextureFormat.Rgba8)

                    | :? Texture as o ->
                        o

                    | _ ->
                        failwith "unsupported texture data"

            )

        member x.Upload(t : Texture, data : ITexture) =
            using x.ResourceLock (fun _ ->
                match data with
                    | :? BitmapTexture as bmp ->
                        uploadTexture2DBitmap t true bmp

                    | PixTexture2D(wantMipMaps, data) -> 
                        uploadTexture2D t wantMipMaps data |> ignore

                    | PixTextureCube(info, data) -> 
                        uploadTextureCube t info data

                    | PixTexture3D(info, image) ->
                        uploadTexture3D t info image

                    | FileTexture(info, file) ->
                        let pi = PixImage.Create(file, PixLoadOptions.UseDevil)
                        let mm = PixImageMipMap [|pi|]
                        uploadTexture2D t info mm |> ignore

                    | :? NullTexture -> failwith "cannot update texture with null texture"

                    | :? Texture as o ->
                        if t.Handle <> o.Handle then
                            failwith "cannot upload to framebuffer-texture"

                    | _ ->
                        failwith "unsupported texture data"
            )

        member x.Download(t : Texture, level : int, slice : int, target : PixImage) =
            using x.ResourceLock (fun _ ->
                match t.Dimension with
                    | TextureDimension.Texture2D -> 
                        downloadTexture2D t level target

                    | TextureDimension.TextureCube ->
                        downloadTextureCube t level (unbox slice) target

                    | _ ->  
                        failwithf "cannot download textures of kind: %A" t.Dimension
            )
            
        member x.Delete(t : Texture) =
            using x.ResourceLock (fun _ ->
                GL.DeleteTexture(t.Handle)
                GL.Check "could not delete texture"
            )
            
    module ExecutionContext =

        let private getTextureTarget (texture : Texture) =
            match texture.Dimension, texture.IsArray, texture.IsMultisampled with

                | TextureDimension.Texture1D,      _,       true     -> failwith "Texture1D cannot be multisampled"
                | TextureDimension.Texture1D,      true,    _        -> TextureTarget.Texture1DArray
                | TextureDimension.Texture1D,      false,   _        -> TextureTarget.Texture1D
                                                   
                | TextureDimension.Texture2D,      false,   false    -> TextureTarget.Texture2D
                | TextureDimension.Texture2D,      true,    false    -> TextureTarget.Texture2DArray
                | TextureDimension.Texture2D,      false,   true     -> TextureTarget.Texture2DMultisample
                | TextureDimension.Texture2D,      true,    true     -> TextureTarget.Texture2DMultisampleArray
                                                   
                | TextureDimension.Texture3D,      false,   false    -> TextureTarget.Texture3D
                | TextureDimension.Texture3D,      _,       _        -> failwith "Texture3D cannot be multisampled or an array"
                                                  
                | TextureDimension.TextureCube,   false,    false    -> TextureTarget.TextureCubeMap
                | TextureDimension.TextureCube,   true,     false    -> TextureTarget.TextureCubeMapArray
                | TextureDimension.TextureCube,   _,        true     -> failwith "TextureCube cannot be multisampled"

                | _ -> failwithf "unknown texture dimension: %A" texture.Dimension

        let bindTexture (unit : int) (texture : Texture) =
            seq {
                yield Instruction.ActiveTexture(int TextureUnit.Texture0 + unit)
                
                let target = getTextureTarget texture
                yield Instruction.BindTexture (int target) texture.Handle
            }            

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Texture =

    let empty =
        Texture(null,0,TextureDimension.Texture2D,0,0,V3i.Zero,0,TextureFormat.Rgba8)

    let create1D (c : Context) (size : int) (mipLevels : int) (format : TextureFormat) =
        c.CreateTexture1D(size, mipLevels, format)

    let create2D (c : Context) (size : V2i) (mipLevels : int) (format : TextureFormat) (samples : int) =
        c.CreateTexture2D(size, mipLevels, format, samples)

    let createCube (c : Context) (size : V2i) (mipLevels : int) (format : TextureFormat) =
        c.CreateTextureCube(size, mipLevels, format)

    let create3D (c : Context) (size : V3i) (mipLevels : int) (format : TextureFormat) (samples : int) =
        c.CreateTexture3D(size, mipLevels, format, samples)

    let delete (tex : Texture) =
        tex.Context.Delete(tex)

    let write (data : ITexture) (tex : Texture) =
        tex.Context.Upload(tex, data)

    let read (format : PixFormat) (level : int) (tex : Texture) : PixImage[] =
        let size = V2i(max 1 (tex.Size.X / (1 <<< level)), max 1 (tex.Size.Y / (1 <<< level)))

        let pi = PixImage.Create(format, int64 size.Y, int64 size.Y)
        tex.Context.Download(tex, level, 0, pi)
        [|pi|]