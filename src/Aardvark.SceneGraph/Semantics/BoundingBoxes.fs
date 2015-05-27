namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal
open System.Collections.Generic


[<AutoOpen>]
module BoundingBoxExtensions =

    open System.Runtime.CompilerServices

    let private bbCache = ConditionalWeakTable<RenderJob, IMod<Box3d>>()
    type RenderJob with
        member x.GetBoundingBox() =
            match bbCache.TryGetValue x with
                | (true, v) -> v
                | _ ->
                    let v = 
                        match x.VertexAttributes.TryGetAttribute DefaultSemantic.Positions with
                            | Some v ->
                                match v.Buffer with
                                    | :? ArrayBuffer as pos ->
                                        let trafo : IMod<Trafo3d> = x.AttributeScope?ModelTrafo

                                        Mod.map2 (fun arr trafo ->
                                            let box = Box3f.op_Explicit (Box3f(arr |> unbox<V3f[]>))
                                            box
                                        ) pos.Data trafo

                                    | _ ->
                                        failwithf "invalid positions in renderjob: %A" x
                            | _ ->
                                failwithf "no positions in renderjob: %A" x
                    bbCache.Add(x,v)
                    v

[<AutoOpen>]
module BoundingBoxes =

    type ISg with
        member x.GlobalBoundingBox() : IMod<Box3d> = x?GlobalBoundingBox()
        member x.LocalBoundingBox()  : IMod<Box3d> = x?LocalBoundingBox()

    let globalBoundingBox (sg : ISg) = sg.GlobalBoundingBox()
    let localBoundingBox  (sg : ISg) = sg.LocalBoundingBox()

    [<Semantic>]
    type GlobalBoundingBoxSem() =

        let boxFromArray (v : V3d[]) = if v.Length = 0 then Box3d.Invalid else Box3d v

        member x.GlobalBoundingBox(node : Sg.RenderNode) : IMod<Box3d> =
            let scope = Ag.getContext()
            let va = node.VertexAttributes
            let positions : BufferView = 
                match Map.tryFind DefaultSemantic.Positions va with
                    | Some v -> v
                    | _ -> failwith "no positions specified"

            match positions.Buffer with
                | :? ArrayBuffer as ab ->
                    let positions = ab.Data

                    adaptive {
                        let! positions = positions
                        let positions = positions |> unbox<V3f[]>
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
                    }
                | _ ->
                    failwithf "unknown IBuffer for positions: %A" positions.Buffer

        member x.GlobalBoundingBox(app : Sg.Group) : IMod<Box3d> =
            app.ASet 
                |> ASet.map (fun sg -> sg.GlobalBoundingBox() ) 
                |> ASet.toMod 
                |> Mod.bind (fun (values : ISet<IMod<Box3d>>) -> (values |> Seq.map (fun a -> a :> IAdaptiveObject) |> Seq.toList) |> Mod.mapCustom (fun () -> Box3d ( values |> Seq.map Mod.force) ) )

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

            match positions.Buffer with
                | :? ArrayBuffer as ab ->
                    let positions = ab.Data
                    adaptive {
                        let! positions = positions
                        let positions = positions |> unbox<V3f[]>
                        match node.VertexIndexArray with
                            | a when a = Aardvark.SceneGraph.Semantics.AttributeSemantics.emptyIndex -> 
                                    return positions |> boxFromArray
                            | indices ->
                                    let! indices = indices
                                    let indices = indices |> unbox<int[]>
                                    return indices |> Array.map (fun (i : int) -> positions.[i]) |> boxFromArray
                    }
                | _ ->
                    failwithf "unknown IBuffer for positions: %A" positions.Buffer

            
        member x.LocalBoundingBox(app : Sg.Group) : IMod<Box3d> =
            app.ASet 
                |> ASet.map (fun sg -> sg.LocalBoundingBox()) 
                |> ASet.toMod 
                |> Mod.bind (fun (values : ISet<IMod<Box3d>>) -> (values |> Seq.map (fun a -> a :> IAdaptiveObject) |> Seq.toList) |> Mod.mapCustom (fun () -> Box3d ( values |> Seq.map Mod.force) ) )


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
