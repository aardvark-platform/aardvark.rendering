namespace Aardvark.Rendering.Tests.Compute

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open Expecto
open FSharp.Quotations
open FSharp.Data.Adaptive

module ComputeTasks =

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
        let useDisposedTask (runtime : IRuntime) =
            use data = runtime.CreateBuffer<int>(512)
            use shader = runtime.CreateComputeShader Shader.reverse<int>

            let input =
                shader.inputBinding {
                    buffer "data"       data
                    value  "dataLength" data.Count
                }

            let task =
                runtime.CompileCompute [
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input
                    ComputeCommand.Dispatch(data.Count / 2)
                ]
            task.Dispose()

            Expect.throwsT<ObjectDisposedException> (fun _ -> task.Run()) "Run() should throw ObjectDisposedException"
            Expect.throwsT<ObjectDisposedException> (fun _ -> task.Update()) "Update() should throw ObjectDisposedException"

    let tests (backend : Backend) =
        [
            "Use disposed task", Cases.useDisposedTask
        ]
        |> prepareComputeCases backend "Tasks"