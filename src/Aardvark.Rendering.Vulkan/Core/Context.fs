namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base


type Context (device : Device) =
    inherit Resource(device)

    let physical = device.Physical
    let hostMem = MemoryManager.aligned 256L device.HostVisibleMemory
    let deviceMem = MemoryManager.aligned 256L device.DeviceLocalMemory
    
    let defaultPool =
        let create() = 
            let fam = device.QueueFamilies |> Map.toSeq |> Seq.head |> fst
            device.CreateCommandPool fam
        new ThreadLocal<_>(Func<_>(create), true)

    let queues =
        lazy (
            Map.ofList [
                for (family, count) in Map.toSeq device.QueueFamilies do
                    let queues =
                        Array.init count (fun i ->
                            let mutable res = VkQueue.Zero
                            VkRaw.vkGetDeviceQueue(device.Handle, uint32 family.Index, uint32 i, &&res)

                            new Queue(device, defaultPool, family, res, i)
                        )
                    yield family, queues
            ]
        )

    let defaultQueue =
        lazy (
            queues.Value 
                |> Map.toSeq
                |> Seq.maxBy (fun (q : PhysicalQueueFamily,_) ->
                        (if q.Graphics then 2 else 0) +
                        (if q.Compute then 1 else 0)
                   )
                |> snd
                |> Array.item 0
        )

    let instructionCtx = InstructionContext(device)


    override x.Release() =
        if defaultPool.IsValueCreated then 
            defaultPool.Values |> Seq.iter (fun p -> p.Dispose())


    member x.InstructionContext = instructionCtx

    member x.Queues = queues.Value
    member x.DefaultQueue = defaultQueue.Value
    member x.DefaultCommandPool = defaultPool.Value

    member x.Device = device
    member x.HostVisibleMemory = hostMem
    member x.DeviceLocalMemory = deviceMem

    member x.Alloc(reqs : VkMemoryRequirements) =
        let dl = reqs.memoryTypeBits &&& (1u <<< deviceMem.Memory.TypeIndex) <> 0u
        let hl = reqs.memoryTypeBits &&& (1u <<< hostMem.Memory.TypeIndex) <> 0u
        
        if hl then hostMem.Alloc(int64 reqs.size)
        elif dl then deviceMem.Alloc(int64 reqs.size)
        else failf "could not find compatible memory"
