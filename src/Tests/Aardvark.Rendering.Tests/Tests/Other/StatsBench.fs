module StatsBench
    open BenchmarkDotNet.Attributes
    open Aardvark.Rendering

    //|              Method |     Mean |     Error |    StdDev |   Median |  Gen 0 | Allocated |
    //|-------------------- |---------:|----------:|----------:|---------:|-------:|----------:|
    //| AddStats_OptionIter | 7.812 ns | 0.1962 ns | 0.5755 ns | 7.624 ns | 0.0057 |      24 B |
    //|      AddStats_Match | 1.124 ns | 0.0297 ns | 0.0264 ns | 1.120 ns |      - |         - |
    //|     AddStats_IsSome | 1.168 ns | 0.0477 ns | 0.0423 ns | 1.154 ns |      - |         - |
    //|  AddStats_Extension | 1.107 ns | 0.0434 ns | 0.0406 ns | 1.106 ns |      - |         - |

    // After rework with nullable member:
    // | Method              | Mean      | Error     | StdDev    | Allocated |
    // |-------------------- |----------:|----------:|----------:|----------:|
    // | AddStats_OptionIter | 0.8676 ns | 0.4124 ns | 0.0226 ns |         - |
    // | AddStats_Match      | 0.8690 ns | 0.0574 ns | 0.0031 ns |         - |
    // | AddStats_IsSome     | 0.8944 ns | 0.2229 ns | 0.0122 ns |         - |
    // | AddStats_Extension  | 0.5627 ns | 0.3937 ns | 0.0216 ns |         - |

    [<CLIMutable>]
    type OldRenderToken =
        {
            Queries    : IQuery list
            Statistics : FrameStatistics option
        }

    [<PlainExporter>] [<MemoryDiagnoser>]
    type StatsTest() =
        let tokenNew = RenderToken.Zero
        let token = { Queries = []; Statistics = Some <| FrameStatistics() }
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
            tokenNew.AddDrawCalls(cnt, 1)