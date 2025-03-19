namespace Aardvark.Rendering.Vulkan

#nowarn "9"

open System
open System.IO
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open Microsoft.FSharp.NativeInterop
open EXTDebugUtils

type MessageSeverity =
    | Debug                 = 0x00000008
    | Information           = 0x00000004
    | Warning               = 0x00000002
    | Error                 = 0x00000001

type DebugMessage =
    {
        id              : Guid
        performance     : bool
        severity        : MessageSeverity
        layerPrefix     : string
        message         : string
        objects         : string list
    }

type DebugSummary =
    {
        messages   : Map<MessageSeverity, string list>
    }

    member x.MessageCounts =
        x.messages |> Map.map (fun _ -> List.length)

    member x.GetMessages(severity : MessageSeverity) =
        x.messages |> Map.tryFind severity |> Option.defaultValue []

    member x.ErrorMessages =
        x.GetMessages MessageSeverity.Error

    member x.HasErrors =
        x.ErrorMessages |> List.isEmpty |> not

[<AutoOpen>]
module private DebugReportHelpers =
    open System.Security.Cryptography
    open System.IO

    module DebugReportVerbosity =
        let toMessageSeverity =
            LookupTable.lookup [
                DebugReportVerbosity.Error, MessageSeverity.Error
                DebugReportVerbosity.Warning, MessageSeverity.Warning
                DebugReportVerbosity.Information, MessageSeverity.Information
                DebugReportVerbosity.Debug, MessageSeverity.Debug
            ]

    module VkDebugUtilsMessageSeverityFlagsEXT =
        let toMessageSeverity =
            LookupTable.lookup [
                VkDebugUtilsMessageSeverityFlagsEXT.ErrorBit, MessageSeverity.Error
                VkDebugUtilsMessageSeverityFlagsEXT.WarningBit, MessageSeverity.Warning
                VkDebugUtilsMessageSeverityFlagsEXT.InfoBit, MessageSeverity.Information
                VkDebugUtilsMessageSeverityFlagsEXT.VerboseBit, MessageSeverity.Debug
            ]

        let ofMessageSeverity =
            LookupTable.lookup [
                MessageSeverity.Error, VkDebugUtilsMessageSeverityFlagsEXT.ErrorBit
                MessageSeverity.Warning, VkDebugUtilsMessageSeverityFlagsEXT.WarningBit
                MessageSeverity.Information, VkDebugUtilsMessageSeverityFlagsEXT.InfoBit
                MessageSeverity.Debug, VkDebugUtilsMessageSeverityFlagsEXT.VerboseBit
            ]


    type VkDebugUtilsMessengerCallbackEXTDelegate =
        delegate of VkDebugUtilsMessageSeverityFlagsEXT * VkDebugUtilsMessageTypeFlagsEXT * nativeptr<VkDebugUtilsMessengerCallbackDataEXT> * nativeint -> int

    type VkDebugUtilsMessageSeverityFlagsEXT with
        static member All =
            VkDebugUtilsMessageSeverityFlagsEXT.ErrorBit |||
            VkDebugUtilsMessageSeverityFlagsEXT.WarningBit |||
            VkDebugUtilsMessageSeverityFlagsEXT.InfoBit |||
            VkDebugUtilsMessageSeverityFlagsEXT.VerboseBit

    type DebugReportAdapter internal(instance : Instance) =

        static let md5 = MD5.Create()

        static let computeHash (action : BinaryWriter -> unit) =
            use mem = new MemoryStream()
            use w = new BinaryWriter(mem)
            action w
            w.Flush()

            mem.ToArray() |> md5.ComputeHash |> Guid


        let mutable verbosity = MessageSeverity.Information
        let mutable tracingEnabled = false
        let mutable refCount = 0
        let mutable currentId = 0
        let observers = ConcurrentDictionary<int, IObserver<DebugMessage>>()
        let objectTraces = ConcurrentDictionary<uint64, string[]>()
        let messages = ConcurrentDictionary<MessageSeverity, string list>()

        let shutdown () =
            for (KeyValue(_,obs)) in observers do
                try obs.OnCompleted()
                with _ -> ()

        let raise (message : DebugMessage) =
            for (KeyValue(_,obs)) in observers do
                try obs.OnNext message
                with _ -> ()

        let callback (severity : VkDebugUtilsMessageSeverityFlagsEXT) (messageType : VkDebugUtilsMessageTypeFlagsEXT) (data : nativeptr<VkDebugUtilsMessengerCallbackDataEXT>) (userData : nativeint) =
            let severity = VkDebugUtilsMessageSeverityFlagsEXT.toMessageSeverity severity
            if severity <= verbosity then
                let data = NativePtr.read data

                let messageIdName =
                    if data.pMessageIdName <> NativePtr.zero then
                        data.pMessageIdName |> CStr.toString
                    else
                        ""
                let indent n = String.replicate n " "

                let objects =
                    data.pObjects
                    |> NativePtr.toList (int data.objectCount)
                    |> List.collect (fun o ->
                        let objectInfo =
                            let name = CStr.toString o.pObjectName

                            if String.IsNullOrEmpty name then
                                sprintf "%A (handle = 0x%016X)" o.objectType o.objectHandle
                            else
                                sprintf "%A (handle = 0x%016X, name = '%s')" o.objectType o.objectHandle name

                        let trace =
                            match objectTraces.TryGetValue(o.objectHandle) with
                            | (true, t) ->
                                t |> Array.toList |> List.map (fun str -> indent 4 + str)
                            | _ ->
                                []

                        let header =
                            match trace with
                            | [] -> objectInfo
                            | _ -> objectInfo + " created at:"

                        header :: trace
                        |> List.map (fun str -> indent 8 + str)
                    )

                let msg = data.pMessage |> CStr.toString
                messages.AddOrUpdate(severity, [msg], fun _ l -> List.append l [msg]) |> ignore

                let hash =
                    computeHash (fun w ->
                        w.Write(messageIdName)
                        w.Write(data.messageIdNumber)
                        w.Write(int messageType)
                        w.Write(int severity)
                    )

                raise {
                    id              = hash
                    performance     = messageType.HasFlag(VkDebugUtilsMessageTypeFlagsEXT.PerformanceBit)
                    severity        = severity
                    layerPrefix     = messageIdName
                    message         = msg
                    objects         = objects
                }

            0

        let callbackDelegate = VkDebugUtilsMessengerCallbackEXTDelegate(callback)
        let mutable gc = Unchecked.defaultof<GCHandle>
        let mutable callback = VkDebugUtilsMessengerEXT.Null

        let destroy() =
            let o = Interlocked.Exchange(&refCount, 0)
            if o <> 0 then
                shutdown()
                gc.Free()

                VkRaw.vkDestroyDebugUtilsMessengerEXT(instance.Handle, callback, NativePtr.zero)

                callback <- VkDebugUtilsMessengerEXT.Null
                observers.Clear()
                currentId <- 0

        let instanceDisposedHandler = Handler<unit>(fun _ () -> destroy())

        let add (obs : IObserver<DebugMessage>) =
            let id = Interlocked.Increment(&currentId)
            observers.TryAdd(id, obs) |> ignore
            let n = Interlocked.Increment(&refCount)
            if n = 1 then
                gc <- GCHandle.Alloc(callbackDelegate)
                let ptr = Marshal.GetFunctionPointerForDelegate(callbackDelegate)
                native {
                    let! pInfo =
                        VkDebugUtilsMessengerCreateInfoEXT(
                            VkDebugUtilsMessengerCreateFlagsEXT.None,
                            VkDebugUtilsMessageSeverityFlagsEXT.All,

                            VkDebugUtilsMessageTypeFlagsEXT.GeneralBit |||
                            VkDebugUtilsMessageTypeFlagsEXT.ValidationBit |||
                            VkDebugUtilsMessageTypeFlagsEXT.PerformanceBit,

                            ptr, 123n
                        )

                    let! pCallback = VkDebugUtilsMessengerEXT.Null

                    VkRaw.vkCreateDebugUtilsMessengerEXT(instance.Handle, pInfo, NativePtr.zero, pCallback)
                        |> check "vkCreateDebugUtilsMessengerEXT"

                    callback <- !!pCallback
                    instance.BeforeDispose.AddHandler(instanceDisposedHandler)
                }

            id

        let remove (id : int) =
            match observers.TryRemove id with
                | (true, obs) ->
                    obs.OnCompleted()
                    let n = Interlocked.Decrement(&refCount)
                    if n = 0 then
                        gc.Free()
                        VkRaw.vkDestroyDebugUtilsMessengerEXT(instance.Handle, callback, NativePtr.zero)
                        callback <- VkDebugUtilsMessengerEXT.Null
                        instance.BeforeDispose.RemoveHandler(instanceDisposedHandler)
                | _ ->
                    Report.Warn "[Vulkan] DebugReport Observer removed which was never added"

        let layer = CStr.malloc "DebugReport"

        member x.DebugSummary =
            let msg = messages |> Seq.map (fun (KeyValue(s, n)) -> s, n) |> Map.ofSeq
            { messages = msg }

        member x.Verbosity
            with get() = verbosity
            and set v = verbosity <- v

        member x.TracingEnabled
            with get() = tracingEnabled
            and set v = tracingEnabled <- v

        interface IObservable<DebugMessage> with
            member x.Subscribe (observer : IObserver<DebugMessage>) =
                let id = add observer
                { new IDisposable with
                    member x.Dispose() = remove id
                }

        member x.Raise(severity : MessageSeverity, msg : string) =
            let flags = VkDebugUtilsMessageSeverityFlagsEXT.ofMessageSeverity severity
            native {
                let! str = msg

                // Validation layer insists on a non-null string here for some reason...
                let! name = ""

                let! pObjectName =
                    VkDebugUtilsObjectNameInfoEXT(
                        VkObjectType.Instance, uint64 instance.Handle,
                        name
                    )

                let! pInfo =
                    VkDebugUtilsMessengerCallbackDataEXT(
                        VkDebugUtilsMessengerCallbackDataFlagsEXT.None,
                        layer, 0,
                        str,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero,
                        1u, pObjectName
                    )

                VkRaw.vkSubmitDebugUtilsMessageEXT(
                    instance.Handle,
                    flags,
                    VkDebugUtilsMessageTypeFlagsEXT.GeneralBit,
                    pInfo
                )
            }

        member x.TraceObject(handle : uint64) =
            let formatFrame (f : StackFrame) =
                let method = f.GetMethod()
                let fname = Path.GetFileName <| f.GetFileName()
                let line = f.GetFileLineNumber()
                sprintf "%A.%s() in %s:%d" method.DeclaringType method.Name fname line

            if tracingEnabled then
                let stack =
                    let trace = StackTrace(true)
                    let frames = trace.GetFrames()

                    frames
                    |> Array.map formatFrame
                    |> Array.skip (min 3 frames.Length)

                objectTraces.AddOrUpdate(handle, stack, fun _ _ -> stack) |> ignore

