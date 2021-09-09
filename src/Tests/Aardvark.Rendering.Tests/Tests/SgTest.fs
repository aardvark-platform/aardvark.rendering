namespace Aardvark.Rendering.Tests

open System
open System.Reflection
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Expecto

module ``SceneGraph Tests`` =

    [<AutoOpen>]
    module private Utilities =
        let semantics =
            [
                "RenderObjects",        typeof<aset<IRenderObject>>
                "GlobalBoundingBox",    typeof<aval<Box3d>>
                "LocalBoundingBox",     typeof<aval<Box3d>>
            ]

        let genericNameRx = Regex @"(?<name>.*?)´[0-9]+"
        let cleanName (name : string) =
            let m = genericNameRx.Match name
            if m.Success then m.Groups.["name"].Value
            else name

        let intrisicNames =
            Dict.ofList [
                typeof<byte>, "byte"
                typeof<int8>, "int8"
                typeof<uint16>, "uint16"
                typeof<int16>, "int16"
                typeof<int>, "int"
                typeof<uint32>, "uint32"
                typeof<int64>, "int64"
                typeof<uint64>, "uint64"
                typeof<obj>, "obj"
            ]

        let rec prettyName (t : Type) =
            match intrisicNames.TryGetValue t with
                | (true, n) -> n
                | _ ->
                    if t.IsArray then
                        sprintf "%s[]" (t.GetElementType() |> prettyName)
                    elif t.IsGenericType then
                        let args = t.GetGenericArguments() |> Seq.map prettyName |> String.concat ","
                        sprintf "%s<%s>" (cleanName t.Name) args
                    else
                        cleanName t.Name

    [<Tests>]
    let tests =
        testList "SceneGraph" [
            test "Check Completeness" {
                IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
                let sgTypes = Introspection.GetAllClassesImplementingInterface(typeof<ISg>)

                let sgModule = typeof<Sg.Set>.DeclaringType

                for (att, expected) in semantics do
                    for t in sgTypes do
                        if t.DeclaringType = sgModule then
                            let hasRule = Ag.hasSynRule t expected att
                            Expect.isTrue hasRule <| sprintf "no semantic %A for type %s" att (prettyName t)
            }
        ]