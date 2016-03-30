namespace Aardvark.Rendering.Vulkan


module Test =
    let run() =
        
        use instance = new Instance()
        let physical = instance.PhysicalDevices.[0]
        let device = instance.CreateDevice(physical, physical.Features, [], [])


        let ptr = device.DeviceLocalMemory.Alloc(1024L)
        printfn "%A" ptr.Handle.Handle
        
        printfn "Vendor = %s" (PCI.vendorName (int physical.Properties.vendorID))
        printfn "Name   = %s" physical.Properties.deviceName.Value
        printfn "Type   = %A" physical.Properties.deviceType

        ptr.Dispose()
