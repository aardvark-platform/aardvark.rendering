namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal
open System.Collections.Generic


[<AutoOpen>]
module BoundingBoxExtensions =

    open System.Runtime.CompilerServices

    let private composeCache = BinaryCache<aval<Box3d>, aval<Box3d>, aval<Box3d>>(AVal.map2 (fun l r -> Box3d.Union(l,r)))
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
                            | Some v ->
                                v.Buffer |> AVal.bind (fun buffer ->
                                    match buffer with
                                        | :? ArrayBuffer as pos ->
                                            let trafo : aval<Trafo3d> = x.AttributeScope?ModelTrafo()

                                            AVal.map (fun trafo ->
                                                let box = Box3f.op_Explicit (Box3f(pos.Data |> unbox<V3f[]>))
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
                let merge (s : Box3d) (v : Box3d) : Box3d = Box3d.Union(s,v)
                l |> AList.toASet |> ASet.choose cmdBB |> ASet.flattenA |> ASet.fold merge Box3d.Invalid |> Some
    
            | RuntimeCommand.RenderCmd objs ->
                let merge (s : Box3d) (v : Box3d) : Box3d = Box3d.Union(s,v)
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
        member x.GlobalBoundingBox() : aval<Box3d> = x?GlobalBoundingBox()
        member x.LocalBoundingBox()  : aval<Box3d> = x?LocalBoundingBox()

    module Semantic =
        let globalBoundingBox (sg : ISg) : aval<Box3d> = sg?GlobalBoundingBox()
        let localBoundingBox  (sg : ISg) : aval<Box3d> = sg?LocalBoundingBox()


    let private trySub (b : Box3d) (d : Box3d) =
        if d.Min.AllGreater b.Min && d.Max.AllSmaller b.Max then
            Some b
        else
            None


    [<Semantic>]
    type GlobalBoundingBoxSem() =

        let boxFromArray (v : V3d[]) = if v.Length = 0 then Box3d.Invalid else Box3d v

        let computeBoundingBox (g : IndexedGeometry) =
            match g.IndexedAttributes.TryGetValue DefaultSemantic.Positions with
                | (true, arr) ->
                    match arr with
                        | :? array<V3f> as arr -> Box3f(arr) |> Box3f.op_Explicit
                        | :? array<V4f> as arr -> Box3f(arr |> Array.map Vec.xyz) |> Box3f.op_Explicit
                        | _ -> failwithf "unknown position-type: %A" arr
                | _ ->
                    Box3d.Invalid

        member x.LocalBoundingBox(r : Sg.GeometrySet) : aval<Box3d> =
            r.Geometries 
                |> ASet.map computeBoundingBox
                |> ASet.foldHalfGroup (curry Box3d.Union) trySub Box3d.Invalid

        member x.GlobalBoundingBox(r : Sg.GeometrySet) : aval<Box3d> =
            let l = r.LocalBoundingBox()
            let t = r.ModelTrafo
            AVal.map2 (fun (t : Trafo3d) (b : Box3d) -> b.Transformed(t)) t l

        member x.GlobalBoundingBox(r : Sg.RenderObjectNode) : aval<Box3d> =
            r.Objects |> ASet.mapA (fun o -> o.GetBoundingBox()) |> ASet.fold  (curry Box3d.Union) Box3d.Invalid

        member x.LocalBoundingBox(r : Sg.RenderObjectNode) : aval<Box3d> =
            r.GlobalBoundingBox()

        member x.GlobalBoundingBox(r : Sg.IndirectRenderNode) : aval<Box3d> =
            AVal.constant Box3d.Infinite

        member x.LocalBoundingBox(r : Sg.IndirectRenderNode) : aval<Box3d> =
            AVal.constant Box3d.Infinite

        member x.LocalBoundingBox(p : Sg.OverlayNode) : aval<Box3d> =
            AVal.constant Box3d.Invalid

        member x.GlobalBoundingBox(p : Sg.OverlayNode) : aval<Box3d> =
            AVal.constant Box3d.Invalid

        member x.GlobalBoundingBox(node : Sg.RenderNode) : aval<Box3d> =
            let scope = Ag.getContext()
            let va = node.VertexAttributes
            let positions : BufferView = 
                match Map.tryFind DefaultSemantic.Positions va with
                    | Some v -> v
                    | _ -> failwith "no positions specified"

            adaptive {
                let! buffer =  positions.Buffer
                match buffer with
                    | :? ArrayBuffer as ab ->
                        let positions = ab.Data |> unbox<V3f[]>

                        let! trafo = node.ModelTrafo
                        match node.VertexIndexBuffer with
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

        member x.GlobalBoundingBox(app : IGroup) : aval<Box3d> =
            app.Children 
                |> ASet.mapA (fun sg -> sg.GlobalBoundingBox() ) 
                |> ASet.foldHalfGroup (curry Box3d.Union) trySub Box3d.Invalid
            
        member x.GlobalBoundingBox(n : IApplicator) : aval<Box3d> = 
            adaptive {
                let! low = n.Child
                return! low.GlobalBoundingBox()
            }


    [<Semantic>]
    type LocalBoundingBoxSem() =

        let boxFromArray (v : V3f[]) = if v.Length = 0 then Box3d.Invalid else Box3d (Box3f v)
        let transform (bb : Box3d) (t : Trafo3d) = bb.Transformed t


        member x.LocalBoundingBox(node : Sg.RenderNode) : aval<Box3d> =
            let scope = Ag.getContext()
            let va = node.VertexAttributes
            let positions : BufferView = 
                match Map.tryFind DefaultSemantic.Positions va with
                    | Some v -> v
                    | _ -> failwith "no positions specified"

            adaptive {
                let! buffer = positions.Buffer
                match buffer with
                    | :? ArrayBuffer as ab ->
                        let positions = ab.Data |> unbox<V3f[]>

                        match node.VertexIndexBuffer with
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
            
        member x.LocalBoundingBox(app : IGroup) : aval<Box3d> =
            app.Children 
                |> ASet.mapA (fun sg -> sg.LocalBoundingBox()) 
                |> ASet.foldHalfGroup (curry Box3d.Union) trySub Box3d.Invalid

        member x.LocalBoundingBox(app : Sg.TrafoApplicator) : aval<Box3d> =  
            adaptive {
                let! c = app.Child
                let! bb = c.LocalBoundingBox() : aval<Box3d>
                let! trafo = app.Trafo
                return transform bb trafo
            }

        member x.LocalBoundingBox(n : IApplicator) : aval<Box3d> = 
            adaptive {
                let! low = n.Child
                return! low.LocalBoundingBox()
            }
