open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

// This example illustrates how to create a simple render window. 
// In contrast to the rest of the examples (beginning from 01-Triangle), we use
// the low level application API for creating a window and attaching 
// mouse and keyboard inputs.
// In your application you most likely want to use this API instead of the more abstract
// show/window computation expression builders (which reduces code duplication
// in this case) to setup applications.
module RadixSort4Shader =
    open FShade

    [<LocalSize(X = 64)>]
    let classify (cnt : int) (shift : int) (data : uint32[]) (dst : V4i[]) =
        compute {
            let id = getGlobalId().X
            if id < cnt then
                let v = (data.[id] >>> shift) &&& 3u

                match v with
                | 0u -> dst.[id] <- V4i.OOOI
                | 1u -> dst.[id] <- V4i.OOII
                | 2u -> dst.[id] <- V4i.OIII
                | _  -> dst.[id] <- V4i.IIII

        }
        
    [<LocalSize(X = 64)>]
    let shuffle (cnt : int) (shift : int) (src : uint32[]) (dst : uint32[]) (index : V4i[]) =
        compute {
            let id = getGlobalId().X
            if id < cnt then
                let k = src.[id]
                let v = (k >>> shift) &&& 3u
                let idx = if id > 0 then index.[id-1] else V4i.OOOO

                match v with
                | 0u -> dst.[idx.X] <- k
                | 1u -> dst.[idx.Y] <- k
                | 2u -> dst.[idx.Z] <- k
                | _  -> dst.[idx.W] <- k
                
        }
        
    [<GLSLIntrinsic("atomicAdd({0}, {1})")>]
    let atomicAdd (r : ref<uint32>) (v : uint32) : unit = onlyInShaderCode "atomicAdd"

    [<LocalSize(X = 256)>]
    let computeHistogram8Local (cnt : int) (shift : int) (src : uint32[]) (dst : uint32[]) =
        compute {
            let l = allocateShared<uint32> 256
            let lid = getLocalId().X
            l.[lid] <- 0u
            barrier()

            let id = getGlobalId().X
            if id < cnt then
                let bin = (src.[id] >>> shift) &&& 255u |> int
                atomicAdd &&l.[bin] 1u

            barrier()

            atomicAdd &&dst.[lid] l.[lid]
        }
        
    [<LocalSize(X = 64)>]
    let computeHistogram8 (cnt : int) (shift : int) (src : uint32[]) (dst : uint32[]) =
        compute {
            let id = getGlobalId().X
            if id < cnt then
                let bin = (src.[id] >>> shift) &&& 255u |> int
                atomicAdd &&dst.[bin] 1u

        }

        
    [<LocalSize(X = 64)>]
    let computeHistogram16 (cnt : int) (shift : int) (src : uint32[]) (dst : uint32[]) =
        compute {
            let id = getGlobalId().X
            if id < cnt then
                let bin = (src.[id] >>> shift) &&& 65535u |> int
                atomicAdd &&dst.[bin] 1u

        }


type RadixSort4(runtime : IRuntime, cnt : int) =
    
    let prim = ParallelPrimitives runtime
    let temp = runtime.CreateBuffer<uint32>(cnt)
    let buffer = runtime.CreateBuffer<V4i>(cnt)

    let scan = prim.CompileScan(<@ fun (a : V4i) (b : V4i) -> a + b @>, buffer, buffer)
    let classify = runtime.CreateComputeShader RadixSort4Shader.classify
    let shuffle = runtime.CreateComputeShader RadixSort4Shader.shuffle

    member x.Compile (src : IBuffer<uint32>, dst : IBuffer<uint32>) =
        
        let classifyInputs =
            Array.init 16 (fun i ->
                let data =
                    if i % 2 = 0 then src
                    else temp

                let classifyInput0 = runtime.NewInputBinding classify
                classifyInput0.["cnt"] <- cnt
                classifyInput0.["shift"] <- (i <<< 1)
                classifyInput0.["data"] <- data.Buffer
                classifyInput0.["dst"] <- buffer.Buffer
                classifyInput0.Flush()

                classifyInput0
            )

            
        let shuffleInputs =
            Array.init 16 (fun i ->
                let src =
                    if i % 2 = 0 then src
                    else temp
                let dst =
                    if i = 15 then dst
                    elif i % 2 = 0 then temp
                    else src

                let shuffleInput0 = runtime.NewInputBinding shuffle
                shuffleInput0.["cnt"] <- cnt
                shuffleInput0.["shift"] <- 0
                shuffleInput0.["src"] <- src.Buffer
                shuffleInput0.["dst"] <- dst.Buffer
                shuffleInput0.["index"] <- buffer.Buffer
                shuffleInput0.Flush()
                shuffleInput0
            )
        


        runtime.Compile [
            for i in 0 .. classifyInputs.Length - 1 do
                ComputeCommand.Bind classify
                ComputeCommand.SetInput classifyInputs.[i]
                ComputeCommand.Dispatch(ceilDiv cnt 64)
                
                ComputeCommand.Sync buffer.Buffer

                ComputeCommand.Execute scan
                
                ComputeCommand.Sync buffer.Buffer

                ComputeCommand.Bind shuffle
                ComputeCommand.SetInput shuffleInputs.[i]
                ComputeCommand.Dispatch(ceilDiv cnt 64)


        ]





