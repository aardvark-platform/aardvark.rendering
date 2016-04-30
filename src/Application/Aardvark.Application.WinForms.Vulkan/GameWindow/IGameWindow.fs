namespace Aardvark.Application.WinForms

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Rendering.Vulkan

type IVulkanGameWindow =
    inherit IRenderControl

    abstract member Run : unit -> unit