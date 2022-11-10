namespace Aardvark.Rendering.Vulkan

open System.Threading
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Sorting
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRRayTracingPipeline
open Aardvark.Rendering.Vulkan.KHRAccelerationStructure
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkShaderStageFlags =
    let ofShaderStage =
        LookupTable.lookupTable [
            ShaderStage.Vertex,         VkShaderStageFlags.VertexBit
            ShaderStage.TessControl,    VkShaderStageFlags.TessellationControlBit
            ShaderStage.TessEval,       VkShaderStageFlags.TessellationEvaluationBit
            ShaderStage.Geometry,       VkShaderStageFlags.GeometryBit
            ShaderStage.Fragment,       VkShaderStageFlags.FragmentBit
            ShaderStage.Compute,        VkShaderStageFlags.ComputeBit
            ShaderStage.RayGeneration,  VkShaderStageFlags.RaygenBitKhr
            ShaderStage.Intersection,   VkShaderStageFlags.IntersectionBitKhr
            ShaderStage.AnyHit,         VkShaderStageFlags.AnyHitBitKhr
            ShaderStage.ClosestHit,     VkShaderStageFlags.ClosestHitBitKhr
            ShaderStage.Miss,           VkShaderStageFlags.MissBitKhr
            ShaderStage.Callable,       VkShaderStageFlags.CallableBitKhr
        ]
        
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TextureFormat =
    open FShade.GLSL

    let ofGLSLType =
        LookupTable.lookupTable [   
            Int(true, 8),           TextureFormat.R8i
            Vec(2, Int(true, 8)),   TextureFormat.Rg8i
            Vec(3, Int(true, 8)),   TextureFormat.Rgb8i
            Vec(4, Int(true, 8)),   TextureFormat.Rgba8i

            Int(true, 16),          TextureFormat.R16i
            Vec(2, Int(true, 16)),  TextureFormat.Rg16i
            Vec(3, Int(true, 16)),  TextureFormat.Rgb16i
            Vec(4, Int(true, 16)),  TextureFormat.Rgba16i

            Int(true, 32),          TextureFormat.R32i
            Vec(2, Int(true, 32)),  TextureFormat.Rg32i
            Vec(3, Int(true, 32)),  TextureFormat.Rgb32i
            Vec(4, Int(true, 32)),  TextureFormat.Rgba32i

            Float(32),              TextureFormat.R32f
            Vec(2, Float(32)),      TextureFormat.Rg32f
            Vec(3, Float(32)),      TextureFormat.Rgb32f
            Vec(4, Float(32)),      TextureFormat.Rgba32f

            Float(64),              TextureFormat.R32f
            Vec(2, Float(64)),      TextureFormat.Rg32f
            Vec(3, Float(64)),      TextureFormat.Rgb32f
            Vec(4, Float(64)),      TextureFormat.Rgba32f
        ]


