namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Runtime.InteropServices
open Microsoft.FSharp.Core

#nowarn "51"

[<AllowNullLiteral>]
type Fence internal (device: IDevice, [<Optional; DefaultParameterValue(false)>] signaled: bool) =
    static let infinite = UInt64.MaxValue

    let mutable fence = VkFence.Null

    do
        let mutable createInfo =
            VkFenceCreateInfo(
                if signaled then VkFenceCreateFlags.SignaledBit
                else VkFenceCreateFlags.None
            )
        use pCreateInfo = fixed &createInfo
        use pFence = fixed &fence
        VkRaw.vkCreateFence(device.Handle, pCreateInfo, NativePtr.zero, pFence)
            |> check "could not create fence"

        device.Instance.RegisterDebugTrace(fence.Handle)

    member x.Handle = fence
    member internal x.DeviceInterface = device

    static member WaitAll(fences: Fence[]) =
        if fences.Length > 0 then
            let pFences = fences |> NativePtr.stackUseArr _.Handle
            VkRaw.vkWaitForFences(fences.[0].DeviceInterface.Handle, uint32 fences.Length, pFences, 1u, infinite)
                |> checkForFault fences.[0].DeviceInterface "failed to wait for fences"

    static member WaitAny(fences: Fence[]) =
        if fences.Length > 0 then
            let pFences = fences |> NativePtr.stackUseArr _.Handle
            VkRaw.vkWaitForFences(fences.[0].DeviceInterface.Handle, uint32 fences.Length, pFences, 0u, infinite)
                |> checkForFault fences.[0].DeviceInterface "failed to wait for fences"

    member x.Signaled =
        if fence.IsValid then
            match VkRaw.vkGetFenceStatus(device.Handle, fence) with
            | VkResult.Success -> true
            | VkResult.NotReady -> false
            | err -> err |> checkForFault device "failed to get fence status" |> unbox
        else
            true

    member x.Reset() =
        if fence.IsValid then
            use pFence = fixed &fence
            VkRaw.vkResetFences(device.Handle, 1u, pFence)
                |> check "failed to reset fence"
        else
            failf "cannot reset disposed fence"

    member x.TryWait([<Optional; DefaultParameterValue(~~~0UL)>] timeoutInNanoseconds: uint64) =
        use pFence = fixed &fence
        match VkRaw.vkWaitForFences(device.Handle, 1u, pFence, 1u, timeoutInNanoseconds) with
        | VkResult.Success -> true
        | VkResult.Timeout -> false
        | err -> err |> checkForFault device "could not wait for fence" |> unbox

    member x.Dispose() =
        if fence.IsValid && device.Handle <> 0n then
            VkRaw.vkDestroyFence(device.Handle, fence, NativePtr.zero)
            fence <- VkFence.Null

    member x.Wait([<Optional; DefaultParameterValue(~~~0UL)>] timeoutInNanoseconds: uint64) =
        if timeoutInNanoseconds <> infinite then
            if not <| x.TryWait(timeoutInNanoseconds) then
                raise <| TimeoutException()
        else
            // Watchdog: an infinite vkWaitForFences that never returns is a GPU hang, and would
            // otherwise silently freeze the whole app with no clue where (exactly how the MoltenVK
            // glyph-upload wedge presented — it took a manual `sample` + dotnet-stack to locate).
            // Wait in chunks and, if it blocks too long, log the managed call stack so the hang is a
            // named, diagnosable warning instead of a silent freeze — then keep waiting (behaviour
            // is unchanged for normal, fast waits).
            let chunkNs = 5_000_000_000UL // 5s
            let mutable warned = false
            while not (x.TryWait chunkNs) do
                if not warned then
                    warned <- true
                    Log.warn "[Vulkan] fence wait has blocked >5s — likely a GPU hang (vkWaitForFences not returning). Still waiting. Stack:\n%s"
                        (System.Diagnostics.StackTrace(true).ToString())

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module DeviceFenceExtensions =

    type IDevice with
        member x.CreateFence([<Optional; DefaultParameterValue(false)>] signaled: bool) =
            new Fence(x, signaled)