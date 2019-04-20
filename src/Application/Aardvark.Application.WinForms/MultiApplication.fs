namespace Aardvark.Application.WinForms

open System
open Aardvark.Application
open System.Drawing
open System.Windows.Forms
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open System.Threading

type MultiFramebuffer(signature : IFramebufferSignature, framebuffers : IFramebuffer[]) =
    member x.Framebuffers = framebuffers

    interface IFramebuffer with
        member x.Dispose() = framebuffers |> Array.iter (fun f -> f.Dispose())
        member x.Signature = signature
        member x.GetHandle _ = null
        member x.Attachments = failwith ""
        member x.Size = framebuffers.[0].Size

type MultiRenderTask(runtime : MultiRuntime, signature : IFramebufferSignature, tasks : IRenderTask[]) =
    inherit AdaptiveObject()

    member x.Tasks = tasks

    interface IRenderTask with
        member x.Dispose() = tasks |> Array.iter (fun t -> t.Dispose())
        member x.Update(t,rt) =
            x.EvaluateIfNeeded t () (fun t ->
                tasks |> Array.iter (fun task -> task.Update(t,rt))
            )

        member x.Use f = f()
        member x.FrameId = tasks |> Seq.map (fun t -> t.FrameId) |> Seq.max
        member x.FramebufferSignature = Some signature
        member x.Runtime = Some (runtime :> IRuntime)
        member x.Run(t,rt,o) =
            x.EvaluateAlways t (fun t ->
                let current = o.framebuffer.Signature.Runtime
                let index = runtime.Runtimes.IndexOf current
                tasks.[index].Run(t, rt, o)
            )

and MultiFramebufferSignature(runtime : IRuntime, signatures : IFramebufferSignature[]) =
    member x.Signatures = signatures
    interface IFramebufferSignature with
        member x.IsAssignableFrom _ = true
        member x.ColorAttachments = signatures.[0].ColorAttachments
        member x.DepthAttachment = signatures.[0].DepthAttachment
        member x.StencilAttachment = signatures.[0].StencilAttachment
        member x.Images = signatures.[0].Images
        member x.LayerCount = signatures.[0].LayerCount
        member x.PerLayerUniforms = signatures.[0].PerLayerUniforms
        member x.Runtime = runtime

//type MultiBlock<'a>(blocks : Management.Block<'a>[]) =
//    inherit Management.Block<'a>(blocks.[0].Parent, blocks.[0].Offset, blocks.[0].Size, blocks.[1].IsFree, null, null)
//    member x.Blocks = blocks
//

//type MultiGeometryPool(pools : IGeometryPool[]) =
//    member x.Pools = pools
//
//    interface IGeometryPool with
//        member x.Dispose() = pools |> Array.iter (fun p -> p.Dispose())
//        member x.Alloc(fvc, g) =
//            let blocks = pools |> Array.map (fun p -> p.Alloc(fvc, g))
//            MultiBlock blocks :> Management.Block<_>
//
//        member x.Free(b : Management.Block<unit>) =
//            match b with
//                | :? MultiBlock<unit> as b ->
//                    for (p,b) in Array.zip pools b.Blocks do
//                        p.Free b
//
//                | _ ->
//                    ()
//
//        member x.TryGetBufferView(sem : Symbol) =
//            let all = pools |> Array.map (fun p -> p.TryGetBufferView sem)
//            if all |> Array.forall Option.isSome then
//                let views = all |> Array.map (fun v -> v.Value)
//                let v = views.[0]
//
//
//
//            else
//                None
//
//        member x.Count = pools.[0].Count
//        member x.UsedMemory = pools |> Array.sumBy (fun p -> p.UsedMemory)



and private NAryTimeMod(inputs : IMod<DateTime>[]) =
    inherit Mod.AbstractMod<DateTime>()

    let all = inputs |> Seq.map (fun m -> m.Id) |> Set.ofSeq
    let mutable missing = all

    override x.InputChanged(_,o) =
        Interlocked.Change(&missing, Set.remove o.Id) |> ignore
        
    override x.Mark() =
        if Set.isEmpty missing then
            missing <- all
            true
        else
            x.OutOfDate <- false
            false

    override x.Compute(t) =
        inputs |> Seq.map (fun i -> i.GetValue t) |> Seq.max


