namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open System
open System.Runtime.CompilerServices
open System.Threading
open TrafoOperators

[<AutoOpen>]
module BoundingBoxExtensions =
    [<Sealed; AllowNullLiteral>]
    type private ScopedBoundingBox(scope: Scope, outerTrafo: aval<Trafo3d>, value: aval<Box3d>) as this =
        let scopeHash = RuntimeHelpers.GetHashCode scope
        let scope = WeakReference<Scope>(scope)
        let self = WeakReference<ScopedBoundingBox>(this)

        member _.OuterTrafo = outerTrafo
        member _.Value = value
        member _.Weak = self
        member _.ScopeHash = scopeHash

        member _.TryGet(query: Scope, result: byref<aval<Box3d>>) =
            let mutable cached = Unchecked.defaultof<Scope>
            if scope.TryGetTarget(&cached) && Object.ReferenceEquals(cached, query) then
                result <- value
                true
            else
                false

    [<Sealed>]
    type private RenderObjectBoundingBoxCache() =
        // Scope semantic lookup allocates, so retain two weak, lock-free aliases for common warmed callers.
        let mutable recent1 : WeakReference<ScopedBoundingBox> = null
        let mutable recent1Hash = 0
        let mutable recent2 : WeakReference<ScopedBoundingBox> = null
        let mutable recent2Hash = 0

        // Entries define cache identity; Scopes weakly memoize each scope's stable effective transform.
        member val Entries = ConditionalWeakTable<aval<Trafo3d>, aval<Box3d>>()
        member val Scopes = ConditionalWeakTable<Scope, ScopedBoundingBox>()

        member _.TryGetRecent(scope: Scope, result: byref<aval<Box3d>>) =
            let hash = RuntimeHelpers.GetHashCode scope
            let mutable entry = Unchecked.defaultof<ScopedBoundingBox>

            if Volatile.Read(&recent1Hash) = hash then
                let first = Volatile.Read(&recent1)
                notNull first && first.TryGetTarget(&entry) && entry.TryGet(scope, &result)
            elif Volatile.Read(&recent2Hash) = hash then
                let second = Volatile.Read(&recent2)
                notNull second && second.TryGetTarget(&entry) && entry.TryGet(scope, &result)
            else
                false

        member _.Remember(entry: ScopedBoundingBox) =
            let weak = entry.Weak
            let first = Volatile.Read(&recent1)
            if not <| Object.ReferenceEquals(first, weak) then
                Volatile.Write(&recent2, first)
                Volatile.Write(&recent2Hash, Volatile.Read(&recent1Hash))
                Volatile.Write(&recent1, weak)
                Volatile.Write(&recent1Hash, entry.ScopeHash)

    let private cache = ConditionalWeakTable<RenderObject, RenderObjectBoundingBoxCache>()

    module internal Box3d =
        let invalid = AVal.constant Box3d.Invalid

        let private union : Box3d -> Box3d -> Box3d = curry Box.Union

        let private trySubtract (a: Box3d) (b: Box3d) =
            if Vec.allGreater b.Min a.Min && Vec.allSmaller b.Max a.Max then
                Some a
            else
                None

        let ofASet set = set |> ASet.foldHalfGroup union trySubtract Box3d.Invalid
        let ofAList list = list |>  AList.foldHalfGroup union trySubtract Box3d.Invalid

    module internal BoundingBox =
        let compute (trafo: aval<Trafo3d>) (positionBuffer: BufferView) (indexBuffer: BufferView option) =
            let positions : aval<V3d[]> =
                positionBuffer
                |> BufferView.download 0 -1
                |> PrimitiveValueConverter.convertArray positionBuffer.ElementType

            match indexBuffer with
            | Some indexBuffer ->
                let indices : aval<int[]> =
                    indexBuffer
                    |> BufferView.download 0 -1
                    |> PrimitiveValueConverter.convertArray indexBuffer.ElementType

                (positions, indices, trafo) |||> AVal.map3 (fun positions indices trafo ->
                    let vertices = indices |> Array.map (fun i -> positions.[i] |> Mat.transformPos trafo.Forward)
                    Box3d vertices
                )

            | _ ->
                (positions, trafo) ||> AVal.map2 (fun positions trafo ->
                    let vertices = positions |> Array.map (Mat.transformPos trafo.Forward)
                    Box3d vertices
                )

    type RenderObject with
        member this.GetBoundingBox(scope: Scope) =
            let objectCache =
                match cache.TryGetValue this with
                | true, cache -> cache
                | _ -> cache.GetOrCreateValue this

            let mutable bb = Unchecked.defaultof<aval<Box3d>>
            if objectCache.TryGetRecent(scope, &bb) then
                bb
            else
                match objectCache.Scopes.TryGetValue scope with
                | true, entry ->
                    objectCache.Remember entry
                    entry.Value
                | _ ->
                    lock objectCache (fun _ ->
                        match objectCache.Scopes.TryGetValue scope with
                        | true, entry ->
                            objectCache.Remember entry
                            entry.Value
                        | _ ->
                            let outerTrafo = scope.ModelTrafo

                            let bb =
                                match objectCache.Entries.TryGetValue outerTrafo with
                                | true, bb -> bb
                                | _ ->
                                    let bb =
                                        match this.VertexAttributes.TryGetAttribute DefaultSemantic.Positions with
                                        | ValueSome positionBuffer ->
                                            let trafo = this.AttributeScope.ModelTrafo <*> outerTrafo
                                            BoundingBox.compute trafo positionBuffer this.Indices

                                        | _ ->
                                            Box3d.invalid

                                    objectCache.Entries.Add(outerTrafo, bb)
                                    bb

                            let entry = ScopedBoundingBox(scope, outerTrafo, bb)
                            objectCache.Scopes.Add(scope, entry)
                            objectCache.Remember entry
                            bb
                    )

        member this.GetBoundingBox() = this.GetBoundingBox Scope.Root

    let rec private objBB (scope: Scope) (ro : IRenderObject) =
        match ro with
        | :? RenderObject as ro ->
            ro.GetBoundingBox scope

        | :? MultiRenderObject as ro ->
            if ro.Children.IsEmpty then
                Box3d.invalid
            else
                let boxes = ro.Children |> List.map (objBB scope)
                AVal.custom (fun t -> boxes |> List.map (fun b -> b.GetValue t) |> Box3d)

        | :? IPreparedRenderObject as ro ->
            match ro.Original with
            | Some ro -> ro.GetBoundingBox scope
            | _ -> Box3d.invalid

        | :? CommandRenderObject as o ->
            cmdBB scope o.Command

        | _ ->
            Box3d.invalid

    and private cmdBB (scope: Scope) (cmd: RuntimeCommand) =
        match cmd with
        | RuntimeCommand.EmptyCmd ->
            Box3d.invalid

        | RuntimeCommand.ClearCmd _ ->
            Box3d.invalid

        | RuntimeCommand.IfThenElseCmd (c, t, f) ->
            let t = cmdBB scope t
            let f = cmdBB scope f
            c |> AVal.bind (fun c -> if c then t else f)

        | RuntimeCommand.DispatchCmd _ ->
            Box3d.invalid

        | RuntimeCommand.OrderedCmd commands ->
            commands |> AList.mapA (cmdBB scope) |> Box3d.ofAList

        | RuntimeCommand.RenderCmd objects ->
            objects |> ASet.mapA (objBB scope) |> Box3d.ofASet

        | RuntimeCommand.LodTreeCmd _
        | RuntimeCommand.GeometriesCmd _
        | RuntimeCommand.GeometriesSimpleCmd _ ->
            Log.warn "[Sg] Bounding box computation for %A not implemented" cmd
            Box3d.invalid

    type IRenderObject with
        member this.GetBoundingBox(scope: Scope) = objBB scope this
        member this.GetBoundingBox() = this.GetBoundingBox Scope.Root

    type RuntimeCommand with
        member this.GetBoundingBox(scope: Scope) = cmdBB scope this |> Some
        member this.GetBoundingBox() = this.GetBoundingBox Scope.Root

