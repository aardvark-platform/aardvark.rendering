namespace Aardvark.Rendering.Vulkan

open System.Threading
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Sorting
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkShaderStageFlags =
    let ofShaderStage =
        LookupTable.lookupTable [
            ShaderStage.Vertex, VkShaderStageFlags.VertexBit
            ShaderStage.TessControl, VkShaderStageFlags.TessellationControlBit
            ShaderStage.TessEval, VkShaderStageFlags.TessellationEvaluationBit
            ShaderStage.Geometry, VkShaderStageFlags.GeometryBit
            ShaderStage.Fragment, VkShaderStageFlags.FragmentBit
            ShaderStage.Compute, VkShaderStageFlags.ComputeBit
        ]
        
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderbufferFormat =
    open FShade.GLSL

    let ofGLSLType =
        LookupTable.lookupTable [   
            Int(true, 8),           RenderbufferFormat.R8i
            Vec(2, Int(true, 8)),   RenderbufferFormat.Rg8i
            Vec(3, Int(true, 8)),   RenderbufferFormat.Rgb8i
            Vec(4, Int(true, 8)),   RenderbufferFormat.Rgba8i

            Int(true, 16),          RenderbufferFormat.R16i
            Vec(2, Int(true, 16)),  RenderbufferFormat.Rg16i
            Vec(3, Int(true, 16)),  RenderbufferFormat.Rgb16i
            Vec(4, Int(true, 16)),  RenderbufferFormat.Rgba16i

            Int(true, 32),          RenderbufferFormat.R32i
            Vec(2, Int(true, 32)),  RenderbufferFormat.Rg32i
            Vec(3, Int(true, 32)),  RenderbufferFormat.Rgb32i
            Vec(4, Int(true, 32)),  RenderbufferFormat.Rgba32i
            
            Float(32),              RenderbufferFormat.R32f
            Vec(2, Float(32)),      RenderbufferFormat.Rg32f
            Vec(3, Float(32)),      RenderbufferFormat.Rgb32f
            Vec(4, Float(32)),      RenderbufferFormat.Rgba32f

            Float(64),              RenderbufferFormat.R32f
            Vec(2, Float(64)),      RenderbufferFormat.Rg32f
            Vec(3, Float(64)),      RenderbufferFormat.Rgb32f
            Vec(4, Float(64)),      RenderbufferFormat.Rgba32f
        ]


