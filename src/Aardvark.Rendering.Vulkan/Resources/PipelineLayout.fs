namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VkShaderStageFlags =
    let ofShaderStage =
        LookupTable.lookupTable [
            ShaderStage.Vertex, VkShaderStageFlags.VertexBit
            ShaderStage.TessControl, VkShaderStageFlags.TessellationControlBit
            ShaderStage.TessEval, VkShaderStageFlags.TessellationEvaluationBit
            ShaderStage.Geometry, VkShaderStageFlags.GeometryBit
            ShaderStage.Fragment, VkShaderStageFlags.FragmentBit
        ]


type PipelineLayout =
    class
        inherit Resource<VkPipelineLayout>

        val mutable public DescriptorSetLayouts : array<DescriptorSetLayout>

        val mutable public UniformBlocks : list<ShaderUniformBlock * VkShaderStageFlags>
        val mutable public Textures : list<ShaderTextureInfo * VkShaderStageFlags>

        new(device, handle, descriptorSetLayouts, ubs, tex) = 
            { inherit Resource<_>(device, handle); DescriptorSetLayouts = descriptorSetLayouts; UniformBlocks = ubs; Textures = tex }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PipelineLayout =

    let ofShaders (shaders : array<Shader>) (device : Device) =
        // figure out which stages reference which uniforms/textures
        let uniformBlocks = Dict.empty
        let textures = Dict.empty
        let mutable setCount = 0

        for shader in shaders do
            let flags = VkShaderStageFlags.ofShaderStage shader.Stage 
            let iface = shader.Interface
            for block in iface.uniformBlocks do
                setCount <- max setCount (block.set + 1)
                let key = (block.set, block.binding)
                let referenced = 
                    match uniformBlocks.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None  
                uniformBlocks.[key] <- (block, referenced ||| flags)
                                    
            for tex in iface.textures do
                setCount <- max setCount (tex.set + 1)
                let key = (tex.set, tex.binding)
                let referenced = 
                    match textures.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None
                                    
                textures.[key] <- (tex, referenced ||| flags)
                            
        let uniformBlocks = uniformBlocks.Values |> Seq.toList
        let textures = textures.Values |> Seq.toList

        // create DescriptorSetLayouts for all used slots (empty if no bindings)
        let sets = Array.init setCount (fun set -> CSharpList.empty)

        for (block, stageFlags) in uniformBlocks do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.UniformBuffer
                    stageFlags
                    (UniformBlockParameter block)
                    device

            sets.[block.set].Add binding

        for (tex, stageFlags) in textures do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.CombinedImageSampler
                    stageFlags
                    (ImageParameter tex)
                    device

            sets.[tex.set].Add binding

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
        handles |> NativePtr.withA (fun pHandles ->
            let mutable info =
                VkPipelineLayoutCreateInfo(
                    VkStructureType.PipelineLayoutCreateInfo, 0n,
                    VkPipelineLayoutCreateFlags.MinValue,
                    uint32 handles.Length, pHandles,
                    0u, NativePtr.zero
                )
            let mutable handle = VkPipelineLayout.Null
            VkRaw.vkCreatePipelineLayout(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create PipelineLayout"
            
            PipelineLayout(device, handle, setLayouts, uniformBlocks, textures)
        )

    let delete (layout : PipelineLayout) (device : Device) =
        if layout.Handle.IsValid then
            for b in layout.DescriptorSetLayouts do
                device |> DescriptorSetLayout.delete b

            VkRaw.vkDestroyPipelineLayout(device.Handle, layout.Handle, NativePtr.zero)
            layout.Handle <- VkPipelineLayout.Null
            layout.DescriptorSetLayouts <- Array.empty

[<AbstractClass; Sealed; Extension>]
type ContextPipelineLayoutExtensions private() =
    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, shaders : array<Shader>) =
        this |> PipelineLayout.ofShaders shaders

    [<Extension>]
    static member inline Delete(this : Device, layout : PipelineLayout) =
        this |> PipelineLayout.delete layout       