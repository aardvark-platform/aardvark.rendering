namespace Aardvark.Rendering.GL

open System.Runtime.InteropServices
open OpenTK.Graphics

type DepthRange = MinusOneToOne=0 | ZeroToOne=1

/// Reporting modes for GL errors.
[<RequireQualifiedAccess>]
type internal ErrorReporting =
    /// Do not check for errors.
    | Disabled

    /// Log errors.
    | Log

    /// Throw an exception on errors.
    | Exception

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
  
    /// If true, no OpenGL queries take place, i.e. no primitive counting etc.
    let mutable SupressGLTimers = false

    let mutable SupressSparseBuffers = false

    let mutable PrintShaderCode = true

    /// <summary>
    /// Use the "new" RenderTask OpenGL RenderTask supporting RuntimeCommands (5.1.0)
    /// </summary>
    let mutable UseNewRenderTask = false

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
    /// Determines whether the debug output should be synchronous.
    /// </summary>
    let mutable DebugOutputSynchronous = true

    /// <summary>
    /// Determines if and how API errors are checked and reported.
    /// </summary>
    let mutable internal ErrorReporting = ErrorReporting.Disabled