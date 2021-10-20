namespace Aardvark.Rendering.GL

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.NativeTensors
open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop
open System.Runtime.CompilerServices
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module internal TextureCopyUtilities =

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

    [<Sealed; AbstractClass>]
    type TextureCopyUtils() =

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

        static member Copy(elementType : Type, src : nativeint, srcInfo : VolumeInfo, dst : PixImage) =
            let gc = GCHandle.Alloc(dst.Array, GCHandleType.Pinned)
            try
                let pDst = gc.AddrOfPinnedObject()
                let imgInfo = dst.VolumeInfo
                let elementSize = dst.PixFormat.Type.GLSize |> int64
                let dstInfo =
                    VolumeInfo(
                        imgInfo.Origin * elementSize,
                        imgInfo.Size,
                        imgInfo.Delta * elementSize
                    )
                TextureCopyUtils.Copy(elementType, src, srcInfo, pDst, dstInfo)
            finally
                gc.Free()

        static member Copy(src : nativeint, srcInfo : VolumeInfo, dst : PixImage) =
            TextureCopyUtils.Copy(dst.PixFormat.Type, src, srcInfo, dst)

        static member Copy(src : PixVolume, dst : nativeint, dstInfo : Tensor4Info) =
            src.PixFormat.Type |> ExistentialHack.run {
                new IUnmanagedAction with
                    member __.Run(def : Option<'a>) =
                        let x = unbox<PixVolume<'a>> src
                        let dst = NativeTensor4<'a>(NativePtr.ofNativeInt dst, dstInfo)
                        NativeTensor4.using x.Tensor4 (fun src ->
                            src.CopyTo(dst)
                        )
            }

        static member Copy(src : nativeint, srcInfo : Tensor4Info, dst : PixVolume) =
            dst.PixFormat.Type |> ExistentialHack.run {
                new IUnmanagedAction with
                    member __.Run(def : Option<'a>) =
                        let x = unbox<PixVolume<'a>> dst
                        let src = NativeTensor4<'a>(NativePtr.ofNativeInt src, srcInfo)
                        NativeTensor4.using x.Tensor4 (fun dst ->
                            src.CopyTo(dst)
                        )
            }

[<AutoOpen>]
module internal ImageCopyImplementation =

    module Image =

        // Encodes the slices in the Z-dimension of the offset and size parameters
        // as expected by glCopyImageSubData. Makes the logic a bit easier but this concise
        // encoding is cumbersome to use.
        let private encodeSlices (dimension : TextureDimension) (offset : V3i) (size : V3i) (slices : Range1i) =
            let offset =
                match dimension with
                | TextureDimension.Texture3D -> offset
                | _ -> V3i(offset.XY, slices.Min)

            let size =
                match dimension with
                | TextureDimension.Texture3D -> size
                | _ -> V3i(size.XY, 1 + slices.Max - slices.Min)

            offset, size

        let private blitInternal (src : Image) (srcLevel : int) (srcOffset : V3i) (srcSize : V3i)
                                 (dst : Image) (dstLevel : int) (dstOffset : V3i) (dstSize : V3i)
                                 (linear : bool)  =

            let mask = src.Mask &&& dst.Mask
            let slices = min srcSize.Z dstSize.Z

            let filter =
                if linear then BlitFramebufferFilter.Linear else BlitFramebufferFilter.Nearest

            Framebuffer.temporary FramebufferTarget.DrawFramebuffer (fun _ ->
                Image.readLayers src srcLevel srcOffset.Z slices (fun srcSlice ->

                    let dstSlice = dstOffset.Z + srcSlice - srcOffset.Z
                    dst |> Image.attach FramebufferTarget.DrawFramebuffer dstLevel dstSlice
                    Framebuffer.check FramebufferTarget.DrawFramebuffer

                    GL.BlitFramebuffer(
                        srcOffset.X, srcOffset.Y,
                        srcOffset.X + srcSize.X, srcOffset.Y + srcSize.Y,
                        dstOffset.X, dstOffset.Y,
                        dstOffset.X + dstSize.X, dstOffset.Y + dstSize.Y,
                        mask, filter
                    )
                    GL.Check "could blit framebuffer"
                )
            )

        let private copyFramebuffer (src : Image) (srcLevel : int) (srcOffset : V3i)
                                    (dst : Texture) (dstLevel : int) (dstOffset : V3i)
                                    (size : V3i)  =
            let target = dst |> TextureTarget.ofTexture

            GL.BindTexture(target, dst.Handle)
            GL.Check "could not bind texture"

            Image.readLayers src srcLevel srcOffset.Z size.Z (fun srcSlice ->
                let dstSlice = dstOffset.Z + srcSlice - srcOffset.Z
                let targetSlice = target |> TextureTarget.toSliceTarget dstSlice

                match dst.Dimension, dst.IsArray with
                | TextureDimension.Texture1D, false ->
                    GL.CopyTexSubImage1D(target, dstLevel, dstOffset.X, srcOffset.X, srcOffset.Y, size.X)
                    GL.Check "could not copy texture subimage 1D"

                | TextureDimension.Texture1D, true
                | TextureDimension.Texture2D, false
                | TextureDimension.TextureCube, false ->
                    let dstOffset =
                        if dst.Dimension = TextureDimension.Texture1D then V2i(dstOffset.X, dstSlice)
                        else dstOffset.XY

                    GL.CopyTexSubImage2D(targetSlice, dstLevel, dstOffset.X, dstOffset.Y, srcOffset.X, srcOffset.Y, size.X, size.Y)
                    GL.Check "could not copy texture subimage 2D"

                | TextureDimension.Texture2D, true
                | TextureDimension.Texture3D, false
                | TextureDimension.TextureCube, true ->
                    let dstOffset =
                        if dst.Dimension = TextureDimension.Texture3D then dstOffset
                        else V3i(dstOffset.XY, dstSlice)

                    GL.CopyTexSubImage3D(target, dstLevel, dstOffset.X, dstOffset.Y, dstOffset.Z, srcOffset.X, srcOffset.Y, size.X, size.Y)
                    GL.Check "could not copy texture subimage 3D"

                | d, a ->
                    failwithf "[GL] unsupported texture data %A%s" d (if a then "[]" else "")
            )

        let private copyDirect (src : Image) (srcLevel : int) (srcOffset : V3i)
                               (dst : Image) (dstLevel : int) (dstOffset : V3i)
                               (size : V3i) =
            GL.CopyImageSubData(src.Handle, src.Target, srcLevel, srcOffset, dst.Handle, dst.Target, dstLevel, dstOffset, size)
            GL.Check "could copy image subdata"

        let copy (src : Image) (srcLevel : int) (srcSlices : Range1i) (srcOffset : V3i)
                 (dst : Image) (dstLevel : int) (dstSlices : Range1i) (dstOffset : V3i)
                 (size : V3i) =

            let srcOffset, srcSize =
                let flipped = src.WindowOffset(srcLevel, srcOffset, size)
                encodeSlices src.Dimension flipped size srcSlices

            let dstOffset, dstSize =
                let flipped = dst.WindowOffset(dstLevel, dstOffset, size)
                encodeSlices dst.Dimension flipped size dstSlices

            let size = min srcSize dstSize

            if GL.ARB_copy_image && src.Format = dst.Format && src.Samples = dst.Samples then
                copyDirect src srcLevel srcOffset dst dstLevel dstOffset size

            else
                match dst with
                | Image.Texture dst when not (src.IsMultisampled || dst.IsMultisampled) ->
                    copyFramebuffer src srcLevel srcOffset dst dstLevel dstOffset size

                | _ ->
                    blitInternal src srcLevel srcOffset size dst dstLevel dstOffset size false

        let blit (src : Image) (srcLevel : int) (srcSlices : Range1i) (srcOffset : V3i) (srcSize : V3i)
                 (dst : Image) (dstLevel : int) (dstSlices : Range1i) (dstOffset : V3i) (dstSize : V3i)
                 (linear : bool)  =

            let srcOffset, srcSize =
                let flipped = src.WindowOffset(srcLevel, srcOffset, srcSize)
                encodeSlices src.Dimension flipped srcSize srcSlices

            let dstOffset, dstSize =
                let flipped = dst.WindowOffset(dstLevel, dstOffset, dstSize)
                encodeSlices dst.Dimension flipped dstSize dstSlices

            blitInternal src srcLevel srcOffset srcSize dst dstLevel dstOffset dstSize linear

[<AutoOpen>]
module ContextTextureCopyExtensions =

    [<Extension; AbstractClass; Sealed>]
    type ContextTextureCopyExtensions =

        // ================================================================================================================
        // Explicit blit
        // ================================================================================================================

        [<Extension>]
        static member internal Blit(this : Context,
                                    src : Image, srcLevel : int, srcSlices : Range1i, srcOffset : V3i, srcSize : V3i,
                                    dst : Image, dstLevel : int, dstSlices : Range1i, dstOffset : V3i, dstSize : V3i,
                                    linear : bool) =
            using this.ResourceLock (fun _ ->
                Image.blit src srcLevel srcSlices srcOffset srcSize dst dstLevel dstSlices dstOffset dstSize linear
            )

        [<Extension>]
        static member internal Blit(this : Context,
                                    src : Image, srcLevel : int, srcSlice : int, srcOffset : V2i, srcSize : V2i,
                                    dst : Image, dstLevel : int, dstSlice : int, dstOffset : V2i, dstSize : V2i,
                                    linear : bool) =
            this.Blit(
                src, srcLevel, Range1i(srcSlice), V3i(srcOffset, 0), V3i(srcSize, 1),
                dst, dstLevel, Range1i(dstSlice), V3i(dstOffset, 0), V3i(dstSize, 1),
                linear
            )

        [<Extension>]
        static member internal Blit(this : Context,
                                    src : Image, srcLevel : int, srcSlice : int, srcRegion : Box2i,
                                    dst : Image, dstLevel : int, dstSlice : int, dstRegion : Box2i,
                                    linear : bool) =
            this.Blit(
                src, srcLevel, srcSlice, srcRegion.Min, srcRegion.Size,
                dst, dstLevel, dstSlice, dstRegion.Min, dstRegion.Size,
                linear
            )

        [<Extension>]
        static member Blit(this : Context,
                           src : Texture, srcLevel : int, srcSlices : Range1i, srcOffset : V3i, srcSize : V3i,
                           dst : Texture, dstLevel : int, dstSlices : Range1i, dstOffset : V3i, dstSize : V3i,
                           linear : bool) =
            this.Blit(
                Image.Texture src, srcLevel, srcSlices, srcOffset, srcSize,
                Image.Texture dst, dstLevel, dstSlices, dstOffset, dstSize,
                linear
            )

        [<Extension>]
        static member Blit(this : Context,
                           src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i, srcSize : V2i,
                           dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i, dstSize : V2i,
                           linear : bool) =
            this.Blit(
                Image.Texture src, srcLevel, srcSlice, srcOffset, srcSize,
                Image.Texture dst, dstLevel, dstSlice, dstOffset, dstSize,
                linear
            )

        [<Extension>]
        static member Blit(this : Context,
                           src : Texture, srcLevel : int, srcSlice : int, srcRegion : Box2i,
                           dst : Texture, dstLevel : int, dstSlice : int, dstRegion : Box2i,
                           linear : bool) =
            this.Blit(
                Image.Texture src, srcLevel, srcSlice, srcRegion,
                Image.Texture dst, dstLevel, dstSlice, dstRegion,
                linear
            )

        // ================================================================================================================
        // Copy (with implicit blit)
        // ================================================================================================================

        [<Extension>]
        static member internal Copy(this : Context,
                                    src : Image, srcLevel : int, srcSlices : Range1i, srcOffset : V3i,
                                    dst : Image, dstLevel : int, dstSlices : Range1i, dstOffset : V3i,
                                    size : V3i) =
            using this.ResourceLock (fun _ ->
                Image.copy src srcLevel srcSlices srcOffset dst dstLevel dstSlices dstOffset size
            )

        [<Extension>]
        static member internal Copy(this : Context,
                                    src : Image, srcLevel : int, srcSlice : int, srcOffset : V2i,
                                    dst : Image, dstLevel : int, dstSlice : int, dstOffset : V2i,
                                    size : V2i) =
            this.Copy(
                src, srcLevel, Range1i(srcSlice), V3i(srcOffset, 0),
                dst, dstLevel, Range1i(dstSlice), V3i(dstOffset, 0),
                V3i(size, 1)
            )

        [<Extension>]
        static member Copy(this : Context,
                           src : Texture, srcLevel : int, srcSlices : Range1i, srcOffset : V3i,
                           dst : Texture, dstLevel : int, dstSlices : Range1i, dstOffset : V3i,
                           size : V3i) =
            this.Copy(
                Image.Texture src, srcLevel, srcSlices, srcOffset,
                Image.Texture dst, dstLevel, dstSlices, dstOffset,
                size
            )

        [<Extension>]
        static member Copy(this : Context,
                           src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i,
                           dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i,
                           size : V2i) =
            this.Copy(
                Image.Texture src, srcLevel, srcSlice, srcOffset,
                Image.Texture dst, dstLevel, dstSlice, dstOffset,
                size
            )