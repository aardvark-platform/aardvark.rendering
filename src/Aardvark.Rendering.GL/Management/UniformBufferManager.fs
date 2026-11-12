namespace Aardvark.Rendering.GL

open System
open Aardvark.Base

open Aardvark.Rendering
open OpenTK.Graphics.OpenGL4
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

                let b = new Buffer(ctx, size, handle)
                if ctx.DebugLabelsEnabled then b.Name <- "Uniform Buffer"
                b

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

    let viewCache = ResourceCache<UniformBufferView, int>(None, None)

    member x.CreateUniformBuffer(block : FShade.GLSL.GLSLUniformBuffer, scope : Ag.Scope, uniforms : IUniformProvider) : IResource<UniformBufferView, int> =
        // specialization constants the provider does not supply keep their zero
        // defaults, as on backends where they specialize the pipeline
        let optional = block.ubName = "SpecializationConstant"

        let values =
            block.ubFields
            |> List.choose (fun f ->
                let name = f.ufName

                let value =
                    match Uniforms.tryGetDerivedUniform name uniforms with
                    | ValueSome v -> ValueSome v
                    | ValueNone -> uniforms.TryGetUniform(scope, Symbol.Create name)

                match value with
                | ValueSome value ->
                    if Object.ReferenceEquals(value, null) then
                        failf "uniform '%s' is null" name

                    Some (f, value)

                | ValueNone ->
                    if optional then None
                    else failf "could not find uniform '%s'" name
            )

        // a block with absent members is not identified by its values alone
        let key =
            (if optional then [ block :> obj ] else [])
            @ (values |> List.map (fun (_, v) -> v :> obj))

        let alignedSize = (block.ubSize + 255) &&& ~~~255 // needs to be multiple of GL_UNIFORM_BUFFER_OFFSET_ALIGNMENT (currently 256)

        viewCache.GetOrCreate(
            key,
            fun () ->
                let writers =
                    values |> List.map (fun (target, value) ->
                        let writer =
                            match value.ContentType |> UniformWriters.tryGetWriter target.ufOffset target.ufType with
                            | Result.Ok writer -> writer
                            | Result.Error msg -> failf $"cannot get writer for uniform '{target.ufName}': {msg}"

                        value, writer
                    )

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
                            | ValueSome old -> old
                            | ValueNone ->
                                block <- manager.Alloc(nativeint alignedSize)
                                store <- System.Runtime.InteropServices.Marshal.AllocHGlobal alignedSize

                                if optional then
                                    System.Runtime.InteropServices.Marshal.Copy(Array.zeroCreate<byte> alignedSize, 0, store, alignedSize)
                                //buffer.Commitment(block.Offset, block.Size, true)

                                // record BufferView statistic: use block.Size instead of alignedSize -> allows to see overhead due to chunked buffers and alignment
                                BufferMemoryUsage.addUniformBufferView ctx (int64 block.Size)

                                UniformBufferView(block.Memory.Value, block.Offset, nativeint block.Size)

                        for value, writer in writers do
                            writer.Write(token, value, store)

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