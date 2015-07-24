namespace Aardvark.Rendering.GL

open Aardvark.Rendering
open Aardvark.Base
open Aardvark.Base.Incremental

 
//module DynamicSorting =
//    let mutable private create = fun (scope : Aardvark.Base.Ag.Scope) (o : RenderJobOrder) (u : unit) -> failwith "no sorter registered"
//
//    let registerSorter(createSorter : Aardvark.Base.Ag.Scope -> RenderJobOrder -> unit -> IDynamicRenderJobSorter) =
//        create <- createSorter
//
//    let createSorter scope order =
//        create scope order
//        

module RenderJobSorters =
    open System.Threading
    open System.Collections.Generic

    type private RenderJobComparisonSorter(cmp : IComparer<RenderJob>) =
        member x.Add (rj : RenderJob) = ()
        member x.Remove (rj : RenderJob) = ()
        member x.Compare(l : RenderJob, r : RenderJob) = cmp.Compare(l,r)

        interface IRenderJobSorter with
            member x.Add rj = x.Add rj
            member x.Remove rj = x.Remove rj
            member x.Compare(l,r) = x.Compare(l,r)

    type private RenderJobGroupingSorter(projections : list<RenderJob -> IMod>) =
        let mutable currentId = 0
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

        let invoke (rj : RenderJob) =
            rjCache.GetOrCreate(rj, fun _ ->
                (projections |> List.map (fun f -> invokeId (f rj))) @ [rj.Id]
            )

        let revoke (rj : RenderJob) =
            rjCache.Remove rj |> ignore
            (projections |> List.map (fun f -> revokeId (f rj))) @ [rj.Id]

        let lookup (rj : RenderJob) =
            rjCache.[rj]


        member x.Add (rj : RenderJob) = invoke rj |> ignore
        member x.Remove (rj : RenderJob) = revoke rj |> ignore
        member x.Compare(l : RenderJob, r : RenderJob) = compare (lookup l) (lookup r)

        interface IRenderJobSorter with
            member x.Add rj = x.Add rj
            member x.Remove rj = x.Remove rj
            member x.Compare(l,r) = x.Compare(l,r)

    let ofSorting (s : RenderJobSorting) =
        match s with
            | Static cmp ->
                RenderJobComparisonSorter(cmp) :> IRenderJobSorter
            | Grouping proj ->
                RenderJobGroupingSorter(proj) :> IRenderJobSorter
            | Dynamic order ->
                failwith "cannot create trie for dynamic sorting"