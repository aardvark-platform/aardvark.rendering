namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive
open Aardvark.Base

open Aardvark.Rendering

[<Struct>]
type BlendState =
    {
        mutable Mode                : aval<BlendMode>
        mutable ColorWriteMask      : aval<ColorMask>
        mutable ConstantColor       : aval<C4f>
        mutable AttachmentMode      : aval<Map<Symbol, BlendMode>>
        mutable AttachmentWriteMask : aval<Map<Symbol, ColorMask>>
    }

    static member Default =
        {
            Mode                = AVal.constant BlendMode.None
            ColorWriteMask      = AVal.constant ColorMask.All
            ConstantColor       = AVal.constant C4f.Black
            AttachmentMode      = AVal.constant Map.empty
            AttachmentWriteMask = AVal.constant Map.empty
        }

[<Struct>]
type DepthState =
    {
        mutable Test        : aval<DepthTest>
        mutable Bias        : aval<DepthBias>
        mutable WriteMask   : aval<bool>
        mutable Clamp       : aval<bool>
    }

    static member Default =
        {
            Test        = AVal.constant DepthTest.LessOrEqual
            Bias        = AVal.constant DepthBias.None
            WriteMask   = AVal.constant true
            Clamp       = AVal.constant false
        }

[<Struct>]
type StencilState =
    {
        mutable ModeFront       : aval<StencilMode>
        mutable WriteMaskFront  : aval<StencilMask>
        mutable ModeBack        : aval<StencilMode>
        mutable WriteMaskBack   : aval<StencilMask>
    }

    static member Default =
        {
            ModeFront      = AVal.constant StencilMode.Default
            WriteMaskFront = AVal.constant StencilMask.All
            ModeBack       = AVal.constant StencilMode.Default
            WriteMaskBack  = AVal.constant StencilMask.All
        }

[<Struct>]
type RasterizerState =
    {
        mutable CullMode            : aval<CullMode>
        mutable FrontFace           : aval<WindingOrder>
        mutable FillMode            : aval<FillMode>
        mutable Multisample         : aval<bool>
        mutable ConservativeRaster  : aval<bool>
    }

    static member Default =
        {
            CullMode            = AVal.constant CullMode.None
            FrontFace           = AVal.constant WindingOrder.Clockwise
            FillMode            = AVal.constant FillMode.Fill
            Multisample         = AVal.constant true
            ConservativeRaster  = AVal.constant false
        }

type PipelineState =
    {
        mutable Mode                : IndexedGeometryMode
        mutable VertexInputTypes    : Map<Symbol, Type>

        mutable DepthState          : DepthState
        mutable BlendState          : BlendState
        mutable StencilState        : StencilState
        mutable RasterizerState     : RasterizerState

        mutable GlobalUniforms      : IUniformProvider
        mutable PerGeometryUniforms : Map<string, Type>
    }