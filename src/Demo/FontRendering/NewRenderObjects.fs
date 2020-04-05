module NewRenderObjects

open System
open Aardvark.Base
open FSharp.Data.Adaptive

type GeometryInstance =
    {
        mode                : IndexedGeometryMode
        uniforms            : Map<string, IAdaptiveValue>
        indexBuffer         : Option<BufferView>
        vertexBuffers       : Map<string, BufferView>
    }

type Tree<'a> =
    | Empty
    | Leaf of 'a
    | Node of 'a * list<Tree<'a>>



type GeometryLoadingMode =
    | Async         = 0b0001
    | Sync          = 0b0000



type GeometryInfo =
    | Single of GeometryInstance
    | Unordered of aset<GeometryInstance>
    | Ordered of alist<GeometryInstance>
    | Dynamic of aval<Tree<GeometryInstance>>

[<AllowNullLiteral>]    
type MNode<'a, 'b>(key : 'a, value : 'b) =
    let mutable children : list<MNode<'a, 'b>> = []
    let mutable value = value
    let mutable key = key
    
    member x.Key
        with get() = key
        and set c = key <- c
    member x.Value
        with get() = value
        and set c = value <- c
        
    member x.Children
        with get() = children
        and set c = children <- c
//
//type MTree<'a, 'b>(input : aval<Tree<'a>>, invoke : 'a -> 'b, revoke : 'b -> unit) =
//    inherit AdaptiveObject()
//
//    let trigger = AVal.init ()
//    let mutable state : MNode<'a, 'b> = null
//
//    let rec difference (old : Tree<'a>) (t : Tree<'a>) =
//        match old, t with
//            | Empty, Empty -> 
//                Empty
//            | Empty, Leaf load -> 
//                Leaf [Add load]
//            | Empty, Node(load, children) ->
//                Node([Add load], children |> List.map (difference Empty))
//
//            | Leaf o, Empty ->
//                Leaf [Rem o]
//
//            | Leaf o, Leaf n ->
//                if Unchecked.equals o n then
//                    Empty
//                else
//                    Leaf [Rem o; Add n]
//
//            | Leaf o, Node(n,nc) ->
//                let selfDelta = 
//                    if Unchecked.equals o n then []
//                    else [Rem o; Add n]
//
//                Node(selfDelta, nc |> List.map (difference Empty))
//
//            | Node(o, oc), Empty ->
//                Node([Rem o], oc |> List.map (fun o -> difference o Empty))
//
//            | Node(o, oc), Leaf n ->
//                let selfDelta =
//                    if Unchecked.equals o n then []
//                    else [Rem o; Add n]
//
//                Node(selfDelta, oc |> List.map (fun o -> difference o Empty))
//                
//            | Node(o, oc), Node(n, nc) ->
//                let selfDelta =
//                    if Unchecked.equals o n then []
//                    else [Rem o; Add n]
//                
//                Node(selfDelta, List.map2 difference oc nc)
//
//    let rec kill (deltas : ref<hdeltaset<'b>>) (m : MNode<'a, 'b>) =
//        match m with
//            | null -> ()
//            | m ->
//                revoke m.Value
//                for c in m.Children do kill c
//
//    let rec apply (deltas : ref<hdeltaset<'b>>) (c : MNode<'a, 'b>) (n : Tree<'a>) : MNode<'a, 'b> =
//        match c, n with
//            | null, Empty -> 
//                null
//
//            | null, Leaf n -> 
//                let r = invoke n
//                deltas := HDeltaSet.add (Add r) !deltas
//                MNode(n, r) 
//
//            | null, Node(v, cs) ->
//                let r = invoke v
//                deltas := HDeltaSet.add (Add r) !deltas
//
//                let n = MNode(v, r)
//                n.Children <- cs |> List.map (apply deltas null)
//                n
//
//            | o, Empty ->
//                kill deltas o
//                null
//
//            | o, Leaf n ->
//                if not (Unchecked.equals o.Key n) then
//                    revoke o.Value
//                    o.Key <- n
//                    o.Value <- invoke n
//
//                for c in o.Children do kill c
//                o.Children <- []
//                o
//
//            | o, Node(n,nc) ->
//                if not (Unchecked.equals o.Key n) then
//                    revoke o.Value
//                    o.Key <- n
//                    o.Value <- invoke n
//
//                match o.Children with
//                    | [] -> o.Children <- nc |> List.map (apply null)
//                    | oc -> o.Children <- List.map2 apply oc nc
//
//                o
//
//
//    member x.Compute(token : AdaptiveToken) =
//        x.EvaluateIfNeeded token [] (fun token ->
//            trigger.GetValue token
//
//            let n = input.GetValue token
//            state <- apply state n
//
//
//
//            []
//        )
//
