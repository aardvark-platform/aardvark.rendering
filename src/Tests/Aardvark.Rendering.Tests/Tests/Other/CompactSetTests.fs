namespace Aardvark.Rendering.Tests

open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto

module ``CompactSet Tests`` =

    [<AutoOpen>]
    module private Common =

        let data = [| "foo"; "bar"; "a"; "b"; "c"; "d" |]

        let isValid (set : amap<'T, int>) =
            let map = set |> AMap.force
            let values = map |> HashMap.toValueList |> List.sort

            printfn "%s" (string map)
            values = [0 .. values.Length - 1]

    module private Cases =

        let validity() =
            let input = cset<string>(data)
            let compact = input |> ASet.compact

            Expect.isTrue (isValid compact) "set not valid"

        let remove() =
            let input = cset<string>(data)
            let compact = input |> ASet.compact

            Expect.isTrue (isValid compact) "set not valid before remove"

            transact (fun _ ->
                input.Value <- input.Value |> HashSet.remove "bar" |> HashSet.remove "c"
            )

            Expect.isTrue (isValid compact) "set not valid after remove"

        let addAndRemove() =
            let input = cset<string>(data)
            let compact = input |> ASet.compact

            Expect.isTrue (isValid compact) "set not valid before add and remove"

            transact (fun _ ->
                input.Value <- input.Value |> HashSet.remove "bar" |> HashSet.remove "c" |> HashSet.add "new"
            )

            Expect.isTrue (isValid compact) "set not valid after add and remove"

    [<Tests>]
    let tests =
        testList "Utilities.CompactSet" [
            testCase "Validity"         Cases.validity
            testCase "Remove"           Cases.remove
            testCase "Add and remove"   Cases.addAndRemove
        ]