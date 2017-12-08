namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop
open System.Runtime.CompilerServices

#nowarn "9"

//[<RequireQualifiedAccess>]
type TextureLayout =
    | Sample            = 1
    | TransferRead      = 2
    | TransferWrite     = 3
    | ShaderRead        = 4
    | ShaderWrite       = 5
    | ShaderReadWrite   = 6

type IComputeShader =
    abstract member Runtime : IComputeRuntime
    abstract member LocalSize : V3i

and IComputeShaderInputBinding =
    inherit IDisposable
    abstract member Shader : IComputeShader
    abstract member Item : string -> obj with set
    abstract member Flush : unit -> unit

and IComputeRuntime =
    inherit IBufferRuntime
    inherit ITextureRuntime

    abstract member MaxLocalSize : V3i
    abstract member CreateComputeShader : FShade.ComputeShader -> IComputeShader
    abstract member DeleteComputeShader : IComputeShader -> unit
    abstract member NewInputBinding : IComputeShader -> IComputeShaderInputBinding
    abstract member Run : list<ComputeCommand> -> unit
    abstract member Compile : list<ComputeCommand> -> ComputeProgram<unit>

and [<AbstractClass>]
    ComputeProgram<'r>() =
    abstract member Run : unit -> 'r
    abstract member RunUnit : unit -> unit
    abstract member Dispose : unit -> unit

    default x.Run() = x.RunUnit(); Unchecked.defaultof<'r>
    default x.RunUnit() = x.Run() |> ignore

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and [<RequireQualifiedAccess>]
    HostMemory =
    | Managed of arr : Array * index : int
    | Unmanaged of nativeint

and [<RequireQualifiedAccess>]
    ComputeCommand =
    | BindCmd of shader : IComputeShader
    | SetInputCmd of inputs : IComputeShaderInputBinding
    | DispatchCmd of groups : V3i
    | CopyBufferCmd of src : IBufferRange * dst : IBufferRange
    | DownloadBufferCmd of src : IBufferRange * dst : HostMemory
    | UploadBufferCmd of src : HostMemory * dst : IBufferRange

    | SyncBufferCmd of buffer : IBackendBuffer
    | TransformLayoutCmd of tex : IBackendTexture * layout : TextureLayout

    | ExecuteCmd of ComputeProgram<unit>


    | CopyImageCmd of src : ITextureLevel * srcOffset : V3i * dst : ITextureLevel * dstOffset : V3i * size : V3i
    | DownloadImageCmd of src : ITextureSubResource * srcOffset : V3i * dst : PixVolume
    | UploadImageCmd of src : PixVolume * dst : ITextureSubResource * dstOffset : V3i

    static member Execute (other : ComputeProgram<unit>) =
        ComputeCommand.ExecuteCmd other

    static member Sync b = SyncBufferCmd b

    static member Bind(shader : IComputeShader) =
        ComputeCommand.BindCmd shader
            
    static member SetInput(input : IComputeShaderInputBinding) =
        ComputeCommand.SetInputCmd input

    static member TransformLayout(tex : IBackendTexture, layout : TextureLayout) =
        ComputeCommand.TransformLayoutCmd(tex, layout)
            
    static member Dispatch(groups : V3i) =
        ComputeCommand.DispatchCmd groups

    static member Dispatch(groups : V2i) =
        ComputeCommand.DispatchCmd (V3i(groups, 1))

    static member Dispatch(groups : int) =
        ComputeCommand.DispatchCmd (V3i(groups, 1, 1))

    static member Copy(src : IBufferRange, dst : IBufferRange) =
        ComputeCommand.CopyBufferCmd(src, dst)

    static member Copy<'a when 'a : unmanaged>(src : IBufferRange<'a>, dst : 'a[], dstIndex : int) =
        ComputeCommand.DownloadBufferCmd(src, HostMemory.Managed (dst :> Array, dstIndex))

    static member Copy<'a when 'a : unmanaged>(src : IBufferRange<'a>, dst : 'a[]) =
        ComputeCommand.DownloadBufferCmd(src, HostMemory.Managed (dst :> Array, 0))

    static member Copy<'a when 'a : unmanaged>(src : 'a[], srcIndex : int, dst : IBufferRange<'a>) =
        ComputeCommand.UploadBufferCmd(HostMemory.Managed (src :> Array, srcIndex), dst)
            
    static member Copy<'a when 'a : unmanaged>(src : 'a[], dst : IBufferRange<'a>) =
        ComputeCommand.UploadBufferCmd(HostMemory.Managed (src :> Array, 0), dst)

    static member Copy<'a when 'a : unmanaged>(src : IBufferRange<'a>, dst : nativeptr<'a>) =
        ComputeCommand.DownloadBufferCmd(src, HostMemory.Unmanaged (NativePtr.toNativeInt dst))

    static member Copy<'a when 'a : unmanaged>(src : nativeptr<'a>, dst : IBufferRange<'a>) =
        ComputeCommand.UploadBufferCmd(HostMemory.Unmanaged (NativePtr.toNativeInt src), dst)



