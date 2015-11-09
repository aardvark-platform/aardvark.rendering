namespace Aardvark.Rendering.GL

open Aardvark.Base
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices

[<AllowNullLiteral>]
type Linked<'a> =
    class

        val mutable public Value : 'a
        val mutable public Prev : Linked<'a>
        val mutable public Next : Linked<'a> 


        new(v, p, n) = { Value = v; Prev = p; Next = n }
        new(v) = { Value = v; Prev = null; Next = null }

        member x.GetEnumerator() = new LinkedEnumerator<'a>(x) :> IEnumerator<'a>

        interface IEnumerable with
            member x.GetEnumerator() = new LinkedEnumerator<'a>(x) :> _

        interface IEnumerable<'a> with
            member x.GetEnumerator() = new LinkedEnumerator<'a>(x) :> _


    end

and private LinkedEnumerator<'a> internal(start : Linked<'a>) =
    let mutable current = start
    let mutable started = false

    interface IEnumerator with
        member x.MoveNext() =
            if not started then started <- true
            else current <- current.Next
            not (isNull current)

        member x.Current =
            current.Value :> obj

        member x.Reset() =
            current <- start
            started <- false

    interface IEnumerator<'a> with
        member x.Current =
            current.Value

        member x.Dispose() =
            current <- null
            started <- false

