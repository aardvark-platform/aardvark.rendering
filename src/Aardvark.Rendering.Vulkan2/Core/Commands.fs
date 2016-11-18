namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base

#nowarn "9"
#nowarn "51"
 


[<AllowNullLiteral; AbstractClass>]
type Disposable() =
    abstract member Dispose : unit -> unit
    interface IDisposable with
        member x.Dispose() = x.Dispose()

    static member inline Empty : Disposable = null

    static member Compose(l : Disposable, r : Disposable) =
        if isNull l then r
        elif isNull r then l
        else new Composite([l; r]) :> Disposable

    static member Compose(l : list<Disposable>) =
        match List.filter (isNull >> not) l with
            | [] -> null
            | l -> new Composite(l) :> Disposable

    static member inline Custom (f : unit -> unit) =
        { new Disposable() with member x.Dispose() = f() }

    static member inline Dispose (d : Disposable) = d.Dispose()

and private Composite(l : list<Disposable>) =
    inherit Disposable()
    override x.Dispose() = l |> List.iter Disposable.Dispose

type ICommand =
    abstract member TryEnqueue : CommandBuffer * byref<Disposable> -> bool

[<AbstractClass>]
type Command() =
    static let nop = { new Command() with member x.Enqueue _ = Disposable.Empty }

    static let barrier =
        { new Command() with
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdPipelineBarrier(
                    cmd.Handle,
                    VkPipelineStageFlags.BottomOfPipeBit,
                    VkPipelineStageFlags.TopOfPipeBit,
                    VkDependencyFlags.None,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero
                )

                Disposable.Empty
        }

    abstract member Enqueue : CommandBuffer -> Disposable
    interface ICommand with
        member x.TryEnqueue(buffer, disp) =
            disp <- x.Enqueue(buffer)
            true

    static member Nop = nop

    static member inline Custom (f : CommandBuffer -> unit) =
        { new Command() with member x.Enqueue b = f b; null }

    static member inline Custom (f : CommandBuffer -> Disposable) =
        { new Command() with member x.Enqueue b = f b }

    static member Execute(cmd : seq<CommandBuffer>) =
        let handles = 
            cmd |> Seq.choose (fun cmd -> 
                if cmd.IsRecording then failf "cannot run recording CommandBuffer"
                if cmd.Level <> CommandBufferLevel.Secondary then failf "cannot execute CommandBuffer with level %A" cmd.Level

                if cmd.IsEmpty then None
                else Some cmd.Handle
               )
            |> Seq.toArray

        if handles.Length = 0 then
            Command.Nop
        else
            { new Command() with
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    handles |> NativePtr.withA (fun pHandles ->
                        VkRaw.vkCmdExecuteCommands(cmd.Handle, uint32 handles.Length, pHandles)
                    )
                    Disposable.Empty
            }

    static member Execute(cmd : CommandBuffer) =
        Command.Execute [cmd]

    static member Barrier = barrier

[<AbstractClass; Sealed; Extension>]
type CommandBufferExtensions private() =
    [<Extension>]
    static member Enqueue(this : CommandBuffer, cmd : ICommand) =
        if not this.IsRecording then
            failf "cannot enqueue commands to non-recording CommandBuffer"

        let mutable disp = null
        if cmd.TryEnqueue(this, &disp) then
            if not (isNull disp) then
                this.AddCompensation disp
        else
            failf "could not enqueue command"

    [<Extension>]
    static member RunSynchronously(this : DeviceQueue, cmd : ICommand) =
        use buffer = this.Family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        buffer.Begin(CommandBufferUsage.OneTimeSubmit)
        CommandBufferExtensions.Enqueue(buffer, cmd)
        buffer.End()
        this.RunSynchronously(buffer)
//
//    [<Extension>]
//    static member Start(this : DeviceQueue, cmd : ICommand) =
//        if not (isNull cmd) then
//            use buffer = this.Family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
//            buffer.Begin(CommandBufferUsage.OneTimeSubmit)
//            CommandBufferExtensions.Enqueue(buffer, cmd)
//            buffer.End()
//            this.Start(buffer)

    [<Extension>]
    static member Enqueue(this : DeviceToken, cmd : ICommand) =
        CommandBufferExtensions.Enqueue(this.CommandBuffer, cmd)


    [<Extension>]
    static member RunSynchronously(this : DeviceQueueFamily, cmd : ICommand) =
        use buffer = this.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        buffer.Begin(CommandBufferUsage.OneTimeSubmit)
        CommandBufferExtensions.Enqueue(buffer, cmd)
        buffer.End()
        this.RunSynchronously(buffer)




[<AutoOpen>]
module CommandAPI = 
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Command =
        let nop = Command.Nop

        let inline delay (f : unit -> Command) =
            { new Command() with
                member x.Enqueue s = f().Enqueue s
            }

        let inline bind (f : unit -> Command) (m : Command) =
            { new Command() with
                member x.Enqueue cmd =
                    let ld = m.Enqueue cmd
                    let rd = f().Enqueue cmd
                    Disposable.Compose(ld, rd)
            }

        let inline append (l : Command) (r : Command) =
            { new Command() with
                member x.Enqueue s =
                    let ld = l.Enqueue s
                    let rd = r.Enqueue s
                    Disposable.Compose(ld, rd)
            }

        let inline tryFinally (m : Command) (comp : unit -> unit) =
            { new Command() with
                member x.Enqueue cmd = 
                    let ld = m.Enqueue cmd
                    let rd = Disposable.Custom comp
                    Disposable.Compose(ld, rd)
            }

        let collect (f : 'a -> Command) (m : seq<'a>) =
            m |> Seq.fold (fun l r -> append l (f r)) nop

    type CommandBuilder() =
        member inline x.Bind(m : Command, f : unit -> Command) = Command.bind f m
        member inline x.Return(v : unit) = Command.Nop
        member inline x.Delay(f : unit -> Command) = Command.delay f
        member inline x.Combine(l : Command, r : Command) = Command.append l r
        member inline x.TryFinally(m : Command, comp : unit -> unit) = Command.tryFinally m comp
        member inline x.Zero() = Command.Nop
        member inline x.For(elements : seq<'a>, f : 'a -> Command) = Command.collect f elements

    let command = CommandBuilder()

    type BufferCommandBuilder(buffer : CommandBuffer) =
        inherit CommandBuilder()
        member x.Run(cmd : Command) = buffer.Enqueue cmd

    type TokenCommandBuilder(buffer : DeviceToken) =
        inherit CommandBuilder()
        member x.Run(cmd : Command) = buffer.CommandBuffer.Enqueue cmd


    type SynchronousCommandBuilder(queueFamily : DeviceQueueFamily) =
        inherit CommandBuilder()
        member x.Run(cmd : Command) = queueFamily.RunSynchronously cmd


    type CommandBuffer with
        member x.enqueue = BufferCommandBuilder(x)

    type DeviceToken with
        member x.enqueue = TokenCommandBuilder(x)

    type Device with
        member x.eventually = TokenCommandBuilder(x.ResourceToken)

    type DeviceQueueFamily with
        member x.run = SynchronousCommandBuilder(x)


[<AutoOpen>]
module ``Memory Commands`` =
    
    type Command with

        static member ExecuteSequential (cmds : list<CommandBuffer>) =
            match cmds with
                | [] -> Command.Nop
                | first :: cmds ->
                    command {
                        do! Command.Execute first
                        for cmd in cmds do
                            do! Command.Barrier
                            do! Command.Execute cmd
                    }


        static member Copy (src : DevicePtr, srcOffset : int64, dst : DevicePtr, dstOffset : int64, size : int64) =
            if size < 0L || srcOffset < 0L || srcOffset + size > src.Size || dstOffset < 0L || dstOffset + size > dst.Size then
                failf "bad copy range"

            { new Command() with
                member x.Enqueue cmd =
                    let mutable srcBuffer = VkBuffer.Null
                    let mutable dstBuffer = VkBuffer.Null
                    let device = src.Memory.Heap.Device
                    let align = device.MinUniformBufferOffsetAlignment

                    let srcOffset = src.Offset + srcOffset
                    let srcBufferOffset = Alignment.prev align srcOffset
                    let srcCopyOffset = srcOffset - srcBufferOffset
                    let srcBufferSize = size + srcCopyOffset

                    let mutable srcInfo =
                        VkBufferCreateInfo(
                            VkStructureType.BufferCreateInfo, 0n,
                            VkBufferCreateFlags.None,
                            uint64 srcBufferSize, VkBufferUsageFlags.TransferSrcBit, VkSharingMode.Exclusive, 
                            0u, NativePtr.zero
                    )

                    VkRaw.vkCreateBuffer(device.Handle, &&srcInfo, NativePtr.zero, &&srcBuffer)
                        |> check "could not create temporary buffer"

                    VkRaw.vkBindBufferMemory(device.Handle, srcBuffer, src.Memory.Handle, uint64 srcBufferOffset)
                        |> check "could not bind temporary buffer memory"


                    let dstOffset = dst.Offset + dstOffset
                    let dstBufferOffset = Alignment.prev align dstOffset
                    let dstCopyOffset = dstOffset - dstBufferOffset
                    let dstBufferSize = size + dstCopyOffset


                    let mutable dstInfo =
                        VkBufferCreateInfo(
                            VkStructureType.BufferCreateInfo, 0n,
                            VkBufferCreateFlags.None,
                            uint64 dstBufferSize, VkBufferUsageFlags.TransferDstBit, VkSharingMode.Exclusive, 
                            0u, NativePtr.zero
                    )

                    VkRaw.vkCreateBuffer(device.Handle, &&dstInfo, NativePtr.zero, &&dstBuffer)
                        |> check "could not create temporary buffer"

                    VkRaw.vkBindBufferMemory(device.Handle, dstBuffer, dst.Memory.Handle, uint64 dstBufferOffset)
                        |> check "could not bind temporary buffer memory"


                    let mutable copyInfo = VkBufferCopy(uint64 srcCopyOffset, uint64 dstCopyOffset, uint64 size)
                    cmd.AppendCommand()
                    VkRaw.vkCmdCopyBuffer(cmd.Handle, srcBuffer, dstBuffer, 1u, &&copyInfo)

                    { new Disposable() with
                        member x.Dispose() =
                            if srcBuffer.IsValid then VkRaw.vkDestroyBuffer(device.Handle, srcBuffer, NativePtr.zero)
                            if dstBuffer.IsValid then VkRaw.vkDestroyBuffer(device.Handle, dstBuffer, NativePtr.zero)
                    }
            }

        static member Copy(src : DevicePtr, dst : DevicePtr, size : int64) =
            Command.Copy(src, 0L, dst, 0L, size)

        static member Copy(src : DevicePtr, dst : DevicePtr) =
            Command.Copy(src, 0L, dst, 0L, min src.Size dst.Size)