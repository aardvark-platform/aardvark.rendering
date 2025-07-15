namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Runtime.InteropServices

#nowarn "9"
#nowarn "51"

type VulkanException(error : VkResult, message : string, [<Optional; DefaultParameterValue(null : Exception)>] innerException : Exception) =
    inherit Exception(message, innerException)

    new(message : string, [<Optional; DefaultParameterValue(null : Exception)>] innerException : Exception) =
        VulkanException(VkResult.ErrorUnknown, message, innerException)

    member x.Error = error
    override x.Message = $"{message} (Error: {error})"

[<AutoOpen>]
module internal Error =

    module DeviceFault =
        open EXTDeviceFault

        let private getDescription (str: String256) =
            let value = str.Value
            if String.IsNullOrWhiteSpace value then "No description"
            else value

        let report (device: VkDevice) =
            let mutable counts = VkDeviceFaultCountsEXT.Empty

            VkRaw.vkGetDeviceFaultInfoEXT(device, &&counts, NativePtr.zero) |> ignore
            let addressInfos = Array.zeroCreate<VkDeviceFaultAddressInfoEXT> (int counts.addressInfoCount)
            use pAddressInfos = fixed addressInfos

            let vendorInfos = Array.zeroCreate<VkDeviceFaultVendorInfoEXT> (int counts.vendorInfoCount)
            use pVendorInfos = fixed vendorInfos

            let vendorBinaryData = Array.zeroCreate<uint8> (int counts.vendorBinarySize)
            use pVenodrBinaryData = fixed vendorBinaryData

            let mutable info = VkDeviceFaultInfoEXT.Empty
            info.pAddressInfos <- pAddressInfos
            info.pVendorInfos <- pVendorInfos
            info.pVendorBinaryData <- pVenodrBinaryData.Address

            VkRaw.vkGetDeviceFaultInfoEXT(device, &&counts, &&info) |> ignore
            let builder = Text.StringBuilder()

            builder.AppendLine $"Description: {getDescription info.description}" |> ignore

            if counts.vendorInfoCount > 0u then
                builder.AppendLine() |> ignore
                builder.AppendLine("Vendor-specific information:") |> ignore

            for i = 0 to int counts.vendorInfoCount - 1 do
                let info = info.pVendorInfos.[i]
                builder.AppendLine $"{getDescription info.description} (Code: {info.vendorFaultCode}, Data: {info.vendorFaultData})" |> ignore

            if counts.addressInfoCount > 0u then
                builder.AppendLine() |> ignore
                builder.AppendLine("Addresses:") |> ignore

            for i = 0 to int counts.addressInfoCount - 1 do
                let info = info.pAddressInfos.[i]
                builder.AppendLine $"  0x%016X{info.reportedAddress} ({info.addressType})" |> ignore

            let message = builder.ToString() |> String.indent 1
            let nl = Environment.NewLine
            Report.ErrorNoPrefix $"[Vulkan] Device fault:{nl}{nl}{message}"

    let check (message: string) (result: VkResult) =
        if result <> VkResult.Success then
            let msg =
                if String.IsNullOrEmpty message then "An error occurred"
                else string (Char.ToUpper message.[0]) + message.Substring(1)

            Report.Error $"[Vulkan] {msg} (Error: {result})"
            raise <| VulkanException(result, msg)

    let checkf (fmt : Printf.StringFormat<'a, VkResult -> unit>) =
        Printf.kprintf check fmt

    let inline failf fmt =
        Printf.kprintf (fun str ->
            let str =
                if String.IsNullOrEmpty str then "An error occurred"
                else string (Char.ToUpper str.[0]) + str.Substring(1)

            let msg = $"[Vulkan] {str}"
            Report.Error msg
            failwith msg
        ) fmt