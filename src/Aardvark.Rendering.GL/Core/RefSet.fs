namespace Aardvark.Rendering.GL

open System.Collections
open System.Collections.Generic

type RefSet<'a when 'a : comparison> =
    private { store : Map<'a, int> }

    interface IEnumerable with
        member x.GetEnumerator() =
            (Map.toSeq x.store |> Seq.map fst).GetEnumerator() :> IEnumerator

    interface IEnumerable<'a> with
        member x.GetEnumerator() =
            (Map.toSeq x.store |> Seq.map fst).GetEnumerator()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RefSet =

    type private EmptyImpl<'a when 'a : comparison>() =
        static let instance = { store = Map.empty<'a, int> }
        static member Instance = instance

    let empty<'a when 'a : comparison> = EmptyImpl<'a>.Instance

    let ofSeq (s : seq<'a>) =
        { store = s |> Seq.countBy id |> Map.ofSeq }

    let ofList (l : list<'a>) =
        match l with
            | [] -> empty
            | l -> ofSeq l

    let ofArray (a : 'a[]) =
        if a.Length = 0 then empty
        else ofSeq a

    let ofSet (set : Set<'a>) =
        { store = set |> Seq.map (fun v -> v,1) |> Map.ofSeq }

    let toSeq (set : RefSet<'a>) =
        set.store |> Map.toSeq |> Seq.map fst

    let toList (set : RefSet<'a>) =
        set.store |> Map.toList |> List.map fst

    let toArray (set : RefSet<'a>) =
        set |> toSeq |> Seq.toArray

    let toSet (set : RefSet<'a>) =
        set.store |> Map.toSeq |> Seq.map fst |> Set.ofSeq


    let add (v : 'a) (set : RefSet<'a>) =
        match Map.tryFind v set.store with
            | Some r -> { store = set.store |> Map.add v (r + 1) }
            | None -> { store = set.store |> Map.add v 1}

    let remove (v : 'a) (set : RefSet<'a>) =
        match Map.tryFind v set.store with
            | Some r -> 
                if r > 1 then { store = set.store |> Map.add v (r - 1) }
                else { store = set.store |> Map.remove v }
            | None -> set

    let contains (v : 'a) (set : RefSet<'a>) =
        Map.containsKey v set.store