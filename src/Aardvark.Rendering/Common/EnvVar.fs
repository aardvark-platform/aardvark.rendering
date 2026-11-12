namespace Aardvark.Rendering

open System
open Aardvark.Base

/// Holds the cached value of one process environment variable.
[<Sealed>]
type private EnvVarCell(name : string) =
    [<VolatileField>]
    let mutable loaded = false

    [<VolatileField>]
    let mutable value : string = null

    member x.Name = name

    /// The value: read from the process environment on first access (null when unset);
    /// setting null forgets the cached value so the environment is read again.
    member x.Value
        with get () =
            if not loaded then
                let v =
                    try Environment.GetEnvironmentVariable name
                    with e ->
                        Log.warn "[Env] Could not read %s: %s" name e.Message
                        null

                if not (isNull v) then
                    Log.line "[Env] %s=%s" name v

                value <- v
                loaded <- true

            value

        and set (v : string) =
            if isNull v then
                loaded <- false
                value <- null
            else
                value <- v
                loaded <- true

/// Registry of the environment variables read by the rendering libraries, one documented
/// string property per variable. A value is read from the process environment on first access
/// and cached; later changes to the process environment are ignored. Setting a property
/// overrides the environment, setting it to null forgets the override so the environment is
/// read again on the next access.
[<AbstractClass; Sealed>]
type EnvVar private () =

    static let vulkanInlineCommands = EnvVarCell "AARDVARK_VULKAN_INLINE_COMMANDS"
    static let vulkanLibrary = EnvVarCell "AARDVARK_VULKAN_LIBRARY"
    static let oit = EnvVarCell "AARDVARK_OIT"
    static let oitABufferDebug = EnvVarCell "AARDVARK_OIT_ABUFFER_DEBUG"
    static let legacySg = EnvVarCell "AARDVARK_LEGACY_SG"
    static let vulkanDevice = EnvVarCell "AARDVARK_VULKAN_DEVICE"

    /// Returns whether a switch value enables it: "1", "on", "true" and "yes" (ignoring case
    /// and surrounding whitespace) do, everything else, including null, does not.
    static member IsEnabled (value : string) =
        if isNull value then false
        else
            match value.Trim().ToLowerInvariant() with
            | "1" | "on" | "true" | "yes" -> true
            | _ -> false

    /// Returns whether the value is neither null nor empty.
    static member IsSet (value : string) =
        not (String.IsNullOrEmpty value)

    // Aardvark.Rendering

    /// Overrides the transparency technique requested from IRuntime.CompileRender: "off" (also "0",
    /// "false") renders without a transparency pass, "auto" picks the best supported technique, "wb"
    /// weighted-blended, "abuffer" the A-buffer; other values keep the request. Read when a render
    /// task is compiled, so it can be set programmatically before compiling.
    static member AARDVARK_OIT
        with get () = oit.Value
        and set (value : string) = oit.Value <- value

    /// Diagnostic mode of the A-buffer resolve pass: 1 visualizes the stored sample coverage of
    /// each pixel, 2 outputs the sample index to check that the pass runs per sample; any other
    /// value composites normally. Read once when the transparency render task module is
    /// initialized.
    static member AARDVARK_OIT_ABUFFER_DEBUG
        with get () = oitABufferDebug.Value
        and set (value : string) = oitABufferDebug.Value <- value

    // Aardvark.Rendering.Vulkan

    /// Set to 1 to replay Vulkan command streams inline into the primary command buffer instead of
    /// recording them into secondary command buffers, which MoltenVK (portability subset) devices do
    /// unconditionally because vkCmdExecuteCommands crashes there. For testing that path on a
    /// conformant driver. Read by the Vulkan render and compute tasks when they are created.
    static member AARDVARK_VULKAN_INLINE_COMMANDS
        with get () = vulkanInlineCommands.Value
        and set (value : string) = vulkanInlineCommands.Value <- value

    /// The Vulkan loader library to load, as a library name or an absolute path, e.g. a specific SDK
    /// loader on macOS where DYLD_* variables are stripped by SIP and cannot redirect the default.
    /// Wins over VulkanLoader.LibraryNames and PreferMoltenVK set by the application. Read once when
    /// the library is loaded.
    static member AARDVARK_VULKAN_LIBRARY
        with get () = vulkanLibrary.Value
        and set (value : string) = vulkanLibrary.Value <- value

    /// Selects the physical device: "integrated", "discrete" (or "dedicated"), or a part of a device
    /// name (case-insensitive). A value matching no device logs a warning and falls back to the
    /// regular choice. Read whenever a device is chosen, unless DeviceChooser.AllowEnvironmentOverride
    /// is false.
    static member AARDVARK_VULKAN_DEVICE
        with get () = vulkanDevice.Value
        and set (value : string) = vulkanDevice.Value <- value

    // Aardvark.SceneGraph

    /// Set to 1 to make IRuntime.CompileRender build the render objects of every scene graph through
    /// the attribute grammar instead of the direct IRenderSg path, e.g. to check whether a rendering
    /// problem is caused by the direct path. Read whenever a scene graph is compiled.
    static member AARDVARK_LEGACY_SG
        with get () = legacySg.Value
        and set (value : string) = legacySg.Value <- value
