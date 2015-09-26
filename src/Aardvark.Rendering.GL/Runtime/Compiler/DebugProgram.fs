namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL


type DebugProgram(manager : ResourceManager, 
                  addInput : IAdaptiveObject -> unit, 
                  removeInput : IAdaptiveObject -> unit) =

    do addInput AdaptiveObject.Time

    let mutable allResources = HashSet<IChangeableResource>()
    let mutable usedResources = HashSet<IChangeableResource>()
    let renderObjects = HashSet<IRenderObject>()

    member x.Add (rj : IRenderObject) =
        renderObjects.Add rj |> ignore
    
    member x.Remove (rj : IRenderObject) =
        renderObjects.Remove rj |> ignore

    member x.Run(fbo : int, ctx : ContextHandle) =
        let ctxMod = Mod.constant ctx
        let mutable stats = FrameStatistics.Zero

        for r in allResources do
            if r.OutOfDate then
                r.UpdateCPU()
                r.UpdateGPU()

        for rj in renderObjects do
            let prep, own =
                match rj with
                    | :? PreparedRenderObject as p -> p, false
                    | :? RenderObject as rj -> (manager.Prepare rj, true)
                    | _ -> failwith "unsupported RenderObject type"

            let prog, _ = DeltaCompiler.compileFull manager ctxMod prep

            // release (duplicated) resources whose reference count was incremented by compileFullPrepared
            if own then prep.Dispose()

            for r in prog.Resources do
                usedResources.Add r |> ignore

            for mi in prog.Instructions do
                let instructions = 
                    match mi with
                        | AdaptiveInstruction m -> Mod.force m
                        | FixedInstruction l -> l
                       
                for i in instructions do
                    ExecutionContext.debug i 
                    stats <- stats + InstructionStatistics.toStats i

        allResources.ExceptWith usedResources
        for unused in allResources do
            unused.Dispose()
        allResources <- usedResources
        usedResources <- HashSet()

        stats

    member x.Dispose() =
        for r in allResources do
            r.Dispose()

        allResources.Clear()

    interface IRenderProgram with
        member x.Disassemble() = Seq.empty // TODO: disassemble debug program
        member x.Resources = ReferenceCountingSet()
        member x.RenderObjects = renderObjects :> seq<_>
        member x.Add rj = x.Add rj
        member x.Remove rj = x.Remove rj
        member x.Dispose() = x.Dispose()
        member x.Run(fbo,ctx) = x.Run(fbo, ctx)


