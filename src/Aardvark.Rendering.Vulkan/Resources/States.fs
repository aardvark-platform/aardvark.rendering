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
module VertexInputState =

    let private toVkFormat =
        let lookup =
            LookupTable.lookupTable' [
                typeof<int8>, VkFormat.R8Sint
                typeof<uint8>, VkFormat.R8Uint

                typeof<int16>, VkFormat.R16Sint
                typeof<uint16>, VkFormat.R16Uint

                typeof<int32>, VkFormat.R32Sint
                typeof<uint32>, VkFormat.R32Uint

                typeof<V2i>, VkFormat.R32g32Sint
                typeof<V3i>, VkFormat.R32g32b32Sint
                typeof<V4i>, VkFormat.R32g32b32a32Sint

                typeof<V2ui>, VkFormat.R32g32Uint
                typeof<V3ui>, VkFormat.R32g32b32Uint
                typeof<V4ui>, VkFormat.R32g32b32a32Uint

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

        fun typ ->
            match lookup typ with
            | Some fmt -> fmt
            | _ -> failf "cannot determine appropriate vertex input format for type %A" typ


    [<RequireQualifiedAccess>]
    type AttributeDescription =
        internal {
            ElementType   : Type
            IsSingleValue : bool
            Offset        : int
        }

    module AttributeDescription =
        let create (elementType : Type) (isSingleValue : bool) (offset : int) =
            { AttributeDescription.ElementType   = elementType
              AttributeDescription.IsSingleValue = isSingleValue
              AttributeDescription.Offset        = offset }

    let ofDescriptions (descriptions : Map<Symbol, bool * AttributeDescription>) =
        descriptions |> Map.map (fun name (perInstance, desc) ->
            match desc.ElementType with
            | TypeInfo.Patterns.MatrixOf(s, TypeInfo.Patterns.Float32) ->
                let rowFormat =
                    match s.X with
                    | 2 -> VkFormat.R32g32Sfloat
                    | 3 -> VkFormat.R32g32b32Sfloat
                    | _ -> VkFormat.R32g32b32a32Sfloat

                let rowSize = VkFormat.pixelSizeInBytes rowFormat
                let totalSize = s.Y * rowSize
                {
                    inputFormat = rowFormat
                    stride = if desc.IsSingleValue then 0 else totalSize
                    stepRate = if perInstance then VkVertexInputRate.Instance else VkVertexInputRate.Vertex
                    offsets = List.init s.Y (fun r -> desc.Offset + r * rowSize)
                }

            | TypeInfo.Patterns.MatrixOf(_, et) ->
                failf "matrix types with element type %A not supported for vertex inputs" et

            | _ ->
                let fmt = toVkFormat desc.ElementType
                {
                    inputFormat = fmt
                    stride = if desc.IsSingleValue then 0 else VkFormat.pixelSizeInBytes fmt
                    stepRate = if perInstance then VkVertexInputRate.Instance else VkVertexInputRate.Vertex
                    offsets = [desc.Offset]
                }
        )

    let ofTypes (types : Map<Symbol, bool * Type>) =
        types|> Map.map (fun _ (perInstance, typ) ->
            perInstance, AttributeDescription.create typ false 0
        ) |> ofDescriptions

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