namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open TrafoOperators

[<AutoOpen>]
module BoundingBoxExtensions =
    let private cache = ConditionalWeakTable<RenderObject, aval<Box3d>>()

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
            lock cache (fun _ ->
                match cache.TryGetValue this with
                | true, bb -> bb
                | _ ->
                    let bb =
                        match this.VertexAttributes.TryGetAttribute DefaultSemantic.Positions with
                        | ValueSome positionBuffer ->
                            let trafo = this.AttributeScope.ModelTrafo <*> scope.ModelTrafo
                            BoundingBox.compute trafo positionBuffer this.Indices

                        | _ ->
                            Box3d.invalid

                    cache.Add(this, bb)
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