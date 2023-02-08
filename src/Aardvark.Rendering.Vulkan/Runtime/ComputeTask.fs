namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Monads.State
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive

module internal ComputeTaskInternals =

    type CompilerState =
        {
            downloads    : list<Buffer * HostMemory>
            uploads      : list<HostMemory * Buffer>
            usedImages   : HashSet<Image>
            imageLayouts : HashMap<Image, VkImageLayout>
        }

    type ICompiledTask =
        abstract member State : CompilerState
        abstract member Stream : VKVM.CommandStream

    module private HostMemory =

        let pinned (f : nativeint -> 'T) = function
            | HostMemory.Unmanaged ptr -> f ptr
            | HostMemory.Managed (arr, index) ->
                let offset = nativeint index * nativeint (Marshal.SizeOf (arr.GetType().GetElementType()))
                pinned arr (fun ptr -> f (ptr + offset))

    [<AutoOpen>]
    module private Compiler =

        type VkImageLayout with
            static member Default = VkImageLayout.ShaderReadOnlyOptimal

        module CompilerState =

            let inline private checkRange (totalSize : int64) (start : nativeint) (size : nativeint) =
                if start < 0n then raise <| ArgumentException("[Buffer] start of subrange must not be negative.")
                if size < 0n then raise <| ArgumentException("[Buffer] size of subrange must not be negative.")

                if int64 start + int64 size > totalSize then
                    let max = start + size - 1n
                    raise <| ArgumentException($"[Buffer] subrange [{start}, {max}] out of bounds (max size = {totalSize}).")

            let empty =
                { uploads      = []
                  downloads    = []
                  usedImages   = HashSet.empty
                  imageLayouts = HashMap.empty }

            let execute (other : ICompiledTask) =
                State.modify (fun s ->
                    let o = other.State
                    { s with
                        uploads = s.uploads @ o.uploads
                        downloads = s.downloads @ o.downloads
                    }
                )

            let inline tempDownloadBuffer (src : Buffer) (srcOffset : nativeint) (dst : HostMemory) (size : nativeint) =
                checkRange src.Size srcOffset size

                state {
                    let temp = src.Device.HostMemory.CreateBuffer(VkBufferUsageFlags.TransferDstBit, int64 size)
                    do! State.modify (fun s -> { s with downloads = (temp, dst) :: s.downloads })
                    return temp
                }

            let inline tempUploadBuffer (src : HostMemory) (dst : Buffer) (dstOffset : nativeint) (size : nativeint) =
                checkRange dst.Size dstOffset size

                state {
                    let temp = dst.Device.HostMemory.CreateBuffer(VkBufferUsageFlags.TransferSrcBit, int64 size)
                    do! State.modify (fun s -> { s with uploads = (src, temp) :: s.uploads })
                    return temp
                }

            let inline useImage (image : Image) =
                State.modify (fun s -> { s with usedImages = s.usedImages |> HashSet.add image })

            let inline layout (image : Image) =
                State.get |> State.map (fun s ->
                    s.imageLayouts
                    |> HashMap.tryFind image
                    |> Option.defaultValue VkImageLayout.ShaderReadOnlyOptimal
                )

            let inline private setLayout (layout : VkImageLayout) (image : Image) =
                State.modify (fun s -> { s with imageLayouts = HashMap.add image layout s.imageLayouts })

            let inline transformLayout (image : Image) (newLayout : VkImageLayout) =
                state {
                    let! oldLayout = layout image

                    if oldLayout <> newLayout then
                        do! useImage image
                        do! setLayout newLayout image

                    return oldLayout
                }

        type VKVM.CommandStream with

            member x.Sync(buffer : Buffer, srcAccess : VkAccessFlags, dstAccess : VkAccessFlags) =
                let supportedStages =
                    VkPipelineStageFlags.ofQueueFlags QueueFlags.Compute

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

            member x.Sync(img : ImageSubresourceRange, layout : VkImageLayout, srcAccess : VkAccessFlags, dstAccess : VkAccessFlags) =
                let supportedStages =
                    VkPipelineStageFlags.ofQueueFlags QueueFlags.Compute

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

        let private compileS (stream : VKVM.CommandStream) (cmd : ComputeCommand) : State<CompilerState, unit> =
            state {
                match cmd with
                | ComputeCommand.BindCmd shader ->
                    let program = unbox<ComputeProgram> shader
                    stream.BindPipeline(VkPipelineBindPoint.Compute, program.Pipeline) |> ignore

                | ComputeCommand.SetInputCmd input ->
                    let input = unbox<ComputeInputBinding> input
                    stream.IndirectBindDescriptorSets(input.Binding.Pointer) |> ignore

                | ComputeCommand.DispatchCmd groups ->
                    stream.Dispatch(uint32 groups.X, uint32 groups.Y, uint32 groups.Z) |> ignore

                | ComputeCommand.ExecuteCmd other ->
                    let other = unbox<ICompiledTask> other
                    do! CompilerState.execute other

                    for image in other.State.usedImages do
                        let! layout = CompilerState.transformLayout image VkImageLayout.Default
                        stream.TransformLayout(image, layout, VkImageLayout.Default)

                    stream.Call(other.Stream) |> ignore

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

                    stream.CopyBuffer(srcBuffer.Handle, dstBuffer.Handle, regions) |> ignore

                | ComputeCommand.DownloadBufferCmd (src, dst) ->
                    let srcBuffer = src.Buffer |> unbox<Buffer>
                    let! temp = CompilerState.tempDownloadBuffer srcBuffer src.Offset dst src.SizeInBytes
                    stream.CopyBuffer(srcBuffer.Handle, temp.Handle, [| VkBufferCopy(uint64 src.Offset, 0UL, uint64 src.SizeInBytes) |]) |> ignore

                | ComputeCommand.UploadBufferCmd (src, dst) ->
                    let dstBuffer = dst.Buffer |> unbox<Buffer>
                    let! temp = CompilerState.tempUploadBuffer src dstBuffer dst.Offset dst.SizeInBytes
                    stream.CopyBuffer(temp.Handle, dstBuffer.Handle, [| VkBufferCopy(0UL, uint64 dst.Offset, uint64 dst.SizeInBytes) |]) |> ignore

                | ComputeCommand.SetBufferCmd (range, pattern) ->
                    let buffer = unbox<Buffer> range.Buffer
                    let value = BitConverter.ToUInt32(pattern, 0)
                    stream.FillBuffer(buffer.Handle, uint64 range.Offset, uint64 range.SizeInBytes, value) |> ignore

                | ComputeCommand.SyncBufferCmd (buffer, srcAccess, dstAccess) ->
                    let buffer = unbox<Buffer> buffer
                    let srcAccess = VkAccessFlags.ofResourceAccess srcAccess
                    let dstAccess = VkAccessFlags.ofResourceAccess dstAccess
                    stream.Sync(buffer, srcAccess, dstAccess) |> ignore

                | ComputeCommand.CopyImageCmd (src, srcOffset, dst, dstOffset, size) ->
                    let src = ImageSubresourceLayers.ofFramebufferOutput src
                    let dst = ImageSubresourceLayers.ofFramebufferOutput dst
                    do! CompilerState.useImage src.Image
                    do! CompilerState.useImage dst.Image

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

                        stream.TransformLayout(
                            image.[range.Aspect, range.Levels.Min .. range.Levels.Max, range.Slices.Min .. range.Slices.Max],
                            VkImageLayout.ofTextureLayout srcLayout,
                            VkImageLayout.ofTextureLayout dstLayout
                        )

                | ComputeCommand.TransformLayoutCmd (image, layout) ->
                    let image = unbox<Image> image
                    let newLayout = VkImageLayout.ofTextureLayout layout
                    let! oldLayout = CompilerState.transformLayout image newLayout
                    stream.TransformLayout(image, oldLayout, newLayout)

                | ComputeCommand.SyncImageCmd (image, srcAccess, dstAccess) ->
                    let image = unbox<Image> image
                    do! CompilerState.useImage image
                    let! layout = CompilerState.layout image
                    let srcAccess = VkAccessFlags.ofResourceAccess srcAccess
                    let dstAccess = VkAccessFlags.ofResourceAccess dstAccess
                    let aspect = TextureFormat.toAspect image.TextureFormat
                    stream.Sync(image.[aspect], layout, srcAccess, dstAccess) |> ignore
            }

        let compile (stream : VKVM.CommandStream) (cmds : seq<ComputeCommand>) =
            let mutable state = CompilerState.empty

            for cmd in cmds do
                let c = compileS stream cmd
                c.Run(&state)

            for (image, layout) in state.imageLayouts do
                stream.TransformLayout(image, layout, VkImageLayout.Default)

            state

    type CompiledCommands(manager : ResourceManager, resources : ResourceLocationSet, commands : alist<ComputeCommand>) =
        inherit AdaptiveObject()

        let stream = new VKVM.CommandStream()
        let mutable state = CompilerState.empty

        let reader = commands.GetReader()
        let inputs = Dict<Index, ComputeInputBinding>()
        let mutable commandList = IndexList<ComputeCommand>.Empty

        let addInput (index : Index) = function
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

            | cmd -> cmd

        let removeInput (index : Index) =
            match inputs.TryRemove(index) with
            | (true, input) -> resources.Remove input.Binding
            | _ -> ()

        let addCommands (deltas : IndexListDelta<ComputeCommand>) =
            for (index, op) in deltas do
                match op with
                | Set cmd ->
                    let cmd = cmd |> addInput index
                    commandList <- commandList |> IndexList.set index cmd

                | _ -> ()

        let removeCommands (deltas : IndexListDelta<ComputeCommand>) =
            for (index, op) in deltas do
                match op with
                | Remove ->
                    removeInput index
                    commandList <- commandList |> IndexList.remove index

                | _ -> ()

        let clear() =
            stream.Clear()

            for (_, buffer) in state.uploads do
                buffer.Dispose()

            for (buffer, _) in state.downloads do
                buffer.Dispose()

            state <- CompilerState.empty

        member x.State = state
        member x.Stream = stream
        member x.IsEmpty = commandList.IsEmpty

        member x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token false (fun token ->
                let deltas = reader.GetChanges(token)
                addCommands deltas
                removeCommands deltas

                clear()
                state <- commandList |> ComputeCommand.compile stream

                true
            )

        member x.Upload() =
            for (src, dst) in state.uploads do
                src |> HostMemory.pinned (fun pSrc ->
                    Buffer.upload pSrc dst 0n (nativeint dst.Size)
                )

        member x.Download() =
            for (src, dst) in state.downloads do
                dst |> HostMemory.pinned (fun pDst ->
                    Buffer.download src 0n pDst (nativeint src.Size)
                )

        member x.Dispose() =
            for KeyValue(_, input) in inputs do
                resources.Remove input.Binding

            clear()
            stream.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()


type internal ComputeTask(manager : ResourceManager, commands : alist<ComputeCommand>) =
    inherit AdaptiveObject()

    let device = manager.Device

    // Use graphics queue family if it supports compute (which is guaranteed by Vulkan spec).
    // Otherwise, we need queue family ownership transfers which are not implemented.
    // On NVIDIA GPUs we can get away with using a dedicated compute family without any ownership transfers.
    let family, getDeviceToken =
        if device.GraphicsFamily.Flags.HasFlag QueueFlags.Compute then
            device.GraphicsFamily, fun () -> device.Token
        else
            device.ComputeFamily, fun () -> device.ComputeToken

    let supportsGraphics = family.Flags.HasFlag QueueFlags.Graphics
    let pool = family.CreateCommandPool(CommandPoolFlags.ResetBuffer)
    let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
    let inner = pool.CreateCommandBuffer(CommandBufferLevel.Secondary)

    let resources = ResourceLocationSet()
    let compiled = new ComputeTaskInternals.CompiledCommands(manager, resources, commands)

    let updateCommands (token : AdaptiveToken) =
        compiled.Update(token)

    let updateResources (token : AdaptiveToken) (renderToken : RenderToken) (action : bool -> 'T) =
        resources.Use(token, renderToken, fun changed ->
            action changed
        )

    member x.Update(token : AdaptiveToken, renderToken : RenderToken) =
        use __ = getDeviceToken()
        updateCommands token |> ignore
        updateResources token renderToken ignore

    member x.Run(token : AdaptiveToken, renderToken : RenderToken) =
        x.EvaluateAlways token (fun t ->
            use __ = renderToken.Use()

            use tt = getDeviceToken()

            let commandChanged =
                updateCommands t

            updateResources t renderToken (fun resourcesChanged ->
                if commandChanged || resourcesChanged then
                    if device.DebugConfig.PrintRenderTaskRecompile then
                        let cause =
                            String.concat "; " [
                                if commandChanged then yield "content"
                                if resourcesChanged then yield "resources"
                            ]
                            |> sprintf "{ %s }"

                        Log.line "[Compute] recompile commands: %s" cause

                    inner.Begin CommandBufferUsage.None

                    if not compiled.IsEmpty then
                        inner.AppendCommand()
                        compiled.Stream.Run(inner.Handle)

                    inner.End()

                tt.Sync()

                let vulkanQueries = renderToken.Query.ToVulkanQuery(onlyTimeQueries = not supportsGraphics)
                cmd.Begin CommandBufferUsage.OneTimeSubmit

                for q in vulkanQueries do
                    q.Begin cmd

                cmd.enqueue {
                    do! Command.Execute inner
                }

                for q in vulkanQueries do
                    q.End cmd

                cmd.End()

                compiled.Upload()
                family.RunSynchronously(cmd)
                compiled.Download()
            )
        )

    member x.Dispose() =
        transact (fun () ->
            compiled.Dispose()
            inner.Dispose()
            cmd.Dispose()
            pool.Dispose()
        )

    interface IComputeTask with
        member x.Runtime = device.Runtime
        member x.Update(token, renderToken) = x.Update(token, renderToken)
        member x.Run(token, renderToken) = x.Run(token, renderToken)

    interface ComputeTaskInternals.ICompiledTask with
        member x.State = compiled.State
        member x.Stream = compiled.Stream

    interface IDisposable with
        member x.Dispose() = x.Dispose()