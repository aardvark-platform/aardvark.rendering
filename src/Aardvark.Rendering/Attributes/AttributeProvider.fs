namespace Aardvark.Rendering

open System
open Aardvark.Base

[<AllowNullLiteral>]
type IAttributeProvider =
    inherit IDisposable
    abstract member TryGetAttribute : name : Symbol -> Option<BufferView>

type AttributeProvider private() =

    static let empty =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.TryGetAttribute _ = None
        }

    static member Empty = empty

    static member union (l : IAttributeProvider) (r : IAttributeProvider) =
        { new IAttributeProvider with
            member x.Dispose() = l.Dispose(); r.Dispose()
            member x.TryGetAttribute(name : Symbol) =
                match l.TryGetAttribute(name) with
                | Some m -> Some m
                | None -> r.TryGetAttribute(name)
        }

    static member onDispose (callback : unit -> unit) (a : IAttributeProvider) =
        { new IAttributeProvider with
            member x.Dispose() = callback(); a.Dispose()
            member x.TryGetAttribute(name : Symbol) = a.TryGetAttribute name
        }

    // Symbol / BufferView
    static member ofSymDict (values : SymbolDict<BufferView>) =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.TryGetAttribute(name : Symbol) =
                match values.TryGetValue name with
                | (true, v) -> Some v
                | _ -> None
        }

    static member ofDict (values : Dict<Symbol, BufferView>) =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.TryGetAttribute(name : Symbol) =
                match values.TryGetValue name with
                | (true, v) -> Some v
                | _ -> None
        }

    static member ofDictionary (values : System.Collections.Generic.Dictionary<Symbol, BufferView>) =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.TryGetAttribute(name : Symbol) =
                match values.TryGetValue name with
                | (true, v) -> Some v
                | _ -> None
        }

    static member ofMap (values : Map<Symbol, BufferView>) =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.TryGetAttribute(name : Symbol) = Map.tryFind name values
        }

    static member ofList (values : list<Symbol * BufferView>) =
        values |> SymDict.ofList |> AttributeProvider.ofSymDict

    static member ofSeq (values : seq<Symbol * BufferView>) =
        values |> SymDict.ofSeq |> AttributeProvider.ofSymDict


    // Symbol / Array
    static member ofSymDict (values : SymbolDict<Array>) =
        values |> SymDict.map (fun _ v -> BufferView.ofArray v) |> AttributeProvider.ofSymDict

    static member ofDict (values : Dict<Symbol, Array>) =
        values |> Dict.map (fun _ v -> BufferView.ofArray v) |> AttributeProvider.ofDict

    static member ofDictionary (values : System.Collections.Generic.Dictionary<Symbol, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[k] <- BufferView.ofArray v
        AttributeProvider.ofSymDict d

    static member ofMap (values : Map<Symbol, Array>) =
        values |> Map.map (fun _ v -> BufferView.ofArray v) |> AttributeProvider.ofMap

    static member ofList (values : list<Symbol * Array>) =
        values |> List.map (fun (k,v) -> k,BufferView.ofArray v) |> AttributeProvider.ofList

    static member ofSeq (values : seq<Symbol * Array>) =
        values |> Seq.map (fun (k,v) -> k,BufferView.ofArray v) |> AttributeProvider.ofSeq


    // string / BufferView
    static member ofDict (values : Dict<string, BufferView>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofSymDict d

    static member ofDictionary (values : System.Collections.Generic.Dictionary<string, BufferView>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofSymDict d

    static member ofMap (values : Map<string, BufferView>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofSymDict d

    static member ofSeq (values : seq<string * BufferView>) =
        let d = SymbolDict<BufferView>()
        for (k,v) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofSymDict d

    static member ofList (values : list<string * BufferView>) =
        AttributeProvider.ofSeq values


    // string / Array
    static member ofDict (values : Dict<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofSymDict d

    static member ofDictionary (values : System.Collections.Generic.Dictionary<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofSymDict d

    static member ofMap (values : Map<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofSymDict d

    static member ofSeq (values : seq<string * Array>) =
        let d = SymbolDict<BufferView>()
        for (k,v) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofSymDict d

    static member ofList (values : list<string * Array>) =
        AttributeProvider.ofSeq values

    // special
    static member ofIndexedGeometry (g : IndexedGeometry) =
        AttributeProvider.ofSymDict g.IndexedAttributes