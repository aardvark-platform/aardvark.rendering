namespace Aardvark.Rendering.Vulkan

open System

type IDevice =
    abstract member VKVM : VKVM
    abstract member Handle : VkDevice
    abstract member Instance : Instance
    abstract member PhysicalDevice : PhysicalDevice
    abstract member EnabledFeatures : DeviceFeatures
    abstract member IsExtensionEnabled : string -> bool

[<AllowNullLiteral>]
type IDeviceObject =
    abstract member DeviceInterface : IDevice

type IDeviceQueueFamily =
    inherit IDeviceObject
    abstract member Info : QueueFamilyInfo

type ICommandPool =
    inherit IDeviceObject
    abstract member Handle : VkCommandPool

type IResource =
    inherit IDisposable
    abstract member AddReference : unit -> unit
    abstract member ReferenceCount : int

type IResource<'Handle> =
    inherit IResource
    abstract member Handle : 'Handle

[<AutoOpen>]
module internal IDeviceExtensions =
    let inline checkForFault (device: IDevice) (message: string) (result: VkResult) =
        if result = VkResult.ErrorDeviceLost && device.EnabledFeatures.Debugging.DeviceFault then DeviceFault.report device.Handle
        result |> check message