[<AutoOpen>]
module BoundingBoxes =

    type ISg with
        member this.GlobalBoundingBox(scope: Scope) : aval<Box3d> = this?GlobalBoundingBox(scope)
        member this.LocalBoundingBox(scope: Scope)  : aval<Box3d> = this?LocalBoundingBox(scope)

    module Semantic =
        let globalBoundingBox (scope: Scope) (sg: ISg) : aval<Box3d> = sg?GlobalBoundingBox(scope)
        let localBoundingBox  (scope: Scope) (sg: ISg) : aval<Box3d> = sg?LocalBoundingBox(scope)

    [<Rule>]
    type internal BoundingBoxSem() =
        member _.GlobalBoundingBox(r : Sg.RenderObjectNode, scope: Scope) : aval<Box3d> =
            r.Objects |> ASet.mapA _.GetBoundingBox(scope) |> Box3d.ofASet

        member this.LocalBoundingBox(r : Sg.RenderObjectNode, scope: Scope) : aval<Box3d> =
            this.GlobalBoundingBox(r, scope)


        member _.GlobalBoundingBox(_: Sg.IndirectRenderNode, _: Scope) : aval<Box3d> =
            Box3d.invalid

        member this.LocalBoundingBox(r: Sg.IndirectRenderNode, scope: Scope) : aval<Box3d> =
            this.GlobalBoundingBox(r, scope)


        member _.GlobalBoundingBox(_: Sg.RenderNode, scope: Scope) : aval<Box3d> =
            match scope.VertexAttributes |> Map.tryFindV DefaultSemantic.Positions with
            | ValueSome positionBuffer -> BoundingBox.compute scope.ModelTrafo positionBuffer scope.VertexIndexBuffer
            | _ -> Box3d.invalid

        member this.LocalBoundingBox(r: Sg.RenderNode, scope: Scope) : aval<Box3d> =
            this.GlobalBoundingBox(r, scope)


        member _.GlobalBoundingBox(app: IGroup, scope: Scope) : aval<Box3d> =
            app.Children |> ASet.mapA _.GlobalBoundingBox(scope) |> Box3d.ofASet

        member this.LocalBoundingBox(app: IGroup, scope: Scope) : aval<Box3d> =
            this.GlobalBoundingBox(app, scope)


        member _.GlobalBoundingBox(app: IApplicator, scope: Scope) : aval<Box3d> =
            app.Child |> AVal.bind _.GlobalBoundingBox(scope)

        member this.LocalBoundingBox(app: IApplicator, scope: Scope) : aval<Box3d> =
            this.GlobalBoundingBox(app, scope)