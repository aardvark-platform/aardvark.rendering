namespace Aardvark.Rendering.Vulkan

module RuntimeConfig =

    /// Logs render task recompilation
    let mutable ShowRecompile = true

    /// Prints the shader code during the compilation (will not show up when cached)
    let mutable PrintShaderCode = true

    /// Indicates if descriptors should not be created with the update-after-bind flag.
    let mutable SuppressUpdateAfterBind = false