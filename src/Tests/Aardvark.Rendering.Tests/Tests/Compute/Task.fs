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

        type AdaptiveArrayBuffer<'T when 'T : unmanaged>(runtime: IRuntime, data: aval<'T[]>) as this =
            inherit AdaptiveResource<IBuffer<'T>>()
            let mutable cache : IBuffer<'T> voption = ValueNone
            let length = data |> AVal.mapNonAdaptive _.Length
            do this.Acquire()

            member _.Length = length
            override this.Create() = ()
            override this.Destroy() =
                cache |> ValueOption.iter _.Dispose()
                cache <- ValueNone

            override this.Compute(t, rt) =
                let data = data.GetValue(t, rt)
                cache |> ValueOption.iter _.Dispose()
                let handle = runtime.CreateBuffer<'T>(data.Length)
                handle.Upload data
                cache <- ValueSome handle
                handle

            interface IDisposable with
                member this.Dispose() = this.Release()

        let reuseInputBinding (runtime: IRuntime) =
            let data = AVal.init <| Array.init 5 (ignore >> Rnd.int32)
            use dataBuffer = new AdaptiveArrayBuffer<_>(runtime, data)

            use shader = runtime.CreateComputeShader Shader.reverse<int>

            let input =
                shader.inputBinding {
                    buffer "data"       dataBuffer
                    value  "dataLength" dataBuffer.Length
                }

            let runAndCheck() =
                runtime.Run [
                    ComputeCommand.Bind shader
                    ComputeCommand.SetInput input
                    ComputeCommand.Dispatch(data.Value.Length / 2)
                ]

                let result = dataBuffer.GetValue().Download()
                Expect.equal result.Length data.Value.Length "Result array has unexpected length"
                Expect.equal result (Array.rev data.Value) "Result mismatch"

            runAndCheck()

            transact (fun _ ->
                data.Value <- Array.init 10 (ignore >> Rnd.int32)
            )

            runAndCheck()

    let tests (backend : Backend) =
        [
            "Use disposed task",   Cases.useDisposedTask
            "Reuse input binding", Cases.reuseInputBinding
        ]
        |> prepareComputeCases backend "Tasks"