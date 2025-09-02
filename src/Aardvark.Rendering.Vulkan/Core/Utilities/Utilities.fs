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
        static member OfExtent (e : VkExtent2D) =
            V2i(int e.width, int e.height)

        member x.ToExtent() =
            VkExtent2D(uint32 x.X, uint32 x.Y)

    type V2l with
        static member OfExtent (e : VkExtent2D) =
            V2l(int64 e.width, int64 e.height)

        member x.ToExtent() =
            VkExtent2D(uint32 x.X, uint32 x.Y)

    type V3i with
        static member OfExtent (e : VkExtent3D) =
            V3i(int e.width, int e.height, int e.depth)

        member x.ToExtent() =
            VkExtent3D(uint32 x.X, uint32 x.Y, uint32 x.Z)

    type V3l with
        static member OfExtent (e : VkExtent3D) =
            V3l(int64 e.width, int64 e.height, int64 e.depth)

        member x.ToExtent() =
            VkExtent3D(uint32 x.X, uint32 x.Y, uint32 x.Z)