and SplitControl(runtime : IRuntime, count : int, samples : int) as this =
    inherit Panel()
    let controls = Array.init count (fun _ -> new RenderControl(Dock = DockStyle.None))

    let beforeRender = Event<unit>()
    let afterRender = Event<unit>()

    let mutable beforeCount = 0
    let mutable afterCount = 0

    do controls |> Array.iter (fun c -> 
        c.BeforeRender.Add (fun () ->
            let nv = Interlocked.Increment(&beforeCount)
            if nv = 1 then
                beforeRender.Trigger()
            elif nv = controls.Length then
                beforeCount <- 0
        )
    )
    do controls |> Array.iter (fun c -> 
        c.AfterRender.Add (fun () ->
            let nv = Interlocked.Increment(&afterCount)
            if nv = controls.Length then
                afterRender.Trigger()
                afterCount <- 0
        )
    )

    let time = lazy (NAryTimeMod (controls |> Array.map (fun c -> c.Time)) :> IMod<_>)

    let signature = lazy (MultiFramebufferSignature(runtime, controls |> Array.map (fun c -> c.FramebufferSignature)))

    let mutable task = RenderTask.empty

    let keyboard, mouse, subscriptions =
        let m = EventMouse(false) 
        let k = EventKeyboard()
        let subscriptions =
            controls |> Array.collect(fun c ->
                [|
                    k.Use(c.Keyboard) 
                    m.Use(c.Mouse) 
                |]
            )

        k, m, subscriptions

    let splitters : SplitContainer[] = Array.zeroCreate (controls.Length - 1)

    let splitterChanged () =
        this.SuspendLayout()
        let mutable off = 0
        for i in 1 .. controls.Length - 1 do
            let last = splitters.[i-1]
            off <- off + last.SplitterDistance + last.SplitterWidth
            controls.[i].Left <- -off

        this.ResumeLayout()

    let withLabel (i : int) (ctrl : RenderControl) =
        let runtime = runtime |> unbox<MultiRuntime>
        let text = 
            let name = runtime.Runtimes.[i].GetType().FullName
            if name.Contains "GL." then "GL"
            else "Vulkan"
            

        let label = new Label(Text = text)
        label.ForeColor <- Color.White
        label.BackColor <- Color.Black
        label.Font <- new Font("Consolas", 20.0f)
        label.Width <- 100000
        label.Height <- 30

        let left = new Panel()
        left.Controls.Add ctrl
        left.Controls.Add label
        left.Dock <- DockStyle.Fill
        
        label.BringToFront()
        left :> Control

    let rec merge (i : int) (controls : RenderControl[]) =
        if i >= controls.Length - 1 then
            withLabel i controls.[i]
        else
            let l = withLabel i controls.[i]

            let container = new SplitContainer(Dock = DockStyle.Fill)
            splitters.[i] <- container

            let r = merge (i + 1) controls

            container.Panel1.Controls.Add l
            container.Panel2.Controls.Add r
            container.SplitterMoved.Add (fun e ->
                splitterChanged()
            )

            container :> Control

    let content = merge 0 controls
    do base.Controls.Add(content)
       
    override x.OnClientSizeChanged(e) =
        base.OnClientSizeChanged(e)
        let newSize = this.ClientSize
        for c in controls do c.Size <- newSize
        splitterChanged()

    override x.OnHandleCreated(e) =
        base.OnHandleCreated(e)
        let newSize = this.ClientSize
        for c in controls do c.Size <- newSize
        splitterChanged()

    member x.Controls = controls
    
    member x.BeforeRender = beforeRender.Publish
    member x.AfterRender = afterRender.Publish

    interface IRenderTarget with
        member x.FramebufferSignature = signature.Value :> IFramebufferSignature
        member x.Runtime = runtime
        member x.RenderTask
            with get() = task 
            and set t =
                Array.iter (fun (c : RenderControl) -> c.RenderTask <- t) controls
                task <- t

        member x.Samples = samples
        member x.Sizes = controls.[0].Sizes
        member x.Time = time.Value
        member x.BeforeRender = beforeRender.Publish
        member x.AfterRender = afterRender.Publish

    interface IRenderControl with
        member x.Mouse = mouse :> IMouse
        member x.Keyboard = keyboard :> IKeyboard


