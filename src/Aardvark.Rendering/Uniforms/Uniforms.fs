namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open System.Collections.Generic

module TrafoOperators =

    module Trafo3d =
        let inverse : aval<Trafo3d> -> aval<Trafo3d> =
            UnaryCache<aval<Trafo3d>, aval<Trafo3d>>(AVal.map (fun t -> t.Inverse)).Invoke

        let normalMatrix : aval<Trafo3d> -> aval<M33d> =
            UnaryCache<aval<Trafo3d>, aval<M33d>>(AVal.map (fun t -> t.Backward.Transposed.UpperLeftM33())).Invoke

        let inverseArr : aval<Trafo3d[]> -> aval<Trafo3d[]> =
            UnaryCache<aval<Trafo3d[]>, aval<Trafo3d[]>>(AVal.map (Array.map (fun t -> t.Inverse))).Invoke

        let normalMatrixArr : aval<Trafo3d[]> -> aval<M33d[]> =
            UnaryCache<aval<Trafo3d[]>, aval<M33d[]>>(AVal.map (Array.map (fun t -> t.Backward.Transposed.UpperLeftM33()))).Invoke

    let (<*>) : aval<Trafo3d> -> aval<Trafo3d> -> aval<Trafo3d> =
        BinaryCache<aval<Trafo3d>, aval<Trafo3d>, aval<Trafo3d>>(AVal.map2 (*)).Invoke

    let (<.*.>) : aval<Trafo3d[]> -> aval<Trafo3d[]> -> aval<Trafo3d[]> =
        BinaryCache<aval<Trafo3d[]>, aval<Trafo3d[]>, aval<Trafo3d[]>>(AVal.map2 (Array.map2 (*))).Invoke

    let (<*.>) : aval<Trafo3d> -> aval<Trafo3d[]> -> aval<Trafo3d[]> =
        BinaryCache<aval<Trafo3d>, aval<Trafo3d[]>, aval<Trafo3d[]>>(AVal.map2 (fun l r -> r |> Array.map (fun r -> l * r ))).Invoke

    let (<.*>) : aval<Trafo3d[]> -> aval<Trafo3d> -> aval<Trafo3d[]> =
        BinaryCache<aval<Trafo3d[]>, aval<Trafo3d>, aval<Trafo3d[]>>(AVal.map2 (fun l r -> l |> Array.map (fun l -> l * r ))).Invoke


module Uniforms =
    open TrafoOperators

    module Patterns =
        open System

        let (|NullUniform|_|) (value : IAdaptiveValue option) =
            match value with
            | Some value when Object.ReferenceEquals(value, null) ->
                Some ()
            | _ ->
                None

        let (|CastUniformResource|_|) (value : IAdaptiveValue option) =
            match value with
            | Some value when typeof<'T>.IsAssignableFrom value.ContentType ->
                Some (AdaptiveResource.cast<'T> value)
            | _ ->
                None

    [<AutoOpen>]
    module private Helpers =
        exception NotFoundException of string

        type Trafo =
            | Single of aval<Trafo3d>
            | Layered of aval<Trafo3d[]>

            member x.Inverse =
                match x with
                    | Single v -> Trafo3d.inverse v |> Single
                    | Layered v -> Trafo3d.inverseArr v |> Layered

            member x.Value =
                match x with
                    | Single v -> v :> IAdaptiveValue
                    | Layered v -> v :> IAdaptiveValue

            member x.NormalMatrix =
                match x with
                    | Single v -> Trafo3d.normalMatrix v :> IAdaptiveValue
                    | Layered v -> Trafo3d.normalMatrixArr v :> IAdaptiveValue


        let (<*>) (l : Trafo) (r : Trafo) : Trafo =
            match l, r with
                | Single l, Single r -> l <*> r |> Single
                | Layered l, Single r -> l <.*> r |> Layered
                | Single l, Layered r -> l <*.> r |> Layered
                | Layered l, Layered r -> l <.*.> r |> Layered


        let inline (?) (p : IUniformProvider) (name : string) : Trafo =
            match p.TryGetUniform(Ag.Scope.Root, Symbol.Create name) with
                | Some (:? aval<Trafo3d> as m) -> Single m
                | Some (:? aval<Trafo3d[]> as m) -> Layered m
                | _ -> raise <| NotFoundException name

    let private table : Dictionary<string, IUniformProvider -> IAdaptiveValue> =

        Dictionary.ofList [
            "ModelTrafoInv",            fun u -> u?ModelTrafo.Inverse.Value
            "ViewTrafoInv",             fun u -> u?ViewTrafo.Inverse.Value
            "ProjTrafoInv",             fun u -> u?ProjTrafo.Inverse.Value

            "ModelViewTrafo",           fun u -> (u?ModelTrafo <*> u?ViewTrafo).Value
            "ViewProjTrafo",            fun u -> (u?ViewTrafo <*> u?ProjTrafo).Value
            "ModelViewProjTrafo",       fun u -> (u?ModelTrafo <*> (u?ViewTrafo <*> u?ProjTrafo)).Value

            "ModelViewTrafoInv",        fun u -> (u?ModelTrafo <*> u?ViewTrafo).Inverse.Value
            "ViewProjTrafoInv",         fun u -> (u?ViewTrafo <*> u?ProjTrafo).Inverse.Value
            "ModelViewProjTrafoInv",    fun u -> (u?ModelTrafo <*> (u?ViewTrafo <*> u?ProjTrafo)).Inverse.Value

            "NormalMatrix",             fun u -> u?ModelTrafo.NormalMatrix
        ]

    let tryGetDerivedUniform (name : string) (p : IUniformProvider) =
        match table.TryGetValue name with
            | (true, getter) ->
                //Log.line "Provider %d: %s" (p.GetHashCode()) name
                try getter p |> Some
                with NotFoundException f -> None
            | _ ->
                None