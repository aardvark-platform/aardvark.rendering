namespace Aardvark.Rendering.GL

open System
open System.Threading
open Aardvark.Base

open Aardvark.Rendering
open FSharp.Data.Adaptive
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.GL

type UniformBufferManager(ctx : Context) =

    let bufferMemory : Management.Memory<Buffer> =

        let alloc (size : nativeint) =
            if size = 0n then
                new Buffer(ctx, 0n, 0)
            else
                use __ = ctx.ResourceLock
                let handle = GL.Dispatch.CreateBuffer()
                GL.Check "failed to create uniform buffer"

                BufferMemoryUsage.addUniformBuffer ctx (int64 size)

                GL.Dispatch.NamedBufferStorage(handle, size, 0n, BufferStorageFlags.DynamicStorageBit)
                GL.Check "could not allocate uniform buffer"

                new Buffer(ctx, size, handle)

        let free (buffer : Buffer) (size : nativeint) =
            if buffer.Handle <> 0 then
                use __ = ctx.ResourceLock
                GL.DeleteBuffer(buffer.Handle)
                BufferMemoryUsage.removeUniformBuffer ctx (int64 size)
                GL.Check "could not free uniform buffer"

        {
            malloc = alloc
            mfree = free
            mcopy = fun _ _ _ _ -> failwith "not implemented"
            mrealloc = fun _ _ _ -> failwith "not implemented"
        }


    let manager = new Management.ChunkedMemoryManager<_>(bufferMemory, 1n <<< 20)

    //let buffer =
    //    // TODO: better implementation for uniform buffers (see https://github.com/aardvark-platform/aardvark.rendering/issues/32)
    //    use __ = ctx.ResourceLock
    //    let handle = GL.GenBuffer()
    //    GL.Check "could not create buffer"
    //    new FakeSparseBuffer(ctx, handle, id, id) :> SparseBuffer

    //let manager = MemoryManager.createNop()

    let viewCache = ResourceCache<UniformBufferView, int>(None, None)
    let rw = new ReaderWriterLockSlim()

    member x.CreateUniformBuffer(block : FShade.GLSL.GLSLUniformBuffer, scope : Ag.Scope, u : IUniformProvider, additional : SymbolDict<IAdaptiveValue>) : IResource<UniformBufferView, int> =
        let values =
            block.ubFields
            |> List.map (fun f ->
                let name = f.ufName

                let value = match Uniforms.tryGetDerivedUniform name u with
                            | Some v -> v
                            | None ->

                                let sem = Symbol.Create name
                                match u.TryGetUniform(scope, sem) with
                                | Some v -> v
                                | None ->
                                    match additional.TryGetValue sem with
                                    | (true, m) -> m
                                    | _ -> failwithf "[GL] could not get uniform: %A" f

                if Object.ReferenceEquals(value, null) then
                    failwithf "[GL] uniform of %A is null" f

                value
            )

        let key = values |> List.map (fun v -> v :> obj)

        let alignedSize = (block.ubSize + 255) &&& ~~~255 // needs to be multiple of GL_UNIFORM_BUFFER_OFFSET_ALIGNMENT (currently 256)

        viewCache.GetOrCreate(
            key,
            fun () ->
                let writers = List.map2 (fun (f : FShade.GLSL.GLSLUniformBufferField) v -> nativeint f.ufOffset, ShaderParameterWriter.adaptive v (ShaderParameterType.ofGLSLType f.ufType)) block.ubFields values

                let mutable block = Unchecked.defaultof<_>
                let mutable store = 0n
                { new Resource<UniformBufferView, int>(ResourceKind.UniformBuffer) with
                    member x.GetInfo b =
                        b.Size |> Mem |> ResourceInfo

                    member x.View(b : UniformBufferView) =
                        b.Buffer.Handle

                    member x.Create(token, rt, old) =
                        use __ = ctx.ResourceLock
                        let handle =
                            match old with
                                | Some old -> old
                                | None ->
                                    block <- manager.Alloc(nativeint alignedSize)
                                    store <- System.Runtime.InteropServices.Marshal.AllocHGlobal alignedSize
                                    //buffer.Commitment(block.Offset, block.Size, true)

                                    // record BufferView statistic: use block.Size instead of alignedSize -> allows to see overhead due to chunked buffers and alignment
                                    BufferMemoryUsage.addUniformBufferView ctx (int64 block.Size)

                                    UniformBufferView(block.Memory.Value, block.Offset, nativeint block.Size)

                        for (offset,w) in writers do w.Write(token, store + offset)

                        GL.Dispatch.NamedBufferSubData(handle.Buffer.Handle, handle.Offset, handle.Size, store)
                        GL.Check "could not upload uniform buffer"
                        //buffer.WriteUnsafe(handle.Offset, handle.Size, store)
                        handle

                    member x.Destroy h =
                        if not block.IsFree then
                            System.Runtime.InteropServices.Marshal.FreeHGlobal store
                            store <- 0n
                            BufferMemoryUsage.removeUniformBufferView ctx (int64 block.Size)
                            use __ = ctx.ResourceLock
                            manager.Free block

                }
        )

    member x.Dispose() =
        manager.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()