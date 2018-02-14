namespace Scratch

open System
open System.Reflection
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms

#nowarn "9"


module TPL =

    module Shader =
        open FShade

        [<LocalSize(X = 64)>]
        let copyShader (src : int[]) (dst : int[]) =
            compute {
                let id = getGlobalId().X
                dst.[id] <- src.[id]
            }

    let runRace() =
        use app = new HeadlessVulkanApplication(true)
        let device = app.Runtime.Device

        let copyEngine = device.CopyEngine

        let mutable pending = 0L



        let evt = new SemaphoreSlim(0)
        let size = 16L <<< 20


        let hostBuffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit size
        hostBuffer.Memory.Mapped(fun ptr -> Marshal.Set(ptr, 1, size))
        let deviceBuffer = device.DeviceMemory |> Buffer.createConcurrent true VkBufferUsageFlags.TransferDstBit size

        let enqueueCopy(size : int64) =
            Interlocked.Add(&pending, size) |> ignore
            let destroy() =
                evt.Release() |> ignore


            let chunkSize = 1L <<< 30
            let chunks = if size % chunkSize = 0L then size / chunkSize else 1L + size / chunkSize

//            let copies = 
//                Array.init (int chunks) (fun ci ->
//                    let offset = chunkSize * int64 ci
//                    let size = min (size - offset) chunkSize
//                    CopyCommand.BufferCopy(hostBuffer.Handle, deviceBuffer.Handle, VkBufferCopy(uint64 offset, uint64 offset,  uint64 size), cont)
//                )
//
//            copyEngine.Enqueue(copies)

            for ci in 0L .. chunks - 1L do
                let offset = chunkSize * ci
                let size = min (size - offset) chunkSize
                let copy = CopyCommand.Copy(hostBuffer.Handle, offset, deviceBuffer.Handle, offset, size)
                copyEngine.Enqueue(copy)

            copyEngine.Enqueue(CopyCommand.Callback destroy)
            evt.Wait()

        let rand = RandomSystem()
        let mutable running = false
        let copyRunning = new ManualResetEventSlim(false)
        let computeRunning = new ManualResetEventSlim(true)

        let copyThreadHate () =
            while true do
                copyRunning.Wait()
                let size = 64L <<< 20 //rand.UniformLong(1L <<< 16) + 1024L
                enqueueCopy size

        let thread = Thread(ThreadStart(copyThreadHate), IsBackground = true)
        thread.Start()

        let computeThread () =
            let data = Array.init (1 <<< 23) (fun i -> i + 1)
            let a = app.Runtime.CreateBuffer(data)
            let b = app.Runtime.CreateBuffer<int>(a.Count)


            let copyCompute =
                let shader = app.Runtime.CreateComputeShader Shader.copyShader
                let inputs = shader.Runtime.NewInputBinding shader
            
                inputs.["src"] <- a
                inputs.["dst"] <- b
                inputs.Flush()


                let prog = 
                    shader.Runtime.Compile [ 
                        ComputeCommand.SetInput inputs
                        ComputeCommand.Bind shader
                        ComputeCommand.Dispatch(data.Length / 64)
                    ]

                let stream = prog.GetType().GetProperty("Stream", BindingFlags.Instance ||| BindingFlags.NonPublic).GetValue(prog) |> unbox<VKVM.CommandStream>

                let pool = device.GraphicsFamily.TakeCommandPool()
                let cmd = pool.CreateCommandBuffer(CommandBufferLevel.Primary)
                cmd.Begin(CommandBufferUsage.SimultaneousUse)
                cmd.AppendCommand()
                stream.Run(cmd.Handle)
                cmd.End()
                fun () -> device.GraphicsFamily.RunSynchronously(cmd)

            let sw = System.Diagnostics.Stopwatch()
            let mutable iter = 0
            while true do
                computeRunning.Wait()

                sw.Start()
                //a.Upload data
                copyCompute()
                sw.Stop()
                iter <- iter + 1

                if sw.Elapsed.TotalMilliseconds >= 500.0 then
                    let t = sw.MicroTime / iter
                    Log.line "took: %A" t
                    iter <- 0
                    sw.Reset()

        let computeThread = Thread(ThreadStart(computeThread), IsBackground = true)
        computeThread.Start()


        let copyThreadHate () =
            while true do
                Thread.Sleep(500)
                copyEngine.PrintStats(true)

        let thread = Thread(ThreadStart(copyThreadHate), IsBackground = true)
        thread.Start()


        while true do
            printfn "press enter to toggle upload"
            Console.ReadLine() |> ignore
            running <- not running
            if running then copyRunning.Set()
            else copyRunning.Reset()


        ()   

    let run() =
        runRace()
        Environment.Exit 0
