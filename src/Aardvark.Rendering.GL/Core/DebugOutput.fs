namespace Aardvark.Rendering.GL

open System
open System.Runtime.InteropServices
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL

[<AutoOpen>]
module Error =

    exception OpenGLException of ec : ErrorCode * msg : string with
        override x.Message = $"{x.msg} (Error: {x.ec})"

    type GL with
        static member private Check(str, throwOnError) =
            let err = GL.GetError()
            if err <> ErrorCode.NoError then
                let str = $"{str}"
                let msg =
                    if String.IsNullOrEmpty str then "An error occurred"
                    else string (Char.ToUpper str.[0]) + str.Substring(1)

                Report.Error($"[GL] {msg} (Error: {err})")

                if throwOnError then
                    raise <| OpenGLException(err, msg)

        /// Gets the value of the GL error flag and logs it
        /// with the given message string if it is not equal to GL_NO_ERROR.
        /// Throws an exception after logging if DebugConfig.ErrorFlagCheck = ThrowOnError.
        /// Does nothing if DebugConfig.ErrorFlagCheck = Disabled.
        static member Check str =
            let mode = GL.CheckErrors

            if mode <> ErrorFlagCheck.Disabled then
                let throwOnError = (mode = ErrorFlagCheck.ThrowOnError)
                GL.Check(str, throwOnError)

        /// Gets the value of the GL error flag and throws an exception
        /// with the given message string if it is not equal to GL_NO_ERROR.
        static member Assert str =
            GL.Check(str, true)

[<AutoOpen>]
module private IGraphicsContextDebugExtensions =
    let private suffixes = [""; "EXT"; "KHR"; "NV"; "AMD"]

    type IGraphicsContextInternal with
        member x.TryGetAddress (name : string) =
            suffixes |> List.tryPick (fun s ->
                let ptr = x.GetAddress(name + s)
                if ptr <> 0n then Some ptr
                else None
            )

[<AutoOpen>]
module private DebugOutputInternals =
    type GLDebugMessageCallbackDel = delegate of callback : nativeint * userData : nativeint -> unit

    let debugCallback (debugErrors : System.Collections.Generic.List<string>)
                      (debugSource : DebugSource) (debugType : DebugType) (id : int) (severity : DebugSeverity)
                      (length : int) (message : nativeint) (userParam : nativeint) =
        let message = Marshal.PtrToStringAnsi(message, length)

        match severity with
        | DebugSeverity.DebugSeverityNotification ->
            Report.Line(2, "[GL:{0}] {1}", userParam, message)

        | DebugSeverity.DebugSeverityMedium ->
            match debugType with
            | DebugType.DebugTypePerformance -> Report.Line(1, "[GL:{0}] {1}", userParam, message)
            | _ -> Report.Warn("[GL:{0}] {1}", userParam, message)

        | DebugSeverity.DebugSeverityHigh ->
            lock debugErrors (fun _ -> debugErrors.Add message)
            Report.Error("[GL:{0}] {1}", userParam, message)

        | _ ->
            Report.Line(2, "[GL:{0}] {1}", userParam, message)

[<RequireQualifiedAccess>]
type internal DebugOutputMode =
    | Disabled
    | Enabled of synchronous: bool

type internal DebugOutput =
    { Mode     : DebugOutputMode
      Callback : PinnedDelegate
      Errors   : System.Collections.Generic.List<string> }

    member x.GetErrors() =
        lock x.Errors (fun _ -> x.Errors.ToArray())

    member x.Dispose() = x.Callback.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

module internal DebugOutput =

    module private DebugSeverityControl =

        let ofVerbosity (verbosity : DebugOutputSeverity)=
            [
                DebugSeverityControl.DebugSeverityHigh

                if verbosity <= DebugOutputSeverity.Medium then
                    DebugSeverityControl.DebugSeverityMedium

                if verbosity <= DebugOutputSeverity.Low then
                    DebugSeverityControl.DebugSeverityLow

                if verbosity <= DebugOutputSeverity.Notification then
                    DebugSeverityControl.DebugSeverityNotification
            ]

    let private enableMessages (severities : List<DebugSeverityControl>) =

        let enable (enabled : bool) (severity : DebugSeverityControl) =
            let arr : uint32[] = null
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, severity, 0, arr, enabled)
            GL.Check "glDebugMessageControl failed"

        enable false DebugSeverityControl.DontCare
        severities |> List.iter (enable true)
       
    let tryInitialize (verbosity : DebugOutputSeverity) =
        let ctx = GraphicsContext.CurrentContext |> unbox<IGraphicsContextInternal>

        match ctx.TryGetAddress("glDebugMessageCallback") with
        | Some ptr ->
            // Setup callback
            Report.BeginTimed(4, "[GL] setting up debug callback")

            let errors = System.Collections.Generic.List<string>()

            let glDebugMessageCallback = Marshal.GetDelegateForFunctionPointer(ptr, typeof<GLDebugMessageCallbackDel>) |> unbox<GLDebugMessageCallbackDel>
            let callback = Marshal.PinDelegate <| DebugProc (debugCallback errors)
            glDebugMessageCallback.Invoke(callback.Pointer, ctx.Context.Handle)

            Report.End(4) |> ignore

            // Set messages
            Report.BeginTimed(4, "[GL] debug message control")

            enableMessages <| DebugSeverityControl.ofVerbosity verbosity

            Report.End(4) |> ignore

            Some { Mode = DebugOutputMode.Disabled; Callback = callback; Errors = errors }

        | _ ->
            None

    let isEnabled (debug : DebugOutput) =
        match debug.Mode with
        | DebugOutputMode.Enabled _ -> true | _ -> false

    let isSynchronous (debug : DebugOutput) =
        match debug.Mode with
        | DebugOutputMode.Enabled s -> s | _ -> false

    let enable (synchronous : bool) (debug : DebugOutput) =
        if not <| isEnabled debug then
            GL.Enable(EnableCap.DebugOutput)
            GL.Check "cannot enable debug output"

        if synchronous && not <| isSynchronous debug then
            GL.Enable(EnableCap.DebugOutputSynchronous)
            GL.Check "cannot enable synchronous debug output"

        { debug with Mode = DebugOutputMode.Enabled synchronous }

    let disable (debug : DebugOutput) =
        if isEnabled debug then
            GL.Disable(EnableCap.DebugOutput)
            GL.Check "cannot disable debug output"

            if isSynchronous debug then
                GL.Disable(EnableCap.DebugOutputSynchronous)
                GL.Check "cannot disable synchronous debug output"

        { debug with Mode = DebugOutputMode.Disabled }