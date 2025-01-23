namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type IManagedBufferWriter =
    inherit IAdaptiveObject
    abstract member Write : AdaptiveToken -> unit

type IManagedBuffer =
    inherit IAdaptiveBuffer

    /// The element type of the buffer.
    abstract member ElementType : Type

    /// <summary>
    /// Sets the given offset range to the given data. The buffer is resized if necessary.
    /// The data is repeated if not enough are provided for the specified range.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="sizeInBytes">The size (in bytes) of the provided data.</param>
    /// <param name="range">The range (i.e. min and max offsets) in the buffer to write to.</param>
    abstract member Set : data: nativeint * sizeInBytes: nativeint * range: Range1l -> unit

    /// <summary>
    /// Adds a writer that adaptively writes the values in the buffer view to the buffer. The buffer is resized if necessary.
    /// The values are automatically converted if they are not of the buffer element type and there is a known conversion.
    /// </summary>
    /// <param name="view">The buffer view to write.</param>
    /// <param name="range">The range (i.e. min and max offsets) in the buffer to write to.</param>
    abstract member Add : view: BufferView * range: Range1l -> IDisposable

    /// <summary>
    /// Adds a writer that adaptively writes the given value to the buffer. The buffer is resized if necessary.
    /// The value is automatically converted if it is not of the buffer element type and there is a known conversion.
    /// </summary>
    /// <param name="value">The adaptive value to write.</param>
    /// <param name="index">The index in the buffer to write to.</param>
    abstract member Add : value: IAdaptiveValue * index: int64 -> IDisposable

type IManagedBuffer<'T when 'T : unmanaged> =
    inherit IManagedBuffer

