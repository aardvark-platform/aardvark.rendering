namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Collections.Concurrent
open Aardvark.Base
open EXTDebugReport

type MessageSeverity =
    | Information           = 0x00000001
    | Warning               = 0x00000002
    | PerformanceWarning    = 0x00000004
    | Error                 = 0x00000008
    | Debug                 = 0x00000010

type ObjectType = 
    | Unknown = 0
    | Instance = 1
    | PhysicalDevice = 2
    | Device = 3
    | Queue = 4
    | Semaphore = 5
    | CommandBuffer = 6
    | Fence = 7
    | DeviceMemory = 8
    | Buffer = 9
    | Image = 10
    | Event = 11
    | QueryPool = 12
    | BufferView = 13
    | ImageView = 14
    | ShaderModule = 15
    | PipelineCache = 16
    | PipelineLayout = 17
    | RenderPass = 18
    | Pipeline = 19
    | DescriptorSetLayout = 20
    | Sampler = 21
    | DescriptorPool = 22
    | DescriptorSet = 23
    | Framebuffer = 24
    | CommandPool = 25
    | SurfaceKhr = 26
    | SwapchainKhr = 27
    | DebugReport = 28

type DebugMessage = 
    { 
        id              : Guid
        severity        : MessageSeverity
        objectType      : ObjectType 
        sourceObject    : uint64 
        layerPrefix     : string
        message         : string
    }

[<AutoOpen>]
module private DebugReportHelpers =
    open EXTDebugReport

    [<AutoOpen>]
    module EnumExtensions =
        type VkDebugReportFlagBitsEXT with
            static member All =
                VkDebugReportFlagBitsEXT.VkDebugReportDebugBitExt |||
                VkDebugReportFlagBitsEXT.VkDebugReportErrorBitExt |||
                VkDebugReportFlagBitsEXT.VkDebugReportInformationBitExt |||
                VkDebugReportFlagBitsEXT.VkDebugReportPerformanceWarningBitExt |||
                VkDebugReportFlagBitsEXT.VkDebugReportWarningBitExt

    type VkDebugReportCallbackEXTDelegate = 
        delegate of 
            VkDebugReportFlagBitsEXT * VkDebugReportObjectTypeEXT * 
            uint64 * uint64 * int * cstr * cstr * nativeint -> uint32


    type DebugReportAdapter internal(instance : Instance) =
        let flags = VkDebugReportFlagBitsEXT.All
//        let load (name : string) : 'a =
//            let ptr = VkRaw.vkGetInstanceProcAddr(instance.Handle, name)
//            if ptr = 0n then failf "could not get %s" name
//            else Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>

        static let md5 = new System.Security.Cryptography.MD5Cng()
        static let ignoreRx = System.Text.RegularExpressions.Regex @"vkBeginCommandBuffer\(\)[ \t]*:[ \t]*Secondary[ \t]+Command[ \t]+Buffers[ \t]+\(0x[0-9A-Fa-f]+\)[ \t]+may[ \t]+perform[ \t]+better[ \t]+if[ \t]+a[ \t]+valid[ \t]+framebuffer[ \t]+parameter[ \t]+is[ \t]+specified\."

        let mutable refCount = 0
        let mutable currentId = 0
        let observers = ConcurrentDictionary<int, IObserver<DebugMessage>>()

        let shutdown () = 
            for (KeyValue(_,obs)) in observers do
                obs.OnCompleted()

        let raise (message : DebugMessage) = 
            for (KeyValue(_,obs)) in observers do
                obs.OnNext message
//
//        let computeHash (f : BinaryWriter -> unit) =
//            use ms = new MemoryStream()
//            f (new BinaryWriter(ms, Text.Encoding.UTF8, true))
//            ms.ToArray() |> md5.ComputeHash |> Guid
//


        let callback (flags : VkDebugReportFlagBitsEXT) (objType : VkDebugReportObjectTypeEXT) (srcObject : uint64) (location : uint64) (msgCode : int) (layerPrefix : cstr) (msg : cstr) (userData : nativeint) =
            let layerPrefix = layerPrefix |> CStr.toString
            let msg = msg |> CStr.toString

            let hash = Guid.Empty
