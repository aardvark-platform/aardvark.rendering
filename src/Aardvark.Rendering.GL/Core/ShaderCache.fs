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

type internal ShaderCache() =
    let codeCache = ConcurrentDictionary<CodeCacheKey, Error<IBackendSurface>>()
    let effectCache = ConcurrentDictionary<EffectCacheKey, Error<IBackendSurface>>()

    static let box (value : Error<'T>) : Error<IBackendSurface> =
        match value with
        | Success v -> Success (v :> IBackendSurface)
        | Error err -> Error err

    static let unbox (value : Error<IBackendSurface>) : Error<'T> =
        match value with
        | Success v -> Success (unbox v)
        | Error err -> Error err

    member x.GetOrAdd<'T when 'T :> IBackendSurface>(key : CodeCacheKey, create : CodeCacheKey -> Error<'T>) : Error<'T> =
        codeCache.GetOrAdd(key, create >> box) |> unbox

    member x.GetOrAdd<'T when 'T :> IBackendSurface>(key : EffectCacheKey, create : EffectCacheKey -> Error<'T>) : Error<'T> =
        effectCache.GetOrAdd(key, create >> box) |> unbox

    member x.Programs =
        [ codeCache.Values; effectCache.Values ]
        |> Seq.concat
        |> Seq.choose (function
            | Success p -> Some p
            | _ -> None
        )
        |> Seq.distinct

    member x.Dispose() =
        for p in x.Programs do p.Dispose()
        codeCache.Clear()
        effectCache.Clear()

    interface IDisposable with
        member x.Dispose() = x.Dispose()