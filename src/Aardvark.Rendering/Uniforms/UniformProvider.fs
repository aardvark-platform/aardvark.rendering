namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

[<AllowNullLiteral>]
type IUniformProvider =
    inherit IDisposable
    abstract member TryGetUniform : scope : Ag.Scope * name : Symbol -> Option<IAdaptiveValue>

type UniformProvider private() =

    static let empty =
        { new IUniformProvider with
            member x.Dispose() = ()
            member x.TryGetUniform(_,_) = None
        }

    static member Empty = empty

    static member union (l : IUniformProvider) (r : IUniformProvider) =
        { new IUniformProvider with
            member x.Dispose() = l.Dispose(); r.Dispose()
            member x.TryGetUniform(scope : Ag.Scope, name : Symbol) =
                match l.TryGetUniform(scope, name) with
                    | Some m -> Some m
                    | None -> r.TryGetUniform(scope, name)

        }

    static member ofDict (values : SymbolDict<IAdaptiveValue>) =
        { new IUniformProvider with
            member x.Dispose() = ()
            member x.TryGetUniform(scope : Ag.Scope, name : Symbol) =
                match values.TryGetValue name with
                    | (true, v) -> Some v
                    | _ -> None
        }

    static member ofMap (values : Map<Symbol, IAdaptiveValue>) =
        { new IUniformProvider with
            member x.Dispose() = ()
            member x.TryGetUniform(scope : Ag.Scope, name : Symbol) = Map.tryFind name values
        }

    static member ofList (values : list<Symbol * IAdaptiveValue>) =
        values |> Map.ofList |> UniformProvider.ofMap

    static member ofSeq (values : seq<Symbol * IAdaptiveValue>) =
        values |> Map.ofSeq |> UniformProvider.ofMap


    static member ofDict (values : Dict<string, IAdaptiveValue>) =
        let d = SymbolDict<IAdaptiveValue>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        UniformProvider.ofDict d

    static member ofDict (values : System.Collections.Generic.Dictionary<string, IAdaptiveValue>) =
        let d = SymbolDict<IAdaptiveValue>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        UniformProvider.ofDict d

    static member ofMap (values : Map<string, IAdaptiveValue>) =
        let d = SymbolDict<IAdaptiveValue>()
        for (KeyValue(k,v)) in values do d.[Symbol.Create k] <- v
        UniformProvider.ofDict d

    static member ofList (values : list<string * IAdaptiveValue>) =
        UniformProvider.ofSeq values

    static member ofSeq (values : seq<string * IAdaptiveValue>) =
        let d = SymbolDict<IAdaptiveValue>()
        for (k,v) in values do d.[Symbol.Create k] <- v
        UniformProvider.ofDict d