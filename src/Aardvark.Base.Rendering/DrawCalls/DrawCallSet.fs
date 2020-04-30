namespace Aardvark.Base.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

type DrawCallSet(collapseAdjacent : bool) =
    inherit AVal.AbstractVal<DrawCallInfo[]>()

    let all = System.Collections.Generic.HashSet<Range1i>()
    let mutable ranges = RangeSet.empty

    member x.AddUnsafe(r : Range1i) =
        lock x (fun () ->
            if all.Add r then
                ranges <- RangeSet.insert r ranges
                true
            else
                false
        )

    member x.RemoveUnsafe(r : Range1i) =
        lock x (fun () ->
            if all.Remove r then
                ranges <- RangeSet.remove r ranges
                true
            else
                false
        )

    member x.Add(r : Range1i) =
        let result =
            lock x (fun () ->
                if all.Add r then
                    ranges <- RangeSet.insert r ranges
                    true
                else
                    false
            )

        if result then transact (fun () -> x.MarkOutdated())

        result

    member x.Remove(r : Range1i) =
        let result =
            lock x (fun () ->
                if all.Remove r then
                    ranges <- RangeSet.remove r ranges
                    true
                else
                    false
            )

        if result then transact (fun () -> x.MarkOutdated())
        result

    override x.Compute(token) =
        let drawRanges =
            if collapseAdjacent then ranges :> seq<_>
            else all :> seq<_>

        drawRanges
            |> Seq.map (fun range ->
                DrawCallInfo(
                    FirstIndex = range.Min,
                    FaceVertexCount = range.Size + 1,
                    FirstInstance = 0,
                    InstanceCount = 1,
                    BaseVertex = 0
                )
               )
            |> Seq.toArray

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DrawCallSet =
    let inline create() = DrawCallSet(true)

    let inline add (r : Range1i) (set : DrawCallSet) = set.Add r
    let inline remvoe (r : Range1i) (set : DrawCallSet) = set.Remove r

    let inline toMod (set : DrawCallSet) = set :> aval<_>


