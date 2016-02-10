namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering


[<AutoOpen>]
module RenderObjectSemantics =

    type ISg with
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()
        member x.OverlayTasks() : aset<uint64 * IRenderTask> = x?OverlayTasks()

    module Semantic =
        [<System.Obsolete("renderJobs is deprecated, please use renderObjects instead.")>]        
        let renderJobs (s : ISg) : aset<IRenderObject> = s?RenderObjects()
        let renderObjects (s : ISg) : aset<IRenderObject> = s?RenderObjects()
        let overlayTasks (s : ISg) : aset<uint64 * IRenderTask> = s?OverlayTasks()


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
            let rj = RenderObject.Create()
            
            rj.AttributeScope <- scope 
            rj.Indices <- let index  = r.VertexIndexArray in if index = AttributeSemantics.emptyIndex then null else index 
         
            rj.IsActive <- r.IsActive
            rj.RenderPass <- r.RenderPass
            
            let vertexAttributes = new Providers.AttributeProvider(scope, "VertexAttributes")
            let instanceAttributes =  new Providers.AttributeProvider(scope, "InstanceAttributes")

            rj.Uniforms <- new Providers.UniformProvider(scope, r?Uniforms, 
                                                         [vertexAttributes; instanceAttributes])
            rj.VertexAttributes <- vertexAttributes
            rj.InstanceAttributes <- instanceAttributes
            
            rj.DepthTest <- r.DepthTestMode
            rj.CullMode <- r.CullMode
            rj.FillMode <- r.FillMode
            rj.StencilMode <- r.StencilMode
            rj.BlendMode <- r.BlendMode
              
            rj.Surface <- r.Surface
            
            let callInfo =
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

            rj.DrawCallInfos <- callInfo
            rj.Mode <- r.Mode
            ASet.single (rj :> IRenderObject)





        member x.RenderObjectsOld(r : Sg.GeometrySet) : aset<IRenderObject> =
            let scope = Ag.getContext()
            let rj = RenderObject.Create()
            
            rj.AttributeScope <- scope 
            rj.Indices <- null
         
            rj.IsActive <- r.IsActive
            rj.RenderPass <- r.RenderPass
            
            let packer = new AttributePackingV2.Packer(r.Geometries, r.AttributeTypes)
            let vertexAttributes = packer.AttributeProvider
            let instanceAttributes =  new Providers.AttributeProvider(scope, "InstanceAttributes")

            rj.Uniforms <- new Providers.UniformProvider(scope, r?Uniforms, 
                                                         [vertexAttributes; instanceAttributes])
            rj.VertexAttributes <- vertexAttributes
            rj.InstanceAttributes <- instanceAttributes
            
            rj.DepthTest <- r.DepthTestMode
            rj.CullMode <- r.CullMode
            rj.FillMode <- r.FillMode
            rj.StencilMode <- r.StencilMode
            rj.BlendMode <- r.BlendMode
              
            rj.Surface <- r.Surface
            
            let indirect = packer.DrawCallInfos |> Mod.map List.toArray
            rj.IndirectBuffer <- indirect |> Mod.map (fun a -> ArrayBuffer(a) :> IBuffer)
            rj.IndirectCount <- indirect |> Mod.map Array.length
            //rj.DrawCallInfos <- packer.DrawCallInfos
            rj.Mode <- Mod.constant r.Mode
            ASet.single (rj :> IRenderObject)

        member x.RenderObjects(r : Sg.GeometrySet) : aset<IRenderObject> =
            let scope = Ag.getContext()
            let rj = RenderObject.Create()
            
            rj.AttributeScope <- scope 
            rj.Indices <- null
         
            rj.IsActive <- r.IsActive
            rj.RenderPass <- r.RenderPass
            
            let packer = new GeometrySetUtilities.GeometryPacker(r.AttributeTypes)
            let vertexAttributes =
                { new IAttributeProvider with
                    member x.TryGetAttribute(sem) =
                        match Map.tryFind sem r.AttributeTypes with
                            | Some t ->
                                match packer.TryGetBuffer sem with
                                    | Some b -> BufferView(b, t) |> Some
                                    | None -> None
                            | None -> None

                    member x.All = Seq.empty

                    member x.Dispose() = ()
                
                }

            let indirect =
                packer |> Mod.map (fun set ->
                    set |> Seq.toArray
                        |> Array.map (fun r ->
                            DrawCallInfo(
                                FirstIndex = 0,
                                FaceVertexCount = 1 + r.Size,
                                FirstInstance = 0,
                                InstanceCount = 1,
                                BaseVertex = 0
                            )
                        )

                )


            let instanceAttributes =  new Providers.AttributeProvider(scope, "InstanceAttributes")

            rj.Uniforms <- new Providers.UniformProvider(scope, r?Uniforms, 
                                                         [vertexAttributes; instanceAttributes])
            rj.VertexAttributes <- vertexAttributes
            rj.InstanceAttributes <- instanceAttributes
            
            rj.DepthTest <- r.DepthTestMode
            rj.CullMode <- r.CullMode
            rj.FillMode <- r.FillMode
            rj.StencilMode <- r.StencilMode
            rj.BlendMode <- r.BlendMode
              
            rj.Surface <- r.Surface
            
//            rj.IndirectBuffer <- indirect |> Mod.map (fun a -> ArrayBuffer(a) :> IBuffer)
//            rj.IndirectCount <- indirect |> Mod.map Array.length

            let activate() =
                let deltas = System.Collections.Concurrent.ConcurrentQueue<Delta<IndexedGeometry>>()
                let sem = new System.Threading.SemaphoreSlim(0)
                let exit = new System.Threading.SemaphoreSlim(0)
                let subscription =
                    r.Geometries |> ASet.unsafeRegisterCallbackKeepDisposable (fun d ->
                        let mutable cnt = 0
                        for op in d do
                            deltas.Enqueue(op)
                            cnt <- cnt + 1

                        if cnt > 0 then
                            sem.Release(cnt) |> ignore
                    )

                let mutable ops = 0
                let sw = System.Diagnostics.Stopwatch()

                let runner =
                    async {
                        do! Async.SwitchToNewThread()
                        sw.Start()
                        while true do
                            let! _ = Async.AwaitIAsyncResult(sem.WaitAsync())

                            let operation = 
                                match deltas.TryDequeue() with
                                    | (true, op) -> Some op
                                    | _ -> None

                            ops <- ops + 1

                            match operation with
                                | Some (Add g) -> packer.Add g |> ignore
                                | Some (Rem g) -> packer.Remove g |> ignore
                                | _ -> Log.warn "bad operation"

                            ()
                        
                    }


                let cancel = new System.Threading.CancellationTokenSource()
                let task = Async.StartAsTask(runner, cancellationToken = cancel.Token)

                { new System.IDisposable with
                    member x.Dispose() =
                        subscription.Dispose()
                        cancel.Cancel()
                        cancel.Dispose()
                        sem.Dispose()
                        packer.Dispose()
                        ()
                }

            rj.Activate <- activate
            rj.DrawCallInfos <- indirect |> Mod.map Array.toList
            rj.Mode <- Mod.constant r.Mode
            ASet.single (rj :> IRenderObject)



        member x.RenderObjects(r : Sg.OverlayNode) : aset<IRenderObject> =
            ASet.empty

    [<Semantic>]
    type SubTaskSem() =
        member x.OverlayTasks(r : ISg) : aset<uint64 * IRenderTask> =
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