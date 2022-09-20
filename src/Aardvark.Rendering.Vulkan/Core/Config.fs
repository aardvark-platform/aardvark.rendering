namespace Aardvark.Rendering.Vulkan

module RuntimeConfig =

    /// Logs render task recompilation
    let mutable ShowRecompile = true

    /// Prints the shader code during the compilation (will not show up when cached)
    let mutable PrintShaderCode = true

    /// Indicates if descriptors should not be created with the update-after-bind flag.
    let mutable SuppressUpdateAfterBind = false


module ValidationConfig =

    type ShaderValidation =

        /// No shader validation enabled.
        | Disabled = 0

        /// Validation layers will process Debug.Printf operations in shaders and send the resulting output to the debug callback.
        | DebugPrintf = 1

        /// Instruments shader programs to generate additional diagnostic data.
        | GpuAssisted = 2


    /// Controls how shader-based validation checks are performed.
    let mutable ShaderBasedValidation = ShaderValidation.Disabled

    /// Report resource access conflicts due to missing or incorrect synchronization operations
    /// between actions (Draw, Copy, Dispatch, Blit) reading or writing the same regions of memory.
    let mutable SynchronizationValidation = false

    /// Enables the output of warnings related to common misuse of
    /// the Vulkan API, but which are not explicitly prohibited by the specification.
    let mutable BestPracticesValidation = false