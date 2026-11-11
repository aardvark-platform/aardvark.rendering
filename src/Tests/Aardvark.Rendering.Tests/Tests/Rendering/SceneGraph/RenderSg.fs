namespace Aardvark.Rendering.Tests.Rendering.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Rendering.Text
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FSharp.Data.Adaptive
open FShade
open Expecto

// The direct render path (IRenderSg / TraversalState) must produce the same render
// objects as the attribute grammar. The CPU cases build the same tree both ways and
// compare the render objects field by field; the GPU cases compare rendered images.
module RenderSg =

    module private Cases =

        let private positions = [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |]
        let private colors    = [| C4b.Red; C4b.Green; C4b.Blue; C4b.White |]
        let private indices   = [| 0; 1; 2; 2; 1; 3 |]

        let private sym (name : string) = Symbol.Create name

        /// Legacy applicator without an IRenderSg implementation: handled by the generic
        /// IApplicator rule of the attribute grammar only.
        type private LegacyApplicator(child : ISg) =
            inherit Sg.AbstractApplicator(AVal.constant child)

        /// A triangle-list leaf with positions and a LeafId uniform to pair render objects.
        let private leaf (id : int) =
            Sg.draw IndexedGeometryMode.TriangleList
            |> Sg.vertexArray DefaultSemantic.Positions positions
            |> Sg.uniform' "LeafId" id

        /// Render objects of the direct path: GetRenderObjects for an IRenderSg root, the
        /// bridge for a legacy root.
        let private direct (runtime : IRuntime) (sg : ISg) =
            let state = TraversalState.empty |> TraversalState.withRuntime runtime
            RenderSg.get sg state

        /// Render objects of the attribute grammar (the AARDVARK_LEGACY_SG entry point).
        let private legacy (runtime : IRuntime) (sg : ISg) =
            let app = Sg.DynamicNode(AVal.constant sg)
            app?Runtime <- runtime
            app.RenderObjects(Ag.Scope.Root)

        let private force (set : aset<IRenderObject>) =
            set.Content |> AVal.force |> HashSet.toList

        let private forceUntyped (value : IAdaptiveValue) =
            value.GetValueUntyped AdaptiveToken.Top

        let private uniform (name : string) (ro : RenderObject) =
            ro.Uniforms.TryGetUniform(Ag.Scope.Root, sym name)

        let private uniformValue (name : string) (ro : RenderObject) =
            match uniform name ro with
            | ValueSome v -> forceUntyped v
            | ValueNone   -> failtestf "uniform %s not found" name

        let private leafId (ro : IRenderObject) =
            match ro with
            | :? RenderObject as ro ->
                match uniform "LeafId" ro with
                | ValueSome v -> unbox<int> (forceUntyped v)
                | ValueNone   -> -1
            | _ -> -1

        let private drawCallInfos (ro : RenderObject) =
            match ro.DrawCalls with
            | DrawCalls.Direct calls -> AVal.force calls
            | DrawCalls.Indirect _   -> failtest "expected direct draw calls"

        let private singleFaceVertexCount (ro : RenderObject) =
            let calls = drawCallInfos ro
            Expect.equal calls.Length 1 "expected a single draw call"
            calls.[0].FaceVertexCount

        /// Uniforms that both paths resolve the same way: user uniforms, trafos, derived
        /// trafo combinations, camera, viewport size and Has-queries.
        let private comparedUniforms =
            [
                "LeafId"; "Foo"; "Bar"; "Missing"
                "ModelTrafo"; "ViewTrafo"; "ProjTrafo"; "ModelViewTrafo"; "ModelViewProjTrafo"; "NormalMatrix"
                "CameraLocation"; "LightLocation"; "ViewportSize"
                "HasFoo"; "HasPositions"; "HasColors"; "HasInstanceTrafo"; "HasMissing"
            ]

        let private comparedAttributes =
            [ DefaultSemantic.Positions; DefaultSemantic.Colors; sym "InstanceTrafo"; sym "Missing" ]

        let private expectUniform (name : string) (direct : RenderObject) (legacy : RenderObject) =
            // The attribute grammar raises for a synthesized uniform whose inputs are missing
            // (e.g. LightLocation without a view trafo); that counts as absent.
            let resolve (ro : RenderObject) =
                try
                    match uniform name ro with
                    | ValueSome v -> ValueSome v
                    | ValueNone   -> Uniforms.tryGetDerivedUniform name ro.Uniforms
                with _ ->
                    ValueNone

            match resolve direct, resolve legacy with
            | ValueSome a, ValueSome b ->
                Expect.equal (forceUntyped a) (forceUntyped b) $"uniform {name}"
                Expect.equal a.IsConstant b.IsConstant $"uniform {name} (IsConstant)"
            | ValueNone, ValueNone -> ()
            | a, b -> failtestf "uniform %s: direct %b, legacy %b" name a.IsSome b.IsSome

        let private expectAttribute (name : Symbol) (direct : RenderObject) (legacy : RenderObject) =
            Expect.equal (direct.VertexAttributes.TryGetAttribute name) (legacy.VertexAttributes.TryGetAttribute name) $"vertex attribute {name}"
            Expect.equal (direct.InstanceAttributes.TryGetAttribute name) (legacy.InstanceAttributes.TryGetAttribute name) $"instance attribute {name}"

        /// Compares two render objects by value: every adaptive member is forced.
        let private expectEquivalent (direct : RenderObject) (legacy : RenderObject) =
            let fv (v : aval<'T>) = AVal.force v

            Expect.equal (fv direct.IsActive) (fv legacy.IsActive) "IsActive"
            Expect.equal direct.IsActive.IsConstant legacy.IsActive.IsConstant "IsActive (IsConstant)"
            Expect.equal direct.RenderPass legacy.RenderPass "RenderPass"
            Expect.equal direct.IsTransparent legacy.IsTransparent "IsTransparent"
            Expect.equal direct.Mode legacy.Mode "Mode"
            Expect.equal direct.Indices legacy.Indices "Indices"
            Expect.isTrue (obj.ReferenceEquals(direct.Surface, legacy.Surface)) "Surface"

            match direct.DrawCalls, legacy.DrawCalls with
            | DrawCalls.Direct a, DrawCalls.Direct b     -> Expect.equal (fv a) (fv b) "DrawCalls"
            | DrawCalls.Indirect a, DrawCalls.Indirect b -> Expect.isTrue (obj.ReferenceEquals(a, b)) "DrawCalls (indirect)"
            | _ -> failtest "DrawCalls kind differs"

            let a, b = direct.BlendState, legacy.BlendState
            Expect.equal (fv a.Mode) (fv b.Mode) "BlendState.Mode"
            Expect.equal (fv a.ColorWriteMask) (fv b.ColorWriteMask) "BlendState.ColorWriteMask"
            Expect.equal (fv a.ConstantColor) (fv b.ConstantColor) "BlendState.ConstantColor"
            Expect.equal (fv a.AttachmentMode) (fv b.AttachmentMode) "BlendState.AttachmentMode"
            Expect.equal (fv a.AttachmentWriteMask) (fv b.AttachmentWriteMask) "BlendState.AttachmentWriteMask"

            let a, b = direct.DepthState, legacy.DepthState
            Expect.equal (fv a.Test) (fv b.Test) "DepthState.Test"
            Expect.equal (fv a.Bias) (fv b.Bias) "DepthState.Bias"
            Expect.equal (fv a.WriteMask) (fv b.WriteMask) "DepthState.WriteMask"
            Expect.equal (fv a.Clamp) (fv b.Clamp) "DepthState.Clamp"

            let a, b = direct.StencilState, legacy.StencilState
            Expect.equal (fv a.ModeFront) (fv b.ModeFront) "StencilState.ModeFront"
            Expect.equal (fv a.WriteMaskFront) (fv b.WriteMaskFront) "StencilState.WriteMaskFront"
            Expect.equal (fv a.ModeBack) (fv b.ModeBack) "StencilState.ModeBack"
            Expect.equal (fv a.WriteMaskBack) (fv b.WriteMaskBack) "StencilState.WriteMaskBack"

            let a, b = direct.RasterizerState, legacy.RasterizerState
            Expect.equal (fv a.CullMode) (fv b.CullMode) "RasterizerState.CullMode"
            Expect.equal (fv a.FrontFacing) (fv b.FrontFacing) "RasterizerState.FrontFacing"
            Expect.equal (fv a.FillMode) (fv b.FillMode) "RasterizerState.FillMode"
            Expect.equal (fv a.Multisample) (fv b.Multisample) "RasterizerState.Multisample"
            Expect.equal (fv a.ConservativeRaster) (fv b.ConservativeRaster) "RasterizerState.ConservativeRaster"

            let a, b = direct.ViewportState, legacy.ViewportState
            Expect.equal (a.Viewport |> Option.map fv) (b.Viewport |> Option.map fv) "ViewportState.Viewport"
            Expect.equal (a.Scissor |> Option.map fv) (b.Scissor |> Option.map fv) "ViewportState.Scissor"

            for name in comparedAttributes do expectAttribute name direct legacy
            for name in comparedUniforms do expectUniform name direct legacy

        /// Builds the tree both ways and compares the render objects pairwise (by LeafId).
        let private expectSameRenderObjects (context : string) (runtime : IRuntime) (sg : ISg) =
            let sort (objects : list<IRenderObject>) =
                objects |> List.sortBy (fun ro -> leafId ro, ro.GetType().Name)

            let d = direct runtime sg |> force |> sort
            let l = legacy runtime sg |> force |> sort
            Expect.equal d.Length l.Length $"{context}: number of render objects"

            for (a, b) in List.zip d l do
                Expect.equal (a.GetType()) (b.GetType()) $"{context}: render object type"
                Expect.equal a.RenderPass b.RenderPass $"{context}: render pass"

                match a, b with
                | (:? RenderObject as a), (:? RenderObject as b) ->
                    try expectEquivalent a b
                    with e -> failtestf "%s: %s" context e.Message
                | _ -> ()

            d, l

        let private singleRenderObject (set : aset<IRenderObject>) =
            match force set with
            | [ :? RenderObject as ro ] -> ro
            | objects -> failtestf "expected a single RenderObject, got %A" objects

        /// Every applicator of Core/Sg.fs with a non-default value.
        let private applicators : list<string * (ISg -> ISg)> =
            let stencil = { StencilMode.None with Pass = StencilOperation.Replace; Reference = 3 }
            [
                "trafo",                 Sg.trafo' (Trafo3d.Translation(1.0, 2.0, 3.0))
                "viewTrafo",             Sg.viewTrafo' (Trafo3d.Translation(-4.0, 5.0, -6.0))
                "projTrafo",             Sg.projTrafo' (Trafo3d.Scale 2.0)
                "uniform",               Sg.uniform' "Foo" 42
                "uniforms",              Sg.uniforms (UniformProvider.ofMap (Map.ofList [sym "Bar", AVal.constant 7 :> IAdaptiveValue]))
                "vertexAttribute",       Sg.vertexArray DefaultSemantic.Colors colors
                "instanceAttribute",     Sg.instanceArray (sym "InstanceTrafo") [| M44f.Identity; M44f.Identity |]
                "index",                 Sg.indexArray indices
                "surface",               Sg.shader { do! DefaultSurfaces.trafo }
                "onOff",                 Sg.onOff (AVal.constant true)
                "pass",                  Sg.pass (RenderPass.after "late" RenderPassOrder.Arbitrary RenderPass.main)
                "transparent",           Sg.transparent
                "blendMode",             Sg.blendMode' BlendMode.Blend
                "blendConstant",         Sg.blendConstant' C4f.Red
                "colorMask",             Sg.colorMask' ColorMask.Red
                "blendModes",            Sg.blendModes' (Map.ofList [DefaultSemantic.Colors, BlendMode.Add])
                "colorMasks",            Sg.colorMasks' (Map.ofList [DefaultSemantic.Colors, ColorMask.Green])
                "depthTest",             Sg.depthTest' DepthTest.Greater
                "depthBias",             Sg.depthBias' { DepthBias.None with Constant = 1.0; SlopeScale = 2.0 }
                "depthWrite",            Sg.depthWrite' false
                "depthClamp",            Sg.depthClamp' true
                "stencilModeFront",      Sg.stencilModeFront' stencil
                "stencilWriteMaskFront", Sg.stencilWriteMaskFront' (StencilMask 0xF0u)
                "stencilModeBack",       Sg.stencilModeBack' stencil
                "stencilWriteMaskBack",  Sg.stencilWriteMaskBack' (StencilMask 0x0Fu)
                "cullMode",              Sg.cullMode' CullMode.Front
                "frontFacing",           Sg.frontFacing' WindingOrder.Clockwise
                "fillMode",              Sg.fillMode' FillMode.Line
                "multisample",           Sg.multisample' false
                "conservativeRaster",    Sg.conservativeRaster' true
                "viewport",              Sg.viewport' (Box2i(V2i(1, 2), V2i(30, 40)))
                "scissor",               Sg.scissor' (Box2i(V2i(3, 4), V2i(50, 60)))
                "onActivation",          Sg.onActivation (fun () -> Disposable.empty)
                "pickable",              Sg.pickable (PickShape.Box Box3d.Unit)
                "requirePicking",        Sg.requirePicking
                "dynamic",               fun sg -> Sg.dynamic (AVal.constant sg)
                "set",                   fun sg -> Sg.ofList [sg]
            ]

        // Equivalence

        let equivalencePerApplicator (runtime : IRuntime) =
            for (name, apply) in applicators do
                leaf 1 |> apply |> expectSameRenderObjects name runtime |> ignore

        let equivalenceFullTree (runtime : IRuntime) =
            let model = cval (Trafo3d.RotationZ 0.5)
            let inner = leaf 1 |> Sg.trafo (model :> aval<_>)
            let tree  = Sg.ofList [ inner; leaf 2; leaf 3 |> Sg.onOff (AVal.constant false) ]
            let sg    = applicators |> List.fold (fun sg (_, apply) -> apply sg) tree

            let d, l = expectSameRenderObjects "full tree" runtime sg
            Expect.equal d.Length 3 "number of render objects"

            transact (fun () -> model.Value <- Trafo3d.Translation(7.0, 8.0, 9.0))
            expectSameRenderObjects "full tree (after change)" runtime sg |> ignore
            Expect.equal l.Length 3 "number of render objects (legacy)"

        // Face vertex count

        let faceVertexCount (runtime : IRuntime) =
            let check (context : string) (expected : int) (sg : ISg) =
                let d = direct runtime sg |> singleRenderObject
                let l = legacy runtime sg |> singleRenderObject
                Expect.equal (singleFaceVertexCount d) expected $"{context}: direct"
                Expect.equal (singleFaceVertexCount l) expected $"{context}: legacy"

            let draw = Sg.draw IndexedGeometryMode.TriangleList

            draw
            |> Sg.vertexArray DefaultSemantic.Positions positions
            |> check "positions only" positions.Length

            draw
            |> Sg.vertexArray DefaultSemantic.Positions positions
            |> Sg.vertexArray DefaultSemantic.Colors colors
            |> check "other attribute does not change the count" positions.Length

            draw
            |> Sg.vertexArray DefaultSemantic.Positions positions
            |> Sg.indexArray indices
            |> check "index buffer applied after positions" indices.Length

            draw
            |> Sg.indexArray indices
            |> Sg.vertexArray DefaultSemantic.Positions positions
            |> check "positions applied after index buffer" indices.Length

            Sg.render IndexedGeometryMode.TriangleList (DrawCallInfo(FaceVertexCount = 2, InstanceCount = 1))
            |> Sg.vertexArray DefaultSemantic.Positions positions
            |> Sg.indexArray indices
            |> check "explicit count is kept" 2

        // Flags

        let isActiveFolding (runtime : IRuntime) =
            let check (context : string) (expectedConstant : bool) (expected : bool) (sg : ISg) =
                let d = direct runtime sg |> singleRenderObject
                let l = legacy runtime sg |> singleRenderObject
                Expect.equal (AVal.force d.IsActive) expected $"{context}: direct value"
                Expect.equal (AVal.force l.IsActive) expected $"{context}: legacy value"
                Expect.equal d.IsActive.IsConstant expectedConstant $"{context}: direct IsConstant"
                Expect.equal l.IsActive.IsConstant expectedConstant $"{context}: legacy IsConstant"
                d

            let outer = cval true
            let inner = cval true

            leaf 1 |> Sg.onOff (AVal.constant true) |> Sg.onOff (AVal.constant true) |> check "constant and constant" true true |> ignore
            leaf 1 |> Sg.onOff (AVal.constant true) |> Sg.onOff (AVal.constant false) |> check "constant false wins" true false |> ignore
            leaf 1 |> Sg.onOff (inner :> aval<_>) |> Sg.onOff (AVal.constant true) |> check "adaptive and constant true" false true |> ignore
            leaf 1 |> Sg.onOff (inner :> aval<_>) |> Sg.onOff (AVal.constant false) |> check "adaptive and constant false" true false |> ignore

            let ro = leaf 1 |> Sg.onOff (inner :> aval<_>) |> Sg.onOff (outer :> aval<_>) |> check "adaptive and adaptive" false true
            transact (fun () -> inner.Value <- false)
            Expect.isFalse (AVal.force ro.IsActive) "inner toggled off"
            transact (fun () -> inner.Value <- true; outer.Value <- false)
            Expect.isFalse (AVal.force ro.IsActive) "outer toggled off"
            transact (fun () -> outer.Value <- true)
            Expect.isTrue (AVal.force ro.IsActive) "both on again"

        // Trafos

        let modelTrafoStack (runtime : IRuntime) =
            let a = Trafo3d.Translation(1.0, 2.0, 3.0)
            let b = cval (Trafo3d.RotationZ 0.5)
            let c = Trafo3d.Scale 2.0

            // Innermost applicator first, like RenderObject.AttributeScope.ModelTrafo.
            let sg = leaf 1 |> Sg.trafo' a |> Sg.trafo (b :> aval<_>) |> Sg.trafo' c
            let d = direct runtime sg |> singleRenderObject
            let l = legacy runtime sg |> singleRenderObject

            let model (ro : RenderObject) = uniformValue "ModelTrafo" ro |> unbox<Trafo3d>
            let expected = a * b.Value * c
            Expect.approxEquals (model d).Forward expected.Forward 1E-8 "direct ModelTrafo"
            Expect.approxEquals (model l).Forward expected.Forward 1E-8 "legacy ModelTrafo"

            transact (fun () -> b.Value <- Trafo3d.RotationX 1.0)
            let expected = a * b.Value * c
            Expect.approxEquals (model d).Forward expected.Forward 1E-8 "direct ModelTrafo after change"
            Expect.approxEquals (model l).Forward expected.Forward 1E-8 "legacy ModelTrafo after change"

            // The unfolded stack is a direct-path uniform: root to leaf order, same instances.
            let stack = uniformValue "ModelTrafoStack" d |> unbox<aval<Trafo3d>[]>
            Expect.equal stack.Length 3 "stack length"
            Expect.isTrue (obj.ReferenceEquals(stack.[1], b)) "adaptive trafo kept by reference"
            Expect.equal (AVal.force stack.[0]) a "innermost trafo first"
            Expect.equal (AVal.force stack.[2]) c "outermost trafo last"

            // Runs of constants fold into one constant.
            let constant = leaf 1 |> Sg.trafo' a |> Sg.trafo' c
            let d = direct runtime constant |> singleRenderObject
            let l = legacy runtime constant |> singleRenderObject
            let modelAval (ro : RenderObject) = (uniform "ModelTrafo" ro).Value
            Expect.isTrue (modelAval d).IsConstant "direct constant chain folds"
            Expect.isTrue (modelAval l).IsConstant "legacy constant chain folds"
            Expect.equal (model d) (model l) "folded value"

        let viewTrafoDerivesCameraLocation (runtime : IRuntime) =
            let view = CameraView.lookAt (V3d(10.0, 20.0, 30.0)) V3d.Zero V3d.OOI |> CameraView.viewTrafo
            let sg = leaf 1 |> Sg.viewTrafo' view
            let d = direct runtime sg |> singleRenderObject
            let l = legacy runtime sg |> singleRenderObject

            for name in [ "CameraLocation"; "LightLocation" ] do
                let location (ro : RenderObject) = uniformValue name ro |> unbox<V3d>
                Expect.approxEquals (location d) (V3d(10.0, 20.0, 30.0)) 1E-6 $"direct {name}"
                Expect.approxEquals (location d) (location l) 1E-6 $"legacy {name}"

            // Without a view trafo neither exists, like in the attribute grammar (which has
            // no root seeder for CameraLocation).
            let d = direct runtime (leaf 1) |> singleRenderObject
            let l = legacy runtime (leaf 1) |> singleRenderObject
            Expect.isTrue (uniform "CameraLocation" d).IsNone "direct: no CameraLocation without a view trafo"
            Expect.isTrue (uniform "LightLocation" d).IsNone "direct: no LightLocation without a view trafo"
            Expect.isTrue (uniform "CameraLocation" l).IsNone "legacy: no CameraLocation without a view trafo"

        // Uniforms

        let uniformOrdering (runtime : IRuntime) =
            let sg = leaf 1 |> Sg.uniform' "Foo" 1 |> Sg.uniform' "Foo" 2
            let d = direct runtime sg |> singleRenderObject
            let l = legacy runtime sg |> singleRenderObject
            Expect.equal (uniformValue "Foo" d) (box 1) "direct: innermost uniform wins"
            Expect.equal (uniformValue "Foo" l) (box 1) "legacy: innermost uniform wins"

            let has (name : string) (ro : RenderObject) = uniformValue name ro |> unbox<bool>
            let sg = leaf 1 |> Sg.instanceArray (sym "InstanceTrafo") [| M44f.Identity |]
            let d = direct runtime sg |> singleRenderObject
            let l = legacy runtime sg |> singleRenderObject
            for ro, path in [ d, "direct"; l, "legacy" ] do
                Expect.isTrue  (has "HasLeafId" ro) $"{path}: HasLeafId"
                Expect.isTrue  (has "HasPositions" ro) $"{path}: HasPositions"
                Expect.isTrue  (has "HasInstanceTrafo" ro) $"{path}: HasInstanceTrafo"
                Expect.isFalse (has "HasMissing" ro) $"{path}: HasMissing"
                Expect.isTrue  (uniform "Missing" ro).IsNone $"{path}: Missing"

        let viewportUniforms (runtime : IRuntime) =
            let region = Box2i(V2i(1, 2), V2i(31, 42))
            let sg = leaf 1 |> Sg.viewport' region
            let d = direct runtime sg |> singleRenderObject
            let l = legacy runtime sg |> singleRenderObject

            Expect.equal (uniformValue "ViewportSize" d) (box region.Size) "direct ViewportSize"
            Expect.equal (uniformValue "ViewportSize" l) (box region.Size) "legacy ViewportSize"
            Expect.equal (uniformValue "RcpViewportSize" d) (box (1.0 / V2d region.Size)) "direct RcpViewportSize"
            Expect.equal (d.ViewportState.Viewport |> Option.map AVal.force) (Some region) "direct viewport"
            Expect.equal (d.ViewportState.Scissor) None "direct scissor untouched"

            let sg = leaf 1 |> Sg.scissor' region
            let d = direct runtime sg |> singleRenderObject
            Expect.isTrue (uniform "ViewportSize" d).IsNone "no ViewportSize without a viewport"
            Expect.isTrue (uniform "RcpViewportSize" d).IsNone "no RcpViewportSize without a viewport"
            Expect.equal (d.ViewportState.Scissor |> Option.map AVal.force) (Some region) "direct scissor"
            Expect.equal (d.ViewportState.Viewport) None "direct viewport untouched"

        // Activation

        let activationOrder (runtime : IRuntime) =
            let events = System.Collections.Generic.List<string>()
            let activation (name : string) () =
                events.Add $"activate {name}"
                { new IDisposable with member _.Dispose() = events.Add $"dispose {name}" }

            let sg = leaf 1 |> Sg.onActivation (activation "inner") |> Sg.onActivation (activation "outer")

            for path, objects in [ "direct", direct runtime sg; "legacy", legacy runtime sg ] do
                events.Clear()
                let ro = singleRenderObject objects
                let handle = ro.Activate()
                Expect.sequenceEqual events [ "activate inner"; "activate outer" ] $"{path}: activation order"
                handle.Dispose()
                Expect.sequenceEqual events [ "activate inner"; "activate outer"; "dispose inner"; "dispose outer" ] $"{path}: disposal order"

        // Bridges

        let legacyLeafInDirectTree (runtime : IRuntime) =
            let trafo = Trafo3d.Translation(1.0, 2.0, 3.0)
            let seen = System.Collections.Generic.List<Trafo3d * BlendMode * int * IRuntime>()

            let sg =
                Sg.delay (fun scope ->
                    seen.Add((AVal.force scope.ModelTrafo, AVal.force scope.BlendMode, scope.Uniforms.Length, scope.Runtime))
                    leaf 1
                )
                |> Sg.uniform' "Foo" 1
                |> Sg.blendMode' BlendMode.Blend
                |> Sg.trafo' trafo

            let d, _ = expectSameRenderObjects "delayed leaf" runtime sg
            Expect.equal d.Length 1 "number of render objects"

            Expect.equal seen.Count 2 "the generator runs once per path"
            for (model, blend, uniforms, seenRuntime) in seen do
                Expect.equal model trafo "the scope carries the model trafo"
                Expect.equal blend BlendMode.Blend "the scope carries the blend mode"
                Expect.equal uniforms 1 "the scope carries the uniform providers"
                Expect.isTrue (obj.ReferenceEquals(seenRuntime, runtime)) "the scope carries the runtime"

            // Without a runtime the attribute stays unset instead of being null.
            let mutable runtimeAttribute = ValueSome Unchecked.defaultof<IRuntime>
            let sg = Sg.delay (fun scope -> runtimeAttribute <- scope.TryGetAttributeValueV<IRuntime>("Runtime"); leaf 1)
            (sg :?> IRenderSg).GetRenderObjects TraversalState.empty |> force |> ignore
            Expect.isTrue runtimeAttribute.IsNone "no runtime attribute without a runtime"

        let legacyApplicatorInDirectTree (runtime : IRuntime) =
            let sg =
                LegacyApplicator(leaf 1 |> Sg.uniform' "Foo" 1 |> Sg.blendMode' BlendMode.Blend) :> ISg
                |> Sg.trafo' (Trafo3d.Translation(1.0, 2.0, 3.0))
                |> Sg.depthTest' DepthTest.Greater

            let d, _ = expectSameRenderObjects "legacy applicator" runtime sg
            Expect.equal d.Length 1 "number of render objects"

            let sg = LegacyApplicator(leaf 1 |> Sg.blendMode' BlendMode.Blend) :> ISg
            let d, _ = expectSameRenderObjects "legacy applicator as root" runtime sg
            Expect.equal d.Length 1 "number of render objects (root)"

        let bridgedLeaves (runtime : IRuntime) =
            // The view trafo provides the CameraLocation the LOD node reads.
            let wrap (sg : ISg) =
                sg
                |> Sg.uniform' "Foo" 3
                |> Sg.cullMode' CullMode.Front
                |> Sg.trafo' (Trafo3d.Scale 3.0)
                |> Sg.viewTrafo' (Trafo3d.Translation(0.0, 0.0, -5.0))

            let lod = NaiveLod.Sg.loD (leaf 1) (leaf 2) (fun _ -> true)
            let d, _ = expectSameRenderObjects "naive lod" runtime (wrap lod)
            Expect.equal (d |> List.map leafId) [2] "lod picks the high node"

            let command = Sg.execute (RenderCommand.Render (leaf 1))
            let d, _ = expectSameRenderObjects "runtime command" runtime (wrap command)
            Expect.equal d.Length 1 "one command render object"

            let air =
                Sg.air {
                    do! Air.BindVertexBuffer(DefaultSemantic.Positions, AVal.constant positions)
                    do! Air.Draw(positions.Length)
                }
            let d, _ = expectSameRenderObjects "air" runtime (wrap air)
            Expect.equal d.Length 1 "one air render object"

        // Dynamic children

        let dynamicChildren (runtime : IRuntime) =
            let child = cval (leaf 1)
            let members = cset [ leaf 2; leaf 3 ]
            let sg = Sg.ofList [ Sg.dynamic (child :> aval<_>); Sg.set (members :> aset<_>) ] |> Sg.trafo' (Trafo3d.Scale 2.0)

            let ids (objects : aset<IRenderObject>) = objects |> force |> List.map leafId |> List.sort

            let d = direct runtime sg
            let l = legacy runtime sg
            Expect.equal (ids d) [1; 2; 3] "initial (direct)"
            Expect.equal (ids l) [1; 2; 3] "initial (legacy)"

            transact (fun () ->
                child.Value <- leaf 4
                members.Remove(members.Value |> Seq.head) |> ignore
                members.Add(leaf 5) |> ignore)

            Expect.equal (ids d) (ids l) "after change: both paths agree"
            Expect.equal (ids d |> List.length) 3 "after change: count"
            Expect.contains (ids d) 4 "after change: new dynamic child"
            Expect.contains (ids d) 5 "after change: new set member"

            expectSameRenderObjects "dynamic children" runtime sg |> ignore

        // GPU

        let private size = V2i(128)

        let private renderWith (runtime : IRuntime) (sg : ISg) =
            use signature = runtime.CreateFramebufferSignature [ DefaultSemantic.Colors, TextureFormat.Rgba8 ]
            use task = sg |> Sg.compile runtime signature
            let output = task |> RenderTask.renderToColor (AVal.constant size)
            output.Acquire()
            try output.GetValue().Download().AsPixImage<uint8>().Data |> Array.copy
            finally output.Release()

        let private expectSameImage (direct : byte[]) (legacy : byte[]) =
            Expect.equal direct.Length legacy.Length "image size"
            let mutable count = 0
            let mutable maxDelta = 0
            let mutable first = -1

            for i in 0 .. direct.Length - 1 do
                let delta = abs (int direct.[i] - int legacy.[i])
                if delta > 0 then
                    count <- count + 1
                    maxDelta <- max maxDelta delta
                    if first < 0 then first <- i

            if count > 0 then
                let pixel = first / 4
                failtestf "direct and legacy images differ in %d of %d channels (max delta %d), first at pixel (%d, %d)"
                    count direct.Length maxDelta (pixel % size.X) (pixel / size.X)

        /// Renders with the direct path and with the attribute-grammar entry point.
        let private renderBothWays (runtime : IRuntime) (sg : unit -> ISg) =
            let direct = renderWith runtime (sg ())

            EnvVar.AARDVARK_LEGACY_SG <- "1"
            let legacy =
                try renderWith runtime (sg ())
                finally EnvVar.AARDVARK_LEGACY_SG <- null

            Expect.isTrue (direct |> Array.exists (fun v -> v <> 0uy)) "direct image is empty"
            Expect.isTrue (legacy |> Array.exists (fun v -> v <> 0uy)) "legacy image is empty"
            expectSameImage direct legacy

        let private coloredQuad (color : C4f) (sg : ISg) =
            sg |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor color
            }

        let renderedImage (runtime : IRuntime) =
            let scene () =
                Sg.ofList [
                    Sg.fullScreenQuad
                    |> Sg.scale 0.4
                    |> Sg.translation' (V3d(-0.5, -0.5, 0.0))
                    |> coloredQuad C4f.Red

                    Sg.draw IndexedGeometryMode.TriangleList
                    |> Sg.vertexArray DefaultSemantic.Positions positions
                    |> Sg.indexArray indices
                    |> Sg.scale 0.3
                    |> Sg.translation' (V3d(0.5, -0.5, 0.0))
                    |> coloredQuad C4f.Green

                    // Blended, so it must not overlap another quad: the draw order is not fixed.
                    Sg.fullScreenQuad
                    |> Sg.scale 0.3
                    |> Sg.translation' (V3d(0.4, 0.5, 0.0))
                    |> coloredQuad (C4f(0.0f, 0.0f, 1.0f, 0.5f))
                    |> Sg.blendMode' BlendMode.Blend

                    Sg.delay (fun scope ->
                        Sg.fullScreenQuad
                        |> Sg.scale 0.2
                        |> coloredQuad (if scope.IsActive.IsConstant then C4f.White else C4f.Gray)
                    )
                    |> Sg.translation' (V3d(-0.6, 0.6, 0.0))

                    Sg.fullScreenQuad
                    |> Sg.scale 0.3
                    |> Sg.translation' (V3d(0.6, 0.6, 0.0))
                    |> coloredQuad C4f.Yellow
                    |> Sg.onOff (AVal.constant false)
                ]
                |> Sg.viewTrafo' (CameraView.lookAt (V3d(0.0, 0.0, 3.0)) V3d.Zero V3d.OIO |> CameraView.viewTrafo)
                |> Sg.projTrafo' (Frustum.perspective 60.0 0.1 10.0 1.0 |> Frustum.projTrafo)

            renderBothWays runtime scene

        let renderedPoolAndGeometrySet (runtime : IRuntime) =
            let geometry =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = (indices :> Array),
                    IndexedAttributes = SymDict.ofList [ DefaultSemantic.Positions, positions :> Array ]
                )

            let signature =
                { IndexType              = typeof<int>
                  VertexAttributeTypes   = Map.ofList [ DefaultSemantic.Positions, typeof<V3f> ]
                  InstanceAttributeTypes = Map.empty }

            use pool = runtime.CreateManagedPool signature
            use call = pool.Add(AdaptiveGeometry.ofIndexedGeometry [] geometry)

            let scene () =
                Sg.ofList [
                    Sg.pool pool IndexedGeometryMode.TriangleList (ASet.single call)
                    |> Sg.scale 0.4
                    |> Sg.translation' (V3d(-0.5, 0.0, 0.0))
                    |> coloredQuad C4f.Red

                    Sg.geometrySet IndexedGeometryMode.TriangleList signature.VertexAttributeTypes (ASet.single geometry)
                    |> Sg.scale 0.4
                    |> Sg.translation' (V3d(0.5, 0.0, 0.0))
                    |> coloredQuad C4f.Green
                ]

            renderBothWays runtime scene

        let renderedText (runtime : IRuntime) =
            let scene () =
                Sg.text DefaultFonts.Hack.Regular C4b.White (AVal.constant "RenderSg")
                |> Sg.scale 0.3
                |> Sg.translation' (V3d(-0.8, 0.0, 0.0))

            renderBothWays runtime scene

    let tests (target : TestTarget) =
        [
            if target = TestTarget.Cpu then
                "Equivalence per applicator",          Cases.equivalencePerApplicator
                "Equivalence of a full tree",          Cases.equivalenceFullTree
                "Face vertex count",                   Cases.faceVertexCount
                "IsActive folding",                    Cases.isActiveFolding
                "Model trafo stack",                   Cases.modelTrafoStack
                "View trafo derives camera location",  Cases.viewTrafoDerivesCameraLocation
                "Uniform ordering and Has queries",    Cases.uniformOrdering
                "Viewport uniforms",                   Cases.viewportUniforms
                "Activation order",                    Cases.activationOrder
                "Legacy leaf in a direct tree",        Cases.legacyLeafInDirectTree
                "Legacy applicator in a direct tree",  Cases.legacyApplicatorInDirectTree
                "Bridged leaves",                      Cases.bridgedLeaves
                "Dynamic children",                    Cases.dynamicChildren

            elif target.IsGpu then
                "Rendered image",                      Cases.renderedImage
                "Rendered pool and geometry set",      Cases.renderedPoolAndGeometrySet
                "Rendered text",                       Cases.renderedText
        ]
        |> prepareCases "SceneGraph.RenderSg" target
