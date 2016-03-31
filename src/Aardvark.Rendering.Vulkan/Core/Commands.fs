namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices

type CommandState = 
    { 
        buffer : CommandBuffer
        cleanupActions : list<unit -> unit>
    }

[<AbstractClass>]
type Command<'a>() = 
    abstract member Run : byref<CommandState> -> 'a
    abstract member RunUnit : byref<CommandState> -> unit

    default x.Run(s) = x.RunUnit(&s); Unchecked.defaultof<_>
    default x.RunUnit(s) = x.Run(&s) |> ignore


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Command =

    
    let ofValue (v : 'a) =
        { new Command<'a>() with member x.Run(_) = v }

    let map (f : 'a -> 'b) (m : Command<'a>) =
        { new Command<'b>() with member x.Run(s) = m.Run(&s) |> f }

    let bind (f : 'a -> Command<'b>) (m : Command<'a>) =
        { new Command<'b>() with member x.Run(s) = (m.Run(&s) |> f).Run(&s) }

    let combine (l : Command<unit>) (r : Command<'a>) =
        { new Command<'a>() with
            member x.Run(s) =
                l.Run(&s)
                r.Run(&s)
        }

    let ofSeq (commands : seq<Command<unit>>) =
        { new Command<unit>() with
            member x.RunUnit(s) =
                for c in commands do c.Run(&s)
                Unchecked.defaultof<unit>
        }

    let custom (f : CommandState -> CommandState * 'a) =
        { new Command<'a>() with
            member x.Run(s) =
                let (n,v) = f s
                s <- n
                v
        }

    let run (m : Command<'a>) (s : CommandState) =
        let mutable s = s
        let v = m.Run(&s)
        s, v

    let rununit (m : Command<unit>) (s : CommandState) =
        let mutable s = s
        m.RunUnit(&s)
        s


[<AutoOpen>]
module ``Command Builder`` =
    type CommandBuilder() =
        member x.Bind(m : Command<'a>, f : 'a -> Command<'b>) = Command.bind f m
        member x.Return(v : 'a) = Command.ofValue v
        member x.Zero() = Command.ofValue ()
        member x.Delay(f : unit -> Command<'a>) = { new Command<'a>() with member x.Run(s) = f().Run(&s) }
        member x.Combine(l : Command<unit>, r : Command<'a>) = Command.combine l r
        member x.For(elements : seq<'a>, f : 'a -> Command<unit>) = elements |> Seq.map f |> Command.ofSeq

    let command = CommandBuilder()


[<AbstractClass; Sealed; Extension>]
type CommandExtensions private() =

    [<Extension>]
    static member Run(this : Queue, pool : CommandPool, cmd : Command<unit>) =
        async {
            let buffer = pool.CreateCommandBuffer()
            buffer.Begin(false)
            let mutable state = { buffer = buffer; cleanupActions = [] }
            cmd.RunUnit(&state)
            buffer.End()

            do! this.Submit [|state.buffer|]
           
            VkRaw.vkQueueWaitIdle(this.Handle) |> check "vkQueueWaitIdle"
            buffer.Dispose()
            for a in state.cleanupActions do a()
        }

    [<Extension>]
    static member RunSynchronously(this : Queue, pool : CommandPool, cmd : Command<unit>) =
        CommandExtensions.Run(this, pool, cmd) |> Async.RunSynchronously

    [<Extension>]
    static member StartAsTask(this : Queue, pool : CommandPool, cmd : Command<unit>) =
        CommandExtensions.Run(this, pool, cmd) |> Async.StartAsTask

    [<Extension>]
    static member Run(this : Queue, cmd : Command<unit>) =
        CommandExtensions.Run(this, this.Device.DefaultCommandPool, cmd)

    [<Extension>]
    static member RunSynchronously(this : Queue, cmd : Command<unit>) =
        CommandExtensions.RunSynchronously(this, this.Device.DefaultCommandPool, cmd)

    [<Extension>]
    static member StartAsTask(this : Queue, cmd : Command<unit>) =
        CommandExtensions.StartAsTask(this, this.Device.DefaultCommandPool, cmd)