namespace Aardvark.Rendering

open System
open Aardvark.Base

type WrapMode =
    | Wrap = 0
    | Mirror = 1
    | Clamp = 2
    | Border = 3
    | MirrorOnce = 4

[<Struct>]
type SamplerState =
    {
        mutable Filter          : TextureFilter
        mutable AddressU        : WrapMode
        mutable AddressV        : WrapMode
        mutable AddressW        : WrapMode
        mutable Comparison      : ComparisonFunction
        mutable BorderColor     : C4f
        mutable MaxAnisotropy   : int
        mutable MaxLod          : float32
        mutable MinLod          : float32
        mutable MipLodBias      : float32
    }

    /// Returns whether the texture filter is anisotropic
    member x.IsAnisotropic =
        x.Filter |> TextureFilter.isAnisotropic

    /// Returns whether the texture filter uses mipmapping.
    member x.UseMipmap =
        x.Filter |> TextureFilter.mipmapMode |> Option.isSome

    static member Default =
        { Filter            = TextureFilter.MinMagMipLinear
          AddressU          = WrapMode.Clamp
          AddressV          = WrapMode.Clamp
          AddressW          = WrapMode.Clamp
          Comparison        = ComparisonFunction.Always
          BorderColor       = C4f.Black
          MaxAnisotropy     = 16
          MinLod            = 0.0f
          MaxLod            = Single.MaxValue
          MipLodBias        = 0.0f }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SamplerState =

    let filter (f : TextureFilter) =
        { SamplerState.Default with Filter = f }

    let anisotropic (maxAnisotropy : int) =
        { filter TextureFilter.Anisotropic with MaxAnisotropy = maxAnisotropy }

    let withFilter (f : TextureFilter) (state : SamplerState) =
        { state with Filter = f }

    let withAdressModes (u : WrapMode) (v : WrapMode) (w : WrapMode) (state : SamplerState) =
        { state with AddressU = u; AddressV = v; AddressW = w }

    let withAdressMode (mode : WrapMode) =
        withAdressModes mode mode mode

    let withBorderColor (color : C4f) (state : SamplerState) =
        { state with
            AddressU = WrapMode.Border
            AddressV = WrapMode.Border
            AddressW = WrapMode.Border
            BorderColor = color }

    let withComparison (compare : ComparisonFunction) (state : SamplerState) =
        { state with Comparison = compare }

    let withLod (minLod : float32) (maxLod : float32) (lodBias : float32) (state : SamplerState) =
        { state with
            MinLod = minLod
            MaxLod = maxLod
            MipLodBias = lodBias }