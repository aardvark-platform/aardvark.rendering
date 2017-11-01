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
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Text


module GeometryComposition =
    
    module Shader =
        open FShade

        type Vertex = { [<Position>] p : V4d; [<WorldPosition>] wp : V4d; [<Color>] c : V4d; [<Normal>] n : V3d }

        let gs0 (v : Triangle<Vertex>) =
            triangle {
                
                let n = v.P0.n
                let cp = (v.P0.wp + v.P1.wp + v.P2.wp) / 3.0
                //let cc = (v.P0.c + v.P1.c + v.P2.c) / 3.0

                yield { v.P0 with c = V4d.IOOI; n = n }
                yield { v.P1 with c = V4d.IOOI; n = n }
                yield { p = uniform.ViewProjTrafo * cp; wp = cp; c = V4d.IOOI; n = n }
                restartStrip()
                
                yield { p = uniform.ViewProjTrafo * cp; wp = cp; c = V4d.OIOI; n = n }
                yield { v.P1 with c = V4d.OIOI; n = n }
                yield { v.P2 with c = V4d.OIOI; n = n }
                restartStrip()
                
                yield { v.P2 with c = V4d.OOII; n = n }
                yield { v.P0 with c = V4d.OOII; n = n }
                yield { p = uniform.ViewProjTrafo * cp; wp = cp; c = V4d.OOII; n = n }

            }

        let gs1 (v : Triangle<Vertex>) =
            triangle {
            
                let cp = (v.P0.wp + v.P1.wp + v.P2.wp) / 3.0
                
                let r = 0.9

                let a = V4d(cp.XYZ * (1.0 - r) + v.P0.wp.XYZ * r, v.P0.wp.W)
                let b = V4d(cp.XYZ * (1.0 - r) + v.P1.wp.XYZ * r, v.P1.wp.W)
                let c = V4d(cp.XYZ * (1.0 - r) + v.P2.wp.XYZ * r, v.P2.wp.W)

                yield { v.P0 with p = uniform.ViewProjTrafo * a; wp = a } 
                yield { v.P1 with p = uniform.ViewProjTrafo * b; wp = b } 
                yield { v.P2 with p = uniform.ViewProjTrafo * c; wp = c } 

            }

    let run() =
        use app = new OpenGlApplication(true)
        let win = app.CreateSimpleRenderWindow(8)



        let cameraView  =  DefaultCameraController.control win.Mouse win.Keyboard win.Time (CameraView.LookAt(V3d(2.5, -3.0, 1.5), V3d(2.5, 0.0, 0.0), V3d.OOI))    
        let frustum     =  win.Sizes    |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))       
            
        let viewTrafo   = cameraView    |> Mod.map CameraView.viewTrafo
        let projTrafo   = frustum       |> Mod.map Frustum.projTrafo        


        let rec allInsertions (v : 'a) (l : list<'a>) =
            match l with
                | [] -> [[v]]
                | h :: t ->
                    List.concat [
                        [v :: h :: t]
                        allInsertions v t |> List.map (fun r -> h :: r)
                    ]

        let rec all (l : list<'a>) =
            match l with
                | [] -> [[]]
                | h :: t ->
                    let t = all t
                    List.append
                        (t |> List.collect (fun l -> allInsertions h l))
                        (t)

        let available = 
            [ 
                "divide", toEffect Shader.gs0
                "shrink", toEffect Shader.gs1 
            ]

        let combinations = 
            all available
                |> List.sortBy List.length
                |> List.map (fun l ->
                    let name = l |> List.map fst |> String.concat ", " |> sprintf "{ %s }"
                    let effect = l |> List.map snd |> FShade.Effect.compose
                    name, effect
                )

        let font = Font("Consolas")

        win.RenderTask <-
            Sg.ofList [
                let mutable index = 0
                for (name, effect) in combinations do

                    let label = 
                        Sg.text font C4b.White (Mod.constant name)
                            |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Scale(0.1))
                            |> Sg.translate 0.0 0.0 1.5

                    let inner = 
                        Sg.box' C4b.Red Box3d.Unit
                            |> Sg.shader {
                                do! DefaultSurfaces.trafo
                                do! effect
                                do! DefaultSurfaces.vertexColor
                                do! DefaultSurfaces.simpleLighting
                            }

                    yield 
                        Sg.ofList [
                            inner
                                |> Sg.cullMode (Mod.constant CullMode.Clockwise)

                            inner
                                |> Sg.translate 0.0 0.0 -1.5
                                |> Sg.fillMode (Mod.constant FillMode.Line)
                                |> Sg.cullMode (Mod.constant CullMode.Clockwise)
                            label
                        ]
                        |> Sg.translate (1.5 * float index) 0.0 0.0

                    index <- index + 1
            ]
            //|> Sg.fillMode (Mod.constant FillMode.Line)
            |> Sg.viewTrafo viewTrafo
            |> Sg.projTrafo projTrafo
            |> Sg.compile app.Runtime win.FramebufferSignature

        win.Run()
        win.Dispose()

