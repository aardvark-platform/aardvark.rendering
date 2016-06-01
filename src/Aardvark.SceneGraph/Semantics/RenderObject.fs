namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering


module RenderObject =
    
    let ofScope (scope : Ag.Scope) =
        let rj = RenderObject.Create()
            
        let indexArray = scope?VertexIndexArray
        let isActive = scope?IsActive
        let renderPass = scope?RenderPass

        rj.AttributeScope <- scope 
        rj.Indices <- if indexArray = AttributeSemantics.emptyIndex then null else indexArray 
         
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
            
        rj.DepthTest <- scope?DepthTestMode
        rj.CullMode <- scope?CullMode
        rj.FillMode <- scope?FillMode
        rj.StencilMode <- scope?StencilMode
        rj.BlendMode <- scope?BlendMode
        rj.Surface <- scope?Surface
            
        rj

    let inline create() =
        Ag.getContext() |> ofScope




[<AutoOpen>]
module RenderObjectSemantics =

    type ISg with
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()
        member x.OverlayTasks() : aset<RenderPass * IRenderTask> = x?OverlayTasks()

    module Semantic =
        [<System.Obsolete("renderJobs is deprecated, please use renderObjects instead.")>]        
        let renderJobs (s : ISg) : aset<IRenderObject> = s?RenderObjects()
        let renderObjects (s : ISg) : aset<IRenderObject> = s?RenderObjects()
        let overlayTasks (s : ISg) : aset<RenderPass * IRenderTask> = s?OverlayTasks()


    [<Semantic>]
    type RenderObjectSem() =
        member x.RenderObjects(a : IApplicator) : aset<IRenderObject> =
            aset {
                let! c = a.Child
                yield! c.RenderObjects()
            }

        member x.RenderObjects(g : IGroup) : aset<IRenderObject> =
            aset {
                for c in g.Children do
                    yield! c.RenderObjects()
            }

        member x.RenderObjects(r : Sg.RenderNode) : aset<IRenderObject> =
            let scope = Ag.getContext()
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

            rj.DrawCallInfos <- callInfos
            rj.Mode <- r.Mode

            ASet.single (rj :> IRenderObject)

        member x.RenderObjects(r : Sg.GeometrySet) : aset<IRenderObject> =
            let scope = Ag.getContext()
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
                packer |> Mod.map (fun set ->
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
                            let! operation = deltas.DequeueAsync()

                            match operation with
                                | Add g -> packer.Add g |> ignore
                                | Rem g -> packer.Remove g |> ignore

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
            rj.IndirectBuffer <- indirect |> Mod.map (fun a -> ArrayBuffer(a) :> IBuffer)
            //rj.IndirectCount <- indirect |> Mod.map Array.length
            rj.Mode <- Mod.constant r.Mode
            rj.Activate <- activate

            ASet.single (rj :> IRenderObject)

        member x.RenderObjects(r : Sg.OverlayNode) : aset<IRenderObject> =
            ASet.empty

    [<Semantic>]
    type SubTaskSem() =
        member x.OverlayTasks(r : ISg) : aset<RenderPass * IRenderTask> =
            ASet.empty

        member x.OverlayTasks(app : IApplicator) =
            aset {
                let! c = app.Child
                yield! c.OverlayTasks()
            }


        member x.OverlayTasks(g : IGroup) =
            aset {
                for c in g.Children do
                    yield! c.OverlayTasks()
            }

        member x.OverlayTasks(r : Sg.OverlayNode) =
            ASet.single (r.RenderPass, r.RenderTask)