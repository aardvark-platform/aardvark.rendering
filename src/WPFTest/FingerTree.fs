namespace Aardvark.Base

open System.Collections
open System.Collections.Generic
    
type private Node<'a> =
    | Branch2 of 'a * 'a
    | Branch3 of 'a * 'a * 'a with

    member x.Seq =
        seq {
            match x with
                | Branch2(a,b) -> yield a; yield b
                | Branch3(a,b,c) -> yield a; yield b; yield c
        }

    member x.SeqBack =
        seq {
            match x with
                | Branch2(a,b) -> yield b; yield a
                | Branch3(a,b,c) -> yield c; yield b; yield a
        }

[<StructuredFormatDisplay("{AsString}")>]
type private Affix<'a> =
    | One of 'a
    | Two of 'a * 'a
    | Three of 'a * 'a * 'a
    | Four of 'a * 'a * 'a * 'a with

    member private x.AsString =
        match x with
            | One(a) -> sprintf "[%A]" a
            | Two(a,b) -> sprintf "[%A;%A]" a b
            | Three(a,b,c) -> sprintf "[%A;%A;%A]" a b c
            | Four(a,b,c,d) -> sprintf "[%A;%A;%A;%A]" a b c d

    member x.Seq =
        seq {
            match x with
                | One(v) -> yield v
                | Two(a,b) -> yield a; yield b
                | Three(a,b,c) -> yield a; yield b; yield c
                | Four(a,b,c,d) -> yield a; yield b; yield c; yield d
        }

    member x.SeqBack =
        seq {
            match x with
                | One(v) -> yield v
                | Two(a,b) -> yield b; yield a
                | Three(a,b,c) -> yield c; yield b; yield a
                | Four(a,b,c,d) -> yield d; yield c; yield b; yield a
        }

