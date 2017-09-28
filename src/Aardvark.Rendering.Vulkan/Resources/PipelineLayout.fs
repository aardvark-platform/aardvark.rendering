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
            ShaderStage.Compute, VkShaderStageFlags.ComputeBit
        ]


type PipelineLayout =
    class
        inherit Resource<VkPipelineLayout>

        val mutable public DescriptorSetLayouts : array<DescriptorSetLayout>
        val mutable public PipelineInfo : PipelineInfo

        val mutable public UniformBlocks : list<ShaderUniformBlock * VkShaderStageFlags>
        val mutable public Textures : list<ShaderTextureInfo * VkShaderStageFlags>
        val mutable public ReferenceCount : int

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
                    |> List.map (fun p -> p.location, (Symbol.Create p.name, AttachmentSignature.ofType p.hostType)) 
                    |> Map.ofList
            member x.IsAssignableFrom _ = false
            member x.Images = Map.empty
            member x.Runtime = Unchecked.defaultof<_>
            member x.StencilAttachment = None
            member x.DepthAttachment = None

        new(device, handle, descriptorSetLayouts, ubs, tex, info) = 
            { inherit Resource<_>(device, handle); DescriptorSetLayouts = descriptorSetLayouts; UniformBlocks = ubs; Textures = tex; PipelineInfo = info; ReferenceCount = 1 }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PipelineLayout =

    let ofPipelineInfo (layout : PipelineInfo) (device : Device) =
        let uniformBlocks = Dict.empty
        let textures = Dict.empty
        let storageBlocks = Dict.empty
        let mutable setCount = 0

        for block in layout.pUniformBlocks do
            setCount <- max setCount (block.set + 1)
            let key = (block.set, block.binding)
            uniformBlocks.[key] <- block
                         
        for block in layout.pStorageBlocks do
            setCount <- max setCount (block.set + 1)
            let key = (block.set, block.binding) 
            storageBlocks.[key] <- block
                                   
        for tex in layout.pTextures do
            setCount <- max setCount (tex.set + 1)
            let key = (tex.set, tex.binding)
            textures.[key] <- tex
        let uniformBlocks = uniformBlocks.Values |> Seq.toList
        let storageBlocks = storageBlocks.Values |> Seq.toList
        let textures = textures.Values |> Seq.toList

        // create DescriptorSetLayouts for all used slots (empty if no bindings)
        let sets = Array.init setCount (fun set -> CSharpList.empty)

        for block in uniformBlocks do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.UniformBuffer
                    VkShaderStageFlags.All
                    (UniformBlockParameter block)
                    device

            sets.[block.set].Add binding

        for block in storageBlocks do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.StorageBuffer
                    VkShaderStageFlags.All
                    (UniformBlockParameter block)
                    device

            sets.[block.set].Add binding
            

        for tex in textures do
            let descriptorType = 
                if tex.isSampled then VkDescriptorType.CombinedImageSampler
                else VkDescriptorType.StorageImage

            let binding = 
                DescriptorSetLayoutBinding.create
                    descriptorType
                    VkShaderStageFlags.All
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
            
            let uniformBlocks =
                uniformBlocks |> List.map (fun b -> (b, VkShaderStageFlags.All))

            let textures =
                textures |> List.map (fun t -> (t, VkShaderStageFlags.All))

            PipelineLayout(device, handle, setLayouts, uniformBlocks, textures, layout)
        )

    let ofEffectLayout (layout : FShade.EffectInputLayout) (device : Device) =
        let info = PipelineInfo.ofEffectLayout layout Map.empty
        ofPipelineInfo info device
 

    let ofShaders (shaders : array<Shader>) (device : Device) =
        // figure out which stages reference which uniforms/textures
        let uniformBlocks = Dict.empty
        let textures = Dict.empty
        let storageBlocks = Dict.empty
        let mutable setCount = 0

        let mutable inputs = []
        let mutable outputs = []

        for shader in shaders do
            let flags = VkShaderStageFlags.ofShaderStage shader.Stage 
            let iface = shader.Interface

            match shader.Stage with
                | ShaderStage.Vertex -> inputs <- iface.inputs
                | ShaderStage.Fragment -> outputs <- iface.outputs
                | _ -> ()

            for block in iface.uniformBlocks do
                setCount <- max setCount (block.set + 1)
                let key = (block.set, block.binding)
                let referenced = 
                    match uniformBlocks.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None  
                uniformBlocks.[key] <- (block, referenced ||| flags)
                         
            for block in iface.storageBlocks do
                setCount <- max setCount (block.set + 1)
                let key = (block.set, block.binding)
                let referenced = 
                    match uniformBlocks.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None  
                storageBlocks.[key] <- (block, referenced ||| flags)
                                   
            for tex in iface.textures do
                setCount <- max setCount (tex.set + 1)
                let key = (tex.set, tex.binding)
                let referenced = 
                    match textures.TryGetValue key with
                        | (true, (_, referencedBy)) -> referencedBy
                        | _ -> VkShaderStageFlags.None
                                    
                textures.[key] <- (tex, referenced ||| flags)
                            
        let uniformBlocks = uniformBlocks.Values |> Seq.toList
        let storageBlocks = storageBlocks.Values |> Seq.toList
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

        for (block, stageFlags) in storageBlocks do
            let binding = 
                DescriptorSetLayoutBinding.create
                    VkDescriptorType.StorageBuffer
                    stageFlags
                    (UniformBlockParameter block)
                    device

            sets.[block.set].Add binding
            

        for (tex, stageFlags) in textures do
            let descriptorType = 
                if tex.isSampled then VkDescriptorType.CombinedImageSampler
                else VkDescriptorType.StorageImage

            let binding = 
                DescriptorSetLayoutBinding.create
                    descriptorType
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
  
            let info =    
                {
                    pInputs         = inputs
                    pOutputs        = outputs
                    pUniformBlocks  = List.map fst uniformBlocks
                    pStorageBlocks  = List.map fst storageBlocks
                    pTextures       = List.map fst textures
                    pEffectLayout   = None
                }    
                      
            PipelineLayout(device, handle, setLayouts, uniformBlocks, textures, info)
        )

    let delete (layout : PipelineLayout) (device : Device) =
        layout.RemoveRef()

[<AbstractClass; Sealed; Extension>]
type ContextPipelineLayoutExtensions private() =
    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, shaders : array<Shader>) =
        this |> PipelineLayout.ofShaders shaders
        
    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, info : PipelineInfo) =
        this |> PipelineLayout.ofPipelineInfo info

    [<Extension>]
    static member inline CreatePipelineLayout(this : Device, info : FShade.EffectInputLayout) =
        this |> PipelineLayout.ofEffectLayout info


    [<Extension>]
    static member inline Delete(this : Device, layout : PipelineLayout) =
        this |> PipelineLayout.delete layout       