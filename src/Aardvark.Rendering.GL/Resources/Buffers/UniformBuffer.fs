namespace Aardvark.Rendering.GL

open System.Threading
open System.Runtime.InteropServices
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module private BufferMemoryUsage =

    let addUniformBuffer (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.UniformBufferCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformBufferMemory,size) |> ignore

    let removeUniformBuffer (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.UniformBufferCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformBufferMemory,-size) |> ignore

    let addUniformPool (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.UniformPoolCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformPoolMemory,size) |> ignore

    let removeUniformPool (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.UniformPoolCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformPoolMemory,-size) |> ignore

    let updateUniformPool (ctx:Context) oldSize newSize =
        Interlocked.Add(&ctx.MemoryUsage.UniformPoolMemory,newSize-oldSize) |> ignore

    let addUniformBufferView (ctx:Context) size =
        Interlocked.Increment(&ctx.MemoryUsage.UniformBufferViewCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformBufferViewMemory,size) |> ignore

    let removeUniformBufferView (ctx:Context) size =
        Interlocked.Decrement(&ctx.MemoryUsage.UniformBufferViewCount) |> ignore
        Interlocked.Add(&ctx.MemoryUsage.UniformBufferViewMemory,-size) |> ignore

type UniformBuffer(ctx : Context, handle : int, size : int) =
    let data = Marshal.AllocHGlobal(size)
    let mutable dirty = true

    member x.Free() = Marshal.FreeHGlobal data
    member x.Context = ctx
    member x.Handle = handle
    member x.Size = size
    member x.Data = data
    member x.Dirty 
        with get() = dirty
        and set d = dirty <- d

type UniformBufferView =
    class
        val mutable public Buffer : Aardvark.Rendering.GL.Buffer
        val mutable public Offset : nativeint
        val mutable public Size : nativeint

        new(buffer, offset, size) = { Buffer = buffer; Offset = offset; Size = size }
    end

[<AutoOpen>]
module UniformBufferExtensions =

    type Context with
        member x.CreateUniformBuffer(dataSize : nativeint) =
            use _ = x.ResourceLock

            let handle = GL.Dispatch.CreateBuffer()
            GL.Check "could not create uniform buffer"

            GL.Dispatch.NamedBufferData(handle, dataSize, 0n, BufferUsageHint.DynamicDraw)
            GL.Check "could not allocate uniform buffer"

            addUniformBuffer x (int64 dataSize)
            UniformBuffer(x, handle, int dataSize)

        member x.Delete(b : UniformBuffer) =
            use _ = x.ResourceLock

            GL.DeleteBuffer(b.Handle)
            GL.Check "could not delete uniform buffer"

            removeUniformBuffer x (int64 b.Size)
            b.Free()

        member x.Upload(b : UniformBuffer) =
            if b.Dirty then
                use _ = x.ResourceLock
                b.Dirty <- false
                GL.Dispatch.NamedBufferSubData(b.Handle, 0n, nativeint b.Size, b.Data)
                GL.Check "could not upload uniform buffer"