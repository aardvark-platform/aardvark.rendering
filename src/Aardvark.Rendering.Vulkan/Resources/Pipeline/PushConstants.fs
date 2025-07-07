namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System

type PushConstantsLayout =
    internal {
        StageFlags : VkShaderStageFlags
        Buffer     : FShade.GLSL.GLSLUniformBuffer
    }

    member this.SizeInBytes = this.Buffer.ubSize
    member this.Range = VkPushConstantRange(this.StageFlags, 0u, uint32 this.SizeInBytes)

type PushConstants(layout: PushConstantsLayout) =
    let mutable ptr = NativePtr.alloc<uint8> layout.SizeInBytes

    member val Layout = layout
    member _.StageFlags = layout.StageFlags
    member _.SizeInBytes = layout.SizeInBytes
    member _.Pointer = ptr

    member _.Dispose() =
        NativePtr.free ptr
        ptr <- NativePtr.zero

    interface IDisposable with
        member this.Dispose() = this.Dispose()

[<AutoOpen>]
module PushConstantsStreamExtensions =

    type VKVM.CommandStream with
        member inline this.PushConstants(layout: VkPipelineLayout, constants: PushConstants) =
            this.PushConstants(layout, constants.StageFlags, 0u, uint32 constants.SizeInBytes, constants.Pointer.Address)