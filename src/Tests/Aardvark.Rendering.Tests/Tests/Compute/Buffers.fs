namespace Aardvark.Rendering.Tests.Rendering

open System
open Aardvark.Base
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

        let uploadDownload (runtime : IRuntime) =
            let src = Array.init 5 (ignore >> Rnd.int32)
            use srcBuffer = runtime.CreateBuffer<int>(src.Length)
            let dst = Array.zeroCreate<int> src.Length

            use shader = runtime.CreateComputeShader Shader.reverse<int>

            let input =
                shader.inputBinding {
                    buffer "data" srcBuffer
                    value "dataLength" src.Length
                }

            runtime.Run([
                ComputeCommand.Upload(src, srcBuffer)
                ComputeCommand.Sync(srcBuffer, ResourceAccess.TransferWrite, ResourceAccess.ShaderRead ||| ResourceAccess.ShaderWrite)

                ComputeCommand.Bind shader
                ComputeCommand.SetInput input
                ComputeCommand.Dispatch(src.Length / 2)

                ComputeCommand.Sync(srcBuffer, ResourceAccess.ShaderWrite, ResourceAccess.TransferRead)
                ComputeCommand.Download(srcBuffer, dst)
            ])

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

        let fill (runtime : IRuntime) =
            use buffer = runtime.CreateBuffer<int>(57)
            let value = 42

            let set (range : IBufferRange) =
                runtime.Run([
                    ComputeCommand.Set(range, uint32 value)
                ])

            set buffer
            let result = buffer.Download()
            Expect.equal result (value |> Array.replicate buffer.Count) "Result mismatch"

            Expect.throwsT<ArgumentException> (fun _ -> set (buffer.Range(1n, 128n))) "Unexpected behavior for misaligned offset"
            Expect.throwsT<ArgumentException> (fun _ -> set (buffer.Range(0n, 127n))) "Unexpected behavior for misaligned size"


    let tests (backend : Backend) =
        [
            "Up- / download",                   Cases.uploadDownload
            "Up- / download mismatching size",  Cases.uploadDownloadMismatchingSize
            "Nested download",                  Cases.nestedDownload
            "Copy",                             Cases.copy
            "Fill",                             Cases.fill
        ]
        |> prepareCases backend "Buffers"