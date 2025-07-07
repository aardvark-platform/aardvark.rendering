namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Sorting
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open KHRAccelerationStructure

#nowarn "9"

type PipelineInfo =
    {
        pInputs                 : list<FShade.GLSL.GLSLParameter>
        pOutputs                : list<FShade.GLSL.GLSLParameter>
        pUniformBlocks          : list<FShade.GLSL.GLSLUniformBuffer>
        pStorageBlocks          : list<FShade.GLSL.GLSLStorageBuffer>
        pTextures               : list<FShade.GLSL.GLSLSampler>
        pImages                 : list<FShade.GLSL.GLSLImage>
        pAccelerationStructures : list<FShade.GLSL.GLSLAccelerationStructure>
        pEffectLayout           : Option<FShade.EffectInputLayout>
    }

type PipelineLayout =
    class
        inherit Resource<VkPipelineLayout>
        val public DescriptorSetLayouts : DescriptorSetLayout[]
        val public PushConstants : PushConstantsLayout voption
        val public PipelineInfo : PipelineInfo
        val public LayerCount : int
        val public PerLayerUniforms : Set<string>

        override x.Destroy() =
            for b in x.DescriptorSetLayouts do
                b.Dispose()

            VkRaw.vkDestroyPipelineLayout(x.Device.Handle, x.Handle, NativePtr.zero)
            x.Handle <- VkPipelineLayout.Null


        new(device, handle, descriptorSetLayouts, pushConstants, info, layerCount : int, perLayer : Set<string>) =
            { inherit Resource<_>(device, handle);
                DescriptorSetLayouts = descriptorSetLayouts;
                PushConstants = pushConstants;
                PipelineInfo = info;
                LayerCount = layerCount;
                PerLayerUniforms = perLayer
            }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PipelineLayout =
    open FShade

    let ofProgramInterface (inputLayout : Option<EffectInputLayout>) (iface : FShade.GLSL.GLSLProgramInterface)
                           (layers : int) (perLayer : Set<string>) (device : Device) =
        // figure out which stages reference which uniforms/textures
        let uniformBlocks = Dict.empty
        let textures = Dict.empty
        let images = Dict.empty
        let storageBlocks = Dict.empty
        let accelerationStructures = Dict.empty
        let mutable pushConstants : PushConstantsLayout voption = ValueNone
        let mutable setCount = 0

        let findStages (name : string) =
            let stages =
                match inputLayout with
                | Some layout ->
                    let stages =
                        layout.Uniforms
                        |> MapExt.tryFind name
                        |> Option.map (fun u -> [u.Stages])
                        |> Option.defaultWith (fun _ ->
                            layout.Uniforms
                            |> MapExt.toList
                            |> List.choose (fun (_, u) ->
                                if u.Buffer = Some name then Some u.Stages
                                else None
                            )
                        )
                        |> Set.unionMany

                    (VkShaderStageFlags.None, stages)
                    ||> Set.fold (fun accum stage ->
                        accum ||| (VkShaderStageFlags.ofShaderStage (ShaderStage.ofFShade stage))
                    )

                | _ ->
                    (VkShaderStageFlags.None, iface.shaders.Slots) ||> MapExt.fold (fun accum slot siface ->
                        let stage = VkShaderStageFlags.ofShaderStage (ShaderStage.ofFShade slot.Stage)

                        let references =
                            [ siface.shaderUniformBuffers
                              siface.shaderStorageBuffers
                              siface.shaderSamplers
                              siface.shaderImages
                              siface.shaderAccelerationStructures ]
                            |> FSharp.Data.Adaptive.HashSet.unionMany

                        if references.Contains name then
                            accum ||| stage
                        else
                            accum
                    )

            if stages = VkShaderStageFlags.None then
                VkShaderStageFlags.All
            else
                stages

        let add (dict : Dict<'K, 'V * VkShaderStageFlags>) (key : 'K) (value : 'V) (flags : VkShaderStageFlags) =
            let curr =
                match dict.TryGetValue key with
                | (true, (_, flags)) -> flags
                | _ -> VkShaderStageFlags.None

            dict.[key] <- (value, curr ||| flags)

        for (KeyValue(n, b)) in iface.uniformBuffers do
            setCount <- max setCount (b.ubSet + 1)
            let key = b.ubSet, b.ubBinding
            let stages = findStages n

            if b.ubSet >= 0 && b.ubBinding >= 0 then
                (b, stages) ||> add uniformBlocks key
            else
                match pushConstants with
                | ValueSome other -> failf $"multiple push constant blocks: {other.Buffer.ubName}, {b.ubName}"
                | _ ->
                    if Mem b.ubSize > device.PhysicalDevice.Limits.Uniform.MaxPushConstantsSize then
                        failf $"push constant block has size {Mem b.ubSize} but device limit is {device.PhysicalDevice.Limits.Uniform.MaxPushConstantsSize}"

                    pushConstants <- ValueSome <| { StageFlags = stages; Buffer = b }

        for (KeyValue(n, b)) in iface.storageBuffers do
            setCount <- max setCount (b.ssbSet + 1)
            let key = b.ssbSet, b.ssbBinding
            let stages = findStages n
            (b, stages) ||> add storageBlocks key

        for (KeyValue(n, b)) in iface.samplers do
            setCount <- max setCount (b.samplerSet + 1)
            let key = b.samplerSet, b.samplerBinding
            let stages = findStages n
            (b, stages) ||> add textures key

        for (KeyValue(n, b)) in iface.images do
            setCount <- max setCount (b.imageSet + 1)
            let key = b.imageSet, b.imageBinding
            let stages = findStages n
            (b, stages) ||> add images key

        for (KeyValue(n, b)) in iface.accelerationStructures do
            setCount <- max setCount (b.accelSet + 1)
            let key = b.accelSet, b.accelBinding
            let stages = findStages n
            (b, stages) ||> add accelerationStructures key
 
        let uniformBlocks = uniformBlocks.Values |> Seq.toList
        let storageBlocks = storageBlocks.Values |> Seq.toList
        let textures = textures.Values |> Seq.toList
        let images = images.Values |> Seq.toList
        let accelerationStructures = accelerationStructures.Values |> Seq.toList

        // create DescriptorSetLayouts for all used slots (empty if no bindings)
        let sets = Array.init setCount (fun set -> CSharpList.empty)

        for (block, stageFlags) in uniformBlocks do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.UniformBuffer
                    stageFlags
                    (UniformBlockParameter block)
                    device

            sets.[block.ubSet].Add binding

        for (block, stageFlags) in storageBlocks do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.StorageBuffer
                    stageFlags
                    (StorageBufferParameter block)
                    device

            sets.[block.ssbSet].Add binding

        for (tex, stageFlags) in textures do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.CombinedImageSampler
                    stageFlags
                    (SamplerParameter tex)
                    device

            sets.[tex.samplerSet].Add binding

        for (img, stageFlags) in images do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.StorageImage
                    stageFlags
                    (ImageParameter img)
                    device

            sets.[img.imageSet].Add binding

        for (accel, stageFlags) in accelerationStructures do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.AccelerationStructureKhr
                    stageFlags
                    (AccelerationStructureParameter accel)
                    device

            sets.[accel.accelSet].Add binding

        let setLayouts =
            sets |> Array.map (fun l -> 
                if l.Count = 0 then
                    device |> DescriptorSetLayout.empty
                else
                    let arr = CSharpList.toArray l
                    arr.QuickSortAscending(fun v -> v.Binding) |> ignore
                    device |> DescriptorSetLayout.create arr
            )

        // create a pipeline layout from the given DescriptorSetLayouts
        let setLayoutHandles = setLayouts |> Array.map _.Handle
        let pushConstantRanges = pushConstants |> ValueOption.toArray |> Array.map _.Range

        native {
            let! pSetLayoutHandles = setLayoutHandles
            let! pPushConstantRanges = pushConstantRanges

            let! pInfo =
                VkPipelineLayoutCreateInfo(
                    VkPipelineLayoutCreateFlags.None,
                    uint32 setLayoutHandles.Length, pSetLayoutHandles,
                    uint32 pushConstantRanges.Length, pPushConstantRanges
                )

            let! pHandle = VkPipelineLayout.Null
            VkRaw.vkCreatePipelineLayout(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create PipelineLayout"

            let info =
                {
                    pInputs                 = iface.inputs
                    pOutputs                = iface.outputs
                    pUniformBlocks          = List.map fst uniformBlocks
                    pStorageBlocks          = List.map fst storageBlocks
                    pTextures               = List.map fst textures
                    pImages                 = List.map fst images
                    pAccelerationStructures = List.map fst accelerationStructures
                    pEffectLayout           = None
                }

            return new PipelineLayout(device, !!pHandle, setLayouts, pushConstants, info, layers, perLayer)
        }

[<AbstractClass; Sealed; Extension>]
type ContextPipelineLayoutExtensions private() =

    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, iface : FShade.GLSL.GLSLProgramInterface,
                                              layers : int, perLayer : Set<string>) =
        this |> PipelineLayout.ofProgramInterface None iface layers perLayer