#if INTERACTIVE
#else
namespace FingerTree2
#endif

open System
open System.Runtime.CompilerServices
open System.Collections
open System.Collections.Generic


type Measure<'a, 'm> = { f : 'a -> 'm; add : 'm -> 'm -> 'm; zero : 'm}

[<AutoOpen>]
module private FingerTreeNode =


    type Affix<'a, 'm> =
        | One of 'm * 'a
        | Two of 'm * 'a * 'a
        | Three of 'm * 'a * 'a * 'a
        | Four of 'm * 'a * 'a * 'a * 'a

    type Node<'a, 'm> =
        | Node2 of 'm * 'a * 'a
        | Node3 of 'm * 'a * 'a * 'a

    type Measure<'a, 'm> with
        member x.one (v : 'a) = One(x.f v, v)
        member x.two (a : 'a) (b : 'a) = Two(x.add (x.f a) (x.f b), a,b)
        member x.three (a : 'a) (b : 'a) (c : 'a) = Three(x.add (x.add (x.f a) (x.f b)) (x.f c), a,b,c)
        member x.four (a : 'a) (b : 'a) (c : 'a) (d : 'a) = Four(x.add (x.add (x.f a) (x.f b)) (x.add (x.f c) (x.f d)), a,b,c,d)

        member x.node2 (a : 'a) (b : 'a) = Node2(x.add (x.f a) (x.f b), a,b)
        member x.node3 (a : 'a) (b : 'a) (c : 'a) = Node3(x.add (x.add (x.f a) (x.f b)) (x.f c), a,b,c)

        member x.compute ([<ParamArray>] values : 'a[]) =
            values |> Array.fold (fun s e -> x.add s (x.f e)) x.zero

        member x.NodeMeasure =
            let measureNode (n : Node<'a, 'm>) =
                match n with
                    | Node2(m,_,_) -> m
                    | Node3(m,_,_,_) -> m

            { f = measureNode; add = x.add; zero = x.zero }

    module Affix =
        let single (ctx : Measure<'a, 'm>) (v : 'a) =
            One(ctx.f v ,v)

        let prepend (ctx : Measure<'a, 'm>) (v : 'a) (a : Affix<'a, 'm>) =
            match a with
                | One(m, x) -> Two(ctx.add (ctx.f v) m,v,x)
                | Two(m,x,y) -> Three(ctx.add (ctx.f v) m,v,x,y)
                | Three(m,x,y,z) -> Four(ctx.add (ctx.f v) m,v,x,y,z)
                | _ -> failwith "affix must have length 1 to 4"

        let append (ctx : Measure<'a, 'm>) (v : 'a) (a : Affix<'a, 'm>) =
            match a with
                | One(m, x) -> Two(ctx.add (ctx.f v) m,x,v)
                | Two(m,x,y) -> Three(ctx.add (ctx.f v) m,x,y,v)
                | Three(m,x,y,z) -> Four(ctx.add (ctx.f v) m,x,y,z,v)
                | _ -> failwith "affix must have length 1 to 4"

        let toNode(a : Affix<'a,'m>) =
            match a with
                | Two(m,a,b) -> Node2(m, a,b)
                | Three(m, a,b,c) -> Node3(m, a,b,c)
                | _ -> failwith "nodes must have length 2 or 3"

        let ofNode(n : Node<'a, 'm>) =
            match n with
                | Node2(m,a,b) -> Two(m,a,b)
                | Node3(m,a,b,c) -> Three(m,a,b,c)

        let viewl (a : Affix<'a, 'm>) =
            match a with
                | One(_,a) -> [a]
                | Two(_,a,b) -> [a;b]
                | Three(_,a,b,c) -> [a;b;c]
                | Four(_,a,b,c,d) -> [a;b;c;d]

        let viewr (a : Affix<'a, 'm>) =
            match a with
                | One(_,a) -> [a]
                | Two(_,a,b) -> [b;a]
                | Three(_,a,b,c) -> [c;b;a]
                | Four(_,a,b,c,d) -> [d;c;b;a]

    module Node =
        let toAffix(n : Node<'a,'m>) = Affix.ofNode n

        let fromAffix(a : Affix<'a, 'm>) = Affix.toNode a

        let viewl (a : Node<'a, 'm>) =
            match a with
                | Node2(_,a,b) -> [a;b]
                | Node3(_,a,b,c) -> [a;b;c]

        let viewr (a : Node<'a, 'm>) =
            match a with
                | Node2(_,a,b) -> [b;a]
                | Node3(_,a,b,c) -> [c;b;a]

    type Deep<'a, 'm> = { annotation : 'm; prefix : Affix<'a, 'm>; deeper : FingerTreeNode<Node<'a, 'm>, 'm>; suffix : Affix<'a, 'm> }

    and FingerTreeNode<'a, 'm> = 
        | Empty 
        | Single of 'a 
        | Deep of Deep<'a, 'm> with

            member x.ViewLeft : seq<'a> =
                seq {
                    match x with
                        | Empty -> ()
                        | Single (a) -> yield a
                        | Deep { prefix = prefix; deeper = deeper; suffix = suffix } ->
                            yield! Affix.viewl prefix
                            for b in deeper.ViewLeft do
                                yield! Node.viewl b
                            yield! Affix.viewl suffix
                }

            member x.ViewRight : seq<'a> =
                seq {
                    match x with
                        | Empty -> ()
                        | Single (a) -> yield a
                        | Deep { prefix = prefix; deeper = deeper; suffix = suffix } ->
                            yield! Affix.viewr suffix
                            for b in deeper.ViewRight do
                                yield! Node.viewr b
                            yield! Affix.viewr prefix
                }



    let rec prepend<'a, 'm> (ctx : Measure<'a, 'm>) (value : 'a) (node : FingerTreeNode<'a, 'm>) : FingerTreeNode<'a, 'm> =
        match node with
            | Empty -> 
                Single value

            | Single y -> 
                Deep { 
                    annotation = ctx.compute(value,y)
                    prefix = ctx.one value
                    deeper = Empty
                    suffix = ctx.one y
                }

            | Deep { annotation = annotation; prefix = Four(_,a,b,c,d); deeper = deeper; suffix = suffix } ->
                Deep { 
                    annotation = ctx.add annotation (ctx.f value) 
                    prefix = ctx.two value a
                    deeper = prepend ctx.NodeMeasure (ctx.node3 b c d) deeper
                    suffix = suffix 
                }

            | Deep deep ->

                Deep { 
                    annotation = ctx.add (ctx.f value) deep.annotation
                    prefix = Affix.prepend ctx value deep.prefix
                    deeper = deep.deeper 
                    suffix = deep.suffix 
                }

    let rec append<'a, 'm> (ctx : Measure<'a, 'm>) (value : 'a) (node : FingerTreeNode<'a, 'm>) : FingerTreeNode<'a, 'm> =
        match node with
            | Empty -> 
                Single value

            | Single y -> 
                Deep { 
                    annotation = ctx.compute(value,y)
                    prefix = ctx.one y
                    deeper = Empty
                    suffix = ctx.one value
                }

            | Deep { annotation = annotation; prefix = prefix; deeper = deeper; suffix = Four(_,a,b,c,d) } ->
                Deep { 
                    annotation = ctx.add annotation (ctx.f value) 
                    prefix = prefix
                    deeper = append ctx.NodeMeasure (ctx.node3 a b c) deeper
                    suffix = ctx.two d value 
                }

            | Deep deep ->

                Deep { 
                    annotation = ctx.add (ctx.f value) deep.annotation
                    prefix = deep.prefix
                    deeper = deep.deeper 
                    suffix = Affix.append ctx value deep.suffix
                }

[<StructuredFormatDisplay("{AsString}")>]
type FingerTree<'a, 'm> = private { ctx : Measure<'a, 'm>; root : FingerTreeNode<'a, 'm> } with

    member x.ViewLeft = x.root.ViewLeft
    member x.ViewRight = x.root.ViewRight

    member private x.AsString =
        x.ViewLeft |> Seq.toList |> sprintf "F %A"

    interface IEnumerable<'a> with
        member x.GetEnumerator() = x.ViewLeft.GetEnumerator()

    interface IEnumerable with
        member x.GetEnumerator() = (x.ViewLeft :> IEnumerable).GetEnumerator()


module FingerTree =
    
    let custom<'a, 'm> (m : Measure<'a, 'm>) = 
        { ctx = m; root = Empty }

    let inline empty (f : 'a -> 'm) : FingerTree<'a, 'm> = 
        custom { f = f; add = (+); zero = LanguagePrimitives.GenericZero }

    let prepend (value : 'a) (t : FingerTree<'a, 'm>) =
        { ctx = t.ctx; root = FingerTreeNode.prepend t.ctx value t.root }

    let append (value : 'a) (t : FingerTree<'a, 'm>) =
        { ctx = t.ctx; root = FingerTreeNode.append t.ctx value t.root }