type PipelineLayout =
    class
        inherit Resource<VkPipelineLayout>

        val mutable public DescriptorSetLayouts : array<DescriptorSetLayout>
        val mutable public PipelineInfo : PipelineInfo
        
        val mutable public ReferenceCount : int
        val mutable public LayerCount : int
        val mutable public PerLayerUniforms : Set<string>

        member x.AddRef() =
            if Interlocked.Increment(&x.ReferenceCount) = 1 then
                failf "cannot revive PipelineLayout"
            
        member x.RemoveRef() =
            if Interlocked.Decrement(&x.ReferenceCount) = 0 then
                for b in x.DescriptorSetLayouts do
                    x.Device |> DescriptorSetLayout.delete b

                VkRaw.vkDestroyPipelineLayout(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkPipelineLayout.Null
                x.DescriptorSetLayouts <- Array.empty

        interface IFramebufferSignature with
            member x.ColorAttachments = 
                let a : AttachmentSignature = failwith ""
                x.PipelineInfo.pOutputs 
                    |> List.map (fun p -> p.paramLocation, (Symbol.Create p.paramSemantic, { samples = 1; format = RenderbufferFormat.ofGLSLType p.paramType })) 
                    |> Map.ofList
            member x.IsAssignableFrom _ = false
            member x.Images = Map.empty
            member x.Runtime = Unchecked.defaultof<_>
            member x.StencilAttachment = None
            member x.DepthAttachment = None
            member x.LayerCount = x.LayerCount
            member x.PerLayerUniforms = x.PerLayerUniforms

        new(device, handle, descriptorSetLayouts, info, layerCount : int, perLayer : Set<string>) = 
            { inherit Resource<_>(device, handle); DescriptorSetLayouts = descriptorSetLayouts; PipelineInfo = info; ReferenceCount = 1; LayerCount = layerCount; PerLayerUniforms = perLayer }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PipelineLayout =

    let ofShaders (shaders : array<Shader>) (layers : int) (perLayer : Set<string>) (device : Device) =
        // figure out which stages reference which uniforms/textures
        let uniformBlocks = Dict.empty
        let textures = Dict.empty
        let images = Dict.empty
        let storageBlocks = Dict.empty
        let mutable setCount = 0

        let mutable inputs = []
        let mutable outputs = []

        for shader in shaders do
            let flags = VkShaderStageFlags.ofShaderStage shader.Stage 
            let iface = shader.Interface

            match shader.Stage with
                | ShaderStage.Vertex -> inputs <- iface.shaderInputs
                | ShaderStage.Fragment -> outputs <- iface.shaderOutputs
                | _ -> ()

            for block in iface.shaderUniformBuffers do
                let block = iface.program.uniformBuffers.[block]
                setCount <- max setCount (block.ubSet + 1)
                let key = (block.ubSet, block.ubBinding)
                let referenced = 
                    match uniformBlocks.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None  
                uniformBlocks.[key] <- (block, referenced ||| flags)
                         
            for block in iface.shaderStorageBuffers do
                let block = iface.program.storageBuffers.[block]
                setCount <- max setCount (block.ssbSet + 1)
                let key = (block.ssbSet, block.ssbBinding)
                let referenced = 
                    match storageBlocks.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None  
                storageBlocks.[key] <- (block, referenced ||| flags)
                                   
            for tex in iface.shaderSamplers do
                let tex = iface.program.samplers.[tex]
                setCount <- max setCount (tex.samplerSet + 1)
                let key = (tex.samplerSet, tex.samplerBinding)
                let referenced = 
                    match textures.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None
                                    
                textures.[key] <- (tex, referenced ||| flags)

            for img in iface.shaderImages do
                let img = iface.program.images.[img]
                setCount <- max setCount (img.imageSet + 1)
                let key = (img.imageSet, img.imageBinding)
                let referenced = 
                    match images.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None
                                    
                images.[key] <- (img, referenced ||| flags)
                            
        let uniformBlocks = uniformBlocks.Values |> Seq.toList
        let storageBlocks = storageBlocks.Values |> Seq.toList
        let textures = textures.Values |> Seq.toList
        let images = images.Values |> Seq.toList

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
                    VkPipelineLayoutCreateFlags.MinValue,
                    uint32 handles.Length, pHandles,
                    0u, NativePtr.zero
                )
            let! pHandle = VkPipelineLayout.Null
            VkRaw.vkCreatePipelineLayout(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create PipelineLayout"
  
            let info =    
                {
                    pInputs         = inputs
                    pOutputs        = outputs
                    pUniformBlocks  = List.map fst uniformBlocks
                    pStorageBlocks  = List.map fst storageBlocks
                    pTextures       = List.map fst textures
                    pImages         = List.map fst images
                    pEffectLayout   = None
                }    
                      
            return PipelineLayout(device, !!pHandle, setLayouts, info, layers, perLayer)
        }

    let delete (layout : PipelineLayout) (device : Device) =
        layout.RemoveRef()
    
    open FShade
    open FShade.Imperative

    let ofEffectInputLayout (layout : EffectInputLayout) (layers : int) (perLayer : Set<string>) (device : Device) : PipelineLayout =
        
        failf "not implemented"

[<AbstractClass; Sealed; Extension>]
type ContextPipelineLayoutExtensions private() =
    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, shaders : array<Shader>, layers : int, perLayer : Set<string>) =
        this |> PipelineLayout.ofShaders shaders layers perLayer

    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, layout : FShade.EffectInputLayout, layers : int, perLayer : Set<string>) =
        this |> PipelineLayout.ofEffectInputLayout layout layers perLayer

    [<Extension>]
    static member inline Delete(this : Device, layout : PipelineLayout) =
        this |> PipelineLayout.delete layout       