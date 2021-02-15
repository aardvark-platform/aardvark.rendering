namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open System.Threading
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering.GL
open FSharp.Data.Adaptive

[<AllowNullLiteral>]
type Fence private(ctx : ContextHandle, handle : nativeint) =
    
    static let getContext() =
        match ContextHandle.Current with
            | ValueSome ctx -> ctx
            | ValueNone -> failwith "[GL] fence cannot be enqueued without a gc"

    static let GL_TIMEOUT_IGNORED = 0xFFFFFFFFFFFFFFFFUL

    let mutable handle = handle

    member x.Context = ctx
        
    member x.IsSignaled =
        lock x (fun () ->
            if handle = 0n then
                true
            else
                let status = GL.ClientWaitSync(handle, ClientWaitSyncFlags.None, 0L)
                status = WaitSyncStatus.AlreadySignaled
        )

    static member Create() =
        let ctx = getContext()

        // enqueue the fence
        let handle = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None)
        GL.Check "could not enqueue fence"
        if handle = 0n then Log.warn "[GL] could not enqueue fence"

        // flush to ensure that the fence will eventually be signaled
        GL.Flush()
        GL.Check "could not flush"
        
        new Fence(ctx, handle)

    member x.WaitGPU(current : ContextHandle) =
        lock x (fun () ->
            let handle = handle
            if handle <> 0n then
                if ctx <> current then 
                    GL.WaitSync(handle, WaitSyncFlags.None, GL_TIMEOUT_IGNORED) |> ignore
                    GL.Check "could not enqueue wait"

            else
                Log.warn "[GL] waiting on disposed fence"
        )

    member x.WaitGPU() =
        let ctx = getContext()
        x.WaitGPU ctx

    member x.WaitCPU() =
        lock x (fun () ->
            if handle <> 0n then
                match GL.ClientWaitSync(handle, ClientWaitSyncFlags.None, GL_TIMEOUT_IGNORED) with
                    | WaitSyncStatus.WaitFailed -> failwith "[GL] failed to wait for fence"
                    | WaitSyncStatus.TimeoutExpired -> failwith "[GL] fance timeout"
                    | _ -> ()
                GL.Check "could not wait for fence"
            else
                Log.warn "[GL] waiting on disposed fence"
        )

    member x.Dispose() = 
        lock x (fun () ->
            let o = Interlocked.Exchange(&handle, 0n)
            if o <> 0n then 
                GL.DeleteSync(o)
                GL.Check "could not delete fence"
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type FenceSet() =
    let mutable fences = HashMap.empty<ContextHandle, Fence>

    member x.Enqueue() =
        let fence = Fence.Create()
        lock x (fun () ->
            fences <-
                fences |> HashMap.update fence.Context (fun old ->
                    match old with
                        | Some old -> old.Dispose()
                        | None -> ()
                    fence
                )
        )
        
    member x.WaitCPU() =
        let waitFor = 
            lock x (fun () -> 
                let res = fences
                fences <- HashMap.empty
                res
            )

        for (_,f) in waitFor do 
            f.WaitCPU()
            f.Dispose()

    member x.WaitGPU() =
        let mine = ContextHandle.Current.Value
        lock x (fun () -> 
            for (_,f) in fences do 
                f.WaitGPU(mine)
        )