[<EntryPoint>]
let main argv =

    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()

    // create an OpenGL/Vulkan application. Use the use keyword (using in C#) in order to
    // properly dipose resources on shutdown...
    use app = new VulkanApplication()
    let runtime = app.Runtime :> IRuntime

    let cnt = 1 <<< 20
    
    let rand = RandomSystem()
    let data = Array.init cnt (fun i -> uint32 i)//rand.UniformUInt())
    let a = runtime.CreateBuffer(data)
    let b = runtime.CreateBuffer<uint32>(data.Length)


    let h = runtime.CreateBuffer<uint32>(65536)

    let computeHisto = runtime.CreateComputeShader RadixSort4Shader.computeHistogram16
    let input = runtime.NewInputBinding computeHisto


    input.["cnt"] <- cnt
    input.["shift"] <- 0
    input.["src"] <- a.Buffer
    input.["dst"] <- h.Buffer
    input.Flush()

    let prog = 
        runtime.Compile [
            ComputeCommand.Set(h, 0u)
            ComputeCommand.Bind computeHisto
            ComputeCommand.SetInput input
            ComputeCommand.Dispatch (ceilDiv cnt computeHisto.LocalSize.X)
        ]

    prog.Run()
    let histo = h.Download()

    let test =
        let arr = Array.zeroCreate 65536
        for v in data do
            let bin = (v &&& 65535u) |> int
            arr.[bin] <- arr.[bin] + 1u
        arr


    if histo <> test then
        Log.warn "bad"

    //Log.start "histo"
    //for i in 0 .. 65535 do
    //    if test.[i] <> 0u then
    //        Log.line "0x%02X: %d" i test.[i]
    //Log.stop()

    let iter = 1000
    let mutable sum = 0.0
    let mutable sumSq = 0.0
    for i in 1 .. iter do
        let iter = 100
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. iter do
            prog.Run()
        sw.Stop()

        let t = sw.MicroTime / iter
        sum <- sum + t.TotalSeconds
        sumSq <- sum + sqr t.TotalSeconds

        Log.line "%d: %A" i t


    Log.line "avg: %A" (MicroTime.FromSeconds sum / iter)

    exit 0




    let s = RadixSort4(runtime, cnt)
    let p = s.Compile(a, b)
    
    for i in 1 .. 2 do
        p.Run()

    let sw = System.Diagnostics.Stopwatch()
    let mutable iter = 0
    while sw.Elapsed.TotalSeconds < 5.0 do
        sw.Start()
        p.Run()
        sw.Stop()
        iter <- iter + 1

    Log.line "%A" (sw.MicroTime / iter)

    let s2 = RadixSort runtime

    for i in 1 .. 2 do
        s2.Sort(a, b)

    let sw = System.Diagnostics.Stopwatch()
    let mutable iter = 0
    while sw.Elapsed.TotalSeconds < 5.0 do
        sw.Start()
        s2.Sort(a, b)
        sw.Stop()
        iter <- iter + 1

    Log.line "%A" (sw.MicroTime / iter)

    


    exit 0
    0
