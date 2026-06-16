namespace Aardvark.Rendering.Vulkan

open Aardvark.Rendering

// The compute input binding record lives here (before CommandTask) rather than in
// ComputeTask.fs (after CommandTask) so the render-integrated compute pre-pass in
// CommandTask can read its DescriptorSets. AutoOpen so existing unqualified uses in
// ComputeTask.fs keep resolving.
[<AutoOpen>]
module internal ComputeInputBindingType =

    type ComputeInputBinding =
        {
            Shader         : IComputeShader
            DescriptorSets : INativeResourceLocation<DescriptorSetBinding>
        }

        interface IComputeInputBinding with
            member x.Shader = x.Shader
