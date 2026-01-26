namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System

type PhysicalDeviceGroup internal(instance: IVulkanInstance, devices: PhysicalDevice[]) =
    inherit PhysicalDevice(instance, devices.[0].Handle)

    let mask = (0u, devices) ||> Array.fold (fun s d -> s ||| d.DeviceMask)

    let allIndicesArr =
        [|
            let mutable mask = 1u
            for i in 0u .. 31u do
                if mask &&& mask <> 0u then
                    yield i
                mask <- mask <<< 1

        |]

    let mutable allIndices = NativePtr.alloc<uint32> allIndicesArr.Length
    do for i in 0 .. allIndicesArr.Length - 1 do
        allIndices.[i] <- allIndicesArr.[i]

    member x.Count = devices.Length
    member x.Devices : PhysicalDevice[] = devices
    member x.AllIndicesArr = allIndicesArr
    member x.AllIndices = allIndices

    override x.Id = devices |> Seq.map (fun d -> d.Id) |> String.concat "_"

    override x.DeviceMask = mask

    member x.Dispose() =
        if allIndices <> NativePtr.zero then
            NativePtr.free allIndices
            allIndices <- NativePtr.zero

    override x.ToString() =
        let cnt = devices.Length
        sprintf "%d x { name = %s; type = %A; api = %A }" cnt x.Name x.Type x.APIVersion

    interface IDisposable with
        member x.Dispose() = x.Dispose()