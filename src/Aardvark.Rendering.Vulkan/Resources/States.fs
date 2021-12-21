﻿namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base

open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

#nowarn "9"
// #nowarn "51"

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
        conservativeRaster      : bool
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
        constant                : V4f
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
        offsets                 : list<int>
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
module InputAssemblyState =
    let ofIndexedGeometryMode (hasTessellation : bool) (mode : IndexedGeometryMode) =
        if hasTessellation then
            { topology = VkPrimitiveTopology.PatchList; restartEnable = false }
        else
            match mode with
            | IndexedGeometryMode.LineAdjacencyList ->      { topology = VkPrimitiveTopology.LineListWithAdjacency; restartEnable = false }
            | IndexedGeometryMode.LineList ->               { topology = VkPrimitiveTopology.LineList; restartEnable = false }
            | IndexedGeometryMode.LineStrip ->              { topology = VkPrimitiveTopology.LineStrip; restartEnable = true }
            | IndexedGeometryMode.PointList ->              { topology = VkPrimitiveTopology.PointList; restartEnable = false }
            | IndexedGeometryMode.TriangleAdjacencyList ->  { topology = VkPrimitiveTopology.TriangleListWithAdjacency; restartEnable = false }
            | IndexedGeometryMode.TriangleList ->           { topology = VkPrimitiveTopology.TriangleList; restartEnable = false }
            | IndexedGeometryMode.TriangleStrip ->          { topology = VkPrimitiveTopology.TriangleStrip; restartEnable = true }
            | IndexedGeometryMode.QuadList ->
                failwith "Vulkan backend does not support quad geometry"
            | _ ->
                failwithf "Unknown indexed geometry mode %A" mode

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VertexInputState =

    let private toVkFormat =
        LookupTable.lookupTable [
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
        LookupTable.lookupTable [
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

    let create (o : Map<Symbol, bool * Aardvark.Rendering.BufferView>) =
        o |> Map.map (fun k (perInstance, view) ->
            match view.ElementType with
                | TypeInfo.Patterns.MatrixOf(s, et) ->
                    let rowFormat =
                        match s.X with
                            | 2 -> VkFormat.R32g32Sfloat
                            | 3 -> VkFormat.R32g32b32Sfloat
                            | _ -> VkFormat.R32g32b32a32Sfloat

                    let rowSize = getFormatSize rowFormat
                    let totalSize = s.Y * rowSize
                    { 
                        inputFormat = rowFormat
                        stride = if view.IsSingleValue then 0 else totalSize
                        stepRate = if perInstance then VkVertexInputRate.Instance else VkVertexInputRate.Vertex
                        offsets = List.init s.Y (fun r -> view.Offset + r * rowSize)
                    }

                | _ -> 
                    let fmt = toVkFormat view.ElementType
                    { 
                        inputFormat = fmt
                        stride = if view.IsSingleValue then 0 else getFormatSize fmt
                        stepRate = if perInstance then VkVertexInputRate.Instance else VkVertexInputRate.Vertex
                        offsets = [view.Offset]
                    }
        )
 
    let ofTypes (o : Map<Symbol, bool * Type>) =
        o |> Map.map (fun k (perInstance, viewType) ->
            match viewType with
                | TypeInfo.Patterns.MatrixOf(s, et) ->
                    let rowFormat =
                        match s.X with
                            | 2 -> VkFormat.R32g32Sfloat
                            | 3 -> VkFormat.R32g32b32Sfloat
                            | _ -> VkFormat.R32g32b32a32Sfloat

                    let rowSize = getFormatSize rowFormat
                    let totalSize = s.Y * rowSize
                    { 
                        inputFormat = rowFormat
                        stride = totalSize
                        stepRate = if perInstance then VkVertexInputRate.Instance else VkVertexInputRate.Vertex
                        offsets = List.init s.Y (fun r -> r * rowSize)
                    }

                | _ -> 
                    let fmt = toVkFormat viewType
                    { 
                        inputFormat = fmt
                        stride = getFormatSize fmt
                        stepRate = if perInstance then VkVertexInputRate.Instance else VkVertexInputRate.Vertex
                        offsets = [0]
                    }
        )
 
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RasterizerState =
    let private toVkPolygonMode =
        LookupTable.lookupTable [
            FillMode.Fill, VkPolygonMode.Fill
            FillMode.Line, VkPolygonMode.Line
            FillMode.Point, VkPolygonMode.Point
        ]

    let private toVkCullMode =
        LookupTable.lookupTable [
            CullMode.Back, VkCullModeFlags.BackBit
            CullMode.Front, VkCullModeFlags.FrontBit
            CullMode.FrontAndBack, VkCullModeFlags.FrontAndBack
            CullMode.None, VkCullModeFlags.None
        ]

    let private toVkFrontFace =
        LookupTable.lookupTable [
            WindingOrder.Clockwise, VkFrontFace.Clockwise
            WindingOrder.CounterClockwise, VkFrontFace.CounterClockwise
        ]

   
    let create (conservativeRaster : bool) (depthClamp : bool)
               (bias : DepthBias) (cull : CullMode) (frontFace : WindingOrder) (fill : FillMode) =
        {
            depthClampEnable        = depthClamp
            rasterizerDiscardEnable = false
            polygonMode             = toVkPolygonMode fill
            cullMode                = toVkCullMode cull
            frontFace               = toVkFrontFace frontFace
            depthBiasEnable         = bias.Enabled
            depthBiasConstantFactor = bias.Constant
            depthBiasClamp          = bias.Clamp
            depthBiasSlopeFactor    = bias.SlopeScale
            lineWidth               = 1.0
            conservativeRaster      = conservativeRaster
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ColorBlendState =


    let private toVkBlendFactor =
        LookupTable.lookupTable [
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
            BlendFactor.SourceAlphaSaturate, VkBlendFactor.SrcAlphaSaturate

            BlendFactor.ConstantColor, VkBlendFactor.ConstantColor
            BlendFactor.InvConstantColor, VkBlendFactor.OneMinusConstantColor
            BlendFactor.ConstantAlpha, VkBlendFactor.ConstantAlpha
            BlendFactor.InvConstantAlpha, VkBlendFactor.OneMinusConstantAlpha
        ]

    let private toVkBlendOp =
        LookupTable.lookupTable [
            BlendOperation.Add, VkBlendOp.Add
            BlendOperation.Maximum, VkBlendOp.Max
            BlendOperation.Minimum, VkBlendOp.Min
            BlendOperation.ReverseSubtract, VkBlendOp.ReverseSubtract
            BlendOperation.Subtract, VkBlendOp.Subtract
        ]

    let private toVkColorComponentFlags (mask : ColorMask) =
        [
            if (mask &&& ColorMask.Red) <> ColorMask.None then VkColorComponentFlags.RBit
            if (mask &&& ColorMask.Green) <> ColorMask.None then VkColorComponentFlags.GBit
            if (mask &&& ColorMask.Blue) <> ColorMask.None then VkColorComponentFlags.BBit
            if (mask &&& ColorMask.Alpha) <> ColorMask.None then VkColorComponentFlags.ABit
        ]
        |> List.fold (|||) VkColorComponentFlags.None

    let private toAttachmentState (writeMask : ColorMask) (blend : BlendMode) =
        {
            enabled                 = blend.Enabled
            srcFactor               = toVkBlendFactor blend.SourceColorFactor
            dstFactor               = toVkBlendFactor blend.DestinationColorFactor
            operation               = toVkBlendOp blend.ColorOperation
            srcFactorAlpha          = toVkBlendFactor blend.SourceAlphaFactor
            dstFactorAlpha          = toVkBlendFactor blend.DestinationAlphaFactor
            operationAlpha          = toVkBlendOp blend.AlphaOperation
            colorWriteMask          = toVkColorComponentFlags writeMask
        }
    
    let create (writeMasks : ColorMask[]) (blendModes : BlendMode[]) (blendConstant : C4f) =
        {
            logicOpEnable           = false
            logicOp                 = VkLogicOp.NoOp
            attachmentStates        = Array.map2 toAttachmentState writeMasks blendModes
            constant                = V4f blendConstant
        }
 
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MultisampleState =
    let create (sampleShading : bool) (samples : int) =
        {
            samples                 = samples
            sampleShadingEnable     = samples > 1 && sampleShading
            minSampleShading        = 0.0
            sampleMask              = [||]
            alphaToCoverageEnable   = false
            alphaToOneEnable        = false
        }
 
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DepthState =
    let private toVkCompareOp =
        LookupTable.lookupTable [
            DepthTest.Greater, VkCompareOp.Greater
            DepthTest.GreaterOrEqual, VkCompareOp.GreaterOrEqual
            DepthTest.Less, VkCompareOp.Less
            DepthTest.LessOrEqual, VkCompareOp.LessOrEqual
            DepthTest.Equal, VkCompareOp.Equal
            DepthTest.NotEqual, VkCompareOp.NotEqual
            DepthTest.Never, VkCompareOp.Never
            DepthTest.Always, VkCompareOp.Always
            DepthTest.None, VkCompareOp.Always
        ]

    let create (write : bool) (test : DepthTest) =
        {
            testEnabled             = test <> DepthTest.None
            writeEnabled            = write
            boundsTest              = false
            compare                 = toVkCompareOp test
            depthBounds             = Range1d(0.0, 1.0)
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StencilState =
    let private toVkStencilOp =
        LookupTable.lookupTable [
            StencilOperation.Increment, VkStencilOp.IncrementAndClamp
            StencilOperation.IncrementWrap, VkStencilOp.IncrementAndWrap
            StencilOperation.Decrement, VkStencilOp.DecrementAndClamp
            StencilOperation.DecrementWrap, VkStencilOp.DecrementAndWrap
            StencilOperation.Keep, VkStencilOp.Keep
            StencilOperation.Replace, VkStencilOp.Replace
            StencilOperation.Zero, VkStencilOp.Zero
            StencilOperation.Invert, VkStencilOp.Invert
        ]

    let private toVkStencilCompareOp  =
        LookupTable.lookupTable [
            ComparisonFunction.Always, VkCompareOp.Always
            ComparisonFunction.Equal, VkCompareOp.Equal
            ComparisonFunction.Greater, VkCompareOp.Greater
            ComparisonFunction.GreaterOrEqual, VkCompareOp.GreaterOrEqual
            ComparisonFunction.Less, VkCompareOp.Less
            ComparisonFunction.LessOrEqual, VkCompareOp.LessOrEqual
            ComparisonFunction.Never, VkCompareOp.Never
            ComparisonFunction.NotEqual, VkCompareOp.NotEqual
        ]

    let private toVkStencilOpState (writeMask : StencilMask) (mode : StencilMode) = //()(state : Aardvark.Rendering.StencilState) =
        VkStencilOpState(
            toVkStencilOp mode.Fail,
            toVkStencilOp mode.Pass,
            toVkStencilOp mode.DepthFail,
            toVkStencilCompareOp mode.Comparison,
            uint32 mode.CompareMask,
            uint32 writeMask,
            uint32 mode.Reference
        )

    let create (writeMaskFront : StencilMask) (writeMaskBack : StencilMask)
               (modeFront : StencilMode) (modeBack : StencilMode) =
        {
            enabled = modeFront.Enabled || modeBack.Enabled
            front   = toVkStencilOpState writeMaskFront modeFront
            back    = toVkStencilOpState writeMaskBack modeBack
        }