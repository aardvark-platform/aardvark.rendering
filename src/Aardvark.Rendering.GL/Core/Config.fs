namespace Aardvark.Rendering.GL

open Aardvark.Rendering
open System.Runtime.InteropServices
open OpenTK.Graphics

/// <summary>
/// A module containing the OpenGL application configuration
/// </summary>
module Config =

    /// <summary>
    /// The major GL Version for default contexts
    /// </summary>
    let mutable MajorVersion = 4

    /// <summary>
    /// The minor GL Version for default contexts
    /// </summary>
    let mutable MinorVersion = if RuntimeInformation.IsOSPlatform OSPlatform.OSX then 1 else 6

    /// <summary>
    /// The number of subsamples for default windows
    /// </summary>
    let Samples = 1

    /// <summary>
    /// The GraphicsContextFlags for default contexts
    /// </summary>
    let mutable ContextFlags = GraphicsContextFlags.Default

    /// <summary>
    /// The number of bits used for color values in default contexts
    /// </summary>
    let BitsPerPixel = 32

    /// <summary>
    /// The number of bits used for the depth buffer in default contexts
    /// </summary>
    let DepthBits = 24

    /// <summary>
    /// The number of bits used for the stencil buffer in default contexts
    /// </summary>
    let StencilBits = 8

    /// <summary>
    /// The number of buffers used by default contexts
    /// </summary>
    let Buffers = 2


module RuntimeConfig =

    /// <summary>
    /// The number of resource context to be created for a default
    /// rendering context instance.
    /// </summary>
    let mutable NumberOfResourceContexts = 2

    /// <summary>
    /// Determines if context creation and sharing is robust to work properly in some edge cases.
    /// When a context is created a parent context has to be provided to enable sharing. If that parent
    /// context happens to be currently in use by another thread, context creation or sharing may fail.
    /// If this flag is false, a resource context is used as parent without checking if it is actually available.
    /// If true, an additional hidden context will be used as parent.
    /// Default is true.
    /// </summary>
    let mutable RobustContextSharing = true

    /// <summary>
    /// Specifies the expected depth range of normalized device coordinates.
    /// Setting a depth range of [0, 1] requires GL_ARB_clip_control or OpenGL 4.5.
    /// </summary>
    let mutable DepthRange = DepthRange.MinusOneToOne

    /// ResourceSet.Update and Program.Run use a GL fence sync if true.
    /// This flag improves timings for GPU uploads but also incurs a (possible) performance
    /// penalty as well as incompatibility on some drivers.
    let mutable SyncUploadsAndFrames = false

    /// <summary>
    /// Determines if GPU sparse buffers should be avoided. These are known to be
    /// broken on non-Windows platforms with some NVIDIA hardware, even though the
    /// required extension is present.
    /// </summary>
    let mutable SuppressSparseBuffers = false

    /// <summary>
    /// Use pixel buffer objects for texture uploads and downloads.
    /// </summary>
    let mutable UsePixelBufferObjects =
        not <| RuntimeInformation.IsOSPlatform(OSPlatform.OSX)

    /// <summary>
    /// Determines whether the CPU implementation of block compression should be
    /// used over the implicit encoding provided by OpenGL.
    /// </summary>
    let mutable PreferHostSideTextureCompression = true

    /// <summary>
    /// Determines whether resources of render and compute tasks are allowed to be accessed
    /// concurrently. If set to false, the update and execution of tasks is made mutual exclusive
    /// by using a global lock. This may mitigate potential race conditions on shared resources but
    /// completely disables concurrent execution of tasks.
    /// </summary>
    let mutable AllowConcurrentResourceAccess = false


/// Reporting modes for OpenGL errors.
type ErrorFlagCheck =

    /// Do not check for errors.
    | Disabled     = 0

    /// Log errors without throwing any exception.
    | PrintError   = 1

    /// Throw an exception on errors.
    | ThrowOnError = 2

type DebugOutputSeverity =
    | Notification = 0
    | Low          = 1
    | Medium       = 2
    | High         = 3

[<CLIMutable>]
type DebugOutputConfig =
    {
        /// The lowest severity of messages to be printed.
        Verbosity : DebugOutputSeverity

        /// Use synchronous OpenGL debug output if available.
        Synchronous : bool
    }

    static member Minimal =
        { Verbosity = DebugOutputSeverity.High
          Synchronous = true }

    static member Normal =
        { DebugOutputConfig.Minimal with
            Verbosity = DebugOutputSeverity.Low }

    static member Full =
        { DebugOutputConfig.Minimal with
            Verbosity = DebugOutputSeverity.Notification }

[<CLIMutable>]
type DebugConfig =
    {
        /// Settings controlling the OpenGL debug output.
        /// None if the debug output is disabled.
        DebugOutput : DebugOutputConfig option

        /// Output debug markers and labels that can be viewed in debugging tools like RenderDoc or NVIDIA Nsight.
        DebugLabels : bool

        /// Determines if and how OpenGL error flags are checked and reported.
        ErrorFlagCheck : ErrorFlagCheck

        /// Print OpenGL calls when a render task is run.
        DebugRenderTasks : bool

        /// Print OpenGL calls when a compute task is run.
        DebugComputeTasks : bool

        /// Print the GLSL code during compilation (will not show up when cached).
        PrintShaderCode : bool

        /// Print a warning when the element type of a provided attribute buffer or value
        /// is double while the expected element type is not.
        DoubleAttributePerformanceWarning : bool
    }

    member internal x.DebugOutputEnabled =
        x.DebugOutput.IsSome

    /// Disables all debugging functionality.
    static member None =
        { DebugOutput                       = None
          ErrorFlagCheck                    = ErrorFlagCheck.Disabled
          DebugLabels                       = false
          DebugRenderTasks                  = false
          DebugComputeTasks                 = false
          PrintShaderCode                   = false
          DoubleAttributePerformanceWarning = false }

    /// OpenGL errors are logged, debug output reports warnings and errors.
    static member Minimal =
        { DebugConfig.None with
            DebugOutput                       = Some DebugOutputConfig.Minimal
            ErrorFlagCheck                    = ErrorFlagCheck.PrintError
            DoubleAttributePerformanceWarning = true }

    /// OpenGL errors raise an exception, debug output reports all messages but information, shader code is printed.
    static member Normal =
        { DebugConfig.Minimal with
            DebugOutput     = Some DebugOutputConfig.Normal
            ErrorFlagCheck  = ErrorFlagCheck.ThrowOnError
            DebugLabels     = true
            PrintShaderCode = true }

    /// All messages are reported via debug output, render and compute tasks print their OpenGL calls.
    static member Full =
        { DebugConfig.Normal with
            DebugOutput       = Some DebugOutputConfig.Full
            DebugRenderTasks  = true
            DebugComputeTasks = true }

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
        | _ -> failwithf "[GL] Unexpected debug configuration %A" cfg

[<AutoOpen>]
module internal GLCheckErrorsConfig =

    module GL =

        /// Internal global variable that is read by GL.Check() and set depending
        /// on the debug config when the runtime is created.
        let mutable CheckErrors = ErrorFlagCheck.Disabled