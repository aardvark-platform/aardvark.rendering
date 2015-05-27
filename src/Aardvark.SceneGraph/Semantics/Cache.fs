namespace Aardvark.SceneGraph.Internal
open System.Runtime.CompilerServices

module internal Caching =

    type BinaryOpCache<'a,'b,'c when 'a : not struct and 'b : not struct and 'c : not struct>
            ( f : 'a -> 'b -> 'c ) =
        let table = ConditionalWeakTable<'a,ConditionalWeakTable<'b,'c>>()

        member x.Invoke a b = 
            let aTable = 
                match table.TryGetValue a with
                    | true,v -> v
                    | _ ->
                        let v = ConditionalWeakTable<'b,'c>()
                        table.Add(a, v)
                        v

            match aTable.TryGetValue b with
                | true,v' -> v'
                | _ ->
                    let r = f a b
                    aTable.Add(b, r) 
                    r
                        
    type UnaryOpCache<'a,'b when 'a : not struct and 'b : not struct>
            ( f : 'a -> 'b ) =
        let table = ConditionalWeakTable<'a,'b>()

        member x.Invoke a b = 
            match table.TryGetValue b with
                | true,v' -> v'
                | _ ->
                    let r = f a
                    table.Add(a, r) 
                    r