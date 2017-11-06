namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices

module ComputeShader =
    module Shaders =
        open FShade

        let plus2 (a : int[]) (b : int[]) =
            compute {
                let i = getGlobalId().X
                b.[i] <- a.[i] + 2
            }

    type IComputeShader =
        inherit IDisposable
        abstract member Invoke : Map<string, obj> -> unit

    type BackendBuffer(runtime : IRuntime, real : IBackendBuffer) =
        member x.Handle = real

        member x.Dispose() = runtime.DeleteBuffer real

        member x.Upload(offset : nativeint, data : nativeint, size : nativeint) =
            runtime.Copy(data, real, offset, size)

        member x.Download(offset : nativeint, data : nativeint, size : nativeint) =
            runtime.Copy(real, offset, data, size)

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type IBufferView<'a when 'a : unmanaged> =
        abstract member Buffer : BackendBuffer
        abstract member Offset : nativeint
        abstract member Count : int
    
    type IBuffer<'a when 'a : unmanaged> =
        inherit IBufferView<'a>
        inherit IDisposable

    type private BackendBufferView<'a when 'a : unmanaged>(buffer : BackendBuffer, offset : nativeint, count : int) =

        member x.Buffer = buffer
        member x.Offset = offset
        member x.Count = count

        interface IBufferView<'a> with
            member x.Buffer = buffer
            member x.Offset = offset
            member x.Count = count        

    type private BackendBuffer<'a when 'a : unmanaged>(buffer : BackendBuffer) =
        inherit BackendBufferView<'a>(buffer, 0n, int (buffer.Handle.SizeInBytes / nativeint sizeof<'a>))
        interface IBuffer<'a> with
            member x.Dispose() = buffer.Dispose()

    [<AutoOpen>]
    module TypedBufferExtensions =
        let private nsa<'a when 'a : unmanaged> = nativeint sizeof<'a>
        
        type IBufferView<'a when 'a : unmanaged> with
            member x.Upload(src : 'a[], srcIndex : int, dstIndex : int, count : int) =
                let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
                try
                    let ptr = gc.AddrOfPinnedObject()
                    x.Buffer.Upload(nativeint dstIndex * nsa<'a>, ptr + nsa<'a> * nativeint srcIndex, nsa<'a> * nativeint count)
                finally
                    gc.Free()
                
            member x.Download(srcIndex : int, dst : 'a[], dstIndex : int, count : int) =
                let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
                try
                    let ptr = gc.AddrOfPinnedObject()
                    x.Buffer.Upload(nativeint dstIndex * nsa<'a>, ptr + nsa<'a> * nativeint srcIndex, nsa<'a> * nativeint count)
                finally
                    gc.Free()

            member x.Upload(src : 'a[], dstIndex : int, count : int) = x.Upload(src, 0, dstIndex, count)
            member x.Upload(src : 'a[], count : int) = x.Upload(src, 0, 0, count)
            member x.Upload(src : 'a[]) = x.Upload(src, 0, 0, src.Length)

            
            member x.Download(srcIndex : int, dst : 'a[], count : int) = x.Download(srcIndex, dst, 0, count)
            member x.Download(dst : 'a[], count : int) = x.Download(0, dst, 0, count)
            member x.Download(dst : 'a[]) = x.Download(0, dst, 0, dst.Length)
            member x.Download() = 
                let dst = Array.zeroCreate x.Count 
                x.Download(0, dst, 0, dst.Length)
                dst

            member x.GetSlice(min : Option<int>, max : Option<int>) =
                let min = defaultArg min 0
                let max = defaultArg max (x.Count - min)
                BackendBufferView<'a>(x.Buffer, x.Offset + nativeint min * nsa<'a>, 1 + max - min) :> IBufferView<_>

        type IRuntime with
            member x.CreateBuffer<'a when 'a : unmanaged>(count : int) =
                let buffer = new BackendBuffer(x, x.CreateBuffer(nsa<'a> * nativeint count))
                new BackendBuffer<'a>(buffer) :> IBuffer<'a>

            member x.CreateBuffer<'a when 'a : unmanaged>(data : 'a[]) =
                let buffer = new BackendBuffer(x, x.CreateBuffer(nsa<'a> * nativeint data.Length))
                let res = new BackendBuffer<'a>(buffer) :> IBuffer<'a>
                res.Upload(data)
                res

    type IComputeRuntime =
        inherit IRuntime
        abstract member MaxLocalSize : V3i
    
        abstract member CreateBuffer : size : int64 -> IBackendBuffer
        abstract member Compile : FShade.ComputeShader -> IComputeShader

    module GLImpl =
        open Aardvark.Rendering.GL

    module VulkanImpl =
        open Aardvark.Rendering.Vulkan


    let run() =
        let runtime : IComputeRuntime = failwith ""
        let sh = FShade.ComputeShader.ofFunction runtime.MaxLocalSize Shaders.plus2

        let c = runtime.Compile sh

        let a = runtime.CreateBuffer<int> 1024
        let b = runtime.CreateBuffer<int> 1024

        c.Invoke(Map.ofList ["a", a :> obj; "b", b :> obj])

        let res = b.Download()
        printfn "%A" res

        ()
