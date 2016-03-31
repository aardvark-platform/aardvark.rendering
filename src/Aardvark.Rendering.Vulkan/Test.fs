namespace Aardvark.Rendering.Vulkan

open System.Threading
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

module Test =
    let run() =
        
        //Instance.AvailableLayers |> List.iter (printfn "layer: %A")

        let instance = new Instance(["VK_LAYER_LUNARG_draw_state"], ["VK_EXT_debug_report"])
        let physical = instance.PhysicalDevices.[0]
        
        let device = instance.CreateDevice(physical, ["VK_LAYER_LUNARG_draw_state"], [])
        let cmdPool = device.DefaultCommandPool

        instance.OnDebugMessage.Add (fun msg ->
            match msg.messageFlags with
                | VkDebugReportFlagBitsEXT.VkDebugReportErrorBitExt ->
                    errorf "%s" msg.message
                    
                | VkDebugReportFlagBitsEXT.VkDebugReportWarningBitExt ->
                    warnf "%s" msg.message

                | _ ->
                    debugf "%s" msg.message
        )

        let m0 = device.HostVisibleMemory.Alloc(1044L)
        let m1 = device.DeviceLocalMemory.Alloc(1024L)
        let m2 = device.HostVisibleMemory.Alloc(1044L)

        let p0 = m0.Skip(10L).Take(1024L)
        let p1 = m1
        let p2 = m2.Skip(10L).Take(1024L)
        
        DevicePtr.map m0 (fun p -> Marshal.Set(p, 0xFFFFFFFF, 1044))
        DevicePtr.map m2 (fun p -> Marshal.Set(p, 0x00000000, 1044))


        DevicePtr.map p0 (fun p ->
            let p = NativePtr.ofNativeInt p
            for i in 0..1023 do
                NativePtr.set p i (byte (i % 255))
        )

        DevicePtr.copy p0 p1 1024L |> device.DefaultQueue.RunSynchronously
        DevicePtr.copy p1 p2 1024L |> device.DefaultQueue.RunSynchronously


        let res : byte[] = Array.zeroCreate 1024
        DevicePtr.map p2 (fun p ->
            let p = NativePtr.ofNativeInt p
            for i in 0..1023 do
                res.[i] <- NativePtr.get p i
        )
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
        
        printfn "Vendor = %A" physical.Vendor
        printfn "Name   = %s" physical.Name
        printfn "Type   = %A" physical.DeviceType

