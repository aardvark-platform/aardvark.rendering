namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices

type IBuffer = interface end

type BufferView(b : IMod<IBuffer>, elementType : Type, offset : int, stride : int) =
    member x.Buffer = b
    member x.ElementType = elementType
    member x.Stride = stride
    member x.Offset = offset

    new(b : IMod<IBuffer>, elementType : Type, offset : int) =
        BufferView(b, elementType, offset, 0)

    new(b : IMod<IBuffer>, elementType : Type) =
        BufferView(b, elementType, 0, 0)

    override x.GetHashCode() =
        HashCode.Combine(b.GetHashCode(), elementType.GetHashCode(), offset.GetHashCode(), stride.GetHashCode())

    override x.Equals o =
        match o with
            | :? BufferView as o ->
                o.Buffer = b && o.ElementType = elementType && o.Offset = offset && o.Stride = stride
            | _ -> false

type NullBuffer(value : V4f) =
    interface IBuffer

    member x.Value = value

    override x.GetHashCode() = value.GetHashCode()
    override x.Equals o =
        match o with
            | :? NullBuffer as o -> value = o.Value
            | _ -> false

    new() = NullBuffer(V4f.Zero)


type INativeBuffer =
    inherit IBuffer
    abstract member SizeInBytes : int
    abstract member Use : (nativeint -> 'a) -> 'a
    abstract member Pin : unit -> nativeint
    abstract member Unpin : unit -> unit

type ArrayBuffer(data : Array) =
    let elementType = data.GetType().GetElementType()
    let mutable gchandle = Unchecked.defaultof<_>

    interface IBuffer
    member x.Data = data
    member x.ElementType = elementType

    interface INativeBuffer with
        member x.SizeInBytes = data.Length * Marshal.SizeOf elementType
        member x.Use (f : nativeint -> 'a) =
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            try f (gc.AddrOfPinnedObject())
            finally gc.Free()

        member x.Pin() =
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            gchandle <- gc
            gc.AddrOfPinnedObject()

        member x.Unpin() =
            gchandle.Free()
            gchandle <- Unchecked.defaultof<_>

    override x.GetHashCode() = data.GetHashCode()
    override x.Equals o =
        match o with
            | :? ArrayBuffer as o -> System.Object.ReferenceEquals(o.Data,data)
            | _ -> false

type NativeMemoryBuffer(ptr : nativeint, sizeInBytes : int) =
    interface INativeBuffer with
        member x.SizeInBytes = sizeInBytes
        member x.Use f = f ptr
        member x.Pin() = ptr
        member x.Unpin() = ()

    member x.Ptr = ptr
    member x.SizeInBytes = sizeInBytes

    override x.GetHashCode() = HashCode.Combine(ptr.GetHashCode(),sizeInBytes)
    override x.Equals o =
        match o with
            | :? NativeMemoryBuffer as n ->
                n.Ptr = ptr && n.SizeInBytes = sizeInBytes
            | _ -> false


type IMappedBuffer =
    inherit IMod<IBuffer>
    inherit IDisposable

    abstract member Write : ptr : nativeint * offset : int * sizeInBytes : int -> unit
    abstract member Read : ptr : nativeint * offset : int * sizeInBytes : int -> unit

    abstract member Capacity : int
    abstract member Resize : newCapacity : int -> unit
    abstract member OnDispose : IObservable<unit>