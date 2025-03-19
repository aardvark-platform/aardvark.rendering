namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open System
open System.Runtime.InteropServices

type VulkanException(error : VkResult, message : string, [<Optional; DefaultParameterValue(null : Exception)>] innerException : Exception) =
    inherit Exception(message, innerException)

    new(message : string, [<Optional; DefaultParameterValue(null : Exception)>] innerException : Exception) =
        VulkanException(VkResult.ErrorUnknown, message, innerException)

    member x.Error = error
    override x.Message = $"{message} (Error: {error})"

[<AutoOpen>]
module internal Error =

    let check (str : string) (err : VkResult) =
        if err <> VkResult.Success then
            let msg =
                if String.IsNullOrEmpty str then "An error occurred"
                else string (Char.ToUpper str.[0]) + str.Substring(1)

            Report.Error $"[Vulkan] {msg} (Error: {err})"
            raise <| VulkanException(err, msg)

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