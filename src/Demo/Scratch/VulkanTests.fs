module VulkanTests


open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Incremental
open System.Diagnostics
open System.Collections.Generic
open Aardvark.Base.Runtime
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

#nowarn "9"
#nowarn "51"


type ResourceManager with
    member x.CreateDescriptorSet(setLayout : DescriptorSetLayout, uniforms : IUniformProvider) =
        let bindings = Array.zeroCreate setLayout.Bindings.Length
        let resources = List<IResourceLocation>()
        for bi in 0 .. bindings.Length - 1 do
            let bindingLayout = setLayout.Bindings.[bi]

            match bindingLayout.Parameter with
                | UniformBlockParameter block ->
                    let buffer = x.CreateUniformBuffer(Ag.emptyScope, block.layout, uniforms, SymDict.empty)
                    resources.Add buffer
                    bindings.[bi] <- Resources.AdaptiveDescriptor.AdaptiveUniformBuffer(bi, buffer)

                | ImageParameter image ->
                    let viewSam = 
                        image.description |> List.map (fun desc -> 
                            let textureName = desc.textureName
                            let samplerState = desc.samplerState
                            match uniforms.TryGetUniform(Ag.emptyScope, textureName) with
                            | Some (:? IMod<ITexture> as tex) ->

                                let tex = x.CreateImage(tex)
                                let view = x.CreateImageView(image.samplerType, tex)
                                let sam = x.CreateSampler(Mod.constant samplerState)

                                Some(view, sam)

                            | _ ->
                                Log.warn "[Vulkan] could not find texture: %A" textureName
                                None
                        )

                    bindings.[bi] <- Resources.AdaptiveDescriptor.AdaptiveCombinedImageSampler(bi, List.toArray viewSam)

        let set = x.CreateDescriptorSet(setLayout, Array.toList bindings)

        (CSharpList.toList resources, set)



type CommandState =
    {
        stats : nativeptr<V2i>
        scope : ResourceSet
        resources : HashSet<IResourceLocation>
        stream : VKVM.CommandStream
    }

