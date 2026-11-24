namespace Aardvark.Rendering.Tests.Compute

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module Float64 =

    module private Shader =
        open FShade

        type UniformScope with
            member this.Output : float[] = this?StorageBuffer?Output

        /// Loads matrix i as a whole dmat4 and stores its 16 components at 16 i.
        [<LocalSize(X = 64)>]
        let copy (count : int) (matrices : M44d[]) =
            compute {
                let i = getGlobalId().X
                if i < count then
                    let m = matrices.[i]
                    let o = 16 * i
                    uniform.Output.[o+0]  <- m.M00; uniform.Output.[o+1]  <- m.M01; uniform.Output.[o+2]  <- m.M02; uniform.Output.[o+3]  <- m.M03
                    uniform.Output.[o+4]  <- m.M10; uniform.Output.[o+5]  <- m.M11; uniform.Output.[o+6]  <- m.M12; uniform.Output.[o+7]  <- m.M13
                    uniform.Output.[o+8]  <- m.M20; uniform.Output.[o+9]  <- m.M21; uniform.Output.[o+10] <- m.M22; uniform.Output.[o+11] <- m.M23
                    uniform.Output.[o+12] <- m.M30; uniform.Output.[o+13] <- m.M31; uniform.Output.[o+14] <- m.M32; uniform.Output.[o+15] <- m.M33
            }

    module private Cases =

        let private count = 8

        /// 16 count distinct doubles.
        let private values = Array.init (16 * count) (fun k -> float (3 * k) + 0.25)

        /// A dmat4 loaded from a storage buffer must return the stored components wherever
        /// the allocator placed the buffer. The buffers are created with storage usage only
        /// (a uniform bit would raise the driver's alignment requirement) and are two doubles
        /// longer than the data, so their size is 16 mod 32 bytes and consecutive allocations
        /// alternate between 32-byte aligned and unaligned bases on devices whose storage
        /// buffer memory alignment is 16. NVIDIA drivers read a dmat4 from an unaligned base
        /// 16 bytes early (see https://github.com/hyazinthh/repro-nvidia-dmat4); the Vulkan
        /// backend works around it with a 32-byte minimum alignment.
        let loadFromStorageBuffers (runtime : IRuntime) =
            if not runtime.SupportsShaderFloat64 then
                Tests.skiptest "shader doubles are not supported"

            use shader = runtime.CreateComputeShader Shader.copy
            use output = runtime.CreateBuffer<float>(16 * count)

            let data = Array.append values [| 0.0; 0.0 |]
            let inputs =
                Array.init 8 (fun _ -> runtime.CreateBuffer<float>(data, BufferUsage.Storage ||| BufferUsage.Write))

            try
                for i = 0 to inputs.Length - 1 do
                    let binding =
                        shader.inputBinding {
                            buffer "Output" output
                            buffer "matrices" inputs.[i]
                            value "count" count
                        }

                    runtime.Run [
                        ComputeCommand.Set(output, 0u)
                        ComputeCommand.Sync(output, ResourceAccess.TransferWrite, ResourceAccess.ShaderWrite)
                        ComputeCommand.Bind shader
                        ComputeCommand.SetInput binding
                        ComputeCommand.Dispatch 1
                        ComputeCommand.Sync(output, ResourceAccess.ShaderWrite, ResourceAccess.TransferRead)
                    ]

                    let result = output.Download()
                    if result <> values then
                        let k = Seq.findIndex id (Seq.map2 (<>) result values)
                        let shown = result |> Array.truncate 20 |> Array.map (sprintf "%g") |> String.concat " "
                        failtestf "buffer %d: first mismatch at %d (expected %g, got %g); output = %s ..." i k values.[k] result.[k] shown
            finally
                for b in inputs do b.Dispose()

    let tests (target : TestTarget) =
        [
            "Load dmat4 from storage buffers", Cases.loadFromStorageBuffers
        ]
        |> prepareCasesCompute "Float64" target
