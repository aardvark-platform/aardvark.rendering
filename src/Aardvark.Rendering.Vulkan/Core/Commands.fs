namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Vulkan11

#nowarn "9"
//// #nowarn "51"
 

[<AbstractClass>]
type Command() =
    static let nop = 
        { new Command() with 
            member x.Enqueue _ = Disposable.Empty 
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

                Disposable.Empty
        }

    static let resetDeviceMask =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdSetDeviceMask(cmd.Handle, cmd.Device.AllMask)
                Disposable.Empty
        }

    abstract member Compatible : QueueFlags
    abstract member Enqueue : CommandBuffer -> Disposable
    interface ICommand with
        member x.Compatible = x.Compatible
        member x.TryEnqueue(buffer, disp) =
            disp <- x.Enqueue(buffer)
            true

    static member Nop = nop

//    static member inline Custom (flags : QueueFlags, f : CommandBuffer -> unit) =
//        { new Command() with 
//            member x.Enqueue b = f b; null 
//            member x.Compatible = flags
//        }
//
//    static member inline Custom (flags : QueueFlags, f : CommandBuffer -> Disposable) =
//        { new Command() with 
//            member x.Enqueue b = f b 
//            member x.Compatible = flags
//        }

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
                    Disposable.Empty
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
                Disposable.Empty
        }
    static member Set(e : Event) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                cmd.Set(e, VkPipelineStageFlags.BottomOfPipeBit)  
                Disposable.Empty
        }
    static member Wait(e : Event) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                cmd.WaitAll [| e |]
                Disposable.Empty
        }
    static member Wait(e : Event, dstFlags : VkPipelineStageFlags) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                cmd.WaitAll([| e |], dstFlags)
                Disposable.Empty
        }


    static member SetDeviceMask(mask : uint32) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                cmd.AppendCommand()
                VkRaw.vkCmdSetDeviceMask(cmd.Handle, mask)
                Disposable.Empty
        }

    static member ResetDevicemask = resetDeviceMask

    static member PerDevice (command : int -> Command) =
        { new Command() with
            member x.Compatible = QueueFlags.All
            member x.Enqueue cmd =
                if cmd.Device.AllCount = 1u then
                    command(0).Enqueue cmd
                else
                    let disp = System.Collections.Generic.List<Disposable>()
                    for di in cmd.Device.AllIndicesArr do
                        let mask = 1u <<< int di
                        cmd.AppendCommand()
                        VkRaw.vkCmdSetDeviceMask(cmd.Handle, mask)

                        let d = command(int di).Enqueue cmd
                        if not (isNull d) then disp.Add d
                    
                    cmd.AppendCommand()
                    VkRaw.vkCmdSetDeviceMask(cmd.Handle, cmd.Device.AllMask)

                    if disp.Count > 0 then
                        Disposable.Compose (CSharpList.toList disp)
                    else
                        Disposable.Empty
        }

        

//
//[<AbstractClass>]
//type QueueCommand() =
//    abstract member Compatible : QueueFlags
//    abstract member Enqueue : DeviceQueue * list<Semaphore> * Option<Semaphore> * Option<Fence> -> Disposable
//    interface IQueueCommand with
//        member x.Compatible = x.Compatible
//        member x.TryEnqueue(queue, waitFor, disp, sem, fence) =
//            disp <- x.Enqueue(queue, waitFor, sem, fence)
//            true
//
//    static member Submit(cmds : list<CommandBuffer>) =
//        { new IQueueCommand with
//            member x.Compatible = QueueFlags.All
//            member x.TryEnqueue(queue, waitFor, disp, sem, fence) =
//                queue.Submit(cmds, waitFor, Option.toList sem, fence)
//                true
//        }
//
//    static member Submit(cmd : CommandBuffer) =
//        QueueCommand.Submit [cmd]
// 
//    static member Submit(cmd : Command) =
//        { new IQueueCommand with
//            member x.Compatible = QueueFlags.All
//            member x.TryEnqueue(queue, waitFor, disp, sem, fence) =
//                let pool = queue.Family.TakeCommandPool()
//                let cb = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
//                let d = cmd.Enqueue(cb)
//                disp <- Disposable.Custom (fun () -> d.Dispose(); cb.Dispose(); pool.Dispose())
//                queue.Submit([cb], waitFor, Option.toList sem, fence)
//                true
//        }       
//
//   

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
    static member RunSynchronously(this : DeviceQueueFamily, cmd : ICommand) =
        use pool = this.TakeCommandPool()
        use buffer = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
        buffer.Begin(CommandBufferUsage.OneTimeSubmit)
        CommandBufferExtensions.Enqueue(buffer, cmd)
        buffer.End()
        this.RunSynchronously(buffer)

    [<Extension>]
    static member Compile(this : CommandPool, level : CommandBufferLevel, cmd : ICommand) =
        let buffer = this.CreateCommandBuffer(level)
        buffer.Begin(CommandBufferUsage.SimultaneousUse)
        CommandBufferExtensions.Enqueue(buffer, cmd)
        buffer.End()
        buffer





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
                    Disposable.Compose(ld, rd)
            }

        let inline append (l : Command) (r : Command) =
            { new Command() with
                member x.Compatible = l.Compatible &&& r.Compatible
                member x.Enqueue s =
                    let ld = l.Enqueue s
                    let rd = r.Enqueue s
                    Disposable.Compose(ld, rd)
            }

        let inline tryFinally (m : Command) (comp : unit -> unit) =
            { new Command() with
                member x.Compatible = m.Compatible
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

    type TokenCommandBuilder(buffer : DeviceToken, fin : DeviceToken -> unit) =
        member x.Bind(m : Command, f : unit -> 'a) =
            buffer.Enqueue m
            f()

        member x.Bind(m : QueueCommand, f : unit -> 'a) =
            buffer.Enqueue m
            f()

        member x.Return(v : 'a) = v

        member x.Delay(f : unit -> 'a) = f

        member x.Combine(l : unit, r : unit -> 'a) = r()

        member x.TryFinally(m : unit -> 'a, comp : unit -> unit) =
            buffer.AddCleanup comp
            m()

        member x.Using(m : 'a, f : 'a -> 'b) =
            buffer.AddCleanup (fun () -> (m :> IDisposable).Dispose())
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

    type Device with
        member x.eventually = TokenCommandBuilder(x.Token, Disposable.dispose)
        member x.perform = TokenCommandBuilder(x.Token, fun t -> t.Sync(); t.Dispose())

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