[<AbstractClass; Sealed>]
type InstanceDebugReportExtensions private() =
    static let table = ConditionalWeakTable<Instance, Option<DebugReportAdapter>>()

    static let notEnabledObservable =
        { new IObservable<DebugMessage> with
            member x.Subscribe (obs : IObserver<DebugMessage>) =
                obs.OnNext {
                    id              = Guid.Empty
                    severity        = MessageSeverity.Warning
                    performance     = false
                    layerPrefix     = "DR"
                    message         = "could not subscribe to DebugMessages since the instance does not provide the needed Extension"
                    objects         = []
                }
                obs.OnCompleted()
                Disposable.empty
        }

    static let getAdapter (instance : Instance) =
        if instance.DebugReportEnabled then
            lock table (fun () ->
                match table.TryGetValue instance with
                    | (true, adapter) -> adapter
                    | _ ->
                        let adapter = new DebugReportAdapter(instance)
                        table.Add(instance, Some adapter)
                        Some adapter
            )
        else
            None

    static let registerDebugTrace instance handle =
        match getAdapter instance with
        | Some a -> a.TraceObject(handle)
        | _ -> ()

    [<Extension>]
    static member GetDebugMessageObservable(this : Instance) =
        match getAdapter this with
        | Some a -> a :> IObservable<_>
        | _ -> notEnabledObservable

    [<Extension>]
    static member RaiseDebugMessage(this : Instance, severity : MessageSeverity, msg : string) =
        match getAdapter this with
        | Some a -> a.Raise(severity, msg)
        | _ -> ()

    [<Extension>]
    static member GetDebugSummary(this : Instance) =
        match getAdapter this with
        | Some a -> a.DebugSummary
        | _ -> { messages = Map.empty }

    [<Extension>]
    static member GetDebugVerbosity(this : Instance) =
        match getAdapter this with
        | Some a -> a.Verbosity
        | _ -> MessageSeverity.Error

    /// Returns whether object tracing is enabled.
    [<Extension>]
    static member GetDebugTracingEnabled(this : Instance) =
        match getAdapter this with
        | Some a -> a.TracingEnabled
        | _ -> false

    /// Sets the debug report verbosity.
    [<Extension>]
    static member SetDebugVerbosity(this : Instance, verbosity : DebugReportVerbosity, traceHandles : bool) =
        match getAdapter this with
        | Some a ->
            a.Verbosity <- DebugReportVerbosity.toMessageSeverity verbosity
            a.TracingEnabled <- traceHandles
        | _ -> ()

    /// Adds the object with the given handle for tracing its origin, which is displayed
    /// in debug messages.
    [<Extension>]
    static member RegisterDebugTrace(this : Instance, handle : uint64) =
        registerDebugTrace this handle

    /// Adds the object with the given handle for tracing its origin, which is displayed
    /// in debug messages.
    [<Extension>]
    static member RegisterDebugTrace(this : Instance, handle : nativeint) =
        registerDebugTrace this (uint64 handle)

    /// Adds the object with the given handle for tracing its origin, which is displayed
    /// in debug messages.
    [<Extension>]
    static member RegisterDebugTrace(this : Instance, handle : int64) =
        registerDebugTrace this (uint64 handle)

    /// Adds the object with the given handle for tracing its origin, which is displayed
    /// in debug messages.
    [<Extension>]
    static member RegisterDebugTrace<'a when 'a : unmanaged>(this : Instance, pHandle : nativeptr<'a>) =
        let handle : uint64 = pHandle |> NativePtr.cast |> NativePtr.read
        registerDebugTrace this handle

[<AutoOpen>]
module ``FSharp Style Debug Extensions`` =

    [<AutoOpen>]
    module private Output =

        [<AutoOpen>]
        module private Patterns =
            open System.Text.RegularExpressions

            let private debugPrintfRegex =
                Regex @".*DEBUG-PRINTF.*\| MessageID = 0x[0-9a-fA-F]+ \| (.*)"

            let (|DebugPrintf|_|) (msg : DebugMessage) =
                let m = debugPrintfRegex.Match msg.message
                if m.Success then
                    Some m.Groups.[1].Value
                else
                    None

        let private getMessageString = function
            | DebugPrintf str -> str
            | msg ->
                let str = (msg.message :: msg.objects) |> String.concat Environment.NewLine
                msg.layerPrefix + ": " + str

        let debugBreak (breakOnError : bool) (msg : DebugMessage) =
            if breakOnError then
                if Debugger.IsAttached then
                    Debugger.Break()

        let debugMessage (onError : DebugMessage -> unit) (msg : DebugMessage) =
            let str = getMessageString msg

            match msg.severity with
            | MessageSeverity.Error ->
                Report.Error("[Vulkan] {0}", str)
                onError msg

            | MessageSeverity.Warning ->
                Report.Warn("[Vulkan] {0}", str)

            | MessageSeverity.Information ->
                Report.Line("[Vulkan] {0}", str)

            | _ ->
                Report.Line("[Vulkan] DEBUG: {0}", str)

        let printDebugSummary (summary : DebugSummary) =
            let messages = summary.MessageCounts

            if not messages.IsEmpty then
                let counts =
                    messages
                    |> Seq.map (fun (KeyValue(s, n)) -> sprintf "%A: %d" s n)
                    |> Seq.reduce (fun a b -> a + ", " + b)

                Report.Begin("[Vulkan] Message summary")
                Report.Line(2, counts)

                if summary.HasErrors then
                    let errors =
                        let sep = Environment.NewLine + Environment.NewLine
                        summary.ErrorMessages |> String.concat sep

                    Report.ErrorNoPrefix(errors)

                Report.End() |> ignore

    type Instance with
        member x.DebugMessages = x.GetDebugMessageObservable()

        member x.DebugSummary = x.GetDebugSummary()

        member x.DebugVerbosity = x.GetDebugVerbosity()

        member x.DebugTracingEnabled = x.GetDebugTracingEnabled()

        member x.SetupDebugMessageOutput(config : DebugReportConfig option) =
            match config with
            | Some cfg ->
                let res =
                    x.DebugMessages.Subscribe {
                        new IObserver<_> with
                            member _.OnNext(msg) = msg |> debugMessage (debugBreak cfg.BreakOnError)
                            member _.OnCompleted() = printDebugSummary x.DebugSummary
                            member _.OnError _ = ()
                    }
                x.RaiseDebugMessage(MessageSeverity.Information, "Enabled debug report")
                x.SetDebugVerbosity(cfg.Verbosity, cfg.TraceObjectHandles)
                res

            | _ ->
                Disposable.empty

