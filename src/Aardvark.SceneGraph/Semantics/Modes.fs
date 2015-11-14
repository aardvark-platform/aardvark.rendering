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
        member x.StencilMode   : IMod<StencilMode> = x?StencilMode
        member x.BlendMode     : IMod<BlendMode> = x?BlendMode

    module Semantic =
        let depthTestMode (s : ISg) : IMod<DepthTestMode> = s?DepthTestMode
        let cullMode (s : ISg) : IMod<CullMode> = s?CullMode
        let fillMode (s : ISg) : IMod<FillMode> = s?FillMode
        let stencilMode (s : ISg) : IMod<DepthTestMode> = s?StencilMode
        let blendMode (s : ISg) : IMod<BlendMode> = s?BlendMode
        
    [<Semantic>]
    type ModeSem() =
        let defaultDepth = Mod.constant DepthTestMode.LessOrEqual
        let defaultCull = Mod.constant CullMode.None
        let defaultFill = Mod.constant FillMode.Fill
        let defaultStencil = Mod.constant StencilMode.Disabled
        let defaultBlend = Mod.constant BlendMode.None

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