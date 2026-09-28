namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering.Text
open BenchmarkDotNet.Attributes

module ShapeListBenchmark =

    // The original implementation, kept as the benchmark baseline.
    let appendFold (many : seq<ShapeList>) =
        use e = many.GetEnumerator()
        if e.MoveNext() then
            let mutable result = e.Current
            while e.MoveNext() do
                result <- ShapeList.append result e.Current
            result
        else
            ShapeList.empty

    let cases =
        [| "empty", 0, 0, 1
           "singleton-empty", 1, 0, 1
           "singleton", 1, 1, 1
           "two-empty", 2, 0, 1
           "two", 2, 1, 1
           "three", 3, 1, 1
           "four", 4, 1, 1
           "eight", 8, 1, 1
           "empty-32", 32, 0, 1
           "empty-1024", 1024, 0, 1
           "empty-8192", 8192, 0, 1
           "sparse-1024x1", 1024, 1, 32
           "sparse-1024x4", 1024, 4, 32
           "256x1", 256, 1, 1
           "512x1", 512, 1, 1
           "1024x1", 1024, 1, 1
           "2048x1", 2048, 1, 1
           "256x4", 256, 4, 1
           "512x4", 512, 4, 1
           "1024x4", 1024, 4, 1
           "2048x4", 2048, 4, 1
           "256x16", 256, 16, 1 |]

    let createInputs count shapes stride =
        let primitive = ConcreteShape.fillRectangle C4b.White Box2d.Unit
        Array.init count (fun i ->
            if shapes = 0 || i % stride <> 0 then ShapeList.empty
            else
                [for j in 0 .. shapes - 1 ->
                    { primitive with trafo = M33d.Translation(float (i * shapes + j), float (j % 3)); z = j }]
                |> ShapeList.ofList)
        :> seq<ShapeList>

/// CPU-only, allocation-diagnosed comparisons; each invocation concatenates the entire input.
/// Run in Release with --benchmark-shapelist. The largest baseline cases allocate gigabytes.
[<MemoryDiagnoser; PlainExporter>]
type ShapeListBenchmarks() =
    let mutable inputs : seq<ShapeList> = Seq.empty
    let mutable before = Unchecked.defaultof<Func<IEnumerable<ShapeList>, ShapeList>>
    let mutable after = Unchecked.defaultof<Func<IEnumerable<ShapeList>, ShapeList>>

    member _.Cases = ShapeListBenchmark.cases |> Array.map (fun (name, _, _, _) -> name)

    [<ParamsSource("Cases")>]
    member val Case = "empty" with get, set

    [<GlobalSetup>]
    member this.Setup() =
        let _, count, shapes, stride = ShapeListBenchmark.cases |> Array.find (fun (name, _, _, _) -> name = this.Case)
        inputs <- ShapeListBenchmark.createInputs count shapes stride
        // Symmetric direct delegates avoid different F# wrapper/tail-call code in the comparison.
        before <- typeof<ShapeListBenchmarks>.Assembly.GetType("Aardvark.Rendering.Tests.ShapeListBenchmark").GetMethod("appendFold")
                    .CreateDelegate<Func<IEnumerable<ShapeList>, ShapeList>>()
        after <- typeof<ShapeList>.Assembly.GetType("Aardvark.Rendering.Text.ShapeListModule").GetMethod("concat")
                    .CreateDelegate<Func<IEnumerable<ShapeList>, ShapeList>>()

    [<Benchmark(Baseline = true)>]
    member _.AppendFold() = before.Invoke inputs

    [<Benchmark>]
    member _.Concat() = after.Invoke inputs
