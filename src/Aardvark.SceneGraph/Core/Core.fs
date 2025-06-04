namespace Aardvark.SceneGraph

open System.Collections.Generic

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open FSharp.Data.Adaptive

type ISg = 
    interface end

type IApplicator =
    inherit ISg
    abstract member Child : aval<ISg>

type IGroup =
    inherit ISg
    abstract member Children : aset<ISg>

module Providers =

    type SingleUniformHolder(name : Symbol, value : IAdaptiveValue) =
        let value = ValueSome value

        interface IUniformProvider with
            member x.TryGetUniform (s,n) = if n = name then value else ValueNone
            member x.Dispose() = ()

    type ScopeDependentUniformHolder(values : Map<Symbol, Scope -> IAdaptiveValue>) =
        let cache = Dictionary<struct (Scope * Symbol), IAdaptiveValue voption>()

        interface IUniformProvider with
            member x.Dispose() = cache.Clear()
            member x.TryGetUniform(scope, name) =
                match cache.TryGetValue((scope, name)) with
                | (true, v) -> v
                | _ ->
                    let v =
                        match Map.tryFindV name values with
                        | ValueSome f -> f scope |> ValueSome
                        | ValueNone -> ValueNone
                    cache.[(scope, name)] <- v
                    v

        new(l) = new ScopeDependentUniformHolder(Map.ofList l)

    type RuntimeDependentUniformHolder(values : Map<Symbol, IRuntime -> IAdaptiveValue>) =
        let cache = Dictionary<struct (IRuntime * Symbol), IAdaptiveValue voption>()

        interface IUniformProvider with
            member x.Dispose() = cache.Clear()
            member x.TryGetUniform(scope, name) =
                let runtime : IRuntime = scope?Runtime

                match cache.TryGetValue((runtime, name)) with
                | (true, v) -> v
                | _ ->
                    let v =
                        match Map.tryFindV name values with
                        | ValueSome f -> f runtime |> ValueSome
                        | ValueNone -> ValueNone
                    cache.[(runtime, name)] <- v
                    v

        new(l) = new RuntimeDependentUniformHolder(Map.ofList l)


    type AttributeProvider(scope : Scope, attName : string) =
        let mutable scope = scope
        let mutable cache : Map<Symbol, BufferView> voption = ValueNone

        let getMap() =
            match cache with
            | ValueSome c -> c
            | ValueNone ->
                match scope.TryGetInheritedV attName with
                | ValueSome (:? Map<Symbol, BufferView> as map) ->
                    cache <- ValueSome map
                    map
                | _ ->
                    failwithf "could not get atttribute map %A for %A" attName scope

        interface IAttributeProvider with
            member x.Dispose() =
                cache <- ValueNone
                scope <- Ag.Scope.Root

            member x.All =
                getMap() |> Map.toSeq

            member x.TryGetAttribute(s : Symbol) =
                getMap() |> Map.tryFindV s


    type SimpleAttributeProvider(ig : IndexedGeometry) =
        let mutable cache = SymbolDict<BufferView>()

            
        member x.Dispose() = cache.Clear()

        member x.All =
            seq {
                for k in ig.IndexedAttributes.Keys do
                    match x.TryGetAttribute(k) with
                    | ValueSome att -> yield k, att
                    | _ -> ()
            }

        member x.TryGetAttribute(s :  Symbol) =
            match cache.TryGetValue s with
            | (true, v) -> ValueSome v
            | _ ->
                match ig.IndexedAttributes.TryGetValue s with
                | (true, att) ->
                    let v = BufferView att
                    cache.[s] <- v
                    ValueSome v
                | _ ->
                    ValueNone

        interface IAttributeProvider with
            member x.All = x.All
            member x.TryGetAttribute key = x.TryGetAttribute key
            member x.Dispose() = x.Dispose()



    type UniformProvider(scope : Scope,  uniforms : list<IUniformProvider>, attributeProviders : list<IAttributeProvider>) =
        let mutable scope = scope
        let mutable cache = SymbolDict<IAdaptiveValue>()

        
        member x.TryGetUniform(dynamicScope, s : Symbol) =
            
            let contains (s : Symbol) =
                let nullResource = 
                    attributeProviders |> List.tryPickV (fun p ->
                        match p.TryGetAttribute s with
                         | ValueSome v -> ValueSome (not v.IsSingleValue) //v.Buffer |> AVal.map (not << NullResources.isNullResource) |> Some
                         | ValueNone -> ValueNone
                    )
                match nullResource with
                | ValueSome v -> v
                | ValueNone -> false

            let str = s.ToString()
            match cache.TryGetValue s with
            | (true, m) ->
                ValueSome m

            | _ ->
                match uniforms |> List.tryPickV (fun u -> u.TryGetUniform (scope,s)) with
                | ValueSome u ->
                    let cs = u
                    cache.Add(s, cs)
                    ValueSome cs
                | ValueNone ->
                    match scope.TryGetAttributeValueV<IAdaptiveValue> (str) with
                    | ValueSome v ->
                        let cs = v
                        cache.Add(s, cs)
                        ValueSome cs
                    | _ ->
                        if str.StartsWith("Has") then
                            let baseName = str.Substring(3).ToSymbol()
                            let sourceUniform = x.TryGetUniform(dynamicScope, baseName)
                            match sourceUniform with
                            | ValueSome v ->
                                NullResources.isValidResourceAdaptive v :> IAdaptiveValue |> ValueSome
                            | ValueNone ->
                                baseName |> contains |> AVal.constant :> IAdaptiveValue |> ValueSome
                        else ValueNone

        interface IUniformProvider with

            member x.Dispose() =
                cache.Clear()
                scope <- Ag.Scope.Root

            member x.TryGetUniform(dynamicScope,s) = x.TryGetUniform(dynamicScope,s)


