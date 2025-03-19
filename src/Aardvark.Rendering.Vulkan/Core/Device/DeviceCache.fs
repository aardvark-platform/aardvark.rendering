namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base

type ICachedResource =
    inherit IResource
    abstract member Cache : IDeviceCache option with get, set

and IDeviceCache =
    abstract member Name : Symbol

type IDeviceCache<'Resource> =
    inherit IDeviceCache
    abstract member Revoke : 'Resource -> unit

type DeviceCache<'Value, 'Resource when 'Resource :> ICachedResource>(name: Symbol, onDispose: IObservable<unit>) =
    let store = Dict<'Value, 'Resource>()
    let back = Dict<'Resource, 'Value>()

    do onDispose.Add(fun _ ->
            for k in back.Keys do
                if k.ReferenceCount > 1 then
                    Log.warn "[Vulkan] Cached resource %A still has %d references" k (k.ReferenceCount - 1)
                k.Dispose()
            store.Clear()
            back.Clear()
        )

    member x.Invoke(value: 'Value, create: 'Value -> 'Resource) : 'Resource =
        lock store (fun () ->
            let res =
                match store.TryGetValue value with
                | true, r -> r
                | _ ->
                    let r = create value
                    r.Cache <- Some x
                    back.[r] <- value
                    store.[value] <- r
                    r

            res.AddReference()
            res
        )

    member x.Revoke(resource: 'Resource) : unit =
        lock store (fun () ->
            match back.TryRemove resource with
            | true, key ->
                store.Remove key |> ignore
                resource.Dispose()
            | _ ->
                Log.warn "[Vulkan] Cached resource to be removed not found"
        )

    interface IDeviceCache<'Resource> with
        member x.Name = name
        member x.Revoke b = x.Revoke b