namespace Aardvark.Rendering.Vulkan

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
module internal VertexInputDescription =

    let create (perInstance : bool) (singleValue : bool) (offset : int) (stride : int) (rows : int) (format : VkFormat) =
        let rowSize = VkFormat.pixelSizeInBytes format
        let totalSize = rows * rowSize

        let stride =
            if singleValue then 0
            elif stride = 0 then totalSize
            else stride

        { inputFormat = format
          stride      = stride
          stepRate    = if perInstance then VkVertexInputRate.Instance else VkVertexInputRate.Vertex
          offsets     = List.init rows (fun r -> offset + r * rowSize) }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VertexInputState =
    open TypeInfo

    // Note: The expected type is not enough to determine the appropriate format.
    // E.g. if V3f is expected, we might need a float32 format if the input is also float32
    // but we might also need an normalized format if the input is a color like C3b.
    // Here we just have to assume that input type = expected type.
    // Similarly, we have to determine the stride here which must be set to 0 for
    // single value buffers. Here, we just assume that the input a regular buffer.
    let ofTypes (types : Map<Symbol, bool * Type>) =
        types |> Map.map (fun name (perInstance, typ) ->
            let format =
                VkFormat.tryGetAttributeFormat typ typ true
                |> Option.defaultWith (fun _ ->
                    failf "cannot determine appropriate format for attribute '%A' (expected type = %A)" name typ
                )

            let rows =
                match typ with
                | MatrixOf(s, _) -> s.Y
                | _ -> 1

            VertexInputDescription.create perInstance false 0 0 rows format
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

    // Vulkan NDC space is right-handed while Aardvark generally assumes OpenGL's left-handed NDC space
    // So we have to flip the winding order here (also in Vulkan we render upside down)
    let private toVkFrontFace =
        LookupTable.lookupTable [
            WindingOrder.Clockwise, VkFrontFace.CounterClockwise
            WindingOrder.CounterClockwise, VkFrontFace.Clockwise
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