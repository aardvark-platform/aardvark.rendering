namespace Aardvark.Rendering.GL

open System.Threading
open System.Collections.Concurrent
open Aardvark.Rendering
open Aardvark.Rendering.GL
open FSharp.Data.Adaptive

type internal RefCountedBuffer(ctx, create : unit -> Buffer, destroy : unit -> unit) =
    inherit Buffer(ctx, 0n, 0)

    let mutable refCount = 0
    let mutable handle = Unchecked.defaultof<_>

    member x.Acquire() =
        if Interlocked.Increment &refCount = 1 then
            let b = Operators.using ctx.ResourceLock (fun _ -> create())
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

    member x.Create(data : IBuffer) =
        let shared = get data
        shared.Acquire()
        shared :> Buffer

    member x.Update(b : Buffer, data : IBuffer) : Buffer =
        match b with
        | :? RefCountedBuffer as b  ->
            let newShared = get data
            if newShared = b then
                b :> Buffer
            else
                newShared.Acquire()
                b.Release()
                newShared :> Buffer
        | _ ->
            if b.Handle = 0 then
                x.Create(data)
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
        | :? IBufferRange as r -> BufferManager.TryUnwrap r.Buffer
        | _ -> ValueNone