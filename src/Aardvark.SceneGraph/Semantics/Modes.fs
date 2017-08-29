namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module ModeSemantics =

    type ISg with
        member x.DepthTestMode : IMod<DepthTestMode> = x?DepthTestMode
        member x.CullMode      : IMod<CullMode>      = x?CullMode
        member x.FillMode      : IMod<FillMode>      = x?FillMode
        member x.StencilMode   : IMod<StencilMode>   = x?StencilMode
        member x.BlendMode     : IMod<BlendMode>     = x?BlendMode

        member x.WriteBuffers   : Option<Set<Symbol>>       = x?WriteBuffers
        member x.ColorWriteMask : IMod<bool*bool*bool*bool> = x?ColorWriteMask
        member x.DepthWriteMask : IMod<bool>                = x?ColorWriteMask
        member x.ConservativeRaster : IMod<bool>            = x?ConservativeRaster
        member x.Multisample : IMod<bool>                   = x?Multisample

    module Semantic =
        let depthTestMode  (s : ISg) = s.DepthTestMode
        let cullMode       (s : ISg) = s.CullMode
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
        let defaultDepth   = Mod.constant DepthTestMode.LessOrEqual
        let defaultCull    = Mod.constant CullMode.None
        let defaultFill    = Mod.constant FillMode.Fill
        let defaultStencil = Mod.constant StencilMode.Disabled
        let defaultBlend   = Mod.constant BlendMode.None
        let defaultWriteBuffers   = Option<Set<Symbol>>.None
        let defaultColorWriteMask = Mod.constant (true,true,true,true)
        let defaultDepthWriteMask = Mod.constant true
        let defaultConservativeRaster = Mod.constant false
        let defaultMultisample = Mod.constant true

        member x.DepthTestMode(e : Root<ISg>) =
            e.Child?DepthTestMode <- defaultDepth

        member x.CullMode(e : Root<ISg>) =
            e.Child?CullMode <- defaultCull

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

        member x.CullMode(a : Sg.CullModeApplicator) =
            a.Child?CullMode <- a.Mode

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

