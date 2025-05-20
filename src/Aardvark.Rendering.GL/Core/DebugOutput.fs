namespace Aardvark.Rendering.GL

open System
open System.Runtime.InteropServices
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering.GL

type OpenGLException =
    inherit Exception
    val Error: ErrorCode

    new () =
        OpenGLException("An error occurred.")

    new (message: string) =
        { inherit Exception(message); Error = ErrorCode.NoError }

    new (message: string, innerException: exn) =
        { inherit Exception(message, innerException); Error = ErrorCode.NoError }

    new (error: ErrorCode) =
        OpenGLException(error, null, null)

    new (error: ErrorCode, message: string) =
        OpenGLException(error, message, null)

    new (error: ErrorCode, message: string, innerException: exn) =
        let message = if String.IsNullOrEmpty message then "An error occurred" else message
        { inherit Exception($"{message} (Error: {error})", innerException); Error = error }

[<AutoOpen>]
module Error =

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

type internal DebugOutput private (context: OpenTK.ContextHandle) =
    let mutable mode = DebugOutputMode.Disabled
    let errors = ResizeArray<string>()
    let mutable callback = null

    static member TryInitialize() =
        let ctx = GraphicsContext.CurrentContext |> unbox<IGraphicsContextInternal>

        if ExtensionHelpers.isSupported (Version(4, 3)) "GL_KHR_debug" then
            Some <| DebugOutput(ctx.Context)
        else
            None

    member _.Mode = mode
    member inline this.IsEnabled = this.Mode <> DebugOutputMode.Disabled
    member inline this.IsSynchronous = match this.Mode with | DebugOutputMode.Enabled s -> s | _ -> false

    member _.GetErrors() =
        lock errors (fun _ -> errors.ToArray())

    member this.Enable(synchronous: bool, verbosity: DebugOutputSeverity) =
        if not this.IsEnabled then
            GL.Enable(EnableCap.DebugOutput)
            GL.Check "cannot enable debug output"

            callback <- DebugProc (debugCallback errors)
            GL.DebugMessageCallback(callback, context.Handle)
            GL.Check "failed to set debug message callback"

        if synchronous && not <| this.IsSynchronous then
            GL.Enable(EnableCap.DebugOutputSynchronous)
            GL.Check "cannot enable synchronous debug output"

        let severities =
            [
                DebugSeverityControl.DebugSeverityHigh, true
                DebugSeverityControl.DebugSeverityMedium, (verbosity <= DebugOutputSeverity.Medium)
                DebugSeverityControl.DebugSeverityLow, (verbosity <= DebugOutputSeverity.Low)
                DebugSeverityControl.DebugSeverityNotification, (verbosity <= DebugOutputSeverity.Notification)
            ]

        for severity, enabled in severities do
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, severity, 0, (null: uint32[]), enabled)
            GL.Check "failed to set debug message control"

        mode <- DebugOutputMode.Enabled synchronous

    member this.Disable() =
        if this.IsEnabled then
            GL.Disable(EnableCap.DebugOutput)
            GL.Check "cannot disable debug output"
            callback <- null

            if this.IsSynchronous then
                GL.Disable(EnableCap.DebugOutputSynchronous)
                GL.Check "cannot disable synchronous debug output"

        mode <- DebugOutputMode.Disabled

    member _.Print(typ: DebugType, severity: DebugSeverity, id: int, message: string) =
        GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, typ, id, severity, -1, message)
        GL.Check "cannot insert debug message"

    member _.PushGroup(message: string) =
        GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 0, -1, message)
        GL.Check "cannot push debug group"

    member _.PopGroup() =
        GL.PopDebugGroup()
        GL.Check "cannot pop debug group"