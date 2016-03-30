namespace Aardvark.Rendering.Vulkan

open System
open System.Collections.Generic
open Microsoft.FSharp.NativeInterop

#nowarn "9"

[<AutoOpen>]
module VulkanHelpers =
    let check str err =
        match err with
            | VkResult.VkSuccess -> ()
            | _ -> failwithf "[Vulkan] %s failed with: %A" str err

    type Version with
        member v.UInt32 =
            ((uint32 v.Major) <<< 22) ||| ((uint32 v.Minor) <<< 12) ||| (uint32 v.Build)

        static member FromUInt32 (v : uint32) =
            Version(int (v >>> 22), int ((v >>> 12) &&& 0x3FFu), int (v &&& 0xFFFu))

    let lookupTable (l : list<'a * 'b>) =
        let d = Dictionary()
        for (k,v) in l do

            match d.TryGetValue k with
                | (true, vo) -> failwithf "duplicated lookup-entry: %A (%A vs %A)" k vo v
                | _ -> ()

            d.[k] <- v

        fun (key : 'a) ->
            match d.TryGetValue key with
                | (true, v) -> v
                | _ -> failwithf "unsupported %A: %A" typeof<'a> key
