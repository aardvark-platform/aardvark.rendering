namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Runtime.CompilerServices

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
    let dynamicCache = ConditionalWeakTable<obj, obj>()

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