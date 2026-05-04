namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open System

[<AutoOpen>]
module TrafoExtensions =

    module internal Trafo3d =
        let identity = AVal.constant Trafo3d.Identity

    type Scope with
        member this.ModelTrafoStack : list<aval<Trafo3d>> = this?ModelTrafoStack

        member this.ModelTrafo : aval<Trafo3d> =
            match this.Parent with
            | Some parent -> this.Node?ModelTrafo(parent)
            | None -> if notNull this.Node then this.Node?ModelTrafo(Scope.Root) else Trafo3d.identity

        member this.ViewTrafo : aval<Trafo3d> = this?ViewTrafo
        member this.ProjTrafo : aval<Trafo3d> = this?ProjTrafo

    module Semantic =
        let modelTrafo (scope: Scope) = scope.ModelTrafo
        let viewTrafo  (scope: Scope) = scope.ViewTrafo
        let projTrafo  (scope: Scope) = scope.ProjTrafo

module TrafoSemantics =
    open TrafoOperators

    [<Obsolete>]
    let rootTrafo = Trafo3d.identity

    let flattenStack (stack : aval<Trafo3d> list) =
        let rec foldConstants (l : aval<Trafo3d> list) =
            match l with
            | [] -> []
            | a::b::rest when a.IsConstant && b.IsConstant ->
                let n = (AVal.constant (a.GetValue() * b.GetValue()))::rest
                foldConstants n
            | a::rest ->
                a::foldConstants rest

        match foldConstants stack with
        | [] -> Trafo3d.identity
        | [a] -> a
        | [a; b] -> a <*> b
        | s ->
            // TODO: add a better logic here
            s |> List.fold (<*>) Trafo3d.identity

    [<Rule>]
    type Trafos() =
        member x.ModelTrafoStack(e: Root<ISg>, _: Scope) =
            e.Child?ModelTrafoStack <- List.empty<aval<Trafo3d>>

        member x.ModelTrafoStack(t: Sg.TrafoApplicator, scope: Scope) =
            t.Child?ModelTrafoStack <- t.Trafo::scope.ModelTrafoStack

        member x.ModelTrafo(_: obj, scope: Scope) =
            let stack = scope.ModelTrafoStack
            flattenStack stack

        member x.ViewTrafo(v: Sg.ViewTrafoApplicator, _: Scope) =
            v.Child?ViewTrafo <- v.ViewTrafo

        member x.ProjTrafo(p: Sg.ProjectionTrafoApplicator, _: Scope) =
            p.Child?ProjTrafo <- p.ProjectionTrafo

        member x.ViewTrafo(r: Root<ISg>, _: Scope) =
            r.Child?ViewTrafo <- Trafo3d.identity

        member x.ProjTrafo(r: Root<ISg>, _: Scope) =
            r.Child?ProjTrafo <- Trafo3d.identity