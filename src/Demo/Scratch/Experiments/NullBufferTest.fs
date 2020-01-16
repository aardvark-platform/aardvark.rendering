namespace Rendering.Examples

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.GL


module NullBufferTest =

    let run () =

        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
        win.Text <- "Aardvark rocks \\o/"

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let quadSg =
            let quad =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = ([|0;1;2; 0;2;3|] :> Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions,                  [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                            DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                            //DefaultSemantic.Colors,                     [| C4b.Blue; C4b.Blue; C4b.Blue; C4b.Blue |] :> Array
                            DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                        ]
                )
                
            quad |> Sg.ofIndexedGeometry

        // Using NullBuffer for Colors (ShaderInput slot 0) -> Draw is not performed, but no warning or error in any form
        // USing NullBuffer for Normals (ShaderI nput slot 1) -> works
        // https://community.amd.com/thread/160069

        let nullBufferColors = BufferView(SingleValueBuffer(AVal.constant V4f.IOII), typeof<V4f>)
        let quadSg = Sg.VertexAttributeApplicator(DefaultSemantic.Colors, nullBufferColors, quadSg)

//        let nullBufferNormals = BufferView(AVal.constant (NullBuffer(V4f.OIOO) :> IBuffer), typeof<V3f>)
//        let quadSg = Sg.VertexAttributeApplicator(DefaultSemantic.Normals, nullBufferNormals, quadSg)

        let sg =
            quadSg 
                |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.vertexColor           |> toEffect
                    //DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                  ]
               |> Sg.viewTrafo (viewTrafo   |> AVal.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> AVal.map Frustum.projTrafo    )

        let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.Default, sg.RenderObjects())

        win.RenderTask <- task //|> DefaultOverlays.withStatistics
        win.Run()
        0





