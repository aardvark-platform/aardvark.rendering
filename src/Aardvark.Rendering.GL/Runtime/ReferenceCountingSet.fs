namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic

type ReferenceCountingSet<'a when 'a : equality>() =
    let d = Dictionary()

    member x.Add(v : 'a) =
        match d.TryGetValue v with
            | (true,c) -> d.[v] <- c + 1
                          false
            | _ -> d.[v] <- 1
                   true

    member x.Remove(v : 'a) =
        match d.TryGetValue v with
            | (true,c) -> if c > 1 then 
                            d.[v] <- c - 1
                            false
                            else
                            d.Remove v |> ignore
                            true
            | _ -> false

    member x.Clear() =
        d.Clear()

    member x.Entries = d.Keys