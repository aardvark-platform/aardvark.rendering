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
open Aardvark.Base.NativeTensors
open Microsoft.FSharp.NativeInterop
open System.Runtime.CompilerServices

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

        interface IContextChild with
            member x.Context = x.Context
            member x.Handle = x.Handle

        interface IBackendTexture with
            member x.WantMipMaps = x.MipMapLevels > 1
            member x.Dimension = x.Dimension
            member x.MipMapLevels = x.MipMapLevels
            member x.Handle = x.Handle :> obj
            member x.Size = x.Size
            member x.Count = x.Count
            member x.Format = x.Format
            member x.Samples = x.Multisamples

        member x.GetSize (level : int)  =
            if level = 0 then x.Size2D
            else 
                let level = Fun.Clamp(level, 0, x.MipMapLevels-1)
                let factor = 1 <<< level
                x.Size2D / factor

        new(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int, size : V3i, count : Option<int>, format : TextureFormat, sizeInBytes : int64, immutable : bool) =
            let cnt, isArray =
                match count with
                    | Some cnt -> cnt, true
                    | None -> 1, false
            { Context = ctx; Handle = handle; Dimension = dimension; MipMapLevels = mipMapLevels; Multisamples = multisamples; Size = size; Count = cnt; IsArray = isArray; Format = format; SizeInBytes = sizeInBytes; ImmutableFormat = immutable }

    end

[<AutoOpen>]
module private ResourceCounts =

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


    let addTexture (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.TextureCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,size) |> ignore

    let removeTexture (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.TextureCount)  |> ignore
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,-size) |> ignore

    let updateTexture (ctx:Context) oldSize newSize =
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,newSize-oldSize) |> ignore

    let texSizeInBytes (size : V3i, t : TextureFormat, samples : int) =
        let pixelCount = (int64 size.X) * (int64 size.Y) * (int64 size.Z) * (int64 samples)
        pixelCount * (int64 (InternalFormat.getSizeInBits (unbox (int t)))) / 8L

module TextureTarget =
    let ofParameters (dim : TextureDimension) (isArray : bool) (isMS : bool) =
        match dim, isArray, isMS with

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

            | _ -> failwithf "unknown texture dimension: %A" dim

    let ofTexture (texture : Texture) =
        ofParameters texture.Dimension texture.IsArray texture.IsMultisampled


[<AutoOpen>]
module TextureCreationExtensions =
    type Context with 
        member x.CreateTexture1D(size : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i.Zero, None, t, 0L, false)
                x.UpdateTexture1D(tex, size, mipMapLevels, t)

                tex
            )

        member x.CreateTexture2D(size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"
                
                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i.Zero, None, t, 0L, false)

                x.UpdateTexture2D(tex, size, mipMapLevels, t, samples)

                tex
            )

        member x.CreateTexture3D(size : V3i, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture3D, mipMapLevels, 1, V3i.Zero, None, t, 0L, false)
                x.UpdateTexture3D(tex, size, mipMapLevels, t)

                tex
            )

        member x.CreateTextureCube(size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size.X, size.Y, 0), None, t, 0L, false)
                x.UpdateTextureCube(tex, size, mipMapLevels, t, samples)

                tex
            )

        member x.CreateTexture1DArray(size : int, count : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"

                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i.Zero, Some count, t, 0L, false)
                x.UpdateTexture1DArray(tex, size, count, mipMapLevels, t)

                tex
            ) 

        member x.CreateTexture2DArray(size : V2i, count : int, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                let h = GL.GenTexture()
                GL.Check "could not create texture"
                
                addTexture x 0L
                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i.Zero, Some count, t, 0L, false)

                x.UpdateTexture2DArray(tex, size, count, mipMapLevels, t, samples)

                tex
            )
            
        member x.UpdateTexture1D(tex : Texture, size : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture1D, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = texSizeInBytes(V3i(size, 1, 1), t, 1)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes
  
                GL.TexStorage1D(TextureTarget1d.Texture1D, mipMapLevels, unbox (int t), size)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture1D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture1D
                tex.Size <- V3i(size, 0, 0)
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture2D(tex : Texture, size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                let target =
                    if samples = 1 then TextureTarget.Texture2D
                    else TextureTarget.Texture2DMultisample

                GL.BindTexture(target, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = texSizeInBytes(size.XYI, t, samples)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipMapLevels - 1)
                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
                GL.TexParameter(target, TextureParameterName.TextureWrapS, int TextureWrapMode.ClampToEdge)
                GL.TexParameter(target, TextureParameterName.TextureWrapT, int TextureWrapMode.ClampToEdge)
                GL.TexParameter(target, TextureParameterName.TextureMinFilter, int TextureMinFilter.Linear)
                GL.TexParameter(target, TextureParameterName.TextureMagFilter, int TextureMagFilter.Linear)


                if samples = 1 then
                    GL.TexStorage2D(TextureTarget2d.Texture2D, mipMapLevels, unbox (int t), size.X, size.Y)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage2DMultisample(TextureTargetMultisample2d.Texture2DMultisample, samples, unbox (int t), size.X, size.Y, false)

                GL.Check "could not allocate texture"
                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture2D
                tex.Multisamples <- samples
                tex.Count <- 1
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture3D(tex : Texture, size : V3i, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture3D, tex.Handle)
                GL.Check "could not bind texture"

                let ifmt = unbox (int t) 

                let sizeInBytes = texSizeInBytes(size, t, 1)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                GL.TexStorage3D(TextureTarget3d.Texture3D, mipMapLevels, ifmt, size.X, size.Y, size.Z)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture3D, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture3D
                tex.Count <- 1
                tex.Multisamples <- 1
                tex.Size <- size
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTextureCube(tex : Texture, size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.TextureCubeMap, tex.Handle)
                GL.Check "could not bind texture"

                if samples = 1 then
                    GL.TexStorage2D(TextureTarget2d.TextureCubeMap, mipMapLevels, unbox (int t), size.X, size.Y)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    Log.warn "[GL] cubemap MS not working atm."
                    // TODO: verify that this works!!
                    for f in 0..5 do
                        let target = int TextureTarget.TextureCubeMapPositiveX + f
                        GL.TexImage2DMultisample(unbox target, samples, unbox (int t), size.X, size.Y, true)

                GL.BindTexture(TextureTarget.TextureCubeMap, 0)
                GL.Check "could not unbind texture"

                let sizeInBytes = texSizeInBytes(size.XYI, t, samples)
                let sizeInBytes = sizeInBytes * 6L
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.TextureCube
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.Count <- 1
                tex.Multisamples <- samples
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture2DArray(tex : Texture, size : V2i, count : int, mipMapLevels : int, t : TextureFormat, samples : int) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                let target =
                    if samples = 1 then TextureTarget.Texture2DArray
                    else TextureTarget.Texture2DMultisampleArray

                GL.BindTexture(target, tex.Handle)
                GL.Check "could not bind texture"


                let sizeInBytes = texSizeInBytes(size.XYI, t, samples) * (int64 count)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes // TODO check multisampling

                if samples = 1 then
                    GL.TexStorage3D(TextureTarget3d.Texture2DArray, mipMapLevels, unbox (int t), size.X, size.Y, count)
                else
                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
                    GL.TexStorage3DMultisample(TextureTargetMultisample3d.Texture2DMultisampleArray, samples, unbox (int t), size.X, size.Y, count, true)
  
                GL.Check "could not allocate texture"

                GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipMapLevels)
                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)


                GL.BindTexture(target, 0)
                GL.Check "could not unbind texture"

                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture2D
                tex.IsArray <- true
                tex.Count <- count
                tex.Multisamples <- samples
                tex.Size <- V3i(size.X, size.Y, 0)
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.UpdateTexture1DArray(tex : Texture, size : int, count : int, mipMapLevels : int, t : TextureFormat) =
            using x.ResourceLock (fun _ ->
                if tex.ImmutableFormat then
                    failwith "cannot update format/size for immutable texture"

                GL.BindTexture(TextureTarget.Texture1DArray, tex.Handle)
                GL.Check "could not bind texture"

                let sizeInBytes = texSizeInBytes(V3i(size, 1, 1), t, 1) * (int64 count)
                updateTexture tex.Context tex.SizeInBytes sizeInBytes
                tex.SizeInBytes <- sizeInBytes
  
                GL.TexStorage2D(TextureTarget2d.Texture1DArray, mipMapLevels, unbox (int t), size, count)
                GL.Check "could not allocate texture"

                GL.BindTexture(TextureTarget.Texture1DArray, 0)
                GL.Check "could not unbind texture"
                
                tex.IsArray <- true
                tex.MipMapLevels <- mipMapLevels
                tex.Dimension <- TextureDimension.Texture1D
                tex.Count <- count
                tex.Multisamples <- 1
                tex.Size <- V3i(size, 0, 0)
                tex.Format <- t
                tex.ImmutableFormat <- true
            )

        member x.CreateTextureView(orig : Texture, levels : Range1i, slices : Range1i, isArray : bool) =
            using x.ResourceLock (fun _ ->
                if not orig.ImmutableFormat then
                    failwithf "cannot create texture-views for mutable textures"

                let handle = GL.GenTexture()
                GL.Check "could not create texture"

                let dim =
                    match orig.Dimension, orig.Count with
                        | TextureDimension.TextureCube, 1 -> 
                            if slices.Min <> slices.Max then failwithf "cannot take multiple slices from CubeMap"
                            TextureDimension.Texture2D
                        | d,_ -> d

                let levelCount = 1 + levels.Max - levels.Min
                let sliceCount =
                    if orig.IsArray && isArray then Some (1 + slices.Max - slices.Min)
                    else None


                let tex = Texture(x, handle, dim, levelCount, orig.Multisamples, orig.Size, sliceCount, orig.Format, 0L, true)
                let target = TextureTarget.ofTexture tex
                  
                GL.TextureView(
                    handle,
                    target,
                    orig.Handle,
                    unbox (int orig.Format),
                    levels.Min, 1 + levels.Max - levels.Min,
                    slices.Min, 1
                )
                GL.Check "could not create texture view"

                tex
            )

        member x.CreateTextureView(orig : Texture, levels : Range1i, slices : Range1i) =
            x.CreateTextureView(orig, levels, slices, true)

        member x.Delete(t : Texture) =
            using x.ResourceLock (fun _ ->
                removeTexture x t.SizeInBytes
                GL.DeleteTexture(t.Handle)
                GL.Check "could not delete texture"
            )
            
        member x.Blit(src : Texture, srcLevel : int, srcSlice : int, srcRegion : Box2i, dst : Texture, dstLevel : int, dstSlice : int, dstRegion : Box2i, linear : bool) =
            using x.ResourceLock (fun _ ->
                let fSrc = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"
                let fDst = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                GL.Check "could not bind framebuffer"
                if src.IsArray then GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel, srcSlice)
                else GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel)
                GL.Check "could not attach texture to framebuffer"

                let srcCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                if srcCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create input framebuffer: %A" srcCheck

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fDst)
                GL.Check "could not bind framebuffer"

                if src.IsArray then GL.FramebufferTextureLayer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, dst.Handle, dstLevel, dstSlice)
                else GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, dst.Handle, dstLevel)
                GL.Check "could not attach texture to framebuffer"

                let dstCheck = GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer)
                if dstCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create output framebuffer: %A" dstCheck

                GL.BlitFramebuffer(
                    srcRegion.Min.X, srcRegion.Min.Y,
                    srcRegion.Max.X, srcRegion.Max.Y,

                    dstRegion.Min.X, dstRegion.Min.Y,
                    dstRegion.Max.X, dstRegion.Max.Y,

                    ClearBufferMask.ColorBufferBit,
                    (if linear then BlitFramebufferFilter.Linear else BlitFramebufferFilter.Nearest)
                )
                GL.Check "could blit framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.Check "could unbind framebuffer"

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.Check "could unbind framebuffer"

                GL.DeleteFramebuffer(fSrc)
                GL.Check "could delete framebuffer"

                GL.DeleteFramebuffer(fDst)
                GL.Check "could delete framebuffer"

            )

        member x.Copy(src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i, dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i, size : V2i) =
            using x.ResourceLock (fun _ ->
                let fSrc = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                GL.Check "could not bind framebuffer"

                if src.IsArray then
                    GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel, srcSlice)
                else
                    GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel)
                GL.Check "could not attach texture to framebuffer"

                let srcCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                if srcCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create input framebuffer: %A" srcCheck

                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.Check "could not set readbuffer"


                let bindTarget = TextureTarget.ofTexture dst
                GL.BindTexture(bindTarget, dst.Handle)
                GL.Check "could not bind texture"

                let copyTarget =
                    match dst.Dimension with
                        | TextureDimension.TextureCube -> snd cubeSides.[dstSlice]
                        | _ -> bindTarget

                GL.CopyTexSubImage2D(
                    copyTarget,
                    dstLevel,
                    dstOffset.X, dstOffset.Y,
                    srcOffset.X, srcOffset.Y,
                    size.X, size.Y
                )
                GL.Check "could not copy texture"

                GL.ReadBuffer(ReadBufferMode.None)
                GL.Check "could not unset readbuffer"

                GL.BindTexture(bindTarget, 0)
                GL.Check "could not unbind texture"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.Check "could not unbind framebuffer"

                GL.DeleteFramebuffer(fSrc)
                GL.Check "could not delete framebuffer"

            )


