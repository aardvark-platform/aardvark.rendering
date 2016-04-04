namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base


type Buffer =
    class
        val mutable public Context : Context
        val mutable public Handle : VkBuffer
        val mutable public Format : VkFormat
        val mutable public Memory : deviceptr
        val mutable public Size : int64
        val mutable public Flags : VkBufferUsageFlags
        member x.Device = x.Context.Device

        new(ctx, handle, fmt, mem, flags) = { Context = ctx; Handle = handle; Format = fmt; Memory = mem; Size = DevicePtr.size mem; Flags = flags }
    end

type BufferView =
    class 
        val mutable public Buffer : Buffer
        val mutable public Handle : VkBufferView
        val mutable public Format : VkFormat
        val mutable public Offset : int64
        val mutable public Size : int64
        member x.Device = x.Buffer.Device

        new(b, h, f, o, s) = { Buffer = b; Handle = h; Format = f; Offset = o; Size = s }
    end

[<AbstractClass; Sealed; Extension>]
type ContextBufferExtensions private() =

    static let typeFormats =
        Dict.ofList [
            typeof<float32>, VkFormat.R32Sfloat
            typeof<V2f>, VkFormat.R32g32Sfloat
            typeof<V3f>, VkFormat.R32g32b32Sfloat
            typeof<V4f>, VkFormat.R32g32b32a32Sfloat

            typeof<int>, VkFormat.R32Sint
            typeof<V2i>, VkFormat.R32g32Sint
            typeof<V3i>, VkFormat.R32g32b32Sint
            typeof<V4i>, VkFormat.R32g32b32a32Sint

            typeof<uint32>, VkFormat.R32Uint
            typeof<uint16>, VkFormat.R16Uint
            typeof<uint8>, VkFormat.R8Uint
            typeof<C4b>, VkFormat.R8g8b8a8Unorm
            typeof<C4us>, VkFormat.R16g16b16a16Unorm
            typeof<C4ui>, VkFormat.R32g32b32a32Uint
            typeof<C4f>, VkFormat.R32g32b32a32Sfloat
        ]

    static let setFormat (this : Buffer, arr : Array) =
        if this.Format = VkFormat.Undefined then
            let fmt =
                match typeFormats.TryGetValue (arr.GetType().GetElementType()) with
                    | (true, fmt) -> fmt
                    | _ -> VkFormat.Undefined
            this.Format <- fmt  

    [<Extension>]
    static member CreateBuffer (this : Context, size : int64, flags : VkBufferUsageFlags) =
        
        // allocate device memory
        let memory = this.DeviceLocalMemory.Alloc size

        // create a buffer
        let mutable info =
            VkBufferCreateInfo(
                VkStructureType.BufferCreateInfo,
                0n, VkBufferCreateFlags.None,
                uint64 size,
                flags,
                VkSharingMode.Exclusive,
                0u,
                NativePtr.zero
            )
        let mutable buffer = VkBuffer.Null

        VkRaw.vkCreateBuffer(this.Device.Handle, &&info, NativePtr.zero, &&buffer) 
            |> check "vkCreateBuffer"

        let mem, off, size =
            match memory.Pointer with
                | Real(mem,s) -> mem, 0L, s
                | View(mem,o,s) -> mem, o, s
                | Null -> devicemem.Null, 0L, 0L
                | Managed(_,b,s) -> b.Memory, b.Offset, s

        // bind the buffer to the device memory
        VkRaw.vkBindBufferMemory(this.Device.Handle, buffer, mem.Handle, uint64 off) 
            |> check "vkBindBufferMemory"

        Buffer(this, buffer, VkFormat.Undefined, memory, flags)

    [<Extension>]
    static member Delete (this : Context, buffer : Buffer) =
        if buffer.Handle.IsValid then
            buffer.Memory.Dispose()
            VkRaw.vkDestroyBuffer(this.Device.Handle, buffer.Handle, NativePtr.zero)
            buffer.Handle <- VkBuffer.Null


    [<Extension>]
    static member Upload (this : Buffer, data : Array, start : int, count : int) =
        setFormat(this, data)
        this.Memory.Upload(data, start, count)

    [<Extension>]
    static member Upload (this : Buffer, data : Array) =
        setFormat(this, data)
        this.Memory.Upload(data, 0, data.Length)

    [<Extension>]
    static member Upload (this : Buffer, data : nativeint, size : int64) =
        this.Memory.Upload(data, size)

    [<Extension>]
    static member Download (this : Buffer, target : Array, start : int, count : int) =
        this.Memory.Download(target, start, count)

    [<Extension>]
    static member Download (this : Buffer, target : Array) =
        this.Memory.Download(target, 0, target.Length)

    [<Extension>]
    static member Download<'a> (this : Buffer) : Command<'a[]> =
        this.Memory.Download()

[<AbstractClass; Sealed; Extension>]
type ContextBufferViewExtensions private() =
    
    [<Extension>]
    static member CreateBufferView(x : Context, buffer : Buffer, format : VkFormat, offset : int64, size : int64) =
        let mutable info =
            VkBufferViewCreateInfo(
                VkStructureType.BufferViewCreateInfo,
                0n, VkBufferViewCreateFlags.MinValue,
                buffer.Handle,
                format,
                uint64 offset,
                uint64 size
            )

        let mutable view = VkBufferView.Null
        VkRaw.vkCreateBufferView(x.Device.Handle, &&info, NativePtr.zero, &&view) |> check "vkCreateBufferView"

        BufferView(buffer, view, format, offset, size)

    [<Extension>]
    static member CreateBufferView(x : Context, buffer : Buffer, offset : int64, size : int64) =
        ContextBufferViewExtensions.CreateBufferView(x, buffer, buffer.Format, offset, size)

    [<Extension>]
    static member CreateBufferView(x : Context, buffer : Buffer, offset : int64) =
        ContextBufferViewExtensions.CreateBufferView(x, buffer, buffer.Format, offset, buffer.Size - offset)

    [<Extension>]
    static member CreateBufferView(x : Context, buffer : Buffer) =
        ContextBufferViewExtensions.CreateBufferView(x, buffer, buffer.Format, 0L, int64 buffer.Size)

    [<Extension>]
    static member CreateBufferView(x : Context, buffer : Buffer, format : VkFormat) =
        ContextBufferViewExtensions.CreateBufferView(x, buffer, format, 0L, int64 buffer.Size)

    [<Extension>]
    static member Delete(x : Context, view : BufferView) =
        if view.Handle.IsValid then
            VkRaw.vkDestroyBufferView(x.Device.Handle, view.Handle, NativePtr.zero)
            view.Handle <- VkBufferView.Null

