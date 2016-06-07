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
        member x.ModelTrafoInv          = x?ModelTrafoInv()         |> trafo
        member x.ViewTrafoInv           = x?ViewTrafoInv()          |> trafo
        member x.ProjTrafoInv           = x?ProjTrafoInv()          |> trafo
        member x.ModelViewTrafo         = x?ModelViewTrafo()        |> trafo
        member x.ViewProjTrafo          = x?ViewProjTrafo()         |> trafo
        member x.ModelViewProjTrafo     = x?ModelViewProjTrafo()    |> trafo
        member x.ModelViewTrafoInv      = x?ModelViewTrafoInv()     |> trafo
        member x.ViewProjTrafoInv       = x?ViewProjTrafoInv()      |> trafo
        member x.ModelViewProjTrafoInv  = x?ModelViewProjTrafoInv() |> trafo
             
    module Semantic =
        let modelTrafo            (s : ISg) : IMod<Trafo3d> = s?ModelTrafo()
        let viewTrafo             (s : ISg) : IMod<Trafo3d> = s?ViewTrafo
        let projTrafo             (s : ISg) : IMod<Trafo3d> = s?ProjTrafo
        let modelTrafoInv         (s : ISg) : IMod<Trafo3d> = s?ModelTrafoInv()
        let viewTrafoInv          (s : ISg) : IMod<Trafo3d> = s?ViewTrafoInv()
        let projTrafoInv          (s : ISg) : IMod<Trafo3d> = s?ProjTrafoInv()
        let modelViewTrafo        (s : ISg) : IMod<Trafo3d> = s?ModelViewTrafo()
        let viewProjTrafo         (s : ISg) : IMod<Trafo3d> = s?ViewProjTrafo()
        let modelViewProjTrafo    (s : ISg) : IMod<Trafo3d> = s?ModelViewProjTrafo()
        let modelViewTrafoInv     (s : ISg) : IMod<Trafo3d> = s?ModelViewTrafoInv()
        let viewProjTrafoInv      (s : ISg) : IMod<Trafo3d> = s?ViewProjTrafoInv()
        let modelViewProjTrafoInv (s : ISg) : IMod<Trafo3d> = s?ModelViewProjTrafoInv()


module TrafoSemantics =

    type TrafoMultiplyMod(l : IMod<Trafo3d>, r : IMod<Trafo3d>) =
        inherit Mod.AbstractMod<Trafo3d>()

        override x.Compute() =
            l.GetValue x * r.GetValue x


    /// the root trafo for the entire Sg (used when no trafos are applied)
    let rootTrafo = Mod.constant Trafo3d.Identity
    let inline private (~%) (l : list<IMod<Trafo3d>>) = l

    open System
    open System.Collections.Generic
    type BinaryWeakCache<'a, 'b, 'c when 'a :> IAdaptiveObject and 'b :> IAdaptiveObject and 'c :> IAdaptiveObject>(f : 'a -> 'b -> 'c) =
        let cache = Dictionary<WeakReference<IAdaptiveObject> * WeakReference<IAdaptiveObject>, WeakReference<IAdaptiveObject>>()

        member x.Invoke (a : 'a) (b : 'b) : 'c =
            let key = (a.Weak, b.Weak)
            match cache.TryGetValue(key) with
                | (true, w) ->
                    match w.TryGetTarget() with
                        | (true, strong) -> unbox<'c> strong
                        | _ ->
                            let strong = f a b
                            cache.[key] <- strong.Weak
                            strong
                | _ ->
                    let strong = f a b
                    cache.[key] <- strong.Weak
                    strong

    type UnaryWeakCache<'a, 'b when 'a :> IAdaptiveObject and 'b :> IAdaptiveObject>(f : 'a -> 'b) =
        let cache = Dictionary<WeakReference<IAdaptiveObject>, WeakReference<IAdaptiveObject>>()

        member x.Invoke (a : 'a)  : 'b =
            let key = a.Weak
            match cache.TryGetValue(key) with
                | (true, w) ->
                    match w.TryGetTarget() with
                        | (true, strong) -> unbox<'b> strong
                        | _ ->
                            let strong = f a
                            cache.[key] <- strong.Weak
                            strong
                | _ ->
                    let strong = f a
                    cache.[key] <- strong.Weak
                    strong

    let mulCache = BinaryWeakCache (fun a b -> TrafoMultiplyMod(a, b) :> IMod<Trafo3d>)
    let invCache = UnaryWeakCache (Mod.map (fun (t : Trafo3d) -> t.Inverse))

    let (<*>) a b = 
        if a = rootTrafo then b
        elif b = rootTrafo then a
        else
            if System.Object.ReferenceEquals(a,null) then 
                Log.warn "mul null"
                System.Diagnostics.Debugger.Break()
            if System.Object.ReferenceEquals(b,null) then 
                Log.warn "mul null"
                System.Diagnostics.Debugger.Break()  
            mulCache.Invoke a b

    let inverse t = invCache.Invoke t

        


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

        member x.ViewTrafo(e : Sg.Environment) =
            e.Child?ViewTrafo <- e.ViewTrafo

        member x.ProjTrafo(e : Sg.Environment) =
            e.Child?ProjTrafo <- e.ProjTrafo


        member x.ModelTrafoInv(s : obj) =
            s.ModelTrafo |> inverse

        member x.ViewTrafoInv(s : obj) =
            s.ViewTrafo |> inverse

        member x.ProjTrafoInv(s : obj) =
            s.ProjTrafo |> inverse


        member x.ModelViewTrafo(s : obj) =
            s.ModelTrafo <*> s.ViewTrafo

        member x.ViewProjTrafo(s : obj) =
            s.ViewTrafo <*> s.ProjTrafo

        member x.ModelViewProjTrafo(s : obj) =
            s.ModelTrafo <*> s.ViewProjTrafo


        member x.ModelViewTrafoInv(s : obj) =
            s.ModelViewTrafo |> inverse
        
        member x.ViewProjTrafoInv(s : obj) =
            s.ViewProjTrafo |> inverse

        member x.ModelViewProjTrafoInv(s : obj) =
            s.ModelViewProjTrafo |> inverse