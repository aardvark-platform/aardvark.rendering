namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module TrafoExtensions =

    let inline private trafo v : IMod<Trafo3d> = v 
    type System.Object with
        member x.ModelTrafoStack : list<IMod<Trafo3d>> = x?ModelTrafoStack         
        member x.ModelTrafo             = x?ModelTrafo()            |> trafo
        member x.ViewTrafo              = x?ViewTrafo               |> trafo
        member x.ProjTrafo              = x?ProjTrafo               |> trafo
             
    module Semantic =
        let modelTrafo            (s : ISg) : IMod<Trafo3d> = s?ModelTrafo()
        let viewTrafo             (s : ISg) : IMod<Trafo3d> = s?ViewTrafo
        let projTrafo             (s : ISg) : IMod<Trafo3d> = s?ProjTrafo

module TrafoSemantics =
    open TrafoOperators

    let rootTrafo = Mod.constant Trafo3d.Identity
    let inline private (~%) (l : list<IMod<Trafo3d>>) = l


    let flattenStack (stack : list<IMod<Trafo3d>>) =
        let rec foldConstants (l : list<IMod<Trafo3d>>) =
            match l with
                | [] -> []
                | a::b::rest when a.IsConstant && b.IsConstant ->
                    let n = (Mod.constant (a.GetValue() * b.GetValue()))::rest
                    foldConstants n
                | a::rest ->
                    a::foldConstants rest

        let s = foldConstants stack

        match s with
            | [] -> rootTrafo
            | [a] -> a
            | [a;b] -> a <*> b
            | _ -> 
                // TODO: add a better logic here
                s |> List.fold (<*>) rootTrafo

    [<Semantic>]
    type Trafos() =


        member x.ModelTrafoStack(e : Root<ISg>) =
            e.Child?ModelTrafoStack <- %[]

        member x.ModelTrafoStack(t : Sg.TrafoApplicator) =
            t.Child?ModelTrafoStack <- t.Trafo::t.ModelTrafoStack


        member x.ModelTrafo(e : obj) =
            let stack = e?ModelTrafoStack
            flattenStack stack

        member x.ViewTrafo(v : Sg.ViewTrafoApplicator) =
            v.Child?ViewTrafo <- v.ViewTrafo

        member x.ProjTrafo(p : Sg.ProjectionTrafoApplicator) =
            p.Child?ProjTrafo <- p.ProjectionTrafo

        member x.ViewTrafo(r : Root<ISg>) =
            r.Child?ViewTrafo <- rootTrafo

        member x.ProjTrafo(r : Root<ISg>) =
            r.Child?ProjTrafo <- rootTrafo