[<AbstractClass; Sealed; Extension>]
type IComputeRuntimeExtensions private() =

    [<Extension>]
    static member CreateComputeShader (this : IComputeRuntime, shader : 'a -> 'b) =
        let sh = FShade.ComputeShader.ofFunction this.MaxLocalSize shader
        this.CreateComputeShader(sh)
        
    [<Extension>]
    static member Invoke (this : IComputeRuntime, cShader : IComputeShader, groupCount : V3i, input : IComputeShaderInputBinding) =
        this.Run [
            ComputeCommand.Bind cShader
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch groupCount
        ]
        
    [<Extension>]
    static member Invoke (this : IComputeRuntime, cShader : IComputeShader, groupCount : V2i, input : IComputeShaderInputBinding) =
        IComputeRuntimeExtensions.Invoke(this, cShader, V3i(groupCount, 1), input)

    [<Extension>]
    static member Invoke (this : IComputeRuntime, cShader : IComputeShader, groupCount : int, input : IComputeShaderInputBinding) =
        IComputeRuntimeExtensions.Invoke(this, cShader, V3i(groupCount, 1, 1), input)

    [<Extension>]
    static member Invoke (this : IComputeRuntime, cShader : IComputeShader, groupCount : V3i, values : seq<string * obj>) =
        use i = this.NewInputBinding cShader
        for (name, value) in values do
            i.[name] <- value
        i.Flush()
        IComputeRuntimeExtensions.Invoke(this, cShader, groupCount, i)

    [<Extension>]
    static member Invoke (this : IComputeRuntime, cShader : IComputeShader, groupCount : V2i, values : seq<string * obj>) =
        IComputeRuntimeExtensions.Invoke(this, cShader, V3i(groupCount, 1), values)

    [<Extension>]
    static member Invoke (this : IComputeRuntime, cShader : IComputeShader, groupCount : int, values : seq<string * obj>) =
        IComputeRuntimeExtensions.Invoke(this, cShader, V3i(groupCount, 1, 1), values)


    [<Extension>]
    static member Invoke (cShader : IComputeShader, groupCount : V3i, input : IComputeShaderInputBinding) =
        IComputeRuntimeExtensions.Invoke(cShader.Runtime, cShader, groupCount, input)

    [<Extension>]
    static member Invoke (cShader : IComputeShader, groupCount : V2i, input : IComputeShaderInputBinding) =
        IComputeRuntimeExtensions.Invoke(cShader.Runtime, cShader, groupCount, input)

    [<Extension>]
    static member Invoke (cShader : IComputeShader, groupCount : int, input : IComputeShaderInputBinding) =
        IComputeRuntimeExtensions.Invoke(cShader.Runtime, cShader, groupCount, input)

    
    [<Extension>]
    static member Invoke (cShader : IComputeShader, groupCount : V3i, values : seq<string * obj>) =
        IComputeRuntimeExtensions.Invoke(cShader.Runtime, cShader, groupCount, values)

    [<Extension>]
    static member Invoke (cShader : IComputeShader, groupCount : V2i, values : seq<string * obj>) =
        IComputeRuntimeExtensions.Invoke(cShader.Runtime, cShader, groupCount, values)

    [<Extension>]
    static member Invoke (cShader : IComputeShader, groupCount : int, values : seq<string * obj>) =
        IComputeRuntimeExtensions.Invoke(cShader.Runtime, cShader, groupCount, values)