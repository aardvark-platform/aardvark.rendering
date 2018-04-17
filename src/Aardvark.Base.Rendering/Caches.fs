namespace Aardvark.Base

open System
open System.Collections.Generic
open System.Runtime.CompilerServices

[<AutoOpen>]
module Caches =

    type UnaryCache<'a, 'b when 'a : not struct and 'b : not struct> private(store : Option<ConditionalWeakTable<'a, 'b>>, f : 'a -> 'b) =
        let store = 
            match store with
                | Some s -> s
                | None -> ConditionalWeakTable<'a, 'b>()

        member x.Invoke (a : 'a) =
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

    type BinaryCache<'a, 'b, 'c when 'a : not struct and 'b : not struct and 'c : not struct>(f : 'a -> 'b -> 'c) =
        inherit OptimizedClosures.FSharpFunc<'a, 'b, 'c>()
        
        let store = ConditionalWeakTable<'a, ConditionalWeakTable<'b, 'c>>()

        override x.Invoke(a : 'a) =
            lock store (fun () ->
                match store.TryGetValue(a) with
                    | (true, inner) ->
                        UnaryCache<'b, 'c>(inner, f a).Invoke
                    | _ ->
                        let inner = ConditionalWeakTable()
                        store.Add(a, inner)
                        UnaryCache<'b, 'c>(inner, f a).Invoke
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
