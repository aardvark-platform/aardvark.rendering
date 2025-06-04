namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

[<AutoOpen>]
module BoundingBoxExtensions =

    open System.Runtime.CompilerServices

    let private composeCache = BinaryCache<aval<Box3d>, aval<Box3d>, aval<Box3d>>(AVal.map2 (fun l r -> Box.Union(l,r)))
    let private (<+>) l r = composeCache.Invoke(l,r)
    let private invalid = AVal.constant Box3d.Invalid

    let private bbCache = ConditionalWeakTable<RenderObject, aval<Box3d>>()
    
    type RenderObject with
        member x.GetBoundingBox() =
            match bbCache.TryGetValue x with
            | (true, v) -> v
            | _ ->
                let v =
                    match x.VertexAttributes.TryGetAttribute DefaultSemantic.Positions with
                    | ValueSome v ->
                        v.Buffer |> AVal.bind (fun buffer ->
                            match buffer with
                            | :? ArrayBuffer as pos ->
                                let trafo : aval<Trafo3d> = x.AttributeScope?ModelTrafo()

                                AVal.map (fun trafo ->
                                    let box = Box3d.op_Explicit (Box3f(pos.Data |> unbox<V3f[]>))
                                    box
                                ) trafo

                            | _ ->
                                failwithf "invalid positions in renderjob: %A" x
                        )
                    | _ ->
                        failwithf "no positions in renderjob: %A" x
                bbCache.Add(x,v)
                v

    let rec private objBB (o : IRenderObject) =
        match o with
            | :? RenderObject as o -> o.GetBoundingBox()
            | :? MultiRenderObject as o ->
                let boxes = o.Children |> List.map objBB
                AVal.custom (fun t ->
                    boxes |> List.map (fun b -> b.GetValue t) |> Box3d
                )
            | :? IPreparedRenderObject as o ->
                match o.Original with
                    | Some o -> o.GetBoundingBox()
                    | _ -> AVal.constant Box3d.Invalid

            | :? CommandRenderObject as o ->
                match cmdBB o.Command with
                    | Some bb -> bb
                    | None -> invalid
            | _ ->
                invalid

    and private cmdBB (c : RuntimeCommand) : Option<aval<Box3d>> =
        match c with
            | RuntimeCommand.EmptyCmd -> None
            | RuntimeCommand.ClearCmd _ -> None
            | RuntimeCommand.IfThenElseCmd(_,i,e) ->
                match cmdBB i, cmdBB e with
                    | Some i, Some e -> Some (i <+> e)
                    | Some i, None -> Some i
                    | None, Some e -> Some e
                    | None, None -> None
            | RuntimeCommand.DispatchCmd _ ->
                None
            | RuntimeCommand.OrderedCmd l ->
                let merge (s : Box3d) (v : Box3d) : Box3d = Box.Union(s,v)
                l |> AList.toASet |> ASet.choose cmdBB |> ASet.flattenA |> ASet.fold merge Box3d.Invalid |> Some
    
            | RuntimeCommand.RenderCmd objs ->
                let merge (s : Box3d) (v : Box3d) : Box3d = Box.Union(s,v)
                objs |> ASet.mapA objBB |> ASet.fold merge Box3d.Invalid |> Some
            | _ ->
                Log.warn "[Sg] bouningbox for %A not implemented" c 
                None


    type IRenderObject with
        member x.GetBoundingBox() = objBB x
    type RuntimeCommand with
        member x.GetBoundingBox() = cmdBB x
