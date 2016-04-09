namespace Aardvark.Base.Rendering


open System
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental

module private Mem =
    let inline alloc (size : nativeint) =
        if size <= 0n then 0n
        else Marshal.AllocHGlobal size

    let inline free (ptr : nativeint) =
        if ptr <> 0n then Marshal.FreeHGlobal ptr

    let inline realloc (ptr : nativeint) (size : nativeint) =
        if ptr = 0n then alloc size
        elif size <= 0n then free ptr; 0n
        else Marshal.ReAllocHGlobal(ptr, size)

    let inline copy (source : nativeint) (target : nativeint) (size : nativeint) =
        if size > 0n then
            Marshal.Copy(source, target, int size)

module private Range = 
    let inline ofOffsetAndSize (offset : nativeint) (size : nativeint) =
        Range1i.FromMinAndSize(int offset, int size - 1)

    let inline ofPtr (ptr : managedptr) =
        Range1i.FromMinAndSize(int ptr.Offset, ptr.Size - 1)


[<CompiledName("ChangeableBuffer")>]
type cbuffer(sizeInBytes : nativeint, release : cbuffer -> unit) =
    inherit Mod.AbstractMod<IBuffer>()

    let mutable capacity = sizeInBytes
    let mutable ptr = Mem.alloc capacity
    let ptrLock = new ReaderWriterLockSlim()

    let readers = HashSet<CBufferReader>()

    let changed (self : cbuffer) (offset : nativeint) (size : nativeint) =
        if size > 0n then
            let range = Range.ofOffsetAndSize offset size
            let transaction = Transaction()
            transaction.Enqueue self

            lock readers (fun () ->
                for r in readers do
                    r.Add range
                    transaction.Enqueue r
            )

            transaction.Commit()


    member x.GetReader() =
        let r = new CBufferReader(x)
        lock readers (fun () -> readers.Add r |> ignore)
        r :> IAdaptiveBufferReader

    member internal x.RemoveReader(r : CBufferReader) =
        lock readers (fun () -> readers.Remove r |> ignore)

    member x.SizeInBytes = capacity

    member x.NativeBuffer = 
        { new INativeBuffer with
            member x.SizeInBytes = int capacity
            member x.Use (f : nativeint -> 'a) =
                ReaderWriterLock.read ptrLock (fun () -> f ptr )
            member x.Pin() = ptr
            member x.Unpin() = ()
        }

    member x.AdjustToSize(capacityInBytes : nativeint) =
        ReaderWriterLock.write ptrLock (fun () ->
            let old = Interlocked.Exchange(&capacity, capacityInBytes)
            if old <> capacityInBytes then
                ptr <- Mem.realloc ptr capacityInBytes
        )

    member x.Write(source : nativeint, offsetInBytes : nativeint, sizeInBytes : nativeint) =
        // check for out of bounds writes
        assert(offsetInBytes >= 0n && sizeInBytes >= 0n)
        assert(offsetInBytes + sizeInBytes <= capacity)

        ReaderWriterLock.read ptrLock (fun () ->
            // copy the content
            Mem.copy source (ptr + offsetInBytes) sizeInBytes
        )

        // notify everyone interested about the change
        changed x offsetInBytes sizeInBytes

    member x.Write(source : Array, offsetInBytes : nativeint, sizeInBytes : nativeint) =
        let gc = GCHandle.Alloc(source, GCHandleType.Pinned)
        try x.Write(gc.AddrOfPinnedObject(), offsetInBytes, sizeInBytes)
        finally gc.Free()

    member x.Dispose() =
        let old = Interlocked.Exchange(&ptr, 0n)
        if old <> 0n then
            release x
            Mem.free ptr
            capacity <- 0n
            ptrLock.Dispose()

    override x.Compute() =
        x.NativeBuffer :> IBuffer

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IAdaptiveBuffer with
        member x.GetReader() = x.GetReader()

and internal CBufferReader(buffer : cbuffer) =
    inherit AdaptiveObject()

    let mutable dirty = RangeSet.empty
    let mutable lastCapacity = -1n

    member x.Add(r : Range1i) =
        lock x (fun () ->
            if lastCapacity = buffer.SizeInBytes then
                Interlocked.Change(&dirty, RangeSet.insert r) |> ignore
        )
    member x.Dispose() =
        buffer.RemoveReader x
        dirty <- RangeSet.empty
        lastCapacity <- -1n

    member x.GetDirtyRanges (caller : IAdaptiveObject) : INativeBuffer * RangeSet =
        x.EvaluateAlways caller (fun () ->
            let ranges = Interlocked.Exchange(&dirty, RangeSet.empty)

            let nb = buffer.NativeBuffer
            let size = nativeint nb.SizeInBytes
            let oldCap = Interlocked.Exchange(&lastCapacity, size)

            if oldCap = size then
                nb,ranges
            else
                nb, RangeSet.ofList [Range1i(0, int size - 1)]
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IAdaptiveBufferReader with
        member x.GetDirtyRanges caller = x.GetDirtyRanges caller

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CBuffer =
    
    let inline create (sizeInBytes : nativeint) =
        new cbuffer(sizeInBytes, ignore)

    let inline destroy (b : cbuffer) =
        b.Dispose()

    let inline resize (size : nativeint) (b : cbuffer) =
        b.AdjustToSize size

    let inline capacity (b : cbuffer) =
        b.SizeInBytes


    let inline writeArray (source : #Array) (offset : nativeint) (size : nativeint) (b : cbuffer) =
        b.Write(source, offset, size)

    let inline write (source : nativeint) (offset : nativeint) (size : nativeint) (b : cbuffer) =
        b.Write(source, offset, size)


    let inline toAdaptiveBuffer (b : cbuffer) = b :> IAdaptiveBuffer

    let inline toMod (b : cbuffer) = b :> IMod<_>