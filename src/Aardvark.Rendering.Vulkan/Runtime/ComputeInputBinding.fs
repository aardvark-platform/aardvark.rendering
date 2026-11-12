namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering

type internal ComputeInputBinding =
    {
        Shader         : IComputeShader
        DescriptorSets : INativeResourceLocation<DescriptorSetBinding>
    }

    interface IComputeInputBinding with
        member x.Shader = x.Shader

[<AutoOpen>]
module internal ComputeInputBindingExtensions =

    type ResourceManager with

        member x.CreateComputeInputBinding(shader : IComputeShader, inputs : IUniformProvider) =
            let provider = UniformProvider.computeInputs inputs

            let descriptorSets =
                let sets = x.CreateDescriptorSets(shader.PipelineLayout, provider)
                x.CreateDescriptorSetBinding(VkPipelineBindPoint.Compute, shader.PipelineLayout, sets)

            { Shader         = shader
              DescriptorSets = descriptorSets }

        /// Returns the prepared binding for the given input binding, preparing a mutable one.
        member x.GetComputeInputBinding(input : IComputeInputBinding) =
            match input with
            | :? ComputeInputBinding as input ->
                input

            | :? MutableComputeInputBinding as binding ->
                x.CreateComputeInputBinding(binding.Shader, binding)

            | _ ->
                failf "unknown input binding type %A" (input.GetType())
