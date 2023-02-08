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

        module private Primitives =

            let reverse (data : 'T[]) (runtime : IRuntime) =
                let dataBuffer = runtime.CreateBuffer<'T>(data.Length)
                let res = Array.zeroCreate<'T> data.Length

                let shader = runtime.CreateComputeShader Shader.reverse<'T>

                let input =
                    shader.inputBinding {
                        buffer "data" dataBuffer
                        value  "dataLength" data.Length
                    }

                let task =
                    runtime.CompileCompute([
                        ComputeCommand.Upload(data, dataBuffer)
                        ComputeCommand.Sync(dataBuffer, ResourceAccess.TransferWrite, ResourceAccess.ShaderRead ||| ResourceAccess.ShaderWrite)

                        ComputeCommand.Bind shader
                        ComputeCommand.SetInput input
                        ComputeCommand.Dispatch(data.Length / 2)

                        ComputeCommand.Sync(dataBuffer, ResourceAccess.ShaderWrite, ResourceAccess.TransferRead)
                        ComputeCommand.Download(dataBuffer, res)
                    ])

                { new IComputePrimitive<'T[]> with
                    member x.Task = task
                    member x.Run(rt) = task.Run(rt); res
                    member x.RunUnit(rt) = task.Run(rt)
                    member x.Dispose() =
                        task.Dispose()
                        shader.Dispose()
                        dataBuffer.Dispose() }

        let download (runtime : IRuntime) =
            let src = Array.init 5 (ignore >> Rnd.int32)
            use reverse = runtime |> Primitives.reverse src
            let dst = reverse.Run()

            Expect.equal dst.Length src.Length "Result array has unexpected length"
            Expect.equal dst (Array.rev src) "Result mismatch"

    let tests (backend : Backend) =
        [
            "Download", Cases.download
        ]
        |> prepareCases backend "Buffers"