#if INTERACTIVE
#I @"E:\Development\Aardvark-2015\build\Release\AMD64"
#r "Aardvark.Base.TypeProviders.dll"
#r "Aardvark.Base.dll"
#r "Aardvark.Base.FSharp.dll"
#r "OpenTK.dll"
#r "FSharp.PowerPack.dll"
#r "FSharp.PowerPack.Linq.dll"
#r "FSharp.PowerPack.Metadata.dll"
#r "FSharp.PowerPack.Parallel.Seq.dll"
#r "Aardvark.Rendering.GL.dll"
open Aardvark.Rendering.GL
#else
namespace Aardvark.Rendering.GL
#endif
open System
open System.Collections.Generic
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations
open FSharp.Data.Adaptive
open System.Reflection
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.GL

#nowarn "9"


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

type UniformBuffer(ctx : Context, handle : int, size : int, block : ShaderBlock) =
    let data = Marshal.AllocHGlobal(size)
    let mutable dirty = true

    member x.Free() = Marshal.FreeHGlobal data
    member x.Context = ctx
    member x.Handle = handle
    member x.Size = size
    member x.Block = block
    member x.Data = data
    member x.Dirty 
        with get() = dirty
        and set d = dirty <- d

type UniformBufferView =
    class
        val mutable public Buffer : Aardvark.Rendering.GL.Buffer
        val mutable public Offset : nativeint
        val mutable public Size : nativeint

        new(b,o,s) = { Buffer = b; Offset = o; Size = s }

    end



[<AutoOpen>]
module UniformBufferExtensions =
    open System.Linq
    open System.Collections.Generic
    open System.Runtime.CompilerServices
    open System.Diagnostics

    type Context with
        member x.CreateUniformBuffer(dataSize : nativeint) =
            Operators.using x.ResourceLock (fun _ ->
                
                let handle = GL.CreateBuffer()
                GL.Check "could not create uniform buffer"

                GL.NamedBufferData(handle, dataSize, 0n, BufferUsageHint.DynamicDraw)
                GL.Check "could not allocate uniform buffer"

                addUniformBuffer x (int64 dataSize)
                UniformBuffer(x, handle, int dataSize, Unchecked.defaultof<ShaderBlock>)
            )


        member x.CreateUniformBuffer(block : ShaderBlock) =
            Operators.using x.ResourceLock (fun _ ->
                
                let handle = GL.CreateBuffer()
                GL.Check "could not create uniform buffer"

                GL.NamedBufferData(handle, nativeint block.DataSize, 0n, BufferUsageHint.DynamicDraw)
                GL.Check "could not allocate uniform buffer"

                addUniformBuffer x (int64 block.DataSize)
                UniformBuffer(x, handle, block.DataSize, block)
            )

        member x.Delete(b : UniformBuffer) =
            Operators.using x.ResourceLock (fun _ ->
                GL.DeleteBuffer(b.Handle)
                GL.Check "could not delete uniform buffer"

                removeUniformBuffer x (int64 b.Size)
                b.Free()
            )

        member x.Upload(b : UniformBuffer) =
            if b.Dirty then
                b.Dirty <- false
                Operators.using x.ResourceLock (fun _ ->
                    GL.NamedBufferSubData(b.Handle, 0n, nativeint b.Size, b.Data)
                    GL.Check "could not upload uniform buffer" 
                )
    
