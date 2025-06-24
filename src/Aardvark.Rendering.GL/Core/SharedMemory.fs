namespace Aardvark.Rendering.GL

open Aardvark.Rendering
open OpenTK.Graphics.OpenGL

open System
open System.Collections.Generic

[<AutoOpen>]
module internal SharedMemory =

    type SharedMemoryBlock(manager : SharedMemoryManager, handle : int, external : IExternalMemoryBlock) =
        let mutable refCount = 1

        member x.Handle = handle
        member x.External = external
        member x.SizeInBytes = external.SizeInBytes

        member x.AddReference() =
            lock manager.Lock (fun _ ->
                refCount <- refCount + 1
            )

        member x.Dispose() =
            lock manager.Lock (fun _ ->
                refCount <- refCount - 1
                if refCount = 0 then
                    manager.Free x
            )

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    and SharedMemoryManager(useContext : unit -> IDisposable) =

        let blocks = Dictionary<IExternalMemoryHandle, SharedMemoryBlock>()

        member x.Lock = blocks :> obj

        member x.Free(block : SharedMemoryBlock) =
            lock (useContext()) (fun _ ->
                blocks.Remove block.External.Handle |> ignore
                GL.Dispatch.DeleteMemoryObject block.Handle
                GL.Check "DeleteMemoryObject"
            )

        member x.Import(external : IExternalMemoryBlock) =
            lock blocks (fun _ ->
                match blocks.TryGetValue external.Handle with
                | (true, shared) ->
                    if external.SizeInBytes <> shared.SizeInBytes then
                        failwithf "[GL] Cannot import the same memory block with varying sizes"

                    shared.AddReference()
                    shared

                | _ ->
                    using (useContext()) (fun _ ->
                        let mo = GL.Dispatch.CreateMemoryObject()
                        GL.Check "CreateMemoryObjects"

                        match external.Handle with
                        | :? Win32Handle as h ->
                            GL.Dispatch.ImportMemoryWin32Handle(mo, int64 external.SizeInBytes, ExternalHandleType.HandleTypeOpaqueWin32Ext, h.Handle)
                        | :? PosixHandle as h ->
                            h.UseHandle (fun fd -> // Importing into the memory object invalidates the fd
                                GL.Dispatch.ImportMemoryFd(mo, int64 external.SizeInBytes, ExternalHandleType.HandleTypeOpaqueFdExt, fd)
                            )
                        | h ->
                            failwithf "[GL] Unknown memory handle %A" <| h.GetType()

                        GL.Check "ImportMemory"

                        let shared = new SharedMemoryBlock(x, mo, external)
                        blocks.[external.Handle] <- shared
                        shared
                    )
            )
