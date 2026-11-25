namespace TransparencyExample

module BuiltIn =

    open Aardvark.Rendering
    open Aardvark.SceneGraph

    /// Uses the transparency support of the runtime: the transparent scene is marked with
    /// Sg.transparent and the render task renders it with the requested technique.
    type Technique(runtime : IRuntime, framebuffer : FramebufferInfo, scene : Scene, transparency : Transparency) =
        let name =
            match transparency with
            | Transparency.WeightedBlended -> "Weighted Blended OIT"
            | Transparency.ABuffer -> "A-Buffer OIT"
            | _ -> string transparency

        let opaque =
            scene.opaque
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
            }

        let transparent =
            scene.transparent
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.sgColor
                do! DefaultSurfaces.simpleLighting
            }
            |> Sg.transparent

        let clear =
            runtime.CompileClear(framebuffer.signature, framebuffer.clearColor)

        let task =
            let sg =
                Sg.ofList [ opaque; transparent ]
                |> Sg.viewTrafo scene.viewTrafo
                |> Sg.projTrafo scene.projTrafo

            runtime.CompileRender(framebuffer.signature, sg, transparency)

        member x.Task = RenderTask.ofList [clear; task]

        member x.Dispose() =
            clear.Dispose()
            task.Dispose()

        interface ITechnique with
            member x.Name = $"{name} (built-in)"
            member x.Task = x.Task
            member x.Dispose() = x.Dispose()
