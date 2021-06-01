namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System

type private AdaptiveValueWriter<'T>(input : aval<'T>, index : int, elementSizeInBytes : int, write : nativeint -> 'T -> unit) =
    inherit AdaptiveObject()

    let mutable currentIndex = index

    member x.SetIndex(index : int) =
        currentIndex <- index
        transact x.MarkOutdated

    member x.Write(token : AdaptiveToken, buffer : ChangeableBuffer) =
        x.EvaluateIfNeeded token () (fun _ ->
            let value = input.GetValue(token)
            buffer.Write(currentIndex * elementSizeInBytes, elementSizeInBytes, fun dst -> write dst value)
        )

    member x.Dispose() =
        input.Outputs.Remove(x) |> ignore
        x.Outputs.Clear()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


type AdaptiveCompactBuffer<'T>(input : CompactSet<aval<'T>>, elementSizeInBytes : int, write : nativeint -> 'T -> unit) =
    inherit AdaptiveResource<IBuffer>()

    let mutable handle = None
    let writers = Dict<aval<'T>, AdaptiveValueWriter<'T>>()

    // Adds a writer or modifies its index
    let set value index =
        match writers.TryGetValue value with
        | (true, w) ->
            w.SetIndex(index)
        | _ ->
            let w = new AdaptiveValueWriter<_>(value, index, elementSizeInBytes, write)
            writers.Add(value, w)

    // Removes writer
    let remove value =
        match writers.TryGetValue value with
        | (true, w) ->
            w.Dispose()
            writers.Remove(value) |> ignore
        | _ -> ()

    let create() =
        let buffer = ChangeableBuffer.create 0n
        let reader = input.Indices.GetReader()
        buffer, reader

    let update (token : AdaptiveToken) (buffer : ChangeableBuffer) (reader : IHashMapReader<aval<'T>, int>) =
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