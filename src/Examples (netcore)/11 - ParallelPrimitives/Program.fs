open Aardvark.Base
open Aardvark.Rendering
open Aardvark.GPGPU
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Rendering.Vulkan
open FSharp.Quotations


// This example illustrates how to use Aardvark.GPGPU primitives and compute shaders
// to do simple parallel programming.
module Shader =
    open FShade

    [<LocalSize(X = 64)>]
    let resolve (n : int) (src : int[]) (targets : int[]) (result : int[]) =
        compute {
            let id = getGlobalId().X
            if id < n then
                // reconstruct the bit from the scan-data
                let r = targets.[id]
                let l = if id > 0 then targets.[id-1] else 0
                let bit = r - l

                // if the bit was 1 then the output-index is (r-1)
                if bit > 0 then result.[r - 1] <- src.[id]
        }

// a simple utility for calculating ceil(v / d) as int
let ceilDiv (v : int) (d : int) =
    if v % d = 0 then v / d
    else 1 + v / d

let parallelFilter<'a when 'a : unmanaged> (par : ParallelPrimitives) (arr : 'a[]) (predicate : Expr<'a -> bool>) =
    let runtime = par.Runtime

    // create buffers holding the data
    use input   = runtime.CreateBuffer<'a>(arr)
    use bits    = runtime.CreateBuffer<int>(input.Count)
    use result  = runtime.CreateBuffer<'a>(arr.Length)

    // compile some tools we need for our computation
    use mapper  = par.CompileMap(<@ fun i e -> if (%predicate) e then 1 else 0 @>, input, bits)
    use scanner = par.CompileScan(<@ (+) @>, bits, bits)

    // compile our custom resolve-shader and setup its inputs
    let resolveShader = runtime.CreateComputeShader Shader.resolve
    use resolveInputs = runtime.CreateInputBinding resolveShader
    resolveInputs.["src"] <- input
    resolveInputs.["n"] <- input.Count
    resolveInputs.["targets"] <- bits
    resolveInputs.["result"] <- result
    resolveInputs.Flush()

    // create the CPU arrays for downloading data
    #if DEBUG
    let bitsCPU = Array.zeroCreate arr.Length
    let bitsumCPU = Array.zeroCreate arr.Length
    #endif
    let overallCount = Array.zeroCreate 1

    // run the overall command
    runtime.Run [
        // create bits for all entries: (0: false, 1: true)
        ComputeCommand.Execute mapper
        ComputeCommand.Sync(bits.Buffer,ResourceAccess.ShaderWrite,ResourceAccess.ShaderRead)

        // download the bits (just for showing the values)
        #if DEBUG
        ComputeCommand.Download(bits, bitsCPU)
        ComputeCommand.Sync(bits.Buffer,ResourceAccess.TransferRead,ResourceAccess.ShaderRead)
        #endif 

        // perform an inclusive scan on the bits (since the bits are no longer needed we can use the identical buffer as output here)
        ComputeCommand.Execute scanner
        ComputeCommand.Sync(bits.Buffer,ResourceAccess.ShaderWrite,ResourceAccess.ShaderRead)

        // download the bits (just for showing the values)
        #if DEBUG
        ComputeCommand.Download(bits, bitsumCPU)
        #endif

        // download the last entry from bits (the total number of elements in output)
        ComputeCommand.Download(bits.[bits.Count - 1 .. bits.Count - 1], overallCount)

        // resolve (write) all the valid entries to their respective output-index
        ComputeCommand.Bind resolveShader
        ComputeCommand.SetInput resolveInputs
        ComputeCommand.Dispatch (ceilDiv (int input.Count) resolveShader.LocalSize.X)

    ]

    // download and print the resulting data
    let overallCount = overallCount.[0]
    let resultCPU = result.[0..overallCount-1].Download()

    // cleanup...
    runtime.DeleteComputeShader resolveShader

    #if DEBUG
    Log.line "data:   %A" arr
    Log.line "bits:   %A" bitsCPU
    Log.line "bitsum: %A" bitsumCPU
    #endif
    Log.line "result: %A" resultCPU


[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()

    // create an application
    use app = new HeadlessVulkanApplication(false)

    // and an instance of our parallel primitives
    let primitives = new ParallelPrimitives(app.Runtime)

    // run the filter
    let testArr = [| 100; 5; 10; 20; 1000; 3 |]
    parallelFilter primitives testArr <@ fun v -> v <= 20 @>

    0
