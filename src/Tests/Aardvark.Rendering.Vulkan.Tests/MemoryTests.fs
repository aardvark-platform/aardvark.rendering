namespace Aardvark.Rendering.Vulkan.Tests

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open NUnit.Framework
open FsUnit
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Base.Incremental.Operators
open System.Diagnostics

module ``Memory Tests`` =

    [<Test>]
    let ``[Memory] map working``() =
        use i = new Instance()
        use d = i.CreateDevice(i.PhysicalDevices.[0])

        use mem = d.HostVisibleMemory.Alloc 1024L

        let data = Array.init 1024 (fun i -> byte (i % 128))
        let test : byte[] = Array.zeroCreate 1024

        // copy the data to the buffer
        DevicePtr.map mem (fun ptr ->
            let ptr = NativePtr.ofNativeInt ptr
            for i in 0..data.Length-1 do
                NativePtr.set ptr i data.[i]
        )

        // read the data back
        DevicePtr.map mem (fun ptr ->
            let ptr = NativePtr.ofNativeInt ptr
            for i in 0..data.Length-1 do
                test.[i] <- NativePtr.get ptr i
        )

        test |> should equal data

    [<Test>]
    let ``[Memory] up-/download working [host]``() =
        use i = new Instance()
        use d = i.CreateDevice(i.PhysicalDevices.[0])
        let ctx = new Context(d)

        use mem = d.HostVisibleMemory.Alloc 1024L

        let data = Array.init 1024 (fun i -> byte (i % 128))
        let test : byte[] = Array.zeroCreate 1024

        // copy the data to the buffer
        mem.Upload(data) |> ctx.DefaultQueue.RunSynchronously

        // read the data back
        mem.Download(test) |> ctx.DefaultQueue.RunSynchronously

        test |> should equal data

    [<Test>]
    let ``[Memory] up-/download working [device]``() =
        use i = new Instance()
        use d = i.CreateDevice(i.PhysicalDevices.[0])
        let ctx = new Context(d)

        use mem = d.DeviceLocalMemory.Alloc 1024L

        let data = Array.init 1024 (fun i -> byte (i % 128))
        let test : byte[] = Array.zeroCreate 1024

        // copy the data to the buffer
        mem.Upload(data) |> ctx.DefaultQueue.RunSynchronously

        // read the data back
        mem.Download(test) |> ctx.DefaultQueue.RunSynchronously

        test |> should equal data

    [<Test>]
    let ``[Memory] copy working``() =
        use i = new Instance()
        use d = i.CreateDevice(i.PhysicalDevices.[0])
        let ctx = new Context(d)

        use mem0 = d.HostVisibleMemory.Alloc 1024L
        use mem1 = d.DeviceLocalMemory.Alloc 1024L
        use mem2 = d.HostVisibleMemory.Alloc 1024L

        let data = Array.init 1024 (fun i -> byte (i % 128))
        let test : byte[] = Array.zeroCreate 1024

        let runTest =
            command {
                do! mem0.Upload data
                do! Command.barrier MemoryTransfer
                
                do! mem0.CopyTo mem1
                do! Command.barrier MemoryTransfer

                do! mem1.CopyTo mem2
                do! Command.barrier MemoryTransfer
            }

        runTest |> ctx.DefaultQueue.RunSynchronously

        // read the data back
        mem2.Download(test) |> ctx.DefaultQueue.RunSynchronously

        test |> should equal data


