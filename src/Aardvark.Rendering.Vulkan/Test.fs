namespace Aardvark.Rendering.Vulkan

open System.Threading
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

module Test =
    let runtest (ctx : Context) =
        let queue = ctx.DefaultQueue
        let cmdPool = ctx.DefaultCommandPool



        use m0 = ctx.HostVisibleMemory.Alloc(1044L)
        use m1 = ctx.DeviceLocalMemory.Alloc(1024L)
        use mt = ctx.DeviceLocalMemory.Alloc(25L)
        use m2 = ctx.HostVisibleMemory.Alloc(1044L)

        let p0 = m0.Skip(10L).Take(1024L)
        let p1 = m1
        let p2 = m2.Skip(10L).Take(1024L)
        
        let copyTest =
            command {
                do! m0.Upload (Array.create 1044 0xFFuy)
                do! m2.Upload (Array.create 1044 0x00uy)
                
                do! Command.barrier MemoryTransfer
                do! p0.Upload (Array.init 1024 (fun i -> byte (i % 255)))
                
                do! Command.barrier MemoryTransfer
                do! p0.CopyTo(p1)

                do! Command.barrier MemoryTransfer
                do! p1.CopyTo(p2)

                do! Command.barrier MemoryTransfer
                return! p2.Download()
            }

        let res = copyTest.RunSynchronously queue
        let check = Array.init 1024 (fun i -> byte (i % 255))
        let success = Array.forall2 (=) res check
        printfn "success: %A" success



        let untouched = 
            DevicePtr.map m2 (fun p ->
                let p = NativePtr.ofNativeInt p
                let start = List.init 10 (fun i -> NativePtr.get p i = 0uy) |> List.forall id
                let rest = List.init 10 (fun i -> NativePtr.get p (1034 + i) = 0uy) |> List.forall id

                start && rest
            )

        printfn "rest untouched: %A" untouched
        


    let run() =
        let instance = new Instance(["VK_LAYER_LUNARG_draw_state"], ["VK_EXT_debug_report"])
        let physical = instance.PhysicalDevices.[0]
        printfn "Vendor = %A" physical.Vendor
        printfn "Name   = %s" physical.Name
        printfn "Type   = %A" physical.DeviceType

        let device = instance.CreateDevice(physical, ["VK_LAYER_LUNARG_draw_state"], [])
        instance.OnDebugMessage.Add (fun msg ->
            match msg.messageFlags with
                | VkDebugReportFlagBitsEXT.VkDebugReportErrorBitExt ->
                    errorf "%s" msg.message
                    
                | VkDebugReportFlagBitsEXT.VkDebugReportWarningBitExt ->
                    warnf "%s" msg.message

                | _ ->
                    debugf "%s" msg.message
        )

        let ctx = Context(device)
        runtest ctx

        device.DeviceLocalMemory.Heap.Used |> printfn "used: %A"
