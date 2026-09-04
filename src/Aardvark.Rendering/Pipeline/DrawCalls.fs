namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Collections.Generic

[<Struct>]
type private CoverageSegment =
    {
        Min : int64
        Max : int64
        Count : int64
    }

[<RequireQualifiedAccess>]
type DrawCalls =
    | Direct   of aval<DrawCallInfo[]>
    | Indirect of aval<IndirectBuffer>

/// <summary>
/// An adaptive set of indexed draw ranges. Exact range membership determines mutation return values.
/// When <paramref name="collapseAdjacent"/> is true, output is the union of all active ranges, with overlapping
/// and adjacent spans coalesced while each active range retains its own coverage contribution.
/// </summary>
type DrawCallSet(collapseAdjacent : bool) =
    inherit AVal.AbstractVal<DrawCallInfo[]>()

    let all = HashSet<Range1i>()
    // Keep the common non-overlapping path compact, then promote to counted disjoint segments on first overlap.
    let mutable disjoint : ResizeArray<Range1i> = null
    let mutable coverage : ResizeArray<CoverageSegment> = null
    let mutable ranges = RangeSet1i.empty

    let lowerBoundByMin (value : int) =
        let mutable lo = 0
        let mutable hi = disjoint.Count
        while lo < hi do
            let mid = lo + (hi - lo) / 2
            if disjoint.[mid].Min < value then lo <- mid + 1
            else hi <- mid
        lo

    let lowerBoundByMax (value : int64) =
        let mutable lo = 0
        let mutable hi = coverage.Count
        while lo < hi do
            let mid = lo + (hi - lo) / 2
            if coverage.[mid].Max < value then lo <- mid + 1
            else hi <- mid
        lo

    let splitAt (value : int64) =
        let index = lowerBoundByMax value
        if index < coverage.Count then
            let segment = coverage.[index]
            if segment.Min < value && value <= segment.Max then
                coverage.[index] <- { segment with Max = value - 1L }
                coverage.Insert(index + 1, { segment with Min = value })

    let mergeAround (minValue : int64) (maxValue : int64) =
        let mutable index = max 0 (lowerBoundByMax minValue - 1)
        let limit = maxValue + 1L
        let mutable running = true
        while running && index + 1 < coverage.Count do
            let left = coverage.[index]
            let right = coverage.[index + 1]
            if left.Min > limit then
                running <- false
            elif left.Max + 1L = right.Min && left.Count = right.Count then
                coverage.[index] <- { left with Max = right.Max }
                coverage.RemoveAt(index + 1)
            else
                index <- index + 1

    let addCoverage (range : Range1i) =
        let minValue = int64 range.Min
        let maxValue = int64 range.Max
        if coverage.Count = 0 then
            coverage.Add { Min = minValue; Max = maxValue; Count = 1L }
        elif maxValue < coverage.[0].Min then
            let first = coverage.[0]
            if maxValue + 1L = first.Min && first.Count = 1L then
                coverage.[0] <- { first with Min = minValue }
            else
                coverage.Insert(0, { Min = minValue; Max = maxValue; Count = 1L })
        elif minValue > coverage.[coverage.Count - 1].Max then
            let lastIndex = coverage.Count - 1
            let last = coverage.[lastIndex]
            if last.Max + 1L = minValue && last.Count = 1L then
                coverage.[lastIndex] <- { last with Max = maxValue }
            else
                coverage.Add { Min = minValue; Max = maxValue; Count = 1L }
        else
            splitAt minValue
            splitAt (maxValue + 1L)

            let mutable index = lowerBoundByMax minValue
            let mutable current = minValue
            while current <= maxValue do
                if index >= coverage.Count || coverage.[index].Min > maxValue then
                    coverage.Insert(index, { Min = current; Max = maxValue; Count = 1L })
                    current <- maxValue + 1L
                else
                    let segment = coverage.[index]
                    if segment.Min > current then
                        let gapMax = min maxValue (segment.Min - 1L)
                        coverage.Insert(index, { Min = current; Max = gapMax; Count = 1L })
                        index <- index + 1
                        current <- gapMax + 1L
                    else
                        coverage.[index] <- { segment with Count = segment.Count + 1L }
                        index <- index + 1
                        current <- segment.Max + 1L

            mergeAround minValue maxValue

    let removeCoverage (range : Range1i) =
        let minValue = int64 range.Min
        let maxValue = int64 range.Max
        let index = lowerBoundByMax minValue
        if index < coverage.Count && coverage.[index].Min = minValue && coverage.[index].Max = maxValue && coverage.[index].Count = 1L then
            coverage.RemoveAt index
            ranges <- RangeSet1i.remove range ranges
            if coverage.Count = 0 then coverage <- null
        else
            splitAt minValue
            splitAt (maxValue + 1L)

            let mutable index = lowerBoundByMax minValue
            while index < coverage.Count && coverage.[index].Min <= maxValue do
                let segment = coverage.[index]
                if segment.Count = 1L then
                    coverage.RemoveAt index
                    ranges <- RangeSet1i.remove (Range1i(int segment.Min, int segment.Max)) ranges
                else
                    coverage.[index] <- { segment with Count = segment.Count - 1L }
                    index <- index + 1

            mergeAround minValue maxValue
            if coverage.Count = 0 then coverage <- null

    let addRange (range : Range1i) =
        if all.Add range then
            if collapseAdjacent && range.IsValid then
                if not (isNull coverage) then
                    addCoverage range
                else
                    if isNull disjoint then disjoint <- ResizeArray<Range1i>()
                    let index = lowerBoundByMin range.Min
                    let overlapsPrevious = index > 0 && disjoint.[index - 1].Max >= range.Min
                    let overlapsNext = index < disjoint.Count && disjoint.[index].Min <= range.Max
                    if overlapsPrevious || overlapsNext then
                        disjoint.Clear()
                        coverage <- ResizeArray<CoverageSegment>(all.Count)
                        for active in all do
                            if active.IsValid then addCoverage active
                    else
                        disjoint.Insert(index, range)

                ranges <- RangeSet1i.add range ranges
            true
        else
            false

    let removeRange (range : Range1i) =
        if all.Remove range then
            if collapseAdjacent && range.IsValid then
                if isNull coverage then
                    let index = lowerBoundByMin range.Min
                    assert (disjoint.[index] = range)
                    disjoint.RemoveAt index
                    ranges <- RangeSet1i.remove range ranges
                else
                    removeCoverage range
            true
        else
            false

    /// <summary>
    /// Adds an exact range under the set's lock without adaptive invalidation. The change becomes observable to existing
    /// adaptive readers only after a later notifying mutation or caller-arranged invalidation.
    /// </summary>
    member x.AddUnsafe(r : Range1i) =
        lock x (fun () -> addRange r)

    /// <summary>
    /// Removes an exact range under the set's lock without adaptive invalidation. The change becomes observable to existing
    /// adaptive readers only after a later notifying mutation or caller-arranged invalidation.
    /// </summary>
    member x.RemoveUnsafe(r : Range1i) =
        lock x (fun () -> removeRange r)

    /// Adds an exact range under the set's lock and invalidates adaptive readers after a successful mutation.
    member x.Add(r : Range1i) =
        let result =
            lock x (fun () -> addRange r)

        if result then transact (fun () -> x.MarkOutdated())

        result

    /// Removes an exact range under the set's lock and invalidates adaptive readers after a successful mutation.
    member x.Remove(r : Range1i) =
        let result =
            lock x (fun () -> removeRange r)

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