//                computeHash (fun w ->
//                    w.Write (int flags)
//                    w.Write (int objType)
//                    w.Write msgCode
//                    w.Write layerPrefix
//                    w.Write location
//                )

            if not (ignoreRx.IsMatch msg) then
                raise {
                    id              = hash
                    severity        = unbox (int flags)
                    objectType      = unbox (int objType)
                    sourceObject    = srcObject
                    layerPrefix     = layerPrefix
                    message         = msg
                }

            0u

        let callbackDelegate = VkDebugReportCallbackEXTDelegate(callback)
        let mutable gc = Unchecked.defaultof<GCHandle>
        let mutable callback = VkDebugReportCallbackEXT.Null

        let destroy() =
            let o = Interlocked.Exchange(&refCount, 0)
            if o <> 0 then
                shutdown()
                gc.Free()
                VkRaw.vkDestroyDebugReportCallbackEXT(instance.Handle, callback, NativePtr.zero)
                callback <- VkDebugReportCallbackEXT.Null
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
                let mutable info =
                    VkDebugReportCallbackCreateInfoEXT(
                        unbox 1000011000, 0n,
                        uint32 flags,
                        ptr,
                        0n
                    )
                VkRaw.vkCreateDebugReportCallbackEXT(instance.Handle, &&info, NativePtr.zero, &&callback)
                    |> check "vkDbgCreateMsgCallback"

                instance.BeforeDispose.AddHandler(instanceDisposedHandler)

            id

        let remove (id : int) =
            match observers.TryRemove id with
                | (true, obs) ->
                    let n = Interlocked.Decrement(&refCount)
                    if n = 0 then
                        gc.Free()
                        VkRaw.vkDestroyDebugReportCallbackEXT(instance.Handle, callback, NativePtr.zero)
                        callback <- VkDebugReportCallbackEXT.Null
                        instance.BeforeDispose.RemoveHandler(instanceDisposedHandler)
                | _ ->
                    Report.Warn "[Vulkan] DebugReport Observer removed which was never added"

        interface IObservable<DebugMessage> with
            member x.Subscribe (observer : IObserver<DebugMessage>) =
                let id = add observer
                { new IDisposable with
                    member x.Dispose() = remove id
                }
                

[<AbstractClass; Sealed; Extension>]
type InstanceExtensions private() =
    static let table = new ConditionalWeakTable<Instance, IObservable<DebugMessage>>()

    static let notEnabledObservable =
        { new IObservable<DebugMessage> with
            member x.Subscribe (obs : IObserver<DebugMessage>) =
                obs.OnNext {
                    id              = Guid.Empty
                    severity        = MessageSeverity.Warning
                    objectType      = ObjectType.DebugReport
                    sourceObject    = 0UL
                    layerPrefix     = "DR"
                    message         = "could not subscribe to DebugMessages since the instance does not provide the needed Extension"
                }
                obs.OnCompleted()
                { new IDisposable with member x.Dispose() = () }
        }

    static let getMessageObservable (instance : Instance) =
        lock table (fun () ->
            match table.TryGetValue instance with
                | (true, adapter) -> adapter
                | _ ->
                    if Set.contains Instance.Extensions.DebugReport instance.EnabledExtensions then
                        let adapter = new DebugReportAdapter(instance) :> IObservable<_>
                        table.Add(instance, adapter)
                        adapter
                    else
                       notEnabledObservable 
        )

    [<Extension>]
    static member GetDebugMessageObservable(this : Instance) =
        getMessageObservable this

[<AutoOpen>]
module ``FSharp Style Debug Extensions`` =
    type Instance with
        member x.DebugMessages = x.GetDebugMessageObservable()
