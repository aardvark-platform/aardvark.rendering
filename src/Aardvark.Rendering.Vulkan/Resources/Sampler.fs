namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering
#nowarn "9"
#nowarn "51"



type Sampler =
    class
        val mutable public Context : Context
        val mutable public Handle : VkSampler
        val mutable public Description : SamplerStateDescription

        new(ctx,h,desc) = { Context = ctx; Handle = h; Description = desc }
    end


[<AutoOpen>]
module private SamplerHelpers =

    let toVkFilter =
        lookupTable [
            TextureFilterMode.None, VkFilter.Nearest
            TextureFilterMode.Point, VkFilter.Nearest
            TextureFilterMode.Linear, VkFilter.Linear
        ]

    let toVkSamplerMipmapMode =
        lookupTable [
            TextureFilterMode.None, VkSamplerMipmapMode.Nearest
            TextureFilterMode.Point, VkSamplerMipmapMode.Nearest
            TextureFilterMode.Linear, VkSamplerMipmapMode.Linear
        ]

    let toVkSamplerAddressMode =
        lookupTable [
            unbox<_> 0, VkSamplerAddressMode.Repeat
            WrapMode.Border, VkSamplerAddressMode.ClampToBorder
            WrapMode.Clamp, VkSamplerAddressMode.ClampToEdge
            WrapMode.Mirror, VkSamplerAddressMode.MirroredRepeat
            WrapMode.MirrorOnce, VkSamplerAddressMode.MirroredRepeat
            WrapMode.Wrap, VkSamplerAddressMode.Repeat

        ]

    let toVkCompareOp =
        lookupTable [
            SamplerComparisonFunction.Always, VkCompareOp.Always
            SamplerComparisonFunction.Equal, VkCompareOp.Equal
            SamplerComparisonFunction.Greater, VkCompareOp.Greater
            SamplerComparisonFunction.GreaterOrEqual, VkCompareOp.GreaterOrEqual
            SamplerComparisonFunction.Less, VkCompareOp.Less
            SamplerComparisonFunction.LessOrEqual, VkCompareOp.LessOrEqual
            SamplerComparisonFunction.Never, VkCompareOp.Never
            SamplerComparisonFunction.NotEqual, VkCompareOp.NotEqual

            SamplerComparisonFunction.None, VkCompareOp.Always
        ]

[<AbstractClass; Sealed; Extension>]
type SamplerExtensions private() =
    [<Extension>]
    static member CreateSampler(this : Context, desc : SamplerStateDescription) =
            
        let cmpEnable = 
            if desc.ComparisonFunction <> SamplerComparisonFunction.None then 1u
            else 0u

        let mutable info =
            VkSamplerCreateInfo(
                VkStructureType.SamplerCreateInfo,
                0n, VkSamplerCreateFlags.MinValue,
                toVkFilter desc.Filter.Mag,
                toVkFilter desc.Filter.Min,
                toVkSamplerMipmapMode desc.Filter.Mip,
                toVkSamplerAddressMode desc.AddressU,
                toVkSamplerAddressMode desc.AddressV,
                toVkSamplerAddressMode desc.AddressW,
                desc.MipLodBias,
                (if desc.Filter.IsAnisotropic then 1u else 0u),
                (if desc.Filter.IsAnisotropic then float32 desc.MaxAnisotropy else 1.0f),
                cmpEnable,
                toVkCompareOp desc.ComparisonFunction,
                (if desc.Filter.Mip <> TextureFilterMode.None then desc.MinLod else 0.0f),
                (if desc.Filter.Mip <> TextureFilterMode.None then desc.MaxLod else 0.0f),
                VkBorderColor.FloatTransparentBlack, // vulkan does not seem to support real bordercolors
                0u // unnormalized
            )

        let mutable sampler = VkSampler.Null
        VkRaw.vkCreateSampler(this.Device.Handle, &&info, NativePtr.zero, &&sampler) |> check "vkCreateSampler"

        Sampler(this, sampler, desc)

    [<Extension>]
    static member Delete(this : Context, sam : Sampler) =
        if sam.Handle.IsValid then
            VkRaw.vkDestroySampler(this.Device.Handle, sam.Handle, NativePtr.zero)
            sam.Handle <- VkSampler.Null