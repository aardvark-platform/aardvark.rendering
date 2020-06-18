namespace Aardvark.Base

open System
open Microsoft.FSharp.Quotations
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

#nowarn "9"
#nowarn "51"


module private BitonicKernels =
    open FShade

    [<Literal>]
    let BitonicGroupSize = 1024
    
    [<Literal>]
    let BitonicHalfGroupSize = 512

    [<LocalSize(X = 64)>]
    let bitonicKernel<'a when 'a : unmanaged> (isGoodOrder : Expr<'a -> 'a -> bool>) (xs : 'a[]) (perm : int[]) (j : int) (jMask : int) (groupSizeP : int) (isFirst : bool) (N : int) =
        compute {
            let id = getGlobalId().X
            let i = (id &&& jMask) + id
            //let i = (id / j) * j + id 

            let off = getGlobalId().Y * N

            let ixj = 
                if isFirst then
                    i + groupSizeP - ((i &&& groupSizeP) <<< 1)
                else
                    i ||| j  

            if ixj < N then
                let i = off + i
                let ixj = off + ixj

                let pi = perm.[i]
                let pixj = perm.[ixj]
                let xi = xs.[pi]
                let xij = xs.[pixj]
                if not ((%isGoodOrder) xi xij) then 
                    perm.[ixj] <- pi
                    perm.[i] <- pixj
        }

    [<LocalSize(X = BitonicHalfGroupSize)>]
    let bitonicKernelJSteps<'a when 'a : unmanaged> (isGoodOrder : Expr<'a -> 'a -> bool>) (xs : 'a[]) (perm : int[]) (isFirst : bool) (groupSizeP : int) (jBit : int) (j : int) (N : int) =
        compute {
            let store = allocateShared<'a> BitonicGroupSize
            let permstore = allocateShared<int> BitonicGroupSize

            let off = getGlobalId().Y * N

            let baseIndex = getWorkGroupId().X * BitonicGroupSize
            let ldid = 2 * getGlobalId().X
            let rdid = ldid + 1

            let tid = getLocalId().X
            let llid = 2 * tid
            let rlid = llid + 1

            // load the elements
            if ldid < N then 
                let lp = perm.[off + ldid]
                store.[llid] <- xs.[lp]
                permstore.[llid] <- lp

            if rdid < N then 
                let rp = perm.[off + rdid]
                store.[rlid] <- xs.[rp]
                permstore.[rlid] <- rp

            barrier()

            // make all j-steps [64; 32; 16; 8; 4; 2; 1]
            let mutable j = j
            let mutable isFirst = isFirst
            while j > 0 do
                //j = 2
                // 0 -> 0
                // 1 -> 1
                // 2 -> 5
                // 3 -> 6
                let i = (tid &&& (~~~(j - 1))) + tid
                //let i = (tid / j) * j + tid 
                let ixj = 
                    if isFirst then
                        i + groupSizeP - ((i &&& groupSizeP) <<< 1)
                    else
                        i ||| j  
                
                let gi = baseIndex + i
                let gixj = baseIndex + ixj

                if gixj < N then
                    let pi = permstore.[i]
                    let pij = permstore.[ixj]

                    let xi = store.[i]
                    let xij = store.[ixj]
                    if not ((%isGoodOrder) xi xij) then 
                        store.[ixj] <- xi
                        store.[i] <- xij
                        permstore.[ixj] <- pi
                        permstore.[i] <- pij

                j <- j >>> 1

                barrier()
                isFirst <- false
                

            if ldid < N then perm.[off + ldid] <- permstore.[llid]
            if rdid < N then perm.[off + rdid] <- permstore.[rlid]
        }

    [<LocalSize(X = BitonicHalfGroupSize)>]
    let bitonicKernelKJSteps<'a when 'a : unmanaged> (isGoodOrder : Expr<'a -> 'a -> bool>) (xs : 'a[]) (perm : int[]) (maxK : int) (N : int) =
        compute {
            let store = allocateShared<'a> BitonicGroupSize
            let permstore = allocateShared<int> BitonicGroupSize
            let off = getGlobalId().Y * N

            let baseIndex = getWorkGroupId().X * BitonicGroupSize
            let ldid = 2 * getGlobalId().X
            let rdid = ldid + 1

            let tid = getLocalId().X
            let llid = 2 * tid
            let rlid = llid + 1

            // load the elements
            if ldid < N then 
                let lp = perm.[off + ldid]
                store.[llid] <- xs.[lp]
                permstore.[llid] <- lp

            if rdid < N then 
                let rp = perm.[off + rdid]
                store.[rlid] <- xs.[rp]
                permstore.[rlid] <- rp

            barrier()

            let mutable k = 2
            while k <= maxK do
                // make all j-steps [64; 32; 16; 8; 4; 2; 1]
                let mutable j = k >>> 1
                let mutable isFirst = true

                while j > 0 do
                    let i = (tid &&& (~~~(j - 1))) + tid
                    //let i = (tid / j) * j + tid 
                    let ixj = 
                        if isFirst then
                            let groupSizeP = (j<<<1)-1
                            i + groupSizeP - ((i &&& groupSizeP) <<< 1)
                        else
                            i ||| j  
                
                    let gi = baseIndex + i
                    let gixj = baseIndex + ixj

                    if gixj < N then
                        let pi = permstore.[i]
                        let pij = permstore.[ixj]

                        let xi = store.[i]
                        let xij = store.[ixj]
                        if not ((%isGoodOrder) xi xij) then 
                            store.[ixj] <- xi
                            store.[i] <- xij
                            permstore.[ixj] <- pi
                            permstore.[i] <- pij

                    j <- j >>> 1

                    barrier()
                    isFirst <- false

                k <- k <<< 1
                
            if ldid < N then perm.[off + ldid] <- permstore.[llid]
            if rdid < N then perm.[off + rdid] <- permstore.[rlid]
        }

    [<LocalSize(X = 64)>]
    let initPermKernel (perm : int[]) (N : int) =
        compute {
            let i = getGlobalId().X
            let index = getGlobalId().Y * N + i

            if i < N then
                perm.[index] <- i
        }


    let inline ceilDiv v a =
        if v % a = LanguagePrimitives.GenericZero then v / a
        else LanguagePrimitives.GenericOne + v / a



type BitonicSorter<'a when 'a : unmanaged>(runtime : IComputeRuntime, isGoodOrder : Expr<'a -> 'a -> bool>) =

    let initPerm    = runtime.CreateComputeShader BitonicKernels.initPermKernel
    let simple      = runtime.CreateComputeShader (BitonicKernels.bitonicKernel isGoodOrder)
    let stepj       = runtime.CreateComputeShader (BitonicKernels.bitonicKernelJSteps isGoodOrder)
    let stepkj      = runtime.CreateComputeShader (BitonicKernels.bitonicKernelKJSteps isGoodOrder)

    member x.Runtime = runtime
    member internal x.InitPerm = initPerm
    member internal x.Simple = simple
    member internal x.StepJ = stepj
    member internal x.StepKJ = stepkj
    
    member x.NewInstance(elements : int) : BitonicSorterInstance<'a> =
        new BitonicSorterInstance<'a>(x, elements, 1)

    member x.NewInstance(elements : int, groups : int) : BitonicSorterInstance<'a> =
        new BitonicSorterInstance<'a>(x, elements, groups)

    member x.Dispose() =
        runtime.DeleteComputeShader initPerm
        runtime.DeleteComputeShader simple
        runtime.DeleteComputeShader stepj
        runtime.DeleteComputeShader stepkj

    interface IDisposable with
        member x.Dispose() = x.Dispose()

and BitonicSorterInstance<'a when 'a : unmanaged>(parent : BitonicSorter<'a>, elements : int, groups : int) =
    let runtime = parent.Runtime

    let totalCount = elements * groups
    let tempBuffer = lazy (runtime.CreateBuffer<'a>(totalCount))
    let permBuffer = runtime.CreateBuffer<int>(totalCount)
    let bindings = System.Collections.Generic.List<IComputeShaderInputBinding>()

    let dummy : IBuffer<'a> = runtime.CreateBuffer<'a>(1)
    let mutable currentBuffer : obj = dummy :> obj

    let setInput (buffer : IBuffer<'x>) : unit =
        if (buffer :> obj) <> currentBuffer then
            currentBuffer <- buffer
            for b in bindings do
                b.["xs"] <- buffer
                b.Flush()

    let prog =
        parent.Runtime.Compile [

            // initialize the permutation-buffer
            let binding = runtime.NewInputBinding parent.InitPerm
            binding.["perm"] <- permBuffer
            binding.["N"] <- elements
            binding.Flush()
            bindings.Add binding
            yield ComputeCommand.Bind parent.InitPerm
            yield ComputeCommand.SetInput binding
            yield ComputeCommand.Dispatch (V2i(BitonicKernels.ceilDiv elements parent.InitPerm.LocalSize.X, groups))


            // 2 4 8 16 32 64 128
            let N2 = Fun.NextPowerOfTwo elements
            let maxK = min BitonicKernels.BitonicGroupSize N2
                

            // wait for data in perm and temp
            //yield ComputeCommand.Sync tempBuffer.Buffer
            yield ComputeCommand.Sync permBuffer.Buffer

            // start the initial k/j loop
            let binding = runtime.NewInputBinding parent.StepKJ
            binding.["maxK"] <- maxK
            binding.["xs"] <- dummy
            binding.["perm"] <- permBuffer
            binding.["N"] <- elements
            binding.Flush()
            bindings.Add binding
            yield ComputeCommand.Bind parent.StepKJ
            yield ComputeCommand.SetInput binding
            yield ComputeCommand.Dispatch (V2i(ceilDiv elements BitonicKernels.BitonicGroupSize, groups))
                
            let mutable k = maxK <<< 1
            while k <= N2 do
                let mutable j = k >>> 1
                let mutable isFirst = true

                // run 
                yield ComputeCommand.Bind parent.Simple
                while j > BitonicKernels.BitonicHalfGroupSize do

                
                    // wait for perm
                    yield ComputeCommand.Sync permBuffer.Buffer


                    // start the single-step
                    let binding = runtime.NewInputBinding parent.Simple
                    let groupSizeP = (j <<< 1) - 1
                    binding.["j"] <- j
                    binding.["jMask"] <- (~~~(j - 1))
                    binding.["isFirst"] <- isFirst
                    binding.["groupSizeP"] <- groupSizeP
                    binding.["xs"] <- dummy
                    binding.["perm"] <- permBuffer
                    binding.["N"] <- elements
                    binding.Flush()
                    bindings.Add binding
                    yield ComputeCommand.SetInput binding
                    yield ComputeCommand.Dispatch (V2i(ceilDiv elements parent.Simple.LocalSize.X, groups))
                    j <- j >>> 1
                    isFirst <- false


                // wait for perm
                yield ComputeCommand.Sync permBuffer.Buffer

                // start the j loop 
                let binding = runtime.NewInputBinding parent.StepJ
                let groupSizeP = (j <<< 1) - 1
                binding.["j"] <- j
                binding.["isFirst"] <- isFirst
                binding.["groupSizeP"] <- groupSizeP
                binding.["xs"] <- dummy
                binding.["perm"] <- permBuffer
                binding.["N"] <- elements
                binding.Flush()
                bindings.Add binding
                yield ComputeCommand.Bind parent.StepJ
                yield ComputeCommand.SetInput binding
                yield ComputeCommand.Dispatch (V2i(ceilDiv elements BitonicKernels.BitonicGroupSize, groups))

                k <- k <<< 1

            // wait for perm 
            yield ComputeCommand.Sync permBuffer.Buffer
        ]

    member x.Run(xs : IBuffer<'a>, perm : IBuffer<int>, queries : IQuery) : unit =
        if xs.Count <> totalCount || perm.Count <> totalCount then
            failwithf "[BitonicSorter] invalid buffer length: { values: %A, perm: %A, sort: %A }" xs.Count perm.Count totalCount

        setInput xs

        lock x (fun () ->
            runtime.Run([
                //ComputeCommand.Copy(xs, tempBuffer)
                ComputeCommand.Execute prog
                ComputeCommand.Copy(permBuffer, perm)
            ], queries)
        )

    member x.Run(xs : IBuffer<'a>, perm : IBuffer<int>) =
        x.Run(xs, perm, Queries.empty)

    member x.Run(xs : 'a[], perm : int[], queries : IQuery) : unit =
        if xs.Length <> totalCount  || perm.Length <> totalCount then
            failwithf "[BitonicSorter] invalid buffer length: { values: %A, perm: %A, sort: %A }" xs.Length perm.Length totalCount

        let tempBuffer = tempBuffer.Value
        setInput tempBuffer

        lock x (fun () ->
            runtime.Run([
                ComputeCommand.Copy(xs, tempBuffer)
                ComputeCommand.Sync(tempBuffer.Buffer)
                ComputeCommand.Execute prog
                ComputeCommand.Copy(permBuffer, perm)
            ], queries)
        )

    member x.Run(xs : 'a[], perm : int[]) =
        x.Run(xs, perm, Queries.empty)

    member x.Dispose() : unit =
        if tempBuffer.IsValueCreated then tempBuffer.Value.Dispose()
        dummy.Dispose()
        permBuffer.Dispose()
        for b in bindings do b.Dispose()
        bindings.Clear()
        prog.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AbstractClass; Sealed; Extension>]
type BitonicSorterExtensions private() =

    [<Extension>]
    static member CreatePermutation(sorter : BitonicSorter<'a>, input : 'a[]) : int[] =
        use i = new BitonicSorterInstance<'a>(sorter, input.Length, 1)
        let perm = Array.zeroCreate input.Length
        i.Run(input, perm)
        perm

    [<Extension>]
    static member CreatePermutation(sorter : BitonicSorter<'a>, input : 'a[], perm : int[]) : unit =
        use i = new BitonicSorterInstance<'a>(sorter, input.Length, 1)
        i.Run(input, perm)

    [<Extension>]
    static member CreatePermutation(sorter : BitonicSorter<'a>, input : IBuffer<'a>, perm : IBuffer<int>) : unit =
        use i = new BitonicSorterInstance<'a>(sorter, input.Count, 1)
        i.Run(input, perm)