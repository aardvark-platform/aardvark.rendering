namespace Aardvark.Rendering.Vulkan

#nowarn "9"
// #nowarn "51"

open System
open System.IO
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open Microsoft.FSharp.NativeInterop
open EXTDebugReport
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
    }

[<AutoOpen>]
module private DebugReportHelpers =
    open System.Security.Cryptography
    open System.IO

    module VkDebugUtilsMessageSeverityFlagsEXT =
        let toMessageSeverity =
            LookupTable.lookupTable [
                VkDebugUtilsMessageSeverityFlagsEXT.ErrorBit, MessageSeverity.Error
                VkDebugUtilsMessageSeverityFlagsEXT.WarningBit, MessageSeverity.Warning
                VkDebugUtilsMessageSeverityFlagsEXT.InfoBit, MessageSeverity.Information
                VkDebugUtilsMessageSeverityFlagsEXT.VerboseBit, MessageSeverity.Debug
            ]

        let ofMessageSeverity =
            LookupTable.lookupTable [
                MessageSeverity.Error, VkDebugUtilsMessageSeverityFlagsEXT.ErrorBit
                MessageSeverity.Warning, VkDebugUtilsMessageSeverityFlagsEXT.WarningBit
                MessageSeverity.Information, VkDebugUtilsMessageSeverityFlagsEXT.InfoBit
                MessageSeverity.Debug, VkDebugUtilsMessageSeverityFlagsEXT.VerboseBit
            ]


    [<AutoOpen>]
    module EnumExtensions =
        type VkDebugReportFlagsEXT with
            static member All =
                VkDebugReportFlagsEXT.DebugBit |||
                VkDebugReportFlagsEXT.ErrorBit |||
                VkDebugReportFlagsEXT.InformationBit |||
                VkDebugReportFlagsEXT.PerformanceWarningBit |||
                VkDebugReportFlagsEXT.WarningBit

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
        let mutable refCount = 0
        let mutable currentId = 0
        let observers = ConcurrentDictionary<int, IObserver<DebugMessage>>()
        let objectTraces = ConcurrentDictionary<uint64, string[]>()

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
                            | (true, t) when t.Length > 0 ->
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

                let msg =
                    let m = data.pMessage |> CStr.toString
                    (m :: objects) |> List.reduce (fun a b -> a + Environment.NewLine + b)

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
                            VkDebugUtilsMessengerCreateFlagsEXT.MinValue,
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
                    let n = Interlocked.Decrement(&refCount)
                    if n = 0 then
                        gc.Free()
                        VkRaw.vkDestroyDebugUtilsMessengerEXT(instance.Handle, callback, NativePtr.zero)
                        callback <- VkDebugUtilsMessengerEXT.Null
                        instance.BeforeDispose.RemoveHandler(instanceDisposedHandler)
                | _ ->
                    Report.Warn "[Vulkan] DebugReport Observer removed which was never added"

        let layer = CStr.malloc "DebugReport"

        member x.Verbosity
            with get() = verbosity
            and set v = verbosity <- v

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
                        VkDebugUtilsMessengerCallbackDataFlagsEXT.MinValue,
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
            let stack =
                let trace = StackTrace(true)
                let frames = trace.GetFrames()

                frames
                |> Array.map (fun f ->
                    let method = f.GetMethod()
                    let fname = Path.GetFileName <| f.GetFileName()
                    let line = f.GetFileLineNumber()
                    sprintf "%A.%s() in %s:%d" method.DeclaringType method.Name fname line
                )
                |> Array.skip (min 3 frames.Length)

            objectTraces.AddOrUpdate(handle, stack, fun _ _ -> stack) |> ignore

[<AbstractClass; Sealed; Extension>]
type InstanceExtensions private() =
    static let table = new ConditionalWeakTable<Instance, Option<DebugReportAdapter>>()

    static let notEnabledObservable =
        { new IObservable<DebugMessage> with
            member x.Subscribe (obs : IObserver<DebugMessage>) =
                obs.OnNext {
                    id              = Guid.Empty
                    severity        = MessageSeverity.Warning
                    performance     = false
                    layerPrefix     = "DR"
                    message         = "could not subscribe to DebugMessages since the instance does not provide the needed Extension"
                }
                obs.OnCompleted()
                { new IDisposable with member x.Dispose() = () }
        }

    static let getAdapter (instance : Instance) =
        lock table (fun () ->
            match table.TryGetValue instance with
                | (true, adapter) -> adapter
                | _ ->
                    if List.contains Instance.Extensions.DebugUtils instance.EnabledExtensions then
                        let adapter = new DebugReportAdapter(instance)
                        table.Add(instance, Some adapter)
                        Some adapter
                    else
                        None
        )

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
    static member GetDebugVerbosity(this : Instance) =
        match getAdapter this with
        | Some a -> a.Verbosity
        | _ -> MessageSeverity.Error

    [<Extension>]
    static member SetDebugVerbosity(this : Instance, v : MessageSeverity) =
        match getAdapter this with
        | Some a -> a.Verbosity <- v
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
    type Instance with
        member x.DebugMessages = x.GetDebugMessageObservable()

        member x.DebugVerbosity
            with get() = x.GetDebugVerbosity()
            and set v = x.SetDebugVerbosity(v)

