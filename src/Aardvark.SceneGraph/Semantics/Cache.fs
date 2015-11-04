namespace Aardvark.SceneGraph.Internal
open System.Runtime.CompilerServices

module internal Caching =

    type BinaryOpCache<'a,'b,'c when 'a : not struct and 'b : not struct and 'c : not struct>
            ( f : 'a -> 'b -> 'c ) =
        let table = ConditionalWeakTable<'a,ConditionalWeakTable<'b,'c>>()
        
        member x.Invoke a b = 
            table.GetOrCreateValue(a).GetValue(b, ConditionalWeakTable<'b,'c>.CreateValueCallback( fun b -> f a b ))

    type UnaryOpCache<'a,'b when 'a : not struct and 'b : not struct>
            ( f : 'a -> 'b ) =
        let table = ConditionalWeakTable<'a,'b>()

        member x.Invoke a = 
            table.GetValue(a, ConditionalWeakTable<'a,'b>.CreateValueCallback( fun a -> f a ))


    type NAryOpCache<'a, 'b when 'a : not struct and 'b : not struct>(f : list<'a> -> 'b) =
        let head = ConditionalWeakTable<'a, NAryOpCache<'a, 'b>>()
        let mutable value = None

        member private x.TryGetCache (l : list<'a>) =
            match l with
                | [] -> value
                | h::t ->
                    match head.TryGetValue h with
                        | (true, n) -> n.TryGetCache t
                        | _ -> None

        member private x.Get (l : list<'a>) (n : unit -> 'b) =
            match l with
                | [] ->
                    match value with
                        | Some v -> v
                        | None ->
                            let v = n()
                            value <- Some v
                            v
                | h::t ->
                    match head.TryGetValue h with
                        | (true, next) -> next.Get t n
                        | _ -> 
                            let next = NAryOpCache(f)
                            head.Add(h,next)
                            next.Get t n

        member x.Invoke l =
            x.Get l (fun () -> f l)                       



