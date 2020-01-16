namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module ModeSemantics =

    type ISg with
        member x.DepthTestMode : aval<DepthTestMode> = x?DepthTestMode
        member x.DepthBias     : aval<DepthBiasState> = x?DepthBias
        member x.CullMode      : aval<CullMode>      = x?CullMode
        member x.FrontFace     : aval<WindingOrder>  = x?FrontFace
        member x.FillMode      : aval<FillMode>      = x?FillMode
        member x.StencilMode   : aval<StencilMode>   = x?StencilMode
        member x.BlendMode     : aval<BlendMode>     = x?BlendMode

        member x.WriteBuffers   : Option<Set<Symbol>>       = x?WriteBuffers
        member x.ColorWriteMask : aval<bool*bool*bool*bool> = x?ColorWriteMask
        member x.DepthWriteMask : aval<bool>                = x?ColorWriteMask
        member x.ConservativeRaster : aval<bool>            = x?ConservativeRaster
        member x.Multisample : aval<bool>                   = x?Multisample

    module Semantic =
        let depthTestMode  (s : ISg) = s.DepthTestMode
        let depthBias      (s : ISg) = s.DepthBias
        let cullMode       (s : ISg) = s.CullMode
        let frontFace      (s : ISg) = s.FrontFace
        let fillMode       (s : ISg) = s.FillMode
        let stencilMode    (s : ISg) = s.StencilMode
        let blendMode      (s : ISg) = s.BlendMode
        let writeBuffers   (s : ISg) = s.WriteBuffers
        let colorWriteMask (s : ISg) = s.ColorWriteMask
        let depthWriteMask (s : ISg) = s.DepthWriteMask
        let conservativeRaster (s : ISg) = s.ConservativeRaster
        let multisample (s : ISg) = s.Multisample
        
    [<Semantic>]
    type ModeSem() =
        let defaultDepth   = AVal.constant DepthTestMode.LessOrEqual
        let defaultCull    = AVal.constant CullMode.None
        let defaultFill    = AVal.constant FillMode.Fill
        let defaultStencil = AVal.constant StencilMode.Disabled
        let defaultBlend   = AVal.constant BlendMode.None
        let defaultWriteBuffers   = Option<Set<Symbol>>.None
        let defaultColorWriteMask = AVal.constant (true,true,true,true)
        let defaultDepthWriteMask = AVal.constant true
        let defaultConservativeRaster = AVal.constant false
        let defaultMultisample = AVal.constant true
        let defaultDepthBias = AVal.constant (DepthBiasState(0.0, 0.0, 0.0))
        let defaultFrontFace = AVal.constant WindingOrder.Clockwise

        member x.DepthTestMode(e : Root<ISg>) =
            e.Child?DepthTestMode <- defaultDepth

        member x.DepthBias(e : Root<ISg>) =
            e.Child?DepthBias <- defaultDepthBias

        member x.CullMode(e : Root<ISg>) =
            e.Child?CullMode <- defaultCull

        member x.FrontFace(e : Root<ISg>) =
            e.Child?FrontFace <- defaultFrontFace

        member x.FillMode(e : Root<ISg>) =
            e.Child?FillMode <- defaultFill

        member x.StencilMode(e : Root<ISg>) =
            e.Child?StencilMode <- defaultStencil

        member x.BlendMode(e : Root<ISg>) =
            e.Child?BlendMode <- defaultBlend

        member x.ConservativeRaster(e : Root<ISg>) =
            e.Child?ConservativeRaster <- defaultConservativeRaster
            
        member x.ConservativeRaster(b : Sg.ConservativeRasterApplicator) =
            b.Child?ConservativeRaster <- b.ConservativeRaster

        member x.Multisample(e : Root<ISg>) =
            e.Child?Multisample <- defaultMultisample
            
        member x.Multisample(b : Sg.MultisampleApplicator) =
            b.Child?Multisample <- b.Multisample

        member x.WriteBuffers(e : Root<ISg>) = e.Child?WriteBuffers <- defaultWriteBuffers
        member x.WriteBuffers(b : Sg.WriteBuffersApplicator) =
            b.Child?WriteBuffers <- b.WriteBuffers

        member x.ColorWriteMask(e : Root<ISg>) = e.Child?ColorWriteMask <- defaultColorWriteMask
        member x.ColorWriteMask(b : Sg.ColorWriteMaskApplicator) =
            b.Child?ColorWriteMask <- b.MaskRgba

        member x.DepthWriteMask(e : Root<ISg>) = e.Child?DepthWriteMask <- defaultDepthWriteMask
        member x.DepthWriteMask(b : Sg.DepthWriteMaskApplicator) =
            b.Child?DepthWriteMask <- b.WriteEnabled

        member x.DepthTestMode(a : Sg.DepthTestModeApplicator) =
            a.Child?DepthTestMode <- a.Mode
        
        member x.DepthBias(a : Sg.DepthBiasApplicator) =
            a.Child?DepthBias <- a.State

        member x.CullMode(a : Sg.CullModeApplicator) =
            a.Child?CullMode <- a.Mode

        member x.FrontFace(a : Sg.FrontFaceApplicator) =
            a.Child?FrontFace <- a.WindingOrder

        member x.FillMode(a : Sg.FillModeApplicator) =
            a.Child?FillMode <- a.Mode

        member x.StencilMode(a : Sg.StencilModeApplicator) =
            a.Child?StencilMode <- a.Mode

        member x.BlendMode(a : Sg.BlendModeApplicator) =
            a.Child?BlendMode <- a.Mode

        member x.DepthTestMode(a : Sg.RasterizerStateApplicator) =
            a.Child?DepthTestMode <- a.DepthTestMode

        member x.CullMode(a : Sg.RasterizerStateApplicator) =
            a.Child?CullMode <- a.CullMode

        member x.FillMode(a : Sg.RasterizerStateApplicator) =
            a.Child?FillMode <- a.FillMode

        member x.StencilMode(a : Sg.RasterizerStateApplicator) =
            a.Child?StencilMode <- a.StencilMode

        member x.BlendMode(a : Sg.RasterizerStateApplicator) =
            a.Child?BlendMode <- a.BlendMode


