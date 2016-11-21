namespace Aardvark.Rendering.Vulkan.NewPipeline


open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

//type DescriptorSetLayout =
//    class
//        inherit Resource<VkDescriptorSetLayout>
//        new(device : Device, handle : VkDescriptorSetLayout) = { inherit Resource<_>(device, handle) }
//    end
//
//type PipelineLayout =
//    class
//        inherit Resource<VkPipelineLayout>
//
//        new(device : Device, handle : VkPipelineLayout) = { inherit Resource<_>(device, handle) }
//    end
//
//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
//module PipelineLayout =
//    let ofShaderModules (shaders : list<ShaderModule>) (device : Device) =
//        if List.isEmpty shaders then failf "cannot create PipelineLayout for 0 shaders"
//
//        let first   = shaders |> List.minBy (fun sh -> sh.Stage)
//        let last    = shaders |> List.maxBy (fun sh -> sh.Stage)
//
//        let inputParameters     = first.Interface.inputs
//        let outputParameters    = last.Interface.outputs
//        let uniformParameters =
//            shaders |> List.collect (fun sh ->
//                sh.Interface.uniforms
//                    |> List.groupBy (fun p ->
//                        let set = ShaderParameter.tryGetDescriptorSet
//                    )
//            )
//
//
//        ()
    