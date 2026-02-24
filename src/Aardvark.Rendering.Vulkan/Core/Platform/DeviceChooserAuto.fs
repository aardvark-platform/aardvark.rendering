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

    static let scorePortability (device: PhysicalDevice) =
        if device.HasExtension KHRPortabilitySubset.Name then 0
        else 100

    /// Selects the device with the highest score according to the given function.
    new (score: PhysicalDevice -> int) =
        { inherit DeviceChooser(); score = score }

    /// Prefers either dedicated or integrated GPUs.
    /// Non-conformant devices are chosen last.
    new (preferDedicated: bool) =
        let typeScore = if preferDedicated then deviceTypeScoreDedicated else deviceTypeScoreIntegrated
        DeviceChooserAuto(fun device -> scorePortability device + typeScore device.Type)

    /// Selects the first reported device.
    /// Non-conformant devices are chosen last.
    new () =
        DeviceChooserAuto(scorePortability)

    override _.IgnoreCache = true
    override this.Choose(devices) = devices |> Seq.sortByDescending this.score |> Seq.head