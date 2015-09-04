namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph
open Aardvark.Base.Rendering

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module RenderJobSemantics =

    type ISg with
        member x.RenderJobs() : aset<RenderObject> = x?RenderJobs()

    module Semantic =
        let renderJobs (s : ISg) : aset<RenderObject> = s?RenderJobs()

    [<Semantic>]
    type RenderJobSem() =

        member x.RenderJobs(a : IApplicator) : aset<RenderObject> =
            aset {
                let! c = a.Child
                yield! c.RenderJobs()
            }

        member x.RenderJobs(g : IGroup) : aset<RenderObject> =
            aset {
                for c in g.Children do
                    yield! c.RenderJobs()
            }

        member x.RenderJobs(r : Sg.RenderNode) : aset<RenderObject> =
            let scope = Ag.getContext()
            let rj = RenderObject.Create(scope.Path)
            
            rj.AttributeScope <- scope 
            rj.Indices <- let index  = r.VertexIndexArray in if index = AttributeSemantics.emptyIndex then null else index 
         
            rj.IsActive <- r.IsActive
            rj.RenderPass <- r.RenderPass
            
            
            rj.Uniforms <- new Providers.UniformProvider(scope, r?Uniforms)
            rj.VertexAttributes <- new Providers.AttributeProvider(scope, "VertexAttributes")
            rj.InstanceAttributes <- new Providers.AttributeProvider(scope, "InstanceAttributes")
            
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
                                Mode = info.Mode
                            )
                    else
                        return info
                }

            rj.DrawCallInfo <- callInfo

            ASet.single rj