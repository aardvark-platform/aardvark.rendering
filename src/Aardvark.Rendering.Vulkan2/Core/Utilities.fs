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



