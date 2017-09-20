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
            let group = getWorkGroupId().X

            let gid0 = gid
            let lid0 =  getLocalId().X

            let lai = lid0
            let lbi = lid0 + halfScanSize
            let ai  = 2 * gid0 - lid0 
            let bi  = ai + halfScanSize 


            if ai < inputSize then mem.[lai] <- inputData.[inputOffset + ai * inputDelta]
            if bi < inputSize then mem.[lbi] <- inputData.[inputOffset + bi * inputDelta]

            //if lgid < inputSize then mem.[llid] <- inputData.[inputOffset + lgid * inputDelta]
            //if rgid < inputSize then mem.[rlid] <- inputData.[inputOffset + rgid * inputDelta]

            let lgid = 2 * gid0
            let rgid = lgid + 1
            let llid = 2 * lid0
            let rlid = llid + 1

            let mutable offset = 1
            let mutable d = halfScanSize
            while d > 0 do
                barrier()
                if lid0 < d then
                    let ai = offset * (llid + 1) - 1
                    let bi = offset * (rlid + 1) - 1
                    mem.[bi] <- (%add) mem.[ai] mem.[bi]
                d <- d >>> 1
                offset <- offset <<< 1

            d <- 2
            offset <- offset >>> 1

            while d < scanSize do
                offset <- offset >>> 1
                barrier()
                if lid0 < d - 1 then
                    let ai = offset*(llid + 2) - 1
                    let bi = offset*(rlid + 2) - 1

                    mem.[bi] <- (%add) mem.[bi] mem.[ai]

                d <- d <<< 1
            barrier()

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
                    let cnt = int input.Size
                    if cnt > 1 then
                        let args0 = ComputeShader.newInputBinding scan pool

                        args0.["inputOffset"] <- input.Offset |> int
                        args0.["inputDelta"] <- input.Delta |> int
                        args0.["inputSize"] <- input.Size |> int
                        args0.["inputData"] <- input.Buffer
                        args0.["outputOffset"] <- output.Offset |> int
                        args0.["outputDelta"] <- output.Delta |> int
                        args0.["outputData"] <- output.Buffer
                        args0.Flush()

                        let cmd =
                            command {
                                try
                                    do! Command.Bind scan
                                    do! Command.SetInputs args0
                                    do! Command.Dispatch(ceilDiv (int input.Size) scanSize)
                                    do! Command.Sync(output.Buffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.ShaderReadBit)
                                finally 
                                    args0.Dispose()
                            }

                        let oSums = output.[int64 scanSize - 1L .. ].Strided(scanSize)

                        
                        if oSums.Size > 0L then
                            let inner = build oSums oSums

                            let args1 = ComputeShader.newInputBinding fixup pool
                            args1.["inputData"] <- oSums.Buffer
                            args1.["inputOffset"] <- oSums.Offset |> int
                            args1.["inputDelta"] <- oSums.Delta |> int
                            args1.["outputData"] <- output.Buffer
                            args1.["outputOffset"] <- output.Offset |> int
                            args1.["outputDelta"] <- output.Delta |> int
                            args1.["count"] <- output.Size |> int
                            args1.["groupSize"] <- scanSize
                            args1.Flush()

                            command {
                                do! cmd
                                do! inner
                                try
                                    do! Command.Bind fixup
                                    do! Command.SetInputs args1
                                    do! Command.Dispatch(ceilDiv (int output.Size - scanSize) halfScanSize)
                                    do! Command.Sync(output.Buffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.ShaderReadBit)
                                finally 
                                    args1.Dispose()
                            }
                        else
                            cmd
                    else
                        Command.Nop

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


    [<Literal>]
    let primitiveScanSize = 32

    [<GLSLIntrinsic("addInvocationsInclusiveScanAMD({0})", "GL_AMD_shader_ballot")>]
    let magic (v : int) : int =
        failwith ""

    [<LocalSize(X = primitiveScanSize)>]
    let primitiveScanKernel (inputData : int[]) (outputData : int[]) =
        compute {
            let inputOffset : int = uniform?Arguments?inputOffset
            let inputDelta : int = uniform?Arguments?inputDelta
            let inputSize : int = uniform?Arguments?inputSize
            let outputOffset : int = uniform?Arguments?outputOffset
            let outputDelta : int = uniform?Arguments?outputDelta

            let gid = getGlobalId().X
            let mutable value = 0

            if gid < inputSize then
                value <- inputData.[inputOffset + gid * inputDelta]

            value <- magic value

            if gid < inputSize then
                outputData.[outputOffset + gid * outputDelta] <- value

        }

    [<LocalSize(X = primitiveScanSize)>]
    let primitiveFixupKernel (inputData : int[]) (outputData : int[]) =
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
                    outputData.[oid] <- inputData.[iid] + outputData.[oid]

        }
      


    type PrimitiveIntScan(runtime : Runtime) =
        inherit OptimizedClosures.FSharpFunc<IBufferVector<int>, IBufferVector<int>, Command>()

        static let ceilDiv (v : int) (d : int) =
            if v % d = 0 then v / d
            else 1 + v / d

        let device  = runtime.Device
        let pool = runtime.DescriptorPool

        let scan    = device |> ComputeShader.ofFunction primitiveScanKernel
        let fixup   = device |> ComputeShader.ofFunction primitiveFixupKernel

        let release() =
            ComputeShader.delete scan
            ComputeShader.delete fixup

        do device.OnDispose.Add (fun _ -> release())

        override x.Invoke(input : IBufferVector<int>, output : IBufferVector<int>) =
            let rec build (input : IBufferVector<int>) (output : IBufferVector<int>) =
                    let cnt = int input.Size
                    if cnt > 1 then
                        let args0 = ComputeShader.newInputBinding scan pool

                        args0.["inputOffset"] <- input.Offset |> int
                        args0.["inputDelta"] <- input.Delta |> int
                        args0.["inputSize"] <- input.Size |> int
                        args0.["inputData"] <- input.Buffer
                        args0.["outputOffset"] <- output.Offset |> int
                        args0.["outputDelta"] <- output.Delta |> int
                        args0.["outputData"] <- output.Buffer
                        args0.Flush()

                        let cmd =
                            command {
                                try
                                    do! Command.Bind scan
                                    do! Command.SetInputs args0
                                    do! Command.Dispatch(ceilDiv (int input.Size) primitiveScanSize)
                                    do! Command.Sync(output.Buffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.ShaderReadBit)
                                finally 
                                    args0.Dispose()
                            }

                        let oSums = output.[int64 primitiveScanSize - 1L .. ].Strided(primitiveScanSize)

                        
                        if oSums.Size > 0L then
                            let inner = build oSums oSums

                            let args1 = ComputeShader.newInputBinding fixup pool
                            args1.["inputData"] <- oSums.Buffer
                            args1.["inputOffset"] <- oSums.Offset |> int
                            args1.["inputDelta"] <- oSums.Delta |> int
                            args1.["outputData"] <- output.Buffer
                            args1.["outputOffset"] <- output.Offset |> int
                            args1.["outputDelta"] <- output.Delta |> int
                            args1.["count"] <- output.Size |> int
                            args1.["groupSize"] <- primitiveScanSize
                            args1.Flush()

                            command {
                                do! cmd
                                do! inner
                                try
                                    do! Command.Bind fixup
                                    do! Command.SetInputs args1
                                    do! Command.Dispatch(ceilDiv (int output.Size - primitiveScanSize) primitiveScanSize)
                                    do! Command.Sync(output.Buffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.ShaderReadBit)
                                finally 
                                    args1.Dispose()
                            }
                        else
                            cmd
                    else
                        Command.Nop

            if input.Size > 1L then
                build input output
            else
                Command.Nop
            
        override x.Invoke(a) = fun b -> x.Invoke(a,b)



[<AbstractClass; Sealed; Extension>]
type DeviceScanExtensions private() =
    [<Extension>]
    static member CompileScan<'a when 'a : unmanaged> (this : Runtime, add : Expr<'a -> 'a -> 'a>) =
        ScanImpl.Scan<'a>(this, add) |> unbox<IBufferVector<'a> -> IBufferVector<'a> -> Command>
        
    [<Extension>]
    static member CompilePrimitiveScan (this : Runtime) =
        ScanImpl.PrimitiveIntScan(this) |> unbox<IBufferVector<int> -> IBufferVector<int> -> Command>

