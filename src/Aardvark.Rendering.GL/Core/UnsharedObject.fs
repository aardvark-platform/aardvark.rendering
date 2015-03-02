namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

/// <summary>
/// UnsharedObject serves as base-class for all OpenGl resources that
/// are not shared between different contexts. These include for example
/// VertexArrayObjects. Since these resources must be (re-)created for
/// each concrete ContextHandle the must be augmented with a creation- and
/// destruction-function
/// </summary>
[<AbstractClass>]
type UnsharedObject(context : Context, createHandle : ContextHandle -> int, destroyHandle : int -> unit) =
    let mutable isLive = 1
    let mutable handleCache = new ThreadLocal<Option<ContextHandle * int>>(fun () -> None)
    let handles = ConcurrentDictionary<ContextHandle, int>()
    let mutable createHandle = createHandle
    
    /// <summary>
    /// gets or creates the resource's handle for the current context.
    /// NOTE: returns 0 if no context is current.
    /// </summary>
    let handle() =
        if isLive = 0 then
            raise <| OpenGLException(ErrorCode.InvalidOperation, "cannot use disposed VertexArrayObject")
        else
            match ContextHandle.Current with
                | Some ctx -> 
                    match handleCache.Value with
                        | Some (c,h) when c = ctx ->
                            h
                        | _ -> 
                            let handle = handles.GetOrAdd(ctx, createHandle)
                            handleCache.Value <- Some(ctx, handle)
                            handle
                        
                | None -> 0

    /// <summary>
    /// destroys all handles created and prevents new
    /// ones from being created.
    /// </summary>
    member internal x.DestroyHandles () =
        let wasLive = Interlocked.Exchange(&isLive, 0)

        if wasLive = 1 then
            handleCache.Dispose()

            let register (h : ContextHandle) =
                h.OnMakeCurrent(fun () ->
                    match handles.TryRemove h with
                        | (true, real) -> destroyHandle real
                        | _ -> ()
                )

            match ContextHandle.Current with
                | Some h -> 
                    match handles.TryRemove h with
                        | (true, real) -> destroyHandle real
                        | _ -> ()
                | None -> ()

            let handles = handles.Keys |> Seq.toList
            handles |> List.iter register


    /// <summary>
    /// destroys all handles created and installs a new creation-function
    /// </summary>
    member internal x.Update (create : ContextHandle -> int) =
        if isLive = 1 then
            
            let old = Interlocked.Exchange(&handleCache, new ThreadLocal<Option<ContextHandle * int>>(fun () -> None))
            old.Dispose()
            createHandle <- create

            let register (h : ContextHandle, real : int) =
                h.OnMakeCurrent(fun () ->
                    destroyHandle real
                )

            let handleList = handles |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Seq.toList
            handles.Clear()
            handleList |> List.iter register

            match ContextHandle.Current with
                | Some h -> 
                    let created = create h
                    handles.[h] <- created
                    handleCache.Value <- Some (h, created)
                | None -> ()


    member x.Context = context
    member x.Handle = handle()


    interface IResource with
        member x.Context = context
        member x.Handle = handle()