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