[<AutoOpen>]
module CommandAPI =
    open Aardvark.Base.Monads.State

    type Command<'a> = Aardvark.Base.Monads.State.State<CommandState, 'a>

    type CommandBuilder() =
        inherit Aardvark.Base.Monads.State.StateBuilder()

        member x.Bind(r : INativeResourceLocation<'a>, f : nativeptr<'a> -> Command<'b>) : Command<'b> =
            state {
                let! s = State.get
                if s.resources.Add r then
                    s.scope.Add r
                let info = r.Update AdaptiveToken.Top
                return! f(info.handle)
            }

    type Command private() =
        static member Use (r : INativeResourceLocation<'a>) =
            State.get |> State.map (fun s ->
                if s.resources.Add r then
                    s.scope.Add r
                let info = r.Update AdaptiveToken.Top
                info.handle
            )
        static member Use (r : IResourceLocation) =
            State.get |> State.map (fun s ->
                if s.resources.Add r then
                    s.scope.Add r
                ()
            )
        
        static member IndirectBindPipeline(p : INativeResourceLocation<VkPipeline>) : Command<unit> =
            State.custom (fun s ->
                if s.resources.Add p then
                    s.scope.Add p
                let info = p.Update AdaptiveToken.Top
                let off = s.stream.IndirectBindPipeline info.handle
                s, ()
            )
        static member IndirectBindVertexBuffer(p : INativeResourceLocation<VertexBufferBinding>) : Command<unit> =
            State.custom (fun s ->
                if s.resources.Add p then
                    s.scope.Add p
                let info = p.Update AdaptiveToken.Top
                let off = s.stream.IndirectBindVertexBuffers info.handle
                s, ()
            )
        static member IndirectBindIndexBuffer(p : INativeResourceLocation<IndexBufferBinding>) : Command<unit> =
            State.custom (fun s ->
                if s.resources.Add p then
                    s.scope.Add p
                let info = p.Update AdaptiveToken.Top
                let off = s.stream.IndirectBindIndexBuffer info.handle
                s, ()
            )
        static member IndirectBindDescriptorSets(p : INativeResourceLocation<DescriptorSetBinding>) : Command<unit> =
            State.custom (fun s ->
                if s.resources.Add p then
                    s.scope.Add p
                let info = p.Update AdaptiveToken.Top
                let off = s.stream.IndirectBindDescriptorSets info.handle
                s, ()
            )
        static member IndirectDraw(isActive : INativeResourceLocation<int>, call : INativeResourceLocation<DrawCall>) : Command<unit> =
            State.custom (fun s ->
                if s.resources.Add call then
                    s.scope.Add call
                if s.resources.Add isActive then
                    s.scope.Add isActive

                let callInfo = call.Update AdaptiveToken.Top
                let isActiveInfo = isActive.Update AdaptiveToken.Top
                let off = s.stream.IndirectDraw(s.stats, isActiveInfo.handle, callInfo.handle)
                s, ()
            )

    let command = CommandBuilder()


[<AbstractClass>]
type PreparedCommand(scope : ResourceSet, owner : IResourceCache, key : list<obj>) =
    inherit AdaptiveObject()

    let stats : nativeptr<V2i> = NativePtr.alloc 1
    let mutable refCount = 0
    let mutable stream : VKVM.CommandStream = Unchecked.defaultof<_>
    let mutable version = 0
    let mutable initial = true
    let mutable resources = HashSet<IResourceLocation>()

    abstract member Compile : token : AdaptiveToken * stream : VKVM.CommandStream -> Command<unit>

    member x.UpdateStream(token : AdaptiveToken) =
        if initial then
            initial <- false
            let mutable state = { scope = scope; resources = HashSet(); stream = stream; stats = stats }
            let s = x.Compile(token, stream)
            s.RunUnit(&state)
            resources <- state.resources

        else
            failwith ""

    member x.DisposeStream() =
        initial <- true
        for r in resources do r.Release()
        resources.Clear()
        stream.Dispose()

    member x.Acquire() =
        if Interlocked.Increment(&refCount) = 1 then
            stream <- new VKVM.CommandStream()
            initial <- true

    member x.Release() = 
        if Interlocked.Decrement(&refCount) = 0 then
            x.DisposeStream()

    member x.ReleaseAll() =
        if refCount > 0 then
            refCount <- 0
            x.DisposeStream()

    member x.Update(token : AdaptiveToken) : ResourceInfo<VKVM.CommandStream> =
        x.EvaluateAlways token (fun token ->
            if x.OutOfDate then
                x.UpdateStream token
            { handle = stream; version = version }
        )

    interface IResourceLocation with
        member x.Acquire() = x.Acquire()
        member x.Release() = x.Release()
        member x.ReleaseAll() = x.ReleaseAll()
        member x.Update(t) = 
            let info = x.Update(t)
            { handle = info.handle :> obj; version = info.version }
        member x.Key = key
        member x.Owner = owner

    interface IResourceLocation<VKVM.CommandStream> with
        member x.Update(t) = x.Update t

type PreparedRenderObjectCommand(scope : ResourceSet, manager : ResourceManager, pass : RenderPass, ro : RenderObject, owner : IResourceCache, key : list<obj>) =
    inherit PreparedCommand(scope, owner, key)

    override x.Compile(token : AdaptiveToken, stream : VKVM.CommandStream) =
        command {
            let layout, program =
                manager.CreateShaderProgram(pass, ro.Surface)

            let inputBuffers =
                layout.PipelineInfo.pInputs
                    |> List.map (fun i ->
                        let sem = Symbol.Create i.name
                        match ro.VertexAttributes.TryGetAttribute(sem) with
                            | Some att -> i, att, false
                            | None ->
                                match ro.InstanceAttributes.TryGetAttribute(sem) with
                                    | Some att -> i, att, true
                                    | None -> failwith ""
                    )

            let bufferViews =
                inputBuffers
                    |> List.map (fun (i,view, pi) -> Symbol.Create i.name, (pi, view))
                    |> Map.ofList

            let inputState =
                manager.CreateVertexInputState(layout.PipelineInfo, Mod.constant (VertexInputState.create bufferViews))

            let inputAssembly =
                manager.CreateInputAssemblyState(ro.Mode)

            let rasterizerState =
                manager.CreateRasterizerState(ro.DepthTest, ro.CullMode, ro.FillMode)

            let colorBlendState =
                manager.CreateColorBlendState(pass, ro.WriteBuffers, ro.BlendMode)

            let depthStenicl = 
                let writeDepth =
                    match ro.WriteBuffers with
                        | Some s -> Set.contains DefaultSemantic.Depth s
                        | None -> true
                manager.CreateDepthStencilState(writeDepth, ro.DepthTest, ro.StencilMode)

            let pipeline =
                manager.CreatePipeline(
                    program,
                    pass,
                    inputState,
                    inputAssembly,
                    rasterizerState,
                    colorBlendState,
                    depthStenicl,
                    ro.WriteBuffers
                )

            let vertexBuffers =
                inputBuffers
                    |> List.map (fun (i, view, perInstance) ->
                        let buffer = manager.CreateBuffer(view.Buffer)
                        buffer, int64 view.Offset
                    )

            let vertexBufferBinding =
                manager.CreateVertexBufferBinding vertexBuffers

            let isIndexed, indexBufferBinding =
                match ro.Indices with
                    | Some i -> 
                        let buffer = manager.CreateIndexBuffer(i.Buffer)
                        let binding = manager.CreateIndexBufferBinding(buffer, VkIndexType.ofType i.ElementType)
                        true, Some binding
                    | None -> 
                        false, None

            let drawCall =
                match ro.IndirectBuffer with
                    | null ->
                        manager.CreateDrawCall(isIndexed, ro.DrawCallInfos)
                    | _ ->
                        let buffer = manager.CreateIndirectBuffer(isIndexed, ro.IndirectBuffer)
                        manager.CreateDrawCall(isIndexed, buffer)

            let isActive =
                manager.CreateIsActive(ro.IsActive)

            let descriptorSets = Array.zeroCreate layout.DescriptorSetLayouts.Length
            for si in 0 .. descriptorSets.Length - 1 do
                let setLayout = layout.DescriptorSetLayouts.[si]
                let locations, set = manager.CreateDescriptorSet(setLayout, ro.Uniforms)
                for l in locations do 
                    do! Command.Use l
                descriptorSets.[si] <- set

            let descriptorBinding =
                manager.CreateDescriptorSetBinding(layout, Array.toList descriptorSets)

            do! Command.IndirectBindPipeline pipeline
            do! Command.IndirectBindDescriptorSets descriptorBinding
            do! Command.IndirectBindVertexBuffer vertexBufferBinding
            match indexBufferBinding with
                | Some b -> do! Command.IndirectBindIndexBuffer b
                | _ -> ()
            do! Command.IndirectDraw(isActive, drawCall)
        }
//
//    override x.Update(token : AdaptiveToken, stream : VKVM.CommandStream) =
//        HSet.empty


let run() =
    let app = new VulkanApplication(false)
    let runtime = app.Runtime
    //let win = app.CreateSimpleRenderWindow(1)

    let signature =
        runtime.CreateFramebufferSignature(
            1,
            [
                DefaultSemantic.Colors, RenderbufferFormat.Rgba8
                DefaultSemantic.Depth, RenderbufferFormat.Depth24Stencil8
            ]
        )

    let size = Mod.constant (V2i(1024, 768))
    let fbo = runtime.CreateFramebuffer(signature, size)
    fbo.Acquire()

    let handle = fbo.GetValue()

    let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    let perspective = size  |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))
    let viewTrafo = Mod.constant view //DefaultCameraController.control win.Mouse win.Keyboard win.Time view

    let box = Box3d(-V3d.Half, V3d.Half)

    let cnt = 20
    let simpleThings =
        Sg.ofList [
            let size = 1.5 * float cnt
            let off = -size / 2.0
            let rand = RandomSystem()

            for x in 0 .. cnt - 1 do
                for y in 0 .. cnt - 1 do
                    let p = V2d(off, off) + V2d(x,y) * 1.5

                    let prim =
                        match rand.UniformInt(3) with
                            | 0 -> Sg.wireBox' C4b.Red box
                            | 1 -> Sg.sphere' 5 C4b.Green 0.5
                            | _ -> Sg.box' C4b.Blue box

                    yield prim |> Sg.translate p.X p.Y 0.0
        ]

    let sg = 
        simpleThings
            |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
            }

    let objects = sg.RenderObjects() |> ASet.toList

    let task = new RenderTaskNew.RenderTask(runtime.Device, unbox signature, false, false)
    
    let tokens = HashSet()

    let sw = System.Diagnostics.Stopwatch()

    //let set = CSet.empty
    //let task = runtime.CompileRender(signature, set)

    let output = OutputDescription.ofFramebuffer handle
    for i in 1 .. 4 do
        transact (fun () -> task.Clear())
        task.Run(AdaptiveToken.Top, RenderToken.Empty, output)
        transact (fun () -> for o in objects do task.Add o |> ignore)
        task.Run(AdaptiveToken.Top, RenderToken.Empty, output)

    let iter = 100000
    for i in 1 .. iter do
        transact (fun () -> task.Clear())
        task.Run(AdaptiveToken.Top, RenderToken.Empty, output)
        sw.Start()
        transact (fun () -> for o in objects do task.Add o |> ignore)
        task.Run(AdaptiveToken.Top, RenderToken.Empty, output)
        sw.Stop()
    let buildAndRender = sw.MicroTime / iter

    sw.Restart()
    for i in 1 .. iter do
        task.Run(AdaptiveToken.Top, RenderToken.Empty, output)
    sw.Stop()
    let renderTime = sw.MicroTime / iter

    let buildTime = buildAndRender - renderTime

    printfn "%A" (buildTime / (cnt * cnt))

//
//    win.Keyboard.KeyDown(Keys.X).Values.Add (fun _ ->
//        if tokens.Count > 0 then
//            let t = tokens |> Seq.head
//            tokens.Remove t |> ignore
//            transact (fun () -> task.Remove t)
//    )
//
//    win.RenderTask <- task
//
//    win.Run()

