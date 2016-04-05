namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System.Runtime.CompilerServices

type CommandState = 
    { 
        buffer : CommandBuffer
        cleanupActions : list<unit -> unit>
    }

[<AbstractClass>]
type Command<'a>() = 
    abstract member Run : byref<CommandState> -> unit
    default x.Run s = ()

    abstract member GetResult : CommandState -> 'a
    default x.GetResult(s) = Unchecked.defaultof<_>


type BarrierKind =
    | MemoryTransfer
    | Global
    


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Command =

    
    let ofValue (v : 'a) =
        { new Command<'a>() with
             member x.GetResult(_) = v 
        }

    let ofFunction (v : unit -> 'a) =
        { new Command<'a>() with
             member x.GetResult(_) = v()
        }

    let map (f : 'a -> 'b) (m : Command<'a>) =
        { new Command<'b>() with 
            member x.Run(s) = m.Run(&s)
            member x.GetResult(s) = m.GetResult(s) |> f
        }

    let combine (l : Command<unit>) (r : Command<'a>) =
        { new Command<'a>() with
            member x.Run(s) =
                l.Run(&s)
                r.Run(&s)

            member x.GetResult(s) =
                r.GetResult(s)
        }

    let ofSeq (commands : seq<Command<unit>>) =
        { new Command<unit>() with
            member x.Run(s) =
                for c in commands do c.Run(&s)
        }

    let custom (f : CommandState -> CommandState) =
        { new Command<unit>() with
            member x.Run(s) =
                s <- f s
        }

    let onCleanup (f : unit -> unit) =
        { new Command<unit>() with
            member x.Run(s) =
                s <- { s with cleanupActions = f :: s.cleanupActions }
        }


    let syncTransfer =
        { new Command<unit>() with
            member x.Run(s) =
                let b = s.buffer

                VkRaw.vkCmdPipelineBarrier(
                    b.Handle,
                    VkPipelineStageFlags.TransferBit,
                    VkPipelineStageFlags.TransferBit,
                    VkDependencyFlags.None,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero
                )

        }

    let sync =
        { new Command<unit>() with
            member x.Run(s) =
                let b = s.buffer

                VkRaw.vkCmdPipelineBarrier(
                    b.Handle,
                    VkPipelineStageFlags.AllCommandsBit,
                    VkPipelineStageFlags.AllCommandsBit,
                    VkDependencyFlags.None,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero
                )

        }

    let syncMem =
        { new Command<unit>() with
            member x.Run(s) =
                let b = s.buffer

                let mutable mem =
                    VkMemoryBarrier(
                        VkStructureType.MemoryBarrier,
                        0n,
                        VkAccessFlags.TransferWriteBit,
                        VkAccessFlags.TransferReadBit
                    )

                VkRaw.vkCmdPipelineBarrier(
                    b.Handle,
                    VkPipelineStageFlags.None,
                    VkPipelineStageFlags.None,
                    VkDependencyFlags.None,
                    1u, &&mem,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero
                )

        }

    let barrier (k : BarrierKind) =
        match k with
            | Global -> sync
            | MemoryTransfer -> syncMem

[<AutoOpen>]
module ``Command Builder`` =
    type CommandBuilder() =
        member x.Bind(m : Command<unit>, f : unit -> Command<'b>) = 
            let mutable res = Unchecked.defaultof<_>
            Command.combine m { 
                new Command<'b>() with 
                    member x.Run(s) = 
                        res <- f()
                        res.Run(&s) 
                    member x.GetResult(s) =
                        res.GetResult(s)
            }
//
//        member x.Bind(m : Command<unit>, f : unit -> unit -> 'b) = 
//            Command.combine m (Command.ofFunction (fun () -> f()()))

        member x.Return(v : 'a) = Command.ofValue v
        member x.ReturnFrom(v : unit -> 'a) = 
            { new Command<'a>() with
                member x.GetResult(s) = v()
            }

        
        member x.ReturnFrom(v : Command<'a>) = v


        member x.Zero() = Command.ofValue ()
        member x.Delay(f : unit -> unit -> 'a) = 
            { new Command<'a>() with 
                member x.GetResult(s) = f()() 
            }
        member x.Delay(f : unit -> Command<'a>) = 
            let mutable res = Unchecked.defaultof<_>
            { new Command<'a>() with 
                member x.Run(s) = 
                    res <- f()
                    res.Run(&s) 
                member x.GetResult(s) =
                    res.GetResult(s)
            
            }
        member x.Combine(l : Command<unit>, r : Command<'a>) = Command.combine l r
        member x.For(elements : seq<'a>, f : 'a -> Command<unit>) = elements |> Seq.map f |> Command.ofSeq

        member x.TryWith(m : Command<'a>, handler : exn -> Command<'a>) =
            let mutable res = m
            { new Command<'a>() with
                member x.Run(s) =
                    try 
                        let mutable e = s
                        m.Run(&e)
                        s <- e
                    with e ->
                        let alt = handler e
                        res <- alt
                        alt.Run(&s)

                member x.GetResult(s) =
                    res.GetResult(s)
            }

        member x.TryFinally(m : Command<'a>, f : unit -> unit) =
            { new Command<'a>() with
                member x.Run(s) = 
                    m.Run(&s)
                    s <- { s with cleanupActions = f :: s.cleanupActions }

                member x.GetResult(s) = m.GetResult(s)
            }

    let command = CommandBuilder()


[<AbstractClass; Sealed; Extension>]
type CommandExtensions private() =

    [<Extension>]
    static member RunSynchronously(this : Command<'a>, queue : Queue) =
        CommandExtensions.RunSynchronously(queue, this)

    [<Extension>]
    static member StartAsTask(this : Command<'a>, queue : Queue) =
        CommandExtensions.StartAsTask(queue, this)

    [<Extension>]
    static member Run(this : Command<'a>, queue : Queue) =
        CommandExtensions.Run(queue, this)


    [<Extension>]
    static member Run(this : Queue, pool : CommandPool, cmd : Command<'a>) =
        async {
            let buffer = pool.CreateCommandBuffer()
            buffer.Begin(false)
            let mutable state = { buffer = buffer; cleanupActions = [] }
            cmd.Run(&state)
            buffer.End()

            do! this.Submit [|state.buffer|]
           
            VkRaw.vkQueueWaitIdle(this.Handle) |> check "vkQueueWaitIdle"
            buffer.Dispose()

            let rec clean (l : list<unit -> unit>) =
                match l with
                    | [] -> ()
                    | h::t ->
                        clean t
                        h()

            clean state.cleanupActions
            let res = cmd.GetResult(state)
            return res
        }

    [<Extension>]
    static member RunSynchronously(this : Queue, pool : CommandPool, cmd : Command<'a>) =
        CommandExtensions.Run(this, pool, cmd) |> Async.RunSynchronously

    [<Extension>]
    static member StartAsTask(this : Queue, pool : CommandPool, cmd : Command<'a>) =
        CommandExtensions.Run(this, pool, cmd) |> Async.StartAsTask


    [<Extension>]
    static member Run(this : Queue, cmd : Command<'a>) =
        CommandExtensions.Run(this, this.CommandPool, cmd)

    [<Extension>]
    static member RunSynchronously(this : Queue, cmd : Command<'a>) =
        CommandExtensions.RunSynchronously(this, this.CommandPool, cmd)

    [<Extension>]
    static member StartAsTask(this : Queue, cmd : Command<'a>) =
        CommandExtensions.StartAsTask(this, this.CommandPool, cmd)
