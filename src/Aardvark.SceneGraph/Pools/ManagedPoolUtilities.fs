﻿namespace Aardvark.SceneGraph

open Aardvark.Base

open System
open System.Collections
open System.Collections.Generic
open System.Threading
open FSharp.Data.Adaptive

module ``Managed Pool Internal Utilities`` =

    // For some reason writing the enumeration with SRTPs directly does not work. Somehow a problem with struct enumerators?
    // Also for IDictionary the code does not compile due to amiguities. Here we just implement the while loop for
    // all relevant types (lambdas get inlined thankfully).
    [<AbstractClass; Sealed>]
    type Enumerate() =
        static member inline While(dictionary: Dictionary<Symbol, 'T>, [<InlineIfLambda>] action: Symbol -> 'T -> unit, [<InlineIfLambda>] condition: unit -> bool) =
            let mutable e = dictionary.GetEnumerator()
            while condition() && e.MoveNext() do
                let kv = e.Current
                action kv.Key kv.Value

        static member inline While(dictionary: SymbolDict<'T>, [<InlineIfLambda>] action: Symbol -> 'T -> unit, [<InlineIfLambda>] condition: unit -> bool) =
            let mutable e = dictionary.GetEnumerator()
            while condition() && e.MoveNext() do
                let kv = e.Current
                action kv.Key kv.Value

        static member inline While(dictionary: Dict<Symbol, 'T>, [<InlineIfLambda>] action: Symbol -> 'T -> unit, [<InlineIfLambda>] condition: unit -> bool) =
            let mutable e = dictionary.GetEnumerator()
            while condition() && e.MoveNext() do
                let kv = e.Current
                action kv.Key kv.Value

        static member inline While(dictionary: IDictionary<Symbol, 'T>, [<InlineIfLambda>] action: Symbol -> 'T -> unit, [<InlineIfLambda>] condition: unit -> bool) =
            let mutable e = dictionary.GetEnumerator()
            while condition() && e.MoveNext() do
                let kv = e.Current
                action kv.Key kv.Value

    let inline private whileAux (_: ^Z) ([<InlineIfLambda>] condition: unit -> bool) ([<InlineIfLambda>] action: Symbol -> ^T -> unit) (dictionary: ^Dict) =
        ((^Z or ^Dict) : (static member While : ^Dict * (Symbol -> ^T -> unit) * (unit -> bool) -> unit) (dictionary, action, condition))

    let inline loopWhile ([<InlineIfLambda>] condition) ([<InlineIfLambda>] action) dictionary =
        whileAux Unchecked.defaultof<Enumerate> condition action dictionary

