namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open FSharp.NativeInterop
open System
open System.Runtime.CompilerServices
open Vulkan11
open EXTDebugUtils

#nowarn "9"
#nowarn "51"

/// Represents a command that can be enqueued in a command buffer.
[<AbstractClass>]
type Command() =
    abstract member Compatible : QueueFlags
    abstract member Enqueue : CommandBuffer -> unit

[<AbstractClass; Sealed; Extension>]
type CommandExtensions() =

    [<Extension>]
    static member Enqueue(token: DeviceToken, command: Command) =
        command.Enqueue(token.CurrentBuffer)

    [<Extension>]
    static member inline RunSynchronously(family: DeviceQueueFamily, command: Command) =
        use token = family.CurrentToken
        token.Enqueue command
        token.Sync()

    [<Extension>]
    static member StartTask(family: DeviceQueueFamily, command: Command) =
        use token = family.CurrentToken
        token.Enqueue command
        token.Flush()

    [<Extension>]
    static member inline Enqueue(buffer: CommandBuffer, command: Command) =
        command.Enqueue(buffer)

[<AutoOpen>]
module ``Common Commands`` =

    let private nop =
        { new Command() with
            member x.Enqueue _ = ()
            member x.Compatible = QueueFlags.All
        }

    let private barrier =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue buffer =
                buffer.AppendCommand()
                VkRaw.vkCmdPipelineBarrier(
                    buffer.Handle,
                    VkPipelineStageFlags.AllCommandsBit,
                    VkPipelineStageFlags.TopOfPipeBit,
                    VkDependencyFlags.None,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero
                )
        }

    let private resetDeviceMask =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue buffer =
                buffer.AppendCommand()
                VkRaw.vkCmdSetDeviceMask(buffer.Handle, buffer.Device.PhysicalDevice.DeviceMask)
        }

    let inline private canExecute (buffer: CommandBuffer) =
        if buffer.IsRecording then failf "cannot run recording CommandBuffer"
        if buffer.Level <> CommandBufferLevel.Secondary then failf "cannot execute CommandBuffer with level %A" buffer.Level
        not buffer.IsEmpty

    type Command with
        static member Nop = nop
        static member Barrier = barrier

        static member Execute(inner: CommandBuffer[]) =
            let handles =
                inner |> Array.choose (fun buffer ->
                    if canExecute buffer then Some buffer.Handle
                    else None
                )

            if handles.Length = 0 then
                Command.Nop
            else
                { new Command() with
                    member x.Compatible = QueueFlags.All
                    member x.Enqueue buffer =
                        buffer.AppendCommand()
                        use pHandles = fixed handles
                        VkRaw.vkCmdExecuteCommands(buffer.Handle, uint32 handles.Length, pHandles)
                }

        static member Execute(inner: CommandBuffer) =
            if canExecute inner then
                { new Command() with
                    member x.Compatible = QueueFlags.All
                    member x.Enqueue buffer =
                        buffer.AppendCommand()
                        let pHandle = NativePtr.stackalloc 1
                        pHandle.[0] <- inner.Handle
                        VkRaw.vkCmdExecuteCommands(buffer.Handle, 1u, pHandle)
                }
            else
                Command.Nop

        static member Reset(event: Event) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue buffer =
                    buffer.AppendCommand()
                    buffer.Reset(event, VkPipelineStageFlags.BottomOfPipeBit)
            }

        static member Set(event: Event) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue buffer =
                    buffer.AppendCommand()
                    buffer.Set(event, VkPipelineStageFlags.BottomOfPipeBit)
            }

        static member Wait(event: Event) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    cmd.WaitAll [| event |]
            }

        static member Wait(event: Event, dstStageFlags: VkPipelineStageFlags) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue buffer =
                    buffer.AppendCommand()
                    buffer.WaitAll([| event |], dstStageFlags)
        }

        static member SetDeviceMask(mask: uint32) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue buffer =
                    buffer.AppendCommand()
                    VkRaw.vkCmdSetDeviceMask(buffer.Handle, mask)
            }

        static member ResetDevicemask = resetDeviceMask

        static member PerDevice (command: int -> Command) =
            { new Command() with
                member x.Compatible = QueueFlags.All
                member x.Enqueue buffer =
                    if not buffer.Device.IsDeviceGroup then
                        command(0).Enqueue buffer
                    else
                        for di in buffer.Device.PhysicalDeviceGroup.AllIndicesArr do
                            let mask = 1u <<< int di
                            buffer.AppendCommand()
                            VkRaw.vkCmdSetDeviceMask(buffer.Handle, mask)
                            command(int di).Enqueue buffer

                        buffer.AppendCommand()
                        VkRaw.vkCmdSetDeviceMask(buffer.Handle, buffer.Device.PhysicalDevice.DeviceMask)
            }

        static member InsertLabel (name: string, color: C4f) =
            { new Command() with
                member _.Compatible = QueueFlags.All
                member _.Enqueue buffer =
                    if buffer.DeviceInterface.Instance.DebugLabelsEnabled then
                        buffer.AppendCommand()
                        CStr.using name (fun pName ->
                            let mutable labelInfo = VkDebugUtilsLabelEXT(pName, v4f color)
                            VkRaw.vkCmdInsertDebugUtilsLabelEXT(buffer.Handle, &&labelInfo)
                        )
            }

        static member BeginLabel (name: string, color: C4f) =
            { new Command() with
                member _.Compatible = QueueFlags.All
                member _.Enqueue buffer =
                    if buffer.DeviceInterface.Instance.DebugLabelsEnabled then
                        buffer.AppendCommand()
                        CStr.using name (fun pName ->
                            let mutable labelInfo = VkDebugUtilsLabelEXT(pName, v4f color)
                            VkRaw.vkCmdBeginDebugUtilsLabelEXT(buffer.Handle, &&labelInfo)
                        )
            }

        static member EndLabel() =
            { new Command() with
                member _.Compatible = QueueFlags.All
                member _.Enqueue buffer =
                    if buffer.DeviceInterface.Instance.DebugLabelsEnabled then
                        buffer.AppendCommand()
                        VkRaw.vkCmdEndDebugUtilsLabelEXT(buffer.Handle)
            }

