namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"

type IBuffer = interface end

type IIndirectBuffer =
    abstract member Buffer : IBuffer
    abstract member Count : int


type SingleValueBuffer(value : IMod<V4f>) =
    inherit Mod.AbstractMod<IBuffer>()

    member x.Value = value

    override x.Compute() = 
        let v = value.GetValue x
        failwithf "NullBuffer cannot be evaluated"

    override x.GetHashCode() = value.GetHashCode()
    override x.Equals o =
        match o with
            | :? SingleValueBuffer as o -> value = o.Value
            | _ -> false

    new() = SingleValueBuffer(Mod.constant V4f.Zero)


type BufferView(b : IMod<IBuffer>, elementType : Type, offset : int, stride : int) =
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

type IndirectBuffer(b : IBuffer, count : int) =
    member x.Buffer = b
    member x.Count = count

    interface IIndirectBuffer with
        member x.Buffer = b
        member x.Count = count


open System.Threading
type RenderTaskLock() =
    let rw = new ReaderWriterLockSlim()
    member x.Run f = ReaderWriterLock.write rw f
    member x.Update f = ReaderWriterLock.read rw f

type ILockedResource =
    abstract member Use         : (unit -> 'a) -> 'a
    abstract member AddLock     : RenderTaskLock -> unit
    abstract member RemoveLock  : RenderTaskLock -> unit

type IMappedIndirectBuffer =
    inherit IMod<IIndirectBuffer>
    inherit IDisposable
    inherit ILockedResource

    abstract member Indexed : bool
    abstract member Capacity : int
    abstract member Count : int with get, set
    abstract member Item : int -> DrawCallInfo with get, set
    abstract member Resize : int -> unit

type IMappedBuffer =
    inherit IMod<IBuffer>
    inherit IDisposable
    inherit ILockedResource

    abstract member Write : ptr : nativeint * offset : nativeint * sizeInBytes : nativeint -> unit
    abstract member Read : ptr : nativeint * offset : nativeint * sizeInBytes : nativeint -> unit
    abstract member UseWrite : offset : nativeint * sizeInBytes : nativeint * (nativeint -> 'a) -> 'a
    abstract member UseRead : offset : nativeint * sizeInBytes : nativeint * (nativeint -> 'a) -> 'a

    abstract member Capacity : nativeint
    abstract member Resize : newCapacity : nativeint -> unit
    abstract member OnDispose : IObservable<unit>


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =
    let ofArray (arr : DrawCallInfo[]) =
        IndirectBuffer(ArrayBuffer arr, arr.Length) :> IIndirectBuffer

    let ofList (l : list<DrawCallInfo>) =
        l |> List.toArray |> ofArray

    let count (b : IIndirectBuffer) = b.Count
    


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

        match view.SingleValue with
            | Some value ->
                value |> Mod.map (fun v -> reader.Initialize(v, count))
            | _ -> 
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

                        | _ ->
                            failwith "[BufferView] unknown buffer-type"
                )