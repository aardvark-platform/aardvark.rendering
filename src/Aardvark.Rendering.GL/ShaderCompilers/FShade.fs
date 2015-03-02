namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations
open System.Collections.Generic
open FShade
open FShade.Compiler

[<AutoOpen>]
module FShadeExtensions =

    let private wrapModes =
        Dict.ofList [
            WrapMode.Border, Aardvark.Base.WrapMode.Border
            WrapMode.Clamp, Aardvark.Base.WrapMode.Clamp
            WrapMode.Mirror, Aardvark.Base.WrapMode.Mirror
            WrapMode.MirrorOnce, Aardvark.Base.WrapMode.MirrorOnce
            WrapMode.Wrap, Aardvark.Base.WrapMode.Wrap
        ]

    let private filterModes =
        Dict.ofList [
            Filter.Anisotropic, TextureFilter.Anisotropic
            Filter.MinLinearMagMipPoint, TextureFilter.MinLinearMagMipPoint
            Filter.MinLinearMagPointMipLinear, TextureFilter.MinLinearMagPointMipLinear
            Filter.MinMagLinearMipPoint, TextureFilter.MinMagLinearMipPoint
            Filter.MinMagMipLinear, TextureFilter.MinMagMipLinear
            Filter.MinMagMipPoint, TextureFilter.MinMagMipPoint
            Filter.MinMagPointMipLinear, TextureFilter.MinMagPointMipLinear
            Filter.MinPointMagLinearMipPoint, TextureFilter.MinPointMagLinearMipPoint
            Filter.MinPointMagMipLinear, TextureFilter.MinPointMagMipLinear
            Filter.MinMagPoint, TextureFilter.MinMagPoint
            Filter.MinMagLinear, TextureFilter.MinMagLinear
            Filter.MinPointMagLinear, TextureFilter.MinPointMagLinear
            Filter.MinLinearMagPoint, TextureFilter.MinLinearMagPoint
        ]

    let private compareFuncs =
        Dict.ofList [
            ComparisonFunction.Always, SamplerComparisonFunction.Always
            ComparisonFunction.Equal, SamplerComparisonFunction.Equal
            ComparisonFunction.Greater, SamplerComparisonFunction.Greater
            ComparisonFunction.GreaterOrEqual, SamplerComparisonFunction.GreaterOrEqual
            ComparisonFunction.Less, SamplerComparisonFunction.Less
            ComparisonFunction.LessOrEqual, SamplerComparisonFunction.LessOrEqual
            ComparisonFunction.Never, SamplerComparisonFunction.Never
            ComparisonFunction.NotEqual, SamplerComparisonFunction.NotEqual
        ]


    let private toWrapMode w =
        match wrapModes.TryGetValue w with
            | (true, m) -> m
            | _ -> failwithf "unknown wrap mode: %A" w

    let private toFilterMode f =
        match filterModes.TryGetValue f with
            | (true, f) -> f
            | _ -> failwithf "unknown filter mode: %A" f

    let private toCompareFunc f =
        match compareFuncs.TryGetValue f with
            | (true, f) -> f
            | _ -> failwithf "unknown compare function: %A" f


    let private toSamplerState (sam : SamplerState) : SamplerStateDescription =
        let d = SamplerStateDescription()

        sam.AddressU |> Option.iter (fun v -> d.AddressU <- toWrapMode v)
        sam.AddressV |> Option.iter (fun v -> d.AddressV <- toWrapMode v)
        sam.AddressW |> Option.iter (fun v -> d.AddressW <- toWrapMode v)
        sam.Filter |> Option.iter (fun v -> d.Filter <- toFilterMode v)

        sam.BorderColor |> Option.iter (fun v -> d.BorderColor <- v)
        sam.MaxAnisotropy |> Option.iter (fun v -> d.MaxAnisotropy <- v)
        sam.MaxLod |> Option.iter (fun v -> d.MaxLod <- float32 v)
        sam.MinLod |> Option.iter (fun v -> d.MinLod <- float32 v)
        sam.MipLodBias |> Option.iter (fun v -> d.MipLodBias <- float32 v)

        d

    let private toSamplerComparisonState (sam : SamplerComparisonState) : SamplerStateDescription =
        let d = SamplerStateDescription()

        sam.AddressU |> Option.iter (fun v -> d.AddressU <- toWrapMode v)
        sam.AddressV |> Option.iter (fun v -> d.AddressV <- toWrapMode v)
        sam.AddressW |> Option.iter (fun v -> d.AddressW <- toWrapMode v)
        sam.Filter |> Option.iter (fun v -> d.Filter <- toFilterMode v)

        sam.BorderColor |> Option.iter (fun v -> d.BorderColor <- v)
        sam.MaxAnisotropy |> Option.iter (fun v -> d.MaxAnisotropy <- v)
        sam.MaxLod |> Option.iter (fun v -> d.MaxLod <- float32 v)
        sam.MinLod |> Option.iter (fun v -> d.MinLod <- float32 v)
        sam.MipLodBias |> Option.iter (fun v -> d.MipLodBias <- float32 v)
        sam.Comparison |> Option.iter (fun v -> d.ComparisonFunction <- toCompareFunc v)

        d

    type Context with

        member x.TryCompileProgram(s : Compiled<Effect, ShaderState>) =
            match GLSL.compileEffect s with
                | Success(uniformMap, code) ->
                    match x.TryCompileProgram(code) with
                        | Success p ->

                            let samplerStates = SymDict.empty
                            let uniforms = SymDict.empty
                            for (KeyValue(k,v)) in uniformMap do
                                if not v.IsSamplerUniform then
                                    uniforms.[Symbol.Create(k)] <- v.Value
                                else
                                    match v.Value with
                                        | :? (string * SamplerState) as tup ->
                                            let (sem, sam) = tup
                                            samplerStates.[Symbol.Create sem] <- toSamplerState sam
                            
                                        | :? (string * SamplerComparisonState) as tup ->
                                            let (sem, sam) = tup
                                            samplerStates.[Symbol.Create sem] <- toSamplerComparisonState sam

                                        | _ ->
                                            Log.warn "unknown sampler uniform: %A" v.Value
                            

                            Success { p with UniformGetters = uniforms; SamplerStates = samplerStates }

                        | Error e ->
                            Error e
                | Error e ->
                    Error e