and MultiRuntime(runtimes : IRuntime[]) =
    let disp = Event<unit>()
    member x.Dispose() =
        disp.Trigger()

    interface IDisposable with
        member x.Dispose() =
            disp.Trigger()

    member x.Runtimes : IRuntime[] = runtimes

    interface IRuntime with
        member x.DeviceCount = runtimes |> Seq.map (fun r -> r.DeviceCount) |> Seq.min

        member x.CreateLodRenderer(config : LodRendererConfig, data : aset<LodTreeInstance>) =
            failwithf "not implemented"


        member x.Copy<'a when 'a : unmanaged>(src : NativeTensor4<'a>, fmt : Col.Format, dst : ITextureSubResource, dstOffset : V3i, size : V3i) : unit =
            failwith "not implemented"

        member x.Copy<'a when 'a : unmanaged>(src : ITextureSubResource, srcOffset : V3i, dst : NativeTensor4<'a>, fmt : Col.Format, size : V3i) : unit =
            failwith "not implemented"
            
        member x.Copy(src : IFramebufferOutput, srcOffset : V3i, dst : IFramebufferOutput, dstOffset : V3i, size : V3i) : unit =
            failwith "not implemented"

        member x.OnDispose = disp.Publish

        member x.AssembleEffect (effect : FShade.Effect, signature : IFramebufferSignature, topology : IndexedGeometryMode) =
            failwith ""

        member x.AssembleModule (effect : FShade.Effect, signature : IFramebufferSignature, topology : IndexedGeometryMode) =
            failwith ""

        member x.ResourceManager = failwith "not implemented"

        member x.CreateFramebufferSignature(a,b,c,d) = 
            let res = runtimes |> Array.map (fun r -> r.CreateFramebufferSignature(a,b,c,d))
            MultiFramebufferSignature(x, res) :> IFramebufferSignature

        member x.DeleteFramebufferSignature(s) =
            match s with
                | :? MultiFramebufferSignature as s ->
                    for (r, s) in Array.zip runtimes s.Signatures do
                        r.DeleteFramebufferSignature s
                | _ ->
                    ()

        member x.Download(t : IBackendTexture, level : int, slice : int, target : PixImage) : unit = failwith ""
        member x.Download(t : IBackendTexture, level : int, slice : int, target : PixVolume) : unit = failwith ""
        member x.Upload(t : IBackendTexture, level : int, slice : int, source : PixImage) = failwith ""
        member x.DownloadDepth(t : IBackendTexture, level : int, slice : int, target : Matrix<float32>) = failwith ""
        member x.DownloadStencil(t : IBackendTexture, level : int, slice : int, target : Matrix<int>) = failwith ""

        member x.ResolveMultisamples(source, target, trafo) = failwith ""
        member x.GenerateMipMaps(t) = failwith ""
        member x.ContextLock = { new IDisposable with member x.Dispose() = () }
        member x.CompileRender (signature, engine, set) = 
            match signature with
                | :? MultiFramebufferSignature as signature ->
                    let tasks = Array.map2 (fun (r : IRuntime) (s : IFramebufferSignature) -> r.CompileRender(s, engine, set)) runtimes signature.Signatures
                    new MultiRenderTask(x, signature, tasks) :> IRenderTask
                | _ ->
                    failwith ""

        member x.CompileClear(signature, color, depth) = 
            match signature with
                | :? MultiFramebufferSignature as signature ->
                    let tasks = Array.map2 (fun (r : IRuntime) (s : IFramebufferSignature) -> r.CompileClear(s, color, depth)) runtimes signature.Signatures
                    new MultiRenderTask(x, signature, tasks) :> IRenderTask
                | _ ->
                    failwith ""

        member x.PrepareSurface(signature, s) = failwith ""
        member x.DeleteSurface(s) = failwith ""
        member x.PrepareRenderObject(fboSignature, rj) =failwith ""
        member x.PrepareTexture(t) = failwith ""
        member x.DeleteTexture(t) = failwith ""
        member x.PrepareBuffer(b) = failwith ""
        member x.DeleteBuffer(b) = failwith ""

        member x.DeleteRenderbuffer(b) = failwith ""
        member x.DeleteFramebuffer(f) = failwith ""

        member x.CreateStreamingTexture(mipMap) = failwith ""
        member x.DeleteStreamingTexture(t) = failwith ""

        member x.CreateSparseTexture<'a when 'a : unmanaged> (size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, brickSize : V3i, maxMemory : int64) : ISparseTexture<'a> =
            failwith ""

        member x.Copy(src : IBackendTexture, srcBaseSlice : int, srcBaseLevel : int, dst : IBackendTexture, dstBaseSlice : int, dstBaseLevel : int, slices : int, levels : int) = 
            failwith ""


        member x.CreateFramebuffer(signature, bindings) = failwith ""
        member x.CreateTexture(size, format, levels, samples) = failwith ""
        member x.CreateTextureArray(size, format, levels, samples, count) = failwith ""
        member x.CreateTextureCube(size, format, levels, samples) = failwith ""
        member x.CreateTextureCubeArray(size, format, levels, samples, count) = failwith ""

        member x.CreateTexture(size : V3i, dim : TextureDimension, format : TextureFormat, slices : int, levels : int, samples : int) =
            failwith ""


        member x.CreateRenderbuffer(size, format, samples) =
            failwith ""
        member x.CreateMappedBuffer() = failwith ""
        member x.CreateMappedIndirectBuffer(indexed) = failwith ""
        member x.CreateGeometryPool(types) = failwith ""



        member x.CreateBuffer(size : nativeint) = failwith ""

        member x.Copy(src : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) : unit =
            failwith ""

        member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) : unit =
            failwith ""

        member x.CopyAsync(src : IBackendBuffer, srcOffset : nativeint, dst : nativeint, size : nativeint) : unit -> unit =
            failwith ""

        member x.Copy(src : IBackendBuffer, srcOffset : nativeint, dst : IBackendBuffer, dstOffset : nativeint, size : nativeint) : unit = 
            failwith ""


        member x.MaxLocalSize = failwith ""
        member x.CreateComputeShader (c : FShade.ComputeShader) = failwith ""
        member x.NewInputBinding(c : IComputeShader) = failwith ""
        member x.DeleteComputeShader (shader : IComputeShader) = failwith ""
        member x.Run (commands : list<ComputeCommand>) = failwith ""
        member x.Compile (commands : list<ComputeCommand>) = failwith ""

        member x.Clear(fbo : IFramebuffer, clearColors : Map<Symbol,C4f>, depth : Option<float>, stencil : Option<int>) = failwith "not implemented"

        member x.ClearColor(texture : IBackendTexture, color : C4f) = failwith "not implemented"
        member x.ClearDepthStencil(texture : IBackendTexture, depth : Option<float>, stencil : Option<int>) = failwith "not implemented"

        member x.CreateTextureView(texture : IBackendTexture, levels : Range1i, slices : Range1i, isArray : bool) : IBackendTexture = failwith "not implemented"

type MultiApplication(apps : IApplication[]) =
    
    let runtime = new MultiRuntime(apps |> Array.map (fun a -> a.Runtime))

    member x.Initialize(ctrl : IRenderControl, samples : int) =
        match ctrl with
            | :? RenderControl as ctrl ->
                let self = new SplitControl(runtime, apps.Length, samples, Dock = DockStyle.Fill)
                for (a, c) in Array.zip apps self.Controls do
                    a.Initialize(c, samples)

                ctrl.Implementation <- self

            | _ ->
                failwithf "unknown control type: %A" ctrl

    interface IApplication with
        member x.Dispose() = 
            runtime.Dispose()
            apps |> Array.iter (fun a -> a.Dispose())
        member x.Runtime = runtime :> IRuntime
        member x.Initialize(win, samples) = x.Initialize(win, samples)



