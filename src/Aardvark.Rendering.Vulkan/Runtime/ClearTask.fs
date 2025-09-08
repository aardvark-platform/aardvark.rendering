namespace Aardvark.Rendering.Vulkan

open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive

type ClearTask(manager : ResourceManager, renderPass : RenderPass, values : aval<ClearValues>) =
    inherit CommandTask(manager, renderPass, RuntimeCommand.Clear values)

    override _.DefaultName = "Clear Task"
    override _.DebugColor = DebugColor.ClearTask