namespace Aardvark.Rendering

open System
open Aardvark.Base

type WrapMode =
    | Wrap = 0
    | Mirror = 1
    | Clamp = 2
    | Border = 3
    | MirrorOnce = 4

[<Struct; CLIMutable>]
type SamplerState =
    {
        Filter          : TextureFilter
        AddressU        : WrapMode
        AddressV        : WrapMode
        AddressW        : WrapMode
        Comparison      : ComparisonFunction
        BorderColor     : C4f
        MaxAnisotropy   : int
        MaxLod          : float32
        MinLod          : float32
        MipLodBias      : float32
    }

    /// Returns whether the sampler uses anisotropic filtering, i.e. whether MaxAnisotropy is greater than 1.
    member x.IsAnisotropic =
        x.MaxAnisotropy > 1

    /// Returns whether the texture filter uses mipmapping.
    member x.UseMipmap =
        x.Filter |> TextureFilter.mipmapMode |> ValueOption.isSome

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

    /// Creates a sampler state with trilinear filtering and the given maximum anisotropy.
    let anisotropic (maxAnisotropy : int) =
        { filter TextureFilter.MinMagMipLinear with MaxAnisotropy = maxAnisotropy }

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