namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal
open System.Collections.Generic


[<AutoOpen>]
module BoundingBoxExtensions =

    open System.Runtime.CompilerServices

    let private bbCache = ConditionalWeakTable<RenderObject, IMod<Box3d>>()
    type RenderObject with
        member x.GetBoundingBox() =
            match bbCache.TryGetValue x with
                | (true, v) -> v
                | _ ->
                    let v = 
                        match x.VertexAttributes.TryGetAttribute DefaultSemantic.Positions with
                            | Some v ->
                                v.Buffer |> Mod.bind (fun buffer ->
                                    match buffer with
                                        | :? ArrayBuffer as pos ->
                                            let trafo : IMod<Trafo3d> = x.AttributeScope?ModelTrafo()

                                            Mod.map (fun trafo ->
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

[<AutoOpen>]
module BoundingBoxes =

    type ISg with
        member x.GlobalBoundingBox() : IMod<Box3d> = x?GlobalBoundingBox()
        member x.LocalBoundingBox()  : IMod<Box3d> = x?LocalBoundingBox()

    module Semantic =
        let globalBoundingBox (sg : ISg) : IMod<Box3d> = sg?GlobalBoundingBox()
        let localBoundingBox  (sg : ISg) : IMod<Box3d> = sg?LocalBoundingBox()

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

        member x.LocalBoundingBox(r : Sg.GeometrySet) : IMod<Box3d> =
            r.Geometries 
                |> ASet.map computeBoundingBox
                |> ASet.foldMonoid (curry Box3d.Union) Box3d.Invalid

        member x.GlobalBoundingBox(r : Sg.GeometrySet) : IMod<Box3d> =
            let l = r.LocalBoundingBox()
            let t = r.ModelTrafo
            Mod.map2 (fun (t : Trafo3d) (b : Box3d) -> b.Transformed(t)) t l


        member x.LocalBoundingBox(p : Sg.OverlayNode) : IMod<Box3d> =
            Mod.constant Box3d.Invalid

        member x.GlobalBoundingBox(p : Sg.OverlayNode) : IMod<Box3d> =
            Mod.constant Box3d.Invalid

        member x.GlobalBoundingBox(node : Sg.RenderNode) : IMod<Box3d> =
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
                        match node.VertexIndexArray with
                            | a when a = Aardvark.SceneGraph.Semantics.AttributeSemantics.emptyIndex -> 
                                    return positions |> Array.map (fun p -> trafo.Forward.TransformPos(V3d p)) |> boxFromArray
                            | indices ->
                                    let! indices = indices
                                    let filteredPositions = if indices.GetType().GetElementType() = typeof<uint16> 
                                                            then indices |> unbox<uint16[]> |> Array.map (fun i -> positions.[int i])
                                                            else indices |> unbox<int[]> |> Array.map (fun i -> positions.[i])
                                    return filteredPositions |> Array.map (fun p -> trafo.Forward.TransformPos(V3d p)) |> boxFromArray

                    | _ ->
                        return failwithf "unknown IBuffer for positions: %A" buffer
            }

        member x.GlobalBoundingBox(app : IGroup) : IMod<Box3d> =
            app.Children 
                |> ASet.map (fun sg -> sg.GlobalBoundingBox() ) 
                |> ASet.foldMonoidM (curry Box3d.Union) Box3d.Invalid
            
        member x.GlobalBoundingBox(n : IApplicator) : IMod<Box3d> = 
            adaptive {
                let! low = n.Child
                return! low.GlobalBoundingBox()
            }


    [<Semantic>]
    type LocalBoundingBoxSem() =

        let boxFromArray (v : V3f[]) = if v.Length = 0 then Box3d.Invalid else Box3d (Box3f v)
        let transform (bb : Box3d) (t : Trafo3d) = bb.Transformed t


        member x.LocalBoundingBox(node : Sg.RenderNode) : IMod<Box3d> =
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

                        match node.VertexIndexArray with
                            | a when a = Aardvark.SceneGraph.Semantics.AttributeSemantics.emptyIndex -> 
                                    return positions |> boxFromArray
                            | indices ->
                                    let! indices = indices
                                    let indices = indices |> unbox<int[]>
                                    return indices |> Array.map (fun (i : int) -> positions.[i]) |> boxFromArray
                    | _ ->
                        return failwithf "unknown IBuffer for positions: %A" buffer
            }
            
        member x.LocalBoundingBox(app : IGroup) : IMod<Box3d> =
            app.Children 
                |> ASet.map (fun sg -> sg.LocalBoundingBox()) 
                |> ASet.foldMonoidM (curry Box3d.Union) Box3d.Invalid

        member x.LocalBoundingBox(app : Sg.TrafoApplicator) : IMod<Box3d> =  
            adaptive {
                let! c = app.Child
                let! bb = c.LocalBoundingBox() : IMod<Box3d>
                let! trafo = app.Trafo
                return transform bb trafo
            }

        member x.LocalBoundingBox(n : IApplicator) : IMod<Box3d> = 
            adaptive {
                let! low = n.Child
                return! low.LocalBoundingBox()
            }
