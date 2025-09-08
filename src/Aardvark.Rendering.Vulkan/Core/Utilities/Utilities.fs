namespace Aardvark.Rendering.Vulkan

open System
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"

[<AutoOpen>]
module internal Utilities =

    module VkResult =
        open KHRSurface
        open KHRSwapchain

        let inline isSwapFailure (result: VkResult) =
            result = VkResult.ErrorOutOfDateKhr || result = VkResult.ErrorSurfaceLostKhr || result = VkResult.SuboptimalKhr

    module VkBool32 =
        let inline ofBool (value: bool) : VkBool32 = if value then VkTrue else VkFalse
        let inline toBool (value: VkBool32) : bool = value = VkTrue

    module Alignment =
        let prev (align : int64) (v : int64) =
            let r = v % align
            if r = 0L then v
            else v - r

        let next (align : int64) (v : int64) =
            let r = v % align
            if r = 0L then v
            else align + v - r

    module Enum =

        /// Converts a bit field to another given a conversion table
        let inline convertFlags< ^T, ^U when ^T : comparison and ^T :> Enum and
                                             ^U : (static member (|||) : ^U -> ^U -> ^U)> (lookup : Map< ^T, ^U>) (none : ^U) (value : ^T) =
            let mutable result = none

            lookup |> Map.iter (fun src dst ->
                if value.HasFlag src then result <- result ||| dst
            )

            result

[<AutoOpen>]
module BaseLibExtensions = 

    type Version with
        member v.ToVulkan() =
            ((uint32 v.Major) <<< 22) ||| ((uint32 v.Minor) <<< 12) ||| (uint32 v.Build)

        static member FromVulkan (v : uint32) =
            Version(int (v >>> 22), int ((v >>> 12) &&& 0x3FFu), int (v &&& 0xFFFu))

    type V2i with
        static member inline OfExtent (e : VkExtent2D) = V2i(int32 e.width, int32 e.height)
        static member inline OfOffset (o : VkOffset2D) = V2i(o.x, o.y)
        member inline this.ToExtent() = VkExtent2D(uint32 this.X, uint32 this.Y)
        member inline this.ToOffset() = VkOffset2D(this.X, this.Y)

    type V2l with
        static member inline OfExtent (e : VkExtent2D) = V2l(int32 e.width, int32 e.height)
        static member inline OfOffset (o : VkOffset2D) = V2l(int64 o.x, int64 o.y)
        member inline this.ToExtent() = VkExtent2D(uint32 this.X, uint32 this.Y)
        member inline this.ToOffset() = VkOffset2D(int32 this.X, int32 this.Y)

    type V3i with
        static member inline OfExtent (e : VkExtent3D) = V3i(int32 e.width, int32 e.height, int32 e.depth)
        static member inline OfOffset (o : VkOffset3D) = V3i(o.x, o.y, o.z)
        member inline this.ToExtent() = VkExtent3D(uint32 this.X, uint32 this.Y, uint32 this.Z)
        member inline this.ToOffset() = VkOffset3D(this.X, this.Y, this.Z)

    type V3l with
        static member inline OfExtent (e : VkExtent3D) = V3l(int32 e.width, int32 e.height, int32 e.depth)
        static member inline OfOffset (o : VkOffset3D) = V3l(int64 o.x, int64 o.y, int64 o.z)
        member inline this.ToExtent() = VkExtent3D(uint32 this.X, uint32 this.Y, uint32 this.Z)
        member inline this.ToOffset() = VkOffset3D(int32 this.X, int32 this.Y, int32 this.Z)