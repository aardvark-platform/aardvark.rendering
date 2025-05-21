namespace Aardvark.Rendering.GL

open System.Threading
open System.Collections.Concurrent
open Aardvark.Rendering
open Aardvark.Rendering.GL

type internal RefCountedBuffer(ctx, create : unit -> Buffer, destroy : unit -> unit) =
    inherit Buffer(ctx, 0n, 0)

    let mutable refCount = 0
    let mutable handle = Unchecked.defaultof<Buffer>

    override x.Name
        with get() = if refCount > 0 then handle.Name else null
        and set name = if refCount > 0 then handle.Name <- name

    member x.Acquire(name: string) =
        if Interlocked.Increment &refCount = 1 then
            let b = Operators.using ctx.ResourceLock (fun _ -> create())
            if name <> null && b.Name = null then b.Name <- name
            x.Handle <- b.Handle
            x.SizeInBytes <- b.SizeInBytes
            handle <- b

    member x.Release() =
        if Interlocked.Decrement &refCount = 0 then
            destroy()
            Operators.using ctx.ResourceLock (fun _ -> ctx.Delete handle)
            x.Handle <- 0
            x.SizeInBytes <- 0n

type internal BufferManager(ctx : Context) =
    let cache = ConcurrentDictionary<IBuffer, RefCountedBuffer>()

    let get (b : IBuffer) =
        cache.GetOrAdd(b, fun v ->
            new RefCountedBuffer(
                ctx,
                (fun () -> ctx.CreateBuffer b),
                (fun () -> cache.TryRemove b |> ignore)
            )
        )

    member x.Create(name : string, data : IBuffer) =
        let shared = get data
        shared.Acquire(name)
        shared :> Buffer

    member x.Update(name : string, b : Buffer, data : IBuffer) : Buffer =
        match b with
        | :? RefCountedBuffer as b  ->
            let newShared = get data
            if newShared = b then
                b :> Buffer
            else
                b.Release()
                newShared.Acquire(name)
                newShared :> Buffer
        | _ ->
            if b.Handle = 0 then
                x.Create(name, data)
            else
                ctx.Upload(b, data)
                b

    member x.Delete(b : Buffer) =
        if b.Handle <> 0 then
            match b with
            | :? RefCountedBuffer as b -> b.Release()
            | _ -> ctx.Delete b

    static member TryUnwrap(data : IBuffer) =
        match data with
        | :? Buffer as b -> ValueSome b
        | :? IBackendBuffer -> ValueNone
        | :? IBufferRange as r -> BufferManager.TryUnwrap r.Buffer
        | _ -> ValueNone