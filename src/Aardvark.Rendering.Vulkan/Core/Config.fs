namespace Aardvark.Rendering.Vulkan

module RuntimeConfig =

    /// Logs render task recompilation
    let mutable ShowRecompile = true

    /// Prints the shader code during the compilation (will not show up when cached)
    let mutable PrintShaderCode = true
    