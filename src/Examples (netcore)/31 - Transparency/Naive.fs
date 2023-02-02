namespace Transparency

module Naive =

    open Aardvark.Rendering
    open Aardvark.SceneGraph

    module private RenderPass =
        let transparent = RenderPass.after "transprentPass" RenderPassOrder.Arbitrary RenderPass.main

    type Technique(runtime : IRuntime, framebuffer : FramebufferInfo, scene : Scene) =

        let opaque =
            scene.opaque
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
            }

        let transparent =
            scene.transparent
            |> Sg.blendMode' BlendMode.Blend
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.sgColor
            }
            |> Sg.pass RenderPass.transparent

        let task =
            Sg.ofList [ opaque; transparent]
            |> Sg.viewTrafo scene.viewTrafo
            |> Sg.projTrafo scene.projTrafo
            |> Sg.compile runtime framebuffer.signature

        member x.Task = task

        member x.Dispose() =
            task.Dispose()

        interface ITechnique with
            member x.Name = "Alpha blending (unsorted)"
            member x.Task = x.Task
            member x.Dispose() = x.Dispose()