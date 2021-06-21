namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System

type private AdaptiveValueWriter<'T>(getInput : AdaptiveToken -> int -> 'T, index : int, elementSizeInBytes : int, write : nativeint -> 'T -> unit) =
    inherit AdaptiveObject()

    let mutable currentIndex = index

    member x.SetIndex(index : int) =
        currentIndex <- index
        transact x.MarkOutdated

    member x.Write(token : AdaptiveToken, buffer : ChangeableBuffer) =
        x.EvaluateIfNeeded token () (fun _ ->
            let value = getInput token currentIndex
            buffer.Write(currentIndex * elementSizeInBytes, elementSizeInBytes, fun dst -> write dst value)
        )

    member x.Dispose() =
        x.Outputs.Clear()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


type AdaptiveCompactBuffer<'T, 'U>(input : amap<'T, int>, elementSizeInBytes : int,
                                   acquire : 'T -> unit, release : 'T -> unit,
                                   evaluate : AdaptiveToken -> int -> 'T -> 'U,
                                   write : nativeint -> 'U -> unit) =
    inherit AdaptiveResource<IBuffer>()

    let mutable handle = None
    let writers = Dict<'T, AdaptiveValueWriter<'U>>()

    // Adds a writer or modifies its index
    let set value index =
        match writers.TryGetValue value with
        | (true, w) ->
            w.SetIndex(index)
        | _ ->
            acquire value
            let eval t i = evaluate t i value
            let w = new AdaptiveValueWriter<_>(eval, index, elementSizeInBytes, write)
            writers.Add(value, w)

    // Removes writer
    let remove value =
        match writers.TryGetValue value with
        | (true, w) ->
            w.Dispose()
            writers.Remove(value) |> ignore
            release value
        | _ -> ()

    let create() =
        let buffer = ChangeableBuffer.create 0n
        let reader = input.GetReader()
        buffer, reader

    let update (token : AdaptiveToken) (buffer : ChangeableBuffer) (reader : IHashMapReader<'T, int>) =
        let ops = reader.GetChanges(token)
        let mutable delta = 0
        
        // Process deltas
        for o in ops do
            match o with
            | value, Set index ->
                inc &delta;
                set value index

            | value, Remove ->
                dec &delta;
                remove value

        // Grow buffer if necessary
        if delta > 0 then
            buffer |> ChangeableBuffer.resize (buffer.Capacity + nativeint (delta * elementSizeInBytes))

        // Write values
        for (KeyValue(_, writer)) in writers do
            writer.Write(token, buffer)

        buffer.GetValue(token)

    member x.Count =
        writers.Count

    override x.Create() =
        handle <- Some <| create()
    
    override x.Destroy() =
        match handle with
        | Some (b, r) ->
            r.Outputs.Remove(x) |> ignore
            b.Dispose()
            handle <- None

        | _ -> ()

    override x.Compute(t, _) =
        match handle with
        | Some (b, r) ->
            update t b r

        | _ ->
            let (b, r) = create()
            handle <- Some (b, r)
            update t b r