[<AutoOpen>]
module TextureUploadExtensions =
    open Microsoft.FSharp.NativeInterop

    module private StructTypes = 
        [<StructLayout(LayoutKind.Explicit, Size = 1)>] type byte1 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 2)>] type byte2 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 3)>] type byte3 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 4)>] type byte4 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 5)>] type byte5 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 6)>] type byte6 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 7)>] type byte7 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 8)>] type byte8 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 9)>] type byte9 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 10)>] type byte10 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 11)>] type byte11 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 12)>] type byte12 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 13)>] type byte13 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 14)>] type byte14 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 15)>] type byte15 = struct end
        [<StructLayout(LayoutKind.Explicit, Size = 16)>] type byte16 = struct end


        let types = 
            Dictionary.ofList [
                1, typeof<byte1>
                2, typeof<byte2>
                3, typeof<byte3>
                4, typeof<byte4>
                5, typeof<byte5>
                6, typeof<byte6>
                7, typeof<byte7>
                8, typeof<byte8>
                9, typeof<byte9>
                10, typeof<byte10>
                11, typeof<byte11>
                12, typeof<byte12>
                13, typeof<byte13>
                14, typeof<byte14>
                15, typeof<byte15>
                16, typeof<byte16>
            ]

    [<AutoOpen>]
    module private ExistentialHack = 
        type IUnmanagedAction =
            abstract member Run<'a when 'a : unmanaged> : Option<'a> -> unit

        let private meth = typeof<IUnmanagedAction>.GetMethod "Run"

        let run (e : IUnmanagedAction) (t : Type) =
            let mi = meth.MakeGenericMethod [|t|]
            mi.Invoke(e, [| null |]) |> ignore

        type Col.Format with
            static member Stencil = unbox<Col.Format> (Int32.MaxValue)
            static member Depth = unbox<Col.Format> (Int32.MaxValue - 1)

        let toChannelCount =
            LookupTable.lookupTable [
                Col.Format.Alpha, 1
                Col.Format.BW, 1
                Col.Format.Gray, 1
                Col.Format.GrayAlpha, 2
                Col.Format.RGB, 3
                Col.Format.BGR, 3
                Col.Format.RGBA, 4
                Col.Format.BGRA, 4
                Col.Format.RGBP, 4
                Col.Format.NormalUV, 2
                Col.Format.Stencil, 1
                Col.Format.Depth, 1
            ]

    type TextureCopyUtils =

        static member Copy(elementType : Type, src : nativeint, srcInfo : VolumeInfo, dst : nativeint, dstInfo : VolumeInfo) =
            elementType |> run { 
                new IUnmanagedAction with
                    member x.Run(a : Option<'a>) =
                        let vSrc = NativeVolume<byte>(NativePtr.ofNativeInt src, srcInfo)
                        let vDst = NativeVolume<byte>(NativePtr.ofNativeInt dst, dstInfo)

                        let copy (s : nativeptr<byte>) (d : nativeptr<byte>) =
                            let s : nativeptr<'a> = NativePtr.cast s
                            let d : nativeptr<'a> = NativePtr.cast d
                            NativePtr.write d (NativePtr.read s)

                        NativeVolume.iter2 vSrc vDst copy
            }

        static member Copy(elementSize : int, src : nativeint, srcInfo : VolumeInfo, dst : nativeint, dstInfo : VolumeInfo) =
            TextureCopyUtils.Copy(StructTypes.types.[elementSize], src, srcInfo, dst, dstInfo)

        static member Copy(src : PixImage, dst : nativeint, dstInfo : VolumeInfo) =
            let gc = GCHandle.Alloc(src.Array, GCHandleType.Pinned)
            try
                let pSrc = gc.AddrOfPinnedObject()
                let imgInfo = src.VolumeInfo
                let elementType = src.PixFormat.Type
                let elementSize = elementType.GLSize |> int64
                let srcInfo =
                    VolumeInfo(
                        imgInfo.Origin * elementSize,
                        imgInfo.Size,
                        imgInfo.Delta * elementSize
                    )
                TextureCopyUtils.Copy(elementType, pSrc, srcInfo, dst, dstInfo)
            finally
                gc.Free()   

        static member Copy(src : nativeint, srcInfo : VolumeInfo, dst : PixImage) =
            let gc = GCHandle.Alloc(dst.Array, GCHandleType.Pinned)
            try
                let pDst = gc.AddrOfPinnedObject()
                let imgInfo = dst.VolumeInfo
                let elementType = dst.PixFormat.Type
                let elementSize = elementType.GLSize |> int64
                let dstInfo =
                    VolumeInfo(
                        imgInfo.Origin * elementSize,
                        imgInfo.Size,
                        imgInfo.Delta * elementSize
                    )
                TextureCopyUtils.Copy(elementType, src, srcInfo, pDst, dstInfo)
            finally
                gc.Free()     

    [<AutoOpen>]
    module FormatConversions = 
        type Col.Format with
            member x.ChannelCount = toChannelCount x

        module PixelFormat =
        
            let channels =
                LookupTable.lookupTable [
                    PixelFormat.Bgr, 3
                    PixelFormat.Bgra, 4
                    PixelFormat.Red, 1
                    PixelFormat.Rg, 2
                    PixelFormat.Rgb, 3
                    PixelFormat.Rgba, 4
                ]

            let ofColFormat =
                LookupTable.lookupTable [
                    Col.Format.Alpha, PixelFormat.Red
                    Col.Format.BGR, PixelFormat.Bgr
                    Col.Format.BGRA, PixelFormat.Bgra
                    Col.Format.BGRP, PixelFormat.Bgra
                    Col.Format.BW, PixelFormat.Red
                    Col.Format.Gray, PixelFormat.Red
                    Col.Format.GrayAlpha, PixelFormat.Rg
                    Col.Format.NormalUV, PixelFormat.Rg
                    Col.Format.RGB, PixelFormat.Rgb
                    Col.Format.RGBA, PixelFormat.Rgba
                    Col.Format.RGBP, PixelFormat.Rgba
                ]

        module PixelType =

            let size =
                LookupTable.lookupTable [
                    PixelType.UnsignedByte, 1
                    PixelType.Byte, 1
                    PixelType.UnsignedShort, 2
                    PixelType.Short, 2
                    PixelType.UnsignedInt, 4
                    PixelType.Int, 4
                    PixelType.HalfFloat, 2
                    PixelType.Float, 4
                ]

            let ofType =
                LookupTable.lookupTable [
                    typeof<uint8>, PixelType.UnsignedByte
                    typeof<int8>, PixelType.Byte
                    typeof<uint16>, PixelType.UnsignedShort
                    typeof<int16>, PixelType.Short
                    typeof<uint32>, PixelType.UnsignedInt
                    typeof<int32>, PixelType.Int
                    typeof<float16>, PixelType.HalfFloat
                    typeof<float32>, PixelType.Float
                ]

        module TextureFormat =
//            let ofFormatAndType =
//                LookupTable.lookupTable [
//                     (PixelFormat.Bgr, PixelType.UnsignedByte), TextureFormat.Bgr8 
//                     (PixelFormat.Bgra, PixelType.UnsignedByte), TextureFormat.Bgra8 
//                     (PixelFormat.Rgb, PixelType.UnsignedByte), TextureFormat.Rgb8 
//                     (PixelFormat.Rgb, PixelType.UnsignedShort), TextureFormat.Rgb16 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.Rgba8 
//                     (PixelFormat.Rgba, PixelType.UnsignedInt1010102), TextureFormat.Rgb10A2 
//                     (PixelFormat.Rgba, PixelType.UnsignedShort), TextureFormat.Rgba16 
//                     (PixelFormat.DepthComponent, PixelType.HalfFloat), TextureFormat.DepthComponent16 
//                     (PixelFormat.DepthComponent, PixelType.Float), TextureFormat.DepthComponent24 
//                     //(PixelFormat.DepthComponent, PixelType.Float), TextureFormat.DepthComponent32 
//                     (PixelFormat.Red, PixelType.UnsignedByte), TextureFormat.CompressedRed 
//                     (PixelFormat.Rg, PixelType.UnsignedByte), TextureFormat.CompressedRg 
//                     (PixelFormat.Red, PixelType.UnsignedByte), TextureFormat.R8 
//                     (PixelFormat.Red, PixelType.UnsignedShort), TextureFormat.R16 
//                     (PixelFormat.Rg, PixelType.UnsignedByte), TextureFormat.Rg8 
//                     (PixelFormat.Rg, PixelType.UnsignedShort), TextureFormat.Rg16 
//                     (PixelFormat.Red, PixelType.HalfFloat), TextureFormat.R16f 
//                     (PixelFormat.Red, PixelType.Float), TextureFormat.R32f 
//                     (PixelFormat.Rg, PixelType.HalfFloat), TextureFormat.Rg16f 
//                     (PixelFormat.Rg, PixelType.Float), TextureFormat.Rg32f 
//                     (PixelFormat.Red, PixelType.Byte), TextureFormat.R8i 
//                     (PixelFormat.Red, PixelType.UnsignedByte), TextureFormat.R8ui 
//                     (PixelFormat.Red, PixelType.Short), TextureFormat.R16i 
//                     (PixelFormat.Red, PixelType.UnsignedShort), TextureFormat.R16ui 
//                     (PixelFormat.Red, PixelType.Int), TextureFormat.R32i 
//                     (PixelFormat.Red, PixelType.UnsignedInt), TextureFormat.R32ui 
//                     (PixelFormat.Rg, PixelType.Byte), TextureFormat.Rg8i 
//                     (PixelFormat.Rg, PixelType.UnsignedByte), TextureFormat.Rg8ui 
//                     (PixelFormat.Rg, PixelType.Short), TextureFormat.Rg16i 
//                     (PixelFormat.Rg, PixelType.UnsignedShort), TextureFormat.Rg16ui 
//                     (PixelFormat.Rg, PixelType.Int), TextureFormat.Rg32i 
//                     (PixelFormat.Rg, PixelType.UnsignedInt), TextureFormat.Rg32ui 
//                     (PixelFormat.Rgb, PixelType.UnsignedByte), TextureFormat.CompressedRgbS3tcDxt1Ext 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedRgbaS3tcDxt1Ext 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedRgbaS3tcDxt3Ext 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedRgbaS3tcDxt5Ext 
//                     (PixelFormat.Alpha, PixelType.UnsignedByte), TextureFormat.CompressedAlpha 
//                     (PixelFormat.Luminance, PixelType.UnsignedByte), TextureFormat.CompressedLuminance 
//                     (PixelFormat.LuminanceAlpha, PixelType.UnsignedByte), TextureFormat.CompressedLuminanceAlpha 
//                     (PixelFormat.Rgb, PixelType.UnsignedByte), TextureFormat.CompressedRgb 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedRgba 
//                     (PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev), TextureFormat.DepthStencil 
//
//                     (PixelFormat.Rgba, PixelType.Float), TextureFormat.Rgba32f 
//                     (PixelFormat.Rgb, PixelType.Float), TextureFormat.Rgb32f 
//                     (PixelFormat.Rgba, PixelType.HalfFloat), TextureFormat.Rgba16f 
//                     (PixelFormat.Rgb, PixelType.HalfFloat), TextureFormat.Rgb16f 
//                     (PixelFormat.DepthComponent, PixelType.Float32UnsignedInt248Rev), TextureFormat.Depth24Stencil8 
//                     (PixelFormat.Rgb, PixelType.UnsignedByte), TextureFormat.Srgb 
//                     (PixelFormat.Rgb, PixelType.UnsignedByte), TextureFormat.Srgb8 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.SrgbAlpha 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.Srgb8Alpha8 
//
//                     (PixelFormat.Rgb, PixelType.UnsignedByte), TextureFormat.CompressedSrgb 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedSrgbAlpha 
//                     (PixelFormat.Rgb, PixelType.UnsignedByte), TextureFormat.CompressedSrgbS3tcDxt1Ext 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext 
//                     //(PixelFormat.DepthComponent, PixelType.Float), TextureFormat.DepthComponent32f 
//                     (PixelFormat.DepthComponent, PixelType.Float), TextureFormat.Depth32fStencil8 
//                     (PixelFormat.Rgba, PixelType.UnsignedInt), TextureFormat.Rgba32ui 
//                     (PixelFormat.Rgb, PixelType.UnsignedInt), TextureFormat.Rgb32ui 
//                     (PixelFormat.Rgba, PixelType.UnsignedShort), TextureFormat.Rgba16ui 
//                     (PixelFormat.Rgb, PixelType.UnsignedShort), TextureFormat.Rgb16ui 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.Rgba8ui 
//                     (PixelFormat.Rgb, PixelType.UnsignedByte), TextureFormat.Rgb8ui 
//                     (PixelFormat.Rgba, PixelType.Int), TextureFormat.Rgba32i 
//                     (PixelFormat.Rgb, PixelType.Int), TextureFormat.Rgb32i 
//                     (PixelFormat.Rgba, PixelType.Short), TextureFormat.Rgba16i 
//                     (PixelFormat.Rgb, PixelType.Short), TextureFormat.Rgb16i 
//                     (PixelFormat.Rgba, PixelType.Byte), TextureFormat.Rgba8i 
//                     (PixelFormat.Rgb, PixelType.Byte), TextureFormat.Rgb8i 
//                     (PixelFormat.DepthComponent, PixelType.Float32UnsignedInt248Rev), TextureFormat.Float32UnsignedInt248Rev 
//                     (PixelFormat.Red, PixelType.UnsignedByte), TextureFormat.CompressedRedRgtc1 
//                     (PixelFormat.Red, PixelType.Byte), TextureFormat.CompressedSignedRedRgtc1 
//                     (PixelFormat.Rg, PixelType.UnsignedByte), TextureFormat.CompressedRgRgtc2 
//                     (PixelFormat.Rg, PixelType.Byte), TextureFormat.CompressedSignedRgRgtc2 
//                     (PixelFormat.Rgba, PixelType.UnsignedByte), TextureFormat.CompressedRgbaBptcUnorm 
//                     (PixelFormat.Rgb, PixelType.Float), TextureFormat.CompressedRgbBptcSignedFloat 
//                     (PixelFormat.Rgb, PixelType.Float), TextureFormat.CompressedRgbBptcUnsignedFloat 
//                     (PixelFormat.Red, PixelType.Byte), TextureFormat.R8Snorm 
//                     (PixelFormat.Rg, PixelType.Byte), TextureFormat.Rg8Snorm 
//                     (PixelFormat.Rgb, PixelType.Byte), TextureFormat.Rgb8Snorm 
//                     (PixelFormat.Rgba, PixelType.Byte), TextureFormat.Rgba8Snorm 
//                     (PixelFormat.Red, PixelType.Short), TextureFormat.R16Snorm 
//                     (PixelFormat.Rg, PixelType.Short), TextureFormat.Rg16Snorm 
//                     (PixelFormat.Rgb, PixelType.Short), TextureFormat.Rgb16Snorm 
//                     (PixelFormat.Rgba, PixelType.Short), TextureFormat.Rgba16Snorm 
//                ]

            let toFormatAndType =
                LookupTable.lookupTable [
                    TextureFormat.Bgr8 , (PixelFormat.Bgr, PixelType.UnsignedByte)
                    TextureFormat.Bgra8 , (PixelFormat.Bgra, PixelType.UnsignedByte)
                    TextureFormat.Rgb8 , (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.Rgb16 , (PixelFormat.Rgb, PixelType.UnsignedShort)
                    TextureFormat.Rgba8 , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.Rgb10A2 , (PixelFormat.Rgba, PixelType.UnsignedInt1010102)
                    TextureFormat.Rgba16 , (PixelFormat.Rgba, PixelType.UnsignedShort)

                    TextureFormat.DepthComponent16 , (PixelFormat.DepthComponent, PixelType.HalfFloat)
                    TextureFormat.DepthComponent24 , (PixelFormat.DepthComponent, PixelType.Float)
                    TextureFormat.DepthComponent32 , (PixelFormat.DepthComponent, PixelType.Float)
                    TextureFormat.CompressedRed , (PixelFormat.Red, PixelType.UnsignedByte)
                    TextureFormat.CompressedRg , (PixelFormat.Rg, PixelType.UnsignedByte)
                    TextureFormat.R8 , (PixelFormat.Red, PixelType.UnsignedByte)
                    TextureFormat.R16 , (PixelFormat.Red, PixelType.UnsignedShort)
                    TextureFormat.Rg8 , (PixelFormat.Rg, PixelType.UnsignedByte)
                    TextureFormat.Rg16 , (PixelFormat.Rg, PixelType.UnsignedShort)
                    TextureFormat.R16f , (PixelFormat.Red, PixelType.HalfFloat)
                    TextureFormat.R32f , (PixelFormat.Red, PixelType.Float)
                    TextureFormat.Rg16f , (PixelFormat.Rg, PixelType.HalfFloat)
                    TextureFormat.Rg32f , (PixelFormat.Rg, PixelType.Float)
                    TextureFormat.R8i , (PixelFormat.Red, PixelType.Byte)
                    TextureFormat.R8ui , (PixelFormat.Red, PixelType.UnsignedByte)
                    TextureFormat.R16i , (PixelFormat.Red, PixelType.Short)
                    TextureFormat.R16ui , (PixelFormat.Red, PixelType.UnsignedShort)
                    TextureFormat.R32i , (PixelFormat.Red, PixelType.Int)
                    TextureFormat.R32ui , (PixelFormat.Red, PixelType.UnsignedInt)
                    TextureFormat.Rg8i , (PixelFormat.Rg, PixelType.Byte)
                    TextureFormat.Rg8ui , (PixelFormat.Rg, PixelType.UnsignedByte)
                    TextureFormat.Rg16i , (PixelFormat.Rg, PixelType.Short)
                    TextureFormat.Rg16ui , (PixelFormat.Rg, PixelType.UnsignedShort)
                    TextureFormat.Rg32i , (PixelFormat.Rg, PixelType.Int)
                    TextureFormat.Rg32ui , (PixelFormat.Rg, PixelType.UnsignedInt)
                    TextureFormat.CompressedRgbS3tcDxt1Ext , (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgbaS3tcDxt1Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgbaS3tcDxt3Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgbaS3tcDxt5Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedAlpha , (PixelFormat.Alpha, PixelType.UnsignedByte)
                    TextureFormat.CompressedLuminance , (PixelFormat.Luminance, PixelType.UnsignedByte)
                    TextureFormat.CompressedLuminanceAlpha , (PixelFormat.LuminanceAlpha, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgb , (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgba , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.DepthStencil , (PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev)

                    TextureFormat.Rgba32f , (PixelFormat.Rgba, PixelType.Float)
                    TextureFormat.Rgb32f , (PixelFormat.Rgb, PixelType.Float)
                    TextureFormat.Rgba16f , (PixelFormat.Rgba, PixelType.HalfFloat)
                    TextureFormat.Rgb16f , (PixelFormat.Rgb, PixelType.HalfFloat)
                    TextureFormat.Depth24Stencil8 , (PixelFormat.DepthComponent, PixelType.Float32UnsignedInt248Rev)
                    TextureFormat.Srgb , (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.Srgb8 , (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.SrgbAlpha , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.Srgb8Alpha8 , (PixelFormat.Rgba, PixelType.UnsignedByte)

                    TextureFormat.CompressedSrgb , (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbAlpha , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbS3tcDxt1Ext , (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.DepthComponent32f , (PixelFormat.DepthComponent, PixelType.Float)
                    TextureFormat.Depth32fStencil8 , (PixelFormat.DepthComponent, PixelType.Float)
                    TextureFormat.Rgba32ui , (PixelFormat.Rgba, PixelType.UnsignedInt)
                    TextureFormat.Rgb32ui , (PixelFormat.Rgb, PixelType.UnsignedInt)
                    TextureFormat.Rgba16ui , (PixelFormat.Rgba, PixelType.UnsignedShort)
                    TextureFormat.Rgb16ui , (PixelFormat.Rgb, PixelType.UnsignedShort)
                    TextureFormat.Rgba8ui , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.Rgb8ui , (PixelFormat.Rgb, PixelType.UnsignedByte)
                    TextureFormat.Rgba32i , (PixelFormat.Rgba, PixelType.Int)
                    TextureFormat.Rgb32i , (PixelFormat.Rgb, PixelType.Int)
                    TextureFormat.Rgba16i , (PixelFormat.Rgba, PixelType.Short)
                    TextureFormat.Rgb16i , (PixelFormat.Rgb, PixelType.Short)
                    TextureFormat.Rgba8i , (PixelFormat.Rgba, PixelType.Byte)
                    TextureFormat.Rgb8i , (PixelFormat.Rgb, PixelType.Byte)
                    TextureFormat.Float32UnsignedInt248Rev , (PixelFormat.DepthComponent, PixelType.Float32UnsignedInt248Rev)
                    TextureFormat.CompressedRedRgtc1 , (PixelFormat.Red, PixelType.UnsignedByte)
                    TextureFormat.CompressedSignedRedRgtc1 , (PixelFormat.Red, PixelType.Byte)
                    TextureFormat.CompressedRgRgtc2 , (PixelFormat.Rg, PixelType.UnsignedByte)
                    TextureFormat.CompressedSignedRgRgtc2 , (PixelFormat.Rg, PixelType.Byte)
                    TextureFormat.CompressedRgbaBptcUnorm , (PixelFormat.Rgba, PixelType.UnsignedByte)
                    TextureFormat.CompressedRgbBptcSignedFloat , (PixelFormat.Rgb, PixelType.Float)
                    TextureFormat.CompressedRgbBptcUnsignedFloat , (PixelFormat.Rgb, PixelType.Float)
                    TextureFormat.R8Snorm , (PixelFormat.Red, PixelType.Byte)
                    TextureFormat.Rg8Snorm , (PixelFormat.Rg, PixelType.Byte)
                    TextureFormat.Rgb8Snorm , (PixelFormat.Rgb, PixelType.Byte)
                    TextureFormat.Rgba8Snorm , (PixelFormat.Rgba, PixelType.Byte)
                    TextureFormat.R16Snorm , (PixelFormat.Red, PixelType.Short)
                    TextureFormat.Rg16Snorm , (PixelFormat.Rg, PixelType.Short)
                    TextureFormat.Rgb16Snorm , (PixelFormat.Rgb, PixelType.Short)
                    TextureFormat.Rgba16Snorm , (PixelFormat.Rgba, PixelType.Short)

                ]

    [<AutoOpen>]
    module DevilExtensions =
        open DevILSharp

        module PixelFormat =
        
//            let channels =
//                LookupTable.lookupTable [
//                    PixelFormat.Bgr, 3
//                    PixelFormat.Bgra, 4
//                    PixelFormat.Red, 1
//                    PixelFormat.Rg, 2
//                    PixelFormat.Rgb, 3
//                    PixelFormat.Rgba, 4
//                ]
//
//            let ofColFormat =
//                LookupTable.lookupTable [
//                    Col.Format.Alpha, PixelFormat.Red
//                    Col.Format.BGR, PixelFormat.Bgr
//                    Col.Format.BGRA, PixelFormat.Bgra
//                    Col.Format.BGRP, PixelFormat.Bgra
//                    Col.Format.BW, PixelFormat.Red
//                    Col.Format.Gray, PixelFormat.Red
//                    Col.Format.GrayAlpha, PixelFormat.Rg
//                    Col.Format.NormalUV, PixelFormat.Rg
//                    Col.Format.RGB, PixelFormat.Rgb
//                    Col.Format.RGBA, PixelFormat.Rgba
//                    Col.Format.RGBP, PixelFormat.Rgba
//                ]

            let ofDevil =
                LookupTable.lookupTable [
                    ChannelFormat.RGB, PixelFormat.Rgb
                    ChannelFormat.BGR, PixelFormat.Bgr
                    ChannelFormat.RGBA, PixelFormat.Rgba
                    ChannelFormat.BGRA, PixelFormat.Bgra
                    ChannelFormat.Luminance, PixelFormat.Red
                    ChannelFormat.Alpha, PixelFormat.Red
                    ChannelFormat.LuminanceAlpha, PixelFormat.Rg

                ]

        module PixelType =

            let size =
                LookupTable.lookupTable [
                    PixelType.UnsignedByte, 1
                    PixelType.Byte, 1
                    PixelType.UnsignedShort, 2
                    PixelType.Short, 2
                    PixelType.UnsignedInt, 4
                    PixelType.Int, 4
                    PixelType.HalfFloat, 2
                    PixelType.Float, 4
                ]

            let ofType =
                LookupTable.lookupTable [
                    typeof<uint8>, PixelType.UnsignedByte
                    typeof<int8>, PixelType.Byte
                    typeof<uint16>, PixelType.UnsignedShort
                    typeof<int16>, PixelType.Short
                    typeof<uint32>, PixelType.UnsignedInt
                    typeof<int32>, PixelType.Int
                    typeof<float16>, PixelType.HalfFloat
                    typeof<float32>, PixelType.Float
                ]

            let ofDevil =
                LookupTable.lookupTable [
                    ChannelType.Byte, PixelType.Byte
                    //ChannelType.Double, PixelType.Double
                    ChannelType.Float, PixelType.Float
                    ChannelType.Half, PixelType.HalfFloat
                    ChannelType.Int, PixelType.Int
                    ChannelType.Short, PixelType.Short
                    ChannelType.UnsignedByte, PixelType.UnsignedByte
                    ChannelType.UnsignedInt, PixelType.UnsignedInt
                    ChannelType.UnsignedShort, PixelType.UnsignedShort
                ]

        module IL =
            let lockObj = 
                let fi = typeof<PixImage>.GetField("s_devilLock", Reflection.BindingFlags.Static ||| Reflection.BindingFlags.NonPublic)
                fi.GetValue(null)

            let GetPixelType() = IL.GetDataType() |> PixelType.ofDevil

            let GetPixelFormat() = IL.GetInteger(IntName.ImageFormat) |> unbox |> PixelFormat.ofDevil

            let PinBPO(align : int, trafo : ImageTrafo, f : V2i -> PixelType -> PixelFormat-> nativeint -> unit) =
                let align = nativeint align
                let mask = align - 1n

                let data = IL.GetData()
                let w = IL.GetInteger(IntName.ImageWidth)
                let h = IL.GetInteger(IntName.ImageHeight)
                let pt = GetPixelType()
                let pf = GetPixelFormat()

                let elementSize = PixelType.size pt
                let channels = PixelFormat.channels pf

                let lineSize = nativeint w * nativeint elementSize * nativeint channels

                let alignedLineSize =
                    if lineSize % align = 0n then lineSize
                    else (lineSize + mask) &&& ~~~mask

                let sizeInBytes = alignedLineSize * nativeint h

                let pbo = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
                GL.BufferData(BufferTarget.PixelUnpackBuffer, sizeInBytes, 0n, BufferUsageHint.DynamicDraw)

                let dst = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, sizeInBytes, BufferAccessMask.MapWriteBit)
                
                let viSize = V3l(int64 w, int64 h, int64 channels)
                let dstInfo =
                    match trafo with
                        | ImageTrafo.Rot0 -> 
                            VolumeInfo(
                                0L,
                                viSize,
                                V3l(int64 channels * int64 elementSize, int64 alignedLineSize, int64 elementSize)
                            )

                        | ImageTrafo.MirrorY -> 
                            VolumeInfo(
                                int64 alignedLineSize * (int64 h - 1L),
                                viSize,
                                V3l(int64 channels * int64 elementSize, int64 -alignedLineSize, int64 elementSize)
                            )

                        | ImageTrafo.MirrorX ->
                            VolumeInfo(
                                int64 w - 1L,
                                viSize,
                                V3l(int64 -channels * int64 elementSize, int64 alignedLineSize, int64 elementSize)
                            )

                        | ImageTrafo.Rot180 ->
                            VolumeInfo(
                                int64 alignedLineSize * (int64 h - 1L) + int64 w - 1L,
                                viSize,
                                V3l(int64 -channels * int64 elementSize, int64 -alignedLineSize, int64 elementSize)
                            )

                        | _ -> 
                            failwithf "[GL] only supports ImageTrafo.[Rot0|MirrorY|MirrorX|Rot180] atm. but got %A" trafo

                let srcInfo =
                    VolumeInfo(
                        0L,
                        viSize,
                        V3l(int64 channels * int64 elementSize, int64 alignedLineSize, int64 elementSize)
                    )

                TextureCopyUtils.Copy(elementSize, data, srcInfo, dst, dstInfo)

                GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore
                f (V2i(w,h)) pt pf sizeInBytes

                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                GL.DeleteBuffer(pbo)


                
                ()
  
    type PixImage with
        member image.PinPBO(align : int, trafo : ImageTrafo, f : V2i -> PixelType -> PixelFormat -> nativeint -> unit) =
            let pt = PixelType.ofType image.PixFormat.Type
            let pf = PixelFormat.ofColFormat image.Format
            
            let align = align |> nativeint
            let mask = align - 1n |> nativeint
            let size = image.Size
            let elementSize = image.PixFormat.Type.GLSize
            let channels = toChannelCount image.Format

            let lineSize = nativeint size.X * nativeint elementSize * nativeint channels

            let alignedLineSize =
                if lineSize % align = 0n then lineSize
                else (lineSize + mask) &&& ~~~mask

            let sizeInBytes = alignedLineSize * nativeint size.Y

            let pbo = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
            GL.Check "could not bind PBO"

            GL.BufferData(BufferTarget.PixelUnpackBuffer, sizeInBytes, 0n, BufferUsageHint.DynamicDraw)
            GL.Check "could not initialize PBO"

            let dst = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, sizeInBytes, BufferAccessMask.MapWriteBit)
            GL.Check "could not map PBO"

            let dstInfo =
                let viSize = V3l(int64 size.X, int64 size.Y, int64 channels)
                match trafo with
                    | ImageTrafo.Rot0 -> 
                        VolumeInfo(
                            0L,
                            viSize,
                            V3l(int64 channels * int64 elementSize, int64 alignedLineSize, int64 elementSize)
                        )

                    | ImageTrafo.MirrorY -> 
                        VolumeInfo(
                            int64 alignedLineSize * (int64 size.Y - 1L),
                            viSize,
                            V3l(int64 channels * int64 elementSize, int64 -alignedLineSize, int64 elementSize)
                        )

                    | ImageTrafo.MirrorX ->
                        VolumeInfo(
                            int64 size.X - 1L,
                            viSize,
                            V3l(int64 -channels * int64 elementSize, int64 alignedLineSize, int64 elementSize)
                        )

                    | ImageTrafo.Rot180 ->
                        VolumeInfo(
                            int64 alignedLineSize * (int64 size.Y - 1L) + int64 size.X - 1L,
                            viSize,
                            V3l(int64 -channels * int64 elementSize, int64 -alignedLineSize, int64 elementSize)
                        )

                    | _ -> 
                        failwithf "[GL] only supports ImageTrafo.[Rot0|MirrorY|MirrorX|Rot180] atm. but got %A" trafo

           

            TextureCopyUtils.Copy(image, dst, dstInfo)

            let worked = GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer)
            if not worked then Log.warn "[GL] could not unmap buffer"
            GL.Check "could not unmap PBO"
            f size pt pf sizeInBytes

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.Check "could not unbind PBO"

            GL.DeleteBuffer(pbo)
            GL.Check "could not delete PBO"




            ()

    type PixVolume with
        member x.PinPBO(align : int, f : V3i -> PixelType -> PixelFormat -> nativeint -> unit) =
            let size = x.Size
            let pt = PixelType.ofType x.PixFormat.Type
            let pf = PixelFormat.ofColFormat x.Format
            
            let align = align |> nativeint
            let alignMask = align - 1n |> nativeint
            let channelSize = x.PixFormat.Type.GLSize |> nativeint
            let channels = toChannelCount x.Format |> nativeint

            let pixelSize = channelSize * channels

            let rowSize = pixelSize * nativeint size.X
            let alignedRowSize = (rowSize + (alignMask - 1n)) &&& ~~~alignMask
            let sizeInBytes = alignedRowSize * nativeint size.Y * nativeint size.Z


            let pbo = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
            GL.BufferData(BufferTarget.PixelUnpackBuffer, sizeInBytes, 0n, BufferUsageHint.DynamicDraw)
            let pDst = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, sizeInBytes, BufferAccessMask.MapWriteBit)


            if alignedRowSize % channelSize <> 0n then
                failwith "[GL] unexpected row alignment (not implemented atm.)"

            let dstInfo =
                let rowPixels = alignedRowSize / channelSize
                let viSize = V4l(int64 size.X, int64 size.Y, int64 size.Z, int64 channels)
                Tensor4Info(
                    0L,
                    viSize,
                    V4l(
                        int64 channels, 
                        int64 rowPixels, 
                        int64 rowPixels * viSize.Y, 
                        1L
                    )
                )

            let elementType = x.PixFormat.Type

           
            elementType |> ExistentialHack.run {
                new IUnmanagedAction with
                    member __.Run(def : Option<'a>) =
                        let x = unbox<PixVolume<'a>> x
                        let dst = NativeTensor4<'a>(NativePtr.ofNativeInt pDst, dstInfo)
                        NativeTensor4.using x.Tensor4 (fun src ->
                            src.CopyTo(dst)
                        )
            }

            GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore
            f size pt pf sizeInBytes

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.DeleteBuffer(pbo)

    type PBOInfo =
        {
            size        : V3i
            flags       : BufferStorageFlags
            pixelFormat : PixelFormat
            pixelType   : PixelType
        }

    module NativeTensor4 =

        let private pixelFormat =
            LookupTable.lookupTable [
                1L, PixelFormat.Red
                2L, PixelFormat.Rg
                3L, PixelFormat.Rgb
                4L, PixelFormat.Rgba
            ]

        let withPBO (x : NativeTensor4<'a>) (align : int) (f : V3i -> PixelType -> PixelFormat -> nativeint -> unit) =
            let size = x.Info.Size
            let pt = PixelType.ofType typeof<'a>
            let pf = pixelFormat size.W
            
            let align = align |> nativeint
            let alignMask = align - 1n |> nativeint
            let channelSize = typeof<'a>.GLSize |> nativeint
            let channels = size.W |> nativeint

            let pixelSize = channelSize * channels

            let rowSize = pixelSize * nativeint size.X
            let alignedRowSize = (rowSize + (alignMask - 1n)) &&& ~~~alignMask
            let sizeInBytes = alignedRowSize * nativeint size.Y * nativeint size.Z

            if alignedRowSize % channelSize <> 0n then
                failwith "[GL] unexpected row alignment (not implemented atm.)"

            let dstInfo =
                let rowPixels = alignedRowSize / channelSize
                let viSize = V4l(int64 size.X, int64 size.Y, int64 size.Z, int64 channels)
                Tensor4Info(
                    0L,
                    viSize,
                    V4l(
                        int64 channels, 
                        int64 rowPixels, 
                        int64 rowPixels * viSize.Y, 
                        1L
                    )
                )

            let pbo = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
            GL.Check "could not bind PBO"
            GL.BufferStorage(BufferTarget.PixelUnpackBuffer, sizeInBytes, 0n, BufferStorageFlags.MapWriteBit)
            GL.Check "could not allocate PBO"
            let pDst = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, sizeInBytes, BufferAccessMask.MapWriteBit)
            GL.Check "could not map PBO"
            if pDst = 0n then failwith "[GL] could not map PBO"

            let dst = NativeTensor4<'a>(NativePtr.ofNativeInt pDst, dstInfo)
            x.CopyTo(dst)

            let worked = GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer)
            if not worked then failwith "[GL] could not unmap PBO"

            f (V3i size.XYZ) pt pf sizeInBytes

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.Check "could not unbind PBO"

            GL.DeleteBuffer(pbo)
            GL.Check "could not delete PBO"

        let usePBO (info : PBOInfo) (align : int) (mapping : int -> nativeint -> Tensor4Info -> 'r) =
            let size = info.size
            let pt = info.pixelType
            let pf = info.pixelFormat
            
            let align = align |> nativeint
            let alignMask = align - 1n |> nativeint
            let channelSize = PixelType.size pt |> nativeint
            let channels = PixelFormat.channels pf |> nativeint

            let pixelSize = channelSize * channels

            let rowSize = pixelSize * nativeint size.X
            let alignedRowSize = (rowSize + (alignMask - 1n)) &&& ~~~alignMask
            let sizeInBytes = alignedRowSize * nativeint size.Y * nativeint size.Z
            
            if alignedRowSize % channelSize <> 0n then
                failwith "[GL] unexpected row alignment (not implemented atm.)"

            let srcInfo =
                let rowPixels = alignedRowSize / channelSize
                let viSize = V4l(int64 size.X, int64 size.Y, int64 size.Z, int64 channels)
                Tensor4Info(
                    0L,
                    viSize,
                    V4l(
                        int64 channels, 
                        int64 rowPixels, 
                        int64 rowPixels * viSize.Y, 
                        1L
                    )
                )
                
            let pbo = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, pbo)
            GL.Check "could not bind PBO"
            GL.BufferStorage(BufferTarget.CopyWriteBuffer, sizeInBytes, 0n, BufferStorageFlags.MapReadBit)
            GL.Check "could not allocate PBO"
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0)

            try 
                mapping pbo sizeInBytes srcInfo

            finally
                GL.DeleteBuffer(pbo)
                GL.Check "could not delete PBO"
                


    type Context with
        
        member x.Upload(texture : Texture, data : PixImageMipMap, config : TextureParams) =
            using x.ResourceLock (fun _ ->
                if data.LevelCount < 1 then
                    failwith "[GL] cannot upload empty PixImageMipMap"

                let levels = data.LevelCount
                let level0 = data.[0]
                let size = level0.Size
                let expectedLevels = min size.X size.Y |> Fun.Log2 |> ceil |> int 
                let generateMipMaps = data.LevelCount = 1 && config.wantMipMaps

                for level in 0 .. levels - 1 do
                    let image = data.[level]
                    ()


                ()
            )





[<AutoOpen>]
module TextureExtensions =

    let addTexture (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.TextureCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,size) |> ignore

    let removeTexture (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.TextureCount)  |> ignore
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,-size) |> ignore

    let updateTexture (ctx:Context) oldSize newSize =
        Interlocked.Add(&ctx.MemoryUsage.TextureMemory,newSize-oldSize) |> ignore


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

        let compressedFormats =
            HashSet.ofList [
                TextureFormat.CompressedRed
                TextureFormat.CompressedRg
                TextureFormat.CompressedRgbS3tcDxt1Ext
                TextureFormat.CompressedRgbaS3tcDxt1Ext
                TextureFormat.CompressedRgbaS3tcDxt3Ext
                TextureFormat.CompressedRgbaS3tcDxt5Ext
                TextureFormat.CompressedAlpha
                TextureFormat.CompressedLuminance
                TextureFormat.CompressedLuminanceAlpha
                TextureFormat.CompressedIntensity
                TextureFormat.CompressedRgb
                TextureFormat.CompressedRgba
                TextureFormat.CompressedSrgb
                TextureFormat.CompressedSrgbAlpha
                TextureFormat.CompressedSluminance
                TextureFormat.CompressedSluminanceAlpha
                TextureFormat.CompressedSrgbS3tcDxt1Ext
                TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext
                TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext
                TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext
                TextureFormat.CompressedRedRgtc1
                TextureFormat.CompressedSignedRedRgtc1
                TextureFormat.CompressedRgRgtc2
                TextureFormat.CompressedSignedRgRgtc2
                TextureFormat.CompressedRgbaBptcUnorm
                TextureFormat.CompressedRgbBptcSignedFloat
                TextureFormat.CompressedRgbBptcUnsignedFloat
            ]

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

    type Col.Format with
        static member Stencil = unbox<Col.Format> (Int32.MaxValue)
        static member Depth = unbox<Col.Format> (Int32.MaxValue - 1)

    let internal toPixelType =
        lookupTable [
            typeof<uint8>, PixelType.UnsignedByte
            typeof<int8>, PixelType.Byte
            typeof<uint16>, PixelType.UnsignedShort
            typeof<int16>, PixelType.Short
            typeof<uint32>, PixelType.UnsignedInt
            typeof<int32>, PixelType.Int
            typeof<float32>, PixelType.Float
            typeof<float16>, PixelType.HalfFloat


        ]

    let internal toPixelFormat =
        lookupTable [
        
            Col.Format.Alpha, PixelFormat.Alpha
            Col.Format.BW, PixelFormat.Red
            Col.Format.Gray, PixelFormat.Red
            Col.Format.GrayAlpha, PixelFormat.Rg
            Col.Format.RGB, PixelFormat.Rgb
            Col.Format.BGR, PixelFormat.Bgr
            Col.Format.RGBA, PixelFormat.Rgba
            Col.Format.BGRA, PixelFormat.Bgra
            Col.Format.RGBP, PixelFormat.Rgba
            Col.Format.NormalUV, PixelFormat.Rg
            Col.Format.Stencil, PixelFormat.StencilIndex
            Col.Format.Depth, PixelFormat.DepthComponent
        ]

    let toUntypedPixelFormat =
        lookupTable [
            TextureFormat.DepthComponent16, PixelFormat.DepthComponent
            TextureFormat.Depth24Stencil8, PixelFormat.DepthComponent
            TextureFormat.DepthComponent32, PixelFormat.DepthComponent
            TextureFormat.DepthComponent32f, PixelFormat.DepthComponent

            TextureFormat.Rgba8, PixelFormat.Rgba
            TextureFormat.Rgba16, PixelFormat.Rgba
            TextureFormat.Rgba16f, PixelFormat.Rgba
            TextureFormat.Rgba32f, PixelFormat.Rgba

            TextureFormat.Rgb8, PixelFormat.Rgb
            TextureFormat.Rgb16, PixelFormat.Rgb
            TextureFormat.Rgb16f, PixelFormat.Rgb
            TextureFormat.Rgb32f, PixelFormat.Rgb

            TextureFormat.Rg8, PixelFormat.Rg
            TextureFormat.Rg16, PixelFormat.Rg
            TextureFormat.Rg16f, PixelFormat.Rg
            TextureFormat.Rg32f, PixelFormat.Rg

            TextureFormat.R8, PixelFormat.Red
            TextureFormat.R16, PixelFormat.Red
            TextureFormat.R16f, PixelFormat.Red
            TextureFormat.R32f, PixelFormat.Red

            TextureFormat.CompressedRgbS3tcDxt1Ext, PixelFormat.Rgb
            TextureFormat.CompressedRgbaS3tcDxt5Ext, PixelFormat.Rgba
            TextureFormat.CompressedSrgbS3tcDxt1Ext, PixelFormat.Rgb
            TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext, PixelFormat.Rgba
        ]

    let internal toChannelCount =
        lookupTable [
            Col.Format.Alpha, 1
            Col.Format.BW, 1
            Col.Format.Gray, 1
            Col.Format.GrayAlpha, 2
            Col.Format.RGB, 3
            Col.Format.BGR, 3
            Col.Format.RGBA, 4
            Col.Format.BGRA, 4
            Col.Format.RGBP, 4
            Col.Format.NormalUV, 2
            Col.Format.Stencil, 1
            Col.Format.Depth, 1
        ]


    let internal toFormatAndType =
        LookupTable.lookupTable [
            TextureFormat.Bgr8 , (PixelFormat.Bgr, PixelType.UnsignedByte)
            TextureFormat.Bgra8 , (PixelFormat.Bgra, PixelType.UnsignedByte)
            TextureFormat.Rgb8 , (PixelFormat.Rgb, PixelType.UnsignedByte)
            TextureFormat.Rgb16 , (PixelFormat.Rgb, PixelType.UnsignedShort)
            TextureFormat.Rgba8 , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.Rgb10A2 , (PixelFormat.Rgba, PixelType.UnsignedInt1010102)
            TextureFormat.Rgba16 , (PixelFormat.Rgba, PixelType.UnsignedShort)

            TextureFormat.DepthComponent16 , (PixelFormat.DepthComponent, PixelType.HalfFloat)
            TextureFormat.DepthComponent24 , (PixelFormat.DepthComponent, PixelType.Float)
            TextureFormat.DepthComponent32 , (PixelFormat.DepthComponent, PixelType.Float)
            TextureFormat.CompressedRed , (PixelFormat.Red, PixelType.UnsignedByte)
            TextureFormat.CompressedRg , (PixelFormat.Rg, PixelType.UnsignedByte)
            TextureFormat.R8 , (PixelFormat.Red, PixelType.UnsignedByte)
            TextureFormat.R16 , (PixelFormat.Red, PixelType.UnsignedShort)
            TextureFormat.Rg8 , (PixelFormat.Rg, PixelType.UnsignedByte)
            TextureFormat.Rg16 , (PixelFormat.Rg, PixelType.UnsignedShort)
            TextureFormat.R16f , (PixelFormat.Red, PixelType.HalfFloat)
            TextureFormat.R32f , (PixelFormat.Red, PixelType.Float)
            TextureFormat.Rg16f , (PixelFormat.Rg, PixelType.HalfFloat)
            TextureFormat.Rg32f , (PixelFormat.Rg, PixelType.Float)
            TextureFormat.R8i , (PixelFormat.Red, PixelType.Byte)
            TextureFormat.R8ui , (PixelFormat.Red, PixelType.UnsignedByte)
            TextureFormat.R16i , (PixelFormat.Red, PixelType.Short)
            TextureFormat.R16ui , (PixelFormat.Red, PixelType.UnsignedShort)
            TextureFormat.R32i , (PixelFormat.Red, PixelType.Int)
            TextureFormat.R32ui , (PixelFormat.Red, PixelType.UnsignedInt)
            TextureFormat.Rg8i , (PixelFormat.Rg, PixelType.Byte)
            TextureFormat.Rg8ui , (PixelFormat.Rg, PixelType.UnsignedByte)
            TextureFormat.Rg16i , (PixelFormat.Rg, PixelType.Short)
            TextureFormat.Rg16ui , (PixelFormat.Rg, PixelType.UnsignedShort)
            TextureFormat.Rg32i , (PixelFormat.Rg, PixelType.Int)
            TextureFormat.Rg32ui , (PixelFormat.Rg, PixelType.UnsignedInt)
            TextureFormat.CompressedRgbS3tcDxt1Ext , (PixelFormat.Rgb, PixelType.UnsignedByte)
            TextureFormat.CompressedRgbaS3tcDxt1Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.CompressedRgbaS3tcDxt3Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.CompressedRgbaS3tcDxt5Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.CompressedAlpha , (PixelFormat.Alpha, PixelType.UnsignedByte)
            TextureFormat.CompressedLuminance , (PixelFormat.Luminance, PixelType.UnsignedByte)
            TextureFormat.CompressedLuminanceAlpha , (PixelFormat.LuminanceAlpha, PixelType.UnsignedByte)
            TextureFormat.CompressedRgb , (PixelFormat.Rgb, PixelType.UnsignedByte)
            TextureFormat.CompressedRgba , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.DepthStencil , (PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev)

            TextureFormat.Rgba32f , (PixelFormat.Rgba, PixelType.Float)
            TextureFormat.Rgb32f , (PixelFormat.Rgb, PixelType.Float)
            TextureFormat.Rgba16f , (PixelFormat.Rgba, PixelType.HalfFloat)
            TextureFormat.Rgb16f , (PixelFormat.Rgb, PixelType.HalfFloat)
            TextureFormat.Depth24Stencil8 , (PixelFormat.DepthComponent, PixelType.Float32UnsignedInt248Rev)
            TextureFormat.Srgb , (PixelFormat.Rgb, PixelType.UnsignedByte)
            TextureFormat.Srgb8 , (PixelFormat.Rgb, PixelType.UnsignedByte)
            TextureFormat.SrgbAlpha , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.Srgb8Alpha8 , (PixelFormat.Rgba, PixelType.UnsignedByte)

            TextureFormat.CompressedSrgb , (PixelFormat.Rgb, PixelType.UnsignedByte)
            TextureFormat.CompressedSrgbAlpha , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.CompressedSrgbS3tcDxt1Ext , (PixelFormat.Rgb, PixelType.UnsignedByte)
            TextureFormat.CompressedSrgbAlphaS3tcDxt1Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.CompressedSrgbAlphaS3tcDxt3Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.CompressedSrgbAlphaS3tcDxt5Ext , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.DepthComponent32f , (PixelFormat.DepthComponent, PixelType.Float)
            TextureFormat.Depth32fStencil8 , (PixelFormat.DepthComponent, PixelType.Float)
            TextureFormat.Rgba32ui , (PixelFormat.Rgba, PixelType.UnsignedInt)
            TextureFormat.Rgb32ui , (PixelFormat.Rgb, PixelType.UnsignedInt)
            TextureFormat.Rgba16ui , (PixelFormat.Rgba, PixelType.UnsignedShort)
            TextureFormat.Rgb16ui , (PixelFormat.Rgb, PixelType.UnsignedShort)
            TextureFormat.Rgba8ui , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.Rgb8ui , (PixelFormat.Rgb, PixelType.UnsignedByte)
            TextureFormat.Rgba32i , (PixelFormat.Rgba, PixelType.Int)
            TextureFormat.Rgb32i , (PixelFormat.Rgb, PixelType.Int)
            TextureFormat.Rgba16i , (PixelFormat.Rgba, PixelType.Short)
            TextureFormat.Rgb16i , (PixelFormat.Rgb, PixelType.Short)
            TextureFormat.Rgba8i , (PixelFormat.Rgba, PixelType.Byte)
            TextureFormat.Rgb8i , (PixelFormat.Rgb, PixelType.Byte)
            TextureFormat.Float32UnsignedInt248Rev , (PixelFormat.DepthComponent, PixelType.Float32UnsignedInt248Rev)
            TextureFormat.CompressedRedRgtc1 , (PixelFormat.Red, PixelType.UnsignedByte)
            TextureFormat.CompressedSignedRedRgtc1 , (PixelFormat.Red, PixelType.Byte)
            TextureFormat.CompressedRgRgtc2 , (PixelFormat.Rg, PixelType.UnsignedByte)
            TextureFormat.CompressedSignedRgRgtc2 , (PixelFormat.Rg, PixelType.Byte)
            TextureFormat.CompressedRgbaBptcUnorm , (PixelFormat.Rgba, PixelType.UnsignedByte)
            TextureFormat.CompressedRgbBptcSignedFloat , (PixelFormat.Rgb, PixelType.Float)
            TextureFormat.CompressedRgbBptcUnsignedFloat , (PixelFormat.Rgb, PixelType.Float)
            TextureFormat.R8Snorm , (PixelFormat.Red, PixelType.Byte)
            TextureFormat.Rg8Snorm , (PixelFormat.Rg, PixelType.Byte)
            TextureFormat.Rgb8Snorm , (PixelFormat.Rgb, PixelType.Byte)
            TextureFormat.Rgba8Snorm , (PixelFormat.Rgba, PixelType.Byte)
            TextureFormat.R16Snorm , (PixelFormat.Red, PixelType.Short)
            TextureFormat.Rg16Snorm , (PixelFormat.Rg, PixelType.Short)
            TextureFormat.Rgb16Snorm , (PixelFormat.Rgb, PixelType.Short)
            TextureFormat.Rgba16Snorm , (PixelFormat.Rgba, PixelType.Short)

        ]

    module private Devil =
        open DevILSharp

        let private pixelType =
            lookupTable [
                ChannelType.Byte, PixelType.Byte
                //ChannelType.Double, PixelType.Double
                ChannelType.Float, PixelType.Float
                ChannelType.Half, PixelType.HalfFloat
                ChannelType.Int, PixelType.Int
                ChannelType.Short, PixelType.Short
                ChannelType.UnsignedByte, PixelType.UnsignedByte
                ChannelType.UnsignedInt, PixelType.UnsignedInt
                ChannelType.UnsignedShort, PixelType.UnsignedShort
            ]

        let private pixelFormat =
            lookupTable [
                ChannelFormat.RGB, PixelFormat.Rgb
                ChannelFormat.BGR, PixelFormat.Bgr
                ChannelFormat.RGBA, PixelFormat.Rgba
                ChannelFormat.BGRA, PixelFormat.Bgra
                ChannelFormat.Luminance, PixelFormat.Luminance
                ChannelFormat.Alpha, PixelFormat.Alpha
                ChannelFormat.LuminanceAlpha, PixelFormat.LuminanceAlpha

            ]

        let private compressedFormat =
            lookupTable [
                (ChannelFormat.RGB, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedRgbS3tcDxt1Ext)
                (ChannelFormat.RGBA, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt5, PixelInternalFormat.CompressedRgbaS3tcDxt5Ext)
                (ChannelFormat.RGB, ChannelType.UnsignedByte, true), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedSrgbS3tcDxt1Ext)
                (ChannelFormat.RGBA, ChannelType.UnsignedByte, true), (CompressedDataFormat.Dxt5, PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext)
                
                (ChannelFormat.BGR, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedRgbS3tcDxt1Ext)
                (ChannelFormat.BGRA, ChannelType.UnsignedByte, false), (CompressedDataFormat.Dxt5, PixelInternalFormat.CompressedRgbaS3tcDxt5Ext)
                (ChannelFormat.BGR, ChannelType.UnsignedByte, true), (CompressedDataFormat.Dxt1, PixelInternalFormat.CompressedSrgbS3tcDxt1Ext)
                (ChannelFormat.BGRA, ChannelType.UnsignedByte, true), (CompressedDataFormat.Dxt5, PixelInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext)
            ]

        module private PixFormat =
            let private types =
                lookupTable [
                    ChannelType.Byte, typeof<int8>
                    //ChannelType.Double, PixelType.Double
                    ChannelType.Float, typeof<float32>
                    ChannelType.Half, typeof<float16>
                    ChannelType.Int, typeof<int>
                    ChannelType.Short, typeof<int16>
                    ChannelType.UnsignedByte, typeof<uint8>
                    ChannelType.UnsignedInt, typeof<uint32>
                    ChannelType.UnsignedShort, typeof<uint16>
                ]

            let private colFormat =
                lookupTable [
                    ChannelFormat.RGB, Col.Format.RGB
                    ChannelFormat.BGR, Col.Format.BGR
                    ChannelFormat.RGBA, Col.Format.RGBA
                    ChannelFormat.BGRA, Col.Format.BGRA
                    ChannelFormat.Luminance, Col.Format.Gray
                    ChannelFormat.Alpha, Col.Format.Alpha
                    ChannelFormat.LuminanceAlpha, Col.Format.GrayAlpha
                ]

            let get(fmt : ChannelFormat, t : ChannelType) =
                match types t, colFormat fmt with
                    | Some t, Some fmt -> PixFormat(t, fmt) |> Some
                    | _ -> None

        let devilLock =
            let fi = typeof<PixImage>.GetField("s_devilLock", Reflection.BindingFlags.Static ||| Reflection.BindingFlags.NonPublic)
            fi.GetValue(null)

        let uploadTexture2DLevelFile (t : Texture) (level : int) (file : string) (config : TextureParams) =
            lock devilLock (fun () -> 
                PixImage.InitDevil()
                let img = IL.GenImage()
                try
                    IL.BindImage(img)
                    IL.LoadImage(file) |> IL.check "could not load image"
                

                
                    let w = IL.GetInteger(IntName.ImageWidth)
                    let h = IL.GetInteger(IntName.ImageHeight)
                    let fmt = IL.GetInteger(IntName.ImageFormat) |> unbox<DevILSharp.ChannelFormat>
                    let pt = IL.GetDataType()

                    let compressedFormat =
                        if config.wantCompressed then
                            match compressedFormat(fmt, pt, config.wantSrgb) with
                                | Some t -> Some t
                                | _ -> None
                        else
                            None


                    match compressedFormat with
                        | Some (fmt, ifmt) ->
                            ILU.FlipImage() |> IL.check "could not flip image"
                            let channels = IL.GetInteger(IntName.ImageChannels)
                            let size = IL.GetDXTCData(0n, 0, fmt)
                
                            Log.line "compression: %.2f%%" (100.0 * float size / float (w * h * channels)) 

                            let pbo = GL.GenBuffer()
                            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
                            GL.BufferStorage(BufferTarget.PixelUnpackBuffer, nativeint size, 0n, BufferStorageFlags.MapWriteBit)
                            GL.Check "[uploadTexture2DLevelFile] BufferStorage"

                            let ptr = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly)
                            GL.Check "[uploadTexture2DLevelFile] MapBuffer"
                            IL.GetDXTCData(ptr, size, fmt) |> ignore
                            GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore

                            GL.BindTexture(TextureTarget.Texture2D, t.Handle)
                            GL.Check "[uploadTexture2DLevelFile] BindTexture"

                            GL.CompressedTexImage2D(TextureTarget.Texture2D, level, ifmt, w, h, 0, size, 0n)
                            GL.Check "[uploadTexture2DLevelFile] CompressedTexImage2D"
                            GL.BindTexture(TextureTarget.Texture2D, 0)
                            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                            GL.DeleteBuffer(pbo)

                            updateTexture t.Context t.SizeInBytes (int64 size)
                            t.Format <- unbox (int ifmt)
                            t.SizeInBytes <- int64 size

                        | _ ->
                            match PixFormat.get(fmt, pt) with
                                | Some pixFormat ->
                                    let ifmt = TextureFormat.ofPixFormat pixFormat config

                                    let pixelType, pixelFormat =
                                        match toPixelType pixFormat.Type, toPixelFormat pixFormat.Format with
                                            | Some t, Some f -> (t,f)
                                            | _ ->
                                                failwith "conversion not implemented"


                                    let elementSize = pixFormat.Type.GLSize
                                    let channelCount =
                                        match toChannelCount pixFormat.Format with
                                            | Some c -> c
                                            | _ -> pixFormat.ChannelCount
                                
                                    let align = t.Context.PackAlignment
                                    let lineSize = w * elementSize * channelCount
                                    let alignedLineSize =
                                        if lineSize % align = 0 then lineSize
                                        else (lineSize + (align - 1)) &&& ~~~(align - 1)


                                    let pbo = GL.GenBuffer()


                                    let size = int64 (alignedLineSize * h)
                                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
                                    GL.BufferStorage(BufferTarget.PixelUnpackBuffer, nativeint size, 0n, BufferStorageFlags.MapWriteBit)
                                    GL.Check "[uploadTexture2DLevelFile] BufferStorage"
                                    let dst = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly)
                                    GL.Check "[uploadTexture2DLevelFile] MapBuffer"
                                    let src = IL.GetData()

                                    let d = channelCount * elementSize

                                    let srcInfo =
                                        VolumeInfo(
                                            0L, 
                                            V3l(int64 w, int64 h, int64 d),
                                            V3l(int64 d, int64 lineSize, 1L)
                                        )

                                    let dstInfo = 
                                        VolumeInfo(
                                            int64 alignedLineSize * (srcInfo.SY-1L), 
                                            srcInfo.Size, 
                                            V3l(srcInfo.DX, int64 -alignedLineSize, srcInfo.DZ)
                                        )

                                    let vSrc = 
                                        NativeVolume<byte>(
                                            NativePtr.ofNativeInt src, 
                                            srcInfo
                                        )

                                    let vDst = 
                                        NativeVolume<byte>(
                                            NativePtr.ofNativeInt dst, 
                                            dstInfo
                                        )

                                    NativeVolume.iter2 vSrc vDst (fun src dst -> NativePtr.write dst (NativePtr.read src))
                                    GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore


                                    GL.BindTexture(TextureTarget.Texture2D, t.Handle)
                                    GL.TexImage2D(TextureTarget.Texture2D, level, unbox (int ifmt), w, h, 0, pixelFormat, pixelType, 0n)
                                    GL.Check "[uploadTexture2DLevelFile] TexImage2D"
                                    GL.BindTexture(TextureTarget.Texture2D, 0)

                                    GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                                    GL.DeleteBuffer(pbo)
                                    GL.Check "[uploadTexture2DLevelFile] DeleteBuffer"

                                    updateTexture t.Context t.SizeInBytes size
                                    t.SizeInBytes <- size
                                    t.Format <- ifmt


                                | _ -> 
                                    failwith "[GL] could not get PixFormat for devil-texture"




                    t.Size <- V3i(w,h,1)
                    t.Dimension <- TextureDimension.Texture2D
                    t.Count <- 1
                    t.ImmutableFormat <- false
                    t.MipMapLevels <- 1
                    t.Multisamples <- 1
                    IL.BindImage(0)
                finally
                    IL.DeleteImage(img)
            )

        let uploadTexture2D (t : Texture) (file : string) (config : TextureParams) =
            uploadTexture2DLevelFile t 0 file config
            GL.Check "uploadTexture2D"

            GL.BindTexture(TextureTarget.Texture2D, t.Handle)
            GL.Check "BindTexture"
            if config.wantMipMaps then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "GenerateMipmap"
                let newSize = 4L * t.SizeInBytes / 3L
                updateTexture t.Context t.SizeInBytes newSize
                t.SizeInBytes <- newSize
            else
                GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, [| 0 |])
                GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMaxLod, [| 0 |])
                GL.Check "TexParameterI"
            GL.BindTexture(TextureTarget.Texture2D, 0)



    [<AutoOpen>]
    module private Uploads =
        let getTextureTarget' (dim : TextureDimension, isArray : bool, isMS : bool) =
            match dim, isArray, isMS with

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

                | _ -> failwithf "unknown texture dimension: %A" dim

        let getTextureTarget (texture : Texture) =
            getTextureTarget' ( texture.Dimension, texture.IsArray, texture.IsMultisampled)

        let private uploadTexture2DInternal (bindTarget : TextureTarget) (target : TextureTarget) (isTopLevel : bool) (t : Texture) (startLevel : int) (textureParams : TextureParams) (data : PixImageMipMap) =
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

            let internalFormat = TextureFormat.ofPixFormat data.[0].PixFormat textureParams |> int |> unbox<PixelInternalFormat>
            let sizeChanged = size <> t.Size2D

            if sizeChanged then
                let sizeInBytes = int64 <| ((InternalFormat.getSizeInBits internalFormat) * size.X * size.Y) / 8
                let sizeInBytes =  if textureParams.wantMipMaps then (sizeInBytes * 4L) / 3L else sizeInBytes
                updateTexture t.Context t.SizeInBytes sizeInBytes
                t.SizeInBytes <- sizeInBytes

            GL.BindTexture(bindTarget, t.Handle)
            GL.Check "could not bind texture"

            if not generateMipMap then
                GL.TexParameterI(bindTarget, TextureParameterName.TextureMaxLevel, [|uploadLevels - 1|])
                GL.TexParameterI(bindTarget, TextureParameterName.TextureBaseLevel, [| 0 |])

            for l in 0..uploadLevels-1 do
                let image = data.[l]

                image.PinPBO(t.Context.PackAlignment,ImageTrafo.MirrorY, fun dim pixelType pixelFormat size ->
                    if sizeChanged || formatChanged then
                        GL.TexImage2D(target, startLevel + l, internalFormat, dim.X, dim.Y, 0, pixelFormat, pixelType, 0n)
                    else
                        GL.TexSubImage2D(target, startLevel + l, 0, 0, dim.X, dim.Y, pixelFormat, pixelType, 0n)
                    GL.Check (sprintf "could not upload texture data for level %d" l)
                )


            // if the image did not contain a sufficient
            // number of MipMaps and the user demanded 
            // MipMaps we generate them using OpenGL
            if generateMipMap && isTopLevel then
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
                GL.Check "failed to generate mipmaps"

            GL.BindTexture(bindTarget, 0)
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

        let uploadTexture2D (t : Texture) (textureParams : TextureParams) (data : PixImageMipMap) =
            uploadTexture2DInternal TextureTarget.Texture2D TextureTarget.Texture2D true t 0 textureParams data |> ignore

        let uploadTextureCube (t : Texture) (textureParams : TextureParams) (data : PixImageCube) =
            for (s,_) in cubeSides do
                if data.[s].LevelCount <= 0 then
                    failwith "cannot upload texture having 0 levels"

            let mutable generateMipMaps = false
            let size = data.[CubeSide.NegativeX].[0].Size

            let mutable minLevels = Int32.MaxValue
            for (side, target) in cubeSides do
                let data = data.[side]
                
                minLevels <- min minLevels data.LevelCount
                let generate = uploadTexture2DInternal TextureTarget.TextureCubeMap target false t 0 textureParams data

                if generate && textureParams.wantMipMaps then
                    generateMipMaps <- true

            let realSize = t.SizeInBytes * 6L
            updateTexture t.Context t.SizeInBytes realSize
            t.SizeInBytes <- realSize

            let levels =
                if generateMipMaps then
                    GL.BindTexture(TextureTarget.TextureCubeMap, t.Handle)
                    GL.Check "could not bind texture"

                    GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap)
                    GL.Check "failed to generate mipmaps"

                    GL.BindTexture(TextureTarget.TextureCubeMap, 0)
                    GL.Check "could not unbind texture"

                    GL.GetTexParameterI(TextureTarget.TextureCubeMap, GetTextureParameter.TextureMaxLevel, &minLevels)
                    minLevels + 1
                else
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMaxLevel, minLevels - 1)
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureBaseLevel, 0)

                    minLevels
                
            t.MipMapLevels <- levels
            t.Size <- V3i(size.X, size.Y, 0)
            t.Multisamples <- 1
            t.Count <- 1
            t.Dimension <- TextureDimension.TextureCube

        let uploadTexture2DBitmap (t : Texture) (mipMaps : bool) (bmp : BitmapTexture) =
            let size = V2i(bmp.Bitmap.Width, bmp.Bitmap.Height)
            let expectedLevels = Fun.Min(size.X, size.Y) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let uploadLevels = 1
            let generateMipMap = mipMaps
            let internalFormat = PixelInternalFormat.CompressedRgba
            let sizeChanged = size <> t.Size2D
            GL.BindTexture(TextureTarget.Texture2D, t.Handle)
            GL.Check "could not bind texture"

            if not generateMipMap then
                GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, [|0|])
                GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, [|0|])


            // determine the input format and covert the image
            // to a supported format if necessary.
            let pixelType, pixelFormat =
                PixelType.UnsignedByte, PixelFormat.Bgra


            let elementSize = 1
            let lineSize = size.X * 4 * elementSize
            let packAlign = t.Context.PackAlignment

            let alignedLineSize = (lineSize + (packAlign - 1)) &&& ~~~(packAlign - 1)
            let targetSize = alignedLineSize * size.Y



            let locked = bmp.Bitmap.LockBits(Drawing.Rectangle(0,0,bmp.Bitmap.Width, bmp.Bitmap.Height), Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppArgb)          
            let srcInfo = VolumeInfo(V3l(size.X, size.Y, 4), V3l(4, size.X * 4, 1))
            let src : NativeVolume<byte> = locked.Scan0 |> NativeVolume.ofNativeInt srcInfo
  
  
            let b = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, b)
            GL.BufferStorage(BufferTarget.PixelUnpackBuffer, nativeint targetSize, 0n, BufferStorageFlags.MapWriteBit)

            let ptr = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, nativeint targetSize, BufferAccessMask.MapWriteBit)
            try
                let dy = int64 (alignedLineSize / elementSize)

                let dstInfo = 
                    VolumeInfo(
                        dy * (srcInfo.Size.Y-1L), 
                        srcInfo.Size, 
                        V3l(srcInfo.SZ, -dy, 1L)
                    )
                let dst = ptr |> NativeVolume.ofNativeInt dstInfo

                NativeVolume.iter2 src dst (fun s d -> NativePtr.write d (NativePtr.read s))
            finally
                GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore

            if sizeChanged then
                GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, size.X, size.Y, 0, pixelFormat, pixelType, 0n)
            else
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, size.X, size.Y, pixelFormat, pixelType, 0n)
            GL.Check "could not upload texture data"

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            GL.DeleteBuffer(b)


            bmp.Bitmap.UnlockBits(locked)

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

        let uploadTexture3D (t : Texture) (textureParams : TextureParams) (data : PixVolume) =
            let size = data.Size
            let expectedLevels = Fun.Min(size.X, size.Y, size.Z) |> Fun.Log2 |> Fun.Ceiling |> int //int(Fun.Ceiling(Fun.Log2(Fun.Min(size.X, size.Y))))
            let generateMipMap = textureParams.wantMipMaps
            let newFormat = TextureFormat.ofPixFormat data.PixFormat textureParams
            let formatChanged = t.Format <> newFormat
            let sizeChanged = size <> t.Size3D
            let internalFormat = TextureFormat.ofPixFormat data.PixFormat textureParams |> int |> unbox<PixelInternalFormat>

            GL.BindTexture(TextureTarget.Texture3D, t.Handle)
            GL.Check "could not bind texture"

            data.PinPBO (t.Context.PackAlignment, fun size pt pf sizeInBytes ->
                if sizeChanged || formatChanged then
                    if not generateMipMap then
                        GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMaxLod, 0)
                        GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureBaseLevel, 0)

                    GL.TexImage3D(TextureTarget.Texture3D, 0, internalFormat, size.X, size.Y, size.Z, 0, pf, pt, 0n)
                else
                    GL.TexSubImage3D(TextureTarget.Texture3D, 0, 0, 0, 0, size.X, size.Y, size.Z, pf, pt, 0n)
                GL.Check "could not upload texture data"
            )

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
            t.MipMapLevels <- (if generateMipMap then expectedLevels else 1)
            t.Dimension <- TextureDimension.Texture3D
            t.Format <- newFormat

        let uploadNativeTexture (t : Texture) (data : INativeTexture) =
            match data.Dimension, data.Count with
                | TextureDimension.Texture2D, 1 ->
                    let target = TextureTarget.Texture2D
                    GL.BindTexture(target, t.Handle)

                    let isCompressed = compressedFormats.Contains data.Format

                    let mutable totalSize = 0L
                    for l in 0 .. data.MipMapLevels - 1 do
                        let levelData = data.[0,l]
                        
                        totalSize <- totalSize + levelData.SizeInBytes
                        levelData.Use(fun src ->
                            let pbo = GL.GenBuffer()
                            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)

                            GL.BufferData(BufferTarget.PixelUnpackBuffer, nativeint levelData.SizeInBytes, 0n, BufferUsageHint.StaticDraw)
                            let dst = GL.MapBufferRange(BufferTarget.PixelUnpackBuffer, 0n, nativeint levelData.SizeInBytes, BufferAccessMask.MapWriteBit)
                            Marshal.Copy(src, dst, levelData.SizeInBytes)
                            GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) |> ignore

                            if isCompressed then
                                GL.CompressedTexImage2D(target, l, unbox (int data.Format), levelData.Size.X, levelData.Size.Y, 0, int levelData.SizeInBytes, 0n)
                            else
                                let pf, pt = toFormatAndType data.Format
                                GL.TexImage2D(target, l, unbox (int data.Format), levelData.Size.X, levelData.Size.Y, 0, pf, pt, 0n)
                        
                            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
                            GL.DeleteBuffer(pbo)
                        )

                    GL.TexParameterI(target, TextureParameterName.TextureMaxLevel, [|data.MipMapLevels - 1|])

                    updateTexture t.Context t.SizeInBytes totalSize
                    t.SizeInBytes <- totalSize
                    t.Count <- 1
                    t.Dimension <- data.Dimension
                    t.Format <- data.Format
                    t.ImmutableFormat <- false
                    t.MipMapLevels <- data.MipMapLevels
                    t.Multisamples <- 1
                    GL.BindTexture(target, 0)
                | _ ->

                    failwith "implement me"
            ()

        let downloadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (level : int) (image : PixImage) =
            let format = image.PixFormat
            let bindTarget =  getTextureTarget t
            GL.BindTexture(bindTarget, t.Handle)
            GL.Check "could not bind texture"

            let pixelType, pixelFormat =
                match toPixelType format.Type, toPixelFormat image.Format with
                    | Some t, Some f -> (t,f)
                    | _ ->
                        failwith "conversion not implemented"


            let elementSize = image.PixFormat.Type.GLSize
            let channelCount =
                match toChannelCount image.Format with
                    | Some c -> c
                    | _ -> image.PixFormat.ChannelCount

            let lineSize = image.Size.X * channelCount * elementSize
            let packAlign = t.Context.PackAlignment

            let alignedLineSize = (lineSize + (packAlign - 1)) &&& ~~~(packAlign - 1)
            let targetSize = alignedLineSize * image.Size.Y

            let b = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelPackBuffer, b)
            GL.Check "could not bind buffer"
            GL.BufferStorage(BufferTarget.PixelPackBuffer, nativeint targetSize, 0n, BufferStorageFlags.MapReadBit)
            GL.Check "could not set buffer storage"
            GL.GetTexImage(target, level, pixelFormat, pixelType, 0n)
            GL.Check "could not get texture image"

            let src = GL.MapBufferRange(BufferTarget.PixelPackBuffer, 0n, nativeint targetSize, BufferAccessMask.MapReadBit)
            GL.Check "could not map buffer"
            try
                let dstInfo = image.VolumeInfo
                let dy = int64(alignedLineSize / elementSize)
                let srcInfo = 
                    VolumeInfo(
                        dy * (dstInfo.Size.Y - 1L), 
                        dstInfo.Size, 
                        V3l(dstInfo.SZ, -dy, 1L)
                    )

                NativeVolume.copyNativeToImage src srcInfo image

            finally
                GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore
                GL.Check "could not unmap buffer"

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
            GL.Check "could not unbind buffer"
            GL.DeleteBuffer(b)
            GL.Check "could not delete buffer"

            GL.BindTexture(bindTarget, 0)
            GL.Check "could not unbind texture"

        let downloadTexture2D (t : Texture) (level : int) (image : PixImage) =
            downloadTexture2DInternal TextureTarget.Texture2D true t level image

        let downloadTextureCube (t : Texture) (level : int) (side : CubeSide) (image : PixImage) =
            let target = cubeSides.[int side] |> snd
            downloadTexture2DInternal target false t level image

    let texSizeInBytes (size : V3i, t : TextureFormat, samples : int) =
        let pixelCount = (int64 size.X) * (int64 size.Y) * (int64 size.Z) * (int64 samples)
        pixelCount * (int64 (InternalFormat.getSizeInBits (unbox (int t)))) / 8L

    type Context with
