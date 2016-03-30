namespace Aardvark.Rendering.Vulkan


module Test =
    let run() =
        
        use instance = new Instance()
        let physical = instance.PhysicalDevices.[0]
        let device = instance.CreateDevice(physical, physical.Features, [], [])


        let ptr = device.DeviceLocalMemory.Alloc(1024L)
        printfn "%A" ptr.Handle.Handle
        
        printfn "Vendor = %A" physical.Vendor
        printfn "Name   = %s" physical.Name
        printfn "Type   = %A" physical.DeviceType

        ptr.Dispose()
