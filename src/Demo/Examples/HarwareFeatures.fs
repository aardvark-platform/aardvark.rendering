namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

module HarwareFeatures =
    
    let size = 50
    let bb = Backend.GL
    let game = true
    let samples = 1
    let newWin() =
        let app =
            match bb with
                | Backend.GL -> new OpenGlApplication() :> IApplication
                | Backend.Vulkan -> new VulkanApplication() :> IApplication
                | Backend.Both -> failwith "not implemented"
        let win = 
            if game && bb = Backend.GL then (unbox<OpenGlApplication> app).CreateGameWindow(samples) :> IRenderWindow
            else app.CreateSimpleRenderWindow(samples) :> IRenderWindow

        let view =
            CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                |> Mod.map CameraView.viewTrafo

        let proj =
            win.Sizes 
                |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))
                |> Mod.map Frustum.projTrafo

        win, view, proj

    let instancing() =
        let cube = Primitives.unitBox

        let trafos =
            [|
                for x in -size .. size do
                    for y in -size .. size do
                        for z in -size .. size do
                            yield M44f.Translation(float32 x,float32 y,float32 z) * M44f.Scale(0.3f)
            |]

        let call = DrawCallInfo(FaceVertexCount = cube.IndexedAttributes.[DefaultSemantic.Positions].Length, InstanceCount = trafos.Length)

        let sg =    
            Sg.render IndexedGeometryMode.TriangleList call
                |> Sg.vertexArray DefaultSemantic.Positions cube.IndexedAttributes.[DefaultSemantic.Positions]
                |> Sg.vertexArray DefaultSemantic.Normals cube.IndexedAttributes.[DefaultSemantic.Normals]
                |> Sg.instanceArray DefaultSemantic.InstanceTrafo trafos
                |> Sg.shader {
                    do! DefaultSurfaces.instanceTrafo
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.White
                    do! DefaultSurfaces.simpleLighting
                }


        show {
            display Display.Mono
            backend bb
            game true
            samples 8
            debug false
            scene sg
        }
  
    let indirect() =
        let cube = Primitives.unitBox

        let trafos =
            [|
                for x in -size .. size do
                    for y in -size .. size do
                        for z in -size .. size do
                            yield M44f.Translation(float32 x,float32 y,float32 z) * M44f.Scale(0.3f)
            |]

        let indirect =
            [|
                let mutable i = 0
                for x in -size .. size do
                    for y in -size .. size do
                        for z in -size .. size do
                            //if i % 2 = 0 then
                            yield  DrawCallInfo(FaceVertexCount = cube.IndexedAttributes.[DefaultSemantic.Positions].Length, InstanceCount = 1, FirstInstance = i)
                            inc &i
            |]

        let indirect = IndirectBuffer(ArrayBuffer indirect, indirect.Length) :> IIndirectBuffer


        let sg =    
            Sg.indirect IndexedGeometryMode.TriangleList (Mod.constant indirect)
                |> Sg.vertexArray DefaultSemantic.Positions cube.IndexedAttributes.[DefaultSemantic.Positions]
                |> Sg.vertexArray DefaultSemantic.Normals cube.IndexedAttributes.[DefaultSemantic.Normals]
                |> Sg.instanceArray DefaultSemantic.InstanceTrafo trafos
                |> Sg.shader {
                    do! DefaultSurfaces.instanceTrafo
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.White
                    do! DefaultSurfaces.simpleLighting
                }


        show {
            display Display.Mono
            backend bb
            game true
            samples 1
            debug false
            scene sg
        }

    let naive() =


        let cube = Sg.box' C4b.Red Box3d.Unit

        let template =
            cube
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.White
                    do! DefaultSurfaces.simpleLighting
                }

        let template =
            template.RenderObjects() |> ASet.toList |> List.head |> unbox<RenderObject>


        let win, view, proj = newWin()

        let cam = view |> Mod.map (fun v -> v.Backward.TransformPosProj V3d.Zero)

        let uniforms (t : Trafo3d) =
            UniformProvider.ofList [
                "ModelTrafo", Mod.constant t :> IMod
                "ViewTrafo", view :> IMod
                "CameraLocation", cam :> IMod
                "LightLocation", cam :> IMod
                "ProjTrafo", proj :> IMod
            ]

        let objs =
            ASet.ofList [
                let mutable i = 0
                for x in -size .. size do
                    for y in -size .. size do
                        for z in -size .. size do
                            let trafo = Trafo3d.Scale(0.3) * Trafo3d.Translation(float x, float y, float z)
                            yield 
                                { template with
                                    Id = newId()
                                    Uniforms = uniforms trafo
                                } :> IRenderObject
            ]


        let task = win.Runtime.CompileRender(win.FramebufferSignature, objs)

        let sw = System.Diagnostics.Stopwatch()
        let mutable cnt = 0

        let task = 
            RenderTask.ofList [
                task
                RenderTask.custom(fun _ ->
                    if cnt % 50 = 0 then
                        let t = sw.MicroTime
                        printfn "%.2ffps" (50.0 / t.TotalSeconds)
                        sw.Restart()
                        
                    cnt <- cnt + 1
                )
            ]

        win.RenderTask <- task
        win.Run()

          
    let run() =
        //instancing()
        //indirect()
        naive()


