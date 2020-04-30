namespace Aardvark.Base

open System.Collections.Generic

type ILinked<'a when 'a :> ILinked<'a>> =
    abstract member Prev : Option<'a> with get, set
    abstract member Next : Option<'a> with get, set

// TODO: Remove whole file once this is moved to Base
[<AllowNullLiteral>]
type private TrieNode<'a when 'a :> ILinked<'a>> (parent : Trie<'a>, key : obj) =
    let mutable prev : TrieNode<'a> = null
    let mutable next : TrieNode<'a> = null

    let mutable value : Option<'a> = None
    let mutable firstChild : TrieNode<'a> = null
    let mutable lastChild  : TrieNode<'a> = null
    let children = Dictionary<obj, TrieNode<'a>>()

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
                        let c = TrieNode<'a>(parent, k)
                        children.[k] <- c

                        let lc = lastChild
                        c.Prev <- lc
                        c.Next <- null

                        match lc with
                            | null -> firstChild <- c
                            | l -> l.Next <- c

                        lastChild <- c

                        let lInner =
                            match lc with
                                | null ->
                                    match value with
                                        | Some v -> Value v
                                        | None -> l
                                | l ->
                                    Node l

                        c.Add(rest, lInner, r, newValue)

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

                                children.Remove k |> ignore
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

and [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>] private TrieRef<'a when 'a :> ILinked<'a>> =
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

and [<StructuredFormatDisplay("{AsString}")>] Trie<'a when 'a :> ILinked<'a>>() =
    let mutable first : Option<'a> = None
    let mutable last  : Option<'a> = None
    let mutable root : TrieNode<'a> = null

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
                let r = TrieNode<'a>(x, null)
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
