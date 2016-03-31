namespace Aardvark.Rendering.Vulkan

open System
open System.Collections.Generic
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

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


[<AutoOpen>]
module internal VkLog =
    let infof fmt = Printf.kprintf(fun str -> Report.Line("[Vulkan] {0}", str)) fmt
    let debugf fmt = Printf.kprintf(fun str -> Report.Line(4, "[Vulkan] {0}", str)) fmt
    let errorf fmt = Printf.kprintf(fun str -> Report.Error("[Vulkan] {0}", str)) fmt
    let warnf fmt = Printf.kprintf(fun str -> Report.Warn("[Vulkan] {0}", str)) fmt

    let inline failf fmt = Printf.kprintf (fun str -> failwith ("[Vulkan] " + str)) fmt


module Unchecked =
    let isNull<'a when 'a : not struct> (v : 'a) =
        match v :> obj with
            | null -> true
            | _ -> false

    let notNull<'a when 'a : not struct> (v : 'a) =
        match v :> obj with
            | null -> false
            | _ -> true


[<Struct>]
type size_t(s : uint64) =
    
    override x.ToString() =
        if s = 0UL then "0"
        else
            let exp = log (float s) / log 2.0 |> floor |> int
            if exp >= 40 then   sprintf "%.3fTB" (float s / 1099511627776.0)
            elif exp >= 30 then sprintf "%.3fGB" (float s / 1073741824.0)
            elif exp >= 20 then sprintf "%.1fMB" (float s / 1048576.0)
            elif exp >= 10 then sprintf "%.1fkB" (float s / 1024.0)
            else sprintf "%db" s

    member x.Bytes = s
    member x.Kilobytes = float s / 1024.0
    member x.Megabytes = float s / 1048576.0
    member x.Gigabytes = float s / 1073741824.0
    member x.Terabytes = float s / 1099511627776.0
        
    static member op_Explicit (v : int8) = size_t (uint64 v)
    static member op_Explicit (v : uint8) = size_t (uint64 v)
    static member op_Explicit (v : int16) = size_t (uint64 v)
    static member op_Explicit (v : uint16) = size_t (uint64 v)
    static member op_Explicit (v : int32) = size_t (uint64 v)
    static member op_Explicit (v : uint32) = size_t (uint64 v)
    static member op_Explicit (v : int64) = size_t (uint64 v)
    static member op_Explicit (v : uint64) = size_t v

    static member (+) (l : size_t, r : size_t) = size_t (l.Bytes + r.Bytes)
    static member (-) (l : size_t, r : size_t) = size_t (l.Bytes - r.Bytes)
    static member (*) (l : size_t, r : size_t) = size_t (l.Bytes * r.Bytes)
    static member (/) (l : size_t, r : size_t) = size_t (l.Bytes / r.Bytes)
    static member (*) (l : size_t, r : int) = size_t (l.Bytes * uint64 r)
    static member (/) (l : size_t, r : int) = size_t (l.Bytes / uint64 r)
    static member (*) (l : int, r : size_t) = size_t (uint64 l * r.Bytes)
    static member (/) (l : int, r : size_t) = size_t (uint64 l / r.Bytes)

    new(s : int8) = size_t(uint64 s)
    new(s : uint8) = size_t(uint64 s)
    new(s : int16) = size_t(uint64 s)
    new(s : uint16) = size_t(uint64 s)
    new(s : int32) = size_t(uint64 s)
    new(s : uint32) = size_t(uint64 s)
    new(s : int64) = size_t(uint64 s)