[<AutoOpen>]
module ``Command Builders`` =

    module Command =

        let inline append (l: Command) (r: Command) =
            { new Command() with
                member x.Compatible = l.Compatible &&& r.Compatible
                member x.Enqueue buffer =
                    l.Enqueue buffer
                    r.Enqueue buffer
            }

        let inline tryFinally (compensation: unit -> unit) (cmd: Command) =
            { new Command() with
                member x.Compatible = cmd.Compatible
                member x.Enqueue buffer =
                    buffer.Enqueue cmd
                    buffer.AddCompensation compensation
            }

        let inline collect (mapping: 'T -> Command) (elements: seq<'T>) =
            let cmds = ResizeArray<Command>()
            for e in elements do cmds.Add (mapping e)
            { new Command() with
                member x.Compatible =
                    (QueueFlags.All, cmds) ||> Seq.fold (fun s c -> s &&& c.Compatible)
                member x.Enqueue buffer =
                    for c in cmds do c.Enqueue buffer
            }

    type CommandBuilder() =
        member inline x.Combine(l, r) = Command.append l r
        member inline x.Bind(cmd, func) = x.Combine(cmd, func())
        member inline x.Return(_: unit) = Command.Nop
        member inline x.Delay(func: unit -> Command) = func()
        member inline x.TryFinally(cmd, compensation) = Command.tryFinally compensation cmd
        member inline x.Zero() = Command.Nop
        member inline x.For(elements, mapping) = Command.collect mapping elements

    let command = CommandBuilder()

    type BufferCommandBuilder(buffer: CommandBuffer) =
        inherit CommandBuilder()
        member x.Run(cmd: Command) = cmd.Enqueue buffer

    type TokenCommandBuilder(token: DeviceToken, fin: DeviceToken -> unit) =
        member x.Bind(cmd: Command, func: unit -> 'T) =
            token.Enqueue cmd
            func()

        member x.Bind(action: DeviceQueue -> 'Result, func: 'Result -> 'T) =
            let res = token.Sync action
            func res

        member inline x.Return(value: 'T) = value

        member x.ReturnFrom(action: DeviceQueue -> 'Result) =
            token.Sync action

        member inline x.Delay(func: unit -> 'T) = func

        member inline x.Combine(_: unit, r: unit -> 'T) = r()

        member x.TryFinally(func: unit -> 'T, compensation: unit -> unit) =
            token.AddCompensation compensation
            func()

        member x.Using(resource: #IDisposable, expr: 'T -> 'U) =
            token.AddCompensation resource
            expr resource

        member inline x.Zero() = ()

        member inline x.For(elements: 'T seq, func: 'T -> unit) =
            for a in elements do func a

        member x.Run(func: unit -> 'T) =
            try func()
            finally fin token

    type SynchronousCommandBuilder(queueFamily : DeviceQueueFamily) =
        inherit CommandBuilder()
        member x.Run(cmd: Command) = queueFamily.RunSynchronously cmd

    type CommandBuffer with
        member x.enqueue = BufferCommandBuilder(x)

    type DeviceToken with
        member x.enqueue = TokenCommandBuilder(x, ignore)
        member x.perform = TokenCommandBuilder(x, _.Sync())

    type Device with
        member x.eventually = TokenCommandBuilder(x.Token, Disposable.dispose)
        member x.perform = TokenCommandBuilder(x.Token, fun t -> t.Sync(); t.Dispose())

    type DeviceQueueFamily with
        member x.run = SynchronousCommandBuilder(x)