type PipelineLayout =
    class
        inherit Resource<VkPipelineLayout>
        val public DescriptorSetLayouts : array<DescriptorSetLayout>
        val public PipelineInfo : PipelineInfo
        val public LayerCount : int
        val public PerLayerUniforms : Set<string>

        override x.Destroy() =
            for b in x.DescriptorSetLayouts do
                b.Dispose()

            VkRaw.vkDestroyPipelineLayout(x.Device.Handle, x.Handle, NativePtr.zero)
            x.Handle <- VkPipelineLayout.Null


        new(device, handle, descriptorSetLayouts, info, layerCount : int, perLayer : Set<string>) = 
            { inherit Resource<_>(device, handle);
                DescriptorSetLayouts = descriptorSetLayouts;
                PipelineInfo = info;
                LayerCount = layerCount;
                PerLayerUniforms = perLayer
            }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PipelineLayout =

    let ofShaders (iface : FShade.GLSL.GLSLProgramInterface) (shaders : array<ShaderModule>) (layers : int) (perLayer : Set<string>) (device : Device) =
        // figure out which stages reference which uniforms/textures
        let uniformBlocks = Dict.empty
        let textures = Dict.empty
        let images = Dict.empty
        let storageBlocks = Dict.empty
        let accelerationStructures = Dict.empty
        let mutable setCount = 0

        let mutable inputs = []
        let mutable outputs = []

        for shader in shaders do
            let flags = VkShaderStageFlags.ofShaderStage shader.Stage 
            let siface = iface.shaders.[shader.Slot]

            match shader.Stage with
                | ShaderStage.Vertex -> inputs <- siface.shaderInputs
                | ShaderStage.Fragment -> outputs <- siface.shaderOutputs
                | _ -> ()

            for block in siface.shaderUniformBuffers do
                let block = iface.uniformBuffers.[block]
                setCount <- max setCount (block.ubSet + 1)
                let key = (block.ubSet, block.ubBinding)
                let referenced = 
                    match uniformBlocks.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None  
                uniformBlocks.[key] <- (block, referenced ||| flags)
                         
            for block in siface.shaderStorageBuffers do
                let block = iface.storageBuffers.[block]
                setCount <- max setCount (block.ssbSet + 1)
                let key = (block.ssbSet, block.ssbBinding)
                let referenced = 
                    match storageBlocks.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None  
                storageBlocks.[key] <- (block, referenced ||| flags)
                                   
            for tex in siface.shaderSamplers do
                let tex = iface.samplers.[tex]
                setCount <- max setCount (tex.samplerSet + 1)
                let key = (tex.samplerSet, tex.samplerBinding)
                let referenced = 
                    match textures.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None
                                    
                textures.[key] <- (tex, referenced ||| flags)

            for img in siface.shaderImages do
                let img = iface.images.[img]
                setCount <- max setCount (img.imageSet + 1)
                let key = (img.imageSet, img.imageBinding)
                let referenced = 
                    match images.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None
                                    
                images.[key] <- (img, referenced ||| flags)

            for accel in siface.shaderAccelerationStructures do
                let accel = iface.accelerationStructures.[accel]
                setCount <- max setCount (accel.accelSet + 1)
                let key = (accel.accelSet, accel.accelBinding)
                let referenced = 
                    match accelerationStructures.TryGetValue key with
                    | (true, (_, referencedBy)) -> referencedBy
                    | _ -> VkShaderStageFlags.None
                                    
                accelerationStructures.[key] <- (accel, referenced ||| flags)
                
                            
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
        let handles = setLayouts |> Array.map (fun d -> d.Handle)

        native {
            let! pHandles = handles
            let! pInfo =
                VkPipelineLayoutCreateInfo(
                    VkPipelineLayoutCreateFlags.None,
                    uint32 handles.Length, pHandles,
                    0u, NativePtr.zero
                )
            let! pHandle = VkPipelineLayout.Null
            VkRaw.vkCreatePipelineLayout(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create PipelineLayout"
  
            let info =    
                {
                    pInputs                 = inputs
                    pOutputs                = outputs
                    pUniformBlocks          = List.map fst uniformBlocks
                    pStorageBlocks          = List.map fst storageBlocks
                    pTextures               = List.map fst textures
                    pImages                 = List.map fst images
                    pAccelerationStructures = List.map fst accelerationStructures
                    pEffectLayout           = None
                }    
                      
            return new PipelineLayout(device, !!pHandle, setLayouts, info, layers, perLayer)
        }

    open FShade
    open FShade.Imperative

    let ofEffectInputLayout (layout : EffectInputLayout) (layers : int) (perLayer : Set<string>) (device : Device) : PipelineLayout =
        
        failf "not implemented"

[<AbstractClass; Sealed; Extension>]
type ContextPipelineLayoutExtensions private() =
    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, iface : FShade.GLSL.GLSLProgramInterface, shaders : array<ShaderModule>, layers : int, perLayer : Set<string>) =
        this |> PipelineLayout.ofShaders iface shaders layers perLayer

    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, layout : FShade.EffectInputLayout, layers : int, perLayer : Set<string>) =
        this |> PipelineLayout.ofEffectInputLayout layout layers perLayer