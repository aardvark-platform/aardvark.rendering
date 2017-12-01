namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System.Runtime.InteropServices
open Microsoft.FSharp.Quotations
open System.Runtime.CompilerServices
open Aardvark.Rendering.Vulkan

type Buffer<'a when 'a : unmanaged>(device : Device, handle : VkBuffer, mem : DevicePtr, count : int64) =
    inherit Buffer(device, handle, mem, int64 sizeof<'a> * count)

    static let sl = sizeof<'a> |> int64

    member x.Count = count

    member x.Upload(src : 'a[], srcIndex: int64, dstIndex : int64, count : int64) =
        let size = count * sl
        let srcOffset = nativeint (srcIndex * sl)
        let dstOffset = dstIndex * sl

        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit size
        let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
        try 
            temp.Memory.Mapped(fun ptr ->
                Marshal.Copy(gc.AddrOfPinnedObject() + srcOffset, ptr, nativeint size)
            )
        finally 
            gc.Free()

        device.perform {
            try do! Command.Copy(temp, 0L, x, dstOffset, size)
            finally device.Delete temp
        }

    member x.Upload(src : 'a[], count : int64) =
        x.Upload(src, 0L, 0L, count)

    member x.Upload(src : 'a[]) =
        x.Upload(src, 0L, 0L, min src.LongLength count)

    member x.Download(srcIndex : int64, dst : 'a[], dstIndex : int64, count : int64) =
        let size = count * sl
        let dstOffset = nativeint (dstIndex * sl)
        let srcOffset = srcIndex * sl

        let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size

        device.perform {
            do! Command.Copy(x, srcOffset, temp, 0L, size)
        }

        let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
        try 
            temp.Memory.Mapped(fun ptr ->
                Marshal.Copy(ptr, gc.AddrOfPinnedObject() + dstOffset, nativeint size)
            )
        finally 
            gc.Free()
            device.Delete temp

    member x.Download(dst : 'a[], count : int64) =
        x.Download(0L, dst, 0L, count)

    member x.Download(dst : 'a[]) =
        x.Download(0L, dst, 0L, min count dst.LongLength)
        
    member x.Download() =
        let res = Array.zeroCreate (int count)
        x.Download(0L, res, 0L, count)
        res
        
    interface IBufferVector<'a> with
        member x.Buffer = x
        member x.Offset = 0L
        member x.Delta = 1L
        member x.Size = count

and IBufferVector<'a when 'a : unmanaged> =
    abstract member Buffer : Buffer<'a>
    abstract member Offset : int64
    abstract member Delta : int64
    abstract member Size : int64

and private BufferVector<'a when 'a : unmanaged>(b : Buffer<'a>, offset : int64, delta : int64, size : int64) =
    member x.Buffer = b
    member x.Offset = offset
    member x.Delta = delta
    member x.Size = size

    interface IBufferVector<'a> with
        member x.Buffer = x.Buffer
        member x.Offset = x.Offset
        member x.Delta = x.Delta
        member x.Size = x.Size

    member x.Skip(n : int64) =
        BufferVector<'a>(b, offset + n * delta, delta, size - n)

    member x.Strided(n : int64) =
        BufferVector<'a>(b, offset, n * delta, 1L + (size - 1L) / n)
            

    new(b : Buffer<'a>) = BufferVector<'a>(b, 0L, 1L, b.Count)


[<AbstractClass; Sealed; Extension>]
type DeviceTypedBufferExtensions private() =
    static let usage =
        VkBufferUsageFlags.TransferSrcBit ||| 
        VkBufferUsageFlags.TransferDstBit |||
        VkBufferUsageFlags.StorageBufferBit

            
    [<Extension>]
    static member GetSlice(b : IBufferVector<'a>, l : Option<int64>, h : Option<int64>) =
        let l = match l with | Some l -> max 0L l | None -> 0L
        let h = match h with | Some h -> min (b.Size - 1L) h | None -> b.Size - 1L
        BufferVector<'a>(b.Buffer, b.Offset + l * b.Delta, b.Delta, 1L + h - l) :> IBufferVector<_>

    [<Extension>]
    static member GetSlice(b : IBufferVector<'a>, l : Option<int>, h : Option<int>) =
        let l = match l with | Some l -> max 0L (int64 l) | None -> 0L
        let h = match h with | Some h -> min (b.Size - 1L) (int64 h) | None -> b.Size - 1L
        BufferVector<'a>(b.Buffer, b.Offset + l * b.Delta, b.Delta, 1L + h - l) :> IBufferVector<_>
        
    [<Extension>]
    static member Skip(b : IBufferVector<'a>, n : int64) =
        BufferVector<'a>(b.Buffer, b.Offset + n * b.Delta, b.Delta, b.Size - n) :> IBufferVector<_>

    [<Extension>]
    static member Skip(b : IBufferVector<'a>, n : int) =
        BufferVector<'a>(b.Buffer, b.Offset + int64 n * b.Delta, b.Delta, b.Size - int64 n) :> IBufferVector<_>
            
    [<Extension>]
    static member Strided(b : IBufferVector<'a>, delta : int64) =
        BufferVector<'a>(b.Buffer, b.Offset, delta * b.Delta, 1L + (b.Size - 1L) / delta) :> IBufferVector<_>
            
    [<Extension>]
    static member Strided(b : IBufferVector<'a>, delta : int) =
        BufferVector<'a>(b.Buffer, b.Offset, int64 delta * b.Delta, 1L + (b.Size - 1L) / int64 delta) :> IBufferVector<_>


    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(device : Device, count : int64) =
        let b = device.CreateBuffer(usage, int64 sizeof<'a> * count)
        new Buffer<'a>(b.Device, b.Handle, b.Memory, count)
            
    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(device : Device, data : 'a[]) =
        let b = DeviceTypedBufferExtensions.CreateBuffer<'a>(device, data.LongLength)
        b.Upload(data)
        b

    [<Extension>]
    static member Coerce<'a when 'a : unmanaged>(buffer : Buffer) =
        new Buffer<'a>(buffer.Device, buffer.Handle, buffer.Memory, buffer.Size / int64 sizeof<'a>)
