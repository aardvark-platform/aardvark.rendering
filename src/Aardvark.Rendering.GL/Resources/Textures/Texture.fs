namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module internal TextureResourceCounts =

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

        /// Computes an estimate of the memory usage of a texture with the given parameters.
        /// Assumes that per-pixel size in bits is a power of two, so for example RGB textures are layed out as RGBX.
        let texSizeInBytes (dimension : TextureDimension) (size : V3i) (format : TextureFormat) (samples : int) (levels : int) (count : int) =
            let count =
                if dimension = TextureDimension.TextureCube then 6 * count
                else count

            let getLevelSizeInBytes : V3i -> int64 =
                match format.CompressionMode with
                | CompressionMode.None ->
                    let bitsPerPixel = format.PixelSizeInBits |> Fun.NextPowerOfTwo |> max 8 |> int64
                    fun size -> (int64 size.X) * (int64 size.Y) * (int64 size.Z) * (int64 samples) * (bitsPerPixel / 8L)

                | mode ->
                    fun size -> int64 <| CompressionMode.sizeInBytes size mode

            let mutable layerSizeInBytes = 0L

            for level = 0 to levels - 1 do
                let levelSize = Fun.MipmapLevelSize(size, level)
                layerSizeInBytes <- layerSizeInBytes + getLevelSizeInBytes levelSize

            layerSizeInBytes * (int64 count)

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
        val mutable public IsArray : bool
        val mutable private name : string

        abstract member Name : string with get, set
        default x.Name
            with get() = x.name
            and set name =
                if x.Context <> null then
                    x.name <- name
                    x.Context.SetObjectLabel(ObjectLabelIdentifier.Texture, x.Handle, name)

        member x.IsMultisampled = x.Multisamples > 1

        member x.Size1D = x.Size.X
        member x.Size2D = x.Size.XY
        member x.Size3D = x.Size

        abstract member Destroy : unit -> unit
        default x.Destroy() =
            GL.DeleteTexture x.Handle
            ResourceCounts.removeTexture x.Context x.SizeInBytes
            GL.Check "could not delete texture"

        member x.Dispose() =
            if x.Context <> null then // NullTexture has no context
                using x.Context.ResourceLock (fun _ ->
                    x.Destroy()
                    x.Handle <- 0
                )

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
            member x.Name with get() = x.Name and set name = x.Name <- name
            member x.Dispose() = x.Dispose()

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int,
            size : V3i, count : int, isArray : bool, format : TextureFormat, sizeInBytes : int64) =
            { Context = ctx
              Handle = handle
              Dimension = dimension
              MipMapLevels = mipMapLevels
              Multisamples = multisamples
              Size = size
              Count = count
              IsArray = isArray
              Format = format
              SizeInBytes = sizeInBytes
              name = null }

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int,
            size : V3i, count : int, isArray : bool, format : TextureFormat) =
            let sizeInBytes = ResourceCounts.texSizeInBytes dimension size format multisamples mipMapLevels count
            new Texture(ctx, handle, dimension, mipMapLevels, multisamples, size, count, isArray, format, sizeInBytes)

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int,
            size : V3i, count : Option<int>, format : TextureFormat, sizeInBytes : int64) =
            let cnt, isArray =
                match count with
                | Some cnt -> cnt, true
                | None -> 1, false

            new Texture(ctx, handle, dimension, mipMapLevels, multisamples, size, cnt, isArray, format, sizeInBytes)

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int,
            size : V3i, count : Option<int>, format : TextureFormat) =
            let cnt, isArray =
                match count with
                | Some cnt -> cnt, true
                | None -> 1, false

            new Texture(ctx, handle, dimension, mipMapLevels, multisamples, size, cnt, isArray, format)
    end

