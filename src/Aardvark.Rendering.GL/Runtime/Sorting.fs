namespace Aardvark.Rendering.GL

open Aardvark.Rendering
open Aardvark.Base
open Aardvark.Base.Incremental

 
//module DynamicSorting =
//    let mutable private create = fun (scope : Aardvark.Base.Ag.Scope) (o : RenderObjectOrder) (u : unit) -> failwith "no sorter registered"
//
//    let registerSorter(createSorter : Aardvark.Base.Ag.Scope -> RenderObjectOrder -> unit -> IDynamicRenderObjectSorter) =
//        create <- createSorter
//
//    let createSorter scope order =
//        create scope order
//        

module RenderObjectSorters =
    open System.Threading
    open System.Collections.Generic

    type private RenderObjectComparisonSorter(cmp : IComparer<IRenderObject>) =
        member x.Add (rj : IRenderObject) = ()
        member x.Remove (rj : IRenderObject) = ()
        member x.Compare(l : IRenderObject, r : IRenderObject) = cmp.Compare(l,r)

        interface IRenderObjectSorter with
            member x.Add rj = x.Add rj
            member x.Remove rj = x.Remove rj
            member x.Compare(l,r) = x.Compare(l,r)

    type private RenderObjectGroupingSorter(projections : list<RenderObject -> IMod>) =
        let mutable currentId = 0
        let passCache = ConcurrentDict<IRenderObject, int>(Dict())
        let idCache = ConcurrentDict<IMod, ref<int> * int>(Dict())
        let rjCache = Dict()

        let invokeId (a : IMod) =
            if a.IsConstant then 
                let (cnt, id) = idCache.GetOrCreate(a, fun _ -> ref 0, Interlocked.Increment &currentId)
                cnt := !cnt + 1
                id
            else
                a.Id

        let revokeId (a : IMod) =
            if a.IsConstant then 
                match idCache.TryGetValue a with
                    | (true, (cnt, id)) ->
                        cnt := !cnt - 1
                        if !cnt = 0 then idCache.Remove a |> ignore
                        id
                    | _ ->
                        failwith "cannot revoke id for unknown IMod"
            else
                a.Id

        let getId (a : IMod) =
            if a.IsConstant then
                let (cnt, id) = idCache.[a]
                id
            else
                a.Id


        let toRj (rj : IRenderObject) =
            match rj with
                | :? RenderObject as rj -> rj
                | :? PreparedRenderObject as p -> p.Original
                | _ -> failwith "unknown render object type"

        let invoke (rj : IRenderObject) =
            let ro = rj |> toRj

            rjCache.GetOrCreate(rj, fun _ ->
                (projections |> List.map (fun f -> invokeId (f ro))) @ [ro.Id]
            )

        let revoke (rj : IRenderObject) =
            let ro = rj |> toRj

            rjCache.Remove rj |> ignore
            (projections |> List.map (fun f -> revokeId (f ro))) @ [ro.Id]

        let lookup (rj : IRenderObject) = rjCache.[rj]


        member x.Add (rj : IRenderObject) = 
            invoke rj |> ignore

        member x.Remove (rj : IRenderObject) = 
            revoke rj |> ignore

        member x.Compare(l : IRenderObject, r : IRenderObject) = compare (lookup l) (lookup r)

        interface IRenderObjectSorter with
            member x.Add rj = x.Add rj
            member x.Remove rj = x.Remove rj
            member x.Compare(l,r) = x.Compare(l,r)

    let ofSorting (s : RenderObjectSorting) =
        match s with
            | Static cmp ->
                RenderObjectComparisonSorter(cmp) :> IRenderObjectSorter
            | Grouping proj ->
                RenderObjectGroupingSorter(proj) :> IRenderObjectSorter
            | Dynamic order ->
                failwith "cannot create trie for dynamic sorting"