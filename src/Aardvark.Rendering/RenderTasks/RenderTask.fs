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

    let private defaultClearValues =
        clear {
            color C4f.Black
            depth 1.0
            stencil 0
        }

    /// Runs the given render task using the given framebuffer as output after clearing it according
    /// to the given clear values.
    let renderToWithClear (target : IAdaptiveResource<IFramebuffer>) (clearValues : ClearValues) (task : IRenderTask) =
        task.RenderTo(target, clearValues)

    /// Runs the given render task using the given framebuffer as output.
    let renderTo (target : IAdaptiveResource<IFramebuffer>) (task : IRenderTask) =
        task.RenderTo target

    /// Retrieves the texture attachment with the given name from the given framebuffer.
    let getResult (sem : Symbol) (t : IAdaptiveResource<IFramebuffer>) =
        t.GetOutputTexture sem


    /// Runs a render task for the given adaptive size and returns a map containing the textures specified as output.
    /// The resulting framebuffer is cleared according to the given clear values before the render task is executed.
    let renderSemanticsWithClear (output : Set<Symbol>) (size : aval<V2i>) (clearValues : ClearValues) (task : IRenderTask) =
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

        let fbo = runtime.CreateFramebuffer(signature, size, Set.difference attachments output)
        let res = task.RenderTo(fbo, clearValues)
        output |> Seq.map (fun k -> k, getResult k res) |> Map.ofSeq

    /// Runs a render task for the given adaptive size and returns a map containing the textures specified as output.
    let renderSemantics (output : Set<Symbol>) (size : aval<V2i>) (task : IRenderTask) =
        task |> renderSemanticsWithClear output size defaultClearValues


    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Colors as texture.
    /// The resulting framebuffer is cleared according to the given clear values before the render task is executed.
    let renderToColorWithClear (size : aval<V2i>) (clearValues : ClearValues) (task : IRenderTask) =
        task |> renderSemanticsWithClear (Set.singleton DefaultSemantic.Colors) size clearValues |> Map.find DefaultSemantic.Colors

    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Colors as texture.
    let renderToColor (size : aval<V2i>) (task : IRenderTask) =
        task |> renderToColorWithClear size defaultClearValues


    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Depth as texture.
    /// The resulting framebuffer is cleared according to the given clear values before the render task is executed.
    let renderToDepthWithClear (size : aval<V2i>) (clearValues : ClearValues) (task : IRenderTask) =
        task |> renderSemanticsWithClear (Set.singleton DefaultSemantic.Depth) size clearValues |> Map.find DefaultSemantic.Depth

    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Depth as texture.
    let renderToDepth (size : aval<V2i>) (task : IRenderTask) =
        task |> renderToDepthWithClear size defaultClearValues


    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Depth and DefaultSemantic.Stencil as textures.
    /// The resulting framebuffer is cleared according to the given clear values before the render task is executed.
    let renderToDepthAndStencilWithClear (size : aval<V2i>) (clearValues : ClearValues) (task : IRenderTask) =
        let map = task |> renderSemanticsWithClear (Set.singleton DefaultSemantic.Depth) size clearValues
        (Map.find DefaultSemantic.Depth map, Map.find DefaultSemantic.Stencil map)

    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Depth and DefaultSemantic.Stencil as textures.
    let renderToDepthAndStencil (size : aval<V2i>) (task : IRenderTask) =
        task |> renderToDepthAndStencilWithClear size defaultClearValues


    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Colors and DefaultSemantic.Depth as textures.
    /// The resulting framebuffer is cleared according to the given clear values before the render task is executed.
    let renderToColorAndDepthWithClear (size : aval<V2i>) (clearValues : ClearValues) (task : IRenderTask) =
        let map = task |> renderSemanticsWithClear (Set.ofList [DefaultSemantic.Depth; DefaultSemantic.Colors]) size clearValues
        (Map.find DefaultSemantic.Colors map, Map.find DefaultSemantic.Depth map)

    /// Runs a render task for the given adaptive size and returns the output for DefaultSemantic.Colors and DefaultSemantic.Depth as textures.
    let renderToColorAndDepth (size : aval<V2i>) (task : IRenderTask) =
        task |> renderToColorAndDepthWithClear size defaultClearValues


    // ================================================================================================================
    //  Render to cube texture
    // ================================================================================================================

    /// Runs the given render tasks using the given cube framebuffers as output.
    let renderToCube (target : IAdaptiveResource<CubeMap<IFramebuffer>>) (task : CubeMap<IRenderTask>) =
        task.RenderTo target

    /// Runs the given render tasks using the given cube framebuffers as output after clearing them according
    /// to the given clear values.
    let renderToCubeWithClear (target : IAdaptiveResource<CubeMap<IFramebuffer>>) (clearValues : CubeMap<ClearValues>) (task : CubeMap<IRenderTask>) =
        task.RenderTo(target, clearValues)

    /// Runs the given render tasks using the given cube framebuffers as output after clearing them according
    /// to the given clear values.
    let renderToCubeWithUniformClear (target : IAdaptiveResource<CubeMap<IFramebuffer>>) (clearValues : ClearValues) (task : CubeMap<IRenderTask>) =
        task.RenderTo(target, clearValues)

    /// Retrieves the cube texture attachment with the given name from the given framebuffers.
    let getResultCube (sem : Symbol) (t : IAdaptiveResource<CubeMap<IFramebuffer>>) =
        t.GetOutputTexture sem


    /// Runs the mipmap cube render tasks for the given adaptive size.
    /// Returns a map containing the textures specified as output.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderSemanticsCubeMipWithClear (output : Set<Symbol>) (size : aval<int>) (clearValues : CubeMap<ClearValues>) (tasks : CubeMap<IRenderTask>) =
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

        let fbo = runtime.CreateFramebufferCube(signature, size, tasks.Levels, Set.difference attachments output)
        let res = tasks.RenderTo(fbo, clearValues)
        output |> Seq.map (fun k -> k, getResultCube k res) |> Map.ofSeq

    /// Runs the mipmap cube render tasks for the given adaptive size.
    /// Returns a map containing the textures specified as output.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderSemanticsCubeMipWithUniformClear (output : Set<Symbol>) (size : aval<int>) (clearValues : ClearValues) (tasks : CubeMap<IRenderTask>) =
        let clear = clearValues |> CubeMap.single tasks.Levels
        tasks |> renderSemanticsCubeMipWithClear output size clear

    /// Runs the mipmap cube render tasks for the given adaptive size.
    /// Returns a map containing the textures specified as output.
    let renderSemanticsCubeMip (output : Set<Symbol>) (size : aval<int>) (tasks : CubeMap<IRenderTask>) =
        let clear = defaultClearValues |> CubeMap.single tasks.Levels
        tasks |> renderSemanticsCubeMipWithClear output size clear


    /// Runs the cube render tasks for the given adaptive size.
    /// Returns a map containing the textures specified as output.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderSemanticsCubeWithClear (output : Set<Symbol>) (size : aval<int>) (clearValues : CubeSide -> ClearValues) (tasks : CubeSide -> IRenderTask) =
        let clear = CubeMap.init 1 (fun face _ -> clearValues face)
        let tasks = CubeMap.init 1 (fun face _ -> tasks face)
        tasks |> renderSemanticsCubeMipWithClear output size clear

    /// Runs the cube render tasks for the given adaptive size.
    /// Returns a map containing the textures specified as output.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderSemanticsCubeWithUniformClear (output : Set<Symbol>) (size : aval<int>) (clearValues : ClearValues) (tasks : CubeSide -> IRenderTask) =
        let clear = fun _ -> clearValues
        tasks |> renderSemanticsCubeWithClear output size clear

    /// Runs the cube render tasks for the given adaptive size.
    /// Returns a map containing the textures specified as output.
    let renderSemanticsCube (output : Set<Symbol>) (size : aval<int>) (tasks : CubeSide -> IRenderTask) =
        let clear = fun _ -> defaultClearValues
        tasks |> renderSemanticsCubeWithClear output size clear


    /// Runs mipmap cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Colors as texture.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderToColorCubeMipWithClear (size : aval<int>) (clearValues : CubeMap<ClearValues>) (tasks : CubeMap<IRenderTask>) =
        tasks |> renderSemanticsCubeMipWithClear (Set.singleton DefaultSemantic.Colors) size clearValues |> Map.find DefaultSemantic.Colors

    /// Runs mipmap cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Colors as texture.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderToColorCubeMipWithUniformClear (size : aval<int>) (clearValues : ClearValues) (tasks : CubeMap<IRenderTask>) =
        let clear = CubeMap.single tasks.Levels clearValues
        tasks |> renderToColorCubeMipWithClear size clear

    /// Runs mipmap cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Colors as texture.
    let renderToColorCubeMip (size : aval<int>) (tasks : CubeMap<IRenderTask>) =
        let clear = CubeMap.single tasks.Levels defaultClearValues
        tasks |> renderToColorCubeMipWithClear size clear


    /// Runs cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Colors as texture.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderToColorCubeWithClear (size : aval<int>) (clearValues : CubeSide -> ClearValues) (tasks : CubeSide -> IRenderTask) =
        let clear = CubeMap.init 1 (fun face _ -> clearValues face)
        let tasks = CubeMap.init 1 (fun face _ -> tasks face)
        tasks |> renderToColorCubeMipWithClear size clear

    /// Runs cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Colors as texture.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderToColorCubeWithUniformClear (size : aval<int>) (clearValues : ClearValues) (tasks : CubeSide -> IRenderTask) =
        let clear = fun _ -> clearValues
        tasks |> renderToColorCubeWithClear size clear

    /// Runs cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Colors as texture.
    let renderToColorCube (size : aval<int>) (tasks : CubeSide -> IRenderTask) =
        let clear = fun _ -> defaultClearValues
        tasks |> renderToColorCubeWithClear size clear


    /// Runs mipmap cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Depth as texture.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderToDepthCubeMipWithClear (size : aval<int>) (clearValues : CubeMap<ClearValues>) (tasks : CubeMap<IRenderTask>) =
        tasks |> renderSemanticsCubeMipWithClear (Set.singleton DefaultSemantic.Depth) size clearValues |> Map.find DefaultSemantic.Depth

    /// Runs mipmap cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Depth as texture.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderToDepthCubeMipWithUniformClear (size : aval<int>) (clearValues : ClearValues) (tasks : CubeMap<IRenderTask>) =
        let clear = CubeMap.single tasks.Levels clearValues
        tasks |> renderToDepthCubeMipWithClear size clear

    /// Runs mipmap cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Depth as texture.
    let renderToDepthCubeMip (size : aval<int>) (tasks : CubeMap<IRenderTask>) =
        let clear = CubeMap.single tasks.Levels defaultClearValues
        tasks |> renderToDepthCubeMipWithClear size clear


    /// Runs cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Depth as texture.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderToDepthCubeWithClear (size : aval<int>) (clearValues : CubeSide -> ClearValues) (tasks : CubeSide -> IRenderTask) =
        let clear = CubeMap.init 1 (fun face _ -> clearValues face)
        let tasks = CubeMap.init 1 (fun face _ -> tasks face)
        tasks |> renderToDepthCubeMipWithClear size clear

    /// Runs cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Depth as texture.
    /// The resulting framebuffers are cleared according to the given clear values before the render tasks are executed.
    let renderToDepthCubeWithUniformClear (size : aval<int>) (clearValues : ClearValues) (tasks : CubeSide -> IRenderTask) =
        let clear = fun _ -> clearValues
        tasks |> renderToDepthCubeWithClear size clear

    /// Runs cube render tasks for the given adaptive size.
    /// Returns the output for DefaultSemantic.Depth as texture.
    let renderToDepthCube (size : aval<int>) (tasks : CubeSide -> IRenderTask) =
        let clear = fun _ -> defaultClearValues
        tasks |> renderToDepthCubeWithClear size clear


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