open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

(*

This example demonstrates how to implement picking by rendering object IDs into a texture, which is used for subsequent lookups.

*)

module Semantic =
    let CustomId = Sym.ofString "CustomId"
    let All = Set.ofList [DefaultSemantic.Colors; CustomId]

module Shader =
    open FShade

    type CustomIdAttribute() =
        inherit SemanticAttribute("CustomId")

    type Fragment = {
        [<CustomId; Interpolation(InterpolationMode.Flat)>] id : int
        [<FragCoord>] coord : V4d
    }

    [<AutoOpen>]
    module private Samplers =

        let diffuseSampler =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

        let diffuseSamplerMS =
            sampler2dMS {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

        let diffuseSamplerInt =
            intSampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }

        let diffuseSamplerIntMS =
            intSampler2dMS {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagPoint
            }


    let writeId (f : Fragment) =
        fragment {
            return {| CustomId = f.id |}
        }

    let resolveId (samples : int) (f : Fragment) =
        fragment {
            let id =
                if samples > 1 then
                    diffuseSamplerIntMS.Read(V2i f.coord.XY, 0)
                else
                    diffuseSamplerInt.Read(V2i f.coord.XY, 0)

            return { f with id = id.X }
        }

    let resolve (samples : int) (f : Fragment) =
        fragment {
            if samples > 1 then
                let mutable result = V4d.Zero

                for i = 0 to samples - 1 do
                    result <- result + diffuseSamplerMS.Read(V2i f.coord.XY, i)

                return result / float samples
            else
                return diffuseSampler.Read(V2i f.coord.XY, 0)
        }


