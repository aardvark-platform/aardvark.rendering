namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module TrafoExtensions =

    let inline private trafo v : aval<Trafo3d> = v 
    type Ag.Scope with
        member x.ModelTrafoStack : list<aval<Trafo3d>> = x?ModelTrafoStack         
        member x.ModelTrafo             = 
            match x.Parent with
            | Some p -> x.Node?ModelTrafo(p) |> trafo
            | None -> x.Node?ModelTrafo(Ag.Scope.Root) |> trafo

        member x.ViewTrafo              = x?ViewTrafo               |> trafo
        member x.ProjTrafo              = x?ProjTrafo               |> trafo
             
    module Semantic =
        let modelTrafo            (s : Ag.Scope) : aval<Trafo3d> = s.ModelTrafo
        let viewTrafo             (s : Ag.Scope) : aval<Trafo3d> = s?ViewTrafo
        let projTrafo             (s : Ag.Scope) : aval<Trafo3d> = s?ProjTrafo

module TrafoSemantics =
    open TrafoOperators

    let rootTrafo = AVal.constant Trafo3d.Identity
    let inline private (~%) (l : list<aval<Trafo3d>>) = l


    let flattenStack (stack : list<aval<Trafo3d>>) =
        let rec foldConstants (l : list<aval<Trafo3d>>) =
            match l with
                | [] -> []
                | a::b::rest when a.IsConstant && b.IsConstant ->
                    let n = (AVal.constant (a.GetValue() * b.GetValue()))::rest
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

    [<Rule>]
    type Trafos() =


        member x.ModelTrafoStack(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?ModelTrafoStack <- %[]

        member x.ModelTrafoStack(t : Sg.TrafoApplicator, scope : Ag.Scope) =
            t.Child?ModelTrafoStack <- t.Trafo::scope.ModelTrafoStack


        member x.ModelTrafo(e : obj, scope : Ag.Scope) =
            let stack = scope.ModelTrafoStack
            flattenStack stack

        member x.ViewTrafo(v : Sg.ViewTrafoApplicator, scope : Ag.Scope) =
            v.Child?ViewTrafo <- v.ViewTrafo

        member x.ProjTrafo(p : Sg.ProjectionTrafoApplicator, scope : Ag.Scope) =
            p.Child?ProjTrafo <- p.ProjectionTrafo

        member x.ViewTrafo(r : Root<ISg>, scope : Ag.Scope) =
            r.Child?ViewTrafo <- rootTrafo

        member x.ProjTrafo(r : Root<ISg>, scope : Ag.Scope) =
            r.Child?ProjTrafo <- rootTrafo



