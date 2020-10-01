namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

module RenderTask =

    let empty = EmptyRenderTask.Instance

    let ofAFun (f : afun<AdaptiveToken * RenderToken * OutputDescription * IQuery, unit>) =
        new CustomRenderTask(f) :> IRenderTask

    let custom (f : AdaptiveToken * RenderToken * OutputDescription * IQuery -> unit) =
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


    // ================================================================================================================
    //  Render to texture
    // ================================================================================================================

    /// Runs the given render task using the given framebuffer as output.
    let renderTo (target : IAdaptiveResource<IFramebuffer>) (task : IRenderTask) =
        task.RenderTo target

    /// Retrieves the texture attachment with the given name from the given framebuffer.
    let getResult (sem : Symbol) (t : IAdaptiveResource<IFramebuffer>) =
        t.GetOutputTexture sem

    /// Runs a render task for the given adaptive size and returns a map containing the textures specified as output.
    let renderSemantics (output : Set<Symbol>) (size : aval<V2i>) (task : IRenderTask) =
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature.Value

        // Gather all attachments to determine which ones are not requested as output
        let attachments =
            let color =
                signature.ColorAttachments |> Map.toList |> List.map (snd >> fst)

            let depth, stencil =
                signature.DepthAttachment |> (function None -> [] | _ -> [DefaultSemantic.Depth]),
                signature.StencilAttachment |> (function None -> [] | _ -> [DefaultSemantic.Stencil])

            [color; depth; stencil]
            |> List.concat |> Set.ofList

        let clear = runtime.CompileClear(signature, C4f.Black, 1.0, 0)
        let fbo = runtime.CreateFramebuffer(signature, size, Set.difference attachments output)

        let task = new SequentialRenderTask([|clear; task|])
        let res = task.RenderTo(fbo, dispose = true)
        output |> Seq.map (fun k -> k, getResult k res) |> Map.ofSeq

    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Colors as texture.
    let renderToColor (size : aval<V2i>) (task : IRenderTask) =
        task |> renderSemantics (Set.singleton DefaultSemantic.Colors) size |> Map.find DefaultSemantic.Colors

    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Depth as texture.
    let renderToDepth (size : aval<V2i>) (task : IRenderTask) =
        task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size |> Map.find DefaultSemantic.Depth

    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Depth and DefaultSemantic.Stencil as textures.
    let renderToDepthAndStencil (size : aval<V2i>) (task : IRenderTask) =
        let map = task |> renderSemantics (Set.singleton DefaultSemantic.Depth) size
        (Map.find DefaultSemantic.Depth map, Map.find DefaultSemantic.Stencil map)

    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Colors and DefaultSemantic.Depth as textures.
    let renderToColorAndDepth (size : aval<V2i>) (task : IRenderTask) =
        let map = task |> renderSemantics (Set.ofList [DefaultSemantic.Depth; DefaultSemantic.Colors]) size
        (Map.find DefaultSemantic.Colors map, Map.find DefaultSemantic.Depth map)


    // ================================================================================================================
    //  Render to cube texture
    // ================================================================================================================

    /// Runs the given render tasks using the given cube framebuffers as output.
    let renderToCube (target : IAdaptiveResource<CubeMap<IFramebuffer>>) (task : CubeMap<IRenderTask>) =
        task.RenderTo target

    /// Retrieves the cube texture attachment with the given name from the given framebuffers.
    let getResultCube (sem : Symbol) (t : IAdaptiveResource<CubeMap<IFramebuffer>>) =
        t.GetOutputTexture sem

    /// Runs the mipmap cube render tasks for the given adaptive size.
    /// Returns a map containing the textures specified as output.
    let renderSemanticsCubeMip (output : Set<Symbol>) (size : aval<int>) (tasks : CubeMap<IRenderTask>) =
        let task = tasks.Data.[0]
        let runtime = task.Runtime.Value
        let signature = task.FramebufferSignature.Value

        // Gather all attachments to determine which ones are not requested as output
        let attachments =
            let color =
                signature.ColorAttachments |> Map.toList |> List.map (snd >> fst)

            let depth, stencil =
                signature.DepthAttachment |> (function None -> [] | _ -> [DefaultSemantic.Depth]),
                signature.StencilAttachment |> (function None -> [] | _ -> [DefaultSemantic.Stencil])

            [color; depth; stencil]
            |> List.concat |> Set.ofList

        let clear = runtime.CompileClear(signature, C4f.Black, 1.0, 0)
        let fbo = runtime.CreateFramebufferCube(signature, size, tasks.Levels, Set.difference attachments output)

        let tasks = tasks |> CubeMap.map (fun task -> new SequentialRenderTask([|clear; task|]))
        let res = tasks.RenderTo(fbo, dispose = true)
        output |> Seq.map (fun k -> k, getResultCube k res) |> Map.ofSeq

    /// Runs the cube render tasks for the given adaptive size.
    /// Returns a map containing the textures specified as output.
    let renderSemanticsCube (output : Set<Symbol>) (size : aval<int>) (tasks : CubeSide -> IRenderTask) =
        CubeMap.init 1 (fun face _ -> tasks face) |> renderSemanticsCubeMip output size

    /// Runs mipmap cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Colors as texture.
    let renderToColorCubeMip (size : aval<int>) (tasks : CubeMap<IRenderTask>) =
       tasks |> renderSemanticsCubeMip (Set.singleton DefaultSemantic.Colors) size |> Map.find DefaultSemantic.Colors

    /// Runs cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Colors as texture.
    let renderToColorCube (size : aval<int>) (tasks : CubeSide -> IRenderTask) =
        CubeMap.init 1 (fun face _ -> tasks face) |> renderToColorCubeMip size

    /// Runs mipmap cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Depth as texture.
    let renderToDepthCubeMip (size : aval<int>) (tasks : CubeMap<IRenderTask>) =
        tasks |> renderSemanticsCubeMip (Set.singleton DefaultSemantic.Depth) size |> Map.find DefaultSemantic.Depth

    /// Runs cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Depth as texture.
    let renderToDepthCube (size : aval<int>) (tasks : CubeSide -> IRenderTask) =
        CubeMap.init 1 (fun face _ -> tasks face) |> renderToDepthCubeMip size


    let log fmt =
        Printf.kprintf (fun str ->
            let task =
                custom (fun (self, token, out, queries) ->
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
                RenderTask.custom (fun (self, token, out, queries) ->
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