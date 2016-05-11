namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering
open System.Collections.Generic
open System.Threading
open Aardvark.Rendering.Vulkan

type InputSet(o : IAdaptiveObject) =
    let l = obj()
    let inputs = ReferenceCountingSet<IAdaptiveObject>()

    abstract member Add : IAdaptiveObject -> unit
    abstract member Remove : IAdaptiveObject -> unit

    default x.Add(m : IAdaptiveObject) = 
        lock l (fun () ->
            if inputs.Add m then
                m.Outputs.Add o |> ignore
        )

    default x.Remove (m : IAdaptiveObject) = 
        lock l (fun () ->
            if inputs.Remove m then
                m.Outputs.Remove o |> ignore
        )

type RenderTaskInputSet(target : IRenderTask) =
    inherit InputSet(target)

    let resources = ReferenceCountingSet<IResource>()

 
    member x.Resources = resources

    override x.Add(o : IAdaptiveObject) =
        match o with
            | :? IResource as r ->
                resources.Add r |> ignore
            | _ -> ()

        base.Add(o)
            
    override x.Remove(o : IAdaptiveObject) =
        match o with
            | :? IResource as r ->
                resources.Remove r |> ignore
            | _ -> ()

        base.Remove(o)


[<AbstractClass>]
type AbstractRenderTask(runtime : IRuntime, context : Context, renderPass : RenderPass) =
    inherit AdaptiveObject()

    let mutable frameId = 0UL


    member x.Run(caller : IAdaptiveObject, outputs : OutputDescription) =
        x.EvaluateAlways caller (fun () ->
            let stats = x.Run (renderPass, outputs)
            frameId <- frameId + 1UL
            RenderingResult(outputs.framebuffer, stats)
        )

    abstract member Run : RenderPass * OutputDescription -> FrameStatistics
    abstract member Dispose : unit -> unit

    member x.Context = context
    member x.Runtime = runtime
    member x.FramebufferSignature = renderPass :> IFramebufferSignature
    member x.FrameId = frameId

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IRenderTask with
        member x.Runtime = Some runtime
        member x.FramebufferSignature = renderPass :> IFramebufferSignature
        member x.Run(caller, outputs) = x.Run(caller, outputs)
        member x.FrameId = frameId

[<AbstractClass>]
type AbstractRenderTaskWithResources(manager : ResourceManager, fboSignature : RenderPass) as this =
    inherit AbstractRenderTask(manager.Runtime, manager.Context, fboSignature)

    let context = manager.Context

    let dirtyLock = obj()
    let mutable dirtyResources = HashSet<IResource>()
    let mutable dirtyPoolIds = Array.empty
    let inputSet = RenderTaskInputSet(this) 

    let updateCPUTime = System.Diagnostics.Stopwatch()
    let updateGPUTime = System.Diagnostics.Stopwatch()
    let executionTime = System.Diagnostics.Stopwatch()

    let mutable frameStatistics = FrameStatistics.Zero
    let mutable oneTimeStatistics = FrameStatistics.Zero

    let taskScheduler = new CustomTaskScheduler(Environment.ProcessorCount)
    let threadOpts = System.Threading.Tasks.ParallelOptions(TaskScheduler = taskScheduler)

    member x.Manager = manager

    member x.AddInput(i : IAdaptiveObject) =
        match i with
            | :? IResource as r ->
                if r.OutOfDate then
                    lock dirtyLock (fun () ->
                        dirtyResources.Add(r) |> ignore
                    )
            | _ -> ()

        inputSet.Add i

    member x.RemoveInput(i : IAdaptiveObject) =
        inputSet.Remove i

    member x.AddOneTimeStats (f : FrameStatistics) =
        oneTimeStatistics <- oneTimeStatistics + f

    member x.AddStats (f : FrameStatistics) =
        frameStatistics <- frameStatistics + f

    member x.RemoveStats (f : FrameStatistics) = 
        frameStatistics <- frameStatistics - f

    override x.InputChanged(transaction : obj, o : IAdaptiveObject) =
        match o with
            | :? IResource as o ->
                lock dirtyLock (fun () ->
                    dirtyResources.Add(o) |> ignore
                )
            | _ -> ()


    member x.UpdateDirtyResources() =
        let rec recurse() =
            let mutable stats = FrameStatistics.Zero
            let mutable count = 0 
            let counts = Dictionary<ResourceKind, ref<int>>()


            let dirty = System.Threading.Interlocked.Exchange(&dirtyResources, HashSet())
            if dirty.Count > 0 then
                dirty 
                    |> Seq.toList
                    |> List.map (fun d -> d.Update(x))
                    |> Command.ofSeq 
                    |> context.DefaultQueue.RunSynchronously
  

                let counts = counts |> Dictionary.toSeq |> Seq.map (fun (k,v) -> k,float !v) |> Map.ofSeq

                let own = 
                    { stats with
                        ResourceUpdateCount = stats.ResourceUpdateCount + float count
                        ResourceUpdateCounts = counts
                        ResourceUpdateTime = updateCPUTime.Elapsed + updateGPUTime.Elapsed
                    }

                if dirtyResources.Count > 0 then
                    Log.line "nested shit"
                    x.UpdateDirtyResources() + own
                else
                    own
            else
                FrameStatistics.Zero
        

        let res = lock dirtyLock recurse
        res

    member x.GetStats() =
        let res = oneTimeStatistics + frameStatistics
        oneTimeStatistics <- FrameStatistics.Zero
        { res with
            ResourceCount = float inputSet.Resources.Count 
        }

