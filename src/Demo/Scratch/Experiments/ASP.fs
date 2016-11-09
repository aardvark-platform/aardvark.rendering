namespace ASP

open Aardvark.Base
open Aardvark.Base.Incremental


module ASP =
    open System.Collections.Generic
    let a = Aardvark.SceneGraph.IO.Loader.Empty
    
    
    type IntersectByReader<'a, 'b, 'c, 'r when 'c : equality>(l : IReader<'a>, r : IReader<'b>, pa : 'a -> 'c, pb : 'b -> 'c, f : 'a -> 'b -> Option<'r>) =
        inherit ASetReaders.AbstractReader<'r>()

        let a = Dict<'c, HashSet<'a>>()
        let b = Dict<'c, HashSet<'b>>()

        let addA (c : 'c) (v : 'a) =
            match a.TryGetValue c with
                | (true, set) -> set.Add v |> ignore
                | _ ->
                    let set = HashSet()
                    set.Add v |> ignore
                    a.[c] <- set

        let addB (c : 'c) (v : 'b) =
            match b.TryGetValue c with
                | (true, set) -> set.Add v |> ignore
                | _ ->
                    let set = HashSet()
                    set.Add v |> ignore
                    b.[c] <- set

        let remA (c : 'c) (v : 'a) =
            match a.TryGetValue c with
                | (true, set) -> 
                    set.Remove v |> ignore
                    if set.Count = 0 then
                        a.Remove c |> ignore

                | _ -> ()

        let remB (c : 'c) (v : 'b) =
            match b.TryGetValue c with
                | (true, set) -> 
                    set.Remove v |> ignore
                    if set.Count = 0 then
                        b.Remove c |> ignore

                | _ -> ()

        let allA (c : 'c) =
            match a.TryGetValue c with
                | (true, a) -> a :> seq<_>
                | _ -> Seq.empty

        let allB (c : 'c) =
            match b.TryGetValue c with
                | (true, b) -> b :> seq<_>
                | _ -> Seq.empty

        override x.ComputeDelta() =
            let l = l.GetDelta(x)
            let r = r.GetDelta(x)


            let result = List<Delta<'r>>()

            for a in l do
                match a with
                    | Add a -> 
                        let c = pa a
                        addA c a

                        let bb = allB c
                        for b in bb do
                            match f a b with
                                | Some r -> result.Add(Add r)
                                | None -> ()
                    | Rem a ->
                        let c = pa a
                        remA c a

                        let bb = allB c
                        for b in bb do
                            match f a b with
                                | Some r -> result.Add(Rem r)
                                | None -> ()


            for b in r do
                match b with
                    | Add b -> 
                        let c = pb b
                        addB c b

                        let aa = allA c
                        for a in aa do
                            match f a b with
                                | Some r -> result.Add(Add r)
                                | None -> ()
                    | Rem b ->
                        let c = pb b
                        remB c b

                        let aa = allA c
                        for a in aa do
                            match f a b with
                                | Some r -> result.Add(Rem r)
                                | None -> ()

            result |> CSharpList.toList

        override x.Release() =
            l.Dispose()
            r.Dispose()


    module ASet = 
        let intersectBy (a : Lazy<aset<'a>>) (fa : 'a -> 'c)  (b : Lazy<aset<'b>>) (fb : 'b -> 'c) (r : 'a -> 'b -> Option<'r>) =
            ASet.AdaptiveSet(fun () -> new IntersectByReader<_,_,_,_>(a.Value.GetReader(),b.Value.GetReader(),fa,fb,r) :> IReader<_>) :> aset<_>



    type Fact<'a> = { all : aset<'a>; definition : cset<aset<'a>> }

    module Fact =
        let ofDef (def : cset<aset<'a>>) =
            { all = ASet.union def; definition = def }

        let ofList (def : list<aset<'a>>) =
            let def = CSet.ofList def
            { all = ASet.union def; definition = def }

        let inline all (f : Fact<'a>) = f.all
        let inline definition (f : Fact<'a>) = f.definition

    let parent =
        Fact.ofList [
            // parent(1,2).
            ASet.single (1,2)

            // parent(2,3).
            ASet.single (2,3)

            // parent(1,4).
            ASet.single (1,4)
        ]

    let child =
        Fact.ofList [
            // child(A,B) :- parent(B, A).
            parent.all |> ASet.map (fun (a,b) -> (b,a))
        ]

    let sibling =
        Fact.ofList [
            // sibling(A,B) :- parent(X,A), parent(X,B), dif(A,B).
            ASet.intersectBy 
                (lazy parent.all) fst
                (lazy parent.all) fst 
                (fun (x0,a) (x1,b) -> if a <> b then Some (a,b) else None)
        ]


    let test() =
        let m = sibling |> Fact.all |> ASet.toMod
        m |> Mod.force |> Seq.toList |> printfn "anc: %A"
        printfn "done"
