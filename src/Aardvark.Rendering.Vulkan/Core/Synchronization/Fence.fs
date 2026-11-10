namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Runtime.InteropServices
open Microsoft.FSharp.Core

#nowarn "9"

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
        if not <| x.TryWait(timeoutInNanoseconds) then
            raise <| TimeoutException()

    interface IDeviceObject with
        member x.DeviceInterface = x.DeviceInterface

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module DeviceFenceExtensions =

    type IDevice with
        member x.CreateFence([<Optional; DefaultParameterValue(false)>] signaled: bool) =
            new Fence(x, signaled)