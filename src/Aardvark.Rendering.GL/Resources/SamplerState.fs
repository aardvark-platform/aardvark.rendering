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
open Microsoft.FSharp.Linq.QuotationEvaluation


type Sampler =
    class
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Description : SamplerStateDescription

        new(ctx : Context, handle : int, desc : SamplerStateDescription) =
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

    let private minFilters =
        Dict.ofList [
            (TextureFilterMode.Linear, TextureFilterMode.Linear), TextureMinFilter.LinearMipmapLinear
            (TextureFilterMode.Linear, TextureFilterMode.Point), TextureMinFilter.LinearMipmapNearest
            (TextureFilterMode.Linear, TextureFilterMode.None), TextureMinFilter.Linear

            (TextureFilterMode.Point, TextureFilterMode.Linear), TextureMinFilter.NearestMipmapLinear
            (TextureFilterMode.Point, TextureFilterMode.Point), TextureMinFilter.NearestMipmapNearest
            (TextureFilterMode.Point, TextureFilterMode.None), TextureMinFilter.Nearest
        ]

    let private magFilters =
        Dict.ofList [
            TextureFilterMode.Linear, TextureMagFilter.Linear
            TextureFilterMode.Point, TextureMagFilter.Nearest
        ]

    let private compareFuncs =
        Dict.ofList [
            SamplerComparisonFunction.Always, All.Always
            SamplerComparisonFunction.Equal, All.Equal
            SamplerComparisonFunction.Greater, All.Greater
            SamplerComparisonFunction.GreaterOrEqual, All.Gequal
            SamplerComparisonFunction.Never, All.Never
            SamplerComparisonFunction.NotEqual, All.Notequal
            SamplerComparisonFunction.Less, All.Less
            SamplerComparisonFunction.LessOrEqual, All.Lequal
        ]

    let private wrapMode m =
        match wrapModes.TryGetValue m with
            | (true, r) -> int r
            | _ -> int TextureWrapMode.Repeat //failwithf "unsupported WrapMode: %A"  m

    let private minFilter min mip =
        match minFilters.TryGetValue ((min, mip)) with
            | (true, f) -> int f
            | _ -> int TextureMinFilter.Linear //failwithf "unsupported filter combination min: %A mip: %A" min mip

    let private magFilter mag =
        match magFilters.TryGetValue (mag) with
            | (true, f) -> int f
            | _ -> int TextureMagFilter.Linear //failwithf "unsupported mag filter: %A" mag

    let private compareFunc f =
        match compareFuncs.TryGetValue f with
            | (true, f) -> int f
            | _ -> int All.Lequal //failwithf "unsupported comparison function: %A" f

    let private setSamplerParameters (handle : int) (d : SamplerStateDescription) =
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

        if d.Filter.IsAnisotropic then
            GL.SamplerParameter(handle, SamplerParameterName.TextureMaxAnisotropyExt, d.MaxAnisotropy)
            GL.Check "could not set MaxAnisotropy for sampler"
        else
            GL.SamplerParameter(handle, SamplerParameterName.TextureMaxAnisotropyExt, 1)
            GL.Check "could not set MaxAnisotropy for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureMinFilter, minFilter d.Filter.Min d.Filter.Mip)
        GL.Check "could not set MinFilter for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureMagFilter, magFilter d.Filter.Mag)
        GL.Check "could not set MagFilter for sampler"

        GL.SamplerParameter(handle, SamplerParameterName.TextureCompareFunc, compareFunc d.ComparisonFunction)
        GL.Check "could not set CompareFunc for sampler"

    type Context with

        member x.CreateSampler(description : SamplerStateDescription) =
            using x.ResourceLock (fun _ ->
                if ExecutionContext.samplersSupported then
                    let handle = GL.GenSampler()
                    GL.Check "could not create sampler"

                    setSamplerParameters handle description

                    Sampler(x, handle, description)
                else
                    Sampler(x, -1, description)

            )

        member x.Update(s : Sampler, description : SamplerStateDescription) =
            using x.ResourceLock (fun _ ->
                setSamplerParameters s.Handle description
            )

        member x.Delete(s : Sampler) =
            if ExecutionContext.samplersSupported then
                using x.ResourceLock (fun _ ->
                    GL.DeleteSampler(s.Handle)
                    GL.Check "could not delete sampler"
                )


    module ExecutionContext =
        let bindSampler (index : int) (s : Sampler) =
            seq {
                if ExecutionContext.samplersSupported then
                    yield Instruction.BindSampler index s.Handle
                else
                    yield Instruction.ActiveTexture (int TextureUnit.Texture0 + index)
                    failwith "not implemented"
            }