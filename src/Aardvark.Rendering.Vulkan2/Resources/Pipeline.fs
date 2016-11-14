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

type PipelineDescription =
    {
        renderPass              : RenderPass
        shaderProgram           : ShaderProgram
        vertexInputState        : Map<string, VertexInputDescription>
        inputAssembly           : InputAssemblyState
        rasterizerState         : RasterizerState
        colorBlendState         : ColorBlendState
        multisampleState        : MultisampleState
        depthState              : DepthState
        stencilState            : StencilState
        dynamicStates           : VkDynamicState[]
    }


type Pipeline =
    class
        inherit Resource<VkPipeline>
        val mutable public Description : PipelineDescription

        new(device : Device, handle : VkPipeline, description : PipelineDescription) = { inherit Resource<_>(device, handle); Description = description }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Pipeline =
    let createGraphics (desc : PipelineDescription) (device : Device) =
        let vkbool b = if b then 1u else 0u

        let prog = desc.shaderProgram

        let inputs = prog.Shaders.[ShaderStage.Vertex].Interface.inputs

        let paramsWithInputs =
            inputs |> List.map (fun p ->
                match Map.tryFind p.paramName desc.vertexInputState with
                    | Some ip -> 
                        match ShaderParameter.tryGetLocation p with
                            | Some loc -> loc, p, ip
                            | _ -> failf "could not get parameter location for %A" p
                    | None ->
                        failf "could not get vertex input-type for %A" p
            )

        let inputBindings =
            paramsWithInputs |> List.map (fun (loc, p, ip) ->
                VkVertexInputBindingDescription(
                    uint32 loc,
                    uint32 ip.stride,
                    ip.stepRate
                )
            )

        let inputAttributes =
            paramsWithInputs |> List.map (fun (loc, p, ip) ->
                VkVertexInputAttributeDescription(
                    uint32 loc,
                    uint32 loc,
                    ip.inputFormat,
                    uint32 ip.offset
                )
            )

        let pInputBindings = NativePtr.pushStackArray inputBindings
        let pInputAttributes = NativePtr.pushStackArray inputAttributes

        let mutable vertexInputState =
            VkPipelineVertexInputStateCreateInfo(
                VkStructureType.PipelineVertexInputStateCreateInfo, 0n, 
                VkPipelineVertexInputStateCreateFlags.MinValue,

                uint32 inputBindings.Length,
                pInputBindings,

                uint32 inputAttributes.Length,
                pInputAttributes
            )

        let mutable inputAssemblyState =
            VkPipelineInputAssemblyStateCreateInfo(
                VkStructureType.PipelineInputAssemblyStateCreateInfo, 0n, 
                VkPipelineInputAssemblyStateCreateFlags.MinValue,

                desc.inputAssembly.topology,
                vkbool desc.inputAssembly.restartEnable
            )
        
        let mutable rasterizerState =
            let rs = desc.rasterizerState
            VkPipelineRasterizationStateCreateInfo(
                VkStructureType.PipelineRasterizationStateCreateInfo, 0n,
                VkPipelineRasterizationStateCreateFlags.MinValue,
                
                vkbool rs.depthClampEnable,
                0u, //vkbool rs.rasterizerDiscardEnable, //breaks if true
                rs.polygonMode,
                rs.cullMode,
                rs.frontFace,
                vkbool rs.depthBiasEnable,
                float32 rs.depthBiasConstantFactor,
                float32 rs.depthBiasClamp,
                float32 rs.depthBiasSlopeFactor,
                float32 rs.lineWidth
            )


        let pAttachmentBlendStates = 
            NativePtr.pushStackArray (
                desc.colorBlendState.attachmentStates |> Array.map (fun s ->
                    VkPipelineColorBlendAttachmentState(
                        vkbool s.enabled,
                        s.srcFactor, s.dstFactor, s.operation,
                        s.srcFactorAlpha, s.dstFactorAlpha, s.operationAlpha,
                        s.colorWriteMask
                    )
                )
            )

        let mutable colorBlendState =
            let cb = desc.colorBlendState
            VkPipelineColorBlendStateCreateInfo(
                VkStructureType.PipelineColorBlendStateCreateInfo, 0n,
                VkPipelineColorBlendStateCreateFlags.MinValue,

                vkbool cb.logicOpEnable,
                cb.logicOp,
                uint32 cb.attachmentStates.Length,
                pAttachmentBlendStates,
                cb.constants
            )


        let mutable viewportState =
            let vp = desc.renderPass.AttachmentCount
            VkPipelineViewportStateCreateInfo(
                VkStructureType.PipelineViewportStateCreateInfo, 0n,
                VkPipelineViewportStateCreateFlags.MinValue,
                
                uint32 vp,
                NativePtr.zero,

                uint32 vp,
                NativePtr.zero
            )

        let pSampleMasks = NativePtr.pushStackArray desc.multisampleState.sampleMask
        let mutable multisampleState =
            let ms = desc.multisampleState
            VkPipelineMultisampleStateCreateInfo(
                VkStructureType.PipelineMultisampleStateCreateInfo, 0n,
                VkPipelineMultisampleStateCreateFlags.MinValue,
                
                unbox ms.samples,
                vkbool ms.sampleShadingEnable,
                float32 ms.minSampleShading,
                pSampleMasks,
                vkbool ms.alphaToCoverageEnable,
                vkbool ms.alphaToOneEnable
            )


        let mutable depthStencilState =
            let d = desc.depthState
            let s = desc.stencilState
            VkPipelineDepthStencilStateCreateInfo(
                VkStructureType.PipelineDepthStencilStateCreateInfo, 0n,
                VkPipelineDepthStencilStateCreateFlags.MinValue,
                
                vkbool d.testEnabled,
                vkbool d.writeEnabled,
                d.compare,
                vkbool d.boundsTest,
                vkbool s.enabled,
                s.front,
                s.back,
                float32 d.depthBounds.Min,
                float32 d.depthBounds.Max
            )

        let pEntryName = CStr.salloc "main"
        let shaderCreateInfos =
            desc.shaderProgram.Shaders 
                |> Map.toArray
                |> Array.map (fun (_,sh) ->
                    VkPipelineShaderStageCreateInfo(
                        VkStructureType.PipelineShaderStageCreateInfo, 0n, 
                        VkPipelineShaderStageCreateFlags.MinValue,

                        VkShaderStageFlags.ofShaderStage sh.Stage,
                        sh.Handle,
                        pEntryName,
                        NativePtr.zero
                    )
                )
        let pShaderCreateInfos = NativePtr.pushStackArray shaderCreateInfos

        let pDynamicStates = NativePtr.pushStackArray desc.dynamicStates

        let mutable dynamicStates =
            VkPipelineDynamicStateCreateInfo(
                VkStructureType.PipelineDynamicStateCreateInfo, 0n,
                VkPipelineDynamicStateCreateFlags.MinValue, 

                uint32 desc.dynamicStates.Length,
                pDynamicStates
            )


        let mutable pipelineCreateInfo =
            VkGraphicsPipelineCreateInfo(
                VkStructureType.GraphicsPipelineCreateInfo,
                0n, VkPipelineCreateFlags.None,
                uint32 shaderCreateInfos.Length,
                pShaderCreateInfos,
                &&vertexInputState,
                &&inputAssemblyState,
                NativePtr.zero,
                &&viewportState,
                &&rasterizerState,
                &&multisampleState,
                &&depthStencilState,
                &&colorBlendState,
                &&dynamicStates,
                desc.shaderProgram.PipelineLayout.Handle,
                desc.renderPass.Handle,
                0u,
                VkPipeline.Null,
                -1
            )

        let mutable pipeline = VkPipeline.Null
        VkRaw.vkCreateGraphicsPipelines(device.Handle, VkPipelineCache.Null, 1u, &&pipelineCreateInfo, NativePtr.zero, &&pipeline) 
            |> check "vkCreateGraphicsPipelines"


        Pipeline(device, pipeline, desc)

    let delete (p : Pipeline) (device : Device) =
        if p.Handle.IsValid then
            VkRaw.vkDestroyPipeline(device.Handle, p.Handle, NativePtr.zero)
            p.Handle <- VkPipeline.Null


[<AbstractClass; Sealed; Extension>]
type ContextPipelineExtensions private() =
    [<Extension>]
    static member inline CreateGraphicsPipeline(this : Device, description : PipelineDescription) =
        this |> Pipeline.createGraphics description

    [<Extension>]
    static member inline Delete(this : Device, pipeline : Pipeline) =
        this |> Pipeline.delete pipeline
