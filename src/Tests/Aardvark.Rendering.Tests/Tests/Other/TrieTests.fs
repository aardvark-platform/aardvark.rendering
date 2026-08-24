namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open Aardvark.Rendering
open Expecto

module ``Trie Tests`` =

    type private LinkedValue(id : int) =
        let mutable prev : LinkedValue voption = ValueNone
        let mutable next : LinkedValue voption = ValueNone

        member _.Id = id

        member _.Prev
            with get() = prev
            and set value = prev <- value

        member _.Next
            with get() = next
            and set value = next <- value

        interface ILinked<LinkedValue> with
            member _.Prev
                with get() = prev
                and set value = prev <- value

            member _.Next
                with get() = next
                and set value = next <- value

        override _.ToString() = sprintf "v%d" id

    let private key (components : int list) = components |> List.map box

    let private ascending =
        { new IComparer<obj> with
            member _.Compare(l, r) = compare (unbox<int> l) (unbox<int> r)
        }

    let private descending =
        { new IComparer<obj> with
            member _.Compare(l, r) = compare (unbox<int> r) (unbox<int> l)
        }

    let private isSame (l : LinkedValue) (r : LinkedValue) = obj.ReferenceEquals(l, r)

    let private assertReferences (message : string) (expected : LinkedValue list) (actual : LinkedValue list) =
        Expect.equal actual.Length expected.Length (message + " length")
        List.iter2 (fun e a ->
            Expect.isTrue (isSame e a) (sprintf "%s: expected %O, got %O" message e a)
        ) expected actual

    let private asOption = function
        | ValueSome value -> Some value
        | ValueNone -> None

    let private expectLink (message : string) (expected : LinkedValue option) (actual : LinkedValue voption) =
        match expected, asOption actual with
        | None, None -> ()
        | Some e, Some a when isSame e a -> ()
        | _ -> failtestf "%s: expected %A, got %A" message expected actual

    let private validate (message : string) (trie : Trie<LinkedValue>) (known : seq<LinkedValue>) (expected : LinkedValue list) =
        // Truncation makes cycle failures bounded rather than allowing a broken Values sequence to hang the test.
        let forward = trie.Values |> Seq.truncate (expected.Length + 1) |> Seq.toList
        assertReferences (message + " forward") expected forward

        let backward =
            let result = ResizeArray<LinkedValue>()
            let mutable current = trie.Last
            let mutable remaining = expected.Length + 1
            while remaining > 0 && ValueOption.isSome current do
                let value = current.Value
                result.Add value
                current <- value.Prev
                remaining <- remaining - 1
            result |> Seq.toList

        assertReferences (message + " backward") (List.rev expected) backward

        let expectedFirst = expected |> List.tryHead
        let expectedLast = expected |> List.tryLast
        expectLink (message + " First") expectedFirst trie.First
        expectLink (message + " Last") expectedLast trie.Last

        let unique = HashSet<LinkedValue>()
        for value in forward do
            Expect.isTrue (unique.Add value) (sprintf "%s: %O occurs more than once" message value)

        let expectedArray = List.toArray expected
        for i in 0 .. expectedArray.Length - 1 do
            let value = expectedArray.[i]
            let prev = if i > 0 then Some expectedArray.[i - 1] else None
            let next = if i + 1 < expectedArray.Length then Some expectedArray.[i + 1] else None
            expectLink (sprintf "%s: %O.Prev" message value) prev value.Prev
            expectLink (sprintf "%s: %O.Next" message value) next value.Next

            match value.Next with
            | ValueSome next -> expectLink (sprintf "%s: next/prev pair at %O" message value) (Some value) next.Prev
            | ValueNone -> ()

            match value.Prev with
            | ValueSome prev -> expectLink (sprintf "%s: prev/next pair at %O" message value) (Some value) prev.Next
            | ValueNone -> ()

        for value in known do
            if not (expected |> List.exists (isSame value)) then
                expectLink (sprintf "%s: detached %O.Prev" message value) None value.Prev
                expectLink (sprintf "%s: detached %O.Next" message value) None value.Next

    type private ModelNode() =
        let children = ResizeArray<obj * ModelNode>()
        member val Value : LinkedValue option = None with get, set
        member _.Children = children
        member x.IsEmpty = x.Value.IsNone && children.Count = 0

    type private ModelTrie(sorted : bool) =
        let mutable root = ModelNode()

        let tryFindChild (node : ModelNode) (part : obj) =
            node.Children
            |> Seq.tryFindIndex (fun (key, _) -> Object.Equals(key, part))

        let rec add (node : ModelNode) (components : obj list) (value : LinkedValue) =
            match components with
            | [] -> node.Value <- Some value
            | part :: rest ->
                let child =
                    match tryFindChild node part with
                    | Some index -> snd node.Children.[index]
                    | None ->
                        let child = ModelNode()
                        node.Children.Add(part, child)
                        child
                add child rest value

        let rec remove (node : ModelNode) (components : obj list) =
            match components with
            | [] ->
                match node.Value with
                | Some _ ->
                    node.Value <- None
                    true
                | None -> false
            | part :: rest ->
                match tryFindChild node part with
                | None -> false
                | Some index ->
                    let child = snd node.Children.[index]
                    if remove child rest then
                        if child.IsEmpty then node.Children.RemoveAt index
                        true
                    else
                        false

        let rec tryValue (node : ModelNode) (components : obj list) =
            match components with
            | [] -> node.Value
            | part :: rest ->
                match tryFindChild node part with
                | Some index -> tryValue (snd node.Children.[index]) rest
                | None -> None

        let rec appendValues (result : ResizeArray<LinkedValue>) (node : ModelNode) =
            node.Value |> Option.iter result.Add

            if sorted then
                node.Children
                |> Seq.sortWith (fun (l, _) (r, _) -> ascending.Compare(l, r))
                |> Seq.iter (snd >> appendValues result)
            else
                node.Children |> Seq.iter (snd >> appendValues result)

        member _.Add(components, value) = add root components value
        member _.Remove components = remove root components
        member _.TryValue components = tryValue root components
        member _.Clear() = root <- ModelNode()
        member _.Values =
            let result = ResizeArray<LinkedValue>()
            appendValues result root
            result |> Seq.toList

    module Cases =

        let emptyAndPrefixKeys() =
            let trie = Trie<LinkedValue>()
            let known = ResizeArray<LinkedValue>()
            let create id =
                let value = LinkedValue id
                known.Add value
                value

            validate "initial" trie known []

            let abc = create 3
            trie.Add(key [1; 2; 3], abc)
            validate "deep leaf" trie known [abc]

            let ab = create 2
            trie.Add(key [1; 2], ab)
            validate "nested prefix" trie known [ab; abc]

            let a = create 1
            trie.Add(key [1], a)
            validate "top prefix" trie known [a; ab; abc]

            let root = create 0
            trie.Add([], root)
            validate "empty key" trie known [root; a; ab; abc]

        let sortedChildren() =
            let trie = Trie<LinkedValue>([| ValueSome ascending; ValueSome descending; ValueSome ascending |])
            let known = ResizeArray<LinkedValue>()
            let create id =
                let value = LinkedValue id
                known.Add value
                value

            let v21 = create 21
            let v10 = create 10
            let v23 = create 23
            let v3 = create 3
            let v2 = create 2
            let v1 = create 1
            let root = create 0
            let v230 = create 230
            let v22 = create 22
            let v31 = create 31
            let v4 = create 4
            let v0 = create 100

            trie.Add(key [2; 1], v21)
            trie.Add(key [1; 0], v10)
            trie.Add(key [2; 3], v23)
            trie.Add(key [3], v3)
            trie.Add(key [1], v1)
            trie.Add(key [2], v2)
            trie.Add([], root)
            trie.Add(key [2; 3; 0], v230)
            trie.Add(key [2; 2], v22)
            trie.Add(key [3; 1], v31)
            trie.Add(key [4], v4)
            trie.Add(key [0], v0)

            validate "configured child comparers" trie known [root; v0; v1; v10; v2; v23; v230; v22; v21; v3; v31; v4]

        let unsortedChildren() =
            let trie = Trie<LinkedValue>()
            let known = ResizeArray<LinkedValue>()
            let create id =
                let value = LinkedValue id
                known.Add value
                value

            let v22 = create 22
            let v1 = create 1
            let v21 = create 21
            let v3 = create 3
            let v2 = create 2
            let v19 = create 19
            let v18 = create 18
            let root = create 0

            trie.Add(key [2; 2], v22)
            trie.Add(key [1], v1)
            trie.Add(key [2; 1], v21)
            trie.Add(key [3], v3)
            trie.Add(key [2], v2)
            trie.Add(key [1; 9], v19)
            trie.Add(key [1; 8], v18)
            trie.Add([], root)

            validate "insertion-ordered children" trie known [root; v2; v22; v21; v1; v19; v18; v3]

        let replacements() =
            let trie = Trie<LinkedValue>()
            let known = ResizeArray<LinkedValue>()
            let create id =
                let value = LinkedValue id
                known.Add value
                value

            let v0 = create 0
            let v1 = create 1
            let v15 = create 15
            let v2 = create 2
            trie.Add(key [0], v0)
            trie.Add(key [1], v1)
            trie.Add(key [1; 5], v15)
            trie.Add(key [2], v2)
            validate "before replacement" trie known [v0; v1; v15; v2]

            let oldPrev = v1.Prev
            let oldNext = v1.Next
            trie.Add(key [1], v1)
            Expect.isTrue (v1.Prev = oldPrev) "Same-instance replacement changed Prev"
            Expect.isTrue (v1.Next = oldNext) "Same-instance replacement changed Next"
            validate "same-instance replacement" trie known [v0; v1; v15; v2]

            let replacement = create 10
            trie.Add(key [1], replacement)
            validate "distinct middle replacement" trie known [v0; replacement; v15; v2]

            let lastReplacement = create 20
            trie.Add(key [2], lastReplacement)
            validate "distinct endpoint replacement" trie known [v0; replacement; v15; lastReplacement]

        let removalPruningAndReinsertion() =
            let trie = Trie<LinkedValue>()
            let known = ResizeArray<LinkedValue>()
            let create id =
                let value = LinkedValue id
                known.Add value
                value

            let a = create 1
            let b = create 2
            let b1 = create 21
            let b2 = create 22
            let c = create 3
            trie.Add(key [1], a)
            trie.Add(key [2], b)
            trie.Add(key [2; 1], b1)
            trie.Add(key [2; 2], b2)
            trie.Add(key [3], c)
            validate "before removal" trie known [a; b; b1; b2; c]

            Expect.isTrue (trie.Remove(key [2])) "Prefix value was not removed"
            validate "removed prefix value" trie known [a; b1; b2; c]

            Expect.isTrue (trie.Remove(key [2; 1])) "First child was not removed"
            validate "removed first child" trie known [a; b2; c]

            Expect.isTrue (trie.Remove(key [2; 2])) "Last child was not removed"
            validate "pruned subtree" trie known [a; c]

            Expect.isFalse (trie.Remove(key [2])) "Removing a pruned key unexpectedly succeeded"
            validate "missing removal" trie known [a; c]

            let reinserted = create 20
            trie.Add(key [2], reinserted)
            validate "unsorted reinsertion appends" trie known [a; c; reinserted]

        let clearDetachesValues() =
            let trie = Trie<LinkedValue>()
            let known = ResizeArray<LinkedValue>()
            let values = [for id in 0 .. 5 -> LinkedValue id]
            known.AddRange values

            trie.Add([], values.[0])
            trie.Add(key [1], values.[1])
            trie.Add(key [1; 1], values.[2])
            trie.Add(key [2], values.[3])
            validate "before clear" trie known values.[0 .. 3]

            trie.Clear()
            validate "after clear" trie known []

            trie.Add(key [3; 1], values.[4])
            trie.Add(key [3], values.[5])
            validate "reuse after clear" trie known [values.[5]; values.[4]]

        let referenceOperations sorted seed =
            let comparers =
                if sorted then
                    [| ValueSome ascending; ValueSome ascending; ValueSome ascending |]
                else
                    [||]

            let trie = Trie<LinkedValue>(comparers)
            let model = ModelTrie(sorted)
            let known = ResizeArray<LinkedValue>()
            let random = Random seed
            let mutable nextId = 0
            let keys =
                [|
                    []; [0]; [1]; [2]; [3]
                    [0; 0]; [0; 2]; [1; 0]; [1; 2]; [2; 1]; [2; 3]; [3; 0]
                    [0; 0; 1]; [1; 2; 0]; [2; 1; 3]; [3; 0; 2]
                |]
                |> Array.map key

            for step in 0 .. 399 do
                let components = keys.[random.Next keys.Length]
                match random.Next 100 with
                | operation when operation < 50 ->
                    let value = LinkedValue nextId
                    nextId <- nextId + 1
                    known.Add value
                    trie.Add(components, value)
                    model.Add(components, value)

                | operation when operation < 68 ->
                    match model.TryValue components with
                    | Some value ->
                        trie.Add(components, value)
                        model.Add(components, value)
                    | None ->
                        let value = LinkedValue nextId
                        nextId <- nextId + 1
                        known.Add value
                        trie.Add(components, value)
                        model.Add(components, value)

                | operation when operation < 94 ->
                    let actual = trie.Remove components
                    let expected = model.Remove components
                    Expect.equal actual expected (sprintf "step %d removal result" step)

                | _ ->
                    trie.Clear()
                    model.Clear()

                validate (sprintf "%s reference step %d" (if sorted then "sorted" else "unsorted") step) trie known model.Values

    [<Tests>]
    let tests =
        testList "Trie" [
            testCase "empty and nested prefix keys" Cases.emptyAndPrefixKeys
            testCase "sorted children use configured comparers" Cases.sortedChildren
            testCase "unsorted children retain insertion order" Cases.unsortedChildren
            testCase "same and distinct replacements preserve links" Cases.replacements
            testCase "removal, pruning, and reinsertion" Cases.removalPruningAndReinsertion
            testCase "clear detaches values and permits reuse" Cases.clearDetachesValues
            testCase "fixed-seed unsorted reference operations" (fun () -> Cases.referenceOperations false 0x51A7)
            testCase "fixed-seed sorted reference operations" (fun () -> Cases.referenceOperations true 0x51A7)
        ]
