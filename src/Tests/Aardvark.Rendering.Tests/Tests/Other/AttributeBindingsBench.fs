namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering.GL
open BenchmarkDotNet.Attributes
open System.Reflection

//BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.4651/22H2/2022Update)
//AMD Ryzen 9 7900, 1 CPU, 24 logical and 12 physical cores
//.NET SDK 8.0.400
//  [Host] : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI DEBUG

//Job=InProcess  Toolchain=InProcessEmitToolchain  

//| Method               | Mean       | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
//|--------------------- |-----------:|---------:|---------:|------:|-------:|----------:|------------:|
//| AttributeBindings    | 4,959.4 ns | 27.96 ns | 24.78 ns |  1.00 | 1.1597 |     19 KB |        1.00 |
//| AttributeBindingsNew |   132.7 ns |  0.35 ns |  0.32 ns |  0.03 | 0.0880 |   1.44 KB |        0.08 |

[<PlainExporter>] [<MemoryDiagnoser>] [<InProcess>]
type AttributeBindingsBench() =
        
    let attributes = 
        [|
            struct(0, Attribute.Buffer { Type = typeof<C4f>; Frequency = AttributeFrequency.PerInstances 1; Format = VertexAttributeFormat.Default; Stride = 0; Offset = 0; Buffer = new Buffer(Unchecked.defaultof<_>, 0n, 0)  })
            struct(1, Attribute.Buffer { Type = typeof<int32>; Frequency = AttributeFrequency.PerInstances 1; Format = VertexAttributeFormat.Default; Stride = 0; Offset = 0; Buffer = new Buffer(Unchecked.defaultof<_>, 0n, 0)  })
            struct(2, Attribute.Buffer { Type = typeof<M44f>; Frequency = AttributeFrequency.PerInstances 1; Format = VertexAttributeFormat.Default; Stride = 0; Offset = 0; Buffer = new Buffer(Unchecked.defaultof<_>, 0n, 0)  })
            struct(6, Attribute.Buffer { Type = typeof<M44f>; Frequency = AttributeFrequency.PerInstances 1; Format = VertexAttributeFormat.Default; Stride = 0; Offset = 0; Buffer = new Buffer(Unchecked.defaultof<_>, 0n, 0)  })
            struct(10, Attribute.Buffer { Type = typeof<V3f>; Frequency = AttributeFrequency.PerVertex; Format = VertexAttributeFormat.Default; Stride = 0; Offset = 0; Buffer = new Buffer(Unchecked.defaultof<_>, 0n, 0)  })
            struct(11, Attribute.Buffer { Type = typeof<V3f>; Frequency = AttributeFrequency.PerVertex; Format = VertexAttributeFormat.Default; Stride = 0; Offset = 0; Buffer = new Buffer(Unchecked.defaultof<_>, 0n, 0)  })
        |]

    let bindingsFun = Assembly.GetAssembly(typeof<Aardvark.Rendering.GL.VertexBufferBinding>)
                        .GetType("Aardvark.Rendering.GL.PointerContextExtensions")
                        .GetNestedType("Helpers", BindingFlags.Static ||| BindingFlags.NonPublic)
                        .GetNestedType("Attribute", BindingFlags.Static ||| BindingFlags.NonPublic)
                        .GetMethod("bindings", BindingFlags.Static ||| BindingFlags.NonPublic)

    [<Benchmark>]
    member x.AttributeBindings() =
        //PointerContextExtensions.Helpers.Attribute.bindings attributes
        bindingsFun.Invoke(null, [| attributes |])


