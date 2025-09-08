namespace Aardvark.SceneGraph.Semantics

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Rendering

[<AutoOpen>]
module ModeSemantics =

    type Ag.Scope with
        // Blending
        member x.BlendMode                  : aval<BlendMode>               = x?BlendMode
        member x.BlendConstant              : aval<C4f>                     = x?BlendConstant
        member x.ColorWriteMask             : aval<ColorMask>               = x?ColorWriteMask
        member x.AttachmentBlendMode        : aval<Map<Symbol, BlendMode>>  = x?AttachmentBlendMode
        member x.AttachmentColorWriteMask   : aval<Map<Symbol, ColorMask>>  = x?AttachmentColorWriteMask

        // Depth
        member x.DepthTest                  : aval<DepthTest>               = x?DepthTest
        member x.DepthBias                  : aval<DepthBias>               = x?DepthBias
        member x.DepthWriteMask             : aval<bool>                    = x?DepthWriteMask
        member x.DepthClamp                 : aval<bool>                    = x?DepthClamp

        // Stencil
        member x.StencilModeFront           : aval<StencilMode>             = x?StencilModeFront
        member x.StencilWriteMaskFront      : aval<StencilMask>             = x?StencilWriteMaskFront
        member x.StencilModeBack            : aval<StencilMode>             = x?StencilModeBack
        member x.StencilWriteMaskBack       : aval<StencilMask>             = x?StencilWriteMaskBack

        // Rasterizer
        member x.CullMode                   : aval<CullMode>                = x?CullMode
        member x.FrontFacing                : aval<WindingOrder>            = x?FrontFacing
        member x.FillMode                   : aval<FillMode>                = x?FillMode
        member x.Multisample                : aval<bool>                    = x?Multisample
        member x.ConservativeRaster         : aval<bool>                    = x?ConservativeRaster

        // Viewport
        member x.Viewport                   : aval<Box2i> option            = x?Viewport
        member x.Scissor                    : aval<Box2i> option            = x?Scissor

    module Semantic =
        let blendMode                (s : Ag.Scope) = s.BlendMode
        let blendConstant            (s : Ag.Scope) = s.BlendConstant
        let colorWriteMask           (s : Ag.Scope) = s.ColorWriteMask
        let attachmentBlendMode      (s : Ag.Scope) = s.AttachmentBlendMode
        let attachmentColorWriteMask (s : Ag.Scope) = s.AttachmentColorWriteMask

        let depthTest                (s : Ag.Scope) = s.DepthTest
        let depthBias                (s : Ag.Scope) = s.DepthBias
        let depthWriteMask           (s : Ag.Scope) = s.DepthWriteMask
        let depthClamp               (s : Ag.Scope) = s.DepthClamp

        let stencilModeFront         (s : Ag.Scope) = s.StencilModeFront
        let stencilWriteMaskFront    (s : Ag.Scope) = s.StencilWriteMaskFront
        let stencilModeBack          (s : Ag.Scope) = s.StencilModeBack
        let stencilWriteMaskBack     (s : Ag.Scope) = s.StencilWriteMaskBack

        let cullMode                 (s : Ag.Scope) = s.CullMode
        let frontFacing              (s : Ag.Scope) = s.FrontFacing
        let fillMode                 (s : Ag.Scope) = s.FillMode
        let multisample              (s : Ag.Scope) = s.Multisample
        let convervativeRaster       (s : Ag.Scope) = s.ConservativeRaster


    [<Rule>]
    type ModeSem() =

        // Blending
        member x.BlendMode(r : Root<ISg>, scope : Ag.Scope) =  r.Child?BlendMode <- BlendState.Default.Mode
        member x.BlendMode(a : Sg.BlendModeApplicator, scope : Ag.Scope) =
            a.Child?BlendMode <- a.Mode

        member x.BlendConstant(r : Root<ISg>, scope : Ag.Scope) = r.Child?BlendConstant <- BlendState.Default.ConstantColor
        member x.BlendConstant(a : Sg.BlendConstantApplicator, scope : Ag.Scope) =
            a.Child?BlendConstant <- a.Color

        member x.ColorWriteMask(r : Root<ISg>, scope : Ag.Scope) = r.Child?ColorWriteMask <- BlendState.Default.ColorWriteMask
        member x.ColorWriteMask(a : Sg.ColorWriteMaskApplicator, scope : Ag.Scope) =
            a.Child?ColorWriteMask <- a.Mask

        member x.AttachmentBlendMode(r : Root<ISg>, scope : Ag.Scope) = r.Child?AttachmentBlendMode <- BlendState.Default.AttachmentMode
        member x.AttachmentBlendMode(a : Sg.AttachmentBlendModeApplicator, scope : Ag.Scope) =
            a.Child?AttachmentBlendMode <- a.Modes

        member x.AttachmentColorWriteMask(r : Root<ISg>, scope : Ag.Scope) = r.Child?AttachmentColorWriteMask <- BlendState.Default.AttachmentWriteMask
        member x.AttachmentColorWriteMask(a : Sg.AttachmentColorWriteMaskApplicator, scope : Ag.Scope) =
            a.Child?AttachmentColorWriteMask <- a.Masks

        // Depth
        member x.DepthTest(r : Root<ISg>, scope : Ag.Scope) = r.Child?DepthTest <- DepthState.Default.Test
        member x.DepthTest(a : Sg.DepthTestApplicator, scope : Ag.Scope) =
            a.Child?DepthTest <- a.Test

        member x.DepthBias(r : Root<ISg>, scope : Ag.Scope) = r.Child?DepthBias <- DepthState.Default.Bias
        member x.DepthBias(a : Sg.DepthBiasApplicator, scope : Ag.Scope) =
            a.Child?DepthBias <- a.State

        member x.DepthWriteMask(r : Root<ISg>, scope : Ag.Scope) = r.Child?DepthWriteMask <- DepthState.Default.WriteMask
        member x.DepthWriteMask(a : Sg.DepthWriteMaskApplicator, scope : Ag.Scope) =
            a.Child?DepthWriteMask <- a.WriteEnabled

        member x.DepthClamp(r : Root<ISg>, scope : Ag.Scope) = r.Child?DepthClamp <- DepthState.Default.Clamp
        member x.DepthClamp(a : Sg.DepthClampApplicator, scope : Ag.Scope) =
            a.Child?DepthClamp <- a.Clamp

        // Stencil
        member x.StencilModeFront(r : Root<ISg>, scope : Ag.Scope) = r.Child?StencilModeFront <- StencilState.Default.ModeFront
        member x.StencilModeFront(a : Sg.StencilModeFrontApplicator, scope : Ag.Scope) =
            a.Child?StencilModeFront <- a.Mode

        member x.StencilWriteMaskFront(r : Root<ISg>, scope : Ag.Scope) = r.Child?StencilWriteMaskFront <- StencilState.Default.WriteMaskFront
        member x.StencilWriteMaskFront(a : Sg.StencilWriteMaskFrontApplicator, scope : Ag.Scope) =
            a.Child?StencilWriteMaskFront <- a.Mask

        member x.StencilModeBack(r : Root<ISg>, scope : Ag.Scope) = r.Child?StencilModeBack <- StencilState.Default.ModeBack
        member x.StencilModeBack(a : Sg.StencilModeBackApplicator, scope : Ag.Scope) =
            a.Child?StencilModeBack <- a.Mode

        member x.StencilWriteMaskBack(r : Root<ISg>, scope : Ag.Scope) = r.Child?StencilWriteMaskBack <- StencilState.Default.WriteMaskBack
        member x.StencilWriteMaskBack(a : Sg.StencilWriteMaskBackApplicator, scope : Ag.Scope) =
            a.Child?StencilWriteMaskBack <- a.Mask

        // Rasterizer
        member x.CullMode(r : Root<ISg>, scope : Ag.Scope) = r.Child?CullMode <- RasterizerState.Default.CullMode
        member x.CullMode(a : Sg.CullModeApplicator, scope : Ag.Scope) =
            a.Child?CullMode <- a.Mode

        member x.FrontFacing(r : Root<ISg>, scope : Ag.Scope) = r.Child?FrontFacing <- RasterizerState.Default.FrontFacing
        member x.FrontFacing(a : Sg.FrontFacingApplicator, scope : Ag.Scope) =
            a.Child?FrontFacing <- a.WindingOrder

        member x.FillMode(r : Root<ISg>, scope : Ag.Scope) = r.Child?FillMode <- RasterizerState.Default.FillMode
        member x.FillMode(a : Sg.FillModeApplicator, scope : Ag.Scope) =
            a.Child?FillMode <- a.Mode

        member x.Multisample(r : Root<ISg>, scope : Ag.Scope) = r.Child?Multisample <- RasterizerState.Default.Multisample
        member x.Multisample(b : Sg.MultisampleApplicator, scope : Ag.Scope) =
            b.Child?Multisample <- b.Multisample

        member x.ConservativeRaster(r : Root<ISg>, scope : Ag.Scope) = r.Child?ConservativeRaster <- RasterizerState.Default.ConservativeRaster
        member x.ConservativeRaster(b : Sg.ConservativeRasterApplicator, scope : Ag.Scope) =
            b.Child?ConservativeRaster <- b.ConservativeRaster

        // Viewport
        member x.Viewport(r : Root<ISg>, scope : Ag.Scope) = r.Child?Viewport <- ViewportState.Default.Viewport
        member x.Viewport(a : Sg.ViewportApplicator, scope : Ag.Scope) =
            a.Child?Viewport <- a.Viewport

        member x.Scissor(r : Root<ISg>, scope : Ag.Scope) = r.Child?Scissor <- ViewportState.Default.Scissor
        member x.Scissor(a : Sg.ScissorApplicator, scope : Ag.Scope) =
            a.Child?Scissor <- a.Scissor