[<EntryPoint>]
let main argv =

    Aardvark.Init()

    // uncomment/comment to switch between the backends
    //use app = new VulkanApplication(debug = true)
    use app = new OpenGlApplication()
    let runtime = app.Runtime :> IRuntime
    let samples = 8

    use win = app.CreateGameWindow(samples = 1)

    // Define how many cubes we want
    let rows = 32
    let cols = 32
    let count = rows * cols

    let frustum =
        win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 500.0 (float s.X / float s.Y))

    let cameraView =
        let center = V2d(float rows * 1.5, float cols * 1.5)
        let initialView = CameraView.LookAt(V3d(10.0,10.0,10.0), center.XYO, V3d.OOI)
        DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView


    let rnd = RandomSystem(0)

    // Layout cubes in a XY grid
    let instancePositions =
        Array.init count (fun index ->
            let x = index % cols
            let y = index / cols

            V3d(float x * 3.0, float y * 3.0, 0.0)
        )
        |> AVal.constant

    // Rotations are based on time
    let instanceRotations =
        let factors = Array.init count (fun _ -> rnd.UniformDouble() * 2.0 - 1.0)
        let startTime = System.DateTime.Now

        win.Time |> AVal.map (fun t ->
            let dt = (t - startTime).TotalSeconds
            factors |> Array.map (fun f -> dt * f)
        )

    let instanceTrafos =
        let offset = Trafo3d.Translation(V3d(-0.5))

        (instancePositions, instanceRotations)
        ||> AVal.map2 (
            Array.map2 (fun p r ->
                offset * Trafo3d.RotationZ(r) * Trafo3d.Translation(p)
            )
        )

    // Generate some random IDs, don't care about collisions in this example
    let instanceCustomIds =
        Array.init count (fun _ -> rnd.UniformIntNonZero()) |> AVal.constant

    // Colors are changeable
    let instanceColors =
        Array.init count (fun _ -> rnd.UniformC3f() |> C4f) |> AVal.init

    // First we render the scene into an offscreen buffer, which has the
    // default color and depth attachments, and additionally a 32bit integer texture for object IDs.
    let offscreenSignature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = samples }
            DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = samples }
            Semantic.CustomId, { format = RenderbufferFormat.R32i; samples = samples }
        ]

    use offscreenTask =
        let geometry =
            IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.Black

        let indirectBuffer =
            Array.init count (fun i ->
                DrawCallInfo(
                    FaceVertexCount = geometry.FaceVertexCount,
                    InstanceCount = 1,
                    FirstInstance = i,
                    BaseVertex = 0
                )
            )
            |> IndirectBuffer.ofArray geometry.IsIndexed

        let makeBufferView (f : 'T -> 'U) (data : aval<'T[]>) =
            let buffer =
                data |> AVal.map (fun data ->
                    let array = data |> Array.map f
                    ArrayBuffer array :> IBuffer
                )

            BufferView(buffer, typeof<'U>)

        let instanceIndex =
            instanceCustomIds |> makeBufferView id

        let instanceColor =
            instanceColors |> makeBufferView id

        let instanceTrafo =
            instanceTrafos |> makeBufferView (Trafo.forward >> M44f)

        let instanceTrafoInv =
            instanceTrafos |> makeBufferView (Trafo.backward >> M44f)

        Sg.indirectDraw geometry.Mode ~~indirectBuffer
        |> Sg.indexArray geometry.IndexArray
        |> Sg.vertexArray DefaultSemantic.Positions geometry.IndexedAttributes.[DefaultSemantic.Positions]
        |> Sg.vertexArray DefaultSemantic.Normals geometry.IndexedAttributes.[DefaultSemantic.Normals]
        |> Sg.instanceBuffer Semantic.CustomId instanceIndex
        |> Sg.instanceBuffer DefaultSemantic.Colors instanceColor
        |> Sg.instanceBuffer DefaultSemantic.InstanceTrafo instanceTrafo
        |> Sg.instanceBuffer DefaultSemantic.InstanceTrafoInv instanceTrafoInv
        |> Sg.uniform' "LightLocation" (V3d(10.0, 5.0, 15.0))
        |> Sg.shader {
            do! DefaultSurfaces.instanceTrafo
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.vertexColor
            do! DefaultSurfaces.simpleLighting
            do! Shader.writeId
        }
        |> Sg.viewTrafo (cameraView |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
        |> Sg.compile runtime offscreenSignature

    let offscreenBuffer =
        let clear =
            clear {
                colors [
                    Semantic.CustomId, C4f.Zero
                    DefaultSemantic.Colors, C4f.AliceBlue
                ]

                depth 1.0
            }

        offscreenTask |> RenderTask.renderSemanticsWithClear Semantic.All win.Sizes clear

    // Before we can use the pick ID buffer for our lookups we first need to resolve it (in case it is multisampled)
    // However, we can't use a normal resolve as this would average our samples resulting in invalid IDs possibly.
    // Instead, we use a fragment shader to extract the first sample.
    let resolvePickSignature =
        runtime.CreateFramebufferSignature [
            Semantic.CustomId, { format = RenderbufferFormat.R32i; samples = 1 }
        ]

    use resolvePickTask =
        Sg.fullScreenQuad
        |> Sg.diffuseTexture offscreenBuffer.[Semantic.CustomId]
        |> Sg.shader {
            do! Shader.resolveId samples
            do! Shader.writeId
        }
        |> Sg.compile runtime resolvePickSignature

    let pickTexture =
        let output = Set.singleton Semantic.CustomId
        resolvePickTask |> RenderTask.renderSemantics output win.Sizes |> Map.find Semantic.CustomId

    pickTexture.Acquire()

    // Finally we install a callback on mouse click events.
    // We download the resolved pick ID texture and simply lookup the value in the PixImage.
    win.Mouse.Up.Values.Add(fun btn ->
        if btn.HasFlag(MouseButtons.Left) then
            let ids = pickTexture.GetValue().Download().AsPixImage<int>()
            let pos = win.Mouse.Position.GetValue().Position

            if Vec.allGreaterOrEqual pos 0 && Vec.allSmaller pos ids.Size then
                let id = ids.Matrix.[pos]
                let index = instanceCustomIds.GetValue() |> Array.tryFindIndex ((=) id)

                match index with
                | Some idx ->
                    transact (fun _ ->
                        instanceColors.Value <-
                            instanceColors.Value |> Array.mapi (fun i c ->
                                if i <> idx then c else C4f (rnd.UniformC3f())
                            )
                    )

                | None ->
                    Log.warn "No instance with index %d" id
    )

    // The color buffer is also resolved and blitted to the framebuffer.
    use task =
        Sg.fullScreenQuad
        |> Sg.diffuseTexture offscreenBuffer.[DefaultSemantic.Colors]
        |> Sg.shader {
            do! Shader.resolve samples
        }
        |> Sg.compile runtime win.FramebufferSignature

    win.RenderTask <- task
    win.Run()

    // Cleanup
    pickTexture.Release()
    runtime.DeleteFramebufferSignature(offscreenSignature)
    runtime.DeleteFramebufferSignature(resolvePickSignature)

    0