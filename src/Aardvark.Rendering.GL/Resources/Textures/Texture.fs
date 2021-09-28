namespace Aardvark.Rendering.GL

open System
open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

#nowarn "9"

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
        val mutable public SizeInBytes : int64
        val mutable public ImmutableFormat : bool
        val mutable public IsArray : bool

        member x.IsMultisampled = x.Multisamples > 1

        member x.Size1D = x.Size.X
        member x.Size2D = x.Size.XY
        member x.Size3D = x.Size

        interface IBackendTexture with
            member x.Runtime = x.Context.Runtime :> ITextureRuntime
            member x.WantMipMaps = x.MipMapLevels > 1
            member x.Dimension = x.Dimension
            member x.MipMapLevels = x.MipMapLevels
            member x.Handle = x.Handle :> obj
            member x.Size = x.Size
            member x.Count = x.Count
            member x.Format = x.Format
            member x.Samples = x.Multisamples

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int,
            size : V3i, count : Option<int>, format : TextureFormat, sizeInBytes : int64, immutable : bool) =
            let cnt, isArray =
                match count with
                    | Some cnt -> cnt, true
                    | None -> 1, false
            { Context = ctx; Handle = handle; Dimension = dimension; MipMapLevels = mipMapLevels; Multisamples = multisamples;
              Size = size; Count = cnt; IsArray = isArray; Format = format; SizeInBytes = sizeInBytes; ImmutableFormat = immutable }

    end

type TextureViewHandle(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int,
                       multisamples : int, size : V3i, count : Option<int>, format : TextureFormat) =
    inherit Texture(ctx, handle, dimension, mipMapLevels, multisamples, size, count, format, 0L, true)


[<AutoOpen>]
module internal TextureUtilitiesAndExtensions =

    module ResourceCounts =

        let addTexture (ctx:Context) size =
            Interlocked.Increment(&ctx.MemoryUsage.TextureCount) |> ignore
            Interlocked.Add(&ctx.MemoryUsage.TextureMemory,size) |> ignore

        let addTextureView (ctx:Context) =
            Interlocked.Increment(&ctx.MemoryUsage.TextureViewCount) |> ignore

        let removeTexture (ctx:Context) size =
            Interlocked.Decrement(&ctx.MemoryUsage.TextureCount)  |> ignore
            Interlocked.Add(&ctx.MemoryUsage.TextureMemory,-size) |> ignore

        let removeTextureView (ctx:Context) =
            Interlocked.Decrement(&ctx.MemoryUsage.TextureViewCount) |> ignore

        let updateTexture (ctx:Context) oldSize newSize =
            Interlocked.Add(&ctx.MemoryUsage.TextureMemory,newSize-oldSize) |> ignore

        let texSizeInBytes (size : V3i, t : TextureFormat, samples : int, levels : int) =
            let pixelCount = (int64 size.X) * (int64 size.Y) * (int64 size.Z) * (int64 samples)
            let mutable size = pixelCount * (int64 (InternalFormat.getSizeInBits (unbox (int t)))) / 8L
            let mutable temp = size
            for i in 1..levels-1 do
                temp <- temp >>> 2
                size <- size + temp
            size

    module TextureTarget =

        let ofTexture (texture : Texture) =
            TextureTarget.ofParameters texture.Dimension texture.IsArray texture.IsMultisampled


    module PixFormat =

        let toFormatAndType (internalFormat : TextureFormat) (pixFormat : PixFormat) =
            let isInteger = TextureFormat.isIntegerFormat internalFormat

            match PixelFormat.ofColFormat isInteger pixFormat.Format, PixelType.ofType pixFormat.Type with
            | Some f, Some t -> f, t
            | _ ->
                failwith "conversion not implemented"

