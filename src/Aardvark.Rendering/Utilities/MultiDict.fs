namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

type MultiDict<'K, 'V> = Dict<'K, HashSet<'V>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MultiDict =

    let empty<'K, 'V> : MultiDict<'K, 'V> = Dict.empty

    let find (key : 'K) (dict : MultiDict<'K, 'V>) =
        match dict |> Dict.tryFind key with
        | Some values -> values
        | _ -> HashSet.empty

    let add (key : 'K) (value : 'V) (dict : MultiDict<'K, 'V>) =
        let values = dict |> find key
        dict.[key] <- values |> HashSet.add value
        HashSet.isEmpty values

    let remove (key : 'K) (value : 'V) (dict : MultiDict<'K, 'V>) =
        let values = dict |> find key |> HashSet.remove value

        if HashSet.isEmpty values then
            dict |> Dict.remove key
        else
            dict.[key] <- values
            false