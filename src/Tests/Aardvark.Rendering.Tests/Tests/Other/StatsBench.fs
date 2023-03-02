module StatsBench

    open BenchmarkDotNet.Attributes
    open Aardvark.Rendering
    open Aardvark.Base
    
//|              Method |     Mean |     Error |    StdDev |   Median |  Gen 0 | Allocated |
//|-------------------- |---------:|----------:|----------:|---------:|-------:|----------:|
//| AddStats_OptionIter | 7.812 ns | 0.1962 ns | 0.5755 ns | 7.624 ns | 0.0057 |      24 B |
//|      AddStats_Match | 1.124 ns | 0.0297 ns | 0.0264 ns | 1.120 ns |      - |         - |
//|     AddStats_IsSome | 1.168 ns | 0.0477 ns | 0.0423 ns | 1.154 ns |      - |         - |
//|  AddStats_Extension | 1.107 ns | 0.0434 ns | 0.0406 ns | 1.106 ns |      - |         - |

    [<PlainExporter>] [<MemoryDiagnoser>] [<InProcess>]
    type StatsTest() =
        
        let token = RenderToken.Zero
        let mutable value = 0

        [<Benchmark>]
        member x.AddStats_OptionIter() =
            let cnt = value
            value <- value + 1
            token.Statistics |> Option.iter (fun stats ->
                stats.AddDrawCalls(cnt, 1)
            )

        [<Benchmark>]
        member x.AddStats_Match() =
            let cnt = value
            value <- value + 1
            match token.Statistics with
            | Some stats -> stats.AddDrawCalls(cnt, 1)
            | _ -> ()

        [<Benchmark>]
        member x.AddStats_IsSome() =
            let cnt = value
            value <- value + 1
            let stats = token.Statistics
            if stats.IsSome then
                stats.Value.AddDrawCalls(cnt, 1)

        [<Benchmark>]
        member x.AddStats_Extension() =
            let cnt = value
            value <- value + 1
            token.AddDrawCalls(cnt, 1)
