namespace Aardvark.Rendering.GL

open System.Runtime.InteropServices
open System.Collections.Concurrent
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type SparseTexture(ctx : Context, handle : int, dimension : TextureDimension, mipMapLevels : int, multisamples : int, size : V3i, count : Option<int>, format : TextureFormat, pageSize : V3i, sparseLevels : int) = 
    inherit Texture(ctx, handle, dimension, mipMapLevels, multisamples, size, count, format, 0L, true)

    member x.PageSize = pageSize
    member x.SparseLevels = sparseLevels

    member x.GetSize(level : int) =
        if level < 0 || level >= x.MipMapLevels then
            V3i.Zero
        else
            x.Size / (1 <<< level)


[<AutoOpen>]
module ``Sparse Texture Extensions`` =
    
    type TextureParameterName with
        static member inline TextureSparse = 0x91A6 |> unbox<TextureParameterName>

    type InternalFormatParameter with
        static member inline VirtualPageSizeX = 0x9195 |> unbox<InternalFormatParameter>
        static member inline VirtualPageSizeY = 0x9196 |> unbox<InternalFormatParameter>
        static member inline VirtualPageSizeZ = 0x9197 |> unbox<InternalFormatParameter>

    module private Align =

        let prev (align : int) (value : int) =
            if value % align = 0 then value
            else value - (value % align)

        let prev3 (align : V3i) (value : V3i) =
            V3i(
                prev align.X value.X,
                prev align.Y value.Y,
                prev align.Z value.Z
            )

        let next (align : int) (value : int) =
            if value % align = 0 then value
            else value + (align - (value % align))

        let next3 (align : V3i) (value : V3i) =
            V3i(
                next align.X value.X,
                next align.Y value.Y,
                next align.Z value.Z
            )

    module private SparseTexturesArb = 
        type TexPageCommitmentDelegate =
            delegate of target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * commit : bool -> unit

        let commitDelegate = 
            lazy (
                let current = unbox<IGraphicsContextInternal> GraphicsContext.CurrentContext
                let ptr = current.GetAddress "glTexPageCommitmentARB"
                Marshal.GetDelegateForFunctionPointer(ptr, typeof<TexPageCommitmentDelegate>) |> unbox<TexPageCommitmentDelegate>
            )


    let private pixelFormat =
        LookupTable.lookupTable [
            1L, PixelFormat.Red
            2L, PixelFormat.Rg
            3L, PixelFormat.Rgb
            4L, PixelFormat.Rgba
        ]


    type GL with
        static member TexPageCommitment(target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, commit : bool) =
            SparseTexturesArb.commitDelegate.Value.Invoke(target, level, xoffset, yoffset, zoffset, width, height, depth, commit) 


    type Context with
        member x.CreateSparseTexture(virtualSize : V3i, format : TextureFormat, levels : int) =
            use __ = x.ResourceLock

            let ifmt = unbox<SizedInternalFormat> (int format)

            // determine the page size
            let mutable pageSize = V3i.Zero
            GL.GetInternalformat(ImageTarget.Texture3D, ifmt, InternalFormatParameter.VirtualPageSizeX, 1, &pageSize.X)
            GL.Check "could not get virtual page size"
            GL.GetInternalformat(ImageTarget.Texture3D, ifmt, InternalFormatParameter.VirtualPageSizeY, 1, &pageSize.Y)
            GL.Check "could not get virtual page size"
            GL.GetInternalformat(ImageTarget.Texture3D, ifmt, InternalFormatParameter.VirtualPageSizeZ, 1, &pageSize.Z)
            GL.Check "could not get virtual page size"

            let size = virtualSize |> Align.next3 pageSize



            let handle = GL.GenTexture()
            GL.Check "could not create sparse texture"

            GL.BindTexture(TextureTarget.Texture3D, handle)
            GL.Check "could not bind sparse texture"
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureSparse, 1)
            GL.Check "could not set texture to sparse"
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureBaseLevel, 0)
            GL.Check "could not set sparse texture base level"
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMaxLevel, levels - 1)
            GL.Check "could not set sparse texture max level"

            GL.TexStorage3D(TextureTarget3d.Texture3D, levels, ifmt, size.X, size.Y, size.Z)
            GL.Check "could not set sparse texture storage"

            let mutable sparseLevels = -1
            GL.GetTexParameter(TextureTarget.Texture3D, unbox 0x91AA, &sparseLevels)

            GL.BindTexture(TextureTarget.Texture3D, 0)
            GL.Check "could not unbind sparse texture"


            SparseTexture(x, handle, TextureDimension.Texture3D, levels, 1, size, None, format, pageSize, sparseLevels)

        member x.Commitment(t : SparseTexture, level : int, region : Box3i, commit : bool) =
            use __ = x.ResourceLock
            
            let min = region.Min |> Align.prev3 t.PageSize
            let maxE = region.Max + V3i.III |> Align.next3 t.PageSize
            let size = maxE - min
            let levelSize = t.GetSize level



            if min.AnySmaller 0 || maxE.AnyGreater levelSize then
                failwith "[GL] texture commitment region out of bounds"
            
            if size.AllGreater 0 then
                GL.BindTexture(TextureTarget.Texture3D, t.Handle)
                GL.Check "could not bind sparse texture"

                GL.TexPageCommitment(TextureTarget.Texture3D, level, min.X, min.Y, min.Z, size.X, size.Y, size.Z, commit)
                GL.Check "could not change commitment for sparse texture"

                GL.BindTexture(TextureTarget.Texture3D, 0)
                GL.Check "could not unbind sparse texture"

        member inline x.Commit(t : SparseTexture, level : int, region : Box3i) =
            x.Commitment(t, level, region, true)

        member inline x.Decommit(t : SparseTexture, level : int, region : Box3i) =
            x.Commitment(t, level, region, false)

        member x.Upload(t : SparseTexture, level : int, offset : V3i, data : PixVolume) =
            use __ = x.ResourceLock

            let available = t.GetSize level

            let size = data.Size
            let min = offset
            let max = offset + size - V3i.III
            if min.AnySmaller 0 || max.AnyGreaterOrEqual available then
                failwith "[GL] texture upload region out of bounds"
                
            if size.AllGreater 0 then
                data.PinPBO(x.PackAlignment, fun size pt pf _ ->
                    GL.BindTexture(TextureTarget.Texture3D, t.Handle)
                    GL.TexSubImage3D(TextureTarget.Texture3D, level, min.X, min.Y, min.Z, size.X, size.Y, size.Z, pf, pt, 0n)
                    GL.BindTexture(TextureTarget.Texture3D, 0)
                )

        member x.Upload(t : SparseTexture, level : int, offset : V3i, data : NativeTensor4<'a>) =
            use __ = x.ResourceLock

            let available = t.GetSize level

            let size = V3i data.Size.XYZ
            let min = offset
            let max = min + size - V3i.III
            if min.AnySmaller 0 || max.AnyGreaterOrEqual available then
                failwith "[GL] texture upload region out of bounds"
                
            if size.AllGreater 0 then
                NativeTensor4.withPBO data x.PackAlignment (fun size pt pf _ ->
                    GL.BindTexture(TextureTarget.Texture3D, t.Handle)
                    GL.Check "could not unbind sparse texture"
                    GL.TexSubImage3D(TextureTarget.Texture3D, level, min.X, min.Y, min.Z, size.X, size.Y, size.Z, pf, pt, 0n)
                    GL.Check "could not upload sparse region"
                    GL.BindTexture(TextureTarget.Texture3D, 0)
                    GL.Check "could not unbind sparse texture"
                )

        member x.Download(t : SparseTexture, level : int, offset : V3i, target : NativeTensor4<'a>) =
            use __ = x.ResourceLock

            let size = V3i target.Size.XYZ
            let temp = x.CreateTexture3D(size, 1, t.Format)

            try
                GL.CopyImageSubData(
                    t.Handle, ImageTarget.Texture3D, level, 
                    offset.X, offset.Y, offset.Z,
                    temp.Handle, ImageTarget.Texture3D, 0,
                    0, 0, 0,
                    size.X, size.Y, size.Z
                )

                let pboInfo =
                    {
                        size = size
                        flags = BufferStorageFlags.MapReadBit
                        pixelFormat = pixelFormat target.Size.W
                        pixelType = PixelType.ofType typeof<'a>
                    }

                NativeTensor4.usePBO pboInfo x.PackAlignment (fun pbo sizeInBytes info ->
                    GL.BindTexture(TextureTarget.Texture3D, temp.Handle)
                    GL.BindBuffer(BufferTarget.PixelPackBuffer, pbo)

                    GL.GetTexImage(TextureTarget.Texture3D, 0, pboInfo.pixelFormat, pboInfo.pixelType, 0n)

                    GL.BindTexture(TextureTarget.Texture3D, 0)

                    let ptr = GL.MapBufferRange(BufferTarget.PixelPackBuffer, 0n, sizeInBytes, BufferAccessMask.MapReadBit)

                    let t = NativeTensor4<'a>(NativePtr.ofNativeInt ptr, info)
                    t.CopyTo(target)

                    GL.UnmapBuffer(BufferTarget.PixelPackBuffer) |> ignore


                    GL.BindBuffer(BufferTarget.PixelPackBuffer, 0)
                )

            finally
                x.Delete temp

    type SparseTexture with
        member inline x.Commitment(level : int, region : Box3i, commit : bool) =
            x.Context.Commitment(x, level, region, commit)

        member inline x.Commit(level : int, region : Box3i) =
            x.Context.Commit(x, level, region)
            
        member inline x.Decommit(level : int, region : Box3i) =
            x.Context.Decommit(x, level, region)

        member inline x.Upload(level : int, offset : V3i, data : PixVolume) =
            x.Context.Upload(x, level, offset, data)

        member inline x.Upload(level : int, offset : V3i, data : NativeTensor4<'a>) =
            x.Context.Upload(x, level, offset, data)

        member inline x.Download(level : int, offset : V3i, data : NativeTensor4<'a>) =
            x.Context.Download(x, level, offset, data)