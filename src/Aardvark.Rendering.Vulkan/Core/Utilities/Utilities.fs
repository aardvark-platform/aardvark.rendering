namespace Aardvark.Rendering.Vulkan

open System
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"

[<AutoOpen>]
module internal Utilities =

    // TODO: Remove for Aardvark.Base >= 5.3.9
    let inline internal (&&&=) (x: byref<'T>) (y: 'T) = x <- x &&& y
    let inline internal (|||=) (x: byref<'T>) (y: 'T) = x <- x ||| y

    module VkResult =
        open KHRSurface
        open KHRSwapchain

        let inline isSwapFailure (result: VkResult) =
            result = VkResult.ErrorOutOfDateKhr || result = VkResult.ErrorSurfaceLostKhr || result = VkResult.SuboptimalKhr

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

    // TODO: Remove for Aardvark.Base >= 5.3.9
    module NativePtr =
        let inline stackUseArr ([<InlineIfLambda>] mapping: 'T -> 'U) (data: 'T[]) =
            let ptr = NativePtr.stackalloc<'U> data.Length
            for i = 0 to data.Length - 1 do ptr.[i] <- mapping data.[i]
            ptr

    // TODO: Remove for Aardvark.Base >= 5.3.9
    module Map =
        let ofSeqDupl (s : seq<'a * 'b>) =
            let mutable res = Map.empty
            for (k,v) in s do
                match Map.tryFind k res with
                    | Some set ->
                        res <- Map.add k (Set.add v set) res
                    | None ->
                        res <- Map.add k (Set.singleton v) res
            res

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

    // TODO: Remove for Aardvark.Base >= 5.3.9
    module Array =
        let choosei (f : int -> 'a -> Option<'b>) (a : 'a[]) =
            let res = System.Collections.Generic.List<'b>()
            for i in 0 .. a.Length - 1 do
                match f i a.[i] with
                    | Some v -> res.Add v
                    | None -> ()

            res.ToArray()

        let collecti (f : int -> 'a -> #seq<'b>) (a : 'a[]) =
            let mutable i = 0
            let res = System.Collections.Generic.List<'b>()
            for v in a do
                res.AddRange(f i v)
                i <- i + 1

            res.ToArray()

    // TODO: Remove for Aardvark.Base >= 5.3.9
    module List =
        let choosei (f : int -> 'a -> Option<'b>) (a : list<'a>) =
            let res = System.Collections.Generic.List<'b>()
            let mutable i = 0
            for v in a do
                match f i v with
                    | Some v -> res.Add v
                    | None -> ()
                i <- i + 1

            res |> CSharpList.toList