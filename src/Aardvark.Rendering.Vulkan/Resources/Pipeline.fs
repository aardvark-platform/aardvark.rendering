namespace Aardvark.Rendering.Vulkan

type Pipeline(device : Device, handle : VkPipeline) =
    inherit Resource<VkPipeline>(device, handle)

    override x.Destroy() =
        if x.Handle.IsValid then
            VkRaw.vkDestroyPipeline(x.Device.Handle, x.Handle, NativePtr.zero)
            x.Handle <- VkPipeline.Null

//open System
//open System.Threading
//open System.Runtime.CompilerServices
//open System.Runtime.InteropServices
//open Aardvark.Base

//open Aardvark.Rendering.Vulkan
//open Microsoft.FSharp.NativeInterop

//#nowarn "9"
//// #nowarn "51"

//type PipelineDescription =
//    {
//        renderPass              : RenderPass
//        shaderProgram           : ShaderProgram
//        vertexInputState        : Map<Symbol, VertexInputDescription>
//        inputAssembly           : InputAssemblyState
//        rasterizerState         : RasterizerState
//        colorBlendState         : ColorBlendState
//        multisampleState        : MultisampleState
//        depthState              : DepthState
//        stencilState            : StencilState
//        dynamicStates           : VkDynamicState[]
//    }

//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
//module Pipeline =

//    module private List =
//        let collecti (f : int -> 'a -> list<'b>) (m : list<'a>) =
//            m |> List.indexed |> List.collect (fun (i,v) -> f i v)

//    let createGraphics (desc : PipelineDescription) (device : Device) =
//        let vkbool b = if b then 1u else 0u

//        let prog = desc.shaderProgram

//        let inputs = prog.Inputs |> List.sortBy (fun p -> p.paramLocation)

//        let paramsWithInputs =
//            inputs |> List.map (fun p ->
//                match Map.tryFind (Symbol.Create p.paramSemantic) desc.vertexInputState with
//                    | Some ip -> 
//                        p.paramLocation, p, ip
//                    | None ->
//                        failf "could not get vertex input-type for %A" p
//            )

//        let inputBindings =
//            paramsWithInputs |> List.mapi (fun i (loc, p, ip) ->
//                VkVertexInputBindingDescription(
//                    uint32 i,
//                    uint32 ip.stride,
//                    ip.stepRate
//                )
//            )

//        let inputAttributes =
//            paramsWithInputs |> List.collecti (fun bi (loc, p, ip) ->
//                ip.offsets |> List.mapi (fun i off ->
//                    VkVertexInputAttributeDescription(
//                        uint32 (loc + i),
//                        uint32 bi,
//                        ip.inputFormat,
//                        uint32 off
//                    )
//                )
//            )

//        native {

//            let! pInputBindings = inputBindings
//            let! pInputAttributes = inputAttributes

//            let! pVertexInputState =
//                VkPipelineVertexInputStateCreateInfo(
//                    VkPipelineVertexInputStateCreateFlags.None,

//                    uint32 inputBindings.Length,
//                    pInputBindings,

//                    uint32 inputAttributes.Length,
//                    pInputAttributes
//                )

//            let! pInputAssemblyState =
//                VkPipelineInputAssemblyStateCreateInfo(
//                    VkPipelineInputAssemblyStateCreateFlags.None,

//                    desc.inputAssembly.topology,
//                    vkbool desc.inputAssembly.restartEnable
//                )
        
//            let! pRasterizerState =
//                let rs = desc.rasterizerState
//                VkPipelineRasterizationStateCreateInfo(
//                    VkPipelineRasterizationStateCreateFlags.None,
                
//                    vkbool rs.depthClampEnable,
//                    0u, //vkbool rs.rasterizerDiscardEnable, //breaks if true
//                    rs.polygonMode,
//                    rs.cullMode,
//                    rs.frontFace,
//                    vkbool rs.depthBiasEnable,
//                    float32 rs.depthBiasConstantFactor,
//                    float32 rs.depthBiasClamp,
//                    float32 rs.depthBiasSlopeFactor,
//                    float32 rs.lineWidth
//                )


