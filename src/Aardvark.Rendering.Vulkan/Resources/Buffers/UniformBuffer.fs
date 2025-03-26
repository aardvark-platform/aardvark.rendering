namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base

open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Microsoft.FSharp.Reflection

#nowarn "9"
// #nowarn "51"

type UnmanagedStruct(ptr : nativeint, size : int) =
    let mutable ptr = ptr
    let mutable size = size

    member x.Pointer = ptr
    member x.Size = size

    member x.WriteUnsafe(offset : int, value : 'a) =
        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.write ptr value

    member x.Write(offset : int, value : 'a) =
        if ptr = 0n then
            raise <| ObjectDisposedException("UnmanagedStruct")

        if offset < 0 || offset + sizeof<'a> > size then
            raise <| IndexOutOfRangeException(sprintf "%d < 0 || %d > %d" offset (offset + sizeof<'a>) size)

        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.write ptr value

    member x.Read(offset : int) : 'a =
        if ptr = 0n then
            raise <| ObjectDisposedException("UnmanagedStruct")

        if offset < 0 || offset + sizeof<'a> > size then
            raise <| IndexOutOfRangeException()

        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.read ptr

    member x.Dispose() =
        if ptr <> 0n then
            Marshal.FreeHGlobal ptr
            ptr <- 0n
            size <- 0

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UnmanagedStruct =
    
    let Null = new UnmanagedStruct(0n, 0)

    let alloc (size : int) =
        let ptr = Marshal.AllocHGlobal size
        new UnmanagedStruct(ptr, size)

    let inline free (s : UnmanagedStruct) =
        s.Dispose()
    
    let inline write (s : UnmanagedStruct) (offset : int) (value : 'a) =
        s.Write(offset, value)

    let inline writeUnsafe (s : UnmanagedStruct) (offset : int) (value : 'a) =
        s.WriteUnsafe(offset, value)


    let inline read (s : UnmanagedStruct) (offset : int) =
        s.Read(offset)




type UniformBuffer =
    class
        inherit BufferDecorator
        val public Storage : UnmanagedStruct
        val public Layout : FShade.GLSL.GLSLUniformBuffer

        override x.Destroy() =
            UnmanagedStruct.free x.Storage
            base.Destroy()

        new(buffer : Buffer, storage : UnmanagedStruct, layout : FShade.GLSL.GLSLUniformBuffer) =
            { inherit BufferDecorator(buffer); Storage = storage; Layout = layout }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UniformBuffer =

    let create (layout : FShade.GLSL.GLSLUniformBuffer) (device : Device) =
        let size = layout.ubSize
        let storage = UnmanagedStruct.alloc size

        //let align = device.MinUniformBufferOffsetAlignment
        //let alignedSize = Alignment.next align (int64 size)

        let buffer = device.CreateBuffer(VkBufferUsageFlags.UniformBufferBit ||| VkBufferUsageFlags.TransferDstBit, uint64 size)

        new UniformBuffer(buffer, storage, layout)


    let upload (b : UniformBuffer) (device : Device) =
        use t = device.Token

        let upload =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd = 
                    cmd.AppendCommand()
                    VkRaw.vkCmdUpdateBuffer(cmd.Handle, b.Handle, 0UL, uint64 b.Storage.Size, b.Storage.Pointer)
                    cmd.AddResource b
            }

        t.enqueue {
            do! upload
            do! Command.Sync(b, VkPipelineStageFlags.TransferBit, VkAccessFlags.TransferWriteBit)
        }

[<AbstractClass; Sealed>]
type ContextUniformBufferExtensions private() =
    [<Extension>]
    static member inline CreateUniformBuffer(this : Device, layout : FShade.GLSL.GLSLUniformBuffer) =
        this |> UniformBuffer.create layout

    [<Extension>]
    static member inline Upload(this : Device, b : UniformBuffer) =
        this |> UniformBuffer.upload b