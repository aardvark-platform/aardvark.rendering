namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph

module BlendState =
    let ofScope (scope : Ag.Scope) =
        {
            Mode                = scope.BlendMode
            ColorWriteMask      = scope.ColorWriteMask
            ConstantColor       = scope.BlendConstant
            AttachmentMode      = scope.AttachmentBlendMode
            AttachmentWriteMask = scope.AttachmentColorWriteMask
        }

module DepthState =
    let ofScope (scope : Ag.Scope) =
        {
            Test       = scope.DepthTest
            Bias       = scope.DepthBias
            WriteMask  = scope.DepthWriteMask
            Clamp      = scope.DepthClamp
        }

module StencilState =
    let ofScope (scope : Ag.Scope) =
        {
            ModeFront      = scope.StencilModeFront
            WriteMaskFront = scope.StencilWriteMaskFront
            ModeBack       = scope.StencilModeBack
            WriteMaskBack  = scope.StencilWriteMaskBack
        }

module RasterizerState =
    let ofScope (scope : Ag.Scope) =
        {
            CullMode           = scope.CullMode
            FrontFace          = scope.FrontFace
            FillMode           = scope.FillMode
            Multisample        = scope.Multisample
            ConservativeRaster = scope.ConservativeRaster
        }

module RenderObject =

    let ofScope (scope : Ag.Scope) =
        let rj = RenderObject.Create()

        rj.AttributeScope <- scope
        rj.Indices <- scope.VertexIndexBuffer

        rj.IsActive <- scope.IsActive
        rj.RenderPass <- scope.RenderPass

        let attributes =
            { new IAttributeProvider with
                member x.TryGetAttribute(sem) =
                    match rj.VertexAttributes.TryGetAttribute sem with
                        | Some att -> Some att
                        | None -> rj.InstanceAttributes.TryGetAttribute sem

                member x.All = Seq.append rj.VertexAttributes.All rj.InstanceAttributes.All

                member x.Dispose() = ()
            }

        rj.Uniforms <- new Providers.UniformProvider(scope, scope.Uniforms, [attributes])

        let vertexAttributes = new Providers.AttributeProvider(scope, "VertexAttributes")
        let instanceAttributes =  new Providers.AttributeProvider(scope, "InstanceAttributes")

        rj.VertexAttributes <- vertexAttributes
        rj.InstanceAttributes <- instanceAttributes

        rj.Surface <- scope.Surface

        rj.BlendState <- BlendState.ofScope scope
        rj.DepthState <- DepthState.ofScope scope
        rj.StencilState <- StencilState.ofScope scope
        rj.RasterizerState <- RasterizerState.ofScope scope

        rj


module PipelineState =
    let ofScope (scope : Ag.Scope) =
        let vertexAttributes = new Providers.AttributeProvider(scope, "VertexAttributes") :> IAttributeProvider
        let instanceAttributes =  new Providers.AttributeProvider(scope, "InstanceAttributes") :> IAttributeProvider

        let attributes =
            { new IAttributeProvider with
                member x.TryGetAttribute(sem) =
                    match vertexAttributes.TryGetAttribute sem with
                        | Some att -> Some att
                        | None -> instanceAttributes.TryGetAttribute sem

                member x.All = Seq.append vertexAttributes.All instanceAttributes.All

                member x.Dispose() = ()
            }

        {
            Mode                = IndexedGeometryMode.PointList
            VertexInputTypes    = Map.empty

            BlendState          = BlendState.ofScope scope
            DepthState          = DepthState.ofScope scope
            StencilState        = StencilState.ofScope scope
            RasterizerState     = RasterizerState.ofScope scope

            GlobalUniforms      = new Providers.UniformProvider(scope, scope.Uniforms, [attributes])
            PerGeometryUniforms = Map.empty
        }

