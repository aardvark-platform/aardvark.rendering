namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Monads.State
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive

module internal ComputeTaskInternals =

    type ComputeInputBinding =
        {
            Program : ComputeProgram
            Binding : INativeResourceLocation<DescriptorSetBinding>
        }

        interface IComputeInputBinding with
            member x.Shader = x.Program

    type ResourceManager with

        member x.CreateComputeInputBinding(program : ComputeProgram, inputs : IUniformProvider) =
            let provider = UniformProvider.computeInputs inputs

            let binding =
                let sets = x.CreateDescriptorSets(program.PipelineLayout, provider)
                x.CreateDescriptorSetBinding(VkPipelineBindPoint.Compute, program.PipelineLayout, sets)

            { Program = program
              Binding = binding }


    [<RequireQualifiedAccess>]
    type private HostCommand =
       | Execute  of task: IComputeTask
       | Upload   of src: HostMemory * dst: Buffer * dstOffset: nativeint * size: nativeint
       | Download of src: Buffer * srcOffset: nativeint * dst: HostMemory * size: nativeint

    [<RequireQualifiedAccess>]
    type private CompiledCommand =
        | Host   of cmd: HostCommand
        | Device of stream: VKVM.CommandStream

        member x.Dispose() =
            match x with
            | Device stream -> stream.Dispose()
            | _ -> ()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type private CompilerState =
        {
            Commands     : CompiledCommand list
            UsedImages   : HashSet<Image>
            ImageLayouts : HashMap<Image, VkImageLayout>
        }

    type private ICompiledTask =
        abstract member State : CompilerState

    [<AutoOpen>]
    module private Utilities =

        type Array with
            member x.ElementSize = nativeint (Marshal.SizeOf (x.GetType().GetElementType()))

        module HostMemory =

            let pin (sizeInBytes : nativeint) (f : nativeint -> nativeint -> 'T) = function
                | HostMemory.Unmanaged ptr -> f ptr sizeInBytes
                | HostMemory.Managed (arr, index) ->
                    let elementSize = arr.ElementSize
                    let offset = nativeint index * elementSize

                    let sizeInBytes =
                        let length = arr.Length - index
                        min sizeInBytes (nativeint length * elementSize)

                    pinned arr (fun ptr -> f (ptr + offset) sizeInBytes)

    [<AutoOpen>]
    module private Compiler =

        module private CompilerState =

            let empty =
                { Commands     = []
                  UsedImages   = HashSet.empty
                  ImageLayouts = HashMap.empty }

            let stream =
                State.custom (fun s ->
                    match s.Commands with
                    | (CompiledCommand.Device stream)::_ -> s, stream
                    | _ ->
                        let stream = new VKVM.CommandStream()
                        let cmd = CompiledCommand.Device stream
                        { s with Commands = cmd :: s.Commands }, stream
                )

            let private hostCommand (cmd : HostCommand) =
                State.modify (fun s ->
                    { s with Commands = (CompiledCommand.Host cmd) :: s.Commands }
                )

            let inline execute (other : IComputeTask) =
                hostCommand (HostCommand.Execute other)

            let inline downloadBuffer (src : Buffer) (srcOffset : nativeint) (dst : HostMemory) (size : nativeint) =
                let cmd = HostCommand.Download (src, srcOffset, dst, size)
                hostCommand cmd

            let inline uploadBuffer (src : HostMemory) (dst : Buffer) (dstOffset : nativeint) (size : nativeint) =
                let cmd = HostCommand.Upload (src, dst, dstOffset, size)
                hostCommand cmd

            let inline useImage (image : Image) =
                State.modify (fun s -> { s with UsedImages = s.UsedImages |> HashSet.add image })

            let usedImages =
                State.get |> State.map (fun s -> s.UsedImages)

            let inline layout (image : Image) =
                State.get |> State.map (fun s ->
                    s.ImageLayouts
                    |> HashMap.tryFind image
                    |> Option.defaultValue VkImageLayout.ShaderReadOnlyOptimal
                )

            let inline private setLayout (layout : VkImageLayout) (image : Image) =
                State.modify (fun s -> { s with ImageLayouts = HashMap.add image layout s.ImageLayouts })

            let inline transformLayout (image : Image) (newLayout : VkImageLayout) =
                state {
                    let! oldLayout = layout image

                    if oldLayout <> newLayout then
                        do! useImage image
                        do! setLayout newLayout image

                    return oldLayout
                }

        [<AutoOpen>]
        module private CommandStreamExtensions =

            type VKVM.CommandStream with
                member x.Sync(buffer : Buffer, srcAccess : VkAccessFlags, dstAccess : VkAccessFlags, queueFlags : QueueFlags) =
                    let supportedStages =
                        VkPipelineStageFlags.ofQueueFlags queueFlags

                    let srcStage, srcAccess =
                        let stage = VkBufferUsageFlags.toSrcStageFlags buffer.Usage
                        (stage, srcAccess) ||> filterSrcStageAndAccess supportedStages

                    let dstStage, dstAccess =
                        let stage = VkBufferUsageFlags.toDstStageFlags buffer.Usage
                        (stage, dstAccess) ||> filterDstStageAndAccess supportedStages

                    let barrier =
                        VkBufferMemoryBarrier(
                            srcAccess, dstAccess,
                            VkQueueFamilyIgnored, VkQueueFamilyIgnored,
                            buffer.Handle, 0UL, uint64 buffer.Size
                        )

                    x.PipelineBarrier(
                        srcStage, dstStage,
                        [||], [| barrier |], [||]
                    )

                member x.Sync(img : ImageSubresourceRange, layout : VkImageLayout, srcAccess : VkAccessFlags, dstAccess : VkAccessFlags, queueFlags : QueueFlags) =
                    let supportedStages =
                        VkPipelineStageFlags.ofQueueFlags queueFlags

                    let srcStage, srcAccess =
                        let stage = VkImageLayout.toSrcStageFlags layout
                        (stage, srcAccess) ||> filterSrcStageAndAccess supportedStages

                    let dstStage, dstAccess =
                        let stage = VkImageLayout.toDstStageFlags layout
                        (stage, dstAccess) ||> filterDstStageAndAccess supportedStages

                    x.PipelineBarrier(
                        srcStage, dstStage,
                        [||], [||],
                        [|
                            VkImageMemoryBarrier(
                                srcAccess, dstAccess,
                                layout, layout,
                                VkQueueFamilyIgnored,
                                VkQueueFamilyIgnored,
                                img.Image.Handle,
                                img.VkImageSubresourceRange
                            )
                        |]
                    )

                member x.TransformLayout(img : ImageSubresourceRange, source : VkImageLayout, target : VkImageLayout) =
                    if source <> target then
                        let supportedStages =
                            VkPipelineStageFlags.ofQueueFlags QueueFlags.Compute

                        let srcStage, srcAccess =
                            let stage = VkImageLayout.toSrcStageFlags source
                            let access = VkImageLayout.toSrcAccessFlags source
                            (stage, access) ||> filterSrcStageAndAccess supportedStages

                        let dstStage, dstAccess =
                            let stage = VkImageLayout.toDstStageFlags target
                            let access = VkImageLayout.toDstAccessFlags target
                            (stage, access) ||> filterDstStageAndAccess supportedStages

                        let barrier =
                            VkImageMemoryBarrier(
                                srcAccess, dstAccess,
                                source, target,
                                VkQueueFamilyIgnored, VkQueueFamilyIgnored,
                                img.Image.Handle, img.VkImageSubresourceRange
                            )

                        x.PipelineBarrier(
                            srcStage, dstStage,
                            [||], [||], [| barrier |]
                        ) |> ignore

                member x.TransformLayout(img : Image, source : VkImageLayout, target : VkImageLayout) =
                    x.TransformLayout(
                        img.[img.TextureFormat.Aspect, *, *],
                        source, target
                    )

        module private ComputeCommand =

            let private restoreLayouts (images : HashSet<Image>) : State<CompilerState, unit> =
                state {
                    for image in images do
                        let! layout = CompilerState.transformLayout image VkImageLayout.ShaderReadOnlyOptimal

                        if layout <> VkImageLayout.ShaderReadOnlyOptimal then
                            let! stream = CompilerState.stream
                            stream.TransformLayout(image, layout, VkImageLayout.ShaderReadOnlyOptimal)
                }

            let private compileS (queueFlags : QueueFlags) (cmd : ComputeCommand) : State<CompilerState, unit> =
                state {
                    match cmd with
                    | ComputeCommand.BindCmd shader ->
                        let program = unbox<ComputeProgram> shader
                        let! stream = CompilerState.stream
                        stream.BindPipeline(VkPipelineBindPoint.Compute, program.Pipeline) |> ignore

                    | ComputeCommand.SetInputCmd input ->
                        let input = unbox<ComputeInputBinding> input
                        let! stream = CompilerState.stream
                        stream.IndirectBindDescriptorSets(input.Binding.Pointer) |> ignore

                    | ComputeCommand.DispatchCmd groups ->
                        let! stream = CompilerState.stream
                        stream.Dispatch(uint32 groups.X, uint32 groups.Y, uint32 groups.Z) |> ignore

                    | ComputeCommand.ExecuteCmd other ->
                        let compiled = unbox<ICompiledTask> other
                        do! restoreLayouts compiled.State.UsedImages
                        do! CompilerState.execute other

                    | ComputeCommand.CopyBufferCmd (src, dst) ->
                        let srcBuffer = src.Buffer |> unbox<Buffer>
                        let dstBuffer = dst.Buffer |> unbox<Buffer>

                        let regions =
                            [|
                                VkBufferCopy(
                                    uint64 src.Offset,
                                    uint64 dst.Offset,
                                    uint64 (min src.SizeInBytes dst.SizeInBytes)
                                )
                            |]

                        let! stream = CompilerState.stream
                        stream.CopyBuffer(srcBuffer.Handle, dstBuffer.Handle, regions) |> ignore

                    | ComputeCommand.DownloadBufferCmd (src, dst) ->
                        let srcBuffer = src.Buffer |> unbox<Buffer>
                        do! CompilerState.downloadBuffer srcBuffer src.Offset dst src.SizeInBytes

                    | ComputeCommand.UploadBufferCmd (src, dst) ->
                        let dstBuffer = dst.Buffer |> unbox<Buffer>
                        do! CompilerState.uploadBuffer src dstBuffer dst.Offset dst.SizeInBytes

                    | ComputeCommand.SetBufferCmd (range, value) ->
                        let buffer = unbox<Buffer> range.Buffer
                        let! stream = CompilerState.stream
                        stream.FillBuffer(buffer.Handle, uint64 range.Offset, uint64 range.SizeInBytes, value) |> ignore

                    | ComputeCommand.SyncBufferCmd (buffer, srcAccess, dstAccess) ->
                        let buffer = unbox<Buffer> buffer
                        let srcAccess = VkAccessFlags.ofResourceAccess srcAccess
                        let dstAccess = VkAccessFlags.ofResourceAccess dstAccess
                        let! stream = CompilerState.stream
                        stream.Sync(buffer, srcAccess, dstAccess, queueFlags) |> ignore

                    | ComputeCommand.CopyImageCmd (src, srcOffset, dst, dstOffset, size) ->
                        let src = ImageSubresourceLayers.ofFramebufferOutput src
                        let dst = ImageSubresourceLayers.ofFramebufferOutput dst
                        do! CompilerState.useImage src.Image
                        do! CompilerState.useImage dst.Image

                        let srcOffset =
                            if src.Image.IsCubeOr2D then
                                V3i(srcOffset.X, src.Size.Y - (srcOffset.Y + size.Y), srcOffset.Z)
                            else
                                srcOffset

                        let dstOffset =
                            if dst.Image.IsCubeOr2D then
                                V3i(dstOffset.X, dst.Size.Y - (dstOffset.Y + size.Y), dstOffset.Z)
                            else
                                dstOffset

                        let! stream = CompilerState.stream
                        stream.CopyImage(
                            src.Image.Handle, VkImageLayout.TransferSrcOptimal,
                            dst.Image.Handle, VkImageLayout.TransferDstOptimal,
                            [|
                                VkImageCopy(
                                    src.VkImageSubresourceLayers,
                                    VkOffset3D(srcOffset.X, srcOffset.Y, srcOffset.Z),
                                    dst.VkImageSubresourceLayers,
                                    VkOffset3D(dstOffset.X, dstOffset.Y, dstOffset.Z),
                                    VkExtent3D(size.X, size.Y, size.Z)
                                )
                            |]
                        ) |> ignore

                    | ComputeCommand.TransformSubLayoutCmd (range, srcLayout, dstLayout) ->
                        let image = unbox<Image> range.Texture

                        if srcLayout <> dstLayout then
                            do! CompilerState.useImage image

                            let! stream = CompilerState.stream
                            stream.TransformLayout(
                                image.[range.Aspect, range.Levels.Min .. range.Levels.Max, range.Slices.Min .. range.Slices.Max],
                                VkImageLayout.ofTextureLayout srcLayout,
                                VkImageLayout.ofTextureLayout dstLayout
                            )

                    | ComputeCommand.TransformLayoutCmd (image, layout) ->
                        let image = unbox<Image> image
                        let newLayout = VkImageLayout.ofTextureLayout layout
                        let! oldLayout = CompilerState.transformLayout image newLayout
                        let! stream = CompilerState.stream
                        stream.TransformLayout(image, oldLayout, newLayout)

                    | ComputeCommand.SyncImageCmd (image, srcAccess, dstAccess) ->
                        let image = unbox<Image> image
                        do! CompilerState.useImage image
                        let! layout = CompilerState.layout image
                        let srcAccess = VkAccessFlags.ofResourceAccess srcAccess
                        let dstAccess = VkAccessFlags.ofResourceAccess dstAccess
                        let aspect = TextureFormat.toAspect image.TextureFormat
                        let! stream = CompilerState.stream
                        stream.Sync(image.[aspect], layout, srcAccess, dstAccess, queueFlags) |> ignore
                }

            let private epilogue : State<CompilerState, unit> =
                state {
                    let! images = CompilerState.usedImages
                    do! restoreLayouts images
                }

            let compile (queueFlags : QueueFlags) (cmds : seq<ComputeCommand>) =
                let mutable state = CompilerState.empty

                for cmd in cmds do
                    let c = compileS queueFlags cmd
                    c.Run(&state)

                epilogue.Run(&state)

                { state with Commands = List.rev state.Commands }

        type CommandCompiler(owner : IAdaptiveObject, queueFlags : QueueFlags, manager : ResourceManager, resources : ResourceLocationSet, input : alist<ComputeCommand>) =
            let reader = input.GetReader()
            let inputs = Dict<Index, ComputeInputBinding>()
            let nested = Dict<Index, IComputeTask>()
            let mutable commands = IndexList<ComputeCommand>.Empty
            let mutable compiled = CompilerState.empty

            let add (index : Index) (command : ComputeCommand) =
                match command with
                | ComputeCommand.SetInputCmd input ->
                    let input =
                        match input with
                        | :? ComputeInputBinding as input ->
                            input

                        | :? MutableComputeInputBinding as binding ->
                            let program = unbox<ComputeProgram> binding.Shader
                            manager.CreateComputeInputBinding(program, binding)

                        | _ ->
                            failf "unknown input binding type %A" (input.GetType())

                    resources.Add input.Binding
                    inputs.[index] <- input

                    ComputeCommand.SetInputCmd input

                | ComputeCommand.ExecuteCmd other ->
                    nested.[index] <- other
                    command

                | _ -> command

            let remove (removedInputs : System.Collections.Generic.List<_>) (index : Index) =
                match inputs.TryRemove(index) with
                | (true, input) -> removedInputs.Add input
                | _ ->
                    match nested.TryRemove(index) with
                    | (true, other) -> other.Outputs.Remove owner |> ignore
                    | _ -> ()

            member x.State =
                compiled

            member x.Update(token : AdaptiveToken) =
                let deltas = reader.GetChanges(token)
                let removedInputs = System.Collections.Generic.List<ComputeInputBinding>()

                // Process deltas
                for (index, op) in deltas do
                    remove removedInputs index

                    match op with
                    | Set cmd ->
                        let cmd = cmd |> add index
                        commands <- commands |> IndexList.set index cmd

                    | Remove ->
                        commands <- commands |> IndexList.remove index

                // Delay removing inputs from the resource set
                // This way nothing will be released if the input just moved in the command list
                for input in removedInputs do
                    resources.Remove input.Binding

                // Compile updated command list
                if deltas.Count > 0 then
                    compiled.Commands |> List.iter (fun c -> c.Dispose())
                    compiled <- ComputeCommand.compile queueFlags commands
                    true
                else
                    false

            member x.Dispose() =
                for KeyValue(_, input) in inputs do
                    resources.Remove input.Binding
                inputs.Clear()

                for KeyValue(_, task) in nested do
                    task.Outputs.Remove owner |> ignore
                nested.Clear()

                commands <- IndexList.empty
                compiled.Commands |> List.iter (fun c -> c.Dispose())
                compiled <- CompilerState.empty

            interface IDisposable with
                member x.Dispose() = x.Dispose()


    type private IPreparedCommand =
        abstract member Run : primary: CommandBuffer * token: AdaptiveToken * renderToken: RenderToken -> unit

    module private PreparedCommand =

        type private UploadCmd(src : HostMemory, dst : Buffer, dstOffset : nativeint, size : nativeint) =
            member x.Run() =
                src |> HostMemory.pin size (fun pSrc size ->
                    Buffer.upload pSrc dst dstOffset size
                )

            interface IPreparedCommand with
                member x.Run(_, _, _) = x.Run()

        type private DownloadCmd(src : Buffer, srcOffset : nativeint, dst : HostMemory, size : nativeint) =
            member x.Run() =
                dst |> HostMemory.pin size (fun pDst size ->
                    Buffer.download src srcOffset pDst size
                )

            interface IPreparedCommand with
                member x.Run(_, _, _) = x.Run()

        type private ExecuteCmd(task : IComputeTask) =
            interface IPreparedCommand with
                member x.Run(_, t, rt) = task.Run(t, rt)

        type private DeviceCmd(inner : CommandBuffer) =
            let family = inner.QueueFamily
            let hasGraphics = family.Flags.HasFlag QueueFlags.Graphics

            member x.Run(primary : CommandBuffer, renderToken : RenderToken) =
                let vulkanQueries = renderToken.GetVulkanQueries(onlyTimeQueries = not hasGraphics)
                primary.Begin CommandBufferUsage.OneTimeSubmit

                for q in vulkanQueries do
                    q.Begin primary

                primary.Enqueue(
                    Command.Execute inner
                )

                for q in vulkanQueries do
                    q.End primary

                primary.End()

                family.RunSynchronously(primary)

            interface IPreparedCommand with
                member x.Run(p, _, rt) = x.Run(p, rt)

        let private empty =
            { new IPreparedCommand with
                member x.Run(_,_, _) = () }

        let ofCompiled (getSecondaryCommandBuffer : unit -> CommandBuffer) = function
            | CompiledCommand.Host (HostCommand.Upload (src, dst, dstOffset, size)) ->
                new UploadCmd(src, dst, dstOffset, size) :> IPreparedCommand

            | CompiledCommand.Host (HostCommand.Download (src, srcOffset, dst, size)) ->
                new DownloadCmd(src, srcOffset, dst, size) :> IPreparedCommand

            | CompiledCommand.Host (HostCommand.Execute task) ->
                new ExecuteCmd(task) :> IPreparedCommand

            | CompiledCommand.Device stream ->
                if stream.IsEmpty then
                    empty
                else
                    let inner = getSecondaryCommandBuffer()
                    inner.Begin CommandBufferUsage.None
                    inner.AppendCommand()
                    stream.Run(inner.Handle)
                    inner.End()

                    new DeviceCmd(inner) :> IPreparedCommand

    type ComputeTask(manager : ResourceManager, input : alist<ComputeCommand>) as this =
        inherit AdaptiveObject()

        let device = manager.Device
        let resources = ResourceLocationSet()

        // Use graphics queue family if it supports compute (which is guaranteed by Vulkan spec).
        // Otherwise, we need queue family ownership transfers which are not implemented.
        // On NVIDIA GPUs we can get away with using a dedicated compute family without any ownership transfers.
        let family, getDeviceToken =
            if device.GraphicsFamily.Flags.HasFlag QueueFlags.Compute then
                device.GraphicsFamily, fun () -> device.Token
            else
                device.ComputeFamily, fun () -> device.ComputeToken

        let pool = family.CreateCommandPool(CommandPoolFlags.ResetBuffer)
        let primary = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
        let secondary = System.Collections.Generic.List<CommandBuffer>()
        let secondaryAvailable = System.Collections.Generic.Queue<CommandBuffer>()

        let compiler = new CommandCompiler(this, family.Flags, manager, resources, input)
        let prepared = System.Collections.Generic.List<IPreparedCommand>()

        let getSecondaryCommandBuffer() =
            if secondaryAvailable.Count > 0 then
                secondaryAvailable.Dequeue()
            else
                let cmd = pool.CreateCommandBuffer CommandBufferLevel.Secondary
                secondary.Add cmd
                cmd

        let updateCommandsAndResources (token : AdaptiveToken) (renderToken : RenderToken) (action : unit -> 'T) =
            use __ = renderToken.Use()

            use tt = getDeviceToken()

            let commandChanged =
                compiler.Update(token)

            resources.Use(token, renderToken, fun resourcesChanged ->
                if commandChanged || resourcesChanged then
                    if device.DebugConfig.PrintRenderTaskRecompile then
                        let cause =
                            String.concat "; " [
                                if commandChanged then yield "content"
                                if resourcesChanged then yield "resources"
                            ]
                            |> sprintf "{ %s }"

                        Log.line "[Compute] recompile commands: %s" cause

                    prepared.Clear()
                    secondaryAvailable.Clear()

                    for cmd in secondary do
                        secondaryAvailable.Enqueue cmd

                    for c in compiler.State.Commands do
                        let p = c |> PreparedCommand.ofCompiled getSecondaryCommandBuffer
                        prepared.Add p

                tt.Sync()

                action()
            )

        member x.Update(token : AdaptiveToken, renderToken : RenderToken) =
            x.EvaluateIfNeeded token () (fun token ->
                updateCommandsAndResources token renderToken ignore
            )

        member x.Run(token : AdaptiveToken, renderToken : RenderToken) =
            x.EvaluateAlways token (fun token ->
                updateCommandsAndResources token renderToken (fun _ ->
                    for p in prepared do
                        p.Run(primary, token, renderToken)
                )
            )

        member x.Dispose() =
            lock x (fun _ ->
                prepared.Clear()
                compiler.Dispose()
                primary.Dispose()
                pool.Dispose()
            )

        interface IComputeTask with
            member x.Runtime = device.Runtime
            member x.Update(token, renderToken) = x.Update(token, renderToken)
            member x.Run(token, renderToken) = x.Run(token, renderToken)

        interface ICompiledTask with
            member x.State = compiler.State

        interface IDisposable with
            member x.Dispose() = x.Dispose()