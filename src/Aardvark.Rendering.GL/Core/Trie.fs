namespace Aardvark.Rendering.GL

open Aardvark.Base
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices

[<AllowNullLiteral>]
type Linked<'a> =
    class
        val mutable public Value : 'a
        val mutable public Next : Linked<'a>
        val mutable public Prev : Linked<'a>

        new(value) = { Value = value; Next = null; Prev = null }

        interface IEnumerable with
            member x.GetEnumerator() = new LinkedEnumerator<'a>(x) :> IEnumerator

        interface IEnumerable<'a> with
            member x.GetEnumerator() = new LinkedEnumerator<'a>(x) :> IEnumerator<_>

    end

and private LinkedEnumerator<'a> (n : Linked<'a>) =
    let mutable head =
        let l = Linked(Unchecked.defaultof<_>)
        l.Next <- n
        l

    let mutable current = head

    interface IEnumerator with
        member x.MoveNext() =
            if current.Next <> null then
                current <- current.Next
                true
            else
                false

        member x.Reset() = current <- head

        member x.Current = current.Value :> obj

    interface IEnumerator<'a> with
        member x.Current = current.Value

        member x.Dispose() =
            current <- null
            head <- null

module Linked =
    let create (v : 'a) = Linked(v)

    let link (left : Linked<'a>) (right : Linked<'a>) =
        if left <> null then
            left.Next <- right

        if right <> null then
            right.Prev <- left

    let ofSeq (s : seq<'a>) =
        let mutable last = null
        use e = s.GetEnumerator()
        if e.MoveNext() then
            let first = Linked(e.Current)
            let mutable last = first
            while e.MoveNext() do
                let v = Linked e.Current
                link last v
                last <- v
            first
        else
            null

    let ofList (l : list<'a>) =
        ofSeq l

    let toSeq (l : Linked<'a>) =
        if l = null then Seq.empty
        else l :> seq<_>

    let toList (l : Linked<'a>) =
        if l = null then []
        else
            let current = ref l
            [ while !current <> null do
                yield current.Value.Value
                current := current.Value.Next
            ]

    let insertAfter (left : Linked<'a>) (v : 'a) =
        let n = Linked(v)
        if left <> null then
            n.Next <- left.Next
            left.Next <- n
            if left.Next <> null then
                left.Next.Prev <- n

        n.Prev <- left
        
    let insertBefore (right : Linked<'a>) (v : 'a) =
        let n = Linked(v)
        if right <> null then
            n.Prev <- right.Prev
            right.Prev <- n
            if right.Prev <> null then
                right.Prev.Next <- n

        n.Next <- right

    let value (l : Linked<'a>) = l.Value


type OrderedDictionary<'k, 'v when 'k : equality>() =
    let store = Dictionary<'k, Linked<'k * 'v>>()
    let mutable last : Linked<'k * 'v> = null
    let mutable first : Linked<'k * 'v> = null

    member x.Add(key : 'k, value : 'v) =
        let n = Linked(key, value)
        store.Add(key, n)
        n.Prev <- last
        if last <> null then
            last.Next <- n
        else
            first <- n
        last <- n

    member x.Remove(key : 'k) =
        match store.TryGetValue key with
            | (true, n) ->

                if n.Prev <> null then
                    n.Prev.Next <- n.Next
                else
                    first <- n.Next

                if n.Next <> null then
                    n.Next.Prev <- n.Prev
                else
                    last <- n.Prev

                n.Prev <- null
                n.Next <- null
                n.Value <- Unchecked.defaultof<_>
                store.Remove key |> ignore
                true
            | _ ->
                false

    member x.TryGetValue(key : 'k, [<Out>] value : byref<'v>) =
        match store.TryGetValue key with
            | (true, v) ->
                value <- snd v.Value
                true
            | _ ->
                false
     
    member x.Item
        with get(key : 'k) =
            store.[key].Value |> snd
        and set (key : 'k) (value : 'v) =
            match store.TryGetValue key with
                | (true, n) ->
                    n.Value <- (key, value)
                | _ ->
                    x.Add(key, value)  
     
    member x.TryFindNeighbours (key : 'k) =
        match store.TryGetValue key with
            | (true, v) ->
                let left = if v.Prev <> null then Some v.Prev.Value else None
                let right = if v.Next <> null then Some v.Next.Value else None
                left, Some v.Value, right
            | _ ->
                let left = if last <> null then Some last.Value else None
                left, None, None
         
    member x.TryFindNeighbourValues (key : 'k) =
        match store.TryGetValue key with
            | (true, v) ->
                let left = if v.Prev <> null then Some (snd v.Prev.Value) else None
                let right = if v.Next <> null then Some (snd v.Next.Value) else None
                left, Some (snd v.Value), right
            | _ ->
                let left = if last <> null then Some (snd last.Value) else None
                left, None, None
      
    member x.Alter(key : 'k, f : Option<'k * 'v> -> Option<'v> -> Option<'k * 'v> -> Option<'v>) =
        match store.TryGetValue key with
            | (true, v) ->
                let left = if v.Prev <> null then Some v.Prev.Value else None
                let right = if v.Next <> null then Some v.Next.Value else None
                match f left (Some <| snd v.Value) right with
                    | Some nv ->
                        if not <| System.Object.Equals(v.Value, nv) then
                            v.Value <- (fst v.Value, nv)
                        
                    | None ->
                        if v.Prev <> null then
                            v.Prev.Next <- v.Next
                        else
                            first <- v.Next

                        if v.Next <> null then
                            v.Next.Prev <- v.Prev
                        else
                            last <- v.Prev

                        v.Prev <- null
                        v.Next <- null
                        v.Value <- Unchecked.defaultof<_>
                        store.Remove(key) |> ignore
            | _ ->
                let left = if last <> null then Some last.Value else None
                match f left None None with
                    | Some v ->
                        let n = Linked(key, v)
                        store.Add(key, n)
                        n.Prev <- last
                        if last <> null then
                            last.Next <- n
                        else
                            first <- n
                        last <- n

                    | None ->
                        ()

    member x.Clear() =
        store.Clear()
        first <- null
        last <- null

    member x.Count = store.Count

    member x.First = first
    member x.Last = last

    member x.Items =
        first |> Linked.toSeq

    member x.Keys =
        first |> Seq.map fst

    member x.Values =
        first |> Seq.map snd

    interface IEnumerable with
        member x.GetEnumerator() = failwith ""

    interface IEnumerable<KeyValuePair<'k, 'v>> with
        member x.GetEnumerator() = failwith ""

    interface ICollection<KeyValuePair<'k, 'v>> with
        member x.Contains(KeyValue(k,v)) =
            match x.TryGetValue k with
                | (true, v') when System.Object.Equals(v, v') -> true
                | _ -> false

        member x.CopyTo(arr, index) =
            let mutable i = index
            for (k,v) in Linked.toSeq first do
                arr.[i] <- KeyValuePair(k,v)
                i <- i + 1

        member x.Add((KeyValue(k,v))) = x.Add(k, v)
        member x.Remove(KeyValue(k,v)) =
            let success = ref false
            x.Alter(k, fun _ o _ ->
                match o with
                    | Some o when System.Object.Equals(o,v) -> 
                        success := true
                        None
                    | _ -> o
            )
            !success
        member x.Clear() = x.Clear()
        member x.Count = x.Count
        member x.IsReadOnly = false

    interface IDictionary<'k, 'v> with
        member x.Add(k, v) = x.Add(k,v)
        member x.Remove(k) = x.Remove k
        member x.TryGetValue(k, v) = x.TryGetValue(k,&v)
        member x.Keys = List(x.Keys) :> ICollection<_>
        member x.Values = List(x.Values) :> ICollection<_>
        member x.ContainsKey k = store.ContainsKey k
        member x.Item
            with get (k : 'k) = x.[k]
            and set (k : 'k) (v : 'v) = x.[k] <- v

module OrderedDictionary =
    let empty<'k, 'v when 'k : equality> = OrderedDictionary<'k, 'v>()
    
    let add (key : 'k) (value : 'v) (d : OrderedDictionary<'k, 'v>) =
        d.[key] <- value
        
    let remove (key : 'k) (d : OrderedDictionary<'k, 'v>) =
        d.Remove key

    let alter (key : 'k) (valueFun : Option<'k * 'v> -> Option<'v> -> Option<'k * 'v> -> Option<'v>) (d : OrderedDictionary<'k, 'v>) =
        d.Alter(key, valueFun)

    let clear (d : OrderedDictionary<'k, 'v>) =
        d.Clear()

    let tryFind (key : 'k) (d : OrderedDictionary<'k, 'v>) =
        match d.TryGetValue key with
            | (true, v) -> Some v
            | _ -> None

    let toSeq (d : OrderedDictionary<'k, 'v>) =
        d.First |> Linked.toSeq
             
    let toList (d : OrderedDictionary<'k, 'v>) =
        d.First |> Linked.toList


type Trie<'k, 'v when 'k : equality>() =
    let children = OrderedDictionary<'k,Trie<'k, 'v>>()
    let values = OrderedDictionary<obj, Linked<'v>>()

    let mutable first : Linked<'v> = null
    let mutable last : Linked<'v>  = null

    member private x.FirstNode = first
    member private x.LastNode = last

    member x.First =
        if first = null then None
        else Some first

    member x.Last =
        if last = null then None
        else Some last

    member x.Clear() = 
        children.Clear()
        values.Clear()
        first <- null
        last <- null

    member x.Add(key : list<'k>, value : 'v, adjust : Option<'v> -> Option<'v> -> unit) =
        x.Add(key, null, null, fun l r -> adjust l r; value)

    member x.IsEmpty =
        children.Count = 0 && values.Count = 0

    member x.Add(key : list<'k>, value : 'v) =
        x.Add(key, value, fun _ _ -> ())

    member x.Remove(key : list<'k>, value : 'v) =
        x.Remove(key, value, fun _ _ -> ())

    member x.Remove(key : list<'k>, value : 'v, destroy : Option<'v> -> Option<'v> -> unit) =
        let success = ref false
        match key with
            | k::key ->
                children |> OrderedDictionary.alter k (fun l selfOpt r ->
                    match selfOpt with
                        | Some self ->
                            if self.Remove(key, value, destroy) then
                                success := true
                                if self = (snd children.First.Value) then
                                    first <- self.FirstNode
                                if self = (snd children.Last.Value) then
                                    last <- self.LastNode
                                
                                if self.IsEmpty then
                                    None
                                else
                                    Some self
                            else
                                Some self
                        | None ->
                            None
                )
                !success
            | [] ->
                values.Alter(value :> obj, fun l self r ->
                    match self with
                        | Some v ->
                            if v.Prev <> null then
                                v.Prev.Next <- v.Next

                            if v.Next <> null then
                                v.Next.Prev <- v.Prev

                            let leftVal = match v.Prev with | null -> None | v -> Some v.Value
                            let rightVal = match v.Next with | null -> None | v -> Some v.Value
                            destroy leftVal rightVal

                            success := true
                        | None -> ()

                    None
                )

                if values.Count = 0 then
                    first <- null
                    last <- null
                else
                    first <- values.First.Value |> snd 
                    last <- values.Last.Value |> snd

                !success

    member private x.Add(key : list<'k>, left : Linked<'v>, right : Linked<'v>, valueFun : Option<'v> -> Option<'v> -> 'v) =
        match key with
            | k::key ->
                children.Alter(k, fun l self r ->

                    let l' =
                        match l with
                            | Some (_,t) -> t.LastNode
                            | None -> left

                    let r' =
                        match r with
                            | Some (_,t) -> t.FirstNode
                            | None -> right

                    match self with
                        | Some self ->
                            self.Add(key, l', r', valueFun)

                            let lastTrie = snd children.Last.Value
                            if self = lastTrie then
                                last <- lastTrie.LastNode

                            Some self
                        | None ->
                            let n = Trie<'k, 'v>()
                            n.Add(key, l', r', valueFun)
                            last <- n.LastNode
                            match first with
                                | null -> first <- n.FirstNode
                                | _ -> ()

                            Some n
                )
            | [] ->
                let leftNode =
                    match values.Last with
                        | null -> left
                        | l -> l.Value |> snd

                let rightNode = right

                let realLeft =
                    match leftNode with
                        | null -> None
                        | r -> Some r.Value
                
                let realRight =
                    match rightNode with
                        | null -> None
                        | r -> Some r.Value

                let v = valueFun realLeft realRight
                let node = Linked(v)


                if leftNode <> null then
                    leftNode.Next <- node
                    node.Prev <- leftNode
   
                if rightNode <> null then
                    rightNode.Prev <- node
                    node.Next <- rightNode
      

                values.[v :> obj] <- node
                if first = null then
                    first <- node

                last <- node

                ()


    member x.Item
        with get(key : list<'k>) =
            match key with
                | k::key ->
                    children.[k].[key]
                | [] ->
                    values.Values |> Seq.map (Linked.value)

module Trie =
    
    let empty<'k, 'v when 'k : equality>  = Trie<'k, 'v>()

    let add (key : list<'k>) (value : 'v) (t : Trie<'k, 'v>) =
        t.Add(key, value)

    let remove (key : list<'k>) (value : 'v) (t : Trie<'k, 'v>) =
        t.Remove(key, value)

    let clear (t : Trie<'k, 'v>) =
        t.Clear()


module TrieTest =

    let test () = ()

//    let test() =
//        let r = System.Random()
//        let randomList _ =
//            List.init 3 (fun _ -> r.Next(100))
//        
//        let cnt = 1000000
//        let lists = List.init cnt randomList |> List.sort |> List.distinct
//        let t = Trie<int, list<int>>()
//        let cnt = lists |> List.length
//        printfn "count: %A" cnt
//        printfn "ours"
//        let sw = System.Diagnostics.Stopwatch()
//        sw.Start()
//        for l in lists do
//            t.Add(l, l, fun _ _ -> ())
//        sw.Stop()
//        printfn "done: %Ams" (sw.Elapsed.TotalMilliseconds / float cnt)
//
//
//        printfn "dict"
//        let sw = System.Diagnostics.Stopwatch()
//        sw.Start()
//        let dict = SortedDictionary()
//        for l in lists do
//            dict.[l] <- l
//        sw.Stop()
//        printfn "done: %Ams" (sw.Elapsed.TotalMilliseconds / float cnt)
//
//        let trieList = List.zip lists (t.First.Value |> Seq.toList)
//        for (ll,rl) in trieList do
//            if ll <> rl then
//                printfn "asdaskldmkofjas"
//
//        for l in lists do
//            if not <| t.Remove(l, l) then
//                printfn "esklfmklsdmf"
//
//        if t.First.IsSome then
//            printfn "asdasdasd"
//
//        if t.Last.IsSome then
//            printfn "asdasdasd"
//        
//
//
//
