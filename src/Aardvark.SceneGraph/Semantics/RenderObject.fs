namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Base.Ag
open Aardvark.SceneGraph

module RenderObject =
    
    let ofScope (scope : Ag.Scope) =
        let rj = RenderObject.Create()
            
        let indexBufferView = scope?VertexIndexBuffer
        let isActive = scope?IsActive
        let renderPass = scope?RenderPass

        rj.AttributeScope <- scope 
        rj.Indices <- indexBufferView
         
        rj.IsActive <- isActive
        rj.RenderPass <- renderPass
            
        let attributes =
            { new IAttributeProvider with
                member x.TryGetAttribute(sem) =
                    match rj.VertexAttributes.TryGetAttribute sem with
                        | Some att -> Some att
                        | None -> rj.InstanceAttributes.TryGetAttribute sem

                member x.All = Seq.append rj.VertexAttributes.All rj.InstanceAttributes.All

                member x.Dispose() = ()
            }

        rj.Uniforms <- new Providers.UniformProvider(scope, scope?Uniforms, [attributes])


        let vertexAttributes = new Providers.AttributeProvider(scope, "VertexAttributes")
        let instanceAttributes =  new Providers.AttributeProvider(scope, "InstanceAttributes")

        rj.VertexAttributes <- vertexAttributes
        rj.InstanceAttributes <- instanceAttributes
            
        rj.WriteBuffers <- scope?WriteBuffers

        rj.DepthTest <- scope?DepthTestMode
        rj.DepthBias <- scope?DepthBias
        rj.CullMode <- scope?CullMode
        rj.FrontFace <- scope?FrontFace
        rj.FillMode <- scope?FillMode
        rj.StencilMode <- scope?StencilMode
        rj.BlendMode <- scope?BlendMode
        rj.Surface <- scope?Surface
        rj.ConservativeRaster <- scope?ConservativeRaster
        rj.Multisample <- scope?Multisample

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
            depthTest           = scope?DepthTestMode
            depthBias           = scope?DepthBias
            cullMode            = scope?CullMode
            frontFace           = scope?FrontFace
            blendMode           = scope?BlendMode
            fillMode            = scope?FillMode
            stencilMode         = scope?StencilMode
            multisample         = scope?Multisample
            writeBuffers        = scope?WriteBuffers
            globalUniforms      = new Providers.UniformProvider(scope, scope?Uniforms, [attributes])
                         
            geometryMode        = IndexedGeometryMode.PointList
            vertexInputTypes    = Map.empty
            perGeometryUniforms = Map.empty
        }

[<AutoOpen>]
module RenderObjectSemantics =

    type ISg with
        member x.RenderObjects(scope : Ag.Scope) : aset<IRenderObject> = x?RenderObjects(scope)
        member x.OverlayTasks(scope : Ag.Scope) : aset<RenderPass * IRenderTask> = x?OverlayTasks(scope)

    module Semantic =
        let renderObjects (scope : Ag.Scope) (s : ISg) : aset<IRenderObject> = s?RenderObjects(scope)
        let overlayTasks (scope : Ag.Scope) (s : ISg) : aset<RenderPass * IRenderTask> = s?OverlayTasks(scope)


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
                            [ DrawCallInfo(
                                FirstIndex = info.FirstIndex,
                                FirstInstance = info.FirstInstance,
                                InstanceCount = info.InstanceCount,
                                FaceVertexCount = count,
                                BaseVertex = 0
                            ) ]
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
                            DrawCallInfo(
                                FirstIndex = r.Min,
                                FaceVertexCount = 1 + r.Size,
                                FirstInstance = 0,
                                InstanceCount = 1,
                                BaseVertex = 0
                            )
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

        member x.RenderObjects(r : Sg.OverlayNode, scope : Ag.Scope) : aset<IRenderObject> =
            ASet.empty

    [<Rule>]
    type SubTaskSem() =
        member x.OverlayTasks(r : ISg, scope : Ag.Scope) : aset<RenderPass * IRenderTask> =
            ASet.empty

        member x.OverlayTasks(app : IApplicator, scope : Ag.Scope) =
            aset {
                let! c = app.Child
                yield! c.OverlayTasks(scope)
            }


        member x.OverlayTasks(g : IGroup, scope : Ag.Scope) =
            aset {
                for c in g.Children do
                    yield! c.OverlayTasks(scope)
            }

        member x.OverlayTasks(r : Sg.OverlayNode, scope : Ag.Scope) =
            ASet.single (scope.RenderPass, r.RenderTask)