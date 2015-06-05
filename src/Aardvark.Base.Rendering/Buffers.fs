namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices

type IBuffer = interface end

type BufferView(b : IBuffer, elementType : Type, offset : int, stride : int) =
    member x.Buffer = b
    member x.ElementType = elementType
    member x.Stride = stride
    member x.Offset = offset

    new(b : IBuffer, elementType : Type, offset : int) =
        BufferView(b, elementType, offset, 0)

    new(b : IBuffer, elementType : Type) =
        BufferView(b, elementType, 0, 0)

    override x.GetHashCode() =
        HashCode.Combine(b.GetHashCode(), elementType.GetHashCode(), offset.GetHashCode(), stride.GetHashCode())

    override x.Equals o =
        match o with
            | :? BufferView as o ->
                o.Buffer = b && o.ElementType = elementType && o.Offset = offset && o.Stride = stride
            | _ -> false

type ArrayBuffer(data : IMod<Array>) =
    interface IBuffer
    member x.Data = data


    override x.GetHashCode() = data.GetHashCode()
    override x.Equals o =
        match o with
            | :? ArrayBuffer as o -> o.Data = data
            | _ -> false