[<AutoOpen>]
module BoundingBoxes =

    type ISg with
        member x.GlobalBoundingBox(scope : Ag.Scope) : aval<Box3d> = x?GlobalBoundingBox(scope)
        member x.LocalBoundingBox(scope : Ag.Scope)  : aval<Box3d> = x?LocalBoundingBox(scope)

    module Semantic =
        let globalBoundingBox (scope : Ag.Scope) (sg : ISg) : aval<Box3d> = sg?GlobalBoundingBox(scope)
        let localBoundingBox  (scope : Ag.Scope) (sg : ISg) : aval<Box3d> = sg?LocalBoundingBox(scope)


    let private trySub (b : Box3d) (d : Box3d) =
        if d.Min.AllGreater b.Min && d.Max.AllSmaller b.Max then
            Some b
        else
            None


    [<Rule>]
    type GlobalBoundingBoxSem() =

        let boxFromArray (v : V3d[]) = if v.Length = 0 then Box3d.Invalid else Box3d v

        member x.GlobalBoundingBox(r : Sg.RenderObjectNode, scope : Ag.Scope) : aval<Box3d> =
            r.Objects |> ASet.mapA (fun o -> o.GetBoundingBox()) |> ASet.fold  (curry Box.Union) Box3d.Invalid

        member x.LocalBoundingBox(r : Sg.RenderObjectNode, scope : Ag.Scope) : aval<Box3d> =
            r.GlobalBoundingBox(scope)

        member x.GlobalBoundingBox(r : Sg.IndirectRenderNode, scope : Ag.Scope) : aval<Box3d> =
            AVal.constant Box3d.Infinite

        member x.LocalBoundingBox(r : Sg.IndirectRenderNode, scope : Ag.Scope) : aval<Box3d> =
            AVal.constant Box3d.Infinite

        member x.GlobalBoundingBox(node : Sg.RenderNode, scope : Ag.Scope) : aval<Box3d> =
            let va = scope.VertexAttributes
            let positions : BufferView = 
                match Map.tryFind DefaultSemantic.Positions va with
                    | Some v -> v
                    | _ -> failwith "no positions specified"

            adaptive {
                let! buffer =  positions.Buffer
                match buffer with
                    | :? ArrayBuffer as ab ->
                        let positions = ab.Data |> unbox<V3f[]>

                        let! trafo = scope.ModelTrafo
                        match scope.VertexIndexBuffer with
                            | None -> 
                                    return positions |> Array.map (fun p -> trafo.Forward.TransformPos(V3d p)) |> boxFromArray
                            | Some indices ->
                                let! indices = indices.Buffer
                                match indices with
                                    | :? ArrayBuffer as b ->
                                        let indices = b.Data
                                        let filteredPositions = if indices.GetType().GetElementType() = typeof<uint16> 
                                                                then indices |> unbox<uint16[]> |> Array.map (fun i -> positions.[int i])
                                                                else indices |> unbox<int[]> |> Array.map (fun i -> positions.[i])
                                        return filteredPositions |> Array.map (fun p -> trafo.Forward.TransformPos(V3d p)) |> boxFromArray
                                    | _ ->
                                        return failwithf "unknown IBuffer for indices: %A" indices
                                            

                    | _ ->
                        return failwithf "unknown IBuffer for positions: %A" buffer
            }

        member x.GlobalBoundingBox(app : IGroup, scope : Ag.Scope) : aval<Box3d> =
            app.Children 
                |> ASet.mapA (fun sg -> sg.GlobalBoundingBox(scope) ) 
                |> ASet.foldHalfGroup (curry Box.Union) trySub Box3d.Invalid
            
        member x.GlobalBoundingBox(n : IApplicator, scope : Ag.Scope) : aval<Box3d> = 
            adaptive {
                let! low = n.Child
                return! low.GlobalBoundingBox(scope)
            }


    [<Rule>]
    type LocalBoundingBoxSem() =

        let boxFromArray (v : V3f[]) = if v.Length = 0 then Box3d.Invalid else Box3d (Box3f v)
        let transform (bb : Box3d) (t : Trafo3d) = bb.Transformed t


        member x.LocalBoundingBox(node : Sg.RenderNode, scope : Ag.Scope) : aval<Box3d> =
            let va = scope.VertexAttributes
            let positions : BufferView = 
                match Map.tryFind DefaultSemantic.Positions va with
                    | Some v -> v
                    | _ -> failwith "no positions specified"

            adaptive {
                let! buffer = positions.Buffer
                match buffer with
                    | :? ArrayBuffer as ab ->
                        let positions = ab.Data |> unbox<V3f[]>

                        match scope.VertexIndexBuffer with
                            | None -> 
                                    return positions |> boxFromArray
                            | Some indices ->
                                let! indices = indices.Buffer
                                match indices with
                                    | :? ArrayBuffer as b ->
                                        let indices = b.Data
                                        let filteredPositions = if indices.GetType().GetElementType() = typeof<uint16> 
                                                                then indices |> unbox<uint16[]> |> Array.map (fun i -> positions.[int i])
                                                                else indices |> unbox<int[]> |> Array.map (fun i -> positions.[i])
                                        return filteredPositions |> boxFromArray
                                    | _ ->
                                        return failwithf "unknown IBuffer for indices: %A" indices
                    | _ ->
                        return failwithf "unknown IBuffer for positions: %A" buffer
            }
            
        member x.LocalBoundingBox(app : IGroup, scope : Ag.Scope) : aval<Box3d> =
            app.Children 
                |> ASet.mapA (fun sg -> sg.LocalBoundingBox(scope)) 
                |> ASet.foldHalfGroup (curry Box.Union) trySub Box3d.Invalid

        member x.LocalBoundingBox(app : Sg.TrafoApplicator, scope : Ag.Scope) : aval<Box3d> =  
            adaptive {
                let! c = app.Child
                let! bb = c.LocalBoundingBox(scope) : aval<Box3d>
                let! trafo = app.Trafo
                return transform bb trafo
            }

        member x.LocalBoundingBox(n : IApplicator, scope : Ag.Scope) : aval<Box3d> = 
            adaptive {
                let! low = n.Child
                return! low.LocalBoundingBox(scope)
            }
