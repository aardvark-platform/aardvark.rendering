namespace Aardvark.Rendering.Tests.Utilities

open System
open System.Runtime.CompilerServices
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module CubeMapTraversal =

    module private Cases =

        type private Payload = { Value : int }

        let private levels = [0; 1; 8; 16]

        let private sameArray (expected : 'T[]) (actual : 'T[]) context =
            Expect.sequenceEqual actual expected context

        let private error (action : unit -> unit) =
            let result =
                try action(); None
                with e -> Some e
            match result with
            | Some e -> e
            | None -> failtest "Expected an exception"

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwMarker (marker : exn) = raise marker

        let valuesAndOrder() =
            for levelCount in levels do
                let leftData = Array.init (6 * levelCount) (fun i -> 17 + i)
                let rightData = Array.init leftData.Length (fun i -> string (1000 + i))
                let left = CubeMap<int>(leftData)
                let right = CubeMap<string>(rightData)
                let leftBefore, rightBefore = Array.copy leftData, Array.copy rightData
                let context = $"levels={levelCount}"
                let singles = ResizeArray<int>()
                let pairs = ResizeArray<int * string>()

                let mapped = left |> CubeMap.map (fun value -> singles.Add value; string (value * 2))
                sameArray (Array.map (fun value -> string (value * 2)) leftData) mapped.Data (context + ": type-changing map")
                sameArray leftData (singles.ToArray()) (context + ": map callback order and count")
                singles.Clear()
                CubeMap.iter singles.Add left
                sameArray leftData (singles.ToArray()) (context + ": iter callback order and count")

                let mappedPairs = CubeMap.map2 (fun x y -> pairs.Add(x, y); float x + float y) left right
                sameArray (Array.map2 (fun x y -> float x + float y) leftData rightData) mappedPairs.Data (context + ": heterogeneous map2")
                let expectedPairs = Array.zip leftData rightData
                sameArray expectedPairs (pairs.ToArray()) (context + ": map2 callback order and count")
                pairs.Clear()
                CubeMap.iter2 (fun x y -> pairs.Add(x, y)) left right
                sameArray expectedPairs (pairs.ToArray()) (context + ": iter2 callback order and count")
                sameArray leftBefore leftData (context + ": left input unchanged")
                sameArray rightBefore rightData (context + ": right input unchanged")
                Expect.isTrue (obj.ReferenceEquals(left.Data, leftData)) (context + ": left ownership unchanged")
                Expect.isTrue (obj.ReferenceEquals(right.Data, rightData)) (context + ": right ownership unchanged")

        let referenceIndependence() =
            for levelCount in levels do
                let left = CubeMap<Payload>(Array.init (6 * levelCount) (fun i -> { Value = i }))
                let right = CubeMap<Payload>(Array.init left.Data.Length (fun i -> { Value = 1000 + i }))
                let context = $"levels={levelCount}"
                let mapped = CubeMap.map id left
                let paired = CubeMap.map2 (fun x y -> if x.Value % 2 = 0 then x else y) left right
                let calls = ResizeArray<Payload>()
                CubeMap.iter calls.Add left
                for i in 0 .. left.Data.Length - 1 do
                    Expect.isTrue (obj.ReferenceEquals(mapped.Data.[i], left.Data.[i])) (context + $": map payload {i}")
                    let expected = if i % 2 = 0 then left.Data.[i] else right.Data.[i]
                    Expect.isTrue (obj.ReferenceEquals(paired.Data.[i], expected)) (context + $": map2 payload {i}")
                    Expect.isTrue (obj.ReferenceEquals(calls.[i], left.Data.[i])) (context + $": iter payload {i}")
                calls.Clear()
                CubeMap.iter2 (fun x y -> calls.Add x; calls.Add y) left right
                Expect.equal calls.Count (2 * left.Data.Length) (context + ": iter2 count")
                for i in 0 .. left.Data.Length - 1 do
                    Expect.isTrue (obj.ReferenceEquals(calls.[2 * i], left.Data.[i])) (context + $": iter2 left {i}")
                    Expect.isTrue (obj.ReferenceEquals(calls.[2 * i + 1], right.Data.[i])) (context + $": iter2 right {i}")
                if not left.IsEmpty then
                    Expect.isFalse (obj.ReferenceEquals(mapped.Data, left.Data)) (context + ": separate map array")
                    Expect.isFalse (obj.ReferenceEquals(paired.Data, left.Data)) (context + ": separate map2 left array")
                    Expect.isFalse (obj.ReferenceEquals(paired.Data, right.Data)) (context + ": separate map2 right array")
                    let first, last = left.Data.[0], mapped.Data.[mapped.Data.Length - 1]
                    mapped.Data.[0] <- { Value = -1 }
                    paired.Data.[0] <- { Value = -2 }
                    Expect.isTrue (obj.ReferenceEquals(left.Data.[0], first)) (context + ": result writes do not mutate input")
                    Expect.equal right.Data.[0].Value 1000 (context + ": result writes do not mutate right")
                    left.Data.[left.Data.Length - 1] <- { Value = -3 }
                    Expect.isTrue (obj.ReferenceEquals(mapped.Data.[mapped.Data.Length - 1], last)) (context + ": source replacement does not mutate result")

        let callbackFailures() =
            for levelCount in [1; 8; 16] do
                let left = CubeMap<int>(Array.init (6 * levelCount) id)
                let right = CubeMap<int>(Array.init left.Data.Length (fun i -> 1000 + i))
                for stop in [0; left.Data.Length / 2; left.Data.Length - 1] do
                    for operation in ["map"; "map2"; "iter"; "iter2"] do
                        let context = $"levels={levelCount}, stop={stop}, operation={operation}"
                        let marker = InvalidOperationException context
                        let seen = ResizeArray<int>()
                        let callback x =
                            seen.Add x
                            if x = stop then throwMarker marker
                            x + 1
                        let actual =
                            error (fun () ->
                                match operation with
                                | "map" -> CubeMap.map callback left |> ignore
                                | "map2" -> CubeMap.map2 (fun x y -> Expect.equal y (1000 + x) context; callback x) left right |> ignore
                                | "iter" -> CubeMap.iter (callback >> ignore) left
                                | _ -> CubeMap.iter2 (fun x y -> Expect.equal y (1000 + x) context; callback x |> ignore) left right
                            )
                        Expect.isTrue (obj.ReferenceEquals(actual, marker)) (context + ": exception identity")
                        Expect.stringContains actual.StackTrace "throwMarker" (context + ": original throwing frame")
                        sameArray [|0 .. stop|] (seen.ToArray()) (context + ": exactly the prefix through the failing callback")
                        sameArray (Array.init left.Data.Length id) left.Data (context + ": left unchanged")
                        sameArray (Array.init right.Data.Length (fun i -> 1000 + i)) right.Data (context + ": right unchanged")

        let emptyAndInvalidInputs() =
            let mutable calls = 0
            let callback x = calls <- calls + 1; x
            for empty in [CubeMap.empty<int>; CubeMap<int>(0)] do
                Expect.isTrue (CubeMap.map callback empty).IsEmpty "empty map"
                Expect.isTrue (CubeMap.map2 (fun x _ -> callback x) empty empty).IsEmpty "empty map2"
                CubeMap.iter (callback >> ignore) empty
                CubeMap.iter2 (fun x _ -> callback x |> ignore) empty empty
            Expect.equal calls 0 "Empty maps do not invoke callbacks"

            let check context original current =
                let before = calls
                let expected, actual = error original, error current
                Expect.equal (actual.GetType()) (expected.GetType()) (context + ": same failure type")
                Expect.equal actual.Message expected.Message (context + ": same failure message")
                Expect.equal calls before (context + ": rejected before any callback")

            let missing = Unchecked.defaultof<CubeMap<int>>
            check "default map" (fun () -> CubeMap.mapi (fun _ _ x -> callback x) missing |> ignore) (fun () -> CubeMap.map callback missing |> ignore)
            check "default iter" (fun () -> CubeMap.iteri (fun _ _ x -> callback x |> ignore) missing) (fun () -> CubeMap.iter (callback >> ignore) missing)
            let valid = CubeMap.single 1 42
            let empty = CubeMap.empty<int>
            let pairs =
                [ "both default", missing, missing
                  "left default", missing, valid
                  "right default", valid, missing
                  "default and empty", missing, empty
                  "empty and default", empty, missing
                  "empty and nonempty", empty, valid
                  "nonempty and empty", valid, empty
                  "short and long", valid, CubeMap.single 8 7
                  "long and short", CubeMap.single 16 7, valid ]
            for label, left, right in pairs do
                check (label + ": map2")
                    (fun () -> CubeMap.mapi2 (fun _ _ x _ -> callback x) left right |> ignore)
                    (fun () -> CubeMap.map2 (fun x _ -> callback x) left right |> ignore)
                check (label + ": iter2")
                    (fun () -> CubeMap.iteri2 (fun _ _ x _ -> callback x |> ignore) left right)
                    (fun () -> CubeMap.iter2 (fun x _ -> callback x |> ignore) left right)

        let boundedAllocations() =
            for levelCount in levels do
                let left = CubeMap.init levelCount (fun face level -> 6 * level + int face)
                let right = CubeMap.single levelCount 7
                let mutable result = CubeMap.empty<int>
                let mutable sum = 0
                let mapValue x = x + 1
                let mapPair x y = x + y
                let consume x = sum <- sum + x
                let consumePair x y = sum <- sum + x + y
                let operations =
                    [ "map", true, (fun () -> result <- CubeMap.map mapValue left)
                      "map2", true, (fun () -> result <- CubeMap.map2 mapPair left right)
                      "iter", false, (fun () -> CubeMap.iter consume left)
                      "iter2", false, (fun () -> CubeMap.iter2 consumePair left right) ]
                for name, maps, action in operations do
                    for _ in 1 .. 128 do action()
                    let before = GC.GetAllocatedBytesForCurrentThread()
                    for _ in 1 .. 256 do action()
                    let allocated = GC.GetAllocatedBytesForCurrentThread() - before
                    // Permit fixed call overhead, but not allocation per visited face.
                    let budget = 256L + (if maps then 24L + 4L * int64 left.Data.Length else 0L)
                    Expect.isLessThanOrEqual allocated (256L * budget) $"levels={levelCount}, operation={name}: traversal allocation budget"
                    GC.KeepAlive result
                    GC.KeepAlive sum

    let tests (target : TestTarget) =
        [
            "Value maps and iterators keep level-major order",                  Cases.valuesAndOrder
            "Reference maps keep payload identity and independent arrays",     Cases.referenceIndependence
            "Callback failures retain identity and stop traversal",            Cases.callbackFailures
            "Empty default and unequal inputs preserve contracts",             Cases.emptyAndInvalidInputs
            "Traversal allocation stays bounded",                              Cases.boundedAllocations
        ]
        |> prepareCasesCpu "CubeMapTraversal" target
