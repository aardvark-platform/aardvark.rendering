namespace Aardvark.Rendering.GL

open Aardvark.Rendering
open System.Runtime.InteropServices
open OpenTK.Graphics

type DepthRange = MinusOneToOne=0 | ZeroToOne=1

/// <summary>
/// A module containing default GL configuration properties
/// </summary>
module Config =
    /// <summary>
    /// The major GL Version for default contexts
    /// </summary>
    let mutable MajorVersion = 4

    /// <summary>
    /// The minor GL Version for default contexts
    /// </summary>
    let mutable MinorVersion = 6

    /// <summary>
    /// The number of subsamples for default windows
    /// </summary>
    let Samples = 1

    /// <summary>
    /// The GraphicsContextFlags for default contexts
    /// </summary>
    let mutable ContextFlags = GraphicsContextFlags.Default

    /// <summary>
    /// The number of resource context to be created for a default
    /// rendering context instance.
    /// </summary>
    let mutable NumberOfResourceContexts = 2

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

    /// <summary>
    /// Specifies the depth range convention: The default is MinusOneToOne [-1, 1]. 
    /// Setting the DepthRange to ZeroToOne [0, 1] allows implementing a "reversed depth" zbuffer with increased precission.
    /// NOTE: Internally it will use glClipControl to set the convention and requires OpenGL 4.5 to work.
    ///       FShade is not aware of this configuration, it still expects shader to follow the [-1, 1] convention and allows automatic conversion to [0, 1] as used by Vulkan.
    ///       This means for OpenGL use the depth values are untouched and it up for the application to use suitable projection transformations when setting the DepthRange to [0, 1].
    /// </summary>
    let mutable DepthRange = DepthRange.MinusOneToOne

module RuntimeConfig =

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
    /// Use the "new" RenderTask OpenGL RenderTask supporting RuntimeCommands.
    /// </summary>
    let mutable UseNewRenderTask = true

    /// <summary>
    /// Use pixel buffer objects for texture uploads and downloads.
    /// </summary>
    let mutable UsePixelBufferObjects =
        not <| RuntimeInformation.IsOSPlatform(OSPlatform.OSX)

    /// <summary>
    /// Determines if buffers are shared between render tasks.
    /// </summary>
    let mutable ShareBuffersBetweenTasks = true

    /// <summary>
    /// Determines if textures are shared between render tasks.
    /// </summary>
    let mutable ShareTexturesBetweenTasks = true

    /// <summary>
    /// Determines whether the CPU implementation of block compression should be
    /// used over the implicit encoding provided by OpenGL.
    /// </summary>
    let mutable PreferHostSideTextureCompression = true

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

        /// Determines if and how OpenGL error flags are checked and reported.
        ErrorFlagCheck : ErrorFlagCheck

        /// Print OpenGL calls when a render task is run.
        /// Only supported if RuntimeConfig.UseNewRenderTask = false.
        DebugRenderTasks : bool

        /// Print the GLSL code during compilation (will not show up when cached).
        PrintShaderCode : bool
    }

    member internal x.DebugOutputEnabled =
        x.DebugOutput.IsSome

    /// Disables all debugging functionality.
    static member None =
        { DebugOutput      = None
          ErrorFlagCheck   = ErrorFlagCheck.Disabled
          DebugRenderTasks = false
          PrintShaderCode  = false }

    /// OpenGL errors are logged, debug output reports warnings and errors.
    static member Minimal =
        { DebugConfig.None with
            DebugOutput    = Some DebugOutputConfig.Minimal
            ErrorFlagCheck = ErrorFlagCheck.PrintError }

    /// OpenGL errors raise an exception, debug output reports all messages but information, shader code is printed.
    static member Normal =
        { DebugConfig.Minimal with
            DebugOutput     = Some DebugOutputConfig.Normal
            ErrorFlagCheck  = ErrorFlagCheck.ThrowOnError
            PrintShaderCode = true }

    /// All messages are reported via debug output, render tasks print their OpenGL calls.
    static member Full =
        { DebugConfig.Normal with
            DebugOutput      = Some DebugOutputConfig.Full
            DebugRenderTasks = true }

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