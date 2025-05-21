namespace Aardvark.Rendering.Vulkan

open Aardvark.Rendering

module RuntimeConfig =

    /// <summary>
    /// Specifies the expected depth range of normalized device coordinates.
    /// If this is not the native Vulkan range of [0, 1], shaders will be automatically adjusted to
    /// map from [-1, 1] to [0, 1].
    /// </summary>
    let mutable DepthRange = DepthRange.MinusOneToOne

    /// <summary>
    /// Indicates if descriptors should not be created with the update-after-bind flag.
    /// </summary>
    let mutable SuppressUpdateAfterBind = false


type DebugReportVerbosity =
    | Error       = 1
    | Warning     = 2
    | Information = 3
    | Debug       = 4

[<CLIMutable>]
type DebugReportConfig =
    {
        /// Determines the lowest severity of debug messages to be printed.
        Verbosity : DebugReportVerbosity

        /// Trigger a breakpoint if a debugger is attached and an error occurs.
        BreakOnError : bool

        /// Trace and print the origin of object handles in debug messages.
        TraceObjectHandles : bool
    }

    static member Minimal =
        { Verbosity = DebugReportVerbosity.Warning
          BreakOnError = false
          TraceObjectHandles = false }

    static member Normal =
        { DebugReportConfig.Minimal with
            Verbosity = DebugReportVerbosity.Information
            BreakOnError = true }

    static member Full =
        { DebugReportConfig.Normal with
            Verbosity = DebugReportVerbosity.Debug
            TraceObjectHandles = true }


type ShaderValidation =

    /// No shader validation enabled.
    | Disabled = 0

    /// Validation layers will process Debug.Printf operations in shaders and send the resulting output to the debug callback.
    | DebugPrint = 1

    /// Instruments shader programs to generate additional diagnostic data.
    | GpuAssisted = 2

[<CLIMutable>]
type ValidationLayerConfig =
    {
        /// Enable thread safety validation checks.
        ThreadSafetyValidation : bool

        /// Enable object lifetime validation checks.
        ObjectLifetimesValidation : bool

        /// Controls if and how shader-based validation checks are performed.
        ShaderBasedValidation : ShaderValidation

        /// Report resource access conflicts due to missing or incorrect synchronization operations
        /// between actions (Draw, Copy, Dispatch, Blit) reading or writing the same regions of memory.
        SynchronizationValidation : bool

        /// Enables the output of warnings related to common misuse of
        /// the Vulkan API, but which are not explicitly prohibited by the specification.
        BestPracticesValidation : bool
    }

    static member Standard =
        { ThreadSafetyValidation    = true
          ObjectLifetimesValidation = true
          ShaderBasedValidation     = ShaderValidation.Disabled
          SynchronizationValidation = false
          BestPracticesValidation   = false }

    static member DebugPrint =
        { ValidationLayerConfig.Standard with
            ShaderBasedValidation = ShaderValidation.DebugPrint }

    static member Full =
        { ValidationLayerConfig.Standard with
            ShaderBasedValidation     = ShaderValidation.GpuAssisted
            SynchronizationValidation = true
            BestPracticesValidation   = true }

[<CLIMutable>]
type DebugConfig =
    {
        /// Settings controlling the debug report.
        /// None if the debug report is disabled.
        DebugReport : DebugReportConfig option

        /// Output debug markers and labels that can be viewed in debugging tools like RenderDoc or NVIDIA Nsight.
        DebugLabels : bool

        /// Features to be enabled in the Vulkan validation layer.
        /// None if the validation layer is disabled.
        ValidationLayer : ValidationLayerConfig option

        /// Recompile shaders and verify integrity of shader caches.
        VerifyShaderCacheIntegrity : bool

        /// Print the GLSL code during compilation (will not show up when cached).
        PrintShaderCode : bool

        /// Print render task recompilation.
        PrintRenderTaskRecompile : bool

        /// Verbosity of the logger to be used to print instance and device information.
        PlatformInformationVerbosity : int

        /// Generate SPIR-V debug information (required to inspect source code in Nsight Graphics).
        GenerateShaderDebugInfo : bool

        /// Optimize shaders after compiling.
        OptimizeShaders : bool
    }

    member internal x.DebugReportEnabled =
        x.DebugReport.IsSome

    member internal x.ValidationLayerEnabled =
        x.ValidationLayer.IsSome

    member internal x.DebugPrintEnabled =
        match x.ValidationLayer with
        | Some cfg -> cfg.ShaderBasedValidation = ShaderValidation.DebugPrint
        | _ -> false

    /// Disables all debugging functionality.
    static member None =
        { DebugReport                   = None
          DebugLabels                   = false
          ValidationLayer               = None
          VerifyShaderCacheIntegrity    = false
          PlatformInformationVerbosity  = 4
          PrintShaderCode               = false
          PrintRenderTaskRecompile      = false
          GenerateShaderDebugInfo       = false
          OptimizeShaders               = true }

    /// Enables validation layers, printing warnings and errors.
    static member Minimal =
        { DebugConfig.None with
            DebugReport                  = Some DebugReportConfig.Minimal
            ValidationLayer              = Some ValidationLayerConfig.Standard
            PlatformInformationVerbosity = 2 }

    /// Enables validation layers, printing everything except debug messages.
    /// Also prints shader code and render task recompilation.
    static member Normal =
        { DebugConfig.Minimal with
            DebugReport              = Some DebugReportConfig.Normal
            DebugLabels              = true
            PrintShaderCode          = true
            PrintRenderTaskRecompile = true }

    /// Enables object trace handling, shader cache integrity checks, and every validation layer feature.
    static member Full =
        { DebugConfig.Normal with
            DebugReport                = Some DebugReportConfig.Full
            ValidationLayer            = Some ValidationLayerConfig.Full
            VerifyShaderCacheIntegrity = true
            GenerateShaderDebugInfo    = true
            OptimizeShaders            = false }

    interface IDebugConfig


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DebugConfig =

    let ofDebugLevel (level : DebugLevel) =
        match level with
        | DebugLevel.None    -> DebugConfig.None
        | DebugLevel.Minimal -> DebugConfig.Minimal
        | DebugLevel.Normal  -> DebugConfig.Normal
        | DebugLevel.Full    -> DebugConfig.Full

    let unbox (cfg : IDebugConfig) =
        match cfg with
        | :? DebugConfig as d -> d
        | :? DebugLevel as level -> ofDebugLevel level
        | _ -> failwithf "[Vulkan] Unexpected debug configuration %A" cfg