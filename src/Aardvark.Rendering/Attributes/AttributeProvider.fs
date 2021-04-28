namespace Aardvark.Rendering

open System
open Aardvark.Base

[<AllowNullLiteral>]
type IAttributeProvider =
    inherit IDisposable
    abstract member TryGetAttribute : name : Symbol -> Option<BufferView>
    abstract member All : seq<Symbol * BufferView>

type AttributeProvider private() =

    static let empty =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = Seq.empty
            member x.TryGetAttribute _ = None
        }

    static member Empty = empty

    static member onDispose (callback : unit -> unit) (a : IAttributeProvider) =
        { new IAttributeProvider with
            member x.Dispose() = callback(); a.Dispose()
            member x.All = a.All
            member x.TryGetAttribute(name : Symbol) = a.TryGetAttribute name
        }

    // Symbol / BufferView
    static member ofDict (values : SymbolDict<BufferView>) =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = values |> SymDict.toSeq
            member x.TryGetAttribute(name : Symbol) =
                match values.TryGetValue name with
                    | (true, v) -> Some v
                    | _ -> None
        }

    static member ofMap (values : Map<Symbol, BufferView>) =
        { new IAttributeProvider with
            member x.Dispose() = ()
            member x.All = values |> Map.toSeq
            member x.TryGetAttribute(name : Symbol) = Map.tryFind name values
        }

    static member ofList (values : list<Symbol * BufferView>) =
        values |> SymDict.ofList |> AttributeProvider.ofDict

    static member ofSeq (values : seq<Symbol * BufferView>) =
        values |> SymDict.ofSeq |> AttributeProvider.ofDict


    // Symbol / Array
    static member ofDict (values : SymbolDict<Array>) =
        values |> SymDict.map (fun _ v -> BufferView.ofArray v) |> AttributeProvider.ofDict

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
        AttributeProvider.ofDict d

    static member ofDict (values : System.Collections.Generic.Dictionary<string, BufferView>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofDict d

    static member ofMap (values : Map<string, BufferView>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofDict d

    static member ofSeq (values : seq<string * BufferView>) =
        let d = SymbolDict<BufferView>()
        for (k,v) in values do d.[Symbol.Create k] <- v
        AttributeProvider.ofDict d

    static member ofList (values : seq<string * BufferView>) =
        AttributeProvider.ofSeq values


    // string / Array
    static member ofDict (values : Dict<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofDict d

    static member ofDict (values : System.Collections.Generic.Dictionary<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofDict d

    static member ofMap (values : Map<string, Array>) =
        let d = SymbolDict<BufferView>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofDict d

    static member ofSeq (values : seq<string * Array>) =
        let d = SymbolDict<BufferView>()
        for (k,v) in values do d.[Symbol.Create k] <- BufferView.ofArray v
        AttributeProvider.ofDict d

    static member ofList (values : seq<string * Array>) =
        AttributeProvider.ofSeq values

    // special
    static member ofIndexedGeometry (g : IndexedGeometry) =
        AttributeProvider.ofDict g.IndexedAttributes