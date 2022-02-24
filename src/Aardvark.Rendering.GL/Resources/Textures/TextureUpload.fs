namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open System.Runtime.InteropServices
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices

#nowarn "9"

[<AutoOpen>]
module internal TextureUploadImplementation =

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Texture =

        let uploadPixelData (texture : Texture) (generateMipmap : bool) (level : int) (offset : V3i) (data : PixelData) =
            let target = texture |> TextureTarget.ofTexture
            let targetSlice = target |> TextureTarget.toSliceTarget offset.Z

            let info = data |> PixelData.getInfo texture.Context.UnpackAlignment

            GL.BindTexture(target, texture.Handle)
            GL.Check "could not bind texture"

            PixelBuffer.unpack info.SizeInBytes (fun pbo ->
                let pixels = pbo.Pixels

                pbo |> PixelBuffer.mapped BufferAccess.WriteOnly (fun dst ->
                    (info, data) ||> PixelData.copy dst
                )

                match texture.Dimension, texture.IsArray with
                | TextureDimension.Texture1D, false ->
                    match data with
                    | PixelData.General d ->
                        GL.TexSubImage1D(target, level, offset.X, d.Size.X, d.Format, d.Type, pixels)
                    | PixelData.Compressed d ->
                        GL.CompressedTexSubImage1D(target, level, offset.X, d.Size.X,
                                                   unbox texture.Format, int d.SizeInBytes, pixels)

                | TextureDimension.Texture1D, true
                | TextureDimension.Texture2D, false
                | TextureDimension.TextureCube, false ->
                    let offset =
                        if texture.Dimension = TextureDimension.Texture1D then offset.XZ
                        else offset.XY

                    match data with
                    | PixelData.General d ->
                        GL.TexSubImage2D(targetSlice, level, offset.X, offset.Y, d.Size.X, d.Size.Y, d.Format, d.Type, pixels)
                    | PixelData.Compressed d ->
                        GL.CompressedTexSubImage2D(targetSlice, level, offset.X, offset.Y, d.Size.X, d.Size.Y,
                                                   unbox texture.Format, int d.SizeInBytes, pixels)

                | TextureDimension.Texture2D, true
                | TextureDimension.Texture3D, false
                | TextureDimension.TextureCube, true ->
                    match data with
                    | PixelData.General d ->
                        GL.TexSubImage3D(target, level, offset.X, offset.Y, offset.Z, d.Size.X, d.Size.Y, d.Size.Z, d.Format, d.Type, pixels)
                    | PixelData.Compressed d ->
                        GL.CompressedTexSubImage3D(target, level, offset.X, offset.Y, offset.Z,
                                                   d.Size.X, d.Size.Y, d.Size.Z, unbox texture.Format, int d.SizeInBytes, pixels)

                | d, a ->
                    failwithf "[GL] unsupported texture data %A%s" d (if a then "[]" else "")

                GL.Check (sprintf "could not upload texture data")
            )

            if generateMipmap then
                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, level)
                GL.Check "could not set base mip map level"

                GL.GenerateMipmap(unbox target)
                GL.Check "failed to generate mipmaps"

                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
                GL.Check "could not reset base mip map level"

            GL.BindTexture(target, 0)
            GL.Check "could not unbind texture"

        let uploadNativeTensor4<'T when 'T : unmanaged> (texture : Texture) (generateMipmap : bool)
                                                        (level : int) (slice : int) (offset : V3i) (size : V3i)
                                                        (src : NativeTensor4<'T>) =

            let pixelFormat, pixelType =
                TextureFormat.toFormatAndType texture.Format

            let offset =
                let flipped = texture.WindowOffset(level, offset, size)
                if texture.Dimension = TextureDimension.Texture3D then flipped
                else V3i(flipped.XY, slice)

            let copy (channels : int) (elementSize : int) (alignedLineSize : nativeint) (dst : nativeint) =
                let srcInfo = src.Info.AsBytes<'T>()
                let dstInfo = Tensor4Info.deviceLayout texture.IsCubeOr2D elementSize alignedLineSize channels (V3l size)
                NativeTensor4.copyBytes<'T> src.Address srcInfo dst dstInfo

            let pixelData =
                PixelData.General {
                    Size   = size
                    Type   = pixelType
                    Format = pixelFormat
                    Copy   = copy
                }

            uploadPixelData texture generateMipmap level offset pixelData


        let uploadNativeTexture (texture : Texture) (baseLevel : int) (baseSlice : int) (data : INativeTexture) =
            let pixelFormat, pixelType = TextureFormat.toFormatAndType data.Format
            let compression = TextureFormat.compressionMode data.Format

            let levelCount =
                if data.WantMipMaps then data.MipMapLevels else 1

            let generateMipmap =
                data.WantMipMaps && levelCount < texture.MipMapLevels

            for slice = 0 to data.Count - 1 do
                let offset =
                    match texture.Dimension with
                    | TextureDimension.Texture3D -> V3i.Zero
                    | _ -> V3i(0, 0, baseSlice + slice)

                for level = 0 to levelCount - 1 do
                    let subdata = data.[slice, level]

                    let copy (dst : nativeint) =
                        subdata.Use (fun src ->
                            if compression <> CompressionMode.None && texture.IsCubeOr2D then
                                BlockCompression.mirrorCopy compression subdata.Size.XY src dst
                            else
                                Marshal.Copy(src, dst, subdata.SizeInBytes)
                        )

                    let pixelData =
                        if compression <> CompressionMode.None then
                            PixelData.Compressed {
                                Size        = subdata.Size
                                SizeInBytes = nativeint subdata.SizeInBytes
                                Copy        = copy
                            }

                        else
                            PixelData.General {
                                Size   = subdata.Size
                                Type   = pixelType
                                Format = pixelFormat
                                Copy   = fun _ _ _ -> copy
                            }

                    let generateMipmap = generateMipmap && slice = data.Count - 1 && level = levelCount - 1
                    uploadPixelData texture generateMipmap (baseLevel + level) offset pixelData


        let uploadPixImage (texture : Texture) (generateMipmap : bool)
                           (level : int) (slice : int) (offset : V2i) (image : PixImage) =
            image.Visit
                { new PixVisitors.PixImageVisitor() with
                    member x.VisitUnit(img : PixImage<'T>) =
                        NativeVolume.using img.Volume (fun src ->
                            let src = src.ToXYWTensor4'()
                            uploadNativeTensor4 texture generateMipmap level slice offset.XYO image.Size.XYI src
                        )
                } |> ignore


        let private uploadPixImageMipMapInternal (texture : Texture) (wantMipmap : bool) (generateMipmap : bool)
                                                 (baseLevel : int) (slice : int) (offset : V2i) (images : PixImageMipMap) =
            let levelCount =
                if wantMipmap then images.LevelCount else 1

            for i = 0 to levelCount - 1 do
                let img = images.ImageArray.[i]
                let mip = generateMipmap && i = levelCount - 1
                uploadPixImage texture mip (baseLevel + i) slice offset img


        let uploadPixImageMipMap (texture : Texture) (wantMipmap : bool)
                                 (baseLevel : int) (slice : int) (offset : V2i) (images : PixImageMipMap) =
            let generateMipmap =
                wantMipmap && images.LevelCount < texture.MipMapLevels

            uploadPixImageMipMapInternal texture wantMipmap generateMipmap baseLevel slice offset images


        let uploadPixImageCube (texture : Texture) (wantMipmap : bool)
                               (baseLevel : int) (slice : int) (offset : V2i) (data : PixImageCube) =
            let generateMipmap =
                wantMipmap && data.MipMapArray |> Array.forany (fun i -> i.LevelCount < texture.MipMapLevels)

            for i = 0 to 5 do
                let data = data.[unbox<CubeSide> i]
                let generateMipMap = generateMipmap && i = 5
                uploadPixImageMipMapInternal texture wantMipmap generateMipMap baseLevel (slice + i) offset data


        let uploadPixVolume (texture : Texture) (generateMipmap : bool)
                            (level : int) (offset : V3i) (volume : PixVolume) =
            volume.Visit
                { new PixVisitors.PixVolumeVisitor() with
                    member x.VisitUnit(img : PixVolume<'T>) =
                        NativeTensor4.using img.Tensor4 (fun src ->
                            uploadNativeTensor4 texture generateMipmap level 0 offset volume.Size src
                        )
                } |> ignore

[<AutoOpen>]
module ContextTextureUploadExtensions =

    module private Texture =

        let private createOfFormat (dimension : TextureDimension) (format : PixFormat)
                                   (size : V3i) (slices : int) (samples : int) (info : TextureParams) (context : Context) =
            let format =
                let baseFormat = TextureFormat.ofPixFormat format info
                if info.wantCompressed then
                    match TextureFormat.toCompressed baseFormat with
                    | Some fmt -> fmt
                    | _ ->
                        Log.warn "[GL] Texture format %A does not support compression" baseFormat
                        baseFormat
                else
                    baseFormat

            let levels = if info.wantMipMaps then Fun.MipmapLevels(size) else 1
            context.CreateTexture(size, dimension, format, slices, levels, samples)

        let createOfFormat2D (format : PixFormat) (size : V2i) (info : TextureParams) (context : Context) =
            let size = V3i(size, 1)
            context |> createOfFormat TextureDimension.Texture2D format size 0 1 info

        let createOfFormat3D (format : PixFormat) (size : V3i) (info : TextureParams) (context : Context) =
            context |> createOfFormat TextureDimension.Texture3D format size 0 1 info

        let createOfFormatCube (format : PixFormat) (size : int) (info : TextureParams) (context : Context) =
            let size = V3i(size, 1, 1)
            context |> createOfFormat TextureDimension.TextureCube format size 0 1 info


    [<AutoOpen>]
    module private Patterns =

        let (|StreamTexture|_|) (t : ITexture) =
            match t with
            | :? StreamTexture as t -> Some(StreamTexture(t.TextureParams, fun seek -> t.Open seek))
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


    let private useTemporaryAndBlit (dst : Texture) (level : int) (slice : int) (offset : V2i) (size : V2i) (f : Texture -> unit) =
        let context = dst.Context
        let temp = context.CreateTexture2D(size, 1, dst.Format, 1)
        try
            f temp
            context.Blit(temp, 0, 0, V2i.Zero, size, dst, level, slice, offset, size, false)
        finally
            context.Delete(temp)

    [<Extension; AbstractClass; Sealed>]
    type ContextTextureUploadExtensions =

        [<Extension>]
        static member CreateTexture(this : Context, data : ITexture) =
            using this.ResourceLock (fun _ ->
                match data with
                | StreamTexture(info, openStream) ->
                    use stream = openStream true
                    let initialPos = stream.Position

                    // Always try to load compressed data first
                    let compressed =
                        stream |> DdsTexture.tryLoadCompressedFromStream

                    match compressed with
                    | Some t -> this.CreateTexture(t)
                    | _ ->
                        stream.Position <- initialPos
                        let pi = PixImage.Create(stream)
                        let mm = PixImageMipMap [|pi|]
                        this.CreateTexture <| PixTexture2d(mm, info)

                | PixTexture2D(info, data) ->
                    let texture = this |> Texture.createOfFormat2D data.PixFormat data.[0].Size info
                    Texture.uploadPixImageMipMap texture info.wantMipMaps 0 0 V2i.Zero data
                    texture

                | PixTextureCube(info, data) ->
                    let img = data.[CubeSide.NegativeX]
                    let texture = this |> Texture.createOfFormatCube img.PixFormat img.[0].Size.X info
                    Texture.uploadPixImageCube texture info.wantMipMaps 0 0 V2i.Zero data
                    texture

                | PixTexture3D(info, data) ->
                    let texture = this |> Texture.createOfFormat3D data.PixFormat data.Size info
                    Texture.uploadPixVolume texture info.wantMipMaps 0 V3i.Zero data
                    texture

                | :? NullTexture ->
                    Texture.empty

                | :? Texture as o ->
                    o

                | :? INativeTexture as data ->
                    let slices = if data.Count > 1 then data.Count else 0
                    let levels = if data.WantMipMaps then Fun.MipmapLevels(data.[0, 0].Size) else 1
                    let texture = this.CreateTexture(data.[0, 0].Size, data.Dimension, data.Format, slices, levels, 1)
                    Texture.uploadNativeTexture texture 0 0 data
                    texture

                | _ ->
                    failwith "unsupported texture data"
            )

        [<Extension>]
        static member Upload(this : Context, texture : Texture, level : int, slice : int,
                             offset : V3i, size : V3i, source : NativeTensor4<'T>) =
            using this.ResourceLock (fun _ ->
                // Multisampled texture requires blit
                if texture.IsMultisampled then
                    useTemporaryAndBlit texture level slice offset.XY size.XY (fun temp ->
                        Texture.uploadNativeTensor4 temp false 0 0 V3i.Zero size source
                    )

                // Upload directly
                else
                    Texture.uploadNativeTensor4 texture false level slice offset size source
            )

        [<Extension>]
        static member Upload(this : Context, texture : Texture, level : int, slice : int, size : V3i, source : NativeTensor4<'T>) =
            this.Upload(texture, level, slice, V3i.Zero, size, source)

        [<Extension>]
        static member Upload(this : Context, texture : Texture, level : int, slice : int, offset : V2i, source : PixImage) =
            using this.ResourceLock (fun _ ->
                // Multisampled texture requires blit
                if texture.IsMultisampled then
                    useTemporaryAndBlit texture level slice offset source.Size (fun temp ->
                        Texture.uploadPixImage temp false 0 0 V2i.Zero source
                    )

                // Upload directly
                else
                    Texture.uploadPixImage texture false level slice offset source
            )

        [<Extension>]
        static member Upload(this : Context, texture : Texture, level : int, slice : int, source : PixImage) =
            this.Upload(texture, level, slice, V2i.Zero, source)

        [<Extension>]
        static member Upload(this : Context, t : Texture, level : int, source : PixImage) =
            this.Upload(t, level, 0, source)