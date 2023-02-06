namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open Aardvark.Base
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type IComputeTask =
    inherit IDisposable
    inherit IAdaptiveObject
    abstract member Runtime : IComputeRuntime
    abstract member Update : token: AdaptiveToken * renderToken: RenderToken -> unit
    abstract member Run : token: AdaptiveToken * renderToken: RenderToken -> unit

and IComputeShader =
    inherit IDisposable
    abstract member Runtime : IComputeRuntime
    abstract member LocalSize : V3i

and IComputeInputBinding =
    abstract member Shader : IComputeShader

and IComputeRuntime =
    inherit IBufferRuntime
    inherit ITextureRuntime
    abstract member MaxLocalSize : V3i
    abstract member CreateComputeShader : shader: FShade.ComputeShader -> IComputeShader
    abstract member NewInputBinding : shader: IComputeShader * inputs: IUniformProvider -> IComputeInputBinding
    abstract member CompileCompute : commands: alist<ComputeCommand> -> IComputeTask

and [<RequireQualifiedAccess>]
    HostMemory =
    | Managed   of array: Array * index: int
    | Unmanaged of nativeint

and [<RequireQualifiedAccess>]
    ComputeCommand =
    | BindCmd                 of shader: IComputeShader
    | SetInputCmd             of input: IComputeInputBinding
    | DispatchCmd             of groups: V3i
    | ExecuteCmd              of task: IComputeTask
    | CopyBufferCmd           of src: IBufferRange * dst: IBufferRange
    | DownloadBufferCmd       of src: IBufferRange * dst: HostMemory
    | UploadBufferCmd         of src: HostMemory * dst: IBufferRange
    | SetBufferCmd            of buffer: IBufferRange * pattern: byte[]
    | SyncBufferCmd           of buffer: IBackendBuffer * srcAccess: ResourceAccess * dstAccess: ResourceAccess
    | CopyImageCmd            of src: IFramebufferOutput * srcOffset: V3i * dst: IFramebufferOutput * dstOffset: V3i * size: V3i
    | TransformLayoutCmd      of image: IBackendTexture * layout: TextureLayout
    | TransformSubLayoutCmd   of image: ITextureRange * srcLayout: TextureLayout * dstLayout: TextureLayout
    | SyncImageCmd            of image: IBackendTexture * srcAccess: ResourceAccess * dstAccess: ResourceAccess

    // ================================================================================================================
    // Input & Dispatch
    // ================================================================================================================

    static member Bind(shader : IComputeShader) =
        ComputeCommand.BindCmd shader

    static member SetInput(input : IComputeInputBinding) =
        ComputeCommand.SetInputCmd input

    static member Dispatch(groups : V3i) =
        ComputeCommand.DispatchCmd groups

    static member Dispatch(groups : V2i) =
        ComputeCommand.DispatchCmd (V3i(groups, 1))

    static member Dispatch(groups : int) =
        ComputeCommand.DispatchCmd (V3i(groups, 1, 1))

    static member Execute(task : IComputeTask) =
        ComputeCommand.ExecuteCmd task

    // ================================================================================================================
    // Buffers
    // ================================================================================================================

    static member Set<'T when 'T : unmanaged>(dst : IBufferRange<'T>, value : 'T) =
        let arr : byte[] = Array.zeroCreate sizeof<'T>
        let mutable value = value
        Marshal.Copy(NativePtr.toNativeInt &&value, arr, 0, arr.Length)
        ComputeCommand.SetBufferCmd(dst, arr)

    static member Zero(buffer : IBufferRange) =
        ComputeCommand.SetBufferCmd(buffer, [| 0uy; 0uy; 0uy; 0uy |])

    static member Copy(src : IBufferRange, dst : IBufferRange) =
        ComputeCommand.CopyBufferCmd(src, dst)


    static member Download(src : IBufferRange, dst : nativeint) =
        ComputeCommand.DownloadBufferCmd(src, HostMemory.Unmanaged dst)

    static member Download<'T when 'T : unmanaged>(src : IBufferRange<'T>, dst : 'T[], dstIndex : int) =
        ComputeCommand.DownloadBufferCmd(src, HostMemory.Managed (dst, dstIndex))

    [<Obsolete("Use ComputeCommand.Download() instead.")>]
    static member Copy<'T when 'T : unmanaged>(src : IBufferRange<'T>, dst : 'T[], dstIndex : int) =
        ComputeCommand.Download(src, dst, dstIndex)

    static member Download<'T when 'T : unmanaged>(src : IBufferRange<'T>, dst : 'T[]) =
        ComputeCommand.DownloadBufferCmd(src, HostMemory.Managed (dst, 0))

    [<Obsolete("Use ComputeCommand.Download() instead.")>]
    static member Copy<'T when 'T : unmanaged>(src : IBufferRange<'T>, dst : 'T[]) =
        ComputeCommand.Download(src, dst)

    static member Download<'T when 'T : unmanaged>(src : IBufferRange<'T>, dst : nativeptr<'T>) =
        ComputeCommand.DownloadBufferCmd(src, HostMemory.Unmanaged (NativePtr.toNativeInt dst))

    [<Obsolete("Use ComputeCommand.Download() instead.")>]
    static member Copy<'T when 'T : unmanaged>(src : IBufferRange<'T>, dst : nativeptr<'T>) =
        ComputeCommand.Download(src, dst)


    static member Upload(src : nativeint, dst : IBufferRange) =
        ComputeCommand.UploadBufferCmd(HostMemory.Unmanaged src, dst)

    static member Upload<'T when 'T : unmanaged>(src : 'T[], srcIndex : int, dst : IBufferRange<'T>) =
        ComputeCommand.UploadBufferCmd(HostMemory.Managed (src :> Array, srcIndex), dst)

    [<Obsolete("Use ComputeCommand.Upload() instead.")>]
    static member Copy<'T when 'T : unmanaged>(src : 'T[], srcIndex : int, dst : IBufferRange<'T>) =
        ComputeCommand.Upload(src, srcIndex, dst)

    static member Upload<'T when 'T : unmanaged>(src : 'T[], dst : IBufferRange<'T>) =
        ComputeCommand.UploadBufferCmd(HostMemory.Managed (src :> Array, 0), dst)

    [<Obsolete("Use ComputeCommand.Upload() instead.")>]
    static member Copy<'T when 'T : unmanaged>(src : 'T[], dst : IBufferRange<'T>) =
        ComputeCommand.Upload(src, dst)

    static member Upload<'T when 'T : unmanaged>(src : nativeptr<'T>, dst : IBufferRange<'T>) =
        ComputeCommand.UploadBufferCmd(HostMemory.Unmanaged (NativePtr.toNativeInt src), dst)

    [<Obsolete("Use ComputeCommand.Upload() instead.")>]
    static member Copy<'T when 'T : unmanaged>(src : nativeptr<'T>, dst : IBufferRange<'T>) =
        ComputeCommand.Upload(src, dst)


    static member Sync(buffer : IBackendBuffer,
                       [<Optional; DefaultParameterValue(ResourceAccess.All)>] srcAccess : ResourceAccess,
                       [<Optional; DefaultParameterValue(ResourceAccess.All)>] dstAccess : ResourceAccess) =
        ComputeCommand.SyncBufferCmd(buffer, srcAccess, dstAccess)

    // ================================================================================================================
    // Images
    // ================================================================================================================

    static member Copy(src : ITextureLevel, srcOffset : V3i, dst : ITextureLevel, dstOffset : V3i, size : V3i) =
        ComputeCommand.CopyImageCmd(src, srcOffset, dst, dstOffset, size)

    static member TransformLayout(texture : IBackendTexture, srcLayout : TextureLayout, dstLayout : TextureLayout) =
        ComputeCommand.TransformSubLayoutCmd(texture.[texture.Format.Aspect, *, *], srcLayout, dstLayout)

    static member TransformLayout(texture : ITextureRange, srcLayout : TextureLayout, dstLayout : TextureLayout) =
        ComputeCommand.TransformSubLayoutCmd(texture, srcLayout, dstLayout)

    static member TransformLayout(texture : IBackendTexture, layout : TextureLayout) =
        ComputeCommand.TransformLayoutCmd(texture, layout)

    static member Sync(image : IBackendTexture,
                       [<Optional; DefaultParameterValue(ResourceAccess.All)>] srcAccess : ResourceAccess,
                       [<Optional; DefaultParameterValue(ResourceAccess.All)>] dstAccess : ResourceAccess) =
        ComputeCommand.SyncImageCmd(image, srcAccess, dstAccess)