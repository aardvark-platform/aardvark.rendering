namespace Aardvark.Rendering.Tests.Buffer

open System
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module BufferCreate =

    type private Call = Allocate of uint64 * BufferUsage * BufferStorage | Unexpected of string

    type private MetadataBuffer(runtime: IBufferRuntime, size: uint64) =
        let mutable name = null
        let mutable disposals = 0
        member _.Disposals = disposals
        interface IBackendBuffer with
            member _.Runtime = runtime
            member _.Handle = 17UL
            member _.Name with get() = name and set value = name <- value
            member x.Buffer = x :> IBackendBuffer
            member _.Offset = 0UL
            member _.SizeInBytes = size
            member _.Dispose() = disposals <- disposals + 1

    type private RecordingRuntime() as this =
        let calls = ResizeArray<Call>()
        let buffers = ResizeArray<MetadataBuffer>()
        let unexpected name = calls.Add(Unexpected name); failtest ("Unexpected runtime operation: " + name)
        member val Error : exn option = None with get, set
        member _.Calls = calls
        member _.Buffers = buffers
        interface IBufferRuntime with
            member _.CreateBuffer(size, usage, storage) =
                calls.Add(Allocate(size, usage, storage))
                match this.Error with
                | Some error -> raise error
                | None ->
                    // Metadata only: even extreme requests never allocate backing storage.
                    let buffer = new MetadataBuffer(this, size)
                    buffers.Add buffer
                    buffer :> IBackendBuffer
            member _.PrepareBuffer(_, _, _) = unexpected "prepare"
            member _.Upload(_, _, _, _, _) = unexpected "upload"
            member _.Download(_, _, _, _) = unexpected "download"
            member _.DownloadAsync(_, _, _, _) = unexpected "async download"
            member _.Copy(_, _, _, _, _, _) = unexpected "copy"
        interface IDisposable with
            member _.Dispose() =
                // Also release metadata objects reached only by an original-source failure.
                for buffer in buffers do
                    if buffer.Disposals = 0 then (buffer :> IDisposable).Dispose()

    type private Arguments = Defaults | Usage of BufferUsage | Storage of BufferStorage | Both of BufferUsage * BufferStorage

    let private arguments =
        [ Defaults
          Both(BufferUsage.All, BufferStorage.Device)
          Usage BufferUsage.None
          Usage(BufferUsage.Vertex ||| BufferUsage.ReadWrite)
          Storage BufferStorage.Host
          Both(BufferUsage.Uniform ||| BufferUsage.Storage, BufferStorage.Host) ]

    let private forwarded = function
        | Defaults -> BufferUsage.All, BufferStorage.Device
        | Usage usage -> usage, BufferStorage.Device
        | Storage storage -> BufferUsage.All, storage
        | Both(usage, storage) -> usage, storage

    let private create<'T when 'T : unmanaged> (runtime: IBufferRuntime) arguments (count: int) =
        match arguments with
        | Defaults -> runtime.CreateBuffer<'T>(count)
        | Usage usage -> runtime.CreateBuffer<'T>(count, usage = usage)
        | Storage storage -> runtime.CreateBuffer<'T>(count, storage = storage)
        | Both(usage, storage) -> runtime.CreateBuffer<'T>(count, usage, storage)

    let private allocation<'T when 'T : unmanaged> arguments (count: int) =
        let usage, storage = forwarded arguments
        Allocate(uint64 count * uint64 sizeof<'T>, usage, storage)

    let private metadata<'T when 'T : unmanaged> context count (runtime: RecordingRuntime) (actual: IBuffer<'T>) =
        let raw = runtime.Buffers.[runtime.Buffers.Count - 1]
        Expect.equal actual.Count count (context + ": element count")
        Expect.equal actual.Origin 0 (context + ": origin")
        Expect.equal actual.Delta 1 (context + ": contiguous elements")
        Expect.equal actual.Offset 0UL (context + ": byte offset")
        Expect.equal actual.SizeInBytes (uint64 count * uint64 sizeof<'T>) (context + ": byte size")
        Expect.isTrue (obj.ReferenceEquals(actual.Buffer, raw)) (context + ": backend identity")
        Expect.isTrue (obj.ReferenceEquals(actual.Buffer.Runtime, runtime)) (context + ": runtime identity")
        Expect.equal raw.Disposals 0 (context + ": caller still owns the allocation")
        actual.Dispose()
        Expect.equal raw.Disposals 1 (context + ": caller disposal forwarded once")

    module private Cases =
        let private negatives<'T when 'T : unmanaged> () =
            for count in [-1; -2; -4096; Int32.MinValue] do
                for args in arguments do
                    for failingBackend in [false; true] do
                        use runtime = new RecordingRuntime()
                        if failingBackend then runtime.Error <- Some(OutOfMemoryException("backend must not be reached"))
                        let context = $"type={typeof<'T>.Name}, count={count}, args={args}, failingBackend={failingBackend}"
                        let error =
                            try
                                use buffer = create<'T> runtime args count
                                None
                            with error -> Some error
                        Expect.equal runtime.Calls.Count 0 (context + ": no runtime call before rejection")
                        Expect.equal runtime.Buffers.Count 0 (context + ": no backend allocation")
                        match error with
                        | Some (:? ArgumentOutOfRangeException as error) -> Expect.equal error.ParamName "count" (context + ": argument name")
                        | _ -> failtest (context + ": expected ArgumentOutOfRangeException")

        let negativeCounts () =
            negatives<byte>()
            negatives<int16>()
            negatives<int32>()
            negatives<int64>()

        let private valid<'T when 'T : unmanaged> () =
            for count in [0; 1; 3; 17; 4096; Int32.MaxValue] do
                for args in arguments do
                    use runtime = new RecordingRuntime()
                    let context = $"type={typeof<'T>.Name}, count={count}, args={args}"
                    let buffer = create<'T> runtime args count
                    Expect.sequenceEqual runtime.Calls [allocation<'T> args count] (context + ": exact allocation and option forwarding")
                    Expect.equal runtime.Buffers.Count 1 (context + ": one allocation, including zero count")
                    metadata context count runtime buffer

        let validCounts () =
            valid<byte>()
            valid<int16>()
            valid<int32>()
            valid<int64>()

        let private failures<'T when 'T : unmanaged> () =
            for count in [0; 1; 17; Int32.MaxValue] do
                for args in arguments do
                    for error in [OutOfMemoryException("backend allocation failure") :> exn; InvalidOperationException("backend failure") :> exn] do
                        use runtime = new RecordingRuntime()
                        runtime.Error <- Some error
                        let context = $"type={typeof<'T>.Name}, count={count}, args={args}, error={error.GetType().Name}"
                        let actual = try create<'T> runtime args count |> ignore; None with caught -> Some caught
                        Expect.isTrue (actual |> Option.exists (fun caught -> obj.ReferenceEquals(caught, error))) (context + ": original backend exception identity")
                        Expect.sequenceEqual runtime.Calls [allocation<'T> args count] (context + ": one backend attempt")
                        Expect.equal runtime.Buffers.Count 0 (context + ": no published allocation after failure")
                        runtime.Error <- None
                        let buffer = create<'T> runtime args count
                        Expect.sequenceEqual runtime.Calls [allocation<'T> args count; allocation<'T> args count] (context + ": successful retry")
                        metadata context count runtime buffer

        let backendFailures () =
            failures<byte>()
            failures<int16>()
            failures<int32>()
            failures<int64>()

    let tests (target: TestTarget) =
        [
            "Negative typed counts fail before allocation",       Cases.negativeCounts
            "Valid typed counts preserve metadata and ownership", Cases.validCounts
            "Backend allocation failures retain their identity",  Cases.backendFailures
        ]
        |> prepareCasesCpu "Create" target
