namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"

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


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferView =
    
    let ofArray (arr : Array) =
        let t = arr.GetType().GetElementType()
        BufferView(Mod.constant (ArrayBuffer arr :> IBuffer), t)


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

        view.Buffer |> Mod.map (fun b -> 
            match b with
                | :? ArrayBuffer as a when stride = elementSize && offset = 0 ->
                    if count = a.Data.Length then 
                        a.Data
                    else
                        let res = Array.CreateInstance(elementType, count)
                        Array.Copy(a.Data, res, count)
                        res

                | :? INativeBuffer as b ->
                    let available = (b.SizeInBytes - view.Offset) / elementSize
                    if count > available then
                        raise <| IndexOutOfRangeException("[BufferView] trying to download too many elements")

                    b.Use (fun ptr -> reader.Read(ptr + nativeint offset, count, stride))

                | :? NullBuffer as nb ->
                    reader.Initialize(nb.Value, count)

                | _ ->
                    failwith "[BufferView] unknown buffer-type"
        )