namespace Aardvark.Base

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base.Incremental

module TrafoOperators =

    module Trafo3d = 
        let inverse : IMod<Trafo3d> -> IMod<Trafo3d> = 
            UnaryCache<IMod<Trafo3d>, IMod<Trafo3d>>(Mod.map (fun t -> t.Inverse)).Invoke 

        let normalMatrix : IMod<Trafo3d> -> IMod<M33d> = 
            UnaryCache<IMod<Trafo3d>, IMod<M33d>>(Mod.map (fun t -> t.Backward.Transposed.UpperLeftM33())).Invoke

        let inverseArr : IMod<Trafo3d[]> -> IMod<Trafo3d[]> = 
            UnaryCache<IMod<Trafo3d[]>, IMod<Trafo3d[]>>(Mod.map (Array.map (fun t -> t.Inverse))).Invoke 

        let normalMatrixArr : IMod<Trafo3d[]> -> IMod<M33d[]> = 
            UnaryCache<IMod<Trafo3d[]>, IMod<M33d[]>>(Mod.map (Array.map (fun t -> t.Backward.Transposed.UpperLeftM33()))).Invoke
    
    type internal MapModWeak<'a, 'b>(a : IMod<'a>, f : 'a -> 'b) =
        inherit Mod.AbstractMod<'b>()

        let a = WeakReference<_>(a)

        member x.Inner = a
        member x.F = f

        override x.Inputs = 
            match a.TryGetTarget() with
            | (true, x) -> Seq.singleton (x :> IAdaptiveObject)
            | _ -> Seq.empty<_>
        
        override x.Compute(token) =
            match a.TryGetTarget() with
                | (true, x) -> f (x.GetValue token)
                | _ -> //failwith "input no longer alive"
                       x.cache

    type internal Map2ModWeak<'a, 'b, 'c>(a : IMod<'a>, b : IMod<'b>, f : 'a -> 'b -> 'c) =
        inherit Mod.AbstractMod<'c>()

        let a = WeakReference<_>(a)
        let b = WeakReference<_>(b)

        member x.Left = a
        member x.Right = b
        member x.F = f

        override x.Inputs = 
            seq {
                match a.TryGetTarget() with
                | (true, x) -> yield (x :> IAdaptiveObject)
                | _ -> ()
                match b.TryGetTarget() with
                | (true, x) -> yield (x :> IAdaptiveObject)
                | _ -> ()
            }
        
        override x.Compute(token) =
            match a.TryGetTarget(), b.TryGetTarget() with
                | ((true, x), (true, y)) -> f (x.GetValue token) (y.GetValue token)
                | _ -> //failwith "input no longer alive"
                       x.cache
      
    module Mod =
        
        let private scoped (f : 'a -> 'b) =
            let scope = Ag.getContext()
            fun v -> Ag.useScope scope (fun () -> f v)

        let mapWeak (f : 'a -> 'b) (m : IMod<'a>) =
            if m.IsConstant then
                let f = scoped f
                Mod.delay (fun () -> m.GetValue(AdaptiveToken.Empty) |> f)
            else
                MapModWeak(m, f) :> IMod<_>

        let map2weak (f : 'a -> 'b -> 'c) (m1 : IMod<'a>) (m2 : IMod<'b>) =
            match m1.IsConstant, m2.IsConstant with
            | (true, true) -> 
                Mod.delay (fun () -> f (m1.GetValue(AdaptiveToken.Empty)) (m2.GetValue(AdaptiveToken.Empty))) 
            | (true, false) -> 
                MapModWeak(m2, fun b -> f (m1.GetValue(AdaptiveToken.Empty)) b) :> IMod<_>
            | (false, true) -> 
                MapModWeak(m1, fun a -> f a (m2.GetValue(AdaptiveToken.Empty))) :> IMod<_>
            | (false, false) ->
                Map2ModWeak(m1, m2, f) :> IMod<_>


    let (<*>) : IMod<Trafo3d> -> IMod<Trafo3d> -> IMod<Trafo3d> =
        //Mod.map2 (*)
        BinaryCache<IMod<Trafo3d>, IMod<Trafo3d>, IMod<Trafo3d>>(Mod.map2weak (*)).Invoke
                
    let (<.*.>) : IMod<Trafo3d[]> -> IMod<Trafo3d[]> -> IMod<Trafo3d[]> = 
        //Mod.map2 (Array.map2 (*))
        BinaryCache<IMod<Trafo3d[]>, IMod<Trafo3d[]>, IMod<Trafo3d[]>>(Mod.map2weak (Array.map2 (*))).Invoke
        
    let (<*.>) : IMod<Trafo3d> -> IMod<Trafo3d[]> -> IMod<Trafo3d[]> = 
        //Mod.map2 (fun l r -> r |> Array.map (fun r -> l * r ))
        BinaryCache<IMod<Trafo3d>, IMod<Trafo3d[]>, IMod<Trafo3d[]>>(Mod.map2weak (fun l r -> r |> Array.map (fun r -> l * r ))).Invoke
        
    let (<.*>) : IMod<Trafo3d[]> -> IMod<Trafo3d> -> IMod<Trafo3d[]> = 
        //Mod.map2 (fun l r -> l |> Array.map (fun l -> l * r ))
        BinaryCache<IMod<Trafo3d[]>, IMod<Trafo3d>, IMod<Trafo3d[]>>(Mod.map2weak (fun l r -> l |> Array.map (fun l -> l * r ))).Invoke
        
[<AbstractClass>]
type DefaultingModTable() =
    abstract member Hook : IMod -> IMod
    abstract member Reset : unit -> unit
    abstract member Set : obj -> unit
            
type DefaultingModTable<'a>() =
    inherit DefaultingModTable()

    let store = ConditionalWeakTable<IAdaptiveObject, DefaultingModRef<'a>>()
    let all = WeakSet<DefaultingModRef<'a>>()

    override x.Hook(m : IMod) = x.Hook(unbox<IMod<'a>> m) :> IMod
    override x.Set(o : obj) = x.Set(unbox<'a> o)

    member x.Hook (m : IMod<'a>) =
        lock store (fun () ->
            match store.TryGetValue m with
                | (true, r) -> 
                    r :> IMod<_>
                | _ -> 
                    let r = DefaultingModRef m
                    store.Add(m, r)
                    all.Add r |> ignore
                    r :> IMod<_>
        )

    member x.Set(v : 'a) =
        lock store (fun () ->
            for r in all do r.Value <- v
        )

    override x.Reset() = 
        lock store (fun () ->
            for r in all do r.Reset()
        )

        

module Uniforms =
    open TrafoOperators
    open Aardvark.Base.ShaderReflection

    [<AutoOpen>]
    module private Helpers = 
        exception NotFoundException of string

        type Trafo =
            | Single of IMod<Trafo3d>
            | Layered of IMod<Trafo3d[]>

            member x.Inverse =
                match x with
                    | Single v -> Trafo3d.inverse v |> Single
                    | Layered v -> Trafo3d.inverseArr v |> Layered
                    
            member x.Value =
                match x with
                    | Single v -> v :> IMod
                    | Layered v -> v :> IMod
                
            member x.NormalMatrix =
                match x with
                    | Single v -> Trafo3d.normalMatrix v :> IMod
                    | Layered v -> Trafo3d.normalMatrixArr v :> IMod


        let (<*>) (l : Trafo) (r : Trafo) : Trafo =
            match l, r with
                | Single l, Single r -> l <*> r |> Single
                | Layered l, Single r -> l <.*> r |> Layered
                | Single l, Layered r -> l <*.> r |> Layered
                | Layered l, Layered r -> l <.*.> r |> Layered


        let inline (?) (p : IUniformProvider) (name : string) : Trafo =
            match p.TryGetUniform(Ag.emptyScope, Symbol.Create name) with
                | Some (:? IMod<Trafo3d> as m) -> Single m
                | Some (:? IMod<Trafo3d[]> as m) -> Layered m
                | _ -> raise <| NotFoundException name

    let private table : Dictionary<string, IUniformProvider -> IMod> =
        let emptyViewport = Mod.init V2i.II
        Dictionary.ofList [
            "ModelTrafoInv",            fun u -> u?ModelTrafo.Inverse.Value
            "ViewTrafoInv",             fun u -> u?ViewTrafo.Inverse.Value
            "ProjTrafoInv",             fun u -> u?ProjTrafo.Inverse.Value

            "ModelViewTrafo",           fun u -> (u?ModelTrafo <*> u?ViewTrafo).Value
            "ViewProjTrafo",            fun u -> (u?ViewTrafo <*> u?ProjTrafo).Value
            "ModelViewProjTrafo",       fun u -> (u?ModelTrafo <*> (u?ViewTrafo <*> u?ProjTrafo)).Value

            "ModelViewTrafoInv",        fun u -> (u?ModelTrafo <*> u?ViewTrafo).Inverse.Value
            "ViewProjTrafoInv",         fun u -> (u?ViewTrafo <*> u?ProjTrafo).Inverse.Value 
            "ModelViewProjTrafoInv",    fun u -> (u?ModelTrafo <*> (u?ViewTrafo <*> u?ProjTrafo)).Inverse.Value

            "NormalMatrix",             fun u -> u?ModelTrafo.NormalMatrix
        ]

    let tryGetDerivedUniform (name : string) (p : IUniformProvider) =
        match table.TryGetValue name with
            | (true, getter) ->
                //Log.line "Provider %d: %s" (p.GetHashCode()) name
                try getter p |> Some
                with NotFoundException f -> None
            | _ ->
                None

  

