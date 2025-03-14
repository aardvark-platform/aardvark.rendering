namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open FSharp.Data.Adaptive
open Vulkan11

module CommandResource =
    let compensation (f : unit -> unit) =
        { new ICommandResource with
            member x.AddReference() = ()
            member x.Dispose() = f() }

    let disposable (d : IDisposable) =
        { new ICommandResource with
            member x.AddReference() = ()
            member x.Dispose() = d.Dispose() }

[<AbstractClass>]
type Command() =
    static let nop =
        { new Command() with
            member x.Enqueue _ = []
            member x.Compatible = QueueFlags.All
        }

    static let barrier =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdPipelineBarrier(
                    cmd.Handle,
                    VkPipelineStageFlags.AllCommandsBit,
                    VkPipelineStageFlags.TopOfPipeBit,
                    VkDependencyFlags.None,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero
                )

                []
        }

    static let resetDeviceMask =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdSetDeviceMask(cmd.Handle, cmd.Device.AllMask)
                []
        }

    abstract member Compatible : QueueFlags
    abstract member Enqueue : CommandBuffer -> list<ICommandResource>

    member private x.EnqueueInner(buffer) =
        let res = x.Enqueue(buffer)
        buffer.AddResources(res)

    interface ICommand with
        member x.Compatible = x.Compatible
        member x.Enqueue(buffer) = x.EnqueueInner(buffer)

    static member Nop = nop

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
                member x.Compatible = QueueFlags.All
                member x.Enqueue cmd =
                    cmd.AppendCommand()
                    native {
                        let! pHandles = handles
                        VkRaw.vkCmdExecuteCommands(cmd.Handle, uint32 handles.Length, pHandles)
                    }
                    []
            }

    static member Execute(cmd : CommandBuffer) =
        Command.Execute [cmd]

    static member Barrier = barrier
    static member Reset(e : Event) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                cmd.Reset(e, VkPipelineStageFlags.BottomOfPipeBit)
                []
        }
    static member Set(e : Event) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                cmd.Set(e, VkPipelineStageFlags.BottomOfPipeBit)
                []
        }
    static member Wait(e : Event) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                cmd.WaitAll [| e |]
                []
        }
    static member Wait(e : Event, dstFlags : VkPipelineStageFlags) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                cmd.WaitAll([| e |], dstFlags)
                []
        }


    static member SetDeviceMask(mask : uint32) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdSetDeviceMask(cmd.Handle, mask)
                []
        }

    static member ResetDevicemask = resetDeviceMask

    static member PerDevice (command : int -> Command) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                if cmd.Device.AllCount = 1u then
                    command(0).Enqueue cmd
                else
                    let res = System.Collections.Generic.List<ICommandResource>()
                    for di in cmd.Device.AllIndicesArr do
                        let mask = 1u <<< int di
                        cmd.AppendCommand()
                        VkRaw.vkCmdSetDeviceMask(cmd.Handle, mask)

                        let r = command(int di).Enqueue cmd
                        res.AddRange(r)

                    cmd.AppendCommand()
                    VkRaw.vkCmdSetDeviceMask(cmd.Handle, cmd.Device.AllMask)

                    CSharpList.toList res
        }

[<AbstractClass; Sealed; Extension>]
type CommandBufferExtensions private() =
    [<Extension>]
    static member inline Enqueue(this : CommandBuffer, cmd : ICommand) =
        cmd.Enqueue(this)

[<AutoOpen>]
module CommandAPI =
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Command =
        let nop = Command.Nop

        let inline delay (f : unit -> Command) =
            f()

        let inline bind (f : unit -> Command) (m : Command) =
            let r = f()
            { new Command() with
                member x.Compatible = m.Compatible  &&& r.Compatible
                member x.Enqueue cmd =
                    let ld = m.Enqueue cmd
                    let rd = r.Enqueue cmd
                    ld @ rd
            }

        let inline append (l : Command) (r : Command) =
            { new Command() with
                member x.Compatible = l.Compatible &&& r.Compatible
                member x.Enqueue s =
                    let ld = l.Enqueue s
                    let rd = r.Enqueue s
                    ld @ rd
            }

        let inline tryFinally (m : Command) (comp : unit -> unit) =
            { new Command() with
                member x.Compatible = m.Compatible
                member x.Enqueue cmd =
                    let ld = m.Enqueue cmd
                    let rd = CommandResource.compensation comp
                    ld @ [rd]
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

    type TokenCommandBuilder(buffer : DeviceToken, fin : DeviceToken -> unit) =
        member x.Bind(m : Command, f : unit -> 'a) =
            buffer.Enqueue m
            f()

        member x.Bind(m : DeviceQueue -> 'Result, f : 'Result -> 'a) =
            let res = buffer.FlushAndPerform m
            f res

        member x.Return(v : 'a) = v

        member x.ReturnFrom(m: DeviceQueue -> 'Result) =
            buffer.FlushAndPerform m

        member x.Delay(f : unit -> 'a) = f

        member x.Combine(l : unit, r : unit -> 'a) = r()

        member x.TryFinally(m : unit -> 'a, comp : unit -> unit) =
            buffer.AddCompensation comp
            m()

        member x.Using(m : 'a, f : 'a -> 'b) =
            buffer.AddCompensation (fun () -> (m :> IDisposable).Dispose())
            f m

        member x.Zero() = ()

        member x.For(elements : seq<'a>, f : 'a -> unit) =
            for a in elements do f a

        member x.Run(f : unit -> 'a) =
            try f()
            finally fin buffer

    type SynchronousCommandBuilder(queueFamily : DeviceQueueFamily) =
        inherit CommandBuilder()
        member x.Run(cmd : Command) = queueFamily.RunSynchronously cmd

    type CommandBuffer with
        member x.enqueue = BufferCommandBuilder(x)

    type DeviceToken with
        member x.enqueue = TokenCommandBuilder(x, ignore)
        member x.perform = TokenCommandBuilder(x, fun t -> t.Flush())

    type Device with
        member x.eventually = TokenCommandBuilder(x.Token, Disposable.dispose)
        member x.perform = TokenCommandBuilder(x.Token, fun t -> t.Flush(); t.Dispose())

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
