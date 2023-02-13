namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
open Aardvark.GPGPU
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Expecto

module ComputeBuffers =

    module private Shader =
        open FShade

        let reverse<'T> (dataLength : int) (data : 'T[]) =
            compute {
                let i = getGlobalId().X

                if i < dataLength / 2 then
                    let j = dataLength - 1 - i
                    let tmp = data.[i]
                    data.[i] <- data.[j]
                    data.[j] <- tmp
            }

    module Cases =
        open FShade

        [<AbstractClass>]
        type private Primitive<'T when 'T : unmanaged>(task : IComputeTask) =
            abstract member Result : 'T[]
            abstract member Release : unit -> unit

            member x.Dispose() =
                task.Dispose()
                x.Release()

            interface IComputePrimitive<'T[]> with
                member x.Task = task
                member x.Run(rt) = task.Run(rt); x.Result
                member x.RunUnit(rt) = task.Run(rt)
                member x.Dispose() = x.Dispose()

        module private Primitives =

            let reverse (data : 'T[]) (runtime : IRuntime) =
                let dataBuffer = runtime.CreateBuffer<'T>(data.Length)
                let result = Array.zeroCreate<'T> data.Length

                let shader = runtime.CreateComputeShader Shader.reverse<'T>

                let input =
                    shader.inputBinding {
                        buffer "data" dataBuffer
                        value "dataLength" data.Length
                    }

                let task =
                    runtime.CompileCompute([
                        ComputeCommand.Upload(data, dataBuffer)
                        ComputeCommand.Sync(dataBuffer, ResourceAccess.TransferWrite, ResourceAccess.ShaderRead ||| ResourceAccess.ShaderWrite)

                        ComputeCommand.Bind shader
                        ComputeCommand.SetInput input
                        ComputeCommand.Dispatch(data.Length / 2)

                        ComputeCommand.Sync(dataBuffer, ResourceAccess.ShaderWrite, ResourceAccess.TransferRead)
                        ComputeCommand.Download(dataBuffer, result)
                    ])

                { new Primitive<'T>(task) with
                    member x.Result = result
                    member x.Release() =
                        shader.Dispose()
                        dataBuffer.Dispose() }

        let uploadDownload (runtime : IRuntime) =
            let src = Array.init 5 (ignore >> Rnd.int32)
            use reverse = runtime |> Primitives.reverse src
            let dst = reverse.Run()

            Expect.equal dst.Length src.Length "Result array has unexpected length"
            Expect.equal dst (Array.rev src) "Result mismatch"

        let uploadDownloadMismatchingSize (runtime : IRuntime) =
            let src = Array.init 64 (ignore >> Rnd.int32)
            use bigBuffer = runtime.CreateBuffer<int>(128)
            use smallBuffer = runtime.CreateBuffer<int>(32)
            let dstBig = Array.zeroCreate<int> 256
            let dstSmall = Array.zeroCreate<int> 16

            runtime.Run([
                ComputeCommand.Upload(src, bigBuffer)
                ComputeCommand.Upload(src, smallBuffer)
                ComputeCommand.Download(bigBuffer, dstBig)
                ComputeCommand.Download(smallBuffer, dstSmall)
            ])

            for i = 0 to (min src.Length dstBig.Length) - 1 do
                Expect.equal dstBig.[i] src.[i] "Result mismatch oversized buffer"

            for i = 0 to (min src.Length dstSmall.Length) - 1 do
                Expect.equal dstSmall.[i] src.[i] "Result mismatch undersized buffer"

        let nestedDownload (runtime : IRuntime) =
            let src = Array.init 5 (ignore >> Rnd.int32)
            use srcBuffer = runtime.CreateBuffer<int>(src)

            use shader = runtime.CreateComputeShader Shader.reverse<int>
            let result1 = Array.zeroCreate<int> src.Length

            use rev1 =
                let input =
                    shader.inputBinding {
                        buffer "data" srcBuffer
                        value "dataLength" srcBuffer.Count
                    }

                runtime.CompileCompute([
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input
                    ComputeCommand.Dispatch(src.Length / 2)

                    ComputeCommand.Sync(srcBuffer, ResourceAccess.ShaderWrite, ResourceAccess.TransferRead)
                    ComputeCommand.Download(srcBuffer, result1)
                ])

            let result2 = Array.zeroCreate<int> src.Length

            use rev2 =
                let input =
                    shader.inputBinding {
                        buffer "data" srcBuffer
                        value "dataLength" srcBuffer.Count
                    }

                runtime.CompileCompute([
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input
                    ComputeCommand.Dispatch(src.Length / 2)

                    ComputeCommand.Sync(srcBuffer, ResourceAccess.ShaderWrite, ResourceAccess.TransferRead)
                    ComputeCommand.Download(srcBuffer, result2)
                ])

            runtime.Run([
                ComputeCommand.Execute rev1
                ComputeCommand.Execute rev2
            ])

            Expect.equal result1 (Array.rev src) "First result mismatch"
            Expect.equal result2 src "Final result mismatch"

        let copy (runtime : IRuntime) =
            let src = Array.init 5 (ignore >> Rnd.int32)
            use srcBuffer = runtime.CreateBuffer<int>(src)
            use dstBuffer = runtime.CreateBuffer<int>(4)
            let dst = Array.zeroCreate<int> 4

            runtime.Run([
                ComputeCommand.Copy(srcBuffer, dstBuffer)
                ComputeCommand.Download(dstBuffer, dst)
            ])

            for i = 0 to dst.Length - 1 do
                Expect.equal dst.[i] src.[i] "Result mismatch"

    let tests (backend : Backend) =
        [
            "Up- / download",                   Cases.uploadDownload
            "Up- / download mismatching size",  Cases.uploadDownloadMismatchingSize
            "Nested download",                  Cases.nestedDownload
            "Copy",                             Cases.copy
        ]
        |> prepareCases backend "Buffers"