type internal SharedTexture(ctx : Context, handle : int, external : IExportedBackendTexture, memory : SharedMemoryBlock) =
    inherit Texture(ctx, handle,
                    external.Dimension, external.MipMapLevels, external.Samples, external.Size,
                    external.Count, external.IsArray, external.Format, external.Memory.Size)

    member x.External = external
    member x.Memory = memory

    override x.Destroy() =
        x.Memory.Dispose()
        base.Destroy()

type TextureViewHandle(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int,
                       multisamples : int, size : V3i, count : Option<int>, format : TextureFormat) =
    inherit Texture(ctx, handle, dimension, mipMapLevels, multisamples, size, count, format, 0L)

    override x.Destroy() =
        GL.DeleteTexture x.Handle
        ResourceCounts.removeTextureView x.Context
        GL.Check "could not delete texture view"

// Used for determining the bind target of null textures
type internal TextureProperties =
    { Dimension      : TextureDimension
      IsMultisampled : bool
      IsArray        : bool }

[<AutoOpen>]
module internal TextureUtilitiesAndExtensions =

    module TextureTarget =
        let ofTexture (texture : Texture) =
            TextureTarget.ofParameters texture.Dimension texture.IsArray texture.IsMultisampled

    type FShade.GLSL.GLSLSamplerType with
        member x.Properties =
            { Dimension      = x.dimension.TextureDimension
              IsMultisampled = x.isMS
              IsArray        = x.isArray }

    type FShade.GLSL.GLSLImageType with
        member x.Properties =
            { Dimension      = x.dimension.TextureDimension
              IsMultisampled = x.isMS
              IsArray        = x.isArray }

    module Image =

        /// Validates the sample count for the given image parameters and returns an appropiate fallback
        /// if the sample count is not supported.
        let validateSampleCount (ctx : Context) (target : ImageTarget) (format : TextureFormat) (samples : int) =
            if samples > 1 then
                let counts = ctx.GetFormatSamples(target, format)
                if counts.Contains samples then samples
                else
                    let fallback =
                        counts
                        |> Set.toList
                        |> List.minBy ((-) samples >> abs)

                    Log.warn "[GL] Cannot create %A image with %d samples (using %d instead)" format samples fallback
                    fallback
            else
                1

    [<AutoOpen>]
    module TensorExtensions =

        module Tensor4Info =

            let mirrorY (info : Tensor4Info) =
                Tensor4Info(
                    info.Index(0L, info.SY - 1L, 0L, 0L),
                    info.Size,
                    V4l(info.DX, -info.DY, info.DZ, info.DW)
                )

            let asBytes (elementSize : int) (info : Tensor4Info) =
                Tensor4Info(
                    info.Origin * int64 elementSize,
                    info.Size,
                    info.Delta * int64 elementSize
                )

            let deviceLayoutWithOffset (flipY : bool) (offset : int) (elementSize : int)
                                       (stride : nativeint) (channels : int) (size : V3l) =
                let size = V4l(size, int64 channels)
                let channelSize = int64 elementSize

                let origin, deltaY =
                    if flipY then
                        int64 stride * (size.Y - 1L), int64 -stride
                    else
                        0L, int64 stride

                let delta = V4l(int64 channels * channelSize, deltaY, int64 stride * size.Y, channelSize)
                Tensor4Info(origin + int64 offset, size, delta)

            let deviceLayout (flipY : bool) (elementSize : int) (stride : nativeint) (channels : int) (size : V3l) =
                deviceLayoutWithOffset flipY 0 elementSize stride channels size

        type Tensor4Info with
            member x.MirrorY() = x |> Tensor4Info.mirrorY
            member x.AsBytes(elementSize) = x |> Tensor4Info.asBytes elementSize
            member x.AsBytes<'T>() = x.AsBytes(sizeof<'T>)

    module WindowOffset =

        let flipY (height : int) (window : Box3i) =
            V3i(window.Min.X, height - window.Max.Y, window.Min.Z)

    type Texture with
        member x.IsCubeOr2D =
            match x.Dimension with
            | TextureDimension.Texture2D | TextureDimension.TextureCube -> true
            | _ -> false

        member x.WindowOffset(level : int, window : Box3i) =
            if x.IsCubeOr2D then
                let levelSize = x.GetSize level
                window |> WindowOffset.flipY levelSize.Y
            else
                window.Min

        member x.WindowOffset(level : int, offset : V3i, size : V3i) =
            x.WindowOffset(level, Box3i.FromMinAndSize(offset, size))

        member x.WindowOffset(level : int, window : Box2i) =
            let window = Box3i(V3i(window.Min, 0), V3i(window.Max, 1))
            x.WindowOffset(level, window).XY

        member x.WindowOffset(level : int, offset : V2i, size : V2i) =
            x.WindowOffset(level, Box2i.FromMinAndSize(offset, size))

    module Framebuffer =
        let check (target : FramebufferTarget) =
            let error = GL.CheckFramebufferStatus(target)
            if error <> FramebufferErrorCode.FramebufferComplete then
                failwithf "framebuffer incomplete: %A" error

        let temporary (target : FramebufferTarget) (f : int -> 'T) =
            let binding =
                if target = FramebufferTarget.ReadFramebuffer then
                    GetPName.ReadFramebufferBinding
                else
                    GetPName.DrawFramebufferBinding

            let old = GL.GetInteger(binding)

            let fbo = GL.GenFramebuffer()
            GL.Check "could not create framebuffer"

            GL.BindFramebuffer(target, fbo)
            GL.Check "could not bind framebuffer"

            try
                f fbo
            finally
                GL.BindFramebuffer(target, old)
                GL.DeleteFramebuffer(fbo)

[<AutoOpen>]
module TextureCreationExtensions =

    [<AutoOpen>]
    module internal MipmapGenerationSupport =

        type Context with

            /// Returns if mipmap generation supported for the given target and format.
            /// Logs a warning if support is limited or missing.
            /// Note: A context handle must be current.
            member x.IsMipmapGenerationSupported(target : TextureTarget, format : TextureFormat) =
                let support = x.GetFormatMipmapGeneration(unbox<ImageTarget> target, format)

                match support with
                | MipmapGenerationSupport.Full -> true
                | MipmapGenerationSupport.Caveat ->
                    Log.warn "[GL] Format %A has only limited support for mipmap generation" format
                    true
                | _ ->
                    Log.warn "[GL] Format %A does not support mipmap generation" format
                    false

        type Texture with

            /// Returns if the texture supports mipmap generation.
            /// Logs a warning if support is limited or missing.
            /// Note: A context handle must be current.
            member x.IsMipmapGenerationSupported =
                let target = TextureTarget.ofTexture x
                x.Context.IsMipmapGenerationSupported(target, x.Format)

            /// Throws an exception if the texture does not support mipmap generation.
            /// Logs a warning if support is limited or missing.
            /// Note: A context handle must be current.
            member x.CheckMipmapGenerationSupport() =
                if not <| x.IsMipmapGenerationSupported then
                    raise <| NotSupportedException($"[GL] Format {x.Format} does not support mipmap generation.")

    type Context with

        // ================================================================================================================
        // SetDefaultTextureParams
        // ================================================================================================================

        member internal x.SetDefaultTextureParams(target : TextureTarget, format : TextureFormat, mipMapLevels : int) =
            match target with
            | TextureTarget.Texture2DMultisample
            | TextureTarget.Texture2DMultisampleArray -> ()
            | _ ->
                // For gray scale textures, duplicate channel
                if TextureFormat.toColFormat format = Col.Format.Gray then
                    GL.TexParameter(target, TextureParameterName.TextureSwizzleG, int PixelFormat.Red)
                    GL.TexParameter(target, TextureParameterName.TextureSwizzleB, int PixelFormat.Red)

                GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipMapLevels - 1)
                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
                GL.TexParameter(target, TextureParameterName.TextureWrapS, int TextureWrapMode.ClampToEdge)
                GL.TexParameter(target, TextureParameterName.TextureWrapT, int TextureWrapMode.ClampToEdge)
                GL.TexParameter(target, TextureParameterName.TextureMinFilter, int TextureMinFilter.Linear)
                GL.TexParameter(target, TextureParameterName.TextureMagFilter, int TextureMagFilter.Linear)

        // ================================================================================================================
        // CreateTexture
        // ================================================================================================================

        member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, slices : int, levels : int, samples : int) =
            using x.ResourceLock (fun _ ->
                let isArray = slices > 0

                if format = TextureFormat.StencilIndex8 && not GL.ARB_texture_stencil8 then
                    failf "textures with format %A not supported" format

                match dim, isArray with
                | TextureDimension.Texture1D, false -> x.CreateTexture1D(size.X, levels, format)
                | TextureDimension.Texture1D, true  -> x.CreateTexture1DArray(size.X, slices, levels, format)
                | TextureDimension.Texture2D, false -> x.CreateTexture2D(size.XY, levels, format, samples)
                | TextureDimension.Texture2D, true  -> x.CreateTexture2DArray(size.XY, slices, levels, format, samples)
                | TextureDimension.Texture3D, false -> x.CreateTexture3D(size, levels, format)
                | TextureDimension.Texture3D, true  -> raise <| ArgumentException("3D textures cannot be arrayed")
                | TextureDimension.TextureCube, false -> x.CreateTextureCube(size.X, levels, format)
                | TextureDimension.TextureCube, true  -> x.CreateTextureCubeArray(size.X, slices, levels, format)
                | _ -> failf "Invalid texture dimension"
            )

        member x.CreateTexture1D(size : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if size > x.MaxTextureSize.X then
                    failf $"cannot create 1D texture with size {size} (maximum is {x.MaxTextureSize.X})"

                let h = GL.GenTexture()
                GL.Check "could not create texture"

                GL.BindTexture(TextureTarget.Texture1D, h)
                GL.Check "could not bind texture"

                x.SetDefaultTextureParams(TextureTarget.Texture1D, format, mipMapLevels)
                GL.Check "could not set default texture parameters"

                GL.Dispatch.TexStorage1D(TextureTarget1d.Texture1D, mipMapLevels, TextureFormat.toSizedInternalFormat format, size)
                GL.Check $"failed to allocate 1D texture storage (format = {format}, size = {size}, levels = {mipMapLevels})"

                GL.BindTexture(TextureTarget.Texture1D, 0)
                GL.Check "could not unbind texture"

                let tex = new Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i(size, 1, 1), None, format)
                ResourceCounts.addTexture x tex.SizeInBytes
                tex
            )

        member x.CreateTexture2D(size : V2i, mipMapLevels : int, format : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if Vec.anyGreater size x.MaxTextureSize then
                    failf $"cannot create 2D texture with size {size} (maximum is {x.MaxTextureSize})"

                let samples =
                    if samples <= 1 then 1
                    else Image.validateSampleCount x ImageTarget.Texture2DMultisample format samples

                let target =
                    if samples = 1 then TextureTarget.Texture2D
                    else TextureTarget.Texture2DMultisample

                let h = GL.GenTexture()
                GL.Check "could not create texture"

                GL.BindTexture(target, h)
                GL.Check "could not bind texture"

                x.SetDefaultTextureParams(target, format, mipMapLevels)
                GL.Check "could not set default texture parameters"

                let ifmt = TextureFormat.toSizedInternalFormat format

                if samples = 1 then
                    GL.Dispatch.TexStorage2D(unbox target, mipMapLevels, ifmt, size.X, size.Y)
                    GL.Check $"failed to allocate 2D texture storage (format = {format}, size = {size}, levels = {mipMapLevels})"
                else
                    GL.Dispatch.TexStorage2DMultisample(unbox target, samples, ifmt, size.X, size.Y, true)
                    GL.Check $"failed to allocate 2D texture storage (format = {format}, size = {size}, samples = {samples})"

                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                let tex = new Texture(x, h, TextureDimension.Texture2D, mipMapLevels, samples, size.XYI, None, format)
                ResourceCounts.addTexture x tex.SizeInBytes
                tex
            )

        member x.CreateTexture3D(size : V3i, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if Vec.anyGreater size x.MaxTextureSize3D then
                    failf $"cannot create 3D texture with size {size} (maximum is {x.MaxTextureSize3D})"

                let h = GL.GenTexture()
                GL.Check "could not create texture"

                GL.BindTexture(TextureTarget.Texture3D, h)
                GL.Check "could not bind texture"

                x.SetDefaultTextureParams(TextureTarget.Texture3D, format, mipMapLevels)
                GL.Check "could not set default texture parameters"

                GL.Dispatch.TexStorage3D(TextureTarget3d.Texture3D, mipMapLevels, TextureFormat.toSizedInternalFormat format, size.X, size.Y, size.Z)
                GL.Check $"failed to allocate 3D texture storage (format = {format}, size = {size}, levels = {mipMapLevels})"

                GL.BindTexture(TextureTarget.Texture3D, 0)
                GL.Check "could not unbind texture"

                let tex = new Texture(x, h, TextureDimension.Texture3D, mipMapLevels, 1, size, None, format)
                ResourceCounts.addTexture x tex.SizeInBytes
                tex
            )

        member x.CreateTextureCube(size : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if size > x.MaxTextureSizeCube then
                    failf $"cannot create cube texture with size {size} (maximum is {x.MaxTextureSizeCube})"

                let h = GL.GenTexture()
                GL.Check "could not create texture"

                GL.BindTexture(TextureTarget.TextureCubeMap, h)
                GL.Check "could not bind texture"

                x.SetDefaultTextureParams(TextureTarget.TextureCubeMap, format, mipMapLevels)
                GL.Check "could not set default texture parameters"

                GL.Dispatch.TexStorage2D(TextureTarget2d.TextureCubeMap, mipMapLevels, TextureFormat.toSizedInternalFormat format, size, size)
                GL.Check $"failed to allocate cube texture storage (format = {format}, size = {size}, levels = {mipMapLevels})"

                GL.BindTexture(TextureTarget.TextureCubeMap, 0)
                GL.Check "could not unbind texture"

                let tex = new Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size, size, 1), None, format)
                ResourceCounts.addTexture x tex.SizeInBytes
                tex
            )

        member x.CreateTexture1DArray(size : int, count : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if size > x.MaxTextureSize.X then
                    failf $"cannot create 1D array texture with size {size} (maximum is {x.MaxTextureSize.X})"

                if count > x.MaxTextureArrayLayers then
                    failf $"cannot create 1D array texture with {count} layers (maximum is {x.MaxTextureArrayLayers})"

                let h = GL.GenTexture()
                GL.Check "could not create texture"

                GL.BindTexture(TextureTarget.Texture1DArray, h)
                GL.Check "could not bind texture"

                x.SetDefaultTextureParams(TextureTarget.Texture1DArray, format, mipMapLevels)
                GL.Check "could not set default texture parameters"

                GL.Dispatch.TexStorage2D(TextureTarget2d.Texture1DArray, mipMapLevels, TextureFormat.toSizedInternalFormat format, size, count)
                GL.Check $"failed to allocate 1D array texture storage (format = {format}, size = {size}, count = {count}, levels = {mipMapLevels})"

                GL.BindTexture(TextureTarget.Texture1DArray, 0)
                GL.Check "could not unbind texture"

                let tex = new Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i(size, 1, 1), Some count, format)
                ResourceCounts.addTexture x tex.SizeInBytes
                tex
            )

        member x.CreateTexture2DArray(size : V2i, count : int, mipMapLevels : int, format : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if Vec.anyGreater size x.MaxTextureSize then
                    failf $"cannot create 2D array texture with size {size} (maximum is {x.MaxTextureSize})"

                if count > x.MaxTextureArrayLayers then
                    failf $"cannot create 2D array texture with {count} layers (maximum is {x.MaxTextureArrayLayers})"

                let samples =
                    if samples <= 1 then 1
                    else Image.validateSampleCount x ImageTarget.Texture2DMultisampleArray format samples

                let target =
                    if samples = 1 then TextureTarget.Texture2DArray
                    else TextureTarget.Texture2DMultisampleArray

                let h = GL.GenTexture()
                GL.Check "could not create texture"

                GL.BindTexture(target, h)
                GL.Check "could not bind texture"

                x.SetDefaultTextureParams(unbox target, format, mipMapLevels)
                GL.Check "could not set default texture parameters"

                let ifmt = TextureFormat.toSizedInternalFormat format

                if samples = 1 then
                    GL.Dispatch.TexStorage3D(unbox target, mipMapLevels, ifmt, size.X, size.Y, count)
                    GL.Check $"failed to allocate 2D array texture storage (format = {format}, size = {size}, count = {count}, levels = {mipMapLevels})"
                else
                    GL.Dispatch.TexStorage3DMultisample(unbox target, samples, ifmt, size.X, size.Y, count, true)
                    GL.Check $"failed to allocate 2D array texture storage (format = {format}, size = {size}, count = {count}, samples = {samples})"

                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                let tex = new Texture(x, h, TextureDimension.Texture2D, mipMapLevels, samples, size.XYI, Some count, format)
                ResourceCounts.addTexture x tex.SizeInBytes
                tex
            )

        member x.CreateTextureCubeArray(size : int, count : int, mipMapLevels : int, format : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if size > x.MaxTextureSizeCube then
                    failf $"cannot create cube array texture with size {size} (maximum is {x.MaxTextureSizeCube})"

                if count > x.MaxTextureArrayLayers then
                    failf $"cannot create cube array texture with {count} layers (maximum is {x.MaxTextureArrayLayers})"

                let h = GL.GenTexture()
                GL.Check "could not create texture"

                GL.BindTexture(TextureTarget.TextureCubeMapArray, h)
                GL.Check "could not bind texture"

                GL.Dispatch.TexStorage3D(TextureTarget3d.TextureCubeMapArray, mipMapLevels, TextureFormat.toSizedInternalFormat format, size, size, count * 6)
                GL.Check $"failed to allocate cube array texture storage (format = {format}, size = {size}, count = {count}, levels = {mipMapLevels})"

                GL.BindTexture(TextureTarget.TextureCubeMapArray, 0)
                GL.Check "could not unbind texture"

                let tex = new Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size, size, 1), Some count, format)
                ResourceCounts.addTexture x tex.SizeInBytes
                tex
            )

        // ================================================================================================================
        // CreateTextureView
        // ================================================================================================================

        member x.CreateTextureView(orig : Texture, levels : Range1i, slices : Range1i, isArray : bool) =
            using x.ResourceLock (fun _ ->
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

                let tex = new TextureViewHandle(x, handle, dim, levelCount, orig.Multisamples, orig.Size, sliceCountHandle, orig.Format)
                let target = TextureTarget.ofTexture tex

                GL.Dispatch.TextureView(
                    handle,
                    target,
                    orig.Handle,
                    TextureFormat.toPixelInternalFormat orig.Format,
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
            t.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Texture =

    let private emptyStore = ConcurrentDictionary<TextureProperties, Texture>()

    let internal empty (properties : TextureProperties) =
        emptyStore.GetOrAdd(properties, fun _ ->
            let count = if properties.IsArray then Some 1 else None
            let samples = if properties.IsMultisampled then 2 else 1
            new Texture(null, 0, properties.Dimension, 1, samples, V3i.Zero, count, TextureFormat.Rgba8, 0L)
        )

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