[<AutoOpen>]
module internal ManagedPoolUtilities =

    type private HashMapDictionary<'K, 'V>(map: HashMap<'K, 'V>) =
        let keys = map.GetKeys()
        let values = lazy (map.ToValueSeq() |> HashSet.ofSeq)
        let pairs = map.ToKeyValueSeq()
        let comparer = DefaultEqualityComparer<'V>.Instance

        member _.Map = map

        override _.Equals (obj: obj) =
            match obj with
            | :? HashMapDictionary<'K, 'V> as other -> map.Equals(other.Map)
            | :? HashMap<'K, 'V> as other -> map.Equals(other)
            | _ -> false

        override _.GetHashCode() = Unchecked.hash map

        interface IEquatable<HashMapDictionary<'K, 'V>> with
            member _.Equals(other) = map.Equals(other.Map)

        interface IEquatable<HashMap<'K, 'V>> with
            member _.Equals(other) = map.Equals(other)

        interface IDictionary<'K, 'V> with
            member _.Item with get sym = map.[sym]  and set _ _ = raise <| NotSupportedException()
            member _.Keys = keys
            member _.Values = values.Value
            member _.ContainsKey(key) = map.ContainsKey key
            member _.TryGetValue(key, value) =
                match map.TryFindV key with
                | ValueSome v -> value <- v; true
                | _ -> false
            member _.Add(_, _) = raise <| NotSupportedException()
            member _.Remove _ = raise <| NotSupportedException()

        interface ICollection<KeyValuePair<'K, 'V>> with
            member _.IsReadOnly = true
            member _.Count = map.Count
            member _.Contains(item) =
                match map.TryFindV item.Key with
                | ValueSome v -> comparer.Equals(item.Value, v)
                | _ -> false
            member _.CopyTo(array, startIndex) =
                pairs |> Seq.iteri (fun i kvp -> array.[startIndex + i] <- kvp)
            member _.Add _ = raise <| NotSupportedException()
            member _.Remove _ = raise <| NotSupportedException()
            member _.Clear() = raise <| NotSupportedException()

        interface IEnumerable with
            member _.GetEnumerator() = pairs.GetEnumerator()

        interface IEnumerable<KeyValuePair<'K, 'V>> with
            member _.GetEnumerator() = pairs.GetEnumerator()

    module HashMap =
        let asDictionary (map: HashMap<'K, 'V>) : IDictionary<'K, 'V> = HashMapDictionary map

    module Dictionary =
        open ``Managed Pool Internal Utilities``

        let inline getHashCode (dictionary: ^Dict) =
            let mutable hash = 17
            dictionary |> loopWhile (fun _ -> true) (fun key value ->
                &hash += HashCode.Combine(key.GetHashCode(), value.GetHashCode())
            )
            hash

        let inline equals<^T, ^Dict when ^Dict : (member Count : int)
                                    and  ^Dict : (member TryGetValue : Symbol * ^T byref -> bool)
                                    and  (Enumerate or ^Dict) : (static member While : ^Dict * (Symbol -> ^T -> unit) * (unit -> bool) -> unit)> (a: ^Dict) (b: ^Dict) =
            if a.Count <> b.Count then false
            else
                let mutable equal = true
                a |> loopWhile (fun _ -> equal) (fun key va ->
                    let mutable vb = Unchecked.defaultof<'T>
                    equal <- b.TryGetValue(key, &vb) && Unchecked.equals va vb
                )
                equal

        // NOTE: SymbolDict enumeration only guaranteed to be equal when created "identical" (order of inserts, resizes, capacity, ...)
        //       GetHashCode -> build sum of key and value hashes
        //       Equals -> lookup each

        // NOTE2: Implementing a SymbolDict (SymbolMap?) that keeps it entries sorted by hash might also be an option

        // NOTE3: Using IDictionary here to be flexible, tried to implement fast paths for Dict, SymbolDict etc. with SRTPs
        // but that somehow doesn't work with the struct enumerators. Would have to copy paste the code for all relevant types...
        [<AbstractClass; Sealed>]
        type StructuralComparer<'T when 'T : equality>() =

            static let instance =
                {
                    new IEqualityComparer<IDictionary<Symbol, 'T>> with
                        member _.Equals(a, b) =
                            if Object.ReferenceEquals(a, b) then true
                            else
                                match a, b with
                                | (:? Dictionary<Symbol, 'T> as a), (:? Dictionary<Symbol, 'T> as b) -> equals a b
                                | (:? SymbolDict<'T> as a), (:? SymbolDict<'T> as b) -> equals a b
                                | (:? Dict<Symbol, 'T> as a), (:? Dict<Symbol, 'T> as b) -> equals a b
                                | _ -> equals a b

                        member _.GetHashCode(d) =
                            match d with
                            | :? Dictionary<Symbol, 'T> as d -> getHashCode d
                            | :? SymbolDict<'T> as d -> getHashCode d
                            | :? Dict<Symbol, 'T> as d -> getHashCode d
                            | _ -> getHashCode d
                }

            static member Instance = instance

    type LayoutManager<'T when 'T : equality>(comparer: IEqualityComparer<'T>)=
        let manager = MemoryManager.createNop()
        let store = Dictionary<'T, managedptr>(comparer)
        let cnts = Dictionary<managedptr, struct('T * ref<int>)>()

        new () = LayoutManager<'T>(null)

        member x.Alloc(key: 'T, size: int) =
            match store.TryGetValue key with
            | true, v ->
                let struct(_,r) = cnts.[v]
                Interlocked.Increment &r.contents |> ignore
                v
            | _ ->
                let v = manager.Alloc (nativeint size)
                let r = ref 1
                cnts.[v] <- (key,r)
                store.[key] <- v
                v

        member x.TryAlloc(key: 'T, size: int) =
            match store.TryGetValue key with
            | true, v ->
                let struct(_,r) = cnts.[v]
                Interlocked.Increment &r.contents |> ignore
                false, v
            | _ ->
                let v = manager.Alloc (nativeint size)
                let r = ref 1
                cnts.[v] <- (key,r)
                store.[key] <- v
                true, v

        member x.Free(value: managedptr) =
            match cnts.TryGetValue value with
            | true, (k, r) ->
                if Interlocked.Decrement &r.contents = 0 then
                    manager.Free value
                    cnts.Remove value |> ignore
                    store.Remove k |> ignore
            | _ ->
                ()