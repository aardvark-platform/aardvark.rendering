namespace Aardvark.Rendering

open System
open Aardvark.Base

open FSharp.Data.Adaptive

[<Struct>]
type LodRendererStats =
    {
        quality         : float
        maxQuality      : float
        totalPrimitives : int64
        totalNodes      : int
        allocatedMemory : Mem
        usedMemory      : Mem
        renderTime      : MicroTime
    }

type LodRendererConfig =
    {
        fbo : IFramebufferSignature
        time : aval<DateTime>
        surface : Surface
        state : PipelineState
        pass : RenderPass
        model : aval<Trafo3d>
        view : aval<Trafo3d>
        proj : aval<Trafo3d>
        budget : aval<int64>
        splitfactor : aval<float>
        renderBounds : aval<bool>
        maxSplits : aval<int>
        stats : cval<LodRendererStats>
        pickTrees : Option<cmap<ILodTreeNode,SimplePickTree>>
        alphaToCoverage : bool
    }

type ILodRuntime =
    abstract member CreateLodRenderer : config : LodRendererConfig * data : aset<LodTreeInstance> -> IPreparedRenderObject

type ILodRenderObject =
    inherit IRenderObject

    abstract member Prepare : ILodRuntime * IFramebufferSignature -> IPreparedRenderObject