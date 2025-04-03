namespace Aardvark.Rendering.Vulkan

/// Device chooser automatically selecting a device.
type DeviceChooserAuto =
    inherit DeviceChooser
    val private score : PhysicalDevice -> int

    static let deviceTypeScoreDedicated = function
        | VkPhysicalDeviceType.DiscreteGpu -> 16
        | VkPhysicalDeviceType.IntegratedGpu -> 8
        | VkPhysicalDeviceType.VirtualGpu -> 4
        | VkPhysicalDeviceType.Cpu -> 2
        | _ -> 1

    static let deviceTypeScoreIntegrated = function
        | VkPhysicalDeviceType.IntegratedGpu -> 16
        | VkPhysicalDeviceType.DiscreteGpu -> 8
        | VkPhysicalDeviceType.VirtualGpu -> 4
        | VkPhysicalDeviceType.Cpu -> 2
        | _ -> 1

    /// Selects the device with the highest score according to the given function.
    new (score: PhysicalDevice -> int) =
        { inherit DeviceChooser(); score = score }

    /// Prefers either dedicated or integrated GPUs.
    new (preferDedicated: bool) =
        let score = if preferDedicated then deviceTypeScoreDedicated else deviceTypeScoreIntegrated
        DeviceChooserAuto(fun device -> score device.Type)

    /// Selects the first reported device.
    new () =
        DeviceChooserAuto(fun _ -> 0)

    override _.IgnoreCache = true
    override this.Choose(devices) = devices |> Array.sortByDescending this.score |> Array.head