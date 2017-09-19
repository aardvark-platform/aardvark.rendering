namespace Aardvark.Rendering.Vulkan


open FShade
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.Quotations
open System.Runtime.CompilerServices

module ScanImpl = 
    
    [<Literal>]
    let scanSize = 128

    [<Literal>]
    let halfScanSize = 64

    [<LocalSize(X = halfScanSize)>]
    let scanKernel (add : Expr<'a -> 'a -> 'a>) (inputData : 'a[]) (outputData : 'a[]) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let inputSize : int = uniform?Arguments?inputSize
            let outputOffset : int = uniform?Arguments?outputOffset
            let outputDelta : int = uniform?Arguments?outputDelta

            let mem : 'a[] = allocateShared scanSize
            let gid = getGlobalId().X

            let gid0 = gid
            let lid0 =  getLocalId().X

            let lgid = 2 * gid0
            let rgid = lgid + 1
            
            let llid = 2 * lid0
            let rlid = llid + 1

            if lgid < inputSize then mem.[llid] <- inputData.[inputOffset + lgid * inputDelta]
            if rlid < inputSize then mem.[rlid] <- inputData.[inputOffset + rgid * inputDelta]


            barrier()
            
            let mutable s = 1
            let mutable d = 2
            while d <= scanSize do
                if llid % d = 0 && llid >= s then
                    mem.[llid] <- (%add) mem.[llid - s] mem.[llid]

                barrier()
                s <- s <<< 1
                d <- d <<< 1

            d <- d >>> 1
            s <- s >>> 1
            while s >= 1 do
                if llid % d = 0 && llid + s < scanSize then
                    mem.[llid + s] <- (%add) mem.[llid] mem.[llid + s]
                    
                barrier()
                s <- s >>> 1
                d <- d >>> 1

            if lgid < inputSize then
                outputData.[outputOffset + lgid * outputDelta] <- mem.[llid]
            if rgid < inputSize then
                outputData.[outputOffset + rgid * outputDelta] <- mem.[rlid]

        }

    [<LocalSize(X = halfScanSize)>]
    let fixupKernel (add : Expr<'a -> 'a -> 'a>) (inputData : 'a[]) (outputData : 'a[]) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let outputOffset : int = uniform?Arguments?outputOffset
            let outputDelta : int = uniform?Arguments?outputDelta
            let groupSize : int = uniform?Arguments?groupSize
            let count : int = uniform?Arguments?count

            let id = getGlobalId().X + groupSize

            if id < count then
                let block = id / groupSize - 1
              
                let iid = inputOffset + block * inputDelta
                let oid = outputOffset + id * outputDelta

                if id % groupSize <> groupSize - 1 then
                    outputData.[oid] <- (%add) inputData.[iid] outputData.[oid]

        }
      
    type Scan<'a when 'a : unmanaged>(runtime : Runtime, add : Expr<'a -> 'a -> 'a>) =
        inherit OptimizedClosures.FSharpFunc<IBufferVector<'a>, IBufferVector<'a>, Command>()

        static let ceilDiv (v : int) (d : int) =
            if v % d = 0 then v / d
            else 1 + v / d

        let device  = runtime.Device
        let pool = runtime.DescriptorPool

        let scan    = device |> ComputeShader.ofFunction (scanKernel add)
        let fixup   = device |> ComputeShader.ofFunction (fixupKernel add)

        let release() =
            ComputeShader.delete scan
            ComputeShader.delete fixup

        do device.OnDispose.Add (fun _ -> release())

        override x.Invoke(input : IBufferVector<'a>, output : IBufferVector<'a>) =
            let rec build (input : IBufferVector<'a>) (output : IBufferVector<'a>) =
                command {
                    let cnt = int input.Size
                    if cnt > 1 then
                        let args0 = ComputeShader.newInputBinding scan pool

                        try
                            args0.["inputOffset"] <- input.Offset |> int
                            args0.["inputDelta"] <- input.Delta |> int
                            args0.["inputSize"] <- input.Size |> int
                            args0.["inputData"] <- input.Buffer
                            args0.["outputOffset"] <- output.Offset |> int
                            args0.["outputDelta"] <- output.Delta |> int
                            args0.["outputData"] <- output.Buffer
                            args0.Flush()

                            do! Command.Bind scan
                            do! Command.SetInputs args0
                            do! Command.Dispatch(ceilDiv (int input.Size) scanSize)
                            do! Command.Sync(output.Buffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.ShaderReadBit)

                            let oSums = output.[int64 scanSize - 1L .. ].Strided(scanSize)
                            if oSums.Size > 0L then
                            
                                do! build oSums oSums

                                let args1 = ComputeShader.newInputBinding fixup pool
                                try
                                    args1.["inputData"] <- oSums.Buffer
                                    args1.["inputOffset"] <- oSums.Offset |> int
                                    args1.["inputDelta"] <- oSums.Delta |> int
                                    args1.["outputData"] <- output.Buffer
                                    args1.["outputOffset"] <- output.Offset |> int
                                    args1.["outputDelta"] <- output.Delta |> int
                                    args1.["count"] <- output.Size |> int
                                    args1.["groupSize"] <- scanSize
                                    args1.Flush()

                                    do! Command.Bind fixup
                                    do! Command.SetInputs args1
                                    do! Command.Dispatch(ceilDiv (int output.Size - scanSize) halfScanSize)
                                    do! Command.Sync(output.Buffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.ShaderReadBit)
                                finally
                                    args1.Dispose()


                        finally
                            args0.Dispose()
                }

            if input.Size > 1L then
                build input output
            else
                Command.Nop
            
        override x.Invoke(a) = fun b -> x.Invoke(a,b)

        member x.Compile(input : Buffer<'a>, output : Buffer<'a>) =
            let cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
            cmd.Begin(CommandBufferUsage.None)
            cmd.Enqueue(x.Invoke(input, output))
            cmd.End()
            cmd

        //override x.Dispose() = release()


[<AbstractClass; Sealed; Extension>]
type DeviceScanExtensions private() =
    [<Extension>]
    static member CompileScan<'a when 'a : unmanaged> (this : Runtime, add : Expr<'a -> 'a -> 'a>) =
        ScanImpl.Scan<'a>(this, add) |> unbox<IBufferVector<'a> -> IBufferVector<'a> -> Command>

