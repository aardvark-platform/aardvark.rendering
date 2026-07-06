namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System.Threading

// Instrumentation backing store for Resource.LiveCount. Counts every live (created
// but not yet destroyed) backend Resource<'T> handle — buffers, images, image views,
// samplers, uniform buffers, query pools, etc. Used by leak-detection probes.
module internal ResourceInstrumentation =
    let mutable liveCountRef = 0

    /// Opt-in (AARDVARK_TRACE_RESOURCE_REFS=1): record creation / AddReference /
    /// Dispose stacks per resource and dump them when a reference count goes
    /// negative. Costs one branch when disabled.
    let traceRefs =
        match System.Environment.GetEnvironmentVariable "AARDVARK_TRACE_RESOURCE_REFS" with
        | null | "" -> false
        | s -> let s = s.Trim().ToLowerInvariant() in s = "1" || s = "true" || s = "on"

    let private stacks = System.Runtime.CompilerServices.ConditionalWeakTable<obj, System.Collections.Generic.List<string>>()

    let record (o : obj) (label : string) =
        if traceRefs then
            let l = stacks.GetOrCreateValue o
            lock l (fun () -> l.Add(label + "\n" + System.Diagnostics.StackTrace(2, true).ToString()))

    let dump (o : obj) =
        if traceRefs then
            match stacks.TryGetValue o with
            | true, l -> lock l (fun () -> String.concat "\n────────\n" l)
            | _ -> "(no recorded stacks)"
        else "(set AARDVARK_TRACE_RESOURCE_REFS=1 to record creation/addref/dispose stacks)"

[<AbstractClass>]
type Resource =
    class
        val public Device : Device
        val mutable private refCount : int

        /// Net count of live backend Resource handles across the whole device.
        static member LiveCount = ResourceInstrumentation.liveCountRef

        member x.ReferenceCount =
            x.refCount

        /// Increments the reference count only if it greater than zero.
        /// Returns if the reference count was incremented.
        member x.TryAddReference() =
            let mutable current = x.refCount

            while current > 0 && Interlocked.CompareExchange(&x.refCount, current + 1, current) <> current do
                current <- x.refCount

            current > 0

        member x.AddReference() =
            Interlocked.Increment(&x.refCount) |> ignore
            if ResourceInstrumentation.traceRefs then ResourceInstrumentation.record x $"addref -> {x.refCount}"

        member x.Dispose() =
            let refs = Interlocked.Decrement(&x.refCount)
            if ResourceInstrumentation.traceRefs then ResourceInstrumentation.record x $"dispose -> {refs}"
            if refs < 0 then
                Log.warn $"[Vulkan] Resource {x} has negative reference count ({refs})"
                Log.warn $"[Vulkan] ref history of {x}:\n{ResourceInstrumentation.dump x}"
            elif refs = 0 then
                Interlocked.Decrement(&ResourceInstrumentation.liveCountRef) |> ignore
                x.Destroy()

        abstract member IsValid : bool
        default x.IsValid =
            not x.Device.IsDisposed && x.refCount > 0

        abstract member Destroy : unit -> unit

        new(device: Device) as x =
            Interlocked.Increment(&ResourceInstrumentation.liveCountRef) |> ignore
            { Device = device; refCount = 1 }
            then if ResourceInstrumentation.traceRefs then ResourceInstrumentation.record x "created (refs=1)"
        new(device: Device, referenceCount: int) as x =
            Interlocked.Increment(&ResourceInstrumentation.liveCountRef) |> ignore
            { Device = device; refCount = referenceCount }
            then if ResourceInstrumentation.traceRefs then ResourceInstrumentation.record x $"created (refs={referenceCount})"

        interface IResource with
            member x.AddReference() = x.AddReference()
            member x.ReferenceCount = x.ReferenceCount
            member x.Dispose() = x.Dispose()
    end

[<AbstractClass>]
type Resource<'T when 'T : unmanaged and 'T : equality> =
    class
        inherit Resource
        val mutable private handle : 'T

        member x.Handle
            with get () = x.handle
            and set value = x.handle <- value

        override x.IsValid =
            base.IsValid && x.handle <> Unchecked.defaultof<_>

        new(device: Device, handle: 'T, refCount: int) =
            handle |> NativePtr.pin device.Instance.RegisterDebugTrace
            { inherit Resource(device, refCount); handle = handle }

        new(device: Device, handle: 'T) =
            new Resource<_>(device, handle, 1)

        interface IResource<'T> with
            member x.Handle = x.Handle
    end

[<AbstractClass>]
type CachedResource =
    class
        inherit Resource
        val mutable private cache : IDeviceCache option

        member x.Cache
            with get () = x.cache
            and set value = x.cache <- value

        new(device: Device) = { inherit Resource(device); cache = None }
        new(device: Device, cache: IDeviceCache) = { inherit Resource(device); cache = Some cache }

        interface ICachedResource with
            member x.Cache
                with get() = x.Cache
                and set value = x.Cache <- value
    end