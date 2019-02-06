namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"
[<AutoOpen>]
module ``Sampler Extensions`` =

    module VkFilter = 
        let ofTextureFilterMode =
            LookupTable.lookupTable [
                TextureFilterMode.None, VkFilter.Nearest
                TextureFilterMode.Point, VkFilter.Nearest
                TextureFilterMode.Linear, VkFilter.Linear
            ]

    module VkSamplerMipmapMode = 
        let ofTextureFilterMode =
            LookupTable.lookupTable [
                TextureFilterMode.None, VkSamplerMipmapMode.Nearest
                TextureFilterMode.Point, VkSamplerMipmapMode.Nearest
                TextureFilterMode.Linear, VkSamplerMipmapMode.Linear
            ]

    module VkSamplerAddressMode =
        let ofWrapMode =
            LookupTable.lookupTable [
                unbox<_> 0, VkSamplerAddressMode.Repeat
                WrapMode.Border, VkSamplerAddressMode.ClampToBorder
                WrapMode.Clamp, VkSamplerAddressMode.ClampToEdge
                WrapMode.Mirror, VkSamplerAddressMode.MirroredRepeat
                WrapMode.MirrorOnce, VkSamplerAddressMode.MirroredRepeat
                WrapMode.Wrap, VkSamplerAddressMode.Repeat

            ]

    module VkCompareOp =
        let ofSamplerComparisonFunction =
            LookupTable.lookupTable [
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

type Sampler =
    class
        inherit Resource<VkSampler>
        val mutable public Description : SamplerStateDescription

        new(device : Device, desc : SamplerStateDescription, handle : VkSampler) = { inherit Resource<_>(device, handle); Description = desc  }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Sampler =
    let create (desc : SamplerStateDescription) (device : Device) =
            
        let cmpEnable = 
            if desc.ComparisonFunction <> SamplerComparisonFunction.None then 1u
            else 0u

        let mutable info =
            VkSamplerCreateInfo(
                VkStructureType.SamplerCreateInfo,
                0n, VkSamplerCreateFlags.MinValue,
                VkFilter.ofTextureFilterMode desc.Filter.Mag,
                VkFilter.ofTextureFilterMode desc.Filter.Min,
                VkSamplerMipmapMode.ofTextureFilterMode desc.Filter.Mip,
                VkSamplerAddressMode.ofWrapMode desc.AddressU,
                VkSamplerAddressMode.ofWrapMode desc.AddressV,
                VkSamplerAddressMode.ofWrapMode desc.AddressW,
                desc.MipLodBias,
                (if desc.Filter.IsAnisotropic then 1u else 0u),
                (if desc.Filter.IsAnisotropic then float32 desc.MaxAnisotropy else 1.0f),
                cmpEnable,
                VkCompareOp.ofSamplerComparisonFunction desc.ComparisonFunction,
                (if desc.Filter.Mip <> TextureFilterMode.None then desc.MinLod else 0.0f),
                (if desc.Filter.Mip <> TextureFilterMode.None then desc.MaxLod else 0.0f),
                VkBorderColor.FloatTransparentBlack, // vulkan does not seem to support real bordercolors
                0u // unnormalized
            )


        let mutable handle = VkSampler.Null
        VkRaw.vkCreateSampler(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create sampler"

        Sampler(device, desc, handle)

    let delete (sampler : Sampler) (device : Device) =
        if sampler.Handle.IsValid then
            VkRaw.vkDestroySampler(device.Handle, sampler.Handle, NativePtr.zero)
            sampler.Handle <- VkSampler.Null

[<AbstractClass; Sealed; Extension>]
type ContextSamplerExtensions private() =
    [<Extension>]
    static member inline CreateSampler(this : Device, desc : SamplerStateDescription) =
        this |> Sampler.create desc

    [<Extension>]
    static member inline Delete(this : Device, sampler : Sampler) =
        this |> Sampler.delete sampler