[<AutoOpen>]
module TextureCreationExtensions =
    type Context with
        member private x.SetDefaultTextureParams(target : TextureTarget, mipMapLevels : int) =
            GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipMapLevels - 1)
            GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
            GL.TexParameter(target, TextureParameterName.TextureWrapS, int TextureWrapMode.ClampToEdge)
            GL.TexParameter(target, TextureParameterName.TextureWrapT, int TextureWrapMode.ClampToEdge)
            GL.TexParameter(target, TextureParameterName.TextureMinFilter, int TextureMinFilter.Linear)
            GL.TexParameter(target, TextureParameterName.TextureMagFilter, int TextureMagFilter.Linear)

        member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, slices : int, levels : int, samples : int) =
            let isArray = slices > 0

            match dim, isArray with
            | TextureDimension.Texture1D, false -> x.CreateTexture1D(size.X, levels, format)
            | TextureDimension.Texture1D, true  -> x.CreateTexture1DArray(size.X, slices, levels, format)
            | TextureDimension.Texture2D, false -> x.CreateTexture2D(size.XY, levels, format, samples)
            | TextureDimension.Texture2D, true  -> x.CreateTexture2DArray(size.XY, slices, levels, format, samples)
            | TextureDimension.Texture3D, false -> x.CreateTexture3D(size, levels, format)
            | TextureDimension.Texture3D, true  -> raise <| ArgumentException("3D textures cannot be arrayed")
            | TextureDimension.TextureCube, false -> x.CreateTextureCube(size.X, levels, format)
            | TextureDimension.TextureCube, true  -> x.CreateTextureCubeArray(size.X, slices, levels, format)
            | _ -> failwith "[GL] Invalid texture dimension"

        member x.CreateTexture(size : V3i, dim : TextureDimension, format : PixFormat, info : TextureParams, slices : int, samples : int) =
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
            x.CreateTexture(size, dim, format, slices, levels, samples)

        member x.CreateTexture1D(size : int, format : PixFormat, info : TextureParams) =
            x.CreateTexture(V3i(size, 1, 1), TextureDimension.Texture1D, format, info, 0, 1)

        member x.CreateTexture2D(size : V2i, format : PixFormat, info : TextureParams, samples : int) =
            x.CreateTexture(V3i(size, 1), TextureDimension.Texture2D, format, info, 0, samples)

        member x.CreateTexture3D(size : V3i, format : PixFormat, info : TextureParams) =
            x.CreateTexture(size, TextureDimension.Texture3D, format, info, 0, 1)

        member x.CreateTextureCube(size : int, format : PixFormat, info : TextureParams) =
            x.CreateTexture(V3i(size, 1, 1), TextureDimension.TextureCube, format, info, 0, 1)

        member x.CreateTexture1DArray(size : int, count : int, format : PixFormat, info : TextureParams) =
            x.CreateTexture(V3i(size, 1, 1), TextureDimension.Texture1D, format, info, count, 1)

        member x.CreateTexture2DArray(size : V2i, count : int, format : PixFormat, info : TextureParams, samples : int) =
            x.CreateTexture(V3i(size, 1), TextureDimension.Texture2D, format, info, count, samples)

        member x.CreateTextureCubeArray(size : int, count : int, format : PixFormat, info : TextureParams) =
            x.CreateTexture(V3i(size, 1, 1), TextureDimension.TextureCube, format, info, count, 1)

        member x.UpdateTexture(tex : Texture, size : V3i, dim : TextureDimension, format : TextureFormat, slices : int, levels : int, samples : int) =
            let isArray = slices > 0

            match dim, isArray with
            | TextureDimension.Texture1D, false -> x.UpdateTexture1D(tex, size.X, levels, format)
            | TextureDimension.Texture1D, true  -> x.UpdateTexture1DArray(tex, size.X, slices, levels, format)
            | TextureDimension.Texture2D, false -> x.UpdateTexture2D(tex, size.XY, levels, format, samples)
            | TextureDimension.Texture2D, true  -> x.UpdateTexture2DArray(tex, size.XY, slices, levels, format, samples)
            | TextureDimension.Texture3D, false -> x.UpdateTexture3D(tex, size, levels, format)
            | TextureDimension.Texture3D, true  -> raise <| ArgumentException("3D textures cannot be arrayed")
            | TextureDimension.TextureCube, false -> x.UpdateTextureCube(tex, size.X, levels, format)
            | TextureDimension.TextureCube, true  -> x.UpdateTextureCubeArray(tex, size.X, slices, levels, format)
            | _ -> failwith "[GL] Invalid texture dimension"


        member x.CreateTexture1D(size : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                ResourceCounts.addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i.Zero, None, format, 0L, false)
                x.UpdateTexture1D(tex, size, mipMapLevels, format)

                tex
            )

        member x.CreateTexture2D(size : V2i, mipMapLevels : int, format : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                ResourceCounts.addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i.Zero, None, format, 0L, false)

                x.UpdateTexture2D(tex, size, mipMapLevels, format, samples)

                tex
            )

        member x.CreateTexture3D(size : V3i, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                ResourceCounts.addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture3D, mipMapLevels, 1, V3i.Zero, None, format, 0L, false)
                x.UpdateTexture3D(tex, size, mipMapLevels, format)

                tex
            )

        member x.CreateTextureCube(size : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                ResourceCounts.addTexture x 0L
                let tex = Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size, size, 0), None, format, 0L, false)
                x.UpdateTextureCube(tex, size, mipMapLevels, format)

                tex
            )

        member x.CreateTexture1DArray(size : int, count : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                ResourceCounts.addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i.Zero, Some count, format, 0L, false)
                x.UpdateTexture1DArray(tex, size, count, mipMapLevels, format)

                tex
            )

        member x.CreateTexture2DArray(size : V2i, count : int, mipMapLevels : int, format : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                ResourceCounts.addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i.Zero, Some count, format, 0L, false)

                x.UpdateTexture2DArray(tex, size, count, mipMapLevels, format, samples)

                tex
            )

        member x.CreateTextureCubeArray(size : int, count : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                ResourceCounts.addTexture x 0L
                let tex = Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size, size, 0), Some count, format, 0L, false)
                x.UpdateTextureCubeArray(tex, size, count, mipMapLevels, format)

                tex
            )

        member x.UpdateTexture1D(tex : Texture, size : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture1D, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = ResourceCounts.texSizeInBytes(V3i(size, 1, 1), format, 1, mipMapLevels)
                ResourceCounts.updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                x.SetDefaultTextureParams(TextureTarget.Texture1D, mipMapLevels)

                GL.TexStorage1D(TextureTarget1d.Texture1D, mipMapLevels, unbox (int format), size)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture1D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture1D
                tex.Size <- V3i(size, 1, 1)
                tex.Format <- format
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture2D(tex : Texture, size : V2i, mipMapLevels : int, format : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                let target =
                    if samples = 1 then TextureTarget.Texture2D
                    else TextureTarget.Texture2DMultisample

                GL.BindTexture(target, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = ResourceCounts.texSizeInBytes(size.XYI, format, samples, mipMapLevels)
                ResourceCounts.updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                if samples = 1 then
                    // parameters only valid for non-multisampled textures
                    x.SetDefaultTextureParams(target, mipMapLevels)
                    GL.TexStorage2D(TextureTarget2d.Texture2D, mipMapLevels, unbox (int format), size.X, size.Y)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage2DMultisample(TextureTargetMultisample2d.Texture2DMultisample, samples, unbox (int format), size.X, size.Y, true)

                GL.Check "could not allocate texture"
                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture2D
                tex.Multisamples <- samples
                tex.Count <- 1
                tex.Size <- V3i(size.X, size.Y, 1)
                tex.Format <- format
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture3D(tex : Texture, size : V3i, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture3D, tex.Handle)
                GL.Check "could not bind texture"

                let ifmt = unbox (int format)

                let sizeInBytes = ResourceCounts.texSizeInBytes(size, format, 1, mipMapLevels)
                ResourceCounts.updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                x.SetDefaultTextureParams(TextureTarget.Texture3D, mipMapLevels)

                GL.TexStorage3D(TextureTarget3d.Texture3D, mipMapLevels, ifmt, size.X, size.Y, size.Z)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture3D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture3D
                tex.Count <- 1
                tex.Multisamples <- 1
                tex.Size <- size
                tex.Format <- format
                tex.ImmutableFormat <- true
            )

        member x.UpdateTextureCube(tex : Texture, size : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.TextureCubeMap, tex.Handle)
                GL.Check "could not bind texture"

                x.SetDefaultTextureParams(TextureTarget.TextureCubeMap, mipMapLevels)
                GL.TexStorage2D(TextureTarget2d.TextureCubeMap, mipMapLevels, unbox (int format), size, size)

                GL.BindTexture(TextureTarget.TextureCubeMap, 0)
                GL.Check "could not unbind texture"

                let sizeInBytes = ResourceCounts.texSizeInBytes(V3i(size, size, 1), format, 1, mipMapLevels) * 6L
                ResourceCounts.updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.TextureCube
                tex.Size <- V3i(size, size, 1)
                tex.Count <- 1
                tex.Format <- format
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture1DArray(tex : Texture, size : int, count : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture1DArray, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = ResourceCounts.texSizeInBytes(V3i(size, 1, 1), format, 1, mipMapLevels) * (int64 count)
                ResourceCounts.updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                x.SetDefaultTextureParams(TextureTarget.Texture1DArray, mipMapLevels)

                GL.TexStorage2D(TextureTarget2d.Texture1DArray, mipMapLevels, unbox (int format), size, count)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture1DArray, 0)
                GL.Check "could not unbind texture"

                tex.IsArray <- true
                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture1D
                tex.Count <- count
                tex.Multisamples <- 1
                tex.Size <- V3i(size, 1, 1)
                tex.Format <- format
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture2DArray(tex : Texture, size : V2i, count : int, mipMapLevels : int, format : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                let target =
                    if samples = 1 then TextureTarget.Texture2DArray
                    else TextureTarget.Texture2DMultisampleArray

                GL.BindTexture(target, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = ResourceCounts.texSizeInBytes(size.XYI, format, samples, mipMapLevels) * (int64 count)
                ResourceCounts.updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                if samples = 1 then
                    x.SetDefaultTextureParams(target, mipMapLevels)
                    GL.TexStorage3D(TextureTarget3d.Texture2DArray, mipMapLevels, unbox (int format), size.X, size.Y, count)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage3DMultisample(TextureTargetMultisample3d.Texture2DMultisampleArray, samples, unbox (int format), size.X, size.Y, count, true)

                GL.Check "could not allocate texture"

                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture2D
                tex.IsArray <- true
                tex.Count <- count
                tex.Multisamples <- samples
                tex.Size <- V3i(size.X, size.Y, 1)
                tex.Format <- format
                tex.ImmutableFormat <- true
            )

        member x.UpdateTextureCubeArray(tex : Texture, size : int, count : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                let target = TextureTarget.TextureCubeMapArray
                GL.BindTexture(target, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = ResourceCounts.texSizeInBytes(V3i(size, size, 1), format, 1, mipMapLevels) * 6L * (int64 count)
                ResourceCounts.updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                x.SetDefaultTextureParams(target, mipMapLevels)
                GL.TexStorage3D(unbox (int target), mipMapLevels, unbox (int format), size, size, count * 6)

                GL.Check "could not allocate texture"

                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.TextureCube
                tex.IsArray <- true
                tex.Count <- count
                tex.Size <- V3i(size, size, 1)
                tex.Format <- format
                tex.ImmutableFormat <- true
            )

        member x.CreateTextureView(orig : Texture, levels : Range1i, slices : Range1i, isArray : bool) =
            using x.ResourceLock (fun _ ->
                if not orig.ImmutableFormat then
                    failwithf "cannot create texture-views for mutable textures"

                let handle = GL.GenTexture()
                GL.Check "could not create texture"

                let dim =
                    match orig.Dimension with
                        | TextureDimension.TextureCube ->
                            if isArray || slices.Min = slices.Max then
                                // address TextureCube or TextureCubeArray as Texture2d or Texture2dArray
                                TextureDimension.Texture2D
                            else
                                // address certain levels or single cube of cubeArray
                                if slices.Max - slices.Min + 1 <> 6 then failwithf "Creating multi-slice view (sliceCount>1 && sliceCount<>6) of CubeTexture(Array) requires isArray=true"
                                TextureDimension.TextureCube
                        | d -> d

                let levelCount = 1 + levels.Max - levels.Min
                let sliceCountHandle = if isArray then Some (1 + slices.Max - slices.Min)  else None
                let sliceCountCreate =
                    // create array if requested -> allows to create single views of array texture and an array view of a single texture
                    if isArray || orig.Dimension = TextureDimension.TextureCube && slices.Min <> slices.Max then Some (1 + slices.Max - slices.Min)
                    else None

                let tex = TextureViewHandle(x, handle, dim, levelCount, orig.Multisamples, orig.Size, sliceCountHandle, orig.Format)
                let target = TextureTarget.ofTexture tex

                GL.TextureView(
                    handle,
                    target,
                    orig.Handle,
                    unbox (int orig.Format),
                    levels.Min, 1 + levels.Max - levels.Min,
                    slices.Min, match sliceCountCreate with | Some x -> x; | _ -> 1
                )
                GL.Check "could not create texture view"

                ResourceCounts.addTextureView x

                tex
            )

        member x.CreateTextureView(orig : Texture, levels : Range1i, slices : Range1i) =
            x.CreateTextureView(orig, levels, slices, orig.IsArray)

        member x.Delete(t : Texture) =
            using x.ResourceLock (fun _ ->
                match t with
                | :? TextureViewHandle -> ResourceCounts.removeTextureView x
                | _ -> ResourceCounts.removeTexture x t.SizeInBytes
                GL.DeleteTexture(t.Handle)
                GL.Check "could not delete texture"
                t.Handle <- 0
            )


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Texture =

    let empty =
        Texture(null,0,TextureDimension.Texture2D,0,0,V3i.Zero,None,TextureFormat.Rgba8,0L, false)

    let create1D (c : Context) (size : int) (mipLevels : int) (format : TextureFormat) =
        c.CreateTexture1D(size, mipLevels, format)

    let create2D (c : Context) (size : V2i) (mipLevels : int) (format : TextureFormat) (samples : int) =
        c.CreateTexture2D(size, mipLevels, format, samples)

    let createCube (c : Context) (size : int) (mipLevels : int) (format : TextureFormat) =
        c.CreateTextureCube(size, mipLevels, format)

    let create3D (c : Context) (size : V3i) (mipLevels : int) (format : TextureFormat)  =
        c.CreateTexture3D(size, mipLevels, format)

    let delete (tex : Texture) =
        tex.Context.Delete(tex)