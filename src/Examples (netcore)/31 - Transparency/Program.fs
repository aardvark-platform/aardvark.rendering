(*

This example demonstrates various techniques for order-independent transparency (OIT):

 * unsorted alpha blending as a reference for the artifacts the other techniques avoid
 * Weighted Blended Order-Independent Transparency [McGuire2013], implemented in this example
 * the transparency support built into the runtime (Sg.transparent): weighted blended OIT and,
   where fragment shader interlock is available, an exact per-pixel A-buffer

The scene combines a checkerboard floor and lit opaque objects with transparent groups that each
expose a weakness of the techniques: a stack of eight fanned panes (eight layers, the capacity of
the A-buffer), three panes crossing on a common axis (rotating, so sorting errors flicker), a sphere
nested in a box around an opaque core, two interpenetrating boxes, a cylinder piercing a pane, and
an almost opaque pane in front of a bright one.

The project is organized in a way that makes it possible to easily add more techniques in the future.

*)

namespace TransparencyExample

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.ImGui
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

module Program =

    type IRenderTask with
        member x.PrepareForRender() =
            match x.FramebufferSignature with
                | Some signature ->
                    let runtime = signature.Runtime
                    let tempFbo = runtime.CreateFramebuffer(signature, AVal.constant(V2i(16,16)))
                    tempFbo.Acquire()
                    x.Run(RenderToken.Empty, tempFbo.GetValue())
                    tempFbo.Release()
                | None ->
                    ()

    let floorSg =
        Sg.quad
        |> Sg.diffuseTexture DefaultTextures.checkerboard
        |> Sg.scale 10.0

    module Scene =

        /// a vertical 2x2 pane (XZ plane) with the given color
        let private pane (color : C4f) =
            Sg.quad
            |> Sg.transform (Trafo3d.RotationXInDegrees 90.0)
            |> Sg.uniform' "Color" color

        let private tinted (color : C4f) (sg : ISg) =
            sg |> Sg.uniform' "Color" color

        /// objects standing on the floor are lifted by an unnoticeable amount, so their
        /// bottom faces do not fight with the floor at equal depth
        let private lift = 0.005

        let private box (size : V3d) =
            Sg.box' C4b.White (Box3d.FromCenterAndSize(V3d.Zero, size))

        /// eight fanned panes: up to eight transparent layers per pixel (the capacity of the
        /// A-buffer); the order of the accumulated tints tells weighted blending apart from
        /// the exact result
        let private paneStack =
            [| C3f.DodgerBlue; C3f.Salmon; C3f.SpringGreen; C3f.Plum; C3f.Gold; C3f.MediumOrchid; C3f.Turquoise; C3f.Coral |]
            |> Array.mapi (fun i c ->
                pane (C4f(c, 0.3f))
                |> Sg.transform (Trafo3d.RotationZInDegrees (12.0 * float i))
                |> Sg.translate -3.5 (-2.4 + 0.6 * float i) 2.0
            )
            |> Sg.ofArray

        /// three panes crossing on a common vertical axis: the intersection lines reveal
        /// every per-object sorting error; rotated by `angle`
        let private star (angle : aval<float>) =
            [ C3f.Gold, 0.0; C3f.MediumOrchid, 60.0; C3f.Teal, 120.0 ]
            |> List.map (fun (c, rotation) ->
                pane (C4f(c, 0.4f)) |> Sg.transform (Trafo3d.RotationZInDegrees rotation)
            )
            |> Sg.ofList
            |> Sg.trafo (angle |> AVal.map Trafo3d.RotationZ)
            |> Sg.translate 3.5 -3.5 2.0

        /// a transparent sphere inside a transparent box: four layers over the opaque core
        let private nested =
            Sg.ofList [
                box (V3d(3.0)) |> tinted (C4f(C3f.LightSkyBlue, 0.2f))
                Sg.sphere' 4 C4b.White 1.0 |> tinted (C4f(C3f.Orange, 0.35f))
            ]
            |> Sg.translate 3.5 3.5 (1.5 + lift)

        /// two boxes of different height standing on the same spot at different rotations:
        /// overlapping volumes (no coplanar faces, those would fight on equal depth)
        let private interpenetrating =
            [ C3f.SpringGreen, 30.0, V3d(2.0, 2.0, 2.0); C3f.Magenta, 75.0, V3d(1.5, 1.5, 2.8) ]
            |> List.map (fun (c, rotation, size) ->
                box size
                |> tinted (C4f(c, 0.3f))
                |> Sg.transform (Trafo3d.RotationZInDegrees rotation)
                |> Sg.translate 0.0 0.0 (size.Z / 2.0 + lift)      // each standing on the floor
            )
            |> Sg.ofList
            |> Sg.translate -3.5 3.5 0.0

        /// a cylinder piercing a pane: a curved intersection
        let private pierced =
            Sg.ofList [
                pane (C4f(C3f.SteelBlue, 0.35f))
                Sg.cylinder' 32 C4b.White 0.6 3.0
                |> tinted (C4f(C3f.Crimson, 0.5f))
                |> Sg.transform (Trafo3d.RotationYInDegrees 90.0)
                |> Sg.translate -1.5 0.0 0.0
            ]
            |> Sg.translate 0.0 -4.5 1.2

        /// an almost opaque pane in front of a bright one, both facing the camera: weighted
        /// blending lets the bright pane bleed through, the exact result does not
        let private occluder =
            Sg.ofList [
                pane (C4f(C3f.Yellow, 0.25f))
                pane (C4f(C3f.DarkSlateGray, 0.9f)) |> Sg.translate 0.0 0.5 0.0
            ]
            |> Sg.transform (Trafo3d.RotationZInDegrees -45.0)
            |> Sg.translate 0.0 4.5 2.0

        /// the lit opaque objects: the core of the nested group, a pillar behind the pane
        /// stack and one in front of the star
        let opaque =
            Sg.ofList [
                Sg.sphere' 4 C4b.FireBrick 0.4 |> Sg.translate 3.5 3.5 1.5
                Sg.box' C4b.Gray (Box3d.FromCenterAndSize(V3d(-5.0, -3.5, 1.5 + lift), V3d(0.6, 0.6, 3.0)))
                Sg.box' C4b.Gray (Box3d.FromCenterAndSize(V3d(5.5, -3.5, 1.5 + lift), V3d(0.6, 0.6, 3.0)))
            ]
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.vertexColor
                do! DefaultSurfaces.simpleLighting
            }

        /// the transparent groups; closed shapes show both faces
        let transparent (angle : aval<float>) =
            Sg.ofList [ paneStack; star angle; nested; interpenetrating; pierced; occluder ]
            |> Sg.cullMode' CullMode.None

        /// the angle of the animated group: advances by 20 degrees per second while
        /// `animate` is on and stays put otherwise
        let rotation (time : aval<System.DateTime>) (animate : aval<bool>) =
            let angle = ref 0.0
            let last = ref None
            AVal.custom (fun t ->
                if animate.GetValue t then
                    let now = time.GetValue t
                    match last.Value with
                    | Some l ->
                        let d : System.TimeSpan = now - l
                        angle.Value <- angle.Value + d.TotalSeconds * 20.0 * Constant.RadiansPerDegree
                    | None -> ()
                    last.Value <- Some now
                else
                    last.Value <- None
                angle.Value
            )

    [<EntryPoint>]
    let main _argv =

        Aardvark.Init()

        // uncomment/comment to switch between the backends
        use app = new VulkanApplication(debug = true)
        //use app = new OpenGlApplication(debug = true)
        let runtime = app.Runtime :> IRuntime

        // create a game window (better for measuring fps)
        use win = app.CreateGameWindow(samples = 8)
        win.RenderAsFastAsPossible <- true

        use gui = win.InitializeImGui()

        let initialView = CameraView.LookAt(V3d(10.0,10.0,10.0), V3d.Zero, V3d.OOI)
        let frustum =
            win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 150.0 (float s.X / float s.Y))

        let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

        let currentTechnique = AVal.init 0
        let backgroundColor = AVal.init C3b.PaleTurquoise
        let animate = AVal.init true

        let framebuffer =
            {
                size = win.Sizes
                signature = win.FramebufferSignature
                clearColor = backgroundColor |> AVal.map c4b
            }

        let scene =
            {
                opaque = Sg.ofList [ floorSg; Scene.opaque ]
                transparent = Scene.transparent (Scene.rotation win.Time animate)
                viewTrafo = cameraView |> AVal.map CameraView.viewTrafo
                projTrafo = frustum |> AVal.map Frustum.projTrafo
            }

        let techniques =
            [|
                new Naive.Technique(runtime, framebuffer, scene) :> ITechnique
                new WeightedBlended.Technique(runtime, framebuffer, scene) :> ITechnique
                new BuiltIn.Technique(runtime, framebuffer, scene, Transparency.WeightedBlended) :> ITechnique
                if runtime.FragmentShaderInterlock then
                    new BuiltIn.Technique(runtime, framebuffer, scene, Transparency.ABuffer) :> ITechnique
            |]

        gui.Render <- fun () ->
            if ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize) then
                ImGui.ColorEdit3("Background color", backgroundColor, ImGuiColorEditFlags.NoInputs)
                ImGui.Checkbox("Animate", animate)

                let technique = techniques.[currentTechnique.Value]
                if ImGui.BeginCombo("##technique", technique.Name) then
                    for i = 0 to techniques.Length - 1 do
                        if ImGui.Selectable(techniques.[i].Name, (currentTechnique.Value = i)) then
                            currentTechnique.Value <- i
                    ImGui.EndCombo()
            ImGui.End()

        // precompile
        for t in techniques do
            Log.startTimed "[%s] compile scene" t.Name
            t.Task.PrepareForRender()
            Log.stop()

        use task =
            RenderTask.custom (fun (t, rt, desc) ->
                let current = currentTechnique.GetValue(t)
                techniques.[current].Task.Run(t, rt, desc)
            )

        use guiTask =
            gui
            |> Sg.compile win.Runtime win.FramebufferSignature

        win.RenderTask <- RenderTask.ofList [ task; guiTask ]
        win.Run()

        for t in techniques do
            t.Dispose()

        0