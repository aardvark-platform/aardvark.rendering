namespace Aardvark.Rendering.Tests.Compute

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Expecto

module DispatchIndirect =

    module private Shader =
        open FShade

        type UniformScope with
            member this.OutputBuffer : int[] = this?StorageBuffer?OutputBuffer

        [<LocalSize(X = 1, Y = 1, Z = 1)>]
        let writeGlobalId () =
            compute {
                let id = getGlobalId()
                let num = getWorkGroupCount()
                let index = id.X * num.Y * num.Z + id.Y * num.Z + id.Z
                uniform.OutputBuffer.[index] <- 42 + id.X + id.Y + id.Z
            }

    module Cases =

        let indirect (withOffset: bool) (runtime : IRuntime) =
            let numGroups = V3i(4, 3, 2)

            use indirectBuffer =
                let args = if withOffset then [| V3i.Zero; numGroups |] else [| numGroups |]
                runtime.PrepareBuffer(ArrayBuffer args)

            use output = runtime.CreateBuffer<int> (numGroups.X * numGroups.Y * numGroups.Z)
            use shader = runtime.CreateComputeShader(Shader.writeGlobalId)
            let input = shader.CreateInputBinding <| uniformMap { buffer "OutputBuffer" output }

            let indirectBufferRange =
                let start = if withOffset then 1 else 0
                indirectBuffer.Coerce<V3i>().Elements(start)

            runtime.Run [
                ComputeCommand.Bind shader
                ComputeCommand.SetInput input
                ComputeCommand.DispatchIndirect indirectBufferRange
            ]

            let expected =
                Array.init output.Count (fun i ->
                    let z = i % numGroups.Z
                    let y = (i / numGroups.Z) % numGroups.Y
                    let x = i / (numGroups.Y * numGroups.Z)
                    42 + x + y + z
                )

            let result = output.Download()

            Expect.equal result expected "Unexpected result"

    let tests (backend : Backend) =
        [
            "Indirect",             Cases.indirect false
            "Indirect with offset", Cases.indirect true
        ]
        |> prepareComputeCases backend "Dispatch indirect"