namespace Aardvark.SceneGraph.CSharp

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.SceneGraph
open Aardvark.Base.Incremental

[<Extension>]
[<AbstractClass>]
[<Sealed>]
type CSharpRuntimeExtensions =
    [<Extension>]
    static member CompileRender (x : IRuntime, e : Sg.Environment) = x.CompileRender e

[<Extension>]
[<AbstractClass>]
[<Sealed>]
type GeometryExtensions =
    [<Extension>]
    static member ToSg(ig : IndexedGeometry) = ig |> Sg.ofIndexedGeometry

[<Extension>]
[<AbstractClass>]
[<Sealed>]
type SceneGraphExtensions =


    [<Extension>]
    static member CameraView(sg : ISg, view : ICameraView) = Sg.ViewTrafoApplicator(view.ViewTrafos, sg) :> ISg


    [<Extension>]
    static member Surface(sg : ISg, surface : ISurface) = Sg.SurfaceApplicator(Mod.initConstant surface, sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, surface : IEvent<ISurface>) = Sg.SurfaceApplicator(surface, sg) :> ISg

    [<Extension>]
    static member Surface(sg : ISg, surface : IMod<ISurface>) = Sg.SurfaceApplicator(surface, sg) :> ISg

    [<Extension>]
    static member FillMode(sg : ISg, mode : IEvent<FillMode>) = Sg.FillModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member FillMode(sg : ISg, mode : FillMode) = Sg.FillModeApplicator(Mod.initConstant mode, sg) :> ISg

    [<Extension>]
    static member FillMode(sg : ISg, mode : IMod<FillMode>) = Sg.FillModeApplicator(mode, sg) :> ISg


    [<Extension>]
    static member CullMode(sg : ISg, mode : IEvent<CullMode>) = Sg.CullModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member CullMode(sg : ISg, mode : CullMode) = Sg.CullModeApplicator(Mod.initConstant mode, sg) :> ISg

    [<Extension>]
    static member CullMode(sg : ISg, mode : IMod<CullMode>) = Sg.CullModeApplicator(mode, sg) :> ISg


    [<Extension>]
    static member BlendMode(sg : ISg, mode : IEvent<BlendMode>) = Sg.BlendModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member BlendMode(sg : ISg, mode : BlendMode) = Sg.BlendModeApplicator(Mod.initConstant mode, sg) :> ISg

    [<Extension>]
    static member BlendMode(sg : ISg, mode : IMod<BlendMode>) = Sg.BlendModeApplicator(mode, sg) :> ISg


    [<Extension>]
    static member StencilMode(sg : ISg, mode : IEvent<StencilMode>) = Sg.StencilModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member StencilMode(sg : ISg, mode : StencilMode) = Sg.StencilModeApplicator(Mod.initConstant mode, sg) :> ISg

    [<Extension>]
    static member StencilMode(sg : ISg, mode : IMod<StencilMode>) = Sg.StencilModeApplicator(mode, sg) :> ISg


    [<Extension>]
    static member DepthTestMode(sg : ISg, mode : IEvent<DepthTestMode>) = Sg.DepthTestModeApplicator(mode, sg) :> ISg

    [<Extension>]
    static member DepthTestMode(sg : ISg, mode : DepthTestMode) = Sg.DepthTestModeApplicator(Mod.initConstant mode, sg) :> ISg

    [<Extension>]
    static member DepthTestMode(sg : ISg, mode : IMod<DepthTestMode>) = Sg.DepthTestModeApplicator(mode, sg) :> ISg


[<Extension>]
[<AbstractClass>]
[<Sealed>]
type SceneGraphTools =

    [<Extension>]
    static member NormalizeToAdaptive (this : ISg, box : Box3d) = Sg.normalizeToAdaptive box this

    [<Extension>]
    static member NormalizeAdaptive (this : ISg)  = Sg.normalizeAdaptive this 
     