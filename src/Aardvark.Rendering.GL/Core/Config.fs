namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base

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
    let mutable MinorVersion = 3

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
    let mutable NumberOfResourceContexts = 4

    /// <summary>
    /// defines whether the GL context should log errors
    /// </summary>
    [<Literal>]
    let mutableCheckErrors = true

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

    [<Literal>]
    let CheckErrors = true

    let enableVertexArrayObjectsIfPossible = true
    let enableSamplersIfPossible = true
    let enableUniformBuffersIfPossible = true

module RuntimeConfig =

    /// ResourceSet.Update and Program.Run use a GL fence sync if true.
    /// This flag improves timings for gpu uploads but also incurs a (possible) performance
    /// penality as well as incompatibiliy on some drivers.
    let mutable SyncUploadsAndFrames = false
  
    /// If true, no OpenGL queries take place, i.e. no primitive counting etc.
    let mutable SupressGLTimers = false

    let mutable SupressSparseBuffers = false

    let mutable PrintShaderCode = true

[<AutoOpen>]
module Error =

    open System.Runtime.InteropServices

    exception OpenGLException of ErrorCode * string


    let private debug (debugSource : DebugSource) (debugType : DebugType) (id : int) (severity : DebugSeverity) (length : int) (message : nativeint) (userParam : nativeint) =
         let message = Marshal.PtrToStringAnsi(message,length)
         match severity with
             | DebugSeverity.DebugSeverityNotification -> 
                Report.Line(5, "[GL:{0}] {1}", userParam, message)

             | DebugSeverity.DebugSeverityMedium ->
                match debugType with
                    | DebugType.DebugTypePerformance -> Report.Line(4, "[GL:{0}] {1}", userParam, message)
                    | _ -> Report.Warn("[GL:{0}] {1}", userParam, message)

             | DebugSeverity.DebugSeverityHigh ->
                 Report.Error("[GL:{0}] {1}", userParam, message)

             | _ ->
                Report.Line(4, "[GL:{0}] {1}", userParam, message)

    let private debugHandler = DebugProc debug

    // in release the literal value of CheckErrors in combination
    // with this inline function leads to a complete elimination of
    // the enire call including the allocation of its arguments
    type GL with
        static member Check str =
            if Config.CheckErrors then
                let err = GL.GetError()
                if err <> ErrorCode.NoError then
                    Aardvark.Base.Report.Warn("{0}: {1}",err,str)
                    //raise <| OpenGLException(err, str)

        static member SetupDebugOutput() =
            let ctx = GraphicsContext.CurrentContext |> unbox<IGraphicsContextInternal>
            GL.DebugMessageCallback(debugHandler,ctx.Context.Handle)
            let arr : uint32[] = null
            let severity = DebugSeverityControl.DontCare //DebugSeverityControl.DebugSeverityHigh ||| DebugSeverityControl.DebugSeverityMedium 
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, severity, 0, arr, true)
            GL.Check "DebugMessageControl"

    type GLTimer private() =
        let counter = GL.GenQuery()
        do GL.BeginQuery(QueryTarget.TimeElapsed, counter)

        static member Start() = new GLTimer()

        member x.GetElapsedSeconds() =
            GL.EndQuery(QueryTarget.TimeElapsed)

            let mutable nanoseconds = 0L
            GL.GetQueryObject(counter, GetQueryObjectParam.QueryResult, &nanoseconds)

            float nanoseconds / 1000000000.0

        member x.Dispose() =
            GL.DeleteQuery(counter)

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type OpenGlStopwatch() =
        let mutable totalTime = 0L
        let mutable handle = -1

        member private x.TotalTime
            with get() = totalTime
            and set v = totalTime <- v

        member x.IsRunning = handle >= 0

        member x.Start() =
            if not x.IsRunning then
                handle <- GL.GenQuery()
                GL.QueryCounter(handle, QueryCounterTarget.Timestamp)
                //GL.BeginQuery(QueryTarget.TimeElapsed, handle)

        member x.Stop() =
            if x.IsRunning then
                let mutable startTime = 0L
                let mutable endTime = 0L
                GL.GetQueryObject(handle, GetQueryObjectParam.QueryResult, &startTime)


                GL.QueryCounter(handle, QueryCounterTarget.Timestamp)
                GL.GetQueryObject(handle, GetQueryObjectParam.QueryResult, &endTime)

                totalTime <- totalTime + (endTime - startTime)

                GL.DeleteQuery(handle)
                handle <- -1

        member x.Restart() =
            x.Stop()
            totalTime <- 0L
            x.Start()

        member x.ElapsedNanoseconds =
            if x.IsRunning then
                x.Stop()
                x.Start()
            totalTime

        member x.ElapsedMicroseconds = float x.ElapsedNanoseconds / 1000.0
        member x.ElapsedMilliseconds = float x.ElapsedNanoseconds / 1000000.0

        member x.Elapsed = TimeSpan.FromTicks (int64 (float x.ElapsedNanoseconds / 100.0))

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