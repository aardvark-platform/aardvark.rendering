namespace Aardvark.SceneGraph

open Aardvark.SceneGraph.Semantics

open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Base.Ag
open FSharp.Data.Adaptive

[<AbstractClass; Sealed; Extension>]
type SceneGraphRuntimeExtensions private() =

    static let toRenderObjects (runtime : IRuntime) (sg : ISg) =
        let app = Sg.DynamicNode(AVal.constant sg)
        app?Runtime <- runtime
        app.RenderObjects(Ag.Scope.Root)

    /// <summary>
    /// Compiles a render task for the given scene graph.
    /// </summary>
    /// <param name="this">The runtime.</param>
    /// <param name="signature">The framebuffer signature the task renders to.</param>
    /// <param name="sg">The scene graph to render.</param>
    /// <param name="transparency">How render objects marked transparent (see Sg.transparent) are rendered. Default is <see cref="Transparency.Auto"/>.</param>
    [<Extension>]
    static member CompileRender(this : IRuntime, signature : IFramebufferSignature, sg : ISg,
                                [<Optional; DefaultParameterValue(Transparency.Auto)>] transparency : Transparency) =
        let ro = sg |> toRenderObjects this
        this.CompileRender(signature, ro, transparency)

[<AutoOpen>]
module RuntimeSgExtensions =
    module Sg =

        /// Compiles a render task for the given scene graph, rendering objects marked
        /// transparent (see Sg.transparent) with the given transparency mode.
        let inline compile' (runtime : IRuntime) (signature : IFramebufferSignature) (transparency : Transparency) (sg : ISg) =
            runtime.CompileRender(signature, sg, transparency)

        /// Compiles a render task for the given scene graph. Objects marked transparent
        /// (see Sg.transparent) are rendered with Transparency.Auto.
        let inline compile (runtime : IRuntime) (signature : IFramebufferSignature) (sg : ISg) =
            runtime.CompileRender(signature, sg)
