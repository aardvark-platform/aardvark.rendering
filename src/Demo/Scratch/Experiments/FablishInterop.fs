namespace Scratch

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Rendering.GL
open System.Windows.Forms

open Fablish
open Fable.Helpers.Virtualdom
open Fable.Helpers.Virtualdom.Html

module FablishInterop =

    module FablishApp =
        type Model = unit // nop
        type Action = unit

        let update m a = m
        let view m : DomNode<Action> = 
            div [] [
                div [ attribute "id" "renderControl"; Style [ "height", "800px"; "width", "800px" ]] [text "render control"]
                div [] [ button [] [text "abc"]; button [] [text "cde"]]
            ]

    let run argv  =
        ChromiumUtilities.unpackCef()
        Chromium.init argv

        Ag.initialize()
        Aardvark.Init()

        use glApp  = new OpenGlApplication()
        use ctrl   = new Aardvark.Application.WinForms.RenderControl()
        let glCtrl = new Aardvark.Application.WinForms.OpenGlRenderControl(glApp.Runtime)
        ctrl.Implementation <- glCtrl

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            ctrl.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time view

        let quadSg =
            let quad =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = ([|0;1;2; 0;2;3|] :> Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions,                  [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                            DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                            DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                        ]
                )
                
            quad |> Sg.ofIndexedGeometry

        let sg =
            quadSg 
                |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                  ]
               |> Sg.viewTrafo (viewTrafo   |> Mod.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )

        let task = ctrl.Runtime.CompileRender(ctrl.FramebufferSignature, BackendConfiguration.ManagedOptimized, sg.RenderObjects())

        ctrl.RenderTask <- task

        use w = new Form()
        let desiredSize = V2i(1280,960)
        let mutable initialized = false
        w.Width <- desiredSize.X
        w.Height <- desiredSize.Y

        let changeViewport (c : Fablish.JavascriptInterop.ClientRect) =
            let change () = 
                ctrl.set_Location(Drawing.Point(int c.left, int c.top))
                ctrl.Size <- Drawing.Size(int c.width,int c.height)

            ctrl.Invoke(Action(change)) |> ignore

        let onRendered model view =
            {
                clientSide = """() => { 
                    var rect = document.getElementById("renderControl").getBoundingClientRect();
                    return { bottom : rect.bottom.toFixed(), height : rect.height.toFixed(), left : rect.left.toFixed(), right : rect.right.toFixed(), top : rect.top.toFixed(), width : rect.width.toFixed() }; 
                } """   
                serverSide = fun (s : string) -> 
                    let rect = ClientRect.ofString s
                    printfn "clientRect: %A" rect
                    changeViewport rect
                    None
            }

        let app = 
            {
                initial = ()
                update = FablishApp.update
                view = FablishApp.view
                onRendered = onRendered
            }

        let browser = Chromium.runControl "8083" app

        use panel = new Panel()
        panel.Controls.Add browser
        panel.Size <- Drawing.Size(200,200)
        panel.Dock <- DockStyle.Fill

        w.Controls.Add ctrl
        w.Controls.Add panel
        w.Width <- desiredSize.X
        w.Height <- desiredSize.Y
        ctrl.BringToFront()

        Application.Run(w) 
        0





