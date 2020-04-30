namespace Aardvark.Base

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices

// ISSUE: Dirty ranges use plain ints which do not cover the whole domain of nativeint.
type private AdaptiveBufferReader(b : IAdaptiveBuffer, remove : AdaptiveBufferReader -> unit) =
    inherit AdaptiveObject()

    let mutable realCapacity = 0
    let mutable dirtyCapacity = -1
    let mutable dirtyRanges = RangeSet.empty

    member x.AddDirty(r : Range1i) =
        if dirtyCapacity = realCapacity then
            Interlocked.Change(&dirtyRanges, RangeSet.insert r) |> ignore

    member x.RemoveDirty(r : Range1i) =
        if dirtyCapacity = realCapacity then
            Interlocked.Change(&dirtyRanges, RangeSet.remove r) |> ignore

    member x.Resize(cap : nativeint) =
        let cap = int cap
        if cap <> realCapacity then
            realCapacity <- cap
            dirtyRanges <- RangeSet.empty

    member x.GetDirtyRanges(token : AdaptiveToken) =
        x.EvaluateAlways token (fun token ->
            let buffer = b.GetValue token :?> INativeBuffer
            let dirtyCap = Interlocked.Exchange(&dirtyCapacity, buffer.SizeInBytes)

            if dirtyCap <> buffer.SizeInBytes then
                buffer, RangeSet.ofList [Range1i(0, buffer.SizeInBytes-1)]
            else
                let dirty = Interlocked.Exchange(&dirtyRanges, RangeSet.empty)
                buffer, dirty
        )

    member x.Dispose() =
        if Interlocked.Exchange(&realCapacity, -1) >= 0 then
            b.Outputs.Remove x |> ignore
            remove x
            dirtyRanges <- RangeSet.empty
            dirtyCapacity <- -1

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IAdaptiveBufferReader with
        member x.GetDirtyRanges(caller) = x.GetDirtyRanges(caller)

type ChangeableBuffer(sizeInBytes : nativeint) =
    inherit AVal.AbstractVal<IBuffer>()

    let rw = new ReaderWriterLockSlim()
    let mutable capacity = sizeInBytes
    let mutable storage = if sizeInBytes > 0n then Marshal.AllocHGlobal(capacity) else 0n
    let readers = HashSet<AdaptiveBufferReader>()

    let removeReader(r : AdaptiveBufferReader) =
        lock readers (fun () -> readers.Remove r |> ignore)

    let addDirty (r : Range1i) =
        let all = lock readers (fun () -> readers |> Aardvark.Base.HashSet.toArray)
        all |> Array.iter (fun reader ->
            reader.Resize capacity
            reader.AddDirty r
        )

    new () = ChangeableBuffer(0n)

    new (sizeInBytes : int) = ChangeableBuffer (nativeint sizeInBytes)

    member x.Capacity = capacity

    member x.Resize(newCapacity : nativeint) =
        let changed =
            ReaderWriterLock.write rw (fun () ->
                if newCapacity = 0n then
                    if storage <> 0n then
                        Marshal.FreeHGlobal(storage)
                        storage <- 0n
                        capacity <- 0n
                        true
                    else
                        false
                elif newCapacity <> capacity then
                    if storage = 0n then
                        capacity <- newCapacity
                        storage <- Marshal.AllocHGlobal(capacity)
                    else
                        capacity <- newCapacity
                        storage <- Marshal.ReAllocHGlobal(storage, capacity)

                    addDirty (Range1i(0, int capacity - 1))
                    true
                else
                    false
            )

        if changed then transact (fun () -> x.MarkOutdated())

    member x.Write(offset : int, data : nativeint, sizeInBytes : int) =
        ReaderWriterLock.read rw (fun () ->
            Marshal.Copy(data, storage + nativeint offset, sizeInBytes)
        )

        addDirty (Range1i.FromMinAndSize(offset, sizeInBytes - 1))
        transact (fun () -> x.MarkOutdated())

    member x.Write(offset : int, data : Array, sizeInBytes : int) =
        ReaderWriterLock.read rw (fun () ->
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            try Marshal.Copy(gc.AddrOfPinnedObject(), storage + nativeint offset, sizeInBytes)
            finally gc.Free()
        )

        addDirty (Range1i.FromMinAndSize(offset, sizeInBytes - 1))
        transact (fun () -> x.MarkOutdated())

    member x.Write(offset : nativeint, data : nativeint, sizeInBytes : nativeint) =
        x.Write(int offset, data, int sizeInBytes)

    member x.Write(offset : nativeint, data : Array, sizeInBytes : nativeint) =
        x.Write(int offset, data, int sizeInBytes)

    member x.Dispose() =
        x.Resize(0n)
        rw.Dispose()
        lock readers (fun () -> readers.Clear())

    override x.Compute(token) =
        ReaderWriterLock.read rw (fun () ->
            NativeMemoryBuffer(storage, int capacity) :> IBuffer
        )

    member private x.GetReader() =
        let r = new AdaptiveBufferReader(x, removeReader)
        lock readers (fun () -> readers.Add r |> ignore)
        r :> IAdaptiveBufferReader

    interface IAdaptiveBuffer with
        member x.GetReader() = x.GetReader()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ChangeableBuffer =

    let inline create (sizeInBytes : nativeint) =
        new ChangeableBuffer(sizeInBytes)

    let inline destroy (b : ChangeableBuffer) =
        b.Dispose()

    let inline resize (size : nativeint) (b : ChangeableBuffer) =
        b.Resize size

    let inline capacity (b : ChangeableBuffer) =
        b.Capacity

    let inline writeArray (source : #Array) (offset : nativeint) (size : nativeint) (b : ChangeableBuffer) =
        b.Write(offset, source, size)

    let inline write (source : nativeint) (offset : nativeint) (size : nativeint) (b : ChangeableBuffer) =
        b.Write(offset, source, size)

    let inline toAdaptiveBuffer (b : ChangeableBuffer) = b :> IAdaptiveBuffer

    let inline toMod (b : ChangeableBuffer) = b :> aval<_>