namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

type DebugProgram(parent : IRenderTask,
                  manager : ResourceManager, 
                  inputSet : InputSet) =

    do inputSet.Add AdaptiveObject.Time

    let mutable allResources = HashSet<IResource>()
    let mutable usedResources = HashSet<IResource>()
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
                stats <- stats + r.Update(parent)

        for rj in renderObjects do
            let prep, own =
                match rj with
                    | :? PreparedRenderObject as p ->
                        if p.FramebufferSignature <> parent.FramebufferSignature then
                            failwithf "cannot add RenderObject with incompatible FramebufferSignature: %A" p.FramebufferSignature
                        p, false
                    | :? RenderObject as rj -> (manager.Prepare(parent.FramebufferSignature, rj), true)
                    | _ -> failwith "unsupported RenderObject type"

            let prog = DeltaCompiler.compileFull ctxMod prep

            // release (duplicated) resources whose reference count was incremented by compileFullPrepared
            if own then prep.Dispose()

            for r in prep.Resources do
                usedResources.Add r |> ignore

            for mi in prog do
                let instructions = mi.GetValue(parent)
                       
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
        //member x.Resources = ReferenceCountingSet()
        member x.RenderObjects = renderObjects :> seq<_>
        member x.Add rj = x.Add rj
        member x.Remove rj = x.Remove rj
        member x.Dispose() = x.Dispose()
        member x.Update(fbo,ctx) = FrameStatistics.Zero
        member x.Run(fbo,ctx) = x.Run(fbo, ctx)


