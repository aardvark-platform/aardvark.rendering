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

type ArrayBuffer(data : Array) =
    interface IBuffer
    member x.Data = data


    override x.GetHashCode() = data.GetHashCode()
    override x.Equals o =
        match o with
            | :? ArrayBuffer as o -> System.Object.ReferenceEquals(o.Data,data)
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

type NativeMemoryBuffer(ptr : nativeint, sizeInBytes : int) =
    interface IBuffer

    member x.Ptr = ptr
    member x.SizeInBytes = sizeInBytes

    override x.GetHashCode() = HashCode.Combine(ptr.GetHashCode(),sizeInBytes)
    override x.Equals o =
        match o with
            | :? NativeMemoryBuffer as n ->
                n.Ptr = ptr && n.SizeInBytes = sizeInBytes
            | _ -> false