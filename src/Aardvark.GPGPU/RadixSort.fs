namespace Aardvark.Base

open Aardvark.Base
open Aardvark.Rendering

type private RadixSortMode =
    | UInt = 0
    | Int = 1
    | Float = 2

module private RadixSortShaders =
    open FShade



    let NUM_SMS = 16
    let NUM_THREADS_PER_SM = 192
    let NUM_THREADS_PER_BLOCK = 64
    let NUM_BLOCKS = (NUM_THREADS_PER_SM / NUM_THREADS_PER_BLOCK) * NUM_SMS
    let RADIX = 8                                                        // Number of bits per radix sort pass
    let RADICES = 1 <<< RADIX                                             // Number of radices
    let RADIXMASK = uint32 (RADICES - 1)                                          // Mask for each radix sort pass

    let RADIXTHREADS = 16                                                // Number of threads sharing each radix counter
    let RADIXGROUPS = NUM_THREADS_PER_BLOCK / RADIXTHREADS               // Number of radix groups per CTA
    let TOTALRADIXGROUPS = NUM_BLOCKS * RADIXGROUPS                      // Number of radix groups for each radix
    let SORTRADIXGROUPS = TOTALRADIXGROUPS * RADICES                     // Total radix count
    let GRFELEMENTS = (NUM_THREADS_PER_BLOCK / RADIXTHREADS) * RADICES
    let GRFSIZE = GRFELEMENTS * sizeof<uint32>

    // Prefix sum variables
    let PREFIX_NUM_THREADS_PER_SM = NUM_THREADS_PER_SM
    let PREFIX_NUM_THREADS_PER_BLOCK = PREFIX_NUM_THREADS_PER_SM
    let PREFIX_NUM_BLOCKS = (PREFIX_NUM_THREADS_PER_SM / PREFIX_NUM_THREADS_PER_BLOCK) * NUM_SMS
    let PREFIX_BLOCKSIZE = SORTRADIXGROUPS / PREFIX_NUM_BLOCKS
    let PREFIX_GRFELEMENTS = PREFIX_BLOCKSIZE + 2 * PREFIX_NUM_THREADS_PER_BLOCK
    let PREFIX_GRFSIZE = PREFIX_GRFELEMENTS * sizeof<uint32>

    // Shuffle variables
    let SHUFFLE_GRFOFFSET = RADIXGROUPS * RADICES
    let SHUFFLE_GRFELEMENTS = SHUFFLE_GRFOFFSET + PREFIX_NUM_BLOCKS
    let SHUFFLE_GRFSIZE = SHUFFLE_GRFELEMENTS * sizeof<uint32>


    [<ReflectedDefinition;Inline>]
    let floatFlip (u : uint32) : uint32 =   
        let mask = - int(u >>> 31) ||| 0x80000000;
        u ^^^ uint32 mask

    [<LocalSize(X = 64)>]
    let radixSum 
        (keys : uint32[]) (keyOffset : int) (keyStride : int) 
        (elements : int) 
        (elements_rounded_to_3072 : int)
        (shift : int) 
        (dRadixSum : uint32[]) 
        (mode : RadixSortMode) =
        compute {
            let sRadixSum = allocateShared<uint32> GRFELEMENTS

            let threadIdx = getLocalId().X
            let blockIdx = getWorkGroupId().X

            let mutable pos = threadIdx;
            while pos < GRFELEMENTS do
                sRadixSum.[pos] <- 0u
                pos <- pos + NUM_THREADS_PER_BLOCK

            let mutable tmod = threadIdx % RADIXTHREADS
            let mutable tpos = threadIdx / RADIXTHREADS

            let element_fraction = elements_rounded_to_3072 / TOTALRADIXGROUPS

            pos <- (blockIdx * RADIXGROUPS + tpos) * element_fraction
            let e = pos + element_fraction
            pos <- pos + tmod
            barrier()

            let mutable kvpk = 0u
            while pos < e do
                let k = 0u
                if pos < elements then
                    let pIndex = pos
                    kvpk <- keys.[keyOffset + keyStride * pIndex]
                else
                    kvpk <- 0u

                let mutable key = kvpk
                match mode with
                | RadixSortMode.Float -> key <- floatFlip key
                | RadixSortMode.Int -> key <- key ^^^ (1u <<< 31)
                | _ -> ()
                key <- (key >>> shift) &&& RADIXMASK

                let mutable p = key * uint32 RADIXGROUPS
                let ppos = p + uint32 tpos
                
                Preprocessor.unroll()
                for i in 0 .. 15 do
                    if tmod = i && pos < elements then
                        sRadixSum.[int ppos] <- sRadixSum.[int ppos] + 1u
                    barrier()
           
                pos <- pos + RADIXTHREADS

            barrier()

            let offset = blockIdx * RADIXGROUPS
            let mutable row    = threadIdx / RADIXGROUPS
            let column = threadIdx % RADIXGROUPS

            while row < RADICES do
                dRadixSum.[offset + row * TOTALRADIXGROUPS + column] <- sRadixSum.[row * RADIXGROUPS + column]
                row <- row + NUM_THREADS_PER_BLOCK / RADIXGROUPS
        }
            
    [<LocalSize(X = 192)>]
    let radixPrefixSum 
        (dRadixSum : uint32[])
        (dRadixBlockSum : uint32[]) =
        compute {
            let sRadixSum = allocateShared<uint32> PREFIX_GRFELEMENTS
            let threadIdx = getLocalId().X
            let blockIdx = getWorkGroupId().X

                
            let mutable brow       = blockIdx * (RADICES / PREFIX_NUM_BLOCKS)
            let mutable drow       = threadIdx / TOTALRADIXGROUPS // In default parameterisation this is always 0
            let mutable dcolumn    = threadIdx % TOTALRADIXGROUPS; // And similarly this is always the same as threadIdx   
            let mutable dpos       = (brow + drow) * TOTALRADIXGROUPS + dcolumn
            let mutable end_       = ((blockIdx + 1) * (RADICES / PREFIX_NUM_BLOCKS)) * TOTALRADIXGROUPS
            // Shared mem addressing
            let mutable srow       = threadIdx / (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)
            let mutable scolumn    = threadIdx % (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)
            let mutable spos       = srow * (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1) + scolumn

            while dpos < end_ do
                sRadixSum.[spos] <- dRadixSum.[dpos]

                spos <- spos + (PREFIX_NUM_THREADS_PER_BLOCK / (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)) *  (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1)
                dpos <- dpos + (TOTALRADIXGROUPS / PREFIX_NUM_THREADS_PER_BLOCK) * TOTALRADIXGROUPS

            barrier()

            let mutable pos = threadIdx * (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1)
            end_ <- pos + (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)
            let mutable sum = 0u
            while pos < end_ do
                sum <- sum + sRadixSum.[pos]
                sRadixSum.[pos] <- sum
                pos <- pos + 1

            barrier()

            let mutable m = (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1)
            pos <- threadIdx  * (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1) + (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)
            sRadixSum.[pos] <- sRadixSum.[pos - 1];
            barrier()

            while (m < PREFIX_NUM_THREADS_PER_BLOCK * (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1)) do
                let p = pos - m
                let t = if p > 0 then sRadixSum.[p] else 0u
                barrier()
                sRadixSum.[pos] <- sRadixSum.[pos] + t
                barrier()
                m <- m <<< 1
            barrier()

            pos <-threadIdx * (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1)
            end_ <- pos + (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)
            let p   = pos - 1
            sum <- if (p > 0) then sRadixSum.[p] else 0u

            while pos < end_ do
                sRadixSum.[pos] <- sRadixSum.[pos] + sum
                pos <- pos + 1

            barrier()

            brow       <- blockIdx * (RADICES / PREFIX_NUM_BLOCKS)
            drow       <- threadIdx / TOTALRADIXGROUPS
            dcolumn    <- threadIdx % TOTALRADIXGROUPS
            srow       <- threadIdx / (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)
            scolumn    <- threadIdx % (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)
            dpos       <- (brow + drow) * TOTALRADIXGROUPS + dcolumn + 1
            spos       <- srow * (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1) + scolumn
            end_       <- ((blockIdx + 1) * RADICES / PREFIX_NUM_BLOCKS) * TOTALRADIXGROUPS


            while dpos < end_ do
                dRadixSum.[dpos] <- sRadixSum.[spos]
                dpos <- dpos + (TOTALRADIXGROUPS / PREFIX_NUM_THREADS_PER_BLOCK) * TOTALRADIXGROUPS
                spos <- spos + (PREFIX_NUM_THREADS_PER_BLOCK / (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK)) * (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1)
                    
            if threadIdx = 0 then
                dRadixBlockSum.[blockIdx] <- sRadixSum.[PREFIX_NUM_THREADS_PER_BLOCK * (PREFIX_BLOCKSIZE / PREFIX_NUM_THREADS_PER_BLOCK + 1) - 1]
                dRadixSum.[blockIdx * PREFIX_BLOCKSIZE] <- 0u
        }
            
    [<LocalSize(X = 64)>]
    let radixAddOffsetsAndShuffle
        (inputKeys : uint32[]) (inputKeyOffset : int) (inputKeyStride : int) 
        (inputValues : uint32[]) (inputValueOffset : int) (inputValueStride : int)
        (outputKeys : uint32[]) (outputKeyOffset : int) (outputKeyStride : int) 
        (outputValues : uint32[]) (outputValueOffset : int) (outputValueStride : int)
        (dRadixBlockSum : uint32[]) (dRadixSum : uint32[])
        (elements : int) (elements_rounded_to_3072 : int) (shift : int) (mode : RadixSortMode) =
        compute {
            let sRadixSum = allocateShared<uint32> SHUFFLE_GRFELEMENTS
            let threadIdx = getLocalId().X
            let blockIdx = getWorkGroupId().X

            if threadIdx = 0 then
                sRadixSum.[SHUFFLE_GRFOFFSET] <- 0u

            if threadIdx < PREFIX_NUM_BLOCKS-1 then
                sRadixSum.[SHUFFLE_GRFOFFSET + threadIdx + 1] <- dRadixBlockSum.[threadIdx]
            barrier()

            let mutable pos = threadIdx
            let mutable n = 1
            while n < PREFIX_NUM_BLOCKS do
                let ppos = pos - n
                let t0 = if pos < PREFIX_NUM_BLOCKS && ppos >= 0 then sRadixSum.[SHUFFLE_GRFOFFSET + ppos] else 0u
                barrier()
                if pos < PREFIX_NUM_BLOCKS then
                    sRadixSum.[SHUFFLE_GRFOFFSET + pos] <- sRadixSum.[SHUFFLE_GRFOFFSET + pos] + t0
                barrier()
                n <- n <<< 1
                    
            let mutable row    = threadIdx / RADIXGROUPS
            let mutable column = threadIdx % RADIXGROUPS
            let mutable spos   = row * RADIXGROUPS + column
            let mutable dpos   = row * TOTALRADIXGROUPS + column + blockIdx * RADIXGROUPS
            while spos < SHUFFLE_GRFOFFSET do
                sRadixSum.[spos] <- dRadixSum.[dpos] + sRadixSum.[SHUFFLE_GRFOFFSET + dpos / (TOTALRADIXGROUPS * RADICES / PREFIX_NUM_BLOCKS)]
                spos <- spos + NUM_THREADS_PER_BLOCK
                dpos <- dpos + (NUM_THREADS_PER_BLOCK / RADIXGROUPS) * TOTALRADIXGROUPS

            barrier()

            let  element_fraction  =   elements_rounded_to_3072 / TOTALRADIXGROUPS;
            let tmod   =   threadIdx % RADIXTHREADS;
            let tpos   =   threadIdx / RADIXTHREADS;

            pos <- (blockIdx * RADIXGROUPS + tpos) * element_fraction
            let mutable end_ = pos + element_fraction //(blockIdx * RADIXGROUPS + tpos + 1) * element_fraction;
            pos <- pos + tmod;

            barrier()

            while pos < end_ do
                let mutable kvpk = 0u
                let mutable kvpv = 0u
                if pos < elements then  
                    let pIndex = pos
                    kvpk <- inputKeys.[inputKeyOffset + inputKeyStride*pIndex]
                    kvpv <- if shift > 0 then inputValues.[inputValueOffset + inputValueStride*pIndex] else uint32 pIndex
                else
                    kvpk <- 0u

                let mutable key = kvpk
                match mode with
                | RadixSortMode.Float -> key <- floatFlip key
                | RadixSortMode.Int -> key <- key ^^^ (1u <<< 31)
                | _ -> ()
                key <- (key >>> shift) &&& RADIXMASK
                let p = key * uint32 RADIXGROUPS
                let ppos = int p + tpos
                    
                Preprocessor.unroll()
                for i in 0 .. 15 do
                    if tmod = i && pos < elements then
                        let index = int sRadixSum.[ppos]
                        sRadixSum.[ppos] <- sRadixSum.[ppos] + 1u

                        outputKeys.[outputKeyOffset + outputKeyStride*index] <- kvpk
                        outputValues.[outputValueOffset + outputValueStride*index] <- kvpv
                    barrier()
                pos <- pos + RADIXTHREADS

            barrier()
        }

    [<LocalSize(X = 64)>]
    let radixAddOffsetsAndShuffleKeysOnly
        (inputKeys : uint32[]) (inputKeyOffset : int) (inputKeyStride : int) 
        (outputKeys : uint32[]) (outputKeyOffset : int) (outputKeyStride : int) 
        (dRadixBlockSum : uint32[]) (dRadixSum : uint32[])
        (elements : int) (elements_rounded_to_3072 : int) (shift : int) (mode : RadixSortMode) =
        compute {
            let sRadixSum = allocateShared<uint32> SHUFFLE_GRFELEMENTS
            let threadIdx = getLocalId().X
            let blockIdx = getWorkGroupId().X

            if threadIdx = 0 then
                sRadixSum.[SHUFFLE_GRFOFFSET] <- 0u

            if threadIdx < PREFIX_NUM_BLOCKS-1 then
                sRadixSum.[SHUFFLE_GRFOFFSET + threadIdx + 1] <- dRadixBlockSum.[threadIdx]
            barrier()

            let mutable pos = threadIdx
            let mutable n = 1
            while n < PREFIX_NUM_BLOCKS do
                let ppos = pos - n
                let t0 = if pos < PREFIX_NUM_BLOCKS && ppos >= 0 then sRadixSum.[SHUFFLE_GRFOFFSET + ppos] else 0u
                barrier()
                if pos < PREFIX_NUM_BLOCKS then
                    sRadixSum.[SHUFFLE_GRFOFFSET + pos] <- sRadixSum.[SHUFFLE_GRFOFFSET + pos] + t0
                barrier()
                n <- n <<< 1
                    
            let mutable row    = threadIdx / RADIXGROUPS
            let mutable column = threadIdx % RADIXGROUPS
            let mutable spos   = row * RADIXGROUPS + column
            let mutable dpos   = row * TOTALRADIXGROUPS + column + blockIdx * RADIXGROUPS
            while spos < SHUFFLE_GRFOFFSET do
                sRadixSum.[spos] <- dRadixSum.[dpos] + sRadixSum.[SHUFFLE_GRFOFFSET + dpos / (TOTALRADIXGROUPS * RADICES / PREFIX_NUM_BLOCKS)]
                spos <- spos + NUM_THREADS_PER_BLOCK
                dpos <- dpos + (NUM_THREADS_PER_BLOCK / RADIXGROUPS) * TOTALRADIXGROUPS

            barrier()

            let  element_fraction  =   elements_rounded_to_3072 / TOTALRADIXGROUPS;
            let tmod   =   threadIdx % RADIXTHREADS;
            let tpos   =   threadIdx / RADIXTHREADS;

            pos <- (blockIdx * RADIXGROUPS + tpos) * element_fraction
            let mutable end_ = pos + element_fraction //(blockIdx * RADIXGROUPS + tpos + 1) * element_fraction;
            pos <- pos + tmod;

            barrier()

            while pos < end_ do
                let mutable kvpk = 0u
                if pos < elements then  
                    let pIndex = pos
                    kvpk <- inputKeys.[inputKeyOffset + inputKeyStride*pIndex]
                else
                    kvpk <- 0u

                let mutable key = kvpk
                match mode with
                | RadixSortMode.Float -> key <- floatFlip key
                | RadixSortMode.Int -> key <- key ^^^ (1u <<< 31)
                | _ -> ()
                key <- (key >>> shift) &&& RADIXMASK
                let p = key * uint32 RADIXGROUPS
                let ppos = int p + tpos
                    
                Preprocessor.unroll()
                for i in 0 .. 15 do
                    if tmod = i && pos < elements then
                        let index = int sRadixSum.[ppos]
                        sRadixSum.[ppos] <- sRadixSum.[ppos] + 1u

                        outputKeys.[outputKeyOffset + outputKeyStride*index] <- kvpk
                    barrier()
                pos <- pos + RADIXTHREADS

            barrier()
        }



