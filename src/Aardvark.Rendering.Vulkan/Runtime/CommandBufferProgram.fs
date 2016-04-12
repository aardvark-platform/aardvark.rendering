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
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Runtime


[<AutoOpen>]
module private Compiler = 

    let instructionToCall (i : Instruction) : NativeCall =
        i.FunctionPointer, i.Arguments

    let callToInstruction (ctx : InstructionContext) (ptr : nativeint, args : obj[]) : Instruction =
        new Instruction(ctx, ptr, args)


    let draw (ctx : InstructionContext) (index : Option<Resource<Buffer>>) (drawCalls : IMod<list<DrawCallInfo>>) =
        [
            match index with
                | Some ib ->
                    yield ib.Handle |> Mod.map (fun ib -> ctx.BindIndexBuffer(ib, 0))

                    yield drawCalls |> Mod.map (
                        List.collect (fun draw ->
                            ctx.DrawIndexed(draw.FirstIndex, draw.FaceVertexCount, draw.FirstInstance, draw.InstanceCount, 0)
                        )
                    )

                | _ ->
                    yield drawCalls |> Mod.map (
                        List.collect (fun draw ->
                            ctx.Draw(draw.FirstIndex, draw.FaceVertexCount, draw.FirstInstance, draw.InstanceCount)
                        )
                    )
        ]

    let drawIndirect (ctx : InstructionContext) (index : Option<Resource<Buffer>>) (indirect : Resource<IndirectBuffer>) =
        [
            match index with
                | Some ib ->
                    yield ib.Handle |> Mod.map (fun ib -> ctx.BindIndexBuffer(ib, 0))

                    yield indirect.Handle |> Mod.map (fun i ->
                        ctx.DrawIndexedIndirect(i.Buffer.Handle, 0UL, i.Count, sizeof<VkDrawIndexedIndirectCommand>)
                    )

                | _ ->
                    yield indirect.Handle |> Mod.map (fun i ->
                        ctx.DrawIndirect(i.Buffer.Handle, 0UL, i.Count, sizeof<VkDrawIndirectCommand>)
                    )
        ]

    let compileInt (ctx : InstructionContext) (prev : Option<PreparedRenderObject>) (self : PreparedRenderObject) =
        let code =
            [
                let prog = self.program.Handle.GetValue()
                match prev with
                    | None ->
                        yield self.pipeline.Handle |> Mod.map (ctx.BindPipeline)

                        yield self.descriptorSets |> List.map (fun d -> d.Handle) |> Mod.mapN (fun handles ->
                            ctx.BindDescriptorSets(prog.PipelineLayout, Seq.toArray handles, 0)
                        )

                        yield self.vertexBuffers |> Array.map (fun (v,_) -> v.Handle) |> Mod.mapN (fun handles ->
                            ctx.BindVertexBuffers(Seq.toArray handles, self.vertexBuffers |> Array.map snd, 0)
                        )

                        match self.indirect with
                            | Some i -> yield! drawIndirect ctx self.indexBuffer i
                            | None -> yield! draw ctx self.indexBuffer self.DrawCallInfos

                    | Some prev ->
                        if prev.pipeline <> self.pipeline then
                            yield self.pipeline.Handle |> Mod.map (ctx.BindPipeline)

                        if prev.descriptorSets <> self.descriptorSets then
                            yield self.descriptorSets |> List.map (fun d -> d.Handle) |> Mod.mapN (fun handles ->
                                ctx.BindDescriptorSets(prog.PipelineLayout, Seq.toArray handles, 0)
                            )

                        if prev.vertexBuffers <> self.vertexBuffers then
                            yield self.vertexBuffers |> Array.map (fun (v,_) -> v.Handle) |> Mod.mapN (fun handles ->
                                ctx.BindVertexBuffers(Seq.toArray handles, self.vertexBuffers |> Array.map snd, 0)
                            )

                        match self.indirect with
                            | Some i -> yield! drawIndirect ctx self.indexBuffer i
                            | None -> yield! draw ctx self.indexBuffer self.DrawCallInfos

            ]

        new AdaptiveCode<Instruction>(code) :> IAdaptiveCode<_>

    let compile ctx prev self =
        compileInt ctx prev self



type ICommandBufferProgram =
    inherit IDisposable
    abstract member Update : IAdaptiveObject -> AdaptiveProgramStatistics
    abstract member Run : CommandBuffer -> unit

type NativeCommandBufferProgram(ctx : Context, renderObjects : aset<list<uint64> * PreparedRenderObject>) =
    let ictx = ctx.InstructionContext

    let program : IAdaptiveProgram<VkCommandBuffer> = 
        let handler = 
            FragmentHandler.warpDifferential 
                instructionToCall 
                (callToInstruction ictx)
                (compile ictx)
                (FragmentHandler.native 8) // at most 8 args (vkBindDescriptorSets)

        AdaptiveProgram.custom 
            Comparer<_>.Default 
            handler 
            renderObjects

    member x.Update(caller : IAdaptiveObject) =
        program.Update(caller)

    member x.Run(cmd : CommandBuffer) =
        program.Run(cmd.Handle)

    member x.Dispose() = program.Dispose()

    interface ICommandBufferProgram with
        member x.Dispose() = program.Dispose()
        member x.Update c = x.Update c
        member x.Run cmd = x.Run cmd

type ManagedCommandBufferProgram(ctx : Context, renderObjects : aset<list<uint64> * PreparedRenderObject>) =
    let ictx = ctx.InstructionContext

    let toFunc (i : Instruction) =
        fun cmd -> ictx.Run(i, cmd)

    let ofFunc (f : VkCommandBuffer -> unit) =
        failwithf "cannot decompile managed instruction"

    let program =
        let handler = 
            FragmentHandler.warpDifferential 
                toFunc 
                ofFunc
                (compile ictx)
                (FragmentHandler.managedSimple (fun _ -> failwith "not supported")) // at most 8 args (vkBindDescriptorSets)

        AdaptiveProgram.custom 
            Comparer<_>.Default 
            handler 
            renderObjects

    member x.Update(caller : IAdaptiveObject) =
        program.Update(caller)

    member x.Run(cmd : CommandBuffer) =
        program.Run(cmd.Handle)
    member x.Dispose() = program.Dispose()

    interface ICommandBufferProgram with
        member x.Dispose() = program.Dispose()
        member x.Update c = x.Update c
        member x.Run cmd = x.Run cmd