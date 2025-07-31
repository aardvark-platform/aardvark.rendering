namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open EXTCustomBorderColor

#nowarn "51"

[<AutoOpen>]
module ``Sampler Extensions`` =

    module VkFilter =
        let ofFilterMode =
            LookupTable.lookup [
                FilterMode.Point, VkFilter.Nearest
                FilterMode.Linear, VkFilter.Linear
            ]

    module VkSamplerMipmapMode =
        let ofTextureFilter (t : TextureFilter) =
            match TextureFilter.mipmapMode t with
            | ValueSome FilterMode.Linear ->
                VkSamplerMipmapMode.Linear
            | _ ->
                VkSamplerMipmapMode.Nearest

    module VkSamplerAddressMode =
        let ofWrapMode =
            let tryGetAddressMode =
                LookupTable.tryLookupV [
                    WrapMode.Border, VkSamplerAddressMode.ClampToBorder
                    WrapMode.Clamp, VkSamplerAddressMode.ClampToEdge
                    WrapMode.Mirror, VkSamplerAddressMode.MirroredRepeat
                    WrapMode.Wrap, VkSamplerAddressMode.Repeat
                ]

            fun m ->
                match tryGetAddressMode m with
                | ValueSome it -> it
                | _ -> failf "unsupported WrapMode: %A" m

    module VkCompareOp =
        let ofSamplerComparisonFunction =
            LookupTable.lookup [
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
        val public Description : SamplerState
        val public Format : VkFormat

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroySampler(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkSampler.Null

        new(device : Device, desc : SamplerState, handle : VkSampler, format : VkFormat) =
            { inherit Resource<_>(device, handle)
              Description = desc
              Format = format }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Sampler =
    let create (format : VkFormat) (desc : SamplerState) (device : Device) =
        let isInteger = VkFormat.isIntegerFormat format

        let hasWrapModeBorder =
            if desc.AddressU = WrapMode.Border || desc.AddressV = WrapMode.Border || desc.AddressW = WrapMode.Border then
                if device.EnabledFeatures.Samplers.CustomBorderColors then
                    true
                else
                    Log.warn "[Vulkan] Custom border colors for samplers are not supported"
                    false
            else
                false

        let borderColor =
            if hasWrapModeBorder then
                if isInteger then VkBorderColor.IntCustomExt
                else VkBorderColor.FloatCustomExt
            else
                if isInteger then VkBorderColor.IntOpaqueBlack
                else VkBorderColor.FloatOpaqueBlack

        let mutable customBorderColor =
            if hasWrapModeBorder then
                let border = v4f desc.BorderColor

                let color =
                    if isInteger then
                        VkClearColorValue(int32 = Fun.FloatToBits border)
                    else
                        VkClearColorValue(float32 = border)

                VkSamplerCustomBorderColorCreateInfoEXT(color, format)
            else
                VkSamplerCustomBorderColorCreateInfoEXT.Empty

        let pNext =
            if hasWrapModeBorder then &&customBorderColor else NativePtr.zero

        let mutable createInfo =
            VkSamplerCreateInfo(
                pNext.Address,
                VkSamplerCreateFlags.None,
                desc.Filter |> TextureFilter.magnification |> VkFilter.ofFilterMode,
                desc.Filter |> TextureFilter.minification |> VkFilter.ofFilterMode,
                VkSamplerMipmapMode.ofTextureFilter desc.Filter,
                VkSamplerAddressMode.ofWrapMode desc.AddressU,
                VkSamplerAddressMode.ofWrapMode desc.AddressV,
                VkSamplerAddressMode.ofWrapMode desc.AddressW,
                desc.MipLodBias,
                (if desc.IsAnisotropic then VkTrue else VkFalse),
                (if desc.IsAnisotropic then float32 desc.MaxAnisotropy else 1.0f),
                (if desc.Comparison <> ComparisonFunction.Always then VkTrue else VkFalse),
                VkCompareOp.ofSamplerComparisonFunction desc.Comparison,
                (if desc.UseMipmap then desc.MinLod else 0.0f),
                (if desc.UseMipmap then desc.MaxLod else 0.25f),
                borderColor,
                VkFalse
            )

        let mutable handle = VkSampler.Null
        VkRaw.vkCreateSampler(device.Handle, &&createInfo, NativePtr.zero, &&handle)
            |> check "could not create sampler"

        new Sampler(device, desc, handle, format)

[<AbstractClass; Sealed; Extension>]
type ContextSamplerExtensions private() =
    [<Extension>]
    static member inline CreateSampler(this : Device, desc : SamplerState, format : VkFormat) =
        this |> Sampler.create format desc