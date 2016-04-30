namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering

#nowarn "9"
#nowarn "51"


type InputAssemblyState =
    {
        topology        : VkPrimitiveTopology
        restartEnable   : bool
    }

type RasterizerState =
    {
        depthClampEnable        : bool
        rasterizerDiscardEnable : bool
        polygonMode             : VkPolygonMode
        cullMode                : VkCullModeFlags
        frontFace               : VkFrontFace
        depthBiasEnable         : bool
        depthBiasConstantFactor : float
        depthBiasClamp          : float
        depthBiasSlopeFactor    : float
        lineWidth               : float
    }

type ColorBlendAttachmentState =
    {
        enabled                 : bool
        srcFactor               : VkBlendFactor
        dstFactor               : VkBlendFactor
        operation               : VkBlendOp
        srcFactorAlpha          : VkBlendFactor
        dstFactorAlpha          : VkBlendFactor
        operationAlpha          : VkBlendOp
        colorWriteMask          : VkColorComponentFlags
    }

type ColorBlendState =
    {
        logicOpEnable           : bool
        logicOp                 : VkLogicOp
        attachmentStates        : ColorBlendAttachmentState[]
        constants               : V4f
    }

type MultisampleState =
    {
        samples                 : int
        sampleShadingEnable     : bool
        minSampleShading        : float
        sampleMask              : uint32[]
        alphaToCoverageEnable   : bool
        alphaToOneEnable        : bool
    }

type VertexInputDescription =
    {
        inputFormat             : VkFormat
        stride                  : int
        stepRate                : VkVertexInputRate
        offset                  : int
    }

type DepthState =
    {
        testEnabled             : bool
        writeEnabled            : bool
        boundsTest              : bool
        compare                 : VkCompareOp
        depthBounds             : Range1d
    }

