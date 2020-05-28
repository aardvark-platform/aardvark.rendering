namespace Aardvark.Application.WinForms.Vulkan

open System
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Reflection
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module private Utilities =
        
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

    let check (str : string) (err : VkResult) =
        if err <> VkResult.VkSuccess then failwithf "[Vulkan] %s" str

    let checkf (fmt : Printf.StringFormat<'a, VkResult -> unit>) =
        Printf.kprintf (fun str ->
            fun (res : VkResult) ->
                if res <> VkResult.VkSuccess then failwith ("[Vulkan] " + str)
        ) fmt

    let inline failf fmt = Printf.kprintf (fun str -> failwith ("[Vulkan] " + str)) fmt
