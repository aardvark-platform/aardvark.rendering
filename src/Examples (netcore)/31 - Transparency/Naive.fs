namespace Transparency

module Naive =

    open Aardvark.Base
    open Aardvark.Base.Rendering
    open Aardvark.SceneGraph
    open FSharp.Data.Adaptive.Operators

    type Technique(runtime : IRuntime, framebuffer : FramebufferInfo, scene : Scene) =

        let opaque =
            scene.opaque
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
            }

        let transparent =
            scene.transparent
            |> Sg.blendMode ~~BlendMode.Blend
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.sgColor
            }

        let makeTask sg =
            let sg =
                sg |> Sg.viewTrafo scene.viewTrafo |> Sg.projTrafo scene.projTrafo

            runtime.CompileRender(framebuffer.signature, sg)

        let opaqueTask = makeTask opaque
        let transparentTask = makeTask transparent

        member x.Task = RenderTask.ofList [opaqueTask; transparentTask]

        member x.Dispose() =
            opaqueTask.Dispose()
            transparentTask.Dispose()

        interface ITechnique with
            member x.Name = "Alpha blending (unsorted)"
            member x.Task = x.Task
            member x.Dispose() = x.Dispose()