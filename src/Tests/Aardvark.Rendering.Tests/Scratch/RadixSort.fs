module RadixSortTest

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL
open Aardvark.Application
open Aardvark.Application.Slim

let run() =

    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()

    // create an OpenGL/Vulkan application. Use the use keyword (using in C#) in order to
    // properly dipose resources on shutdown...
    use app = new OpenGlApplication()

    let runtime = app.Runtime :> IRuntime
    use sorter = new RadixSort(runtime)

    let isSorted (test : 'a[]) =
        if test.Length > 0 then
            let mutable i0 = 0
            let mutable d0 = test.[0]
            let mutable i1 = 1
            let mutable sorted = true
            while sorted && i1 < test.Length do
                let d1 = test.[i1]
                if d0 > d1 then sorted <- false
                i0 <- i1
                d0 <- d1
                i1 <- i1 + 1
            sorted
                            
        else
            true

    let rand = RandomSystem()
    for e in 16 .. 22 do
        let cnt = 1 <<< e
        let swPerm = System.Diagnostics.Stopwatch()
        let swSort = System.Diagnostics.Stopwatch()
        for a in 1 .. 1 do
            let data = Array.init (cnt + rand.UniformInt 256 - 128) (fun _ -> rand.UniformUInt() |> int)

            use a = runtime.CreateBuffer data
            use b = runtime.CreateBuffer<int> data.Length
            swPerm.Restart()
            sorter.CreatePermutation(a, b)
            swPerm.Stop()

            swSort.Restart()
            sorter.SortInPlace(a)
            swSort.Stop()

            let perm = b.Download()
            let test = perm |> Array.map (fun i -> data.[i])
            let test2 = a.Download()
            let sorted = isSorted test && isSorted test2

            if not sorted then
                Log.warn "bad int"
            else
                Log.line "good int %d (%A/%A)" data.Length swSort.MicroTime swPerm.MicroTime


            
            let data = Array.init (cnt + rand.UniformInt 256 - 128) (fun _ -> rand.Gaussian() |> float32)

            use a = runtime.CreateBuffer data
            use b = runtime.CreateBuffer<int> data.Length
            swPerm.Restart()
            sorter.CreatePermutation(a, b)
            swPerm.Stop()

            swSort.Restart()
            sorter.SortInPlace(a)
            swSort.Stop()

            let perm = b.Download()
            let test = perm |> Array.map (fun i -> data.[i])
            let test2 = a.Download()
            let sorted = isSorted test && isSorted test2

            if not sorted then
                Log.warn "bad float"
            else
                Log.line "good float %d (%A/%A)" data.Length swSort.MicroTime swPerm.MicroTime

                
            let data = Array.init (cnt + rand.UniformInt 256 - 128) (fun _ -> rand.UniformUInt())

            use a = runtime.CreateBuffer data
            use b = runtime.CreateBuffer<int> data.Length
            swPerm.Restart()
            sorter.CreatePermutation(a, b)
            swPerm.Stop()

            swSort.Restart()
            sorter.SortInPlace(a)
            swSort.Stop()

            let perm = b.Download()
            let test = perm |> Array.map (fun i -> data.[i])
            let test2 = a.Download()
            let sorted = isSorted test && isSorted test2

            if not sorted then
                Log.warn "bad uint"
            else
                Log.line "good uint %d (%A/%A)" data.Length swSort.MicroTime swPerm.MicroTime