[<Extension; Sealed>]
type ManagedBufferExtensions private() =

    /// <summary>
    /// Adds a writer that adaptively writes the given value to the buffer. The buffer is resized if necessary.
    /// The value is automatically converted if it is not of the buffer element type and there is a known conversion.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="value">The adaptive value to write.</param>
    /// <param name="index">The index in the buffer to write to.</param>
    [<Extension>]
    static member inline Add(this : IManagedBuffer, value : IAdaptiveValue, index : int) =
        this.Add(value, int64 index)

    /// <summary>
    /// Sets the given offset range to the given data. The buffer is resized if necessary.
    /// The data are repeated if not enough are provided for the specified range.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="range">The range (i.e. min and max offsets) in the buffer to write to.</param>
    [<Extension>]
    static member Set(this : IManagedBuffer, data : byte[], range : Range1l) =
        data |> NativePtr.pinArr (fun src ->
            this.Set(src.Address, nativeint data.Length, range)
        )

    /// <summary>
    /// Sets the range defined by the index to the given value. The buffer is resized if necessary.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="index">The index in the buffer to write to.</param>
    [<Extension>]
    static member Set<'T when 'T : unmanaged>(this : IManagedBuffer, value : 'T, index : int64) =
        value |> NativePtr.pin (fun src ->
            this.Set(src.Address, nativeint sizeof<'T>, Range1l(index, index))
        )

    /// <summary>
    /// Sets the range defined by the index to the given value. The buffer is resized if necessary.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="index">The index in the buffer to write to.</param>
    [<Extension>]
    static member inline Set<'T when 'T : unmanaged>(this : IManagedBuffer, value : 'T, index : int) =
        this.Set(value, int64 index)

    /// <summary>
    /// Sets the given offset range to the given values. The buffer is resized if necessary.
    /// The data are repeated if not enough are provided for the specified range.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="values">The values to write.</param>
    /// <param name="range">The range (i.e. min and max offsets) in the buffer to write to.</param>
    [<Extension>]
    static member Set<'T when 'T : unmanaged>(this : IManagedBuffer, values : 'T[], range : Range1l) =
        values |> NativePtr.pinArr (fun src ->
            this.Set(src.Address, nativeint values.Length * nativeint sizeof<'T>, range)
        )

    /// <summary>
    /// Sets the given offset range to the given values. The buffer is resized if necessary.
    /// The data are repeated if not enough are provided for the specified range.
    /// </summary>
    /// <param name="this">The buffer to write to.</param>
    /// <param name="values">The values to write.</param>
    /// <param name="range">The range (i.e. min and max offsets) in the buffer to write to.</param>
    [<Extension>]
    static member Set(this : IManagedBuffer, values : Array, range : Range1l) =
        let elementSize = values.GetType().GetElementType().GetCLRSize()
        let sizeInBytes = nativeint values.Length * nativeint elementSize

        values |> NativeInt.pin (fun src ->
            this.Set(src, sizeInBytes, range)
        )


module internal ManagedBufferImplementation =

    type ManagedBuffer<'T when 'T : unmanaged>(runtime : IBufferRuntime, usage : BufferUsage, storage : BufferStorage) =
        inherit AdaptiveBuffer(runtime, 0n, usage, storage)

        static let elementSize = sizeof<'T> |> nativeint

        let writers = Dict<obj, AbstractWriter>()
        let pending = LockedSet<AbstractWriter>()

        member inline private x.Allocate(range : Range1l) =
            let min = nativeint (range.Max + 1L) * elementSize
            if x.Size < min then
                x.Resize(Fun.NextPowerOfTwo(int64 min) |> nativeint)

        member private x.AddRange(writer : AbstractWriter, input : obj, range : Range1l) =
            transact (fun _ ->
                if writer.AddRange range then
                    // Resize the buffer if necessary
                    x.Allocate range

                    // If the writer already existed (same input aval or buffer) and a new range was added,
                    // we need to mark it as outdated.
                    writer.MarkOutdated()
                    pending.Add writer |> ignore

                // If the writer was just created and no resize was necessary,
                // the buffer is still up-to-date -> mark explicitly
                x.MarkOutdated()
            )

            { new IDisposable with
                member x.Dispose() =
                    lock writers (fun _ ->
                        if writer.RemoveRange range then
                            writers.Remove input |> ignore
                            pending.Remove writer |> ignore
                            writer.Dispose()
                    )
            }

        member x.Set(data : nativeint, sizeInBytes : nativeint, range : Range1l) =
            let count = range.Size + 1L

            if count > 0L then
                x.Allocate range

                let mutable remaining = nativeint count * elementSize
                let mutable offset = nativeint range.Min * elementSize

                while remaining >= sizeInBytes do
                    x.Write(data, offset, sizeInBytes)
                    offset <- offset + sizeInBytes
                    remaining <- remaining - sizeInBytes

                if remaining > 0n then
                    x.Write(data, offset, remaining)

        member x.Add(view : BufferView, range : Range1l) =
            let count = range.Size + 1L

            if count <= 0L then
                Disposable.empty
            else
                if view.Buffer.IsConstant then
                    let data = BufferView.download 0 (int count) view
                    let converted : 'T[] = data |> PrimitiveValueConverter.convertArray view.ElementType |> AVal.force
                    x.Set(converted, range)
                    Disposable.empty
                else
                    lock writers (fun _ ->
                        let writer =
                            writers.GetOrCreate(view, fun _ ->
                                let data = BufferView.download 0 (int count) view
                                let converted : aval<'T[]> = data |> PrimitiveValueConverter.convertArray view.ElementType
                                new ArrayWriter<'T>(x, converted)
                            )

                        x.AddRange(writer, view, range)
                    )

        member x.Add(value : IAdaptiveValue, index : int64) =
            if value.IsConstant then
                let converted : 'T = value |> PrimitiveValueConverter.convertValue |> AVal.force
                x.Set(converted, index)
                Disposable.empty
            else
                lock writers (fun _ ->
                    let writer =
                        writers.GetOrCreate(value, fun _ ->
                            let converted : aval<'T> = value |> PrimitiveValueConverter.convertValue
                            new SingleWriter<'T>(x, converted)
                        )

                    let range = Range1l(int64 index, int64 index)
                    x.AddRange(writer, value, range)
                )

        override x.Destroy() =
            writers.Clear()
            pending.Clear()
            base.Destroy()

        override x.Compute(t, rt) =
            for w in pending.GetAndClear() do
                w.Write(t)

            base.Compute(t, rt)

        override x.InputChangedObject(_, object) =
            match object with
            | :? AbstractWriter as writer -> pending.Add writer |> ignore
            | _ -> ()

        interface IManagedBuffer<'T> with
            member x.ElementType = typeof<'T>
            member x.Set(data, sizeInBytes, range) = x.Set(data, sizeInBytes, range)
            member x.Add(view : BufferView, range) = x.Add(view, range)
            member x.Add(value : IAdaptiveValue, index) = x.Add(value, index)

    and [<AbstractClass>] private AbstractWriter(input : IAdaptiveValue, elementSize : nativeint) =
        inherit AdaptiveObject()

        do input.Acquire()
        let regions = ReferenceCountingSet<Range1l>()

        abstract member Write : AdaptiveToken * nativeint -> unit

        // Adds the given range as target region and returns if it was newly added.
        member x.AddRange(range : Range1l) : bool =
            lock x (fun () ->
                regions.Add range
            )

        /// Removes the given range as target regions and returns true if all ranges are removed.
        member x.RemoveRange(range : Range1l) : bool =
            lock x (fun () ->
                regions.Remove range |> ignore
                regions.Count = 0
            )

        member x.Write(token : AdaptiveToken) =
            x.EvaluateIfNeeded token () (fun token ->
                for region in regions do
                    let offset = nativeint region.Min * elementSize
                    x.Write(token, offset)
            )

        member x.Dispose() =
            input.Release()
            // TODO: Check if these comments are still valid
            // in case the data Mod is a PrimitiveValueConverter, it would be garbage collected and removal of the output is not essential
            // in case the data Mod is directly from the application (no converter), removing the Writer from its Output is essential
            input.Outputs.Remove(x) |> ignore
            x.Outputs.Clear()

        interface IDisposable with
            member x.Dispose() = x.Dispose()


    and private SingleWriter<'T when 'T : unmanaged>(buffer : ManagedBuffer<'T>, data : aval<'T>) =
        inherit AbstractWriter(data, nativeint sizeof<'T>)

        override x.Write(token, offset) =
            let value = data.GetValue(token)
            buffer.Write(value, offset)

    and private ArrayWriter<'T when 'T : unmanaged>(buffer : ManagedBuffer<'T>, data : aval<'T[]>) =
        inherit AbstractWriter(data, nativeint sizeof<'T>)

        override x.Write(token, offset) =
            let value = data.GetValue(token)
            buffer.Write(value, offset)


module ManagedBuffer =
    open System.Reflection
    open ManagedBufferImplementation

    let private ctorCache = Dict<Type, ConstructorInfo>()

    let private ctor (t : Type) =
        lock ctorCache (fun () ->
            ctorCache.GetOrCreate(t, fun t ->
                let tb = typedefof<ManagedBuffer<int>>.MakeGenericType [|t|]
                tb.GetConstructor(
                    BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.CreateInstance,
                    Type.DefaultBinder,
                    [| typeof<IBufferRuntime>; typeof<BufferUsage>; typeof<BufferStorage> |],
                    null
                )
            )
        )

    let createWithType (elementType : Type) (runtime : IBufferRuntime) (usage : BufferUsage) (storage : BufferStorage) =
        let ctor = ctor elementType
        ctor.Invoke [| runtime; usage; storage |] |> unbox<IManagedBuffer>

    let create<'T when 'T : unmanaged> (runtime : IBufferRuntime) (usage : BufferUsage) (storage : BufferStorage) =
        new ManagedBuffer<'T>(runtime, usage, storage) :> IManagedBuffer<'T>


[<AbstractClass; Sealed; Extension>]
type ManagedBufferRuntimeExtensions private() =

    /// <summary>
    /// Creates a managed buffer with the given usage.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="usage">The usage flags of the buffer. Default is BufferUsage.All.</param>
    /// <param name="storage">The type of storage that is preferred. Default is BufferStorage.Host.</param>
    [<Extension>]
    static member CreateManagedBuffer<'T when 'T : unmanaged>(
                        this : IBufferRuntime,
                        [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                        [<Optional; DefaultParameterValue(BufferStorage.Host)>] storage : BufferStorage
                    ) =
        ManagedBuffer.create<'T> this usage storage

    /// <summary>
    /// Creates a managed buffer with the given usage.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="elementType">The element type of the buffer.</param>
    /// <param name="usage">The usage flags of the buffer. Default is BufferUsage.All.</param>
    /// <param name="storage">The type of storage that is preferred. Default is BufferStorage.Host.</param>
    [<Extension>]
    static member CreateManagedBuffer(
                        this : IBufferRuntime, elementType : Type,
                        [<Optional; DefaultParameterValue(BufferUsage.All)>] usage : BufferUsage,
                        [<Optional; DefaultParameterValue(BufferStorage.Host)>] storage : BufferStorage
                    ) =
        ManagedBuffer.createWithType elementType this usage storage