//            let! pAttachmentBlendStates = 
//                desc.colorBlendState.attachmentStates |> Array.map (fun s ->
//                    VkPipelineColorBlendAttachmentState(
//                        vkbool s.enabled,
//                        s.srcFactor, s.dstFactor, s.operation,
//                        s.srcFactorAlpha, s.dstFactorAlpha, s.operationAlpha,
//                        s.colorWriteMask
//                    )
//                )

//            let! pColorBlendState =
//                let cb = desc.colorBlendState
//                VkPipelineColorBlendStateCreateInfo(
//                    VkPipelineColorBlendStateCreateFlags.None,

//                    vkbool cb.logicOpEnable,
//                    cb.logicOp,
//                    uint32 cb.attachmentStates.Length,
//                    pAttachmentBlendStates,
//                    cb.constant
//                )


//            let! pViewportState =
            
//                let vp  =
//                    if device.AllCount > 1u then
//                        if desc.renderPass.LayerCount > 1 then 1u
//                        else device.AllCount
//                    else 1u

//                VkPipelineViewportStateCreateInfo(
//                    VkPipelineViewportStateCreateFlags.None,
                
//                    uint32 vp,
//                    NativePtr.zero,

//                    uint32 vp,
//                    NativePtr.zero
//                )

//            let! pSampleMasks = desc.multisampleState.sampleMask
//            let! pMultisampleState =
//                let ms = desc.multisampleState
//                VkPipelineMultisampleStateCreateInfo(
//                    VkPipelineMultisampleStateCreateFlags.None,
                
//                    unbox ms.samples,
//                    vkbool ms.sampleShadingEnable,
//                    float32 ms.minSampleShading,
//                    pSampleMasks,
//                    vkbool ms.alphaToCoverageEnable,
//                    vkbool ms.alphaToOneEnable
//                )


//            let! pDepthStencilState =
//                let d = desc.depthState
//                let s = desc.stencilState
//                VkPipelineDepthStencilStateCreateInfo(
//                    VkPipelineDepthStencilStateCreateFlags.None,
                
//                    vkbool d.testEnabled,
//                    vkbool d.writeEnabled,
//                    d.compare,
//                    vkbool d.boundsTest,
//                    vkbool s.enabled,
//                    s.front,
//                    s.back,
//                    float32 d.depthBounds.Min,
//                    float32 d.depthBounds.Max
//                )

//            let shaderCreateInfos = desc.shaderProgram.ShaderCreateInfos
//            let! pShaderCreateInfos = shaderCreateInfos

//            let! pDynamicStates = Array.map uint32 desc.dynamicStates

//            let! pDynamicStates =
//                VkPipelineDynamicStateCreateInfo(
//                    VkPipelineDynamicStateCreateFlags.None, 

//                    uint32 desc.dynamicStates.Length,
//                    NativePtr.cast pDynamicStates
//                )

//            let! pTess =
//                VkPipelineTessellationStateCreateInfo(
//                    VkPipelineTessellationStateCreateFlags.None,
//                    10u
//                )

//            let! pPipelineCreateInfo =
//                VkGraphicsPipelineCreateInfo(
//                    VkPipelineCreateFlags.None,
//                    uint32 shaderCreateInfos.Length,
//                    pShaderCreateInfos,
//                    pVertexInputState,
//                    pInputAssemblyState,
//                    NativePtr.zero,
//                    pViewportState,
//                    pRasterizerState,
//                    pMultisampleState,
//                    pDepthStencilState,
//                    pColorBlendState,
//                    pDynamicStates,
//                    desc.shaderProgram.PipelineLayout.Handle,
//                    desc.renderPass.Handle,
//                    0u,
//                    VkPipeline.Null,
//                    -1
//                )

//            let! pPipeline = VkPipeline.Null
//            VkRaw.vkCreateGraphicsPipelines(device.Handle, VkPipelineCache.Null, 1u, pPipelineCreateInfo, NativePtr.zero, pPipeline) 
//                |> check "vkCreateGraphicsPipelines"
                
//            return new Pipeline(device, !!pPipeline, desc)
//        }


//[<AbstractClass; Sealed; Extension>]
//type ContextPipelineExtensions private() =
//    [<Extension>]
//    static member inline CreateGraphicsPipeline(this : Device, description : PipelineDescription) =
//        this |> Pipeline.createGraphics description
