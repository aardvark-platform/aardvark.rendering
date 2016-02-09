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





        member x.RenderObjects(r : Sg.GeometrySet) : aset<IRenderObject> =
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