[<AllowNullLiteral>]
type StableDictionary<'k, 'v>() =
    let store = Dict<'k, Linked<'k * 'v>>()
    let mutable first : Linked<'k * 'v> = null
    let mutable last : Linked<'k * 'v> = null

    member x.Count =
        store.Count

    member x.Item
        with get (key : 'k) = store.[key].Value |> snd
        and set (key : 'k) (value : 'v) =
            match store.TryGetValue key with
                | (true, n) -> 
                    n.Value <- (key, value)
                | _ ->
                    let n = Linked((key, value), last, null)
                    store.Add(key, n)
                    if isNull first then 
                        first <- n

                    if not (isNull last) then last.Next <- n
                    last <- n

    member x.First =
        if isNull first then None
        else Some (first.Value)

    member x.Last =
        if isNull last then None
        else Some (last.Value)

    member x.TryGetPrev (key : 'k, [<Out>] prev : byref<'k * 'v>) =
        match store.TryGetValue key with
            | (true, n) ->
                if isNull n.Prev then
                    false
                else
                    prev <- n.Prev.Value
                    true
            | _ ->
                false

    member x.TryGetNext (key : 'k, [<Out>] prev : byref<'k * 'v>) =
        match store.TryGetValue key with
            | (true, n) ->
                if isNull n.Next then
                    false
                else
                    prev <- n.Next.Value
                    true
            | _ ->
                false

    member x.Alter (key : 'k, f : Option<'v> -> Option<'v>) =
        match store.TryGetValue key with
            | (true, n) ->
                let res = f (Some (snd n.Value))
                match res with
                    | Some res ->
                        // update
                        n.Value <- (key, res)
                        Some res
                    | None ->
                        // delete
                        if isNull n.Prev then first <- n.Next
                        else n.Prev.Next <- n.Next

                        if isNull n.Next then last <- n.Prev
                        else n.Next.Prev <- n.Prev
                        store.Remove key |> ignore

                        None
            | _ ->
                let res = f None
                match res with
                    | Some res ->
                        // add
                        let n = Linked((key, res), last, null)
                        store.Add(key, n)
                        if isNull first then 
                            first <- n

                        if not (isNull last) then last.Next <- n
                        last <- n
                        Some res
                    | None ->
                        None

    member x.AlterWithNeighbours (key : 'k, f : Option<'k * 'v> -> Option<'v> -> Option<'k * 'v> -> Option<'v>) =
        
        match store.TryGetValue key with
            | (true, n) ->
                let left = 
                    if isNull n.Prev then None
                    else Some n.Prev.Value

                let right = 
                    if isNull n.Next then None
                    else Some n.Next.Value

                let res = f left (Some (snd n.Value)) right
                match res with
                    | Some res ->
                        // update
                        n.Value <- (key, res)

                        Some res
                    | None ->
                        // delete
                        if isNull n.Prev then first <- n.Next
                        else n.Prev.Next <- n.Next

                        if isNull n.Next then last <- n.Prev
                        else n.Next.Prev <- n.Prev
                        store.Remove key |> ignore

                        None
            | _ ->
                let left =
                    if isNull last then None
                    else Some last.Value

                let res = f left None None
                match res with
                    | Some res ->
                        // add
                        let n = Linked((key, res), last, null)
                        store.Add(key, n)
                        if isNull first then 
                            first <- n

                        if not (isNull last) then last.Next <- n
                        last <- n
                        Some res

                    | None ->
                        // unchanged
                        None


    member x.Add(key : 'k, value : 'v) =
        let n = Linked((key, value), last, null)
        store.Add(key, n)
        if isNull first then 
            first <- n

        if not (isNull last) then last.Next <- n
        last <- n

    member x.Remove(key : 'k) =
        match store.TryRemove key with
            | (true, n) ->
                
                if isNull n.Prev then first <- n.Next
                else n.Prev.Next <- n.Next

                if isNull n.Next then last <- n.Prev
                else n.Next.Prev <- n.Prev

                true
            | _ ->
                false

    member x.Clear() =
        store.Clear()
        first <- null
        last <- null

    member x.ContainsKey (key : 'k) =
        store.ContainsKey key

    member x.TryGetValue(key : 'k, [<Out>] value : byref<'v>) =
        match store.TryGetValue key with
            | (true,n) ->
                value <- snd n.Value
                true
            | _ ->
                false

    member x.TryRemove(key : 'k, [<Out>] value : byref<'v>) =
        match store.TryRemove key with
            | (true, n) ->
                if isNull n.Prev then first <- n.Next
                else n.Prev.Next <- n.Next

                if isNull n.Next then last <- n.Prev
                else n.Next.Prev <- n.Prev
                
                value <- n.Value |> snd
                true
                 
            | _ ->
                false

    member x.ToArray() =
        let arr = Array.zeroCreate store.Count
        let mutable c = first
        for i in 0..arr.Length-1 do
            arr.[i] <- c.Value
            c <- c.Next

        arr

    member x.Values = first |> Seq.map snd
    member x.Keys = first |> Seq.map fst

    interface IEnumerable with
        member x.GetEnumerator() = new LinkedEnumerator<_>(first) :> _

    interface IEnumerable<'k * 'v> with
        member x.GetEnumerator() = new LinkedEnumerator<_>(first) :> _

[<AllowNullLiteral>]
type StableSet<'a>() =
    let store = Dict<'a, Linked<'a>>()
    let mutable first : Linked<'a> = null
    let mutable last : Linked<'a> = null

    member x.Count =
        store.Count

    member x.First =
        if isNull first then None
        else Some (first.Value)

    member x.Last =
        if isNull last then None
        else Some (last.Value)

    member x.TryGetPrev (key : 'a, [<Out>] prev : byref<'a>) =
        match store.TryGetValue key with
            | (true, n) ->
                if isNull n.Prev then
                    false
                else
                    prev <- n.Prev.Value
                    true
            | _ ->
                false

    member x.TryGetNext (key : 'a, [<Out>] prev : byref<'a>) =
        match store.TryGetValue key with
            | (true, n) ->
                if isNull n.Next then
                    false
                else
                    prev <- n.Next.Value
                    true
            | _ ->
                false


    member x.Add(value : 'a) =
        match store.TryGetValue value with
            | (true, n) ->
                false
            | _ -> 
                let n = Linked(value, last, null)
                store.[value] <- n
                if isNull first then 
                    first <- n

                if not (isNull last) then last.Next <- n
                last <- n

                true

    member x.Remove(value : 'a) =
        match store.TryRemove value with
            | (true, n) ->
                
                if isNull n.Prev then first <- n.Next
                else n.Prev.Next <- n.Next

                if isNull n.Next then last <- n.Prev
                else n.Next.Prev <- n.Prev

                true
            | _ ->
                false

    member x.Clear() =
        store.Clear()
        first <- null
        last <- null

    member x.Contains (value : 'a) =
        store.ContainsKey value

    member x.ToArray() =
        let arr = Array.zeroCreate store.Count
        let mutable c = first
        for i in 0..arr.Length-1 do
            arr.[i] <- c.Value
            c <- c.Next

        arr


    interface IEnumerable with
        member x.GetEnumerator() = new LinkedEnumerator<_>(first) :> _

    interface IEnumerable<'a> with
        member x.GetEnumerator() = new LinkedEnumerator<_>(first) :> _

    interface ICollection<'a> with
        member x.Count = x.Count
        member x.Add v = x.Add v |> ignore
        member x.Remove v = x.Remove v
        member x.Clear() = x.Clear()
        member x.Contains v = x.Contains v
        member x.IsReadOnly = false
        member x.CopyTo(arr, start) =
            let mutable c = first
            for i in start..start + arr.Length-1 do
                arr.[i] <- c.Value
                c <- c.Next

//
//type StableTrie<'k, 'v>() =
//    
//    let mutable prefix = None
//    let mutable values = StableSet<'v>()
//    let mutable children : StableDictionary<'k, StableTrie<'k, 'v>> = null // StableDictionary<'k, StableTrie<'k, 'v>>()
//
//    member x.
//
//    member private x.Values
//        with get() = values
//        and set v = values <- v
//
//    member private x.Prefix
//        with get() = prefix
//        and set p = prefix <- p
//
//    member private x.Add(key : list<'k>, value : 'v, left : Option<'k * 'v>, right : Option<'k * 'v>) =
//        match key with
//            | [] ->
//                if values.Add value then
//                    Some (left, right)
//                else
//                    None
//            | _ when isNull children ->
//                match prefix with
//                    | None ->
//                        prefix <- Some key
//                        values.Add value |> ignore
//                        Some (left, right)
//                    | Some oldPrefix ->
//                        if List.forall2 (curry System.Object.Equals) key oldPrefix then
//                            if values.Add value then
//                                Some (left, right)
//                            else
//                                None
//                        else
//                            prefix <- None
//                            children <- StableDictionary()
//                            match oldPrefix with
//                                | o::os ->
//                                    let oc = StableTrie()
//                                    oc.Values <- values
//                                    match os with
//                                        | [] -> oc.Prefix <- None
//                                        | os -> oc.Prefix <- Some os
//
//                                    children.[o] <- oc
//                                    values <- StableSet()
//
//                                | _ -> failwith "invalid prefix"
//                                        
//
//                            x.Add(key, value, left, right)
//            | k::key ->
//                let mutable left = left
//                let mutable right = right
//                let res =
//                    children.AlterWithNeighbours(k, fun l s r ->
//                        match s with
//                            | None ->
//                                let t = StableTrie()
//                                t.Add(value) |> ignore
//                                left <- l
//                                right <- r
//                                Some t
//                        s
//                    )
//
//                failwith ""
//
//    member x.Add(key : list<'k>, value : 'v) =
//        x.Add(key, value, None, None)
//
//
//
//


