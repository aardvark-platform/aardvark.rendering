namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering

open System
open System.Collections.Concurrent

[<AutoOpen>]
module internal ShaderCacheKeys =
    open FShade

    type CodeCacheKey =
        {
            id : string
            layout : FramebufferLayout
        }

    type EffectCacheKey =
        {
            effect : Effect
            layout : FramebufferLayout
            topology : IndexedGeometryMode
            deviceCount : int
        }

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
    let codeCache = ConcurrentDictionary<CodeCacheKey, Error<ShaderCacheEntry>>()
    let effectCache = ConcurrentDictionary<EffectCacheKey, Error<ShaderCacheEntry>>()

    static let box (destroy : 'T -> unit) (value : Error<'T>) : Error<ShaderCacheEntry> =
        match value with
        | Success v -> Success (new ShaderCacheEntry(v, fun () -> destroy v))
        | Error err -> Error err

    static let unbox (value : Error<ShaderCacheEntry>) : Error<'T> =
        match value with
        | Success v -> Success (unbox v.Surface)
        | Error err -> Error err

    member x.GetOrAdd<'T when 'T :> IBackendSurface>(key : CodeCacheKey, create : CodeCacheKey -> Error<'T>, destroy : 'T -> unit) : Error<'T> =
        codeCache.GetOrAdd(key, create >> box destroy) |> unbox

    member x.GetOrAdd<'T when 'T :> IBackendSurface>(key : EffectCacheKey, create : EffectCacheKey -> Error<'T>, destroy : 'T -> unit) : Error<'T> =
        effectCache.GetOrAdd(key, create >> box destroy) |> unbox

    member x.Entries =
        [ codeCache.Values; effectCache.Values ]
        |> Seq.concat
        |> Seq.choose (function
            | Success p -> Some p
            | _ -> None
        )
        |> Seq.distinct

    member x.Dispose() =
        for e in x.Entries do e.Dispose()
        codeCache.Clear()
        effectCache.Clear()

    interface IDisposable with
        member x.Dispose() = x.Dispose()