namespace Aardvark.Rendering.Vulkan.Raytracing

open Aardvark.Base
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices

type PreparedPipelineState(device : Device, pipeline : IResourceLocation<VkPipeline>) =
    inherit Resource(device)

    member x.Pipeline = pipeline

    override x.Destroy() =
        ()


[<AbstractClass; Sealed; Extension>]
type DevicePreparedRenderObjectExtensions private() =

    [<Extension>]
    static member PreparePipelineState(this : ResourceManager, state : PipelineState) =
        new PreparedPipelineState(this.Device, Unchecked.defaultof<_>)