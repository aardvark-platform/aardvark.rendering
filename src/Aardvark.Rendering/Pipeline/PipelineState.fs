namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive
open Aardvark.Base

open Aardvark.Rendering

[<Struct; CLIMutable>]
type BlendState =
    {
        Mode                : aval<BlendMode>
        ColorWriteMask      : aval<ColorMask>
        ConstantColor       : aval<C4f>
        AttachmentMode      : aval<Map<Symbol, BlendMode>>
        AttachmentWriteMask : aval<Map<Symbol, ColorMask>>
    }

    static member Default =
        {
            Mode                = AVal.constant BlendMode.None
            ColorWriteMask      = AVal.constant ColorMask.All
            ConstantColor       = AVal.constant C4f.Black
            AttachmentMode      = AVal.constant Map.empty
            AttachmentWriteMask = AVal.constant Map.empty
        }

[<Struct; CLIMutable>]
type DepthState =
    {
        Test        : aval<DepthTest>
        Bias        : aval<DepthBias>
        WriteMask   : aval<bool>
        Clamp       : aval<bool>
    }

    static member Default =
        {
            Test        = AVal.constant DepthTest.LessOrEqual
            Bias        = AVal.constant DepthBias.None
            WriteMask   = AVal.constant true
            Clamp       = AVal.constant false
        }

[<Struct; CLIMutable>]
type StencilState =
    {
        ModeFront       : aval<StencilMode>
        WriteMaskFront  : aval<StencilMask>
        ModeBack        : aval<StencilMode>
        WriteMaskBack   : aval<StencilMask>
    }

    static member Default =
        {
            ModeFront      = AVal.constant StencilMode.None
            WriteMaskFront = AVal.constant StencilMask.All
            ModeBack       = AVal.constant StencilMode.None
            WriteMaskBack  = AVal.constant StencilMask.All
        }

[<Struct; CLIMutable>]
type RasterizerState =
    {
        CullMode            : aval<CullMode>
        FrontFacing         : aval<WindingOrder>
        FillMode            : aval<FillMode>
        Multisample         : aval<bool>
        ConservativeRaster  : aval<bool>
    }

    static member Default =
        {
            CullMode            = AVal.constant CullMode.None
            FrontFacing         = AVal.constant WindingOrder.CounterClockwise
            FillMode            = AVal.constant FillMode.Fill
            Multisample         = AVal.constant true
            ConservativeRaster  = AVal.constant false
        }

[<CLIMutable>]
type PipelineState =
    {
        Mode                : IndexedGeometryMode
        VertexInputTypes    : Map<Symbol, Type>

        DepthState          : DepthState
        BlendState          : BlendState
        StencilState        : StencilState
        RasterizerState     : RasterizerState

        GlobalUniforms      : IUniformProvider
        PerGeometryUniforms : Map<string, Type>
    }