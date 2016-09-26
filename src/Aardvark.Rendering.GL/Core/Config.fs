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
    let MajorVersion = 3

    /// <summary>
    /// The minor GL Version for default contexts
    /// </summary>
    let MinorVersion = 3

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
    let NumberOfResourceContexts = 2

    /// <summary>
    /// defines whether the GL context should log errors
    /// </summary>
    [<Literal>]
    let CheckErrors = false

    /// ResourceSet.Update and Program.Run use a GL fence sync if true.
    /// This flag improves timings for gpu uploads but also incurs a (possible) performance
    /// penality as well as incompatibiliy on some drivers.
    [<Literal>]
    let SyncUploadsAndFrames = false

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


    let enableVertexArrayObjectsIfPossible = true
    let enableSamplersIfPossible = true
    let enableUniformBuffersIfPossible = true



[<AutoOpen>]
module Error =

    open System.Runtime.InteropServices

    exception OpenGLException of ErrorCode * string


    let private debug (debugSource : DebugSource) (debugType : DebugType) (id : int) (severity : DebugSeverity) (length : int) (message : nativeint) (userParam : nativeint) =
         let message = Marshal.PtrToStringAnsi(message,length)
         match severity with
             | DebugSeverity.DebugSeverityMedium ->
                 Report.Warn("[GL] {0}", message)
             | DebugSeverity.DebugSeverityNotification -> () 
             | DebugSeverity.DebugSeverityHigh ->
                 Report.Error("[GL] {0}", message)
             | _ ->
                Report.Line("[GL] {0}", message)

    let private debugHandler = DebugProc debug

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

        static member SetupDebugOutput() =
            GL.DebugMessageCallback(debugHandler,nativeint 0)
            let arr : uint32[] = null
            let severity = DebugSeverityControl.DebugSeverityHigh ||| DebugSeverityControl.DebugSeverityMedium 
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, severity, 0, arr, true)
            GL.Check "SetupDebugOutput"

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