[<AutoOpen>]
module private AffixPatterns =
    let (|AConsL|ALeafL|) (a : Affix<'a>) =
        match a with
            | One a -> ALeafL(a)
            | Two(a,b) -> AConsL(a, One b)
            | Three(a,b,c) -> AConsL(a, Two(b,c))
            | Four(a,b,c,d) -> AConsL(a, Three(b,c,d))

    let (|AConsR|ALeafR|) (a : Affix<'a>) =
        match a with
            | One a -> ALeafR(a)
            | Two(a,b) -> AConsR(One a, b)
            | Three(a,b,c) -> AConsR(Two(a,b), c)
            | Four(a,b,c,d) -> AConsR(Three(a,b,c), d)

module private Node =

    let map(f : 'a -> 'b) (a : Node<'a>) =
        match a with
            | Branch2(a,b) -> Branch2(f a, f b)
            | Branch3(a,b,c) -> Branch3(f a, f b, f c)

module private Affix =
    let single (v : 'a) =
        One v
        
    let map(f : 'a -> 'b) (a : Affix<'a>) =
        match a with
            | One(a) -> One(f a)
            | Two(a,b) -> Two(f a, f b)
            | Three(a,b,c) -> Three(f a, f b, f c)
            | Four(a,b,c,d) -> Four(f a, f b, f c, f d)

    let prepend (v : 'a) (a : Affix<'a>) =
        match a with
            | One(a) -> Two(v,a)
            | Two(a,b) -> Three(v,a,b)
            | Three(a,b,c) -> Four(v,a,b,c)
            | _ -> failwith "too large"

    let append (v : 'a) (a : Affix<'a>) =
        match a with
            | One(a) -> Two(a,v)
            | Two(a,b) -> Three(a,b,v)
            | Three(a,b,c) -> Four(a,b,c,v)
            | _ -> failwith "too large"

    let fromNode (node : Node<'a>) =
        match node with
            | Branch2(a,b) -> Two(a,b)
            | Branch3(a,b,c) -> Three(a,b,c)

type FingerTree<'a> = 
    private | Empty 
            | Single of 'a 
            | Deep of prefix : Affix<'a> * deeper : FingerTree<Node<'a>> * suffix : Affix<'a> with

        member x.ViewLeft : seq<'a> =
            seq {
                match x with
                    | Empty -> ()
                    | Single (a) -> yield a
                    | Deep(prefix, deeper, suffix) ->
                        yield! prefix.Seq
                        for b in deeper.ViewLeft do
                            yield! b.Seq
                        yield! suffix.Seq
            }

        member x.ViewRight : seq<'a> =
            seq {
                match x with
                    | Empty -> ()
                    | Single (a) -> yield a
                    | Deep(prefix, deeper, suffix) ->
                        yield! suffix.SeqBack
                        for b in deeper.ViewRight do
                            yield! b.SeqBack
                        yield! prefix.SeqBack
            }

        interface IEnumerable<'a> with
            member x.GetEnumerator() = x.ViewLeft.GetEnumerator()

        interface IEnumerable with
            member x.GetEnumerator() = (x.ViewLeft :> IEnumerable).GetEnumerator()



module FingerTree =

    type internal FingerTreeView<'a> =
        | Nil
        | View of 'a * FingerTree<'a>

    module private List =
        let rec takeLast (l : list<'a>) : list<'a> * 'a =
            match l with
                | [] -> failwith "list empty"
                | [x] -> [], x
                | x::xs -> 
                    let rest,last = takeLast xs
                    x::rest, last

    let rec toSeq<'a> (tree : FingerTree<'a>) : seq<'a> =
        tree.ViewLeft

    let rec toSeqBack<'a> (tree : FingerTree<'a>) : seq<'a> =
        tree.ViewRight

    let toList (tree : FingerTree<'a>) =
        tree |> toSeq |> Seq.toList

    let toListBack (tree : FingerTree<'a>) =
        tree |> toSeqBack |> Seq.toList

    let toArray (tree : FingerTree<'a>) =
        tree |> toSeq |> Seq.toArray

    let toArrayBack (tree : FingerTree<'a>) =
        tree |> toSeqBack |> Seq.toArray


    let rec internal viewl<'a> (t : FingerTree<'a>) : FingerTreeView<'a> =
        match t with
            | Empty -> Nil

            | Single v -> View(v, Empty)

            | Deep(ALeafL x,deeper,suffix) ->
                let rest =
                    match viewl deeper with
                        | View(node,rest') ->
                            Deep(Affix.fromNode node, rest', suffix)
                        | Nil ->
                            match suffix with
                                | One x -> Single x
                                | Two(x,y) -> Deep(One x, Empty, One y)
                                | Three(x,y,z) -> Deep(Two(x,y), Empty, One z)
                                | Four(x,y,z,w) -> Deep(Three(x,y,z), Empty, One w)

                View(x, rest)

            | Deep(AConsL(first,rest), deeper, suffix) ->
                View(first, Deep(rest, deeper, suffix))

    let rec internal viewr<'a> (t : FingerTree<'a>) : FingerTreeView<'a> =
        match t with
            | Empty -> Nil

            | Single v -> View(v, Empty)

            | Deep(prefix,deeper,ALeafR x) ->
                let rest =
                    match viewr deeper with
                        | View(node,rest') ->
                            Deep(prefix, rest', Affix.fromNode node)
                        | Nil ->
                            match prefix with
                                | One x -> Single x
                                | Two(x,y) -> Deep(One x, Empty, One y)
                                | Three(x,y,z) -> Deep(One(x), Empty, Two(y,z))
                                | Four(x,y,z,w) -> Deep(One(x), Empty, Three(y,z,w))

                View(x, rest)

            | Deep(prefix, deeper, AConsR(rest, last)) ->
                View(last, Deep(prefix, deeper, rest))
         


    let head (t : FingerTree<'a>) =
        match viewl t with
            | View(h,_) -> h
            | _ -> failwith "tree is empty"

    let last (t : FingerTree<'a>) =
        match viewr t with
            | View(l,_) -> l
            | _ -> failwith "tree is empty"

    let tail (t : FingerTree<'a>) =
        match viewl t with
            | View(_,t) -> t
            | _ -> failwith "tree is empty"

    let init (t : FingerTree<'a>) =
        match viewr t with
            | View(_,r) -> r
            | _ -> failwith "tree is empty"

    let isEmpty (t : FingerTree<'a>) =
        match viewl t with
            | Nil -> true
            | _ -> false

    let rec prepend<'a> (x : 'a) (tree : FingerTree<'a>) : FingerTree<'a> =
        match tree with
            | Empty -> Single x
            | Single y -> Deep(One(x), Empty, One(y))
            | Deep (Four(a,b,c,d), deeper, suffix) ->
                let node = Branch3(b,c,d)

                Deep(Two(x,a), prepend node deeper, suffix)
            | Deep(prefix, deeper, suffix) ->
                Deep(Affix.prepend x prefix, deeper, suffix)

    let rec append<'a> (x : 'a) (tree : FingerTree<'a>) : FingerTree<'a> =
        match tree with
            | Empty -> Single x
            | Single y -> Deep(One(y), Empty, One(x))
            | Deep (prefix, deeper, Four(a,b,c,d)) ->
                let node = Branch3(a,b,c)
                Deep(prefix, prepend node deeper, Two(d,x))
            | Deep(prefix, deeper, suffix) ->
                Deep(prefix, deeper, Affix.append x suffix)

    let ofList (s : list<'a>) =
        List.foldBack prepend s Empty

    let ofArray (s : 'a[]) =
        Array.foldBack prepend s Empty

    let ofSeq (s : seq<'a>) =
        s |> Seq.toList |> ofList

    [<AutoOpen>]
    module private ConcatUtils = 
        let inline (<!) (a : 'a) (t : FingerTree<'a>) = prepend<'a> a t
        let inline (>!) (t : FingerTree<'a>) (a : 'a) = append<'a> a t

        let rec nodes (l : list<'a>) : list<Node<'a>> =
            match l with
                | [] -> failwith "not enough elements for nodes"
                | [x] -> failwith "not enough elements for nodes"
                | [x; y] -> [Branch2(x,y)]
                | [x; y; z] -> [Branch3(x,y,z)]
                | x::y::rest -> Branch2(x,y) :: nodes rest

    let rec concatWithMiddle<'a> (l : FingerTree<'a>) (middle : list<'a>) (r : FingerTree<'a>) : FingerTree<'a> =
        match l,middle, r with
            | Empty,        [],         right       -> right
            | Empty,        x::xs,      right       -> x <! concatWithMiddle Empty xs right
            | Single y,     xs,         right       -> y <! concatWithMiddle Empty xs right
            | left,         [],         Empty       -> left
            | left,         xs,         Empty       -> let (init, last) = List.takeLast xs in concatWithMiddle left init Empty >! last
            | left,         xs,         Single y    -> concatWithMiddle left xs Empty >! y
                
            | Deep(pl, dl, sl), mid, Deep(pr, dr, sr) ->
                let mid' = [sl.Seq; mid :> _; pr.Seq] |> Seq.concat |> Seq.toList |> nodes
                let deeper' = concatWithMiddle dl mid' dr
                Deep(pl, deeper', sr)

    let concat2 (l : FingerTree<'a>) (r : FingerTree<'a>) =
        concatWithMiddle l [] r

    let concat (s : seq<FingerTree<'a>>) =
        s |> Seq.fold concat2 Empty

    let rec map<'a, 'b> (f : 'a -> 'b) (t : FingerTree<'a>) : FingerTree<'b> =
        match t with
            | Empty -> Empty
            | Single v -> Single (f v)
            | Deep(prefix, deeper, suffix) -> Deep(Affix.map f prefix, deeper |> map (Node.map f), Affix.map f suffix)

    let collect (f : 'a -> FingerTree<'b>) (t : FingerTree<'a>) =
        t |> map f |> concat

    let choose (f : 'a -> Option<'b>) (t : FingerTree<'a>) =
        collect (fun a -> match f a with | Some v -> Single v | None -> Empty) t

    let filter (f : 'a -> bool) (t : FingerTree<'a>) =
        collect (fun a -> if f a then Single a else Empty) t

    let fold (f : 's -> 'a -> 's) (seed : 's) (t : FingerTree<'a>) =
        let mutable res = seed
        for e in t.ViewLeft do
            res <- f res e
        res

    let foldBack (f : 'a -> 's -> 's) (seed : 's) (t : FingerTree<'a>) =
        let mutable res = seed
        for e in t.ViewRight do
            res <- f e res
        res

    let zip (l : FingerTree<'a>) (r : FingerTree<'b>) : FingerTree<'a * 'b> =
        Seq.zip l.ViewLeft r.ViewLeft |> ofSeq



[<AutoOpen>]
module FingerTreePatterns =
    open FingerTree

    let (|FingerFirst|_|) (t : FingerTree<'a>) =
        match viewl t with
            | View(h,t) -> Some(h,t)
            | _ -> None

    let (|FingerLast|_|) (t : FingerTree<'a>) =
        match viewr t with
            | View(h,t) -> Some(h,t)
            | _ -> None

    let (|FingerEmpty|_|) (t : FingerTree<'a>) =
        match viewl t with
            | Nil -> Some()
            | _ -> None
