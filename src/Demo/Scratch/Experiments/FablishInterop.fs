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
            div [attribute "class" "container"; Style ["diplay","inline-block"]] [
                div [ attribute "id" "left_renderControl"; Style [ "height", "600px"; "width", "300px"; "float", "left" ; "background-color", "red"]] [text "render control"]
                div [ attribute "id" "right_renderControl"; Style [ "height", "600px"; "width", "300px" ; "float", "right"]] [text "render control"]
                div [] [ button [] [text "abc"]; button [] [text "cde"]]
            ]

    let createScene (ctrl : IRenderControl) =

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

        quadSg 
            |> Sg.effect [
                DefaultSurfaces.trafo                 |> toEffect
                DefaultSurfaces.constantColor C4f.Red |> toEffect
                DefaultSurfaces.simpleLighting        |> toEffect
                ]
            |> Sg.viewTrafo (viewTrafo   |> Mod.map CameraView.viewTrafo )
            |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )

    let run argv  =
        ChromiumUtilities.unpackCef()
        Chromium.init argv

        Ag.initialize()
        Aardvark.Init()

        use glApp  = new OpenGlApplication()
        use leftCtrl   = new Aardvark.Application.WinForms.RenderControl()
        let leftGlCtrl = new Aardvark.Application.WinForms.OpenGlRenderControl(glApp.Runtime)
        leftCtrl.Implementation <- leftGlCtrl

        use rightCtrl   = new Aardvark.Application.WinForms.RenderControl()
        let rightGlCtrl = new Aardvark.Application.WinForms.OpenGlRenderControl(glApp.Runtime)
        rightCtrl.Implementation <- rightGlCtrl

        leftCtrl.RenderTask <- 
            RenderTask.ofList [
                    leftCtrl.Runtime.CompileClear(leftCtrl.FramebufferSignature,Mod.constant C4f.DarkBlue)
                    leftCtrl.Runtime.CompileRender(leftCtrl.FramebufferSignature, createScene leftCtrl)
            ]


        rightCtrl.RenderTask <- 
            RenderTask.ofList [
                    leftCtrl.Runtime.CompileClear(rightCtrl.FramebufferSignature,Mod.constant C4f.DarkRed)
                    leftCtrl.Runtime.CompileRender(rightCtrl.FramebufferSignature, createScene rightCtrl)
            ]

        use w = new Form()
        w.TransparencyKey <- Drawing.Color.Red
        w.AllowTransparency <- true
        w.BackColor <- Drawing.Color.CadetBlue
        let desiredSize = V2i(1024,768)
        let mutable initialized = false
        w.Width <- desiredSize.X
        w.Height <- desiredSize.Y

        let changeViewport (ctrl : RenderControl) (c : Fablish.JavascriptInterop.ClientRect) =
            let change () = 
                ctrl.set_Location(Drawing.Point(int c.left, int c.top))
                ctrl.Size <- Drawing.Size(int c.width,int c.height)

            ctrl.Invoke(Action(change)) |> ignore


        let onRendered model view =
            {
                clientSide = """() => { 
                    var leftRect = document.getElementById("left_renderControl").getBoundingClientRect();
                    var rightRect = document.getElementById("right_renderControl").getBoundingClientRect();
                    var leftRect_Fixed = { bottom : leftRect.bottom.toFixed(), height : leftRect.height.toFixed(), left : leftRect.left.toFixed(), right : leftRect.right.toFixed(), top : leftRect.top.toFixed(), width : leftRect.width.toFixed() }; 
                    var rightRect_Fixed = { bottom : rightRect.bottom.toFixed(), height : rightRect.height.toFixed(), left : rightRect.left.toFixed(), right : rightRect.right.toFixed(), top : rightRect.top.toFixed(), width : rightRect.width.toFixed() }; 
                    return JSON.stringify(leftRect_Fixed) + "+" + JSON.stringify(rightRect_Fixed);
                } """   
                serverSide = fun (s : string) -> 
                    match s.Split([|"+"|], StringSplitOptions.RemoveEmptyEntries) with
                        | [|leftStr;rightStr|] -> 
                            let left = ClientRect.ofString (leftStr.Substring(1,leftStr.Length-1).Replace("\\",""))
                            let t =  (rightStr.Substring(0,rightStr.Length-1).Replace("\\",""))
                            let right = ClientRect.ofString t
                            printfn "clientRect: %A" (left,right)
                            changeViewport leftCtrl left
                            changeViewport rightCtrl right
                            None
                        | _ -> failwithf "strange result from js: %A" s
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
        panel.Size <- Drawing.Size(640,480)
        panel.Dock <- DockStyle.Fill
        panel.BackColor <- Drawing.Color.Transparent
        

        w.Controls.Add leftCtrl
        w.Controls.Add rightCtrl
        w.Controls.Add panel
        w.Width <- desiredSize.X
        w.Height <- desiredSize.Y
        //panel.BringToFront()
        leftCtrl.BringToFront()
        panel.BringToFront()
        rightCtrl.BringToFront()

        Application.Run(w) 
        0





