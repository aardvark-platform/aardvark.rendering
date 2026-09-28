# ShapeList.concat benchmarks

These CPU-only BenchmarkDotNet benchmarks compare the original `ShapeList.append` fold with `ShapeList.concat`. They do not initialize a rendering backend or run as unit tests.

After restoring dependencies with Paket, build and run from the repository root:

```sh
dotnet build src/Tests/Aardvark.Rendering.Tests/Aardvark.Rendering.Tests.fsproj -c Release -p:LangVersion=8.0 -warnaserror
dotnet bin/Release/net8.0/Aardvark.Rendering.Tests.dll --benchmark-shapelist --filter '*ShapeListBenchmarks*'
```

`--benchmark-shapelist --list flat` lists the benchmark methods without running them. Remaining arguments are passed to BenchmarkDotNet. Existing test execution is unchanged when this flag is absent.

Cases cover empty/singleton/two/small inputs, all-empty and sparse inputs, and 256–2,048 lists with 1–16 shapes each. `KxS` names mean K lists with S shapes per list; sparse cases include shapes only in every 32nd list. The largest **baseline** invocations allocate approximately 2 GB, so allow sufficient time and memory.

Each invocation concatenates the complete sequence. Input construction is excluded from measurement. Results are consumed by the benchmark harness. Both functions are called through direct delegates, avoiding different F# adapter/tail-call code in the comparison. MemoryDiagnoser reports bytes per complete concatenation, not per shape. Timing assertions do not belong in the unit tests; separate unit tests guard allocation scaling.

When reporting results, include absolute before/after times and allocated bytes, units, K and N, runtime/build configuration, and the ratio calculation. For independently summarized timing medians, throughput ratio is `before / after`, while elapsed-time reduction is `1 - after / before`. Do not present those as a median of paired ratios or discard unfavorable controls.
