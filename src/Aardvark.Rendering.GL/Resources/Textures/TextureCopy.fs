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


[<AutoOpen>]
module ContextTextureCopyExtensions =

    [<Extension; AbstractClass; Sealed>]
    type ContextTextureCopyExtensions =

        [<Extension>]
         static member Blit(this : Context,
                            src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i, srcSize : V2i,
                            dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i, dstSize : V2i,
                            linear : bool) =
             this.Blit(src, srcLevel, srcSlice, Box2i.FromMinAndSize(srcOffset, srcSize),
                       dst, dstLevel, dstSlice, Box2i.FromMinAndSize(dstOffset, dstSize),
                       linear)

         [<Extension>]
         static member Blit(this : Context,
                            src : Texture, srcLevel : int, srcSlice : int, srcRegion : Box2i,
                            dst : Texture, dstLevel : int, dstSlice : int, dstRegion : Box2i,
                            linear : bool) =
             using this.ResourceLock (fun _ ->
                 let fSrc = GL.GenFramebuffer()
                 GL.Check "could not create framebuffer"
                 let fDst = GL.GenFramebuffer()
                 GL.Check "could not create framebuffer"

                 GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                 GL.Check "could not bind framebuffer"

                 let attachment, mask, linear =
                     if TextureFormat.isDepthStencil src.Format then
                         FramebufferAttachment.DepthStencilAttachment, ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit, false
                     elif TextureFormat.isDepth src.Format then
                         FramebufferAttachment.DepthAttachment, ClearBufferMask.DepthBufferBit, false
                     else
                         FramebufferAttachment.ColorAttachment0, ClearBufferMask.ColorBufferBit, linear

                 if src.Slices > 1 then GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, attachment, src.Handle, srcLevel, srcSlice)
                 else GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, attachment, src.Handle, srcLevel)
                 GL.Check "could not attach texture to framebuffer"

                 let srcCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                 if srcCheck <> FramebufferErrorCode.FramebufferComplete then
                     failwithf "could not create input framebuffer: %A" srcCheck

                 GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fDst)
                 GL.Check "could not bind framebuffer"

                 if dst.Slices > 1 then GL.FramebufferTextureLayer(FramebufferTarget.DrawFramebuffer, attachment, dst.Handle, dstLevel, dstSlice)
                 else GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, attachment, dst.Handle, dstLevel)
                 GL.Check "could not attach texture to framebuffer"

                 let dstCheck = GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer)
                 if dstCheck <> FramebufferErrorCode.FramebufferComplete then
                     failwithf "could not create output framebuffer: %A" dstCheck

                 GL.BlitFramebuffer(
                     srcRegion.Min.X, srcRegion.Min.Y,
                     srcRegion.Max.X, srcRegion.Max.Y,

                     dstRegion.Min.X, dstRegion.Min.Y,
                     dstRegion.Max.X, dstRegion.Max.Y,

                     mask,
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

         [<Extension>]
         static member Copy(this : Context,
                            src : Texture, srcLevel : int, srcSlice : int, srcOffset : V2i,
                            dst : Texture, dstLevel : int, dstSlice : int, dstOffset : V2i,
                            size : V2i) =
             using this.ResourceLock (fun _ ->
                 let fSrc = GL.GenFramebuffer()
                 GL.Check "could not create framebuffer"

                 GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fSrc)
                 GL.Check "could not bind framebuffer"

                 let attachment, readBuffer =
                     if TextureFormat.isDepthStencil src.Format then
                         FramebufferAttachment.DepthStencilAttachment, ReadBufferMode.None
                     elif TextureFormat.isDepth src.Format then
                         FramebufferAttachment.DepthAttachment, ReadBufferMode.None
                     else
                         FramebufferAttachment.ColorAttachment0, ReadBufferMode.ColorAttachment0

                 if src.Slices > 1 then
                     GL.FramebufferTextureLayer(FramebufferTarget.ReadFramebuffer, attachment, src.Handle, srcLevel, srcSlice)
                 else
                     GL.FramebufferTexture(FramebufferTarget.ReadFramebuffer, attachment, src.Handle, srcLevel)
                 GL.Check "could not attach texture to framebuffer"

                 let srcCheck = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer)
                 if srcCheck <> FramebufferErrorCode.FramebufferComplete then
                     failwithf "could not create input framebuffer: %A" srcCheck

                 GL.ReadBuffer(readBuffer)
                 GL.Check "could not set readbuffer"


                 let bindTarget = TextureTarget.ofTexture dst
                 GL.BindTexture(bindTarget, dst.Handle)
                 GL.Check "could not bind texture"

                 // NOTE: according to glCopyTexSubImage2D/3D documentation: multi-sampled texture are not supported
                 if dst.IsArray then

                     GL.CopyTexSubImage3D(
                         bindTarget,
                         dstLevel,
                         dstOffset.X, dstOffset.Y, dstSlice,
                         srcOffset.X, srcOffset.Y,
                         size.X, size.Y
                     )
                     GL.Check "could not copy texture"

                 else
                     let copyTarget =
                         match dst.Dimension with
                         | TextureDimension.TextureCube -> snd TextureTarget.cubeSides.[dstSlice]
                         | _ -> TextureTarget.Texture2D

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