[<AutoOpen>]
module RenderObjectSemantics =

    type ISg with
        member x.RenderObjects(scope : Ag.Scope) : aset<IRenderObject> = x?RenderObjects(scope)

    module Semantic =
        let renderObjects (scope : Ag.Scope) (s : ISg) : aset<IRenderObject> = s?RenderObjects(scope)


    [<Rule>]
    type RenderObjectSem() =
        member x.RenderObjects(a : IApplicator, scope : Ag.Scope) : aset<IRenderObject> =
            aset {
                let! c = a.Child
                yield! c.RenderObjects(scope)
            }

        member x.RenderObjects(g : IGroup, scope : Ag.Scope) : aset<IRenderObject> =
            aset {
                for c in g.Children do
                    yield! c.RenderObjects(scope)
            }

        member x.RenderObjects(r : Sg.IndirectRenderNode, scope : Ag.Scope) : aset<IRenderObject> =
            let rj = RenderObject.ofScope scope
            rj.DrawCalls <- Indirect r.Indirect
            rj.Mode <- r.Mode

            ASet.single (rj :> IRenderObject)

        member x.RenderObjects(o : Sg.RenderObjectNode, scope : Ag.Scope) =
            o.Objects

        member x.RenderObjects(r : Sg.RenderNode, scope : Ag.Scope) : aset<IRenderObject> =
            let rj = RenderObject.ofScope scope

            let callInfos =
                adaptive {
                    let! info = r.DrawCallInfo
                    if info.FaceVertexCount < 0 then
                        let! (count : int) = scope?FaceVertexCount
                        return
                            [ {
                                FirstIndex = info.FirstIndex
                                FirstInstance = info.FirstInstance
                                InstanceCount = info.InstanceCount
                                FaceVertexCount = count
                                BaseVertex = 0
                            } ]
                    else
                        return [info]
                }

            rj.DrawCalls <- Direct callInfos
            rj.Mode <- r.Mode

            ASet.single (rj :> IRenderObject)

        member x.RenderObjects(r : Sg.GeometrySet, scope : Ag.Scope) : aset<IRenderObject> =
            let rj = RenderObject.ofScope scope

            let packer = new GeometrySetUtilities.GeometryPacker(r.AttributeTypes)
            let vertexAttributes =
                { new IAttributeProvider with
                    member x.TryGetAttribute(sem) =
                        match Map.tryFind sem r.AttributeTypes with
                            | Some t ->
                                let b = packer.GetBuffer sem
                                BufferView(b, t) |> Some
                            | None -> None

                    member x.All = Seq.empty

                    member x.Dispose() = ()

                }

            let indirect =
                packer |> AVal.map (fun set ->
                    set |> Seq.toArray
                        |> Array.map (fun r ->
                            {
                                FirstIndex = r.Min
                                FaceVertexCount = 1 + r.Size
                                FirstInstance = 0
                                InstanceCount = 1
                                BaseVertex = 0
                            }
                        )


                )

            let activate() =
                let deltas = ConcurrentDeltaQueue.ofASet r.Geometries

                let runner =
                    async {
                        do! Async.SwitchToNewThread()
                        while true do
                            let operation = deltas.Dequeue()

                            match operation with
                                | Add(_,g) -> packer.Add g |> ignore
                                | Rem(_,g) -> packer.Remove g |> ignore

                            ()

                    }


                let cancel = new System.Threading.CancellationTokenSource()
                let task = Async.StartAsTask(runner, cancellationToken = cancel.Token)

                { new System.IDisposable with
                    member x.Dispose() =
                        (deltas :> System.IDisposable).Dispose()
                        cancel.Cancel()
                        cancel.Dispose()
                        packer.Dispose()
                        ()
                }

            rj.VertexAttributes <- vertexAttributes
            rj.DrawCalls <- Indirect (indirect |> AVal.map (IndirectBuffer.ofArray false))
            rj.Mode <- r.Mode
            rj.Activate <- activate

            ASet.single (rj :> IRenderObject)