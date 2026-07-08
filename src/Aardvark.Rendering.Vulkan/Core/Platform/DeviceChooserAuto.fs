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

/// Default-chooser resolution with the `AARDVARK_VULKAN` environment override:
///   AARDVARK_VULKAN=integrated | discrete | <name substring>  (case-insensitive)
/// The override applies ONLY when no explicit IDeviceChooser was supplied —
/// an application-provided chooser always wins. A set-but-unmatchable value
/// FAILS loudly (silently benchmarking the wrong GPU is worse than crashing).
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DeviceChooser =
    open Aardvark.Base

    let private envChooser () : IDeviceChooser voption =
        match System.Environment.GetEnvironmentVariable "AARDVARK_VULKAN" with
        | null | "" -> ValueNone
        | v ->
            let key = v.Trim().ToLowerInvariant()
            let pick (devices : PhysicalDevice[]) =
                let chosen =
                    match key with
                    | "integrated" -> devices |> Array.tryFind (fun d -> d.Type = VkPhysicalDeviceType.IntegratedGpu)
                    | "discrete" | "dedicated" -> devices |> Array.tryFind (fun d -> d.Type = VkPhysicalDeviceType.DiscreteGpu)
                    | s -> devices |> Array.tryFind (fun d -> d.Name.ToLowerInvariant().Contains s)
                match chosen with
                | Some d ->
                    Log.line "[Vulkan] AARDVARK_VULKAN=%s -> %s" v d.Name
                    d
                | None ->
                    for d in devices do Log.warn "[Vulkan] available device: %s (%A)" d.Name d.Type
                    failwithf "[Vulkan] AARDVARK_VULKAN=%s matches no device" v
            ValueSome { new IDeviceChooser with member _.Run devices = pick devices }

    /// The chooser to use when the application supplied NONE: the
    /// AARDVARK_VULKAN override if set, otherwise `fallback`.
    let defaultChooserOr (fallback : unit -> IDeviceChooser) : IDeviceChooser =
        match envChooser () with
        | ValueSome c -> c
        | ValueNone -> fallback ()

    /// The standard default: AARDVARK_VULKAN override, else DeviceChooserAuto.
    let defaultChooser (preferDedicated : bool) : IDeviceChooser =
        defaultChooserOr (fun () -> DeviceChooserAuto(preferDedicated = preferDedicated) :> IDeviceChooser)
