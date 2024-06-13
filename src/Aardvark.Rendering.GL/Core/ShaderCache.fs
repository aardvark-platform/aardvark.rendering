namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Runtime.CompilerServices

[<AutoOpen>]
module internal ShaderCacheKeys =

    type EffectCacheKey =
        {
            id : string
            layout : FramebufferLayout
            topology : IndexedGeometryMode
            deviceCount : int
            layeredShaderInputs : bool
        }

    [<RequireQualifiedAccess>]
    type ShaderCacheKey =
        | Effect of EffectCacheKey
        | Compute of id: string

type internal ShaderCacheEntry(surface : IBackendSurface, destroy : unit -> unit) =
    member x.Surface = surface
    member x.Dispose() = destroy()

    member x.Equals(other : ShaderCacheEntry) =
        surface = other.Surface

    override x.Equals(other : obj) =
        match other with
        | :? ShaderCacheEntry as o -> x.Equals(o)
        | _ -> false

    override x.GetHashCode() =
        surface.GetHashCode()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type internal ShaderCache() =
    let staticCache = ConcurrentDictionary<ShaderCacheKey, Error<ShaderCacheEntry>>()
    let dynamicCache = ConditionalWeakTable<obj, obj>()

    static let box (destroy : 'T -> unit) (value : Error<'T>) : Error<ShaderCacheEntry> =
        match value with
        | Success v -> Success (new ShaderCacheEntry(v, fun () -> destroy v))
        | Error err -> Error err

    static let unbox (value : Error<ShaderCacheEntry>) : Error<'T> =
        match value with
        | Success v -> Success (unbox v.Surface)
        | Error err -> Error err

    member x.GetOrAdd<'T when 'T :> IBackendSurface>(key : ShaderCacheKey, create : ShaderCacheKey -> Error<'T>, destroy : 'T -> unit) : Error<'T> =
        staticCache.GetOrAdd(key, create >> box destroy) |> unbox

    member x.GetOrAdd(key: obj, signature: IFramebufferSignature, create: unit -> Error<'T>) : Error<'T> =
        lock dynamicCache (fun _ ->
            let perLayout =
                match dynamicCache.TryGetValue key with
                | (true, d) -> FSharp.Core.Operators.unbox d
                | _ ->
                    let d = Dictionary<FramebufferLayout, Error<'T>>()
                    dynamicCache.Add(key, d)
                    d

            perLayout.GetCreate(signature.Layout, ignore >> create)
        )

    member x.Entries =
        staticCache.Values
        |> Seq.choose (function
            | Success p -> Some p
            | _ -> None
        )
        |> Seq.distinct

    member x.Dispose() =
        for e in x.Entries do e.Dispose()
        staticCache.Clear()

    interface IDisposable with
        member x.Dispose() = x.Dispose()