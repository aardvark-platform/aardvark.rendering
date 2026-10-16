namespace Aardvark.Rendering.Tests.Utilities

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module ArrayVisitorTests =

    module private Cases =

        type private Base() = class end
        type private Derived() = inherit Base()
        type private CacheElement() = class end
        type private CacheResult() = class end

        let private elementTypes = [typeof<byte>; typeof<int>; typeof<V3f>; typeof<string>; typeof<obj>; typeof<int[]>]

        let private rectangles elementType =
            [
                for lengths in [[|0; 0|]; [|0; 3|]; [|2; 0|]; [|2; 3|]; [|1; 2; 3|]; [|0; 2; 3|]] do
                    yield Array.CreateInstance(elementType, lengths)
                yield Array.CreateInstance(elementType, [|2; 3|], [|-2; 5|])
                yield Array.CreateInstance(elementType, [|0; 3|], [|4; -7|])
            ]

        let private nonVectors elementType =
            [for length in [0; 1; 3; 17] do
                for lower in [-7; -1; 1; 5] do
                    yield Array.CreateInstance(elementType, [|length|], [|lower|])]

        let private rejected (array : Array) extension =
            let mutable calls = 0
            // The original implementation may pass an incompatible typed reference.
            // Never inspect that argument, even its Length, in rejection probes.
            let visitor = { new ArrayVisitor<int>() with
                override _.Run<'T>(_ : 'T[]) = calls <- calls + 1; 42
            }
            let context = sprintf "type=%A, rank=%d, length=%d, lower=%A, extension=%b" (array.GetType()) array.Rank array.Length [for axis=0 to array.Rank-1 do array.GetLowerBound axis] extension
            Expect.isFalse (array.GetType().IsSZArray) (context + ": incompatible test shape")
            Expect.throwsT<InvalidCastException> (fun () ->
                if extension then array.Visit visitor |> ignore
                else visitor.Run(array) |> ignore
            ) (context + ": shape rejected")
            Expect.equal calls 0 (context + ": rejection precedes visitor execution")

        let validDispatch () =
            let covariant : obj[] = box [|"first"; "second"|] :?> obj[]
            let derived : Base[] = box [|Derived(); Derived()|] :?> Base[]
            let arrays = [
                Array.empty<int> :> Array
                [|Int32.MinValue; 0; Int32.MaxValue|] :> Array
                [|0uy; 255uy|] :> Array
                [|V3f.Zero; V3f.IOO|] :> Array
                [|C4b.Red; C4b.Blue|] :> Array
                [|"first"; null|] :> Array
                Array.empty<int[]> :> Array
                [|[|1; 2|]; [||]; null|] :> Array
                covariant :> Array
                derived :> Array
                box Array.empty<string> :?> obj[] :> Array
            ]
            let mutable calls = 0
            let mutable visitedType = typeof<unit>
            let visitor = { new ArrayVisitor<obj>() with
                override _.Run<'T>(values : 'T[]) =
                    calls <- calls + 1
                    visitedType <- typeof<'T>
                    box values
            }
            for array in arrays do
                let context = sprintf "array=%A" (array.GetType())
                Expect.isTrue (array.GetType().IsSZArray) (context + ": valid vector control")
                let expectedType = array.GetType().GetElementType()
                for extension in [false; true] do
                    for read = 1 to 4 do
                        let context = sprintf "%s, extension=%b, read=%d" context extension read
                        let before = calls
                        let result = if extension then array.Visit visitor else visitor.Run array
                        Expect.isTrue (obj.ReferenceEquals(result, array)) (context + ": visitor receives and returns the original array")
                        Expect.equal visitedType expectedType (context + ": runtime element type selected")
                        Expect.equal calls (before + 1) (context + ": exactly one visit")

        let rectangularRejection () =
            for elementType in elementTypes do
                for array in rectangles elementType do
                    for extension in [false; true] do rejected array extension

        let nonVectorRejection () =
            for elementType in elementTypes do
                for array in nonVectors elementType do
                    Expect.equal array.Rank 1 "Non-vector rank-one fixture"
                    for extension in [false; true] do rejected array extension

        let cacheReuseAfterRejection () =
            let marker = CacheResult()
            let mutable calls = 0
            let ignoring = { new ArrayVisitor<CacheResult>() with
                override _.Run<'T>(_ : 'T[]) = calls <- calls + 1; marker
            }
            let checking = { new ArrayVisitor<CacheResult>() with
                override _.Run<'T>(array : 'T[]) =
                    calls <- calls + 1
                    Expect.equal typeof<'T> typeof<CacheElement> "Cache dispatch retains the runtime element type"
                    Expect.equal array.Length 3 "Valid vector reaches the checked visitor"
                    marker
            }
            let vector = Array.CreateInstance(typeof<CacheElement>, 3)
            let invalid = rectangles typeof<CacheElement> @ nonVectors typeof<CacheElement>
            for trial, array in invalid |> List.indexed do
                let context = sprintf "trial=%d, type=%A, length=%d, lower=%d" trial (array.GetType()) array.Length (array.GetLowerBound 0)
                let before = calls
                // A rejected first input may create the cached delegate, but must not poison it.
                Expect.throwsT<InvalidCastException> (fun () -> array.Visit ignoring |> ignore) (context + ": cached rejection")
                Expect.equal calls before (context + ": rejected cached delegate does not invoke a visitor")
                for visitor in [ignoring; checking] do
                    Expect.isTrue (obj.ReferenceEquals(vector.Visit visitor, marker)) (context + ": valid visits reuse dispatch after rejection")
                Expect.equal calls (before + 2) (context + ": both visitor instances are invoked once")

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwVisitor (error : exn) = raise error

        let nullReturnsAndExceptions () =
            let array = [|1; 2; 3|] :> Array
            let marker = obj()
            let identity = { new ArrayVisitor<obj>() with override _.Run<'T>(_ : 'T[]) = marker }
            let number = { new ArrayVisitor<struct (int * string)>() with override _.Run<'T>(_ : 'T[]) = struct (42, "result") }
            let nullResult = { new ArrayVisitor<string>() with override _.Run<'T>(_ : 'T[]) = null }
            Expect.isTrue (obj.ReferenceEquals(array.Visit identity, marker)) "Reference return identity"
            Expect.equal (array.Visit number) (struct (42, "result")) "Struct return value"
            Expect.isNull (array.Visit nullResult) "Null return value"
            Expect.throwsT<NullReferenceException> (fun () -> identity.Run(null : Array) |> ignore) "Null array behavior"
            Expect.throwsT<NullReferenceException> (fun () -> ArrayVisitorExtensions.Visit((null : Array), identity) |> ignore) "Null array extension behavior"
            Expect.throwsT<NullReferenceException> (fun () -> array.Visit Unchecked.defaultof<ArrayVisitor<obj>> |> ignore) "Null visitor behavior"
            for input in [Array.empty<int> :> Array; array] do
                for extension in [false; true] do
                    let context = sprintf "length=%d, extension=%b" input.Length extension
                    let expected = InvalidOperationException "visitor"
                    let mutable calls = 0
                    let visitor = { new ArrayVisitor<int>() with
                        override _.Run<'T>(_ : 'T[]) = calls <- calls + 1; throwVisitor expected
                    }
                    let mutable failure = None
                    try
                        if extension then input.Visit visitor |> ignore else visitor.Run input |> ignore
                    with error -> failure <- Some error
                    Expect.isTrue (obj.ReferenceEquals(failure.Value, expected)) (context + ": original visitor exception identity")
                    Expect.stringContains failure.Value.StackTrace "throwVisitor" (context + ": original visitor exception stack")
                    Expect.equal calls 1 (context + ": throwing visitor invoked exactly once")

    let tests (target : TestTarget) =
        [
            "Vector jagged and covariant dispatch",     Cases.validDispatch
            "Rectangular arrays are rejected",          Cases.rectangularRejection
            "Non-vector rank-one arrays are rejected",  Cases.nonVectorRejection
            "Rejected shapes preserve cached dispatch", Cases.cacheReuseAfterRejection
            "Null results and visitor exceptions",      Cases.nullReturnsAndExceptions
        ]
        |> prepareCasesCpu "ArrayVisitor" target
