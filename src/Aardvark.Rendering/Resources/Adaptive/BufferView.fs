namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open Aardvark.Base

type BufferView(b : aval<IBuffer>, elementType : Type, [<Optional; DefaultParameterValue(0)>] offset : int, [<Optional; DefaultParameterValue(0)>] stride : int) =
    let singleValue =
        match b with
        | :? SingleValueBuffer as nb -> Some nb.Value
        | _ -> None

    member x.Buffer = b
    member x.ElementType = elementType
    member x.Stride = stride
    member x.Offset = offset
    member x.SingleValue = singleValue
    member x.IsSingleValue = Option.isSome singleValue

    new(b : aval<IBackendBuffer>, elementType : Type, [<Optional; DefaultParameterValue(0)>] offset : int, [<Optional; DefaultParameterValue(0)>] stride : int) =
        BufferView(AdaptiveResource.cast<IBuffer> b, elementType, offset, stride)

    new(b : IBuffer, elementType : Type, [<Optional; DefaultParameterValue(0)>] offset : int, [<Optional; DefaultParameterValue(0)>] stride : int) =
        BufferView(AVal.constant b, elementType, offset, stride)

    new(arr : System.Array, [<Optional; DefaultParameterValue(0)>] offset : int, [<Optional; DefaultParameterValue(0)>] stride : int) =
        let t = arr.GetType().GetElementType()
        BufferView(ArrayBuffer arr, t, offset, stride)

    override x.GetHashCode() =
        HashCode.Combine(b.GetHashCode(), elementType.GetHashCode(), offset.GetHashCode(), stride.GetHashCode())

    override x.Equals o =
        match o with
        | :? BufferView as o ->
            o.Buffer = b && o.ElementType = elementType && o.Offset = offset && o.Stride = stride
        | _ -> false

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferView =

    let ofArray (arr : Array) =
        BufferView(arr)

    [<AbstractClass>]
    type private Reader() =
        static let cache = Dict<Type, Reader>()

        static member Get (t : Type) =
            cache.GetOrCreate(t, fun t ->
                let rt = typedefof<Reader<int>>.MakeGenericType [|t|]
                let instance = Activator.CreateInstance rt
                instance |> unbox<Reader>
            )

        abstract member Read : ptr : nativeint * count : int * stride : int -> Array
        abstract member Initialize : 'a * int -> Array

    and private Reader<'a when 'a : unmanaged>() =
        inherit Reader()

        override x.Read(ptr : nativeint, count : int, stride : int) =
            let arr : 'a[] = Array.zeroCreate count

            if stride = 0 || stride = sizeof<'a> then
                let size = count * sizeof<'a>
                let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                try NativeInt.memcpy ptr (gc.AddrOfPinnedObject()) size
                finally gc.Free()
            else
                let step = nativeint stride
                let mutable current = ptr
                for i in 0 .. count - 1 do
                    arr.[i] <- NativeInt.read current
                    current <- current + step

            arr :> Array

        override x.Initialize (value : 'x, count : int) =
            let conv = PrimitiveValueConverter.converter<'x, 'a>
            Array.create count (conv value) :> Array

    let download (startIndex : int) (count : int) (view : BufferView) =
        let elementType = view.ElementType
        let elementSize = Marshal.SizeOf elementType
        let reader = Reader.Get elementType
        let offset = view.Offset + elementSize * startIndex

        let stride =
            if view.Stride = 0 then elementSize
            else view.Stride

        match view.SingleValue with
        | Some value ->
            value |> AdaptiveResource.map (fun v -> reader.Initialize(v, count))
        | _ ->
            view.Buffer |> AdaptiveResource.map (fun b ->
                match b with
                | :? ArrayBuffer as a when stride = elementSize && offset = 0 ->
                    if count = a.Data.Length then
                        a.Data
                    else
                        if count > a.Data.Length then
                            raise <| IndexOutOfRangeException("[BufferView] trying to download too many elements from ArrayBuffer")

                        let res = Array.CreateInstance(elementType, count)
                        Array.Copy(a.Data, res, count)
                        res

                | :? INativeBuffer as b ->
                    let available = (b.SizeInBytes - nativeint view.Offset) / nativeint elementSize
                    if count > int available then
                        raise <| IndexOutOfRangeException("[BufferView] trying to download too many elements from NativeBuffer")

                    b.Use (fun ptr -> reader.Read(ptr + nativeint offset, count, stride))

                | _ ->
                    failwith "[BufferView] unknown buffer-type"
            )
