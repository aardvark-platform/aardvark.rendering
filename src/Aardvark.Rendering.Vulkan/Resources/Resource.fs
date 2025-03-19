namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System.Threading

[<AbstractClass>]
type Resource =
    class
        val public Device : Device
        val mutable private refCount : int

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

        member x.Dispose() =
            let refs = Interlocked.Decrement(&x.refCount)
            if refs < 0 then
                Log.warn $"[Vulkan] Resource {x} has negative reference count ({refs})"
            elif refs = 0 then
                x.Destroy()

        abstract member IsValid : bool
        default x.IsValid =
            not x.Device.IsDisposed && x.refCount > 0

        abstract member Destroy : unit -> unit

        new(device: Device) = { Device = device; refCount = 1 }
        new(device: Device, referenceCount: int) = { Device = device; refCount = referenceCount }

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