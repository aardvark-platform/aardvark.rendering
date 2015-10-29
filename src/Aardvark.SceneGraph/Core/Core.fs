namespace Aardvark.SceneGraph

open System.Runtime.InteropServices
open System.Collections.Generic

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag

type ISg = 
    interface end

type IApplicator =
    inherit ISg
    abstract member Child : IMod<ISg>

type IGroup =
    inherit ISg
    abstract member Children : aset<ISg>

module private Providers =

    type SimpleUniformHolder(values : Map<Symbol, IMod>) =
        interface IUniformProvider with
            member x.TryGetUniform (scope,name) = Map.tryFind name values
            member x.Dispose() = ()

        new (l : list<Symbol * IMod>) = new SimpleUniformHolder(Map.ofList l)

    type ScopeDependentUniformHolder(values : Map<Symbol, Scope -> IMod>) =
        let cache = Dictionary<Scope * Symbol, Option<IMod>>()

        interface IUniformProvider with
            member x.Dispose () = cache.Clear()
            member x.TryGetUniform (scope,name) = 
                match cache.TryGetValue((scope,name)) with
                    | (true, v) -> v
                    | _ ->
                        let v =
                            match Map.tryFind name values with
                                | Some f -> f scope |> Some
                                | None -> None
                        cache.[(scope,name)] <- v
                        v

        new(l) = new ScopeDependentUniformHolder(Map.ofList l)


    type AttributeProvider(scope : Scope, attName : string) =
        let mutable scope = scope
        let mutable cache : Option<Map<Symbol, BufferView>> = None

        let getMap() =
            match cache with
                | Some c -> c
                | None -> 
                    match scope.TryGetAttributeValue attName with
                        | Success map ->
                            cache <- Some map
                            map
                        | Error e ->
                            failwithf "could not get atttribute map %A for %A" attName scope

        interface IAttributeProvider with

            member x.Dispose() =
                cache <- None
                scope <- emptyScope

            member x.All =
                getMap() |> Map.toSeq

            member x.TryGetAttribute(s : Symbol) =
                getMap() |> Map.tryFind s


    type SimpleAttributeProvider(ig : IndexedGeometry) =
        let mutable cache = SymbolDict<BufferView>()

            
        member x.Dispose() = cache.Clear()

        member x.TryGetAttribute(s :  Symbol) =
            match cache.TryGetValue s with
                | (true, v) -> Some v
                | _ ->
                    match ig.IndexedAttributes.TryGetValue s with
                        | (true, att) -> 
                            let t = att.GetType().GetElementType()
                            let v = BufferView(Mod.constant (ArrayBuffer att :> IBuffer), t)

                            cache.[s] <- v
                            Some v
                        | _ -> 
                            None
                                
        member x.All =
            seq {
                for k in ig.IndexedAttributes.Keys do
                    match x.TryGetAttribute(k) with
                        | Some att -> yield k, att
                        | _ -> ()
            }
             
        interface IAttributeProvider with
            member x.TryGetAttribute key = x.TryGetAttribute key
            member x.All = x.All 
            member x.Dispose() = x.Dispose()



    type UniformProvider(scope : Scope,  uniforms : list<IUniformProvider>, attributeProviders : list<IAttributeProvider>) =
        let mutable scope = scope
        let mutable cache = SymbolDict<IMod>()

        
        member x.TryGetUniform(dynamicScope, s : Symbol) =
            
            let contains (s : Symbol) =
                attributeProviders |> List.exists (fun p ->
                    match p.TryGetAttribute s with
                     | Some v -> true
                     | None -> false
                )

            let str = s.ToString()
            match cache.TryGetValue s with
                | (true, m) -> 
                    Some m

                | _ -> 
                    match uniforms |> List.tryPick (fun u -> u.TryGetUniform (scope,s)) with
                        | Some u -> 
                            let cs = u
                            cache.Add(s, cs)
                            Some cs
                        | None -> 
                            match scope.TryGetAttributeValue (str) with
                                | Success (v : IMod) -> 
                                    let cs = v
                                    cache.Add(s, cs)
                                    Some cs
                                | _ ->
                                    if str.StartsWith("Has") then
                                        let baseName = str.Substring(3).ToSymbol()
                                        let sourceUniform = x.TryGetUniform(dynamicScope, baseName)
                                        match sourceUniform with    
                                            | Some v -> Some (Mod.constant true :> IMod)
                                            | None -> 
                                                let inAttributes = contains baseName
                                                Some (Mod.constant inAttributes :> IMod)
                                    else None

        interface IUniformProvider with

            member x.Dispose() =
                cache.Clear()
                scope <- emptyScope

            member x.TryGetUniform(dynamicScope,s) = x.TryGetUniform(dynamicScope,s)


