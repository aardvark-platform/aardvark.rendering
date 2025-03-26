namespace Aardvark.Rendering.Vulkan

open System

type IDevice =
    abstract member Handle : VkDevice
    abstract member Instance : Instance
    abstract member PhysicalDevice : PhysicalDevice
    abstract member EnabledFeatures : DeviceFeatures
    abstract member IsExtensionEnabled : string -> bool

[<AllowNullLiteral>]
type IDeviceObject =
    abstract member DeviceInterface : IDevice

and IDeviceQueueFamily =
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