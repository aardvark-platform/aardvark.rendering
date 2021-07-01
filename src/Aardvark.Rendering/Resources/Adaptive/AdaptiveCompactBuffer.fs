namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System
open System.Threading
open OptimizedClosures

type private AdaptiveValueWriter<'T>(value : 'T, elementSizeInBytes : int, write : FSharpFunc<AdaptiveToken, nativeint, 'T, unit>) =
    inherit AdaptiveObject()

    member x.Write(token : AdaptiveToken, index : int, buffer : ChangeableBuffer) =
        x.EvaluateIfNeeded token () (fun token ->
            let offset = index * elementSizeInBytes
            buffer.Write(offset, elementSizeInBytes, fun dst -> write.Invoke(token, dst, value))
        )

    member x.Dispose() =
        x.Outputs.Clear()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


type private AdaptiveValueWriterArray<'T>(value : 'T, index : int, elementSizeInBytes : int, write : FSharpFunc<AdaptiveToken, nativeint, 'T, unit>[]) =
    let writers =
        write |> Array.map (fun w ->
            new AdaptiveValueWriter<'T>(value, elementSizeInBytes, w)
        )

    let mutable currentIndex = index

    member x.SetIndex(index : int) =
        currentIndex <- index
        transact (fun _ -> writers |> Array.iter (fun w -> w.MarkOutdated()))

    member x.Write(token : AdaptiveToken, buffer : ChangeableBuffer) =
        writers |> Array.iter (fun w -> w.Write(token, currentIndex, buffer))

    member x.Dispose() =
        writers |> Array.iter (fun w -> w.Dispose())

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AbstractClass>]
type AdaptiveCompactBuffer<'T>(input : amap<'T, int>, elementSizeInBytes : int) =
    inherit ChangeableBuffer(0n)

    let mutable refCount = 0

    let reader = input.GetReader()
    let writers = Dict<'T, AdaptiveValueWriterArray<'T>>()

    abstract member AcquireValue : value: 'T -> unit
    abstract member ReleaseValue : value: 'T -> unit
    abstract member WriteValue   : FSharpFunc<AdaptiveToken, nativeint, 'T, unit>[]

    abstract member Create : unit -> unit
    default x.Create() = ()

    abstract member Destroy : unit -> unit
    default x.Destroy() =
        reader.Outputs.Remove(x) |> ignore
        x.Dispose()

    member x.Acquire() =
        if Interlocked.Increment(&refCount) = 1 then
            lock x (fun _ ->
                x.Create()
            )

    member x.Release() =
        if Interlocked.Decrement(&refCount) = 0 then
            lock x (fun _ ->
                x.Destroy()
                transact x.MarkOutdated
            )

    member x.ReleaseAll() =
        if Interlocked.Exchange(&refCount, 0) > 0 then
            lock x (fun _ ->
                x.Destroy()
                transact x.MarkOutdated
            )

    member private x.Set(value, index) =
        match writers.TryGetValue value with
        | (true, w) ->
            w.SetIndex(index)
        | _ ->
            x.AcquireValue(value)
            let arr = new AdaptiveValueWriterArray<'T>(value, index, elementSizeInBytes, x.WriteValue)
            writers.Add(value, arr)

    member private x.Remove(value) =
        match writers.TryGetValue value with
        | (true, w) ->
            w.Dispose()
            writers.Remove(value) |> ignore
            x.ReleaseValue(value)
        | _ -> ()

    override x.Compute(token) =
        let ops = reader.GetChanges(token)

        // Process deltas
        for o in ops do
            match o with
            | value, Set index ->
                x.Set(value, index)

            | value, Remove ->
                x.Remove(value)

        // Grow buffer if necessary
        x.Resize <| nativeint (writers.Count * elementSizeInBytes)

        // Write values
        for (KeyValue(_, writer)) in writers do
            writer.Write(token, x)

        base.Compute(token)

    interface IAdaptiveResource with
        member x.Acquire() = x.Acquire()
        member x.Release() = x.Release()
        member x.ReleaseAll() = x.ReleaseAll()
        member x.GetValue(c,t) = x.GetValue(c,t) :> obj

    interface IAdaptiveResource<IBuffer> with
        member x.GetValue(c,t) = x.GetValue(c,t)