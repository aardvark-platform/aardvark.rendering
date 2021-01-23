namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
// #nowarn "51"
[<AutoOpen>]
module ``Sampler Extensions`` =

    module VkFilter =
        let ofFilterMode =
            LookupTable.lookupTable [
                FilterMode.Point, VkFilter.Nearest
                FilterMode.Linear, VkFilter.Linear
            ]

    module VkSamplerMipmapMode =
        let ofTextureFilter (t : TextureFilter) =
            match TextureFilter.mipmapMode t with
            | Some(FilterMode.Linear) ->
                VkSamplerMipmapMode.Linear
            | _ ->
                VkSamplerMipmapMode.Nearest

    module VkSamplerAddressMode =
        let ofWrapMode =
            LookupTable.lookupTable [
                WrapMode.Border, VkSamplerAddressMode.ClampToBorder
                WrapMode.Clamp, VkSamplerAddressMode.ClampToEdge
                WrapMode.Mirror, VkSamplerAddressMode.MirroredRepeat
                WrapMode.MirrorOnce, VkSamplerAddressMode.MirroredRepeat
                WrapMode.Wrap, VkSamplerAddressMode.Repeat

            ]

    module VkCompareOp =
        let ofSamplerComparisonFunction =
            LookupTable.lookupTable [
                ComparisonFunction.Always, VkCompareOp.Always
                ComparisonFunction.Equal, VkCompareOp.Equal
                ComparisonFunction.Greater, VkCompareOp.Greater
                ComparisonFunction.GreaterOrEqual, VkCompareOp.GreaterOrEqual
                ComparisonFunction.Less, VkCompareOp.Less
                ComparisonFunction.LessOrEqual, VkCompareOp.LessOrEqual
                ComparisonFunction.Never, VkCompareOp.Never
                ComparisonFunction.NotEqual, VkCompareOp.NotEqual
            ]

type Sampler =
    class
        inherit Resource<VkSampler>
        val mutable public Description : SamplerState

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroySampler(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkSampler.Null

        new(device : Device, desc : SamplerState, handle : VkSampler) = { inherit Resource<_>(device, handle); Description = desc  }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Sampler =
    let create (desc : SamplerState) (device : Device) =
        native {
            let cmpOp =
                VkCompareOp.ofSamplerComparisonFunction desc.Comparison

            let cmpEnable =
                if cmpOp <> VkCompareOp.Always then 1u
                else 0u

            let! pInfo =
                VkSamplerCreateInfo(
                    VkSamplerCreateFlags.None,
                    desc.Filter |> TextureFilter.magnification |> VkFilter.ofFilterMode,
                    desc.Filter |> TextureFilter.minification |> VkFilter.ofFilterMode,
                    VkSamplerMipmapMode.ofTextureFilter desc.Filter,
                    VkSamplerAddressMode.ofWrapMode desc.AddressU,
                    VkSamplerAddressMode.ofWrapMode desc.AddressV,
                    VkSamplerAddressMode.ofWrapMode desc.AddressW,
                    desc.MipLodBias,
                    (if desc.IsAnisotropic then 1u else 0u),
                    (if desc.IsAnisotropic then float32 desc.MaxAnisotropy else 1.0f),
                    cmpEnable,
                    cmpOp,
                    (if desc.UseMipmap then desc.MinLod else 0.0f),
                    (if desc.UseMipmap then desc.MaxLod else 0.25f),
                    VkBorderColor.FloatTransparentBlack, // vulkan does not seem to support real bordercolors
                    0u // unnormalized
                )


            let! pHandle = VkSampler.Null
            VkRaw.vkCreateSampler(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create sampler"

            return new Sampler(device, desc, !!pHandle)
        }

[<AbstractClass; Sealed; Extension>]
type ContextSamplerExtensions private() =
    [<Extension>]
    static member inline CreateSampler(this : Device, desc : SamplerState) =
        this |> Sampler.create desc