//        member x.CreateTexture1D(size : int, mipMapLevels : int, t : TextureFormat) =
//            using x.ResourceLock (fun _ ->
//                let h = GL.GenTexture()
//                GL.Check "could not create texture"
//
//                addTexture x 0L
//                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i.Zero, None, t, 0L, false)
//                x.UpdateTexture1D(tex, size, mipMapLevels, t)
//
//                tex
//            )
//
//        member x.CreateTexture2D(size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
//            using x.ResourceLock (fun _ ->
//                let h = GL.GenTexture()
//                GL.Check "could not create texture"
//                
//                addTexture x 0L
//                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i.Zero, None, t, 0L, false)
//
//                x.UpdateTexture2D(tex, size, mipMapLevels, t, samples)
//
//                tex
//            )
//
//        member x.CreateTexture3D(size : V3i, mipMapLevels : int, t : TextureFormat) =
//            using x.ResourceLock (fun _ ->
//                let h = GL.GenTexture()
//                GL.Check "could not create texture"
//
//                addTexture x 0L
//                let tex = Texture(x, h, TextureDimension.Texture3D, mipMapLevels, 1, V3i.Zero, 1, t, 0L, false)
//                x.UpdateTexture3D(tex, size, mipMapLevels, t)
//
//                tex
//            )
//
//        member x.CreateTextureCube(size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
//            using x.ResourceLock (fun _ ->
//                let h = GL.GenTexture()
//                GL.Check "could not create texture"
//
//                addTexture x 0L
//                let tex = Texture(x, h, TextureDimension.TextureCube, mipMapLevels, 1, V3i(size.X, size.Y, 0), 1, t, 0L, false)
//                x.UpdateTextureCube(tex, size, mipMapLevels, t, samples)
//
//                tex
//            )
//
//        member x.CreateTexture1DArray(size : int, count : int, mipMapLevels : int, t : TextureFormat) =
//            using x.ResourceLock (fun _ ->
//                let h = GL.GenTexture()
//                GL.Check "could not create texture"
//
//                addTexture x 0L
//                let tex = Texture(x, h, TextureDimension.Texture1D, mipMapLevels, 1, V3i.Zero, 1, t, 0L, false)
//                x.UpdateTexture1DArray(tex, size, count, mipMapLevels, t)
//
//                tex
//            ) 
//
//        member x.CreateTexture2DArray(size : V2i, count : int, mipMapLevels : int, t : TextureFormat, samples : int) =
//            using x.ResourceLock (fun _ ->
//                let h = GL.GenTexture()
//                GL.Check "could not create texture"
//                
//                addTexture x 0L
//                let tex = Texture(x, h, TextureDimension.Texture2D, mipMapLevels, 1, V3i.Zero, 1, t, 0L, false)
//
//                x.UpdateTexture2DArray(tex, size, count, mipMapLevels, t, samples)
//
//                tex
//            )
//            
//        member x.UpdateTexture1D(tex : Texture, size : int, mipMapLevels : int, t : TextureFormat) =
//            using x.ResourceLock (fun _ ->
//                if tex.ImmutableFormat then
//                    failwith "cannot update format/size for immutable texture"
//
//                GL.BindTexture(TextureTarget.Texture1D, tex.Handle)
//                GL.Check "could not bind texture"
//
//                let sizeInBytes = texSizeInBytes(V3i(size, 1, 1), t, 1)
//                updateTexture tex.Context tex.SizeInBytes sizeInBytes
//                tex.SizeInBytes <- sizeInBytes
//  
//                GL.TexStorage1D(TextureTarget1d.Texture1D, mipMapLevels, unbox (int t), size)
//                GL.Check "could not allocate texture"
//
//                GL.BindTexture(TextureTarget.Texture1D, 0)
//                GL.Check "could not unbind texture"
//
//                tex.MipMapLevels <- mipMapLevels
//                tex.Dimension <- TextureDimension.Texture1D
//                tex.Size <- V3i(size, 0, 0)
//                tex.Format <- t
//                tex.ImmutableFormat <- true
//            )
//
//        member x.UpdateTexture2D(tex : Texture, size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
//            using x.ResourceLock (fun _ ->
//                if tex.ImmutableFormat then
//                    failwith "cannot update format/size for immutable texture"
//
//                let target =
//                    if samples = 1 then TextureTarget.Texture2D
//                    else TextureTarget.Texture2DMultisample
//
//                GL.BindTexture(target, tex.Handle)
//                GL.Check "could not bind texture"
//
//                let sizeInBytes = texSizeInBytes(size.XYI, t, samples)
//                updateTexture tex.Context tex.SizeInBytes sizeInBytes
//                tex.SizeInBytes <- sizeInBytes
//
//                if samples = 1 then
//                    GL.TexStorage2D(TextureTarget2d.Texture2D, mipMapLevels, unbox (int t), size.X, size.Y)
//                else
//                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
//                    GL.TexStorage2DMultisample(TextureTargetMultisample2d.Texture2DMultisample, samples, unbox (int t), size.X, size.Y, false)
//
//                GL.Check "could not allocate texture"
//
//                GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipMapLevels)
//                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
//
//
//                GL.BindTexture(target, 0)
//                GL.Check "could not unbind texture"
//
//                tex.MipMapLevels <- mipMapLevels
//                tex.Dimension <- TextureDimension.Texture2D
//                tex.Multisamples <- samples
//                tex.Count <- 1
//                tex.Size <- V3i(size.X, size.Y, 0)
//                tex.Format <- t
//                tex.ImmutableFormat <- true
//            )
//
//        member x.UpdateTexture3D(tex : Texture, size : V3i, mipMapLevels : int, t : TextureFormat) =
//            using x.ResourceLock (fun _ ->
//                if tex.ImmutableFormat then
//                    failwith "cannot update format/size for immutable texture"
//
//                GL.BindTexture(TextureTarget.Texture3D, tex.Handle)
//                GL.Check "could not bind texture"
//
//                let ifmt = unbox (int t) 
//
//                let sizeInBytes = texSizeInBytes(size, t, 1)
//                updateTexture tex.Context tex.SizeInBytes sizeInBytes
//                tex.SizeInBytes <- sizeInBytes
//
//                GL.TexStorage3D(TextureTarget3d.Texture3D, mipMapLevels, ifmt, size.X, size.Y, size.Z)
//                GL.Check "could not allocate texture"
//
//                GL.BindTexture(TextureTarget.Texture3D, 0)
//                GL.Check "could not unbind texture"
//
//                tex.MipMapLevels <- mipMapLevels
//                tex.Dimension <- TextureDimension.Texture3D
//                tex.Count <- 1
//                tex.Multisamples <- 1
//                tex.Size <- size
//                tex.Format <- t
//                tex.ImmutableFormat <- true
//            )
//
//        member x.UpdateTextureCube(tex : Texture, size : V2i, mipMapLevels : int, t : TextureFormat, samples : int) =
//            using x.ResourceLock (fun _ ->
//                if tex.ImmutableFormat then
//                    failwith "cannot update format/size for immutable texture"
//
//                GL.BindTexture(TextureTarget.TextureCubeMap, tex.Handle)
//                GL.Check "could not bind texture"
//
//                if samples = 1 then
//                    GL.TexStorage2D(TextureTarget2d.TextureCubeMap, mipMapLevels, unbox (int t), size.X, size.Y)
//                else
//                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
//                     TODO: verify that this works!!
//                    for f in 0..5 do
//                        let target = int TextureTarget.TextureCubeMapPositiveX + f
//                        GL.TexImage2DMultisample(unbox target, samples, unbox (int t), size.X, size.Y, true)
//
//                GL.BindTexture(TextureTarget.TextureCubeMap, 0)
//                GL.Check "could not unbind texture"
//
//                let sizeInBytes = texSizeInBytes(size.XYI, t, samples)
//                let sizeInBytes = sizeInBytes * 6L
//                updateTexture tex.Context tex.SizeInBytes sizeInBytes
//                tex.SizeInBytes <- sizeInBytes
//
//                tex.MipMapLevels <- mipMapLevels
//                tex.Dimension <- TextureDimension.TextureCube
//                tex.Size <- V3i(size.X, size.Y, 0)
//                tex.Count <- 1
//                tex.Multisamples <- samples
//                tex.Format <- t
//                tex.ImmutableFormat <- true
//            )
//
//        member x.UpdateTexture2DArray(tex : Texture, size : V2i, count : int, mipMapLevels : int, t : TextureFormat, samples : int) =
//            using x.ResourceLock (fun _ ->
//                if tex.ImmutableFormat then
//                    failwith "cannot update format/size for immutable texture"
//
//                let target =
//                    if samples = 1 then TextureTarget.Texture2DArray
//                    else TextureTarget.Texture2DMultisampleArray
//
//                GL.BindTexture(target, tex.Handle)
//                GL.Check "could not bind texture"
//
//
//                let sizeInBytes = texSizeInBytes(size.XYI, t, samples) * (int64 count)
//                updateTexture tex.Context tex.SizeInBytes sizeInBytes
//                tex.SizeInBytes <- sizeInBytes // TODO check multisampling
//
//                if samples = 1 then
//                    GL.TexStorage3D(TextureTarget3d.Texture2DArray, mipMapLevels, unbox (int t), size.X, size.Y, count)
//                else
//                    if mipMapLevels > 1 then failwith "multisampled textures cannot have MipMaps"
//                    GL.TexStorage3DMultisample(TextureTargetMultisample3d.Texture2DMultisampleArray, samples, unbox (int t), size.X, size.Y, count, true)
//  
//                GL.Check "could not allocate texture"
//
//                GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipMapLevels)
//                GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
//
//
//                GL.BindTexture(target, 0)
//                GL.Check "could not unbind texture"
//
//                tex.MipMapLevels <- mipMapLevels
//                tex.Dimension <- TextureDimension.Texture2D
//                tex.Count <- count
//                tex.Multisamples <- samples
//                tex.Size <- V3i(size.X, size.Y, 0)
//                tex.Format <- t
//                tex.ImmutableFormat <- true
//            )
//
//        member x.UpdateTexture1DArray(tex : Texture, size : int, count : int, mipMapLevels : int, t : TextureFormat) =
//            using x.ResourceLock (fun _ ->
//                if tex.ImmutableFormat then
//                    failwith "cannot update format/size for immutable texture"
//
//                GL.BindTexture(TextureTarget.Texture1DArray, tex.Handle)
//                GL.Check "could not bind texture"
//
//                let sizeInBytes = texSizeInBytes(V3i(size, 1, 1), t, 1) * (int64 count)
//                updateTexture tex.Context tex.SizeInBytes sizeInBytes
//                tex.SizeInBytes <- sizeInBytes
//  
//                GL.TexStorage2D(TextureTarget2d.Texture1DArray, mipMapLevels, unbox (int t), size, count)
//                GL.Check "could not allocate texture"
//
//                GL.BindTexture(TextureTarget.Texture1DArray, 0)
//                GL.Check "could not unbind texture"
//
//                tex.MipMapLevels <- mipMapLevels
//                tex.Dimension <- TextureDimension.Texture1D
//                tex.Count <- count
//                tex.Multisamples <- 1
//                tex.Size <- V3i(size, 0, 0)
//                tex.Format <- t
//                tex.ImmutableFormat <- true
//            )
//
//        member x.CreateTextureView(orig : Texture, levels : Range1i, slices : Range1i) =
//            using x.ResourceLock (fun _ ->
//                if not orig.ImmutableFormat then
//                    failwithf "cannot create texture-views for mutable textures"
//
//                let handle = GL.GenTexture()
//                GL.Check "could not create texture"
//
//                let dim =
//                    match orig.Dimension, orig.Count with
//                        | TextureDimension.TextureCube, 1 -> 
//                            if slices.Min <> slices.Max then failwithf "cannot take multiple slices from CubeMap"
//                            TextureDimension.Texture2D
//                        | d,_ -> d
//
//                let tex = Texture(x, handle, dim, 1 + levels.Max - levels.Min, orig.Multisamples, orig.Size, 1 + slices.Max - slices.Min, orig.Format, 0L, true)
//                let target = getTextureTarget tex
//                  
//                GL.TextureView(
//                    handle,
//                    target,
//                    orig.Handle,
//                    unbox (int orig.Format),
//                    levels.Min, 1 + levels.Max - levels.Min,
//                    slices.Min, 1
//                )
//                GL.Check "could not create texture view"
//
//                tex
//            )


        member x.CreateTexture(data : ITexture) =
            using x.ResourceLock (fun _ ->
                let newTexture () = // not all cases need new textures
                    let h = GL.GenTexture()
                    GL.Check "could not create texture"
                    addTexture x 0L
                    Texture(x, h, TextureDimension.Texture2D, 1, 1, V3i(-1,-1,-1), None, TextureFormat.Rgba8, 0L, false)

                match data with

                    | :? BitmapTexture as bmp ->
                        let t = newTexture ()
                        uploadTexture2DBitmap t true bmp
                        t

                    | FileTexture(info, file) ->
                        let t = newTexture ()
                        if isNull file then 
                            t
                        else
                            if info.wantCompressed then
                                Devil.uploadTexture2D t file info
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
                        Texture(x, 0, TextureDimension.Texture2D, 1, 1, V3i(-1,-1,-1), None, TextureFormat.Rgba8, 0L, false)

                    | :? Texture as o ->
                        o

                    | :? INativeTexture as data ->
                        let t = newTexture () 
                        uploadNativeTexture t data
                        t

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
                        Devil.uploadTexture2D t file info

                    | :? NullTexture -> failwith "cannot update texture with null texture"

                    | :? Texture as o ->
                        if t.Handle <> o.Handle then
                            failwith "cannot upload to framebuffer-texture"

                    | :? INativeTexture as data ->
                        uploadNativeTexture t data

                    | _ ->
                        failwith "unsupported texture data"
            )


            
        member x.Delete(t : Texture) =
            using x.ResourceLock (fun _ ->
                removeTexture x t.SizeInBytes
                GL.DeleteTexture(t.Handle)
                GL.Check "could not delete texture"
            )
            
        member x.Blit(src : Texture, srcLevel : int, srcSlice : int, srcRegion : Box2i, dst : Texture, dstLevel : int, dstSlice : int, dstRegion : Box2i, linear : bool) =
            using x.ResourceLock (fun _ ->
                let fSrc = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"
                let fDst = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                GL.Check "could not bind framebuffer"
                if src.IsArray then GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel, srcSlice)
                else GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel)
                GL.Check "could not attach texture to framebuffer"

                let srcCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                if srcCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create input framebuffer: %A" srcCheck

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fDst)
                GL.Check "could not bind framebuffer"

                if src.IsArray then GL.FramebufferTextureLayer(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, dst.Handle, dstLevel, dstSlice)
                else GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, dst.Handle, dstLevel)
                GL.Check "could not attach texture to framebuffer"

                let dstCheck = GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer)
                if dstCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create output framebuffer: %A" dstCheck

                GL.BlitFramebuffer(
                    srcRegion.Min.X, srcRegion.Min.Y,
                    srcRegion.Max.X, srcRegion.Max.Y,

                    dstRegion.Min.X, dstRegion.Min.Y,
                    dstRegion.Max.X, dstRegion.Max.Y,

                    ClearBufferMask.ColorBufferBit,
                    (if linear then BlitFramebufferFilter.Linear else BlitFramebufferFilter.Nearest)
                )
                GL.Check "could blit framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.Check "could unbind framebuffer"

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
                GL.Check "could unbind framebuffer"

                GL.DeleteFramebuffer(fSrc)
                GL.Check "could delete framebuffer"

                GL.DeleteFramebuffer(fDst)
                GL.Check "could delete framebuffer"

            )

        member x.Copy(src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i, dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i, size : V2i) =
            using x.ResourceLock (fun _ ->
                let fSrc = GL.GenFramebuffer()
                GL.Check "could not create framebuffer"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                GL.Check "could not bind framebuffer"

                if src.IsArray then
                    GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel, srcSlice)
                else
                    GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, src.Handle, srcLevel)
                GL.Check "could not attach texture to framebuffer"

                let srcCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                if srcCheck <> FramebufferErrorCode.FramebufferComplete then
                    failwithf "could not create input framebuffer: %A" srcCheck

                GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
                GL.Check "could not set readbuffer"


                let bindTarget = getTextureTarget dst
                GL.BindTexture(bindTarget, dst.Handle)
                GL.Check "could not bind texture"

                let copyTarget =
                    match dst.Dimension with
                        | TextureDimension.TextureCube -> snd cubeSides.[dstSlice]
                        | _ -> bindTarget


                GL.CopyTexSubImage2D(
                    copyTarget,
                    dstLevel,
                    dstOffset.X, dstOffset.Y,
                    srcOffset.X, srcOffset.Y,
                    size.X, size.Y
                )
                GL.Check "could not copy texture"

                GL.ReadBuffer(ReadBufferMode.None)
                GL.Check "could not unset readbuffer"

                GL.BindTexture(bindTarget, 0)
                GL.Check "could not unbind texture"

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
                GL.Check "could not unbind framebuffer"

                GL.DeleteFramebuffer(fSrc)
                GL.Check "could not delete framebuffer"

            )


        member x.Upload(t : Texture, level : int, slice : int, offset : V2i, source : PixImage) =
            using x.ResourceLock (fun _ ->
                let levelSize = t.Size2D / (1 <<< level)
                if offset = V2i.Zero && source.Size = levelSize then
                    x.Upload(t, level, slice, source)
                else
                    let temp = x.CreateTexture2D(source.Size, 1, t.Format, 1)
                    x.Upload(temp, 0, 0, source)
                    x.Copy(temp, 0, 0, V2i.Zero, t, level, slice, offset, source.Size)
                    x.Delete(temp)
            )

        member x.Upload(t : Texture, level : int, slice : int, source : PixImage) =
            using x.ResourceLock (fun _ ->
                match t.Dimension with
                    | TextureDimension.Texture2D -> 
                        let target = getTextureTarget t
                        GL.BindTexture(target, t.Handle)
                        GL.Check "could not bind texture"

                        source.PinPBO(t.Context.PackAlignment, ImageTrafo.MirrorY, fun dim pixelType pixelFormat size ->
                            if target = TextureTarget.Texture2DArray then
                                GL.TexSubImage3D(target, level, 0, 0, slice, dim.X, dim.Y, 1, pixelFormat, pixelType, 0n)
                            else
                                GL.TexSubImage2D(target, level, 0, 0, dim.X, dim.Y, pixelFormat, pixelType, 0n)
                            GL.Check (sprintf "could not upload texture data for level %d" level)
                        )

                        GL.BindTexture(target, 0)
                        GL.Check "could not unbind texture"

                    | TextureDimension.TextureCube ->
                        let target = getTextureTarget t
                        GL.BindTexture(target, t.Handle)
                        GL.Check "could not bind texture"

                        let target = snd cubeSides.[slice]
                        source.PinPBO(t.Context.PackAlignment, ImageTrafo.MirrorY, fun dim pixelType pixelFormat size ->
                            GL.TexSubImage2D(target, level, 0, 0, dim.X, dim.Y, pixelFormat, pixelType, 0n)
                            GL.Check (sprintf "could not upload texture data for level %d" level)
                        )

                        GL.BindTexture(target, 0)
                        GL.Check "could not unbind texture"
                    | _ ->  
                        failwithf "cannot upload textures of kind: %A" t.Dimension
            )

        member x.Upload(t : Texture, level : int, source : PixImage) =
            x.Upload(t, level, 0, source)



        member x.Download(t : Texture, level : int, slice : int, offset : V2i, target : PixImage) =
            using x.ResourceLock (fun _ ->
                let levelSize = t.Size2D / (1 <<< level)
                let offset = V2i(offset.X, levelSize.Y - offset.Y - target.Size.Y) // flip y-offset
                if offset = V2i.Zero && target.Size = levelSize then
                    x.Download(t, level, slice, target)
                else
                    let temp = x.CreateTexture2D(target.Size, 1, t.Format, 1)
                    
                    if t.IsMultisampled then // resolve multisamples
                        x.Blit(t, level, slice, Box2i.FromMinAndSize(offset, levelSize), temp, 0, 0, Box2i(V2i.Zero, target.Size), true)
                    else
                        x.Copy(t, level, slice, offset, temp, 0, 0, V2i.Zero, target.Size)                        
                    
                    x.Download(temp, 0, 0, target)
                    x.Delete(temp)
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

        member x.DownloadStencil(t : Texture, level : int, slice : int, target : Matrix<int>) =
            using x.ResourceLock (fun _ ->
                match t.Dimension with
                    | TextureDimension.Texture2D -> 
                        
                        //let downloadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (level : int) (image : PixImage)
                        
                        let img : PixImage<int> = PixImage<int>()
                        img.Volume <- target.AsVolume()
                        img.Format <- Col.Format.Stencil
                        downloadTexture2DInternal TextureTarget.Texture2D true t level img

                    | _ ->  
                        failwithf "cannot download stecil-texture of kind: %A" t.Dimension
            )

        member x.DownloadDepth(t : Texture, level : int, slice : int, target : Matrix<float32>) =
            using x.ResourceLock (fun _ ->
                match t.Dimension with
                    | TextureDimension.Texture2D -> 
                        
                        //let downloadTexture2DInternal (target : TextureTarget) (isTopLevel : bool) (t : Texture) (level : int) (image : PixImage)
                        
                        let img : PixImage<float32> = PixImage<float32>()
                        img.Volume <- target.AsVolume()
                        img.Format <- Col.Format.Depth
                        downloadTexture2DInternal TextureTarget.Texture2D true t level img

                    | _ ->  
                        failwithf "cannot download stecil-texture of kind: %A" t.Dimension
            )

        member x.Download(t : Texture, level : int, slice : int) : PixImage =
            let fmt = TextureFormat.toDownloadFormat t.Format
            let levelSize = t.Size2D / (1 <<< level)
            let img = PixImage.Create(fmt, int64 levelSize.X, int64 levelSize.Y)
            x.Download(t, level, slice, img)
            img

        member x.Download(t : Texture, level : int) : PixImage =
            x.Download(t, level, 0)

        member x.Download(t : Texture) : PixImage =
            x.Download(t, 0, 0)

        




    module ExecutionContext =

        let internal getTextureTarget (texture : Texture) = Uploads.getTextureTarget texture

        let bindTexture (unit : int) (texture : Texture) =
            seq {
                yield Instruction.ActiveTexture(int TextureUnit.Texture0 + unit)
                
                let target = getTextureTarget texture
                yield Instruction.BindTexture (int target) texture.Handle
            }            

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Texture =

    let empty =
        Texture(null,0,TextureDimension.Texture2D,0,0,V3i.Zero,None,TextureFormat.Rgba8,0L, false)

    let create1D (c : Context) (size : int) (mipLevels : int) (format : TextureFormat) =
        c.CreateTexture1D(size, mipLevels, format)

    let create2D (c : Context) (size : V2i) (mipLevels : int) (format : TextureFormat) (samples : int) =
        c.CreateTexture2D(size, mipLevels, format, samples)

    let createCube (c : Context) (size : V2i) (mipLevels : int) (format : TextureFormat) (samples : int) =
        c.CreateTextureCube(size, mipLevels, format, samples)

    let create3D (c : Context) (size : V3i) (mipLevels : int) (format : TextureFormat)  =
        c.CreateTexture3D(size, mipLevels, format)

    let delete (tex : Texture) =
        tex.Context.Delete(tex)

    let write (data : ITexture) (tex : Texture) =
        tex.Context.Upload(tex, data)

    let read (format : PixFormat) (level : int) (tex : Texture) : PixImage[] =
        let size = V2i(max 1 (tex.Size.X / (1 <<< level)), max 1 (tex.Size.Y / (1 <<< level)))

        let pi = PixImage.Create(format, int64 size.Y, int64 size.Y)
        tex.Context.Download(tex, level, 0, pi)
        [|pi|]


module private ImplicitConversionHate =
    let inline download (ctx : Context, t : Texture, level : int, slice : int, offset : V2i, target : PixImage) = 
        ctx.Download(t, level, slice, offset, target)

    let inline upload (ctx : Context, t : Texture, level : int, slice : int, offset : V2i, source : PixImage) =
        ctx.Upload(t, level, slice, offset, source)

    let inline copy(ctx : Context, src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i, dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i, size : V2i) =
        ctx.Copy(src, srcLevel, srcSlice, srcOffset, dst, dstLevel, dstSlice, dstOffset, size)

    let inline blit(ctx : Context, src : Texture, srcLevel : int, srcSlice : int, srcRegion : Box2i, dst : Texture, dstLevel : int, dstSlice : int, dstRegion : Box2i, linear : bool) =
        ctx.Blit(src, srcLevel, srcSlice, srcRegion, dst, dstLevel, dstSlice, dstRegion, linear)

[<Extension; AbstractClass; Sealed>]
type TextureExtensionsCSharp =
    [<Extension>]
    static member Download(ctx : Context, t : Texture, level : int, slice : int, offset : V2i, target : PixImage) =
        ImplicitConversionHate.download(ctx, t, level, slice, offset, target)

    [<Extension>]
    static member Upload(ctx : Context, t : Texture, level : int, slice : int, offset : V2i, source : PixImage) =
        ImplicitConversionHate.upload(ctx, t, level, slice, offset, source)

    [<Extension>]
    static member Copy(ctx : Context, src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i, dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i, size : V2i) =
        ImplicitConversionHate.copy(ctx, src, srcLevel, srcSlice, srcOffset, dst, dstLevel, dstSlice, dstOffset, size)

    [<Extension>]
    static member Blit(ctx : Context, src : Texture, srcLevel : int, srcSlice : int, srcRegion : Box2i, dst : Texture, dstLevel : int, dstSlice : int, dstRegion : Box2i, linear : bool) =
        ImplicitConversionHate.blit(ctx, src, srcLevel, srcSlice, srcRegion, dst, dstLevel, dstSlice, dstRegion, linear)