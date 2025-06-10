namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open System
open System.Collections.Generic
open System.Reflection
open Expecto

module ``IDictionary StructuralComparer Tests`` =

    let private structuralComparer<'T> : IEqualityComparer<IDictionary<Symbol, 'T>> =
        let asm = typeof<ManagedPool>.Assembly
        let utilsModule = asm.GetType("Aardvark.SceneGraph.ManagedPoolUtilities")
        let dictModule = utilsModule.GetNestedType("Dictionary", BindingFlags.NonPublic)
        let comparerType = dictModule.GetNestedType("StructuralComparer`1", BindingFlags.NonPublic).MakeGenericType [| typeof<'T> |]
        let instanceProperty = comparerType.GetProperty("Instance", BindingFlags.Static ||| BindingFlags.NonPublic)
        let cmp = instanceProperty.GetValue(null) |> unbox
        Expect.isNotNull cmp "Failed to get comparer"
        cmp

    let rnd = Random(0)

    module Array =
        let shuffle arr =
            let shuffled = Array.copy arr
            rnd.Shuffle(shuffled)
            shuffled

    module Sym =
        let rnd() =
            let chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
            let str = String (Array.init 32 (fun _ -> chars.[rnd.Next(chars.Length)]))
            Sym.ofString str

    module Map =
        let ofArray arr = arr |> Array.shuffle |> Map.ofArray

    module Dict =
        let ofArray arr = arr |> Array.shuffle |> Dict.ofArray

    module SymDict =
        let ofArray arr = arr |> Array.shuffle |> SymDict.ofArray

    module Dictionary =
        let ofArray arr = arr |> Array.shuffle |> Dictionary.ofArray

    module Cases =
        let equals() =
            let cmp = structuralComparer<int>

            let values =
                [|
                    DefaultSemantic.Positions, 42
                    DefaultSemantic.Normals, 1001
                    DefaultSemantic.Colors, -13
                |]

            let map = Map.ofArray values
            let dict = Dict.ofArray values
            let symDict = SymDict.ofArray values
            let netDict = Dictionary.ofArray values

            Expect.isTrue (cmp.Equals(map, dict)) "Map does not equal Dict"
            Expect.isTrue (cmp.Equals(map, symDict)) "Map does not equal SymbolDict"
            Expect.isTrue (cmp.Equals(map, netDict)) "Map does not equal Dictionary"
            Expect.isTrue (cmp.Equals(dict, symDict)) "Dict does not equal SymbolDict"
            Expect.isTrue (cmp.Equals(dict, netDict)) "Dict does not equal Dictionary"
            Expect.isTrue (cmp.Equals(symDict, netDict)) "SymbolDict does not equal Dictionary"

            let map2 =
                Map.ofArray [|
                    DefaultSemantic.Positions, 42
                    DefaultSemantic.Normals, 1000
                    DefaultSemantic.Colors, -13
                |]

            let map3 =
                Map.ofArray [|
                    DefaultSemantic.Normals, 42
                    DefaultSemantic.Positions, 1001
                    DefaultSemantic.Colors, -13
                |]

            Expect.isFalse (cmp.Equals(dict, map2)) "Dict equals Map with modified value"
            Expect.isFalse (cmp.Equals(dict, map3)) "Dict equals Map with modified key"

        let getHashCode() =
            let cmp = structuralComparer<int>

            let values =
                Array.init 64 (fun _ ->
                    Sym.rnd(), rnd.Next()
                )

            let map = cmp.GetHashCode <| Map.ofArray values
            let dict = cmp.GetHashCode <| Dict.ofArray values
            let symDict = cmp.GetHashCode <| SymDict.ofArray values
            let netDict = cmp.GetHashCode <| Dictionary.ofArray values

            Expect.equal map dict "Map hash does not equal Dict hash"
            Expect.equal map symDict "Map hash does not equal SymbolDict hash"
            Expect.equal map netDict "Map hash does not equal Dictionary hash"

    [<Tests>]
    let tests =
        testList "IDictionary.StructuralComparer" [
            testCase "Equals"       Cases.equals
            testCase "GetHashCode"  Cases.getHashCode
        ]