namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

type internal ComputeInputBinding =
    {
        Program : ComputeProgram
        Binding : INativeResourceLocation<DescriptorSetBinding>
    }

    interface IComputeInputBinding with
        member x.Shader = x.Program

[<AutoOpen>]
module internal ComputeInputBindingExtensions =

    type ResourceManager with

        member x.CreateComputeInputBinding(program : ComputeProgram, inputs : IUniformProvider) =
            let provider =
                { new IUniformProvider with
                    member x.Dispose() = inputs.Dispose()
                    member x.TryGetUniform(scope, name) =
                        match inputs.TryGetUniform(scope, name) with
                        | None ->
                            let name = name.ToString()
                            if name.StartsWith "cs_" then
                                let sem = Sym.ofString <| name.Substring(3)
                                inputs.TryGetUniform(scope, sem)
                            else
                                None

                        | res -> res
                }

            let binding =
                let sets = x.CreateDescriptorSets(program.PipelineLayout, provider)
                x.CreateDescriptorSetBinding(VkPipelineBindPoint.Compute, program.PipelineLayout, sets)

            { Program = program
              Binding = binding }