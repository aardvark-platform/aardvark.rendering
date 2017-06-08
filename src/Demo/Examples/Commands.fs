namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.GL
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open FShade
open FShade.Imperative

module CommandTest =
    let run() =
        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
        let runtime = app.Runtime



        let initialView = CameraView.LookAt(V3d(3,3,3), V3d.Zero)
        let view = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView
        let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))


        let sg =
            Sg.box' C4b.Red (Box3d(-V3d.III, V3d.III))
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! fun (v : Effects.Vertex) ->
                        vertex {
                            let scale : float = uniform?Scale
                            let wp = V4d(v.wp.XYZ * scale, v.wp.W)
                            return { v with wp = wp; pos = uniform.ViewProjTrafo * wp }
                        }
                    do! fun (v : Effects.Vertex) ->
                        fragment {
                            let c : V4d = uniform?Color
                            return c
                        }
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)
                |> Sg.uniform "Color" (Mod.constant C4b.Red)
                |> Sg.uniform "Scale" (Mod.constant 1.0)

        let a = sg.RenderObjects() |> ASet.toList |> List.head |> unbox<RenderObject>
        let newBox (color : C4b) (scale : float) = 
            { RenderObject.Clone(a) with Uniforms = UniformProvider.union (UniformProvider.ofList [Symbol.Create "Color", Mod.constant color :> IMod; Symbol.Create "Scale", Mod.constant scale :> IMod]) a.Uniforms }
           
        let n = Mod.init 2

        let hsv2rgb (h : float) (s : float) (v : float) =
            let s = clamp 0.0 1.0 s
            let v = clamp 0.0 1.0 v

            let h = h % 1.0
            let h = if h < 0.0 then h + 1.0 else h
            let hi = floor ( h * 6.0 ) |> int
            let f = h * 6.0 - float hi
            let p = v * (1.0 - s)
            let q = v * (1.0 - s * f)
            let t = v * (1.0 - s * ( 1.0 - f ))
            match hi with
                | 1 -> C4b(q,v,p)
                | 2 -> C4b(p,v,t)
                | 3 -> C4b(p,q,v)
                | 4 -> C4b(t,p,v)
                | 5 -> C4b(v,p,q)
                | _ -> C4b(v,t,p)

        let boxes =
            seq {
                yield a
                yield! Seq.initInfinite (fun i -> newBox (hsv2rgb (float (i + 1) / 10.0) 1.0 1.0) (1.0 / float (i+1)))
            }
            |> Seq.cache

        let commands = 
            alist {
                let! n = n
                for i in 0 .. n-1 do
                    let a = Seq.item i boxes
                    yield RenderCommand.Render a
                    yield RenderCommand.Clear(Mod.constant C4f.Red, Mod.constant 1.0, Mod.constant 0)

            }

        win.Keyboard.DownWithRepeats.Values.Add(fun k ->    
            let newValue =
                match k with
                    | Keys.Add -> n.Value + 1
                    | Keys.Subtract -> max 1 (n.Value - 1)
                    | _ -> n.Value
            
            if newValue <> n.Value then
                Log.warn "count: %d" newValue
                transact (fun () -> n.Value <- newValue)
        )

        let task = new Aardvark.Rendering.GL.RenderTasks.CommandRenderTask(runtime.ResourceManager, win.FramebufferSignature, commands, Mod.constant BackendConfiguration.Default, true, true)

        win.RenderTask <- task
        win.Run()
  


