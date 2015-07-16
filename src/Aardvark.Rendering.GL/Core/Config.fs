namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4

/// <summary>
/// A module containing default GL configuration properties
/// </summary>
module Config =
    /// <summary>
    /// The major GL Version for default contexts
    /// </summary>
    let MajorVersion = 3

    /// <summary>
    /// The minor GL Version for default contexts
    /// </summary>
    let MinorVersion = 0

    /// <summary>
    /// The number of subsamples for default windows
    /// </summary>
    let Samples = 1

    /// <summary>
    /// The GraphicsContextFlags for default contexts
    /// </summary>
    let ContextFlags = GraphicsContextFlags.Default

    /// <summary>
    /// The number of resource context to be created for a default
    /// rendering context instance.
    /// </summary>
    let NumberOfResourceContexts = 1

    /// <summary>
    /// defines whether the GL context should log errors
    /// </summary>
    [<Literal>]
    let CheckErrors = false

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



[<AutoOpen>]
module Error =

    exception OpenGLException of ErrorCode * string

    // in release the literal value of CheckErrors in combination
    // with this inline function leads to a complete elimination of
    // the enire call including the allocation of its arguments
    type GL with
        static member inline Check str =
            if Config.CheckErrors then
                let err = GL.GetError()
                if err <> ErrorCode.NoError then
                    Aardvark.Base.Report.Warn("{0}:{1}",err,str)
                    //raise <| OpenGLException(err, str)


    // Here's a comparison of what ILSpy says:
    //   Debug:
    //		GL.DeleteSync(this.f.Handle);
    //		string data = "failed to delete fence";
    //		if (false)
    //		{
    //			ErrorCode error = GL.GetError();
    //			if (error != ErrorCode.NoError)
    //			{
    //				Operators.Raise<Unit>(new Error.OpenGLException(error, data));
    //			}
    //		}
    //		this.builder@.Zero();
    //		return null;
    //
    //   Release:
    //    	GL.DeleteSync(this.f.Handle);
    //		return null;