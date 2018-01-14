open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Rendering.Vulkan
open FSharp.Quotations

// This example illustrates how to use Aardvark.GPGPU primitives and compute shaders
// to do simple parallel programming.

module Shader =
    open FShade

    [<LocalSize(X = 64)>]
    let resolve (n : int) (src : int[]) (bits : int[]) (targets : int[]) (result : int[]) =
        compute {
            let id = getGlobalId().X
            if id < n then
                let inputValue = src.[id]
                let targetLocation = targets.[id]-1
                if bits.[id] = 1 then
                    result.[targetLocation] <- inputValue
        }

let parallelFilter<'a when 'a : unmanaged> (runtime : IRuntime) (arr : 'a[]) (predicate : Expr<'a -> bool>) =
    
    let par = ParallelPrimitives(runtime)
    let input = runtime.CreateBuffer<'a>(arr)
    let bits = runtime.CreateBuffer<int>(input.Count)
    let bitsum = runtime.CreateBuffer<int>(input.Count)
    let result = runtime.CreateBuffer<'a>(arr.Length)

    let mapper = par.CompileMap(<@ fun i e -> if (%predicate) e then 1 else 0 @>, input, bits)
    let scanner = par.CompileScan(<@ (+) @>, bits, bitsum)

    let resolveTargetShader = runtime.CreateComputeShader Shader.resolve
    let targetWrite = runtime.NewInputBinding resolveTargetShader
    targetWrite.["src"] <- input
    targetWrite.["n"] <- input.Count
    targetWrite.["bits"] <- bits
    targetWrite.["targets"] <- bitsum
    targetWrite.["result"] <- result
    targetWrite.Flush()
    let ceilDiv (v : int) (d : int) =
        if v % d = 0 then v / d
        else 1 + v / d

    let commands = [
        ComputeCommand.Execute mapper
        ComputeCommand.Sync(bits.Buffer,BufferAccess.ShaderWrite,BufferAccess.ShaderRead)
        ComputeCommand.Execute scanner
        ComputeCommand.Sync(bitsum.Buffer,BufferAccess.ShaderWrite,BufferAccess.ShaderRead)
        ComputeCommand.Bind(resolveTargetShader)
        ComputeCommand.SetInput targetWrite
        ComputeCommand.Dispatch (ceilDiv (int input.Count) 64)
    ]

    let program = runtime.Compile commands
    program.Run()

    let bitsCPU =  bits.Download()
    printfn "bits: %A" bitsCPU
    let bitsumCPU = bitsum.Download()
    printfn "bitsum: %A" bitsumCPU
    let max = bitsumCPU.[bitsumCPU.Length-1]
    let result2 = result.[0..max-1].Download()
    printfn "result: %A" result2
    ()

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    use app = new HeadlessVulkanApplication(false)
    
    let testArr = [| 100; 5; 10; 20; 1000; 3 |]
    parallelFilter app.Runtime  testArr <@ fun v -> v <= 20 @>

    0
