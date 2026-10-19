namespace Aardvark.Rendering.Tests.Buffer

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module BufferDownloadAsync =

    module private Cases =

        let private timeout = TimeSpan.FromSeconds 5.0

        let private collect() =
            GC.Collect(2, GCCollectionMode.Forced, true, true)
            GC.WaitForPendingFinalizers()
            GC.Collect(2, GCCollectionMode.Forced, true, true)

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private movable<'T> count =
            let holes = Array.init 64 (fun _ -> Array.zeroCreate<byte> 1024)
            let result = Array.zeroCreate<'T> count
            GC.KeepAlive holes
            result

        let private pinned (data : Array) byteOffset action =
            let pin = GCHandle.Alloc(data, GCHandleType.Pinned)
            try action (pin.AddrOfPinnedObject() + nativeint byteOffset)
            finally pin.Free()

        let private address (data : Array) byteOffset = pinned data byteOffset id

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private alive (reference : WeakReference<Array>) =
            let mutable target = Unchecked.defaultof<Array>
            reference.TryGetTarget(&target)

        type private Backend(runtime : IBufferRuntime, size : uint64) as this =
            let mutable name = null
            interface IBackendBuffer with
                member _.Runtime = runtime
                member _.Handle = 0UL
                member _.Buffer = this :> IBackendBuffer
                member _.Offset = 0UL
                member _.SizeInBytes = size
                member _.Name with get() = name and set value = name <- value
                member _.Dispose() = ()

        type private DeferredRuntime(bytes : byte[]) as this =
            let mutable submissions = 0
            let mutable completions = 0
            let mutable offset = 0UL
            let mutable destination = 0n
            let mutable size = 0UL
            let mutable expected : (WeakReference<Array> * int) option = None

            member val CopyAtStartup = false with get, set
            member val StartupError : exn option = None with get, set
            member val CompletionError : exn option = None with get, set
            member val BeforeCompletion : unit -> unit = ignore with get, set
            member _.Submissions = submissions
            member _.Completions = completions
            member _.Offset = offset
            member _.Destination = destination
            member _.Size = size
            member _.Expect(data : Array, byteOffset) = expected <- Some (WeakReference<Array>(data), byteOffset)
            member _.Buffer = new Backend(this :> IBufferRuntime, uint64 bytes.Length) :> IBackendBuffer

            member private _.Copy(startup : bool) =
                match expected with
                | Some (reference, byteOffset) ->
                    let mutable target = Unchecked.defaultof<Array>
                    Expect.isTrue (reference.TryGetTarget(&target)) "Destination was collected before completion"
                    pinned target byteOffset (fun current ->
                        // Never dereference the submitted address unless it still names this array.
                        Expect.equal current destination "Destination moved before completion; refusing a stale-pointer write"
                        Marshal.Copy(bytes, int offset, current, int size)
                    )
                | None when startup ->
                    // Both original and fixed implementations hold the array pin during submission.
                    Marshal.Copy(bytes, int offset, destination, int size)
                | None -> failtest "Deferred writes require an independently checked destination"

            interface IBufferRuntime with
                member _.PrepareBuffer(data, _, _) = data :?> IBackendBuffer
                member _.CreateBuffer(size, _, _) = new Backend(this :> IBufferRuntime, size) :> IBackendBuffer
                member _.Upload(_, _, _, _, _) = raise <| NotSupportedException()
                member _.Download(_, _, _, _) = raise <| NotSupportedException()
                member _.Copy(_, _, _, _, _, _) = raise <| NotSupportedException()
                member _.DownloadAsync(buffer, srcOffset, dst, count) =
                    ResourceValidation.Buffers.validateRange srcOffset count buffer
                    submissions <- submissions + 1
                    offset <- srcOffset
                    destination <- dst
                    size <- count
                    match this.StartupError with Some error -> raise error | None -> ()
                    if this.CopyAtStartup then this.Copy true
                    fun () ->
                        Interlocked.Increment(&completions) |> ignore
                        this.BeforeCompletion()
                        match this.CompletionError with Some error -> raise error | None -> ()
                        if not this.CopyAtStartup then this.Copy false

        let private runtimeFor (data : 'T[]) =
            let bytes = Array.zeroCreate<byte> (data.Length * typeof<'T>.CLRSize)
            data |> NativeInt.pin (fun source -> Marshal.Copy(source, bytes, 0, bytes.Length))
            DeferredRuntime bytes

        let private range<'T when 'T : unmanaged> (runtime : DeferredRuntime) origin count =
            runtime.Buffer.Coerce<'T>().Elements(origin, count)

        let private cleanup wait = try wait() with _ -> ()

        let private sameError context expected action =
            let mutable failure = None
            try action()
            with error -> failure <- Some error
            Expect.isSome failure (context + ": expected failure")
            Expect.isTrue (obj.ReferenceEquals(failure.Value, expected)) (context + ": original exception identity")

        type private IMatrix =
            abstract Run<'T when 'T : unmanaged and 'T : equality> : 'T[] -> unit

        let private forTypes (matrix : IMatrix) =
            matrix.Run [|1uy; 17uy; 251uy|]
            matrix.Run [|Int32.MinValue; 17; Int32.MaxValue|]
            matrix.Run [|V3f.IOO; V3f.OIO; V3f.OOI|]
            matrix.Run [|C4b.Red; C4b.Green; C4b.Blue|]

        let private data (values : 'T[]) count = Array.init count (fun index -> values.[index % values.Length])

        let deferredSubranges _ =
            let mutable movedControls = 0
            forTypes { new IMatrix with
                member _.Run<'T when 'T : unmanaged and 'T : equality>(values : 'T[]) =
                    for count in [1; 17; 1024] do
                        let source = data values (count + 12)
                        let runtime = runtimeFor source
                        let view = range<'T> runtime 3 (count + 4)
                        let control = movable<'T> (count + 9)
                        let beforeControl = address control 0
                        let destination = movable<'T> (count + 9)
                        let context = sprintf "type=%A, count=%d" typeof<'T> count
                        runtime.Expect(destination, 4 * sizeof<'T>)
                        let wait = view.DownloadAsync(destination, 2, 4, count)
                        try
                            Expect.equal runtime.Submissions 1 (context + ": submission is immediate")
                            Expect.equal runtime.Completions 0 (context + ": copy is deferred")
                            Expect.equal runtime.Offset (5UL * uint64 sizeof<'T>) (context + ": composed source offset")
                            Expect.equal runtime.Size (uint64 (count * sizeof<'T>)) (context + ": transfer size")
                            collect()
                            if address control 0 <> beforeControl then movedControls <- movedControls + 1
                            Expect.equal (address destination (4 * sizeof<'T>)) runtime.Destination (context + ": destination stays pinned")
                            wait()
                            let expected = Array.zeroCreate<'T> destination.Length
                            Array.Copy(source, 5, expected, 4, count)
                            Expect.equal destination expected (context + ": subrange payload and guards")
                        finally cleanup wait
                        GC.KeepAlive control
            }
            Expect.isGreaterThan movedControls 0 "Compacting collections moved at least one unpinned control"

        let convenienceArrays _ =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : unmanaged and 'T : equality>(values : 'T[]) =
                    for count in [1; 17; 1024] do
                        let source = data values (count + 7)
                        for explicitCount in [true; false] do
                            let runtime = runtimeFor source
                            let view = range<'T> runtime 2 count
                            let destination = movable<'T> (count + 3)
                            runtime.Expect(destination, 0)
                            let wait = if explicitCount then view.DownloadAsync(destination, count) else view.DownloadAsync(destination)
                            try
                                collect()
                                Expect.equal (address destination 0) runtime.Destination "Convenience overload retains the caller array pin"
                                wait()
                                Expect.equal destination.[0 .. count - 1] source.[2 .. count + 1] "Convenience payload"
                                Expect.equal destination.[count ..] (Array.zeroCreate<'T> 3) "Convenience tail guard"
                            finally cleanup wait
                        let runtime = runtimeFor source
                        runtime.CopyAtStartup <- true
                        let view = range<'T> runtime 2 count
                        let wait = view.DownloadAsync()
                        try
                            collect()
                            let result = wait()
                            // No original-source deferred write occurs: compare only after obtaining the array.
                            Expect.equal (address result 0) runtime.Destination "Allocated destination stays pinned through completion"
                            Expect.equal result source.[2 .. count + 1] "Allocated convenience payload"
                            Expect.isTrue (obj.ReferenceEquals(wait(), result)) "Allocated completion retains result identity"
                        finally cleanup (fun () -> wait() |> ignore)
            }

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private pending (runtime : DeferredRuntime) =
            let destination = movable<int> 33
            runtime.Expect(destination, 0)
            let reference = WeakReference<Array>(destination)
            let wait = (range<int> runtime 0 33).DownloadAsync(destination)
            reference, wait

        let destinationOwnership _ =
            let runtime = runtimeFor (Array.init 33 id)
            let reference, wait = pending runtime
            try
                collect()
                Expect.isTrue (alive reference) "Pending readback roots its destination without a caller reference"
                wait()
                collect()
                Expect.isFalse (alive reference) "Successful completion releases the destination even while completion is retained"
                wait()
                Expect.equal runtime.Completions 1 "Repeated completion cannot revisit released storage"
                GC.KeepAlive wait
            finally cleanup wait

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private failedStartup (runtime : DeferredRuntime) expected =
            let destination = movable<int> 33
            runtime.Expect(destination, 0)
            let reference = WeakReference<Array>(destination)
            sameError "startup" expected (fun () -> (range<int> runtime 0 33).DownloadAsync(destination) |> ignore)
            reference

        let failureCleanup _ =
            let startup = InvalidOperationException "startup"
            let runtime = runtimeFor (Array.init 33 id)
            runtime.StartupError <- Some startup
            let reference = failedStartup runtime startup
            collect()
            Expect.isFalse (alive reference) "Startup failure frees the pin"
            Expect.equal runtime.Submissions 1 "Startup failure is submitted once"
            Expect.equal runtime.Completions 0 "Startup failure returns no completion"
            let expected = InvalidOperationException "completion"
            let runtime = runtimeFor (Array.init 33 id)
            runtime.CompletionError <- Some expected
            let reference, wait = pending runtime
            for _ = 1 to 4 do sameError "completion" expected wait
            collect()
            Expect.isFalse (alive reference) "Failed completion frees the pin while caching the original exception"
            Expect.equal runtime.Completions 1 "Failed completion is not retried after unpinning"
            GC.KeepAlive wait

        let concurrentCompletion _ =
            for failing in [false; true] do
                let runtime = runtimeFor (Array.init 33 id)
                let destination = movable<int> 33
                runtime.Expect(destination, 0)
                let expected = InvalidOperationException "concurrent completion"
                if failing then runtime.CompletionError <- Some expected
                use entered = new ManualResetEventSlim(false)
                use release = new ManualResetEventSlim(false)
                runtime.BeforeCompletion <- fun () -> entered.Set(); Expect.isTrue (release.Wait(timeout)) "Completion gate timed out"
                let wait = (range<int> runtime 0 33).DownloadAsync(destination)
                let errors : exn option[] = Array.create 4 None
                let workers = Array.init 4 (fun index ->
                    Thread(ThreadStart(fun () -> try wait() with error -> errors.[index] <- Some error), IsBackground = true)
                )
                try
                    for worker in workers do worker.Start()
                    Expect.isTrue (entered.Wait(timeout)) "A completion caller reached the backend"
                    collect()
                    Expect.equal (address destination 0) runtime.Destination "Completion keeps storage pinned while other callers wait"
                    release.Set()
                    for worker in workers do Expect.isTrue (worker.Join(timeout)) "Completion worker did not finish"
                    Expect.equal runtime.Completions 1 "Concurrent callers execute one backend completion"
                    for error in errors do
                        if failing then
                            Expect.isSome error "Every caller receives the completion failure"
                            Expect.isTrue (obj.ReferenceEquals(error.Value, expected)) "Concurrent exception identity"
                        else Expect.isNone error "Successful completion caller"
                    if not failing then Expect.equal destination (Array.init 33 id) "Concurrent completion payload"
                finally
                    release.Set()
                    for worker in workers do
                        if (worker.ThreadState &&& ThreadState.Unstarted) = enum<ThreadState> 0 then
                            Expect.isTrue (worker.Join(timeout)) "Completion worker cleanup"
                    cleanup wait

        let zeroAndBounds _ =
            let runtime = runtimeFor (Array.zeroCreate<int> 17)
            let view = range<int> runtime 3 7
            let destination = Array.zeroCreate<int> 9
            for wait in [view.DownloadAsync(destination, view.Count, destination.Length, 0)
                         view.DownloadAsync(destination, 0)] do
                wait(); wait()
            let empty = range<int> runtime 17 0
            Expect.equal (empty.DownloadAsync()()) [||] "Allocated zero readback"
            for srcIndex, dstIndex, count in [-1, 0, 1; 0, -1, 1; 0, 0, -1; 7, 0, 1; 0, 9, 1; Int32.MaxValue, 0, 1] do
                Expect.throwsT<ArgumentException> (fun () -> view.DownloadAsync(destination, srcIndex, dstIndex, count) |> ignore) "Invalid range rejected before submission"
            Expect.equal runtime.Submissions 0 "Zero and invalid readbacks do not reach the runtime"

        let private vulkanReadbacks allocated (runtime : IRuntime) =
            forTypes { new IMatrix with
                member _.Run<'T when 'T : unmanaged and 'T : equality>(values : 'T[]) =
                    for storage in [BufferStorage.Host; BufferStorage.Device] do
                        for count in [17; 4096] do
                            let source = data values (count + 9)
                            use buffer = runtime.CreateBuffer<'T>(source.Length, storage = storage)
                            buffer.Upload source
                            let view = buffer.[3 .. count + 2]
                            let context = sprintf "type=%A, storage=%A, count=%d, allocated=%b" typeof<'T> storage count allocated
                            if allocated then
                                let wait = view.DownloadAsync()
                                try
                                    collect()
                                    let result = wait()
                                    Expect.equal result source.[3 .. count + 2] (context + ": GPU payload")
                                    Expect.isTrue (obj.ReferenceEquals(wait(), result)) (context + ": repeat result identity")
                                finally cleanup (fun () -> wait() |> ignore)
                            else
                                let destination = movable<'T> (count + 5)
                                let wait = view.DownloadAsync(destination, 1, 2, count - 2)
                                try
                                    collect()
                                    wait()
                                    let expected = Array.zeroCreate<'T> destination.Length
                                    Array.Copy(source, 4, expected, 2, count - 2)
                                    Expect.equal destination expected (context + ": GPU subrange and guards")
                                    wait()
                                finally cleanup wait
            }

        let vulkanProvided runtime = vulkanReadbacks false runtime
        let vulkanAllocated runtime = vulkanReadbacks true runtime

    let tests (target : TestTarget) =
        [
            if target = TestTarget.Cpu then
                "Deferred subranges survive collection",        Cases.deferredSubranges
                "Convenience arrays survive collection",        Cases.convenienceArrays
                "Pending destination rooting and release",      Cases.destinationOwnership
                "Failures preserve errors and release storage", Cases.failureCleanup
                "Concurrent completion executes once",          Cases.concurrentCompletion
                "Zero and invalid ranges do not submit",        Cases.zeroAndBounds

            if target = TestTarget.Vulkan then
                "Provided arrays after collection",  Cases.vulkanProvided
                "Allocated arrays after collection", Cases.vulkanAllocated
        ]
        |> prepareCases "DownloadAsync" target
