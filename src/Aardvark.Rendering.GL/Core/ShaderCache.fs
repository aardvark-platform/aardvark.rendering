namespace Aardvark.Rendering.GL

open Aardvark.Rendering
open FSharp.Data.Adaptive
open System.Collections.Concurrent
open System.Runtime.CompilerServices

[<AutoOpen>]
module internal ShaderCacheKeys =
    open FShade
    open FShade.Imperative

    type CodeCacheKey =
        {
            id : string
            layout : FramebufferLayout
        }

    type StaticShaderCacheKey =
        {
            effect : Effect
            layout : FramebufferLayout
            topology : IndexedGeometryMode
            deviceCount : int
        }

    type DynamicShaderCacheKey =
        EffectConfig -> EffectInputLayout * aval<Module>

type internal ShaderCache() =

    let codeCache = ConcurrentDictionary<CodeCacheKey, obj>()
    let staticShaderCache = ConcurrentDictionary<StaticShaderCacheKey, obj>()
    let dynamicShaderCache = ConditionalWeakTable<DynamicShaderCacheKey, obj>()

    member x.GetOrAdd(key : CodeCacheKey, create : CodeCacheKey -> 'T) =
        codeCache.GetOrAdd(key, create >> box) |> unbox<'T>

    member x.GetOrAdd(key : StaticShaderCacheKey, create : StaticShaderCacheKey -> 'T) =
        staticShaderCache.GetOrAdd(key, create >> box) |> unbox<'T>

    member x.GetOrAdd(key : DynamicShaderCacheKey, create : DynamicShaderCacheKey -> 'T) =
        lock dynamicShaderCache (fun _ ->
            match dynamicShaderCache.TryGetValue key with
            | (true, s) -> unbox<'T> s
            | _ ->
                let s = create key
                dynamicShaderCache.Add(key, s :> obj)
                s
        )