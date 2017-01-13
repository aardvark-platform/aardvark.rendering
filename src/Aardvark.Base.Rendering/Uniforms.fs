namespace Aardvark.Base

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base.Incremental

module TrafoOperators =

    type private UnaryCache<'a, 'b when 'a : not struct and 'b : not struct> private(store : Option<ConditionalWeakTable<'a, 'b>>, f : 'a -> 'b) =
        inherit FSharpFunc<'a, 'b>()
        let store = 
            match store with
                | Some s -> s
                | None -> ConditionalWeakTable<'a, 'b>()

        override x.Invoke (a : 'a) =
            lock store (fun () ->
                match store.TryGetValue(a) with
                    | (true, b) -> b
                    | _ ->
                        let b = f a
                        store.Add(a, b)
                        b
            )

        new(f : 'a -> 'b) = UnaryCache<'a, 'b>(None, f)
        new(store : ConditionalWeakTable<'a, 'b>, f : 'a -> 'b) = UnaryCache<'a, 'b>(Some store, f)

    type private BinaryCache<'a, 'b, 'c when 'a : not struct and 'b : not struct and 'c : not struct>(f : 'a -> 'b -> 'c) =
        inherit OptimizedClosures.FSharpFunc<'a, 'b, 'c>()
        
        let store = ConditionalWeakTable<'a, ConditionalWeakTable<'b, 'c>>()

        override x.Invoke(a : 'a) =
            lock store (fun () ->
                match store.TryGetValue(a) with
                    | (true, inner) ->
                        UnaryCache<'b, 'c>(inner, f a) |> unbox
                    | _ ->
                        let inner = ConditionalWeakTable()
                        store.Add(a, inner)
                        UnaryCache<'b, 'c>(inner, f a) |> unbox
            )

        override x.Invoke(a : 'a, b : 'b) =
            lock store (fun () ->
                match store.TryGetValue(a) with
                    | (true, inner) ->
                        match inner.TryGetValue(b) with
                            | (true, v) -> v
                            | _ ->
                                let v = f a b
                                inner.Add(b, v)
                                v
                    | _ ->
                        let v = f a b
                        let inner = ConditionalWeakTable<'b, 'c>()
                        inner.Add(b, v)
                        store.Add(a, inner)
                        v
            )

    module Trafo3d = 
        let inverse : IMod<Trafo3d> -> IMod<Trafo3d> = 
            UnaryCache<IMod<Trafo3d>, IMod<Trafo3d>>(Mod.map (fun t -> t.Inverse)) |> unbox<_>

        let normalMatrix : IMod<Trafo3d> -> IMod<M33d> = 
            UnaryCache<IMod<Trafo3d>, IMod<M33d>>(Mod.map (fun t -> t.Backward.Transposed.UpperLeftM33())) |> unbox<_>

    let (<*>) : IMod<Trafo3d> -> IMod<Trafo3d> -> IMod<Trafo3d> = 
        BinaryCache<IMod<Trafo3d>, IMod<Trafo3d>, IMod<Trafo3d>>(Mod.map2 (*)) |> unbox<_>

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
        transact (fun () ->
            lock store (fun () ->
                for r in all do r.Value <- v
            )
        )

    override x.Reset() = 
        transact (fun () ->
            lock store (fun () ->
                for r in all do r.Reset()
            )
        )

        

module Uniforms =
    open TrafoOperators

    [<AutoOpen>]
    module private Helpers = 
        exception NotFoundException of string

        let inline (?) (p : IUniformProvider) (name : string) : IMod<'a> =
            match p.TryGetUniform(Ag.emptyScope, Symbol.Create name) with
                | Some (:? IMod<'a> as m) -> m
                | _ -> raise <| NotFoundException name

    let private table : Dictionary<string, IUniformProvider -> IMod> =
        let emptyViewport = Mod.init V2i.II
        Dictionary.ofList [
            "ModelTrafoInv",            fun u -> u?ModelTrafo |> Trafo3d.inverse :> IMod
            "ViewTrafoInv",             fun u -> u?ViewTrafo |> Trafo3d.inverse :> IMod
            "ProjTrafoInv",             fun u -> u?ProjTrafo |> Trafo3d.inverse :> IMod

            "ModelViewTrafo",           fun u -> u?ModelTrafo <*> u?ViewTrafo :> IMod
            "ViewProjTrafo",            fun u -> u?ViewTrafo <*> u?ProjTrafo :> IMod
            "ModelViewProjTrafo",       fun u -> u?ModelTrafo <*> u?ViewTrafo <*> u?ProjTrafo :> IMod

            "ModelViewTrafoInv",        fun u -> u?ModelTrafo <*> u?ViewTrafo |> Trafo3d.inverse :> IMod
            "ViewProjTrafoInv",         fun u -> u?ViewTrafo <*> u?ProjTrafo |> Trafo3d.inverse :> IMod
            "ModelViewProjTrafoInv",    fun u -> u?ModelTrafo <*> u?ViewTrafo <*> u?ProjTrafo |> Trafo3d.inverse :> IMod

            "NormalMatrix",             fun u -> u?ModelTrafo |> Trafo3d.normalMatrix :> IMod

        
        ]

    let tryGetDerivedUniform (name : string) (p : IUniformProvider) =
        match table.TryGetValue name with
            | (true, getter) ->
                try getter p |> Some
                with NotFoundException f -> None
            | _ ->
                None

  

