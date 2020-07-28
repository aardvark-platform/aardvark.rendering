namespace Aardvark.Base

open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

module RenderTask =

    let empty = EmptyRenderTask.Instance

    let ofAFun (f : afun<AdaptiveToken * RenderToken * OutputDescription * TaskSync * IQuery, unit>) =
        new CustomRenderTask(f) :> IRenderTask

    let custom (f : AdaptiveToken * RenderToken * OutputDescription * TaskSync * IQuery -> unit) =
        new CustomRenderTask(AFun.create f) :> IRenderTask

    let before (f : unit -> unit) (t : IRenderTask) =
        match t with
        | :? BeforeAfterRenderTask as t ->
            let before =
                match t.Before with
                | None -> f
                | Some old -> f >> old
            new BeforeAfterRenderTask(Some before, t.After, t.Inner) :> IRenderTask
        | _ ->
            new BeforeAfterRenderTask(Some f, None, t) :> IRenderTask

    let after (f : unit -> unit) (t : IRenderTask) =
        match t with
        | :? BeforeAfterRenderTask as t ->
            let after =
                match t.After with
                | None -> f
                | Some old -> old >> f
            new BeforeAfterRenderTask(t.Before, Some after, t.Inner) :> IRenderTask
        | _ ->
            new BeforeAfterRenderTask(None, Some f, t) :> IRenderTask

    let ofMod (m : aval<IRenderTask>) : IRenderTask =
        new ModRenderTask(m) :> IRenderTask

    let bind (f : 'a -> IRenderTask) (m : aval<'a>) : IRenderTask =
        new ModRenderTask(AVal.map f m) :> IRenderTask

    let ofSeq (s : seq<IRenderTask>) =
        new SequentialRenderTask(Seq.toArray s) :> IRenderTask

    let ofList (s : list<IRenderTask>) =
        new SequentialRenderTask(List.toArray s) :> IRenderTask

    let ofArray (s : IRenderTask[]) =
        new SequentialRenderTask(s) :> IRenderTask

    let ofAList (s : alist<IRenderTask>) =
        new AListRenderTask(s) :> IRenderTask

    let ofASet (s : aset<IRenderTask>) =
        new AListRenderTask(s |> ASet.sortWith (fun a b -> compare a.Id b.Id)) :> IRenderTask

    let withFinalize (t : IRenderTask) =
        match t with
        | :? FinalizerRenderTask -> t
        | _ -> new FinalizerRenderTask(t) :> IRenderTask

    let renderTo (target : IOutputMod<IFramebuffer>) (task : IRenderTask) : IOutputMod<IFramebuffer> =
        task.RenderTo target

    let getResult (sem : Symbol) (t : IOutputMod<IFramebuffer>) =
        t.GetOutputTexture sem

    let renderSemantics (sem : Set<Symbol>) (size : aval<V2i>) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature.Value

        let clearColors =
            sem |> Set.toList |> List.filter (fun s -> s <> DefaultSemantic.Depth) |> List.map (fun s -> s,C4f.Black)
        let clear = runtime.CompileClear(signature, ~~clearColors, ~~1.0)
        let fbo = runtime.CreateFramebuffer(signature, sem, size)

        let task = new SequentialRenderTask([|clear; task|])
        let res = task.RenderTo(fbo, dispose = true)
        sem |> Seq.map (fun k -> k, getResult k res) |> Map.ofSeq

    let renderToColor (size : aval<V2i>) (task : IRenderTask) =
        task |> renderSemantics (Set.singleton DefaultSemantic.Colors) size |> Map.find DefaultSemantic.Colors

    let renderToDepth (size : aval<V2i>) (task : IRenderTask) =
        task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size |> Map.find DefaultSemantic.Depth

    let renderToDepthAndStencil (size : aval<V2i>) (task : IRenderTask) =
        let map = task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size
        (Map.find DefaultSemantic.Depth map, Map.find DefaultSemantic.Stencil map)

    let renderToColorAndDepth (size : aval<V2i>) (task : IRenderTask) =
        let map = task |> renderSemantics (Set.ofList [DefaultSemantic.Depth; DefaultSemantic.Colors]) size
        (Map.find DefaultSemantic.Colors map, Map.find DefaultSemantic.Depth map)

    let log fmt =
        Printf.kprintf (fun str ->
            let task =
                custom (fun (self, token, out, sync, queries) ->
                    Log.line "%s" str
                )

            task
        ) fmt

[<AutoOpen>]
module ``RenderTask Builder`` =
    type private Result = list<alist<IRenderTask>>

    type RenderTaskBuilder() =
        member x.Bind(m : aval<'a>, f : 'a -> Result) : Result =
            [alist.Bind(m, f >> AList.concat)]

        member x.For(s : alist<'a>, f : 'a -> Result): Result =
            [alist.For(s,f >> AList.concat)]

        member x.Bind(f : unit -> unit, c : unit -> Result) : Result =
            let task =
                RenderTask.custom (fun (self, token, out, sync, queries) ->
                    f()
                )
            (AList.single task)::c()

        member x.Return(u : unit) : Result =
            []

        member x.Bind(t : IRenderTask, c : unit -> Result) =
            alist.Yield(t)::c()

        member x.Bind(t : list<IRenderTask>, c : unit -> Result) =
            (AList.ofList t)::c()

        member x.Bind(l : alist<IRenderTask>, c : unit -> Result) =
            alist.YieldFrom(l)::c()

        member x.Bind(m : aval<IRenderTask>, c : unit -> Result) =
            let head = m |> RenderTask.ofMod |> alist.Yield
            head::c()

        member x.Combine(l : Result, r : Result) =
            l @ r

        member x.Delay(f : unit -> Result) =
            f()

        member x.Zero() =
            []

        member x.Run(l : Result) =
            let l = AList.concat l
            RenderTask.ofAList l

    let rendertask = RenderTaskBuilder()

    //let test (renderActive : aval<bool>) (clear : aval<IRenderTask>) (render : alist<IRenderTask>) =
    //    rendertask {
    //        do! RenderTask.log "before clear: %d" 190
    //        do! clear
    //        do! RenderTask.log "after clear"

    //        let! active = renderActive
    //        if active then
    //            do! render
    //            do! RenderTask.log "rendered"
    //    }