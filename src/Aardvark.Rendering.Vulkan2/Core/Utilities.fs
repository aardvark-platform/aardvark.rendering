namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module private Utilities =
    let check (str : string) (err : VkResult) =
        if err <> VkResult.VkSuccess then failwithf "[Vulkan] %s" str

    let checkf (fmt : Printf.StringFormat<'a, VkResult -> unit>) =
        Printf.kprintf (fun str ->
            fun (res : VkResult) ->
                if res <> VkResult.VkSuccess then failwith ("[Vulkan] " + str)
        ) fmt

    let inline failf fmt = Printf.kprintf (fun str -> failwith ("[Vulkan] " + str)) fmt

    let VK_QUEUE_FAMILY_IGNORED = ~~~0u

[<AutoOpen>]
module BaseLibExtensions = 
    module NativePtr =
        let withA (f : nativeptr<'a> -> 'b) (a : 'a[]) =
            let gc = GCHandle.Alloc(a, GCHandleType.Pinned)
            try f (gc.AddrOfPinnedObject() |> NativePtr.ofNativeInt)
            finally gc.Free()

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

    type V3i with
        static member OfExtent (e : VkExtent3D) =
            V3i(int e.width, int e.height, int e.depth)
        
        member x.ToExtent() =
            VkExtent3D(uint32 x.X, uint32 x.Y, uint32 x.Z)

    module VkRaw =
        let warn fmt = Printf.kprintf (fun str -> Report.Warn("[Vulkan] {0}", str)) fmt

        let debug fmt = Printf.kprintf (fun str -> Report.Line(2, "[Vulkan] {0}", str)) fmt

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private Alignment = 
    let prev (align : int64) (v : int64) =
        let r = v % align
        if r = 0L then v
        else v - r

    let next (align : int64) (v : int64) =
        let r = v % align
        if r = 0L then v
        else align + v - r


[<AbstractClass>]
type VulkanObject() =
    let mutable isDisposed = 0

    let beforeDispose = Event<unit>()

    abstract member Release : unit -> unit

    [<CLIEvent>]
    member x.BeforeDispose = beforeDispose.Publish

    member x.IsDisposed = isDisposed <> 0

    member inline private x.Dispose(disposing : bool) =
        let o = Interlocked.Exchange(&isDisposed, 1)
        if o = 0 then
            beforeDispose.Trigger()
            x.Release()
            if disposing then GC.SuppressFinalize x

    member x.Dispose() = x.Dispose true
    override x.Finalize() = x.Dispose false

    interface IDisposable with
        member x.Dispose() = x.Dispose()


module Enum =
    [<AutoOpen>]
    module BitUtils =
        let bits (f : int) =
            let rec testBits (i : int) (mask : int) =
                if i > 32 then []
                else
                    if f &&& mask <> 0 then 
                        i :: testBits (i + 1) (mask <<< 1)
                    else
                        testBits (i + 1) (mask <<< 1)
            testBits 0 0x1

        let allSubsets (s : list<'a>) =
            let rec allSubsetsInternal (s : list<'a>) =
                match s with
                    | [] -> [Set.empty]
                    | h :: rest ->
                        let r = allSubsetsInternal rest
                        (r |> List.map (Set.add h)) @ r

            allSubsetsInternal s |> List.filter (Set.isEmpty >> not)

        let allSubMasks (m : int) =
            let bits = bits m
            allSubsets bits |> List.map (fun s ->
                s |> Set.fold (fun m b -> m ||| (1 <<< b)) 0
            )

    let inline allSubFlags (f : ^a) =
        allSubMasks (int f) |> List.map unbox< ^a >

type private MultiTable<'k, 'v when 'k : equality>(initial : seq<'k * 'v>) =
    let store = Dictionary<'k, HashSet<'v>>()

    member private x.TryRemoveEnum (f : 'k -> bool, e : byref<Dictionary.Enumerator<'k, HashSet<'v>>>, res : byref<'v>) =
        if e.MoveNext() then
            let current = e.Current
            let key = current.Key
            if f key then
                let value = current.Value
                let thing = value |> Seq.head
                if value.Count = 1 then
                    store.Remove key |> ignore
                    res <- thing
                    true
                else
                    value.Remove thing |> ignore
                    res <- thing
                    true
            else
                x.TryRemoveEnum(f, &e, &res)
        else
            false

    member private x.TryPeekEnum (f : 'k -> bool, e : byref<Dictionary.Enumerator<'k, HashSet<'v>>>, res : byref<'v>) =
        if e.MoveNext() then
            let current = e.Current
            let key = current.Key
            if f key then
                let value = current.Value
                let thing = value |> Seq.head
                res <- thing
                true
            else
                x.TryPeekEnum(f, &e, &res)
        else
            false

    member x.Add (k : 'k, v : 'v) =
        match store.TryGetValue k with
            | (true, set) -> set.Add v
            | _ ->
                let set = HashSet [v]
                store.[k] <- set
                true

    member x.TryRemove (f : 'k -> bool, [<Out>] res : byref<'v>) =
        let mutable e = store.GetEnumerator()
        try x.TryRemoveEnum(f, &e, &res)
        finally e.Dispose()

    member x.TryPeek (f : 'k -> bool, [<Out>] res : byref<'v>) =
        let mutable e = store.GetEnumerator()
        try x.TryPeekEnum(f, &e, &res)
        finally e.Dispose()

type FlagPool<'k, 'v when 'k : enum<int> >(initial : seq<'v>, flags : 'v -> 'k) =
    static let toInt (k : 'k) = k |> unbox<int>
    let table = MultiTable<int, 'v>(initial |> Seq.map (fun v -> (toInt (flags v), v)))
    let available = initial |> Seq.collect (flags >> toInt >> Enum.allSubFlags) |> HashSet.ofSeq

    let changed = new System.Threading.AutoResetEvent(false)

    static let check (flags : int) (f : int) =
        f &&& flags = flags

    member private x.TryAcquireInt (flags : int, [<Out>] value : byref<'v>) =
        Monitor.Enter table
        try table.TryRemove(check flags, &value)
        finally Monitor.Exit table

    member x.TryAcquire(flags : 'k, [<Out>] value : byref<'v>) =
        let flags = toInt flags
        x.TryAcquireInt(flags, &value)

    member x.Acquire (flags : 'k) =
        let flags = toInt flags
        let mutable result = Unchecked.defaultof<'v>
        while not (x.TryAcquireInt(flags, &result)) do
            changed.WaitOne() |> ignore

        result

    member x.AcquireAsync (flags : 'k) =
        async {
            let flags = toInt flags
            let mutable result = Unchecked.defaultof<'v>
            while not (x.TryAcquireInt(flags, &result)) do
                let! _ = Async.AwaitWaitHandle changed
                ()

            return result
        }

    member x.Release (value : 'v) =
        let flags = flags value
        lock table (fun () ->
            if table.Add(toInt flags, value) then
                changed.Set() |> ignore
        )