type StencilState =
    {
        enabled                 : bool
        front                   : VkStencilOpState
        back                    : VkStencilOpState
    }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VertexInputState =
    
    let private toVkFormat =
        lookupTable [
            typeof<int8>, VkFormat.R8Sint
            typeof<uint8>, VkFormat.R8Uint

            typeof<int16>, VkFormat.R16Sint
            typeof<uint16>, VkFormat.R16Uint

            typeof<int32>, VkFormat.R32Sint
            typeof<uint32>, VkFormat.R32Uint

            typeof<V2i>, VkFormat.R32g32Sint
            typeof<V3i>, VkFormat.R32g32b32Sint
            typeof<V4i>, VkFormat.R32g32b32a32Sint


            typeof<float32>, VkFormat.R32Sfloat
            typeof<V2f>, VkFormat.R32g32Sfloat
            typeof<V3f>, VkFormat.R32g32b32Sfloat
            typeof<V4f>, VkFormat.R32g32b32a32Sfloat

            typeof<double>, VkFormat.R64Sfloat
            typeof<V2d>, VkFormat.R64g64Sfloat
            typeof<V3d>, VkFormat.R64g64b64Sfloat
            typeof<V4d>, VkFormat.R64g64b64a64Sfloat

            // TODO: is that really correct here???
            typeof<C3b>, VkFormat.B8g8r8Unorm
            typeof<C4b>, VkFormat.B8g8r8a8Unorm

            typeof<C3us>, VkFormat.R16g16b16Unorm
            typeof<C4us>, VkFormat.R16g16b16a16Unorm

            typeof<C3ui>, VkFormat.R32g32b32Uint
            typeof<C4ui>, VkFormat.R32g32b32a32Uint

            typeof<C3f>, VkFormat.R32g32b32Sfloat
            typeof<C4f>, VkFormat.R32g32b32a32Sfloat

            typeof<C3d>, VkFormat.R64g64b64Sfloat
            typeof<C4d>, VkFormat.R64g64b64a64Sfloat

        ]

    let private getFormatSize =
        lookupTable [
            //VkFormat.R4g4Unorm, 1
            //VkFormat.R4g4Uscaled, 1
            //VkFormat.R4g4b4a4Unorm, 2
            //VkFormat.R4g4b4a4Uscaled, 2
            //VkFormat.R5g6b5Unorm, 2
            //VkFormat.R5g6b5Uscaled, 2
            //VkFormat.R5g5b5a1Unorm, 2
            //VkFormat.R5g5b5a1Uscaled, 2
            VkFormat.R8Unorm, 1
            VkFormat.R8Snorm, 1
            VkFormat.R8Uscaled, 1
            VkFormat.R8Sscaled, 1
            VkFormat.R8Uint, 1
            VkFormat.R8Sint, 1
            VkFormat.R8Srgb, 1
            VkFormat.R8g8Unorm, 2
            VkFormat.R8g8Snorm, 2
            VkFormat.R8g8Uscaled, 2
            VkFormat.R8g8Sscaled, 2
            VkFormat.R8g8Uint, 2
            VkFormat.R8g8Sint, 2
            VkFormat.R8g8Srgb, 2
            VkFormat.R8g8b8Unorm, 3
            VkFormat.R8g8b8Snorm, 3
            VkFormat.R8g8b8Uscaled, 3
            VkFormat.R8g8b8Sscaled, 3
            VkFormat.R8g8b8Uint, 3
            VkFormat.R8g8b8Sint, 3
            VkFormat.R8g8b8Srgb, 3
            VkFormat.R8g8b8a8Unorm, 4
            VkFormat.R8g8b8a8Snorm, 4
            VkFormat.R8g8b8a8Uscaled, 4
            VkFormat.R8g8b8a8Sscaled, 4
            VkFormat.R8g8b8a8Uint, 4
            VkFormat.R8g8b8a8Sint, 4
            VkFormat.R8g8b8a8Srgb, 4
            //VkFormat.R10g10b10a2Unorm, 4
            //VkFormat.R10g10b10a2Snorm, 4
            //VkFormat.R10g10b10a2Uscaled, 4
            //VkFormat.R10g10b10a2Sscaled, 4
            //VkFormat.R10g10b10a2Uint, 4
            //VkFormat.R10g10b10a2Sint, 4
            VkFormat.R16Unorm, 2
            VkFormat.R16Snorm, 2
            VkFormat.R16Uscaled, 2
            VkFormat.R16Sscaled, 2
            VkFormat.R16Uint, 2
            VkFormat.R16Sint, 2
            VkFormat.R16Sfloat, 2
            VkFormat.R16g16Unorm, 4
            VkFormat.R16g16Snorm, 4
            VkFormat.R16g16Uscaled, 4
            VkFormat.R16g16Sscaled, 4
            VkFormat.R16g16Uint, 4
            VkFormat.R16g16Sint, 4
            VkFormat.R16g16Sfloat, 4
            VkFormat.R16g16b16Unorm, 6
            VkFormat.R16g16b16Snorm, 6
            VkFormat.R16g16b16Uscaled, 6
            VkFormat.R16g16b16Sscaled, 6
            VkFormat.R16g16b16Uint, 6
            VkFormat.R16g16b16Sint, 6
            VkFormat.R16g16b16Sfloat, 6
            VkFormat.R16g16b16a16Unorm, 8
            VkFormat.R16g16b16a16Snorm, 8
            VkFormat.R16g16b16a16Uscaled, 8
            VkFormat.R16g16b16a16Sscaled, 8
            VkFormat.R16g16b16a16Uint, 8
            VkFormat.R16g16b16a16Sint, 8
            VkFormat.R16g16b16a16Sfloat, 8
            VkFormat.R32Uint, 4
            VkFormat.R32Sint, 4
            VkFormat.R32Sfloat, 4
            VkFormat.R32g32Uint, 8
            VkFormat.R32g32Sint, 8
            VkFormat.R32g32Sfloat, 8
            VkFormat.R32g32b32Uint, 12
            VkFormat.R32g32b32Sint, 12
            VkFormat.R32g32b32Sfloat, 12
            VkFormat.R32g32b32a32Uint, 16
            VkFormat.R32g32b32a32Sint, 16
            VkFormat.R32g32b32a32Sfloat, 16
            VkFormat.R64Sfloat, 8
            VkFormat.R64g64Sfloat, 16
            VkFormat.R64g64b64Sfloat, 24
            VkFormat.R64g64b64a64Sfloat, 32
            //VkFormat.R11g11b10Ufloat, 4
            //VkFormat.R9g9b9e5Ufloat, 4
            VkFormat.D16Unorm, 2
            VkFormat.X8D24UnormPack32, 4
            VkFormat.D32Sfloat, 4
            VkFormat.S8Uint, 1
            VkFormat.D16UnormS8Uint, 3
            VkFormat.D24UnormS8Uint, 4
            VkFormat.D32SfloatS8Uint, 5
            //VkFormat.Bc1RgbUnorm, failwith "no"
            //VkFormat.Bc1RgbSrgb, failwith "no"
            //VkFormat.Bc1RgbaUnorm, failwith "no"
            //VkFormat.Bc1RgbaSrgb, failwith "no"
            //VkFormat.Bc2Unorm, failwith "no"
            //VkFormat.Bc2Srgb, failwith "no"
            //VkFormat.Bc3Unorm, failwith "no"
            //VkFormat.Bc3Srgb, failwith "no"
            //VkFormat.Bc4Unorm, failwith "no"
            //VkFormat.Bc4Snorm, failwith "no"
            //VkFormat.Bc5Unorm, failwith "no"
            //VkFormat.Bc5Snorm, failwith "no"
            //VkFormat.Bc6hUfloat, failwith "no"
            //VkFormat.Bc6hSfloat, failwith "no"
            //VkFormat.Bc7Unorm, failwith "no"
            //VkFormat.Bc7Srgb, failwith "no"
            //VkFormat.Etc2R8g8b8Unorm, failwith "no"
            //VkFormat.Etc2R8g8b8Srgb, failwith "no"
            //VkFormat.Etc2R8g8b8a1Unorm, failwith "no"
            //VkFormat.Etc2R8g8b8a1Srgb, failwith "no"
            //VkFormat.Etc2R8g8b8a8Unorm, failwith "no"
            //VkFormat.Etc2R8g8b8a8Srgb, failwith "no"
            //VkFormat.EacR11Unorm, failwith "no"
            //VkFormat.EacR11Snorm, failwith "no"
            //VkFormat.EacR11g11Unorm, failwith "no"
            //VkFormat.EacR11g11Snorm, failwith "no"
            //VkFormat.Astc44Unorm, failwith "no"
            //VkFormat.Astc44Srgb, failwith "no"
            //VkFormat.Astc54Unorm, failwith "no"
            //VkFormat.Astc54Srgb, failwith "no"
            //VkFormat.Astc55Unorm, failwith "no"
            //VkFormat.Astc55Srgb, failwith "no"
            //VkFormat.Astc65Unorm, failwith "no"
            //VkFormat.Astc65Srgb, failwith "no"
            //VkFormat.Astc66Unorm, failwith "no"
            //VkFormat.Astc66Srgb, failwith "no"
            //VkFormat.Astc85Unorm, failwith "no"
            //VkFormat.Astc85Srgb, failwith "no"
            //VkFormat.Astc86Unorm, failwith "no"
            //VkFormat.Astc86Srgb, failwith "no"
            //VkFormat.Astc88Unorm, failwith "no"
            //VkFormat.Astc88Srgb, failwith "no"
            //VkFormat.Astc105Unorm, failwith "no"
            //VkFormat.Astc105Srgb, failwith "no"
            //VkFormat.Astc106Unorm, failwith "no"
            //VkFormat.Astc106Srgb, failwith "no"
            //VkFormat.Astc108Unorm, failwith "no"
            //VkFormat.Astc108Srgb, failwith "no"
            //VkFormat.Astc1010Unorm, failwith "no"
            //VkFormat.Astc1010Srgb, failwith "no"
            //VkFormat.Astc1210Unorm, failwith "no"
            //VkFormat.Astc1210Srgb, failwith "no"
            //VkFormat.Astc1212Unorm, failwith "no"
            //VkFormat.Astc1212Srgb, failwith "no"
            //VkFormat.B4g4r4a4Unorm, 2
            //VkFormat.B5g5r5a1Unorm, 2
            //VkFormat.B5g6r5Unorm, 2
            //VkFormat.B5g6r5Uscaled, 2
            VkFormat.B8g8r8Unorm, 3
            VkFormat.B8g8r8Snorm, 3
            VkFormat.B8g8r8Uscaled, 3
            VkFormat.B8g8r8Sscaled, 3
            VkFormat.B8g8r8Uint, 3
            VkFormat.B8g8r8Sint, 3
            VkFormat.B8g8r8Srgb, 3
            VkFormat.B8g8r8a8Unorm, 4
            VkFormat.B8g8r8a8Snorm, 4
            VkFormat.B8g8r8a8Uscaled, 4
            VkFormat.B8g8r8a8Sscaled, 4
            VkFormat.B8g8r8a8Uint, 4
            VkFormat.B8g8r8a8Sint, 4
            VkFormat.B8g8r8a8Srgb, 4
            //VkFormat.B10g10r10a2Unorm, 4
            //VkFormat.B10g10r10a2Snorm, 4
            //VkFormat.B10g10r10a2Uscaled, 4
            //VkFormat.B10g10r10a2Sscaled, 4
            //VkFormat.B10g10r10a2Uint, 4
            //VkFormat.B10g10r10a2Sint, 4

        ]

    let create (o : Map<string, bool * Type>) =
        o |> Map.map (fun k (perInstance, attType) ->
            let fmt = toVkFormat attType
            { 
                inputFormat = fmt
                stride = getFormatSize fmt
                stepRate = (if perInstance then VkVertexInputRate.Instance else VkVertexInputRate.Vertex)
                offset = 0
            }
        )
 
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RasterizerState =
    let private toVkPolygonMode =
        lookupTable [
            FillMode.Fill, VkPolygonMode.Fill
            FillMode.Line, VkPolygonMode.Line
            FillMode.Point, VkPolygonMode.Point
        ]

    let private toVkCullMode =
        lookupTable [
            CullMode.Clockwise, VkCullModeFlags.FrontBit
            CullMode.CounterClockwise, VkCullModeFlags.BackBit
            CullMode.None, VkCullModeFlags.None
        ]

   
    let create (cull : CullMode) (fill : FillMode) =
        {
            depthClampEnable        = false
            rasterizerDiscardEnable = false
            polygonMode             = toVkPolygonMode fill
            cullMode                = toVkCullMode cull
            frontFace               = VkFrontFace.Clockwise
            depthBiasEnable         = false
            depthBiasConstantFactor = 0.0
            depthBiasClamp          = 0.0
            depthBiasSlopeFactor    = 0.0
            lineWidth               = 1.0
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ColorBlendState =


    let private toVkBlendFactor =
        lookupTable [
            BlendFactor.SecondarySourceAlpha, VkBlendFactor.Src1Alpha
            BlendFactor.SecondarySourceColor, VkBlendFactor.Src1Color
            BlendFactor.InvSecondarySourceAlpha, VkBlendFactor.OneMinusSrc1Alpha
            BlendFactor.InvSecondarySourceColor, VkBlendFactor.OneMinusSrc1Color

            BlendFactor.DestinationAlpha, VkBlendFactor.DstAlpha
            BlendFactor.DestinationColor, VkBlendFactor.DstColor
            BlendFactor.InvDestinationAlpha, VkBlendFactor.OneMinusDstAlpha
            BlendFactor.InvDestinationColor, VkBlendFactor.OneMinusDstColor

            BlendFactor.SourceAlpha, VkBlendFactor.SrcAlpha
            BlendFactor.SourceColor, VkBlendFactor.SrcColor
            BlendFactor.InvSourceAlpha, VkBlendFactor.OneMinusSrcAlpha
            BlendFactor.InvSourceColor, VkBlendFactor.OneMinusSrcColor

            BlendFactor.Zero, VkBlendFactor.Zero
            BlendFactor.One, VkBlendFactor.One
            BlendFactor.SourceAlphaSat, VkBlendFactor.SrcAlphaSaturate

            BlendFactor.BlendFactor, VkBlendFactor.ConstantColor
            BlendFactor.InvBlendFactor, VkBlendFactor.OneMinusConstantColor
        ]

    let private toVkBlendOp =
        lookupTable [
            BlendOperation.Add, VkBlendOp.Add
            BlendOperation.Maximum, VkBlendOp.Max
            BlendOperation.Minimum, VkBlendOp.Min
            BlendOperation.ReverseSubtract, VkBlendOp.ReverseSubtract
            BlendOperation.Subtract, VkBlendOp.Subtract
        ]

    let private rgba = VkColorComponentFlags.RBit ||| VkColorComponentFlags.GBit ||| VkColorComponentFlags.BBit ||| VkColorComponentFlags.ABit

    let private toAttachmentState (blend : BlendMode) =
        {
            enabled                 = blend.Enabled
            srcFactor               = toVkBlendFactor blend.SourceFactor
            dstFactor               = toVkBlendFactor blend.DestinationFactor
            operation               = toVkBlendOp blend.Operation
            srcFactorAlpha          = toVkBlendFactor blend.SourceAlphaFactor
            dstFactorAlpha          = toVkBlendFactor blend.DestinationAlphaFactor
            operationAlpha          = toVkBlendOp blend.AlphaOperation
            colorWriteMask          = rgba
        }
    
    let create (count : int) (blend : BlendMode) =
        {
            logicOpEnable           = false
            logicOp                 = VkLogicOp.NoOp
            attachmentStates        = Array.create count (toAttachmentState blend)
            constants               = V4f.IIII
        }
 
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MultisampleState =
    let create (samples : int) =
        {
            samples                 = samples
            sampleShadingEnable     = false
            minSampleShading        = 0.0
            sampleMask              = [||]
            alphaToCoverageEnable   = false
            alphaToOneEnable        = false
        }
 
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DepthState =
    let private toVkCompareOp =
        lookupTable [
            DepthTestMode.None, VkCompareOp.Always
            DepthTestMode.Greater, VkCompareOp.Greater
            DepthTestMode.GreaterOrEqual, VkCompareOp.GreaterOrEqual
            DepthTestMode.Less, VkCompareOp.Less
            DepthTestMode.LessOrEqual, VkCompareOp.LessOrEqual
        ]

    let create (mode : DepthTestMode) =
        {
            testEnabled             = mode <> DepthTestMode.None
            writeEnabled            = true
            boundsTest              = false
            compare                 = toVkCompareOp mode
            depthBounds             = Range1d(0.0, 1.0)
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StencilState =
    let private toVkStencilOp =
        lookupTable [
            StencilOperationFunction.Increment, VkStencilOp.IncrementAndClamp
            StencilOperationFunction.IncrementWrap, VkStencilOp.IncrementAndWrap
            StencilOperationFunction.Decrement, VkStencilOp.DecrementAndClamp
            StencilOperationFunction.DecrementWrap, VkStencilOp.DecrementAndWrap
            StencilOperationFunction.Keep, VkStencilOp.Keep
            StencilOperationFunction.Replace, VkStencilOp.Replace
            StencilOperationFunction.Zero, VkStencilOp.Zero
            StencilOperationFunction.Invert, VkStencilOp.Invert
        ]

    let private toVkStencilCompareOp  =
        lookupTable [
            StencilCompareFunction.Always, VkCompareOp.Always
            StencilCompareFunction.Equal, VkCompareOp.Equal
            StencilCompareFunction.Greater, VkCompareOp.Greater
            StencilCompareFunction.GreaterOrEqual, VkCompareOp.GreaterOrEqual
            StencilCompareFunction.Less, VkCompareOp.Less
            StencilCompareFunction.LessOrEqual, VkCompareOp.LessOrEqual
            StencilCompareFunction.Never, VkCompareOp.Never
            StencilCompareFunction.NotEqual, VkCompareOp.NotEqual
        ]

    let private toVkStencilOpState (op : StencilOperation) (cmp : StencilFunction) =
        VkStencilOpState(toVkStencilOp op.StencilFail, toVkStencilOp op.DepthPass, toVkStencilOp op.DepthFail, toVkStencilCompareOp cmp.Function, cmp.Mask, cmp.Mask, uint32 cmp.Reference)
    
    let create (mode : StencilMode) =
        {
            enabled                 = mode.IsEnabled
            front                   = toVkStencilOpState mode.OperationFront mode.CompareFront
            back                    = toVkStencilOpState mode.OperationBack mode.CompareBack
        }
    
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
        val mutable public Context : Context
        val mutable public Handle : VkPipeline
        val mutable public Description : PipelineDescription


        new(ctx, handle, desc) = { Context = ctx; Handle = handle; Description = desc }
    end

[<AbstractClass; Sealed; Extension>]
type PipelineExtensions private() =

    [<Extension>]
    static member CreateGraphicsPipeline(this : Context, desc : PipelineDescription) =
        let vkbool b = if b then 1u else 0u

        let prog = desc.shaderProgram

        let paramsWithInputs =
            prog.Inputs |> Array.map (fun p ->
                match Map.tryFind p.paramName desc.vertexInputState with
                    | Some ip -> 
                        match Parameter.tryGetLocation p with
                            | Some loc -> loc, p, ip
                            | _ -> failf "could not get parameter location for %A" p
                    | None ->
                        failf "could not get vertex input-type for %A" p
            )

        let inputBindings =
            paramsWithInputs |> Array.map (fun (loc, p, ip) ->
                VkVertexInputBindingDescription(
                    uint32 loc,
                    uint32 ip.stride,
                    ip.stepRate
                )
            )

        let inputAttributes =
            paramsWithInputs |> Array.map (fun (loc, p, ip) ->
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
            let vp = desc.renderPass.ColorAttachments.Length
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
                |> List.toArray
                |> Array.map (fun sh ->
                    VkPipelineShaderStageCreateInfo(
                        VkStructureType.PipelineShaderStageCreateInfo, 0n, 
                        VkPipelineShaderStageCreateFlags.MinValue,

                        sh.Stage,
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
        VkRaw.vkCreateGraphicsPipelines(this.Device.Handle, VkPipelineCache.Null, 1u, &&pipelineCreateInfo, NativePtr.zero, &&pipeline) 
            |> check "vkCreateGraphicsPipelines"


        Pipeline(this, pipeline, desc)

    [<Extension>]
    static member Delete(this : Context, p : Pipeline) =
        if p.Handle.IsValid then
            VkRaw.vkDestroyPipeline(this.Device.Handle, p.Handle, NativePtr.zero)
            p.Handle <- VkPipeline.Null
            p.Description <- Unchecked.defaultof<_>
