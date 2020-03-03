namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module ModeSemantics =

    type Ag.Scope with
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
        let depthTestMode  (s : Ag.Scope) = s.DepthTestMode
        let depthBias      (s : Ag.Scope) = s.DepthBias
        let cullMode       (s : Ag.Scope) = s.CullMode
        let frontFace      (s : Ag.Scope) = s.FrontFace
        let fillMode       (s : Ag.Scope) = s.FillMode
        let stencilMode    (s : Ag.Scope) = s.StencilMode
        let blendMode      (s : Ag.Scope) = s.BlendMode
        let writeBuffers   (s : Ag.Scope) = s.WriteBuffers
        let colorWriteMask (s : Ag.Scope) = s.ColorWriteMask
        let depthWriteMask (s : Ag.Scope) = s.DepthWriteMask
        let conservativeRaster (s : Ag.Scope) = s.ConservativeRaster
        let multisample (s : Ag.Scope) = s.Multisample
        
    [<Rule>]
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

        member x.DepthTestMode(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?DepthTestMode <- defaultDepth

        member x.DepthBias(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?DepthBias <- defaultDepthBias

        member x.CullMode(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?CullMode <- defaultCull

        member x.FrontFace(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?FrontFace <- defaultFrontFace

        member x.FillMode(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?FillMode <- defaultFill

        member x.StencilMode(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?StencilMode <- defaultStencil

        member x.BlendMode(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?BlendMode <- defaultBlend

        member x.ConservativeRaster(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?ConservativeRaster <- defaultConservativeRaster
            
        member x.ConservativeRaster(b : Sg.ConservativeRasterApplicator, scope : Ag.Scope) =
            b.Child?ConservativeRaster <- b.ConservativeRaster

        member x.Multisample(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?Multisample <- defaultMultisample
            
        member x.Multisample(b : Sg.MultisampleApplicator, scope : Ag.Scope) =
            b.Child?Multisample <- b.Multisample

        member x.WriteBuffers(e : Root<ISg>, scope : Ag.Scope) = e.Child?WriteBuffers <- defaultWriteBuffers
        member x.WriteBuffers(b : Sg.WriteBuffersApplicator, scope : Ag.Scope) =
            b.Child?WriteBuffers <- b.WriteBuffers

        member x.ColorWriteMask(e : Root<ISg>, scope : Ag.Scope) = e.Child?ColorWriteMask <- defaultColorWriteMask
        member x.ColorWriteMask(b : Sg.ColorWriteMaskApplicator, scope : Ag.Scope) =
            b.Child?ColorWriteMask <- b.MaskRgba

        member x.DepthWriteMask(e : Root<ISg>, scope : Ag.Scope) = e.Child?DepthWriteMask <- defaultDepthWriteMask
        member x.DepthWriteMask(b : Sg.DepthWriteMaskApplicator, scope : Ag.Scope) =
            b.Child?DepthWriteMask <- b.WriteEnabled

        member x.DepthTestMode(a : Sg.DepthTestModeApplicator, scope : Ag.Scope) =
            a.Child?DepthTestMode <- a.Mode
        
        member x.DepthBias(a : Sg.DepthBiasApplicator, scope : Ag.Scope) =
            a.Child?DepthBias <- a.State

        member x.CullMode(a : Sg.CullModeApplicator, scope : Ag.Scope) =
            a.Child?CullMode <- a.Mode

        member x.FrontFace(a : Sg.FrontFaceApplicator, scope : Ag.Scope) =
            a.Child?FrontFace <- a.WindingOrder

        member x.FillMode(a : Sg.FillModeApplicator, scope : Ag.Scope) =
            a.Child?FillMode <- a.Mode

        member x.StencilMode(a : Sg.StencilModeApplicator, scope : Ag.Scope) =
            a.Child?StencilMode <- a.Mode

        member x.BlendMode(a : Sg.BlendModeApplicator, scope : Ag.Scope) =
            a.Child?BlendMode <- a.Mode

        member x.DepthTestMode(a : Sg.RasterizerStateApplicator, scope : Ag.Scope) =
            a.Child?DepthTestMode <- a.DepthTestMode

        member x.CullMode(a : Sg.RasterizerStateApplicator, scope : Ag.Scope) =
            a.Child?CullMode <- a.CullMode

        member x.FillMode(a : Sg.RasterizerStateApplicator, scope : Ag.Scope) =
            a.Child?FillMode <- a.FillMode

        member x.StencilMode(a : Sg.RasterizerStateApplicator, scope : Ag.Scope) =
            a.Child?StencilMode <- a.StencilMode

        member x.BlendMode(a : Sg.RasterizerStateApplicator, scope : Ag.Scope) =
            a.Child?BlendMode <- a.BlendMode


