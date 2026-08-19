namespace Aardvark.SceneGraph

open Aardvark.Base

open System
open System.Collections.Generic
open System.Threading

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

    [<Struct; NoEquality; NoComparison>]
    type private LayoutKey<'T> =
        val Content : 'T
        val Size : int
        val Hash : int

        new (content, size, hash) =
            { Content = content; Size = size; Hash = hash }

    [<Sealed>]
    type private LayoutKeyComparer<'T>(contentComparer: IEqualityComparer<'T>) =
        static let isValueType = typeof<'T>.IsValueType

        interface IEqualityComparer<LayoutKey<'T>> with
            member _.Equals(a, b) =
                a.Size = b.Size &&
                    (not isValueType && Object.ReferenceEquals(a.Content, b.Content) ||
                     contentComparer.Equals(a.Content, b.Content))

            member _.GetHashCode(value) =
                value.Hash

    /// Manages reference-counted ranges whose identity consists of both content and requested size.
    /// Content equality follows the configured comparer; only equal content requested at the same size shares a range.
    type LayoutManager<'T when 'T : equality>(comparer: IEqualityComparer<'T>) =
        let manager = MemoryManager.createNop()
        let contentComparer : IEqualityComparer<'T> =
            if isNull comparer then EqualityComparer<'T>.Default
            else comparer
        let store = Dictionary<LayoutKey<'T>, managedptr>(LayoutKeyComparer contentComparer)
        let cnts = Dictionary<managedptr, struct(LayoutKey<'T> * ref<int>)>()

        // Keep the common repeated-sharing path direct while retaining the full dictionary for interleaved keys.
        let isValueType = typeof<'T>.IsValueType
        let mutable hasLast = false
        let mutable lastKey = Unchecked.defaultof<LayoutKey<'T>>
        let mutable lastValue = Unchecked.defaultof<managedptr>

        let contentEquals a b =
            not isValueType && Object.ReferenceEquals(a, b) || contentComparer.Equals(a, b)

        let setLast key value =
            hasLast <- true
            lastKey <- key
            lastValue <- value

        new () = LayoutManager<'T>(null)

        member x.Alloc(key: 'T, size: int) =
            let hash = (contentComparer.GetHashCode key * 397) ^^^ size
            if hasLast && lastKey.Hash = hash && lastKey.Size = size && contentEquals lastKey.Content key then
                let struct(_,r) = cnts.[lastValue]
                Interlocked.Increment &r.contents |> ignore
                lastValue
            else
                let key = LayoutKey(key, size, hash)
                match store.TryGetValue key with
                | true, v ->
                    setLast key v
                    let struct(_,r) = cnts.[v]
                    Interlocked.Increment &r.contents |> ignore
                    v
                | _ ->
                    let v = manager.Alloc (nativeint size)
                    let r = ref 1
                    cnts.[v] <- (key,r)
                    store.[key] <- v
                    setLast key v
                    v

        member x.TryAlloc(key: 'T, size: int) =
            let hash = (contentComparer.GetHashCode key * 397) ^^^ size
            if hasLast && lastKey.Hash = hash && lastKey.Size = size && contentEquals lastKey.Content key then
                let struct(_,r) = cnts.[lastValue]
                Interlocked.Increment &r.contents |> ignore
                false, lastValue
            else
                let key = LayoutKey(key, size, hash)
                match store.TryGetValue key with
                | true, v ->
                    setLast key v
                    let struct(_,r) = cnts.[v]
                    Interlocked.Increment &r.contents |> ignore
                    false, v
                | _ ->
                    let v = manager.Alloc (nativeint size)
                    let r = ref 1
                    cnts.[v] <- (key,r)
                    store.[key] <- v
                    setLast key v
                    true, v

        member x.Free(value: managedptr) =
            match cnts.TryGetValue value with
            | true, (k, r) ->
                if Interlocked.Decrement &r.contents = 0 then
                    manager.Free value
                    cnts.Remove value |> ignore
                    store.Remove k |> ignore
                    if hasLast && Object.ReferenceEquals(lastValue, value) then
                        hasLast <- false
                        lastKey <- Unchecked.defaultof<_>
                        lastValue <- Unchecked.defaultof<_>
            | _ ->
                ()

    type Range1ul with
        static member inline FromManagedPtr(ptr: managedptr, count: int) =
            if count > 0 then
                Range1ul.FromMinAndSize(uint64 ptr.Offset, uint64 count - 1UL)
            else
                Range1ul.Invalid