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
open Microsoft.FSharp.Linq
open Aardvark.Rendering
open Aardvark.Rendering.GL

[<AutoOpen>]
module private SamplerCounters =
    let addSampler (ctx : Context) =
        Interlocked.Increment(&ctx.MemoryUsage.SamplerCount) |> ignore

    let removeSampler (ctx : Context) =
        Interlocked.Decrement(&ctx.MemoryUsage.SamplerCount) |> ignore

type Sampler =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Description : SamplerState

        new(ctx : Context, handle : int, desc : SamplerState) =
            { Context = ctx; Handle = handle; Description = desc }
    end

[<AutoOpen>]
module SamplerExtensions =
    
    let private wrapModes =
        Dict.ofList [
            WrapMode.Wrap, TextureWrapMode.Repeat
            WrapMode.Border, TextureWrapMode.ClampToBorder
            WrapMode.Clamp, TextureWrapMode.ClampToEdge
            WrapMode.Mirror, TextureWrapMode.MirroredRepeat
        ]

    module private TextureFilter =
        let toMinFilter (f : TextureFilter) =
            let mip = TextureFilter.mipmapMode f
            let min = TextureFilter.minification f

            match min, mip with
            | FilterMode.Point, ValueNone                    -> TextureMinFilter.Nearest
            | FilterMode.Point, ValueSome FilterMode.Point   -> TextureMinFilter.NearestMipmapNearest
            | FilterMode.Point, ValueSome FilterMode.Linear  -> TextureMinFilter.NearestMipmapLinear
            | FilterMode.Linear, ValueNone                   -> TextureMinFilter.Linear
            | FilterMode.Linear, ValueSome FilterMode.Point  -> TextureMinFilter.LinearMipmapNearest
            | FilterMode.Linear, ValueSome FilterMode.Linear -> TextureMinFilter.LinearMipmapLinear
            | _ -> TextureMinFilter.Linear

        let toMagFilter (f : TextureFilter) =
            match TextureFilter.magnification f with
            | FilterMode.Point -> TextureMagFilter.Nearest
            | _ -> TextureMagFilter.Linear

    let private compareFuncs =
        Dict.ofList [
            ComparisonFunction.Always, All.Always
            ComparisonFunction.Equal, All.Equal
            ComparisonFunction.Greater, All.Greater
            ComparisonFunction.GreaterOrEqual, All.Gequal
            ComparisonFunction.Never, All.Never
            ComparisonFunction.NotEqual, All.Notequal
            ComparisonFunction.Less, All.Less
            ComparisonFunction.LessOrEqual, All.Lequal
        ]

    module SamplerStateHelpers =

        let wrapMode m =
            match wrapModes.TryGetValue m with
                | (true, r) -> int r
                | _ -> int TextureWrapMode.Repeat //failwithf "unsupported WrapMode: %A"  m

        let minFilter f =
            f |> TextureFilter.toMinFilter |> int

        let magFilter f =
            f |> TextureFilter.toMagFilter |> int

        let compareFunc f =
            match compareFuncs.TryGetValue f with
                | (true, f) -> int f
                | _ -> int All.Lequal //failwithf "unsupported comparison function: %A" f

    open SamplerStateHelpers

    let private setSamplerParameters (handle : int) (d : SamplerState) =
        GL.SamplerParameter(handle, SamplerParameterName.TextureBorderColor, [|d.BorderColor.R; d.BorderColor.G; d.BorderColor.B; d.BorderColor.A|])
        GL.Check "could not set BorderColor for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureWrapS, wrapMode d.AddressU)
        GL.Check "could not set TextureWrapS for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureWrapT, wrapMode d.AddressV)
        GL.Check "could not set TextureWrapT for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureWrapR, wrapMode d.AddressW)
        GL.Check "could not set TextureWrapR for sampler"


        GL.SamplerParameter(handle, SamplerParameterName.TextureLodBias, d.MipLodBias)
        GL.Check "could not set LodBias for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureMinLod, d.MinLod)
        GL.Check "could not set MinLod for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureMaxLod, d.MaxLod)
        GL.Check "could not set MinLod for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureMaxAnisotropyExt, d.MaxAnisotropy)
        GL.Check "could not set MaxAnisotropy for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureMinFilter, minFilter d.Filter)
        GL.Check "could not set MinFilter for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureMagFilter, magFilter d.Filter)
        GL.Check "could not set MagFilter for sampler"

        let cmpFunc = compareFunc d.Comparison

        if cmpFunc <> int All.Always then
            GL.SamplerParameter(handle, SamplerParameterName.TextureCompareMode, OpenTK.Graphics.OpenGL4.TextureCompareMode.CompareRefToTexture |> int)
            GL.Check "could not set comparison mode for sampler"

            GL.SamplerParameter(handle, SamplerParameterName.TextureCompareFunc, cmpFunc)
            GL.Check "could not set CompareFunc for sampler"


    type Context with

        member x.CreateSampler(description : SamplerState) =
            if ExecutionContext.samplersSupported then
                addSampler x
                using x.ResourceLock (fun _ ->
                    let handle = GL.GenSampler()
                    GL.Check "could not create sampler"

                    setSamplerParameters handle description

                    Sampler(x, handle, description)
                )
            else
                Sampler(x, -1, description)

        member x.Update(s : Sampler, description : SamplerState) =
            if ExecutionContext.samplersSupported then
                using x.ResourceLock (fun _ ->
                    setSamplerParameters s.Handle description
                )

        member x.Delete(s : Sampler) =
            if ExecutionContext.samplersSupported then
                removeSampler x
                using x.ResourceLock (fun _ ->
                    GL.DeleteSampler(s.Handle)
                    GL.Check "could not delete sampler"
                )