type RadixSort(runtime : IRuntime) =
    let radixSum = runtime.CreateComputeShader RadixSortShaders.radixSum
    let radixPrefixSum = runtime.CreateComputeShader RadixSortShaders.radixPrefixSum
    let radixAddOffsetsAndShuffle = runtime.CreateComputeShader RadixSortShaders.radixAddOffsetsAndShuffle
    let radixAddOffsetsAndShuffleKeysOnly = runtime.CreateComputeShader RadixSortShaders.radixAddOffsetsAndShuffleKeysOnly

    member private x.CreatePermutation(input : IBufferVector<'a>, permutation : IBufferVector<int>, mode : RadixSortMode) =
        let cnt = input.Count

        let cnt3072 = if cnt % 3072 = 0 then cnt else 3072 * (1 + cnt / 3072)

            

        use keyCache = runtime.CreateBuffer<'a>(cnt)
        use keyCache2 = runtime.CreateBuffer<'a>(cnt)
        use valueCache = runtime.CreateBuffer<int>(cnt)
        use valueCache2 = runtime.CreateBuffer<int>(cnt)
        use empty = runtime.CreateBuffer<int>(count = 0)

        use dRadixSum = runtime.CreateBuffer<uint32>(RadixSortShaders.TOTALRADIXGROUPS * RadixSortShaders.RADICES)
        use dRadixBlockSum = runtime.CreateBuffer<uint32>(RadixSortShaders.PREFIX_NUM_BLOCKS)

        let mutable inputKeys = input
        let mutable inputValues : IBufferVector<int> = Unchecked.defaultof<_>

        let mutable outputKeys = keyCache :> IBufferVector<_>
        let mutable outputValues = valueCache :> IBufferVector<_>

        use radixSumInput = runtime.NewInputBinding radixSum
        use radixPrefixSumInput = runtime.NewInputBinding radixPrefixSum
        use radixAddOffsetsAndShuffleInput = runtime.NewInputBinding radixAddOffsetsAndShuffle

        let mutable shift = 0
        radixSumInput.["elements"] <- cnt
        radixSumInput.["elements_rounded_to_3072"] <- cnt3072
        radixSumInput.["dRadixSum"] <- dRadixSum.Buffer

        radixPrefixSumInput.["dRadixSum"] <- dRadixSum.Buffer
        radixPrefixSumInput.["dRadixBlockSum"] <- dRadixBlockSum.Buffer
        radixPrefixSumInput.Flush()

        radixAddOffsetsAndShuffleInput.["dRadixBlockSum"] <- dRadixBlockSum.Buffer
        radixAddOffsetsAndShuffleInput.["dRadixSum"] <- dRadixSum.Buffer
        radixAddOffsetsAndShuffleInput.["elements"] <- cnt
        radixAddOffsetsAndShuffleInput.["elements_rounded_to_3072"] <- cnt3072

        while shift < 32 do
                
            if (shift + RadixSortShaders.RADIX >= 32) then outputValues <- permutation

            radixSumInput.["keys"] <- inputKeys.Buffer
            radixSumInput.["keyOffset"] <- inputKeys.Origin
            radixSumInput.["keyStride"] <- inputKeys.Delta
            radixSumInput.["shift"] <- shift
            radixSumInput.["mode"] <- mode
            radixSumInput.Flush()
                
            radixAddOffsetsAndShuffleInput.["inputKeys"] <- inputKeys.Buffer
            radixAddOffsetsAndShuffleInput.["inputKeyOffset"] <- inputKeys.Origin
            radixAddOffsetsAndShuffleInput.["inputKeyStride"] <- inputKeys.Delta

            if isNull (inputValues :> obj) then
                radixAddOffsetsAndShuffleInput.["inputValues"] <- empty
                radixAddOffsetsAndShuffleInput.["inputValueOffset"] <- 0
                radixAddOffsetsAndShuffleInput.["inputValueStride"] <- 1
            else
                radixAddOffsetsAndShuffleInput.["inputValues"] <- inputValues.Buffer
                radixAddOffsetsAndShuffleInput.["inputValueOffset"] <- inputValues.Origin
                radixAddOffsetsAndShuffleInput.["inputValueStride"] <- inputValues.Delta
                    
            radixAddOffsetsAndShuffleInput.["outputKeys"] <- outputKeys.Buffer
            radixAddOffsetsAndShuffleInput.["outputKeyOffset"] <- outputKeys.Origin
            radixAddOffsetsAndShuffleInput.["outputKeyStride"] <- outputKeys.Delta
                
            radixAddOffsetsAndShuffleInput.["outputValues"] <- outputValues.Buffer
            radixAddOffsetsAndShuffleInput.["outputValueOffset"] <- outputValues.Origin
            radixAddOffsetsAndShuffleInput.["outputValueStride"] <- outputValues.Delta
            radixAddOffsetsAndShuffleInput.["mode"] <- mode
            radixAddOffsetsAndShuffleInput.["shift"] <- shift
            radixAddOffsetsAndShuffleInput.Flush()


            runtime.Run [
                ComputeCommand.Bind radixSum
                ComputeCommand.SetInput radixSumInput
                ComputeCommand.Dispatch RadixSortShaders.NUM_BLOCKS

                ComputeCommand.Sync dRadixSum.Buffer
                ComputeCommand.Sync dRadixBlockSum.Buffer

                ComputeCommand.Bind radixPrefixSum
                ComputeCommand.SetInput radixPrefixSumInput
                ComputeCommand.Dispatch RadixSortShaders.PREFIX_NUM_BLOCKS

                ComputeCommand.Sync dRadixSum.Buffer
                ComputeCommand.Sync dRadixBlockSum.Buffer

                    
                ComputeCommand.Bind radixAddOffsetsAndShuffle
                ComputeCommand.SetInput radixAddOffsetsAndShuffleInput
                ComputeCommand.Dispatch RadixSortShaders.NUM_BLOCKS

            ]

            Fun.Swap(&inputKeys, &outputKeys)
            Fun.Swap(&inputValues, &outputValues)
            if shift = 0 then
                outputValues <- valueCache2 :> IBufferVector<_>
                outputKeys <- keyCache2 :> IBufferVector<_>

            shift <- shift + RadixSortShaders.RADIX
                
    member private x.Sort(input : IBufferVector<'a>, output : IBufferVector<'a>, mode : RadixSortMode) =
        let cnt = input.Count

        let cnt3072 = if cnt % 3072 = 0 then cnt else 3072 * (1 + cnt / 3072)

        use keyCache = runtime.CreateBuffer<'a>(cnt)
        //use keyCache2 = runtime.CreateBuffer<'a>(cnt)

        let mutable disposeCache2 = false
        let keyCache2 =
            if input.Buffer = output.Buffer then
                let ri = Range1i(input.Origin, input.Origin + input.Count - 1)
                let ro = Range1i(output.Origin, output.Origin + output.Count - 1)
                if ri.Intersects ro then
                    disposeCache2 <- true
                    runtime.CreateBuffer<'a>(cnt) :> IBufferVector<_>
                else
                    output
            else
                disposeCache2 <- true
                runtime.CreateBuffer<'a>(cnt) :> IBufferVector<_>

        use dRadixSum = runtime.CreateBuffer<uint32>(RadixSortShaders.TOTALRADIXGROUPS * RadixSortShaders.RADICES)
        use dRadixBlockSum = runtime.CreateBuffer<uint32>(RadixSortShaders.PREFIX_NUM_BLOCKS)

        let mutable inputKeys = input
        let mutable outputKeys = keyCache :> IBufferVector<_>

        use radixSumInput = runtime.NewInputBinding radixSum
        use radixPrefixSumInput = runtime.NewInputBinding radixPrefixSum
        use radixAddOffsetsAndShuffleInput = runtime.NewInputBinding radixAddOffsetsAndShuffleKeysOnly

        let mutable shift = 0
        radixSumInput.["elements"] <- cnt
        radixSumInput.["elements_rounded_to_3072"] <- cnt3072
        radixSumInput.["dRadixSum"] <- dRadixSum.Buffer

        radixPrefixSumInput.["dRadixSum"] <- dRadixSum.Buffer
        radixPrefixSumInput.["dRadixBlockSum"] <- dRadixBlockSum.Buffer
        radixPrefixSumInput.Flush()

                
        radixAddOffsetsAndShuffleInput.["dRadixBlockSum"] <- dRadixBlockSum.Buffer
        radixAddOffsetsAndShuffleInput.["dRadixSum"] <- dRadixSum.Buffer
        radixAddOffsetsAndShuffleInput.["elements"] <- cnt
        radixAddOffsetsAndShuffleInput.["elements_rounded_to_3072"] <- cnt3072

        while shift < 32 do
                
            if (shift + RadixSortShaders.RADIX >= 32) then 
                outputKeys <- output

            radixSumInput.["keys"] <- inputKeys.Buffer
            radixSumInput.["keyOffset"] <- inputKeys.Origin
            radixSumInput.["keyStride"] <- inputKeys.Delta
            radixSumInput.["shift"] <- shift
            radixSumInput.["mode"] <- mode
            radixSumInput.Flush()
                
            radixAddOffsetsAndShuffleInput.["inputKeys"] <- inputKeys.Buffer
            radixAddOffsetsAndShuffleInput.["inputKeyOffset"] <- inputKeys.Origin
            radixAddOffsetsAndShuffleInput.["inputKeyStride"] <- inputKeys.Delta
            radixAddOffsetsAndShuffleInput.["outputKeys"] <- outputKeys.Buffer
            radixAddOffsetsAndShuffleInput.["outputKeyOffset"] <- outputKeys.Origin
            radixAddOffsetsAndShuffleInput.["outputKeyStride"] <- outputKeys.Delta
            radixAddOffsetsAndShuffleInput.["mode"] <- mode
            radixAddOffsetsAndShuffleInput.["shift"] <- shift
            radixAddOffsetsAndShuffleInput.Flush()


            runtime.Run [
                ComputeCommand.Bind radixSum
                ComputeCommand.SetInput radixSumInput
                ComputeCommand.Dispatch RadixSortShaders.NUM_BLOCKS

                ComputeCommand.Sync dRadixSum.Buffer
                ComputeCommand.Sync dRadixBlockSum.Buffer

                ComputeCommand.Bind radixPrefixSum
                ComputeCommand.SetInput radixPrefixSumInput
                ComputeCommand.Dispatch RadixSortShaders.PREFIX_NUM_BLOCKS

                ComputeCommand.Sync dRadixSum.Buffer
                ComputeCommand.Sync dRadixBlockSum.Buffer

                    
                ComputeCommand.Bind radixAddOffsetsAndShuffleKeysOnly
                ComputeCommand.SetInput radixAddOffsetsAndShuffleInput
                ComputeCommand.Dispatch RadixSortShaders.NUM_BLOCKS

            ]

            Fun.Swap(&inputKeys, &outputKeys)
            if shift = 0 then
                outputKeys <- keyCache2

            shift <- shift + RadixSortShaders.RADIX

        if disposeCache2 then 
            keyCache2.Buffer.Dispose()
        
    member x.CreatePermutation(input : IBufferVector<uint32>, permutation : IBufferVector<int>) =
        x.CreatePermutation(input, permutation, RadixSortMode.UInt)

    member x.CreatePermutation(input : IBufferVector<float32>, permutation : IBufferVector<int>) =
        x.CreatePermutation(input, permutation, RadixSortMode.Float)
            
    member x.CreatePermutation(input : IBufferVector<int>, permutation : IBufferVector<int>) =
        x.CreatePermutation(input, permutation, RadixSortMode.Int)

    member x.Sort(input : IBufferVector<uint32>, output : IBufferVector<uint32>) =
        x.Sort(input, output, RadixSortMode.UInt)

    member x.Sort(input : IBufferVector<float32>, output : IBufferVector<float32>) =
        x.Sort(input, output, RadixSortMode.Float)
            
    member x.Sort(input : IBufferVector<int>, output : IBufferVector<int>) =
        x.Sort(input, output, RadixSortMode.Int)

    member x.SortInPlace(input : IBufferVector<uint32>) =
        x.Sort(input, input, RadixSortMode.UInt)

    member x.SortInPlace(input : IBufferVector<float32>) =
        x.Sort(input, input, RadixSortMode.Float)
            
    member x.SortInPlace(input : IBufferVector<int>) =
        x.Sort(input, input, RadixSortMode.Int)

    member x.Dispose() =
        radixSum.Dispose()
        radixPrefixSum.Dispose()
        radixAddOffsetsAndShuffle.Dispose()
        radixAddOffsetsAndShuffleKeysOnly.Dispose()

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()


