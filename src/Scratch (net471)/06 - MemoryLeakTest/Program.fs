﻿open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Application.WinForms.Vulkan

module Shader =
    open FShade 

    type Vertex =
        {
            [<Position>] pos    : V4d
            [<WorldPosition>] wp : V4d
        }

    let badTrafo (v : Vertex) =
        vertex {
            let wp = uniform.ModelTrafo * v.pos
            let p = uniform.ModelViewProjTrafo * v.pos
            return { pos = p; wp = wp }
        }

[<EntryPoint>]
let main argv = 
    
    
    Aardvark.Init()

    use app = new OpenGlApplication()
    let win = app.CreateSimpleRenderWindow(8)
    win.Title <- "Memory Leak"
    

    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    let frustum = 
        win.Sizes 
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let stuff = cset()
    let sphere = IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere Sphere3d.Unit 10 C4b.White
    let sphere = Sg.ofIndexedGeometry sphere
    stuff.Add(sphere) |> ignore

    let sg =
            stuff
            |> Sg.set
            |> Sg.effect [
                    Shader.badTrafo                       |> toEffect // using ModelView* trafo will pin the computation and source indefinitely
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                ]
            |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo    )


    let renderTask = 
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    
    let renderTask =
        RenderTask.custom (fun (self,token,outputDesc,queries) -> 
            win.Time.GetValue self |> ignore

            transact (fun () -> 
                    // clear old geometry and create new
                    stuff.Clear() 
                    let sphere = IndexedGeometryPrimitives.Sphere.solidPhiThetaSphere Sphere3d.Unit 16 C4b.White
                    // this input and trafo computation will leak forever due to BinaryCache holding Mod2Map of Model*View 
                    // -> fixed in 39a811d
                    // remaining pseudo leak:
                    // global inputs like "CameraLocation" will hold WeakReferences to already collected 
                    // temporary short living runtime outputs as long as the input does not change
                    // after 1m of not moving the camera 1k WeakReference
                    let leak = AVal.init (sphere, Trafo3d.Identity) 
                    let trafo = leak |> AVal.map (fun (_, t) -> t)
                    let sg = Sg.ofIndexedGeometry sphere
                                    |> Sg.trafo trafo
                    stuff.Add(sg) |> ignore
                )
            
            renderTask.Run(token,outputDesc,queries)
        )
        
    win.RenderTask <- renderTask
    win.Run()
    
    0
