namespace Aardvark.Base

open System
open System.Collections.Generic
open System.Threading
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<Flags>]
type BufferUsage = // Buffer usage 
    | None = 0
    | Index = 1
    | Indirect = 2
    | Vertex = 4
    | Uniform = 8
    | Storage = 16
    | Read = 256
    | Write = 512
    | ReadWrite = 0x300
    | Default = 0x031f
            
type IBuffer = interface end

type INativeBuffer =
    inherit IBuffer
    abstract member SizeInBytes : int
    abstract member Use : (nativeint -> 'a) -> 'a
    abstract member Pin : unit -> nativeint
    abstract member Unpin : unit -> unit

type IBackendBuffer =
    inherit IBuffer // ISSUE: this allows a backend buffer to be used everywhere even if it is restricted to a specific type -> HOWEVER buffers can have multiple mixed usage flags -> interface restriction not possible
    inherit IDisposable
    abstract member Runtime : IBufferRuntime
    abstract member Handle : obj
    abstract member SizeInBytes : nativeint
        
and IBufferRuntime =
    /// Deletes a buffer and releases all GPU resources and API handles
    abstract member DeleteBuffer : IBackendBuffer -> unit
    /// Prepares a buffer, allocating and uploading the data to GPU memory
    /// If the buffer is an IBackendBuffer the operation performs NOP
    abstract member PrepareBuffer : data : IBuffer * usage : BufferUsage -> IBackendBuffer
    /// Creates a GPU buffer with the specified size in bytes and usage
    abstract member CreateBuffer : size : nativeint * usage : BufferUsage -> IBackendBuffer
    abstract member Copy : srcData : nativeint * dst : IBackendBuffer * dstOffset : nativeint * size : nativeint -> unit
    abstract member Copy : srcBuffer : IBackendBuffer * srcOffset : nativeint * dstData : nativeint * size : nativeint -> unit
    abstract member Copy : srcBuffer : IBackendBuffer * srcOffset : nativeint * dstBuffer : IBackendBuffer * dstOffset : nativeint * size : nativeint -> unit

    abstract member CopyAsync : srcBuffer : IBackendBuffer * srcOffset : nativeint * dstData : nativeint * size : nativeint -> (unit -> unit)
        
        
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

type SingleValueBuffer(value : aval<V4f>) =
    inherit AVal.AbstractVal<IBuffer>()

    member x.Value = value

    override x.Compute(token) = 
        let v = value.GetValue token
        ArrayBuffer [|v|] :> IBuffer

    override x.GetHashCode() = value.GetHashCode()
    override x.Equals o =
        match o with
            | :? SingleValueBuffer as o -> value = o.Value
            | _ -> false

    new() = SingleValueBuffer(AVal.constant V4f.Zero)


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

type BufferView(b : aval<IBuffer>, elementType : Type, offset : int, stride : int) =
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

    new(b : aval<IBuffer>, elementType : Type, offset : int) =
        BufferView(b, elementType, offset, 0)

    new(b : aval<IBuffer>, elementType : Type) =
        BufferView(b, elementType, 0, 0)

    override x.GetHashCode() =
        HashCode.Combine(b.GetHashCode(), elementType.GetHashCode(), offset.GetHashCode(), stride.GetHashCode())

    override x.Equals o =
        match o with
            | :? BufferView as o ->
                o.Buffer = b && o.ElementType = elementType && o.Offset = offset && o.Stride = stride
            | _ -> false


/// native indirect buffers are supposed to have data layout as required by the graphics API, currently this is equal for OpenGL, Vulkan (and DX11)
/// Indexed:     { uint count; uint primCount; uint firstIndex; uint baseVertex; uint baseInstance; }
/// Non-indexed: { uint count; uint primCount; uint first; uint baseInstance; }
/// NOTE: indexed or non-indexed only matters if directly an IBackendBuffer is used, ArrayBuffers will be uploaded according to if there are indices present
///        -> constructor intended to be used with IBackendBuffer, other should use IndirectBuffer module
type IndirectBuffer(b : IBuffer, count : int, stride : int, indexed : bool) =
    member x.Buffer = b
    member x.Count = count
    member x.Stride = stride
    member x.Indexed = indexed

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =
    let ofArray (indexed : bool) (arr : DrawCallInfo[]) =
        IndirectBuffer(ArrayBuffer arr, arr.Length, sizeof<DrawCallInfo>, indexed)

    let ofList (indexed : bool) (l : list<DrawCallInfo>) =
        l |> List.toArray |> ofArray indexed

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferView =
    
    let ofArray (arr : Array) =
        let t = arr.GetType().GetElementType()
        BufferView(AVal.constant (ArrayBuffer arr :> IBuffer), t)


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
                value |> AVal.map (fun v -> reader.Initialize(v, count))
            | _ -> 
                view.Buffer |> AVal.map (fun b -> 
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
                            let available = (b.SizeInBytes - view.Offset) / elementSize
                            if count > available then
                                raise <| IndexOutOfRangeException("[BufferView] trying to download too many elements from NativeBuffer")

                            b.Use (fun ptr -> reader.Read(ptr + nativeint offset, count, stride))

                        | _ ->
                            failwith "[BufferView] unknown buffer-type"
                )




type IBufferRange =
    abstract member Buffer : IBackendBuffer
    abstract member Offset : nativeint
    abstract member Size : nativeint

    
    
type IBufferVector<'a when 'a : unmanaged> =
    abstract member Buffer : IBackendBuffer
    abstract member Origin : int
    abstract member Delta : int
    abstract member Count : int

type IBufferRange<'a when 'a : unmanaged> =
    inherit IBufferRange
    inherit IBufferVector<'a>
    abstract member Count : int

type IBuffer<'a when 'a : unmanaged> =
    inherit IBuffer
    inherit IBufferRange<'a>
    inherit IDisposable
  
[<AutoOpen>]
module private RuntimeBufferImplementation =

    type RuntimeBufferVector<'a when 'a : unmanaged>(buffer : IBackendBuffer, origin : int, delta : int, count : int) =
        interface IBufferVector<'a> with
            member x.Buffer = buffer
            member x.Origin = origin
            member x.Delta = delta
            member x.Count = count


    type RuntimeBufferRange<'a when 'a : unmanaged>(buffer : IBackendBuffer, offset : nativeint, count : int) =

        member x.Buffer = buffer
        member x.Offset = offset
        member x.Count = count

        interface IBufferVector<'a> with
            member x.Buffer = buffer
            member x.Origin = 0
            member x.Delta = 1
            member x.Count = count

        interface IBufferRange<'a> with
            member x.Buffer = buffer
            member x.Offset = offset
            member x.Count = count        
            member x.Size = nativeint count * nativeint sizeof<'a>

    type RuntimeBuffer<'a when 'a : unmanaged>(buffer : IBackendBuffer, count : int) =
        inherit RuntimeBufferRange<'a>(buffer, 0n, count)
        interface IBuffer<'a> with
            member x.Dispose() = buffer.Runtime.DeleteBuffer buffer

    let nsa<'a when 'a : unmanaged> = nativeint sizeof<'a>
        
[<AbstractClass; Sealed; Extension>]
type IBufferRuntimeExtensions private() =

    static let check (b : IBackendBuffer) (off : nativeint) (size : nativeint) =
        if off < 0n then failwithf "[Buffer] invalid offset %A" off
        if size < 0n then failwithf "[Buffer] invalid size %A" size
        let e = off + size
        if e > b.SizeInBytes then failwithf "[Buffer] range out of bounds { offset = %A; size = %A } (size: %A)" off size b.SizeInBytes

    static let checkRange (b : IBufferRange<'a>) (min : int) (max : int) =
        if min < 0 then failwithf "[BufferRange] invalid offset %A" min
        if max < min then failwithf "[BufferRange] invalid range [%A, %A]" min max
        if max >= b.Count then failwithf "[BufferRange] range out of bounds { min = %A; max = %A } (count: %A)" min max b.Count
        
    [<Extension>]
    static member SubVector(this : IBufferVector<'a>, offset : int, delta : int, count : int) =
        if offset < 0 then failwithf "[Buffer] invalid negative offset: %A" offset
        if offset > this.Count then failwithf "[Buffer] offset out of bounds: %A (count: %A)" offset this.Count

        if count < 0 then failwithf "[Buffer] invalid negative count: %A" offset
        if offset + count > this.Count then failwithf "[Buffer] range out of bounds: (%A, %A) (count: %A)" offset count this.Count

        let origin = this.Origin + offset * this.Delta
        let delta = this.Delta * delta

        let sa = nativeint sizeof<'a>
        let firstByte = sa * nativeint origin
        let lastByte = sa * (nativeint origin + nativeint delta * nativeint (count - 1))
        if firstByte < 0n || firstByte >= this.Buffer.SizeInBytes then failwithf "[Buffer] range out of bounds"
        if lastByte < 0n || lastByte >= this.Buffer.SizeInBytes then failwithf "[Buffer] range out of bounds"

        RuntimeBufferVector<'a>(
            this.Buffer,
            origin,
            delta,
            count
        ) :> IBufferVector<_>
        
    [<Extension>]
    static member SubVector(this : IBufferVector<'a>, offset : int, count : int) =
        IBufferRuntimeExtensions.SubVector(this, offset, 1, count)
           
    [<Extension>]
    static member Skip(this : IBufferVector<'a>, n : int) =
        let n = min (this.Count - 1) n
        IBufferRuntimeExtensions.SubVector(this, n, 1, max 0 (this.Count - n))

    [<Extension>]
    static member Take(this : IBufferVector<'a>, n : int) =
        IBufferRuntimeExtensions.SubVector(this, 0, 1, n)

    [<Extension>]
    static member Strided(this : IBufferVector<'a>, d : int) =
        let n = 1 + (this.Count - 1) / d
        IBufferRuntimeExtensions.SubVector(this, 0, d, n)

    [<Extension>]
    static member Upload(this : IBackendBuffer, offset : nativeint, data : nativeint, size : nativeint) =
        check this offset size
        this.Runtime.Copy(data, this, offset, size)
        
    [<Extension>]
    static member Download(this : IBackendBuffer, offset : nativeint, data : nativeint, size : nativeint) =
        check this offset size
        this.Runtime.Copy(this, offset, data, size)
 
    [<Extension>]
    static member CopyTo(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) =
        check src srcOffset size
        check dst dstOffset size
        let runtime = src.Runtime
        runtime.Copy(src, srcOffset, dst, dstOffset, size)

  
    [<Extension>]
    static member Upload(this : IBufferRange, data : nativeint, size : nativeint) =
        IBufferRuntimeExtensions.Upload(this.Buffer, this.Offset, data, min this.Size size)
    
    [<Extension>]
    static member Download(this : IBufferRange, data : nativeint, size : nativeint) =
        IBufferRuntimeExtensions.Download(this.Buffer, this.Offset, data, min this.Size size)


    [<Extension>]
    static member Upload(x : IBufferRange<'a>, src : 'a[], srcIndex : int, dstIndex : int, count : int) =
        let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
        try
            let ptr = gc.AddrOfPinnedObject()
            IBufferRuntimeExtensions.Upload(x.Buffer, nativeint dstIndex * nsa<'a>, ptr + nsa<'a> * nativeint srcIndex, nsa<'a> * nativeint count)
        finally
            gc.Free()
            
    [<Extension>]
    static member Upload(x : IBufferRange<'a>, src : 'a[], dstIndex : int, count : int) = IBufferRuntimeExtensions.Upload(x, src, 0, dstIndex, count)

    [<Extension>]
    static member Upload(x : IBufferRange<'a>, src : 'a[], count : int) = IBufferRuntimeExtensions.Upload(x, src, 0, 0, count)

    [<Extension>]
    static member Upload(x : IBufferRange<'a>, src : 'a[]) = IBufferRuntimeExtensions.Upload(x, src, 0, 0, src.Length)


    [<Extension>]
    static member Download(x : IBufferRange<'a>, srcIndex : int, dst : 'a[], dstIndex : int, count : int) =
        let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
        try
            let ptr = gc.AddrOfPinnedObject()
            IBufferRuntimeExtensions.Download(x.Buffer, x.Offset + nativeint srcIndex * nsa<'a>, ptr + nsa<'a> * nativeint dstIndex, nsa<'a> * nativeint count)
        finally
            gc.Free()
            
    [<Extension>]
    static member Download(x : IBufferRange<'a>, srcIndex : int, dst : 'a[], count : int) = IBufferRuntimeExtensions.Download(x, srcIndex, dst, 0, count)

    [<Extension>]
    static member Download(x : IBufferRange<'a>, dst : 'a[], count : int) = IBufferRuntimeExtensions.Download(x, 0, dst, 0, count)

    [<Extension>]
    static member Download(x : IBufferRange<'a>, dst : 'a[]) = IBufferRuntimeExtensions.Download(x, 0, dst, 0, dst.Length)

    [<Extension>]
    static member Download(x : IBufferRange<'a>) = 
        let dst = Array.zeroCreate x.Count 
        IBufferRuntimeExtensions.Download(x, 0, dst, 0, dst.Length)
        dst

    [<Extension>]
    static member CopyTo(src : IBufferRange, dst : IBufferRange) =
        if src.Size <> dst.Size then failwithf "[Buffer] mismatching size in copy: { src = %A; dst = %A }" src.Size dst.Size
        IBufferRuntimeExtensions.CopyTo(src.Buffer, src.Offset, dst.Buffer, dst.Offset, src.Size)


    [<Extension>]
    static member GetSlice(x : IBufferRange<'a>, min : Option<int>, max : Option<int>) =
        let min = defaultArg min 0
        let max = defaultArg max (x.Count - 1) 
        checkRange x min max
        RuntimeBufferRange<'a>(x.Buffer, x.Offset + nativeint min * nsa<'a>, 1 + max - min) :> IBufferRange<_>
        
    [<Extension>]
    static member SetSlice(x : IBufferRange<'a>, min : Option<int>, max : Option<int>, data : 'a[]) =
        let slice = IBufferRuntimeExtensions.GetSlice(x, min, max)
        IBufferRuntimeExtensions.Upload(slice, data, Fun.Min(data.Length, slice.Count))
        
    [<Extension>]
    static member SetSlice(x : IBufferRange<'a>, min : Option<int>, max : Option<int>, other : IBufferRange<'a>) =
        let slice = IBufferRuntimeExtensions.GetSlice(x, min, max)
        IBufferRuntimeExtensions.CopyTo(other, slice)
        
    [<Extension>]
    static member SetSlice(x : IBufferRange<'a>, min : Option<int>, max : Option<int>, value : 'a) =
        let slice = IBufferRuntimeExtensions.GetSlice(x, min, max)
        IBufferRuntimeExtensions.Upload(slice, Array.create slice.Count value)

    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(this : IBufferRuntime, count : int) =
        let buffer = this.CreateBuffer(nsa<'a> * nativeint count, BufferUsage.Default)
        new RuntimeBuffer<'a>(buffer, count) :> IBuffer<'a>
        
    [<Extension>]
    static member CreateBuffer<'a when 'a : unmanaged>(this : IBufferRuntime, data : 'a[]) =
        let buffer = this.CreateBuffer(nsa<'a> * nativeint data.Length, BufferUsage.Default)
        let res = new RuntimeBuffer<'a>(buffer, data.Length) :> IBuffer<'a>
        IBufferRuntimeExtensions.Upload(res, data)
        res

    [<Extension>] 
    static member Coerce<'a when 'a : unmanaged>(this : IBackendBuffer) =
        new RuntimeBuffer<'a>(this, int (this.SizeInBytes / nativeint sizeof<'a>)) :> IBuffer<'a>

    /// <summary>
    /// Creates a buffer in GPU memory with default BufferUsage flags, enabling all usages as well as read and write.
    /// </summary>
    [<Extension>]
    static member CreateBuffer(this : IBufferRuntime, size : nativeint) =
        this.CreateBuffer(size, BufferUsage.Default)

    /// <summary>
    /// Prepares a buffer for GPU usage with all BufferUsage flags, but only write permissions.
    /// If the buffer is an IBackendBuffer the operation performs NOP
    /// </summary>
    [<Extension>]
    static member PrepareBuffer(this : IBufferRuntime, buffer : IBuffer) =
        this.PrepareBuffer(buffer, BufferUsage.Default &&& ~~~BufferUsage.Read)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BufferRange =
    let inline upload (data : 'a[]) (range : IBufferRange<'a>) = range.Upload(data)
    let inline download (range : IBufferRange<'a>) = range.Download()
    let inline copy (src : IBufferRange) (dst : IBufferRange) = src.CopyTo dst
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Buffer =
    let inline create<'a when 'a : unmanaged> (runtime : IBufferRuntime) (count : int) = runtime.CreateBuffer<'a>(count)
    let inline ofArray<'a when 'a : unmanaged> (runtime : IBufferRuntime) (data : 'a[]) = runtime.CreateBuffer<'a>(data)
    let inline delete (b : IBuffer<'a>) =  b.Dispose()
