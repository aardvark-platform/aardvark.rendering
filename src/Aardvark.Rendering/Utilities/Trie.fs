namespace Aardvark.Rendering

open Aardvark.Base
open System.Collections.Generic

type ILinked<'a when 'a :> ILinked<'a>> =
    abstract member Prev : Option<'a> with get, set
    abstract member Next : Option<'a> with get, set

[<AutoOpen>]
module private TrieDictionaryImplementation =
    open System.Runtime.CompilerServices
    open System.Runtime.InteropServices
    open System.Collections.Generic
    open System
    [<AbstractClass>]
    type TrieDictionary<'k, 'v>() =
        abstract AlterWithNeighbours : key : 'k * action : (voption<'v> -> voption<'v> -> voption<'v> -> voption<'v>) -> voption<'v> * voption<'v> * voption<'v>
        abstract TryRemove : key : 'k -> voption<'v>
        abstract Count : int
        abstract TryGetValue : key : 'k * [<Out>] value : byref<'v> -> bool

    type SortedTrieDict<'k, 'v>(cmp : IComparer<'k>) =
        inherit TrieDictionary<'k, 'v>()

        let store =
            SortedSetExt<struct ('k * 'v)> {
                new IComparer<struct ('k * 'v)> with
                    member x.Compare(struct(l,_), struct(r,_)) = cmp.Compare(l, r)
            }

        member x.Find(k : 'k) =
            let mutable l = Unchecked.defaultof<_>
            let mutable s = Unchecked.defaultof<_>
            let mutable r = Unchecked.defaultof<_>
            store.FindNeighbours (struct(k, Unchecked.defaultof<'v>), &l, &s, &r)

            let l =
                if l.HasValue then
                    let struct(_, v) = l.Value
                    ValueSome v
                else ValueNone

            let s =
                if s.HasValue then
                    let struct(_, v) = s.Value
                    ValueSome v
                else
                    ValueNone

            let r =
                if r.HasValue then
                    let struct(_, v) = r.Value
                    ValueSome v
                else ValueNone

            struct (l, s, r)

        override x.Count = store.Count

        override x.TryGetValue(key : 'k, [<Out>] value : byref<'v>) =
            let struct (_, s, _) = x.Find key
            match s with
            | ValueSome v ->
                value <- v
                true
            | ValueNone ->
                false

        override x.AlterWithNeighbours(key : 'k, action : voption<'v> -> voption<'v> -> voption<'v> -> voption<'v>) =
            let struct (l, s, r) = x.Find key

            match action l s r with
            | ValueSome v ->
                match s with
                | ValueSome v -> store.Remove(struct(key, v)) |> ignore
                | ValueNone -> ()

                store.Add(struct(key, v)) |> ignore
                l, ValueSome v, r
            | ValueNone ->
                match s with
                | ValueSome v -> store.Remove(struct(key, v)) |> ignore
                | ValueNone -> ()
                l, ValueNone, r

        override x.TryRemove(key : 'k) =
            let struct (l, s, r) = x.Find key
            match s with
            | ValueSome v ->
                store.Remove(struct(key, v)) |> ignore
                ValueSome v
            | ValueNone ->
                ValueNone

    type private Linked<'a> =
        {
            mutable Value : 'a
            mutable Prev : option<Linked<'a>>
            mutable Next : option<Linked<'a>>
        }

    type UnsortedTrieDict<'k, 'v>() =
        inherit TrieDictionary<'k, 'v>()

        let mutable first : option<Linked<'v>> = None
        let mutable last : option<Linked<'v>> = None

        let store = Dict<'k, Linked<'v>>()

        override x.AlterWithNeighbours(key : 'k, action : voption<'v> -> voption<'v> -> voption<'v> -> voption<'v>) =
            match store.TryGetValue key with
            | (true, node) ->
                let l =
                    match node.Prev with
                    | Some p -> ValueSome p.Value
                    | None -> ValueNone

                let r =
                    match node.Next with
                    | Some p -> ValueSome p.Value
                    | None -> ValueNone

                match action l (ValueSome node.Value) r with
                | ValueSome n ->
                    node.Value <- n
                    l, ValueSome n, r
                | ValueNone ->
                    match node.Prev with
                    | Some p -> p.Next <- node.Next
                    | None -> first <- node.Next

                    match node.Next with
                    | Some n -> n.Prev <- node.Prev
                    | None -> last <- node.Prev

                    node.Prev <- None
                    node.Next <- None
                    store.Remove key |> ignore

                    l, ValueNone, r
            | _ ->
                let l =
                    match last with
                    | Some last -> ValueSome last.Value
                    | None -> ValueNone

                match action l ValueNone ValueNone with
                | ValueSome v ->
                    let n = { Value = v; Prev = last; Next = None }
                    store.[key] <- n
                    match last with
                    | Some l -> l.Next <- Some n
                    | None -> first <- Some n

                    last <- Some n

                    l, ValueSome v, ValueNone
                | ValueNone ->
                    l, ValueNone, ValueNone

        override x.Count = store.Count

        override x.TryGetValue(key : 'k, [<Out>] value : byref<'v>) =
            match store.TryGetValue(key) with
            | (true, n) ->
                value <- n.Value
                true
            | _ ->
                false

        override x.TryRemove(key : 'k) =
            match store.TryRemove key with
            | (true, node) ->
                match node.Prev with
                | Some p -> p.Next <- node.Next
                | None -> first <- node.Next
                match node.Next with
                | Some n -> n.Prev <- node.Prev
                | None -> last <- node.Prev

                ValueSome node.Value
            | _ ->
                ValueNone




[<AllowNullLiteral>]
type TrieNode<'a when 'a :> ILinked<'a>>(parent : Trie<'a>, key : obj, level : int) =
    let mutable prev : TrieNode<'a> = null
    let mutable next : TrieNode<'a> = null

    let mutable value : Option<'a> = None
    let mutable firstChild : TrieNode<'a> = null
    let mutable lastChild  : TrieNode<'a> = null
    let children : TrieDictionary<obj, TrieNode<'a>> =
        match parent.GetComparer level with
        | Some cmp -> SortedTrieDict<obj, TrieNode<'a>>(cmp) :> _
        | None -> UnsortedTrieDict<obj, TrieNode<'a>>() :> _


    member x.Key = key

    member x.First =
        match firstChild with
        | null ->
            match value with
            | Some v -> Some v
            | None -> failwith "encountered empty Trie"
        | v -> v.First

    member x.Last =
        match value with
        | Some v -> Some v
        | None ->
            match lastChild with
            | null -> failwith "encountered empty Trie"
            | n -> n.Last

    member x.Prev
        with get() = prev
        and set p = prev <- p

    member x.Next
        with get() = next
        and set n = next <- n

    member x.Count =
        match value with
            | None -> children.Count
            | _ -> 1 + children.Count

    member x.IsEmpty =
        match value with
            | None -> children.Count = 0
            | _ -> false

    member internal x.Add(key : list<obj>, l : TrieRef<'a>, r : TrieRef<'a>, newValue : 'a) =
        match key with
            | [] ->
                match value with
                | Some o ->
                    let p = o.Prev
                    let n = o.Next

                    newValue.Prev <- p
                    newValue.Next <- n
                    match p with
                    | None -> parent.First <- Some newValue
                    | Some p -> p.Next <- Some newValue

                    match n with
                    | None -> parent.Last <- Some newValue
                    | Some n -> n.Prev <- Some newValue

                    o.Next <- None
                    o.Prev <- None
                    value <- Some newValue
                | None ->
                    let l = TrieRef<'a>.Last l
                    let r = TrieRef<'a>.First r

                    newValue.Prev <- l
                    newValue.Next <- r

                    match l with
                    | None -> parent.First <- Some newValue
                    | Some p -> p.Next <- Some newValue

                    match r with
                    | None -> parent.Last <- Some newValue
                    | Some n -> n.Prev <- Some newValue
                    value <- Some newValue

            | k :: rest ->
                match children.TryGetValue k with
                | (true, c) ->
                    let lInner =
                        match c.Prev with
                            | null ->
                                match value with
                                    | Some v -> Value v
                                    | None -> l
                            | p ->
                                Node p

                    let rInner =
                        match c.Next with
                        | null -> r
                        | n -> Node n

                    c.Add(rest, lInner, rInner, newValue)
                | _ ->
                    let lc, c, rc =
                        children.AlterWithNeighbours(k, fun l _ r ->
                            let c = TrieNode<'a>(parent, k, level + 1)

                            c.Prev <-
                                match l with
                                | ValueSome l ->
                                    l.Next <- c
                                    l
                                | _ ->
                                    firstChild <- c
                                    null

                            c.Next <-
                                match r with
                                | ValueSome r ->
                                    r.Prev <- c
                                    r
                                | ValueNone ->
                                    lastChild <- c
                                    null

                            ValueSome c
                        )

                    let c = c.Value


                    let lInner =
                        match lc with
                        | ValueSome l ->
                            match value with
                            | Some v -> Value v
                            | None -> Node l
                        | ValueNone ->
                            l

                    let rInner =
                        match rc with
                        | ValueSome r -> Node r
                        | ValueNone -> r

                    c.Add(rest, lInner, rInner, newValue)

    member x.Remove(key : list<obj>) =
        match key with
            | [] ->
                match value with
                    | Some v ->
                        let p = v.Prev
                        let n = v.Next

                        match p with
                            | Some p -> p.Next <- n
                            | None -> parent.First <- n

                        match n with
                            | Some n -> n.Prev <- p
                            | None -> parent.Last <- p

                        value <- None
                        true
                    | None ->
                        false
            | k :: rest ->
                match children.TryGetValue k with
                    | (true, c) ->
                        if c.Remove(rest) then
                            if c.IsEmpty then
                                let p = c.Prev
                                let n = c.Next

                                match p with
                                    | null -> firstChild <- n
                                    | p -> p.Next <- n

                                match n with
                                    | null -> lastChild <- p
                                    | n -> n.Prev <- p

                                children.TryRemove k |> ignore
                            true
                        else
                            false
                    | _ -> false

    member private x.Children =
        [
            let mutable c = firstChild
            while not (isNull c) do
                yield c
                c <- c.Next
        ]

    override x.ToString() =
        let self =
            match value with
                | Some v -> [sprintf "v:%A" v]
                | None -> []

        let children =
            x.Children |> List.map (fun n -> sprintf "%A: %s" n.Key (n.ToString()))

        self @ children |> String.concat "; " |> sprintf "[ %s ]"

and [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>] internal TrieRef<'a when 'a :> ILinked<'a>> =
    | Nothing
    | Value of 'a
    | Node of TrieNode<'a>

    static member Last (x : TrieRef<'a>) =
        match x with
            | TrieRef.Nothing -> None
            | TrieRef.Value a -> Some a
            | TrieRef.Node t -> t.Last

    static member First (x : TrieRef<'a>) =
        match x with
            | TrieRef.Nothing -> None
            | TrieRef.Value a -> Some a
            | TrieRef.Node t -> t.First

and [<StructuredFormatDisplay("{AsString}")>] Trie<'a when 'a :> ILinked<'a>>(comparers : option<IComparer<obj>>[]) =
    let mutable first : Option<'a> = None
    let mutable last  : Option<'a> = None
    let mutable root : TrieNode<'a> = null

    new() = Trie<'a>([||])

    member internal x.GetComparer(level : int) : option<IComparer<obj>> =
        if level >= 0 && level < comparers.Length then comparers.[level]
        else None

    member x.Clear() =
        root <- null
        first <- None
        last <- None

    member x.First
        with get() : Option<'a> = first
        and set (f : Option<'a>) = first <- f

    member x.Last
        with get() : Option<'a> = last
        and set (l : Option<'a>) = last <- l

    member x.Add(key : list<obj>, value : 'a) =
        match root with
            | null ->
                let r = TrieNode<'a>(x, null, 0)
                root <- r
                r.Add(key, Nothing, Nothing, value)
            | r ->
                r.Add(key, Nothing, Nothing, value)

    member x.Remove(key : list<obj>) =
        match root with
            | null -> false
            | r ->
                if r.Remove(key) then
                    if r.IsEmpty then root <- null
                    true
                else
                    false

    member private x.AsString = x.ToString()

    member x.TopLevelCount =
        match root with
        | null -> 0
        | root -> root.Count

    member x.Values =
        seq {
            let mutable c = first
            while Option.isSome c do
                let v = c.Value
                yield v
                c <- v.Next
        }

    override x.ToString() =
        match root with
            | null -> "{}"
            | r -> r.ToString()
