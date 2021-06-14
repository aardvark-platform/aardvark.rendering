namespace Aardvark.Rendering.Tests

module ``CompactSet Tests`` =
    open Aardvark.Rendering
    open FSharp.Data.Adaptive
    open NUnit.Framework

    let data = [| "foo"; "bar"; "a"; "b"; "c"; "d" |]

    let isValid (set : amap<'T, int>) =
        let map = set |> AMap.force
        let values = map |> HashMap.toValueList |> List.sort

        printfn "%s" (string map)
        values = [0 .. values.Length - 1]

    [<Test>]
    let ``[CompactSet] Validity``() =
        let input = cset<string>(data)
        let compact = input |> ASet.compact

        Assert.IsTrue (isValid compact)

    [<Test>]
    let ``[CompactSet] Remove``() =
        let input = cset<string>(data)
        let compact = input |> ASet.compact

        Assert.IsTrue (isValid compact)

        transact (fun _ ->
            input.Value <- input.Value |> HashSet.remove "bar" |> HashSet.remove "c"
        )

        Assert.IsTrue (isValid compact)

    [<Test>]
    let ``[CompactSet] Add and remove``() =
        let input = cset<string>(data)
        let compact = input |> ASet.compact

        Assert.IsTrue (isValid compact)

        transact (fun _ ->
            input.Value <- input.Value |> HashSet.remove "bar" |> HashSet.remove "c" |> HashSet.add "new"
        )

        Assert.IsTrue (isValid compact)