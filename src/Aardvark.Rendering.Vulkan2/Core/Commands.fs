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
 

[<AbstractClass>]
type AbstractCommand() =
    abstract member TryEnqueue : CommandBuffer -> bool
    abstract member Dispose : unit -> unit
    default x.Dispose() = ()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AbstractClass>]
type AbstractCommand<'a>() =
    inherit AbstractCommand()
    abstract member Value : 'a
    default x.Value = Unchecked.defaultof<'a>

[<AbstractClass>]
type Command<'a>() =
    inherit AbstractCommand<'a>()

    abstract member Enqueue : CommandBuffer -> unit
    override x.TryEnqueue b =
        x.Enqueue b
        true

[<AbstractClass>]
type Command() =
    inherit Command<unit>()
    static member Ignore(cmd : Command<'a>) =
        { new Command() with
            member x.Enqueue b = cmd.Enqueue b
            member x.Dispose() = cmd.Dispose()
        }

[<AbstractClass; Sealed; Extension>]
type CommandBufferExtensions private() =
    [<Extension>]
    static member Enqueue(this : CommandBuffer, cmd : AbstractCommand) =
        if not this.IsRecording then
            failf "cannot enqueue commands to non-recording CommandBuffer"

        if cmd.TryEnqueue this then
            this.AddCompensation cmd
        else
            failf "could not enqueue command"

    [<Extension>]
    static member RunSynchronously(this : DeviceQueue, cmd : AbstractCommand) =
        use buffer = this.Family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        buffer.Begin(CommandBufferUsage.OneTimeSubmit)
        CommandBufferExtensions.Enqueue(buffer, cmd)
        buffer.End()
        this.RunSynchronously(buffer)

    [<Extension>]
    static member Start(this : DeviceQueue, cmd : AbstractCommand) =
        use buffer = this.Family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        buffer.Begin(CommandBufferUsage.OneTimeSubmit)
        CommandBufferExtensions.Enqueue(buffer, cmd)
        buffer.End()
        this.Start(buffer)

    [<Extension>]
    static member Enqueue(this : DeviceToken, cmd : AbstractCommand) =
        CommandBufferExtensions.Enqueue(this.CommandBuffer, cmd)


    [<Extension>]
    static member RunSynchronously(this : DeviceQueueFamily, cmd : AbstractCommand) =
        use buffer = this.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        buffer.Begin(CommandBufferUsage.OneTimeSubmit)
        CommandBufferExtensions.Enqueue(buffer, cmd)
        buffer.End()
        this.RunSynchronously(buffer)


[<AutoOpen>]
module CommandAPI = 
    type CommandBuilder() =
        member x.Bind(m : Command<'a>, f : 'a -> Command<'b>) =
            let res = f m.Value
            { new Command<'b>() with
                member x.Value =
                    res.Value

                member x.Enqueue stream =
                    m.Enqueue stream
                    res.Enqueue stream

                member x.Dispose() =
                    m.Dispose()
                    res.Dispose()
            }

        member x.Return(v : 'a) =
            { new Command<'a>() with
                member x.Value = v
                member x.Enqueue _ = ()
                member x.Dispose() = ()
            }

        member x.Delay(f : unit -> Command<'a>) =
            let inner = lazy (f())
            { new Command<'a>() with
                member x.Value = inner.Value.Value
                member x.Enqueue s = inner.Value.Enqueue s
                member x.Dispose() =
                    if inner.IsValueCreated then
                        inner.Value.Dispose()
                
            }

        member x.Combine(l : Command<unit>, r : Command<'a>) =
            { new Command<'a>() with
                member x.Value = r.Value
                member x.Enqueue s =
                    l.Enqueue s
                    r.Enqueue s
                member x.Dispose() =
                    l.Dispose()
                    r.Dispose()
            }

        member x.TryFinally(m : Command<'a>, comp : unit -> unit) =
            { new Command<'a>() with
                member x.Value = m.Value
                member x.Enqueue cmd = m.Enqueue cmd
                member x.Dispose() =
                    m.Dispose()
                    comp()
            }

        member x.Zero() = x.Return(())

        member x.For(elements : seq<'a>, f : 'a -> Command<unit>) : Command<unit> =
            let seen = List<IDisposable>()
            { new Command<unit>() with
                member x.Enqueue s =
                    for e in elements do
                        let i = f(e)
                        i.Enqueue s
                        seen.Add i
                member x.Dispose() =
                    for s in seen do s.Dispose()
                    seen.Clear()
            }

    let command = CommandBuilder()

    type BufferCommandBuilder(buffer : CommandBuffer) =
        inherit CommandBuilder()

        member x.Run(cmd : Command<'a>) =
            buffer.Enqueue cmd

    type TokenCommandBuilder(buffer : DeviceToken) =
        inherit CommandBuilder()

        member x.Run(cmd : Command<'a>) =
            buffer.CommandBuffer.Enqueue cmd

    type CommandBuffer with
        member x.enqueue = BufferCommandBuilder(x)

    type DeviceToken with
        member x.enqueue = TokenCommandBuilder(x)


[<AutoOpen>]
module ``Memory Commands`` =
    
    type Command with
        static member Copy (src : DevicePtr, srcOffset : int64, dst : DevicePtr, dstOffset : int64, size : int64) =
            if size < 0L || srcOffset < 0L || srcOffset + size > src.Size || dstOffset < 0L || dstOffset + size > dst.Size then
                failf "bad copy range"

            let mutable srcBuffer = VkBuffer.Null
            let mutable dstBuffer = VkBuffer.Null
            let device = src.Memory.Heap.Device
            { new Command<unit>() with
                member x.Enqueue cmd =
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
                    VkRaw.vkCmdCopyBuffer(cmd.Handle, srcBuffer, dstBuffer, 1u, &&copyInfo)

                member x.Dispose() =
                    if srcBuffer.IsValid then VkRaw.vkDestroyBuffer(device.Handle, srcBuffer, NativePtr.zero)
                    if dstBuffer.IsValid then VkRaw.vkDestroyBuffer(device.Handle, dstBuffer, NativePtr.zero)
            }

        static member Copy(src : DevicePtr, dst : DevicePtr, size : int64) =
            Command.Copy(src, 0L, dst, 0L, size)

        static member Copy(src : DevicePtr, dst : DevicePtr) =
            Command.Copy(src, 0L, dst, 0L, min src.Size dst.Size)