namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module RenderObjectSemantics =

    type ISg with
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()

    module Semantic =
        [<System.Obsolete("renderJobs is deprecated, please use renderObjects instead.")>]        
        let renderJobs (s : ISg) : aset<IRenderObject> = s?RenderObjects()
        let renderObjects (s : ISg) : aset<IRenderObject> = s?RenderObjects()

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
                            DrawCallInfo(
                                FirstIndex = info.FirstIndex,
                                FirstInstance = info.FirstInstance,
                                InstanceCount = info.InstanceCount,
                                FaceVertexCount = count,
                                BaseVertex = 0
                            )
                    else
                        return info
                }

            rj.DrawCallInfo <- callInfo
            rj.Mode <- r.Mode
            ASet.single (rj :> IRenderObject)