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

        type Vertex = { [<Position>] p : V4d; [<WorldPosition>] wp : V4d; [<Color>] c : V4d; [<Normal>] n : V3d; [<Semantic("LightDir")>] ldir : V3d }

        let gs0 (v : Triangle<Vertex>) =
            triangle {
                
                let cc = v.P0.c
                let n = v.P0.n
                let cp = (v.P0.wp + v.P1.wp + v.P2.wp) / 3.0
                //let cc = (v.P0.c + v.P1.c + v.P2.c) / 3.0


                // 0 1 c   c 1 2   c 2 3
                yield { v.P0 with c = cc; n = n }
                yield { v.P1 with c = cc; n = n }
                yield { p = uniform.ViewProjTrafo * cp; wp = cp; c = cc; n = n; ldir = V3d.Zero}
                yield { v.P2 with c = cc; n = n }
                yield { v.P0 with c = cc; n = n }
//                restartStrip()
//                
//                yield { p = uniform.ViewProjTrafo * cp; wp = cp; c = V4d.OIOI; n = n; ldir = V3d.Zero }
//                yield { v.P1 with c = V4d.OIOI; n = n }
//                yield { v.P2 with c = V4d.OIOI; n = n }
//                restartStrip()
//                
//                yield { v.P2 with c = V4d.OOII; n = n }
//                yield { v.P0 with c = V4d.OOII; n = n }
//                yield { p = uniform.ViewProjTrafo * cp; wp = cp; c = V4d.OOII; n = n; ldir = V3d.Zero }

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

        let gs2 (v : Triangle<Vertex>) =
            triangle {
                
                let p01 = (v.P0.wp + v.P1.wp) / 2.0
                let p12 = (v.P1.wp + v.P2.wp) / 2.0
                let p20 = (v.P2.wp + v.P0.wp) / 2.0
                
                yield { v.P0 with p = uniform.ViewProjTrafo * p01; wp = p01 } 
                yield { v.P1 with p = uniform.ViewProjTrafo * p12; wp = p12 } 
                yield { v.P2 with p = uniform.ViewProjTrafo * p20; wp = p20 } 

            }


        [<ReflectedDefinition>]
        let computeNormal (p0 : V4d) (p1 : V4d) (p2 : V4d) =
            Vec.cross (p1.XYZ - p0.XYZ) (p2.XYZ - p0.XYZ) |> Vec.normalize

        let extrude (v : Triangle<Vertex>) =
            triangle {
                let p0 = v.P0.wp.XYZ
                let p1 = v.P1.wp.XYZ
                let p2 = v.P2.wp.XYZ

                let c = (p0 + p1 + p2) / 3.0
                
                let n = Vec.cross (p1 - p0) (p2 - p1)
                let ln = Vec.length n
                let area = 0.5 * ln
                let n = n / ln

                let ph = c + n * area

                let c = v.P0.c

                let w0 = V4d(p0, 1.0)
                let w1 = V4d(p1, 1.0)
                let w2 = V4d(p2, 1.0)
                let wh = V4d(ph, 1.0)



//                yield { wp = w0; p = uniform.ViewProjTrafo * w0; n = n; c = c }
//                yield { wp = w1; p = uniform.ViewProjTrafo * w1; n = n; c = c }
//                yield { wp = w2; p = uniform.ViewProjTrafo * w2; n = n; c = c }
//                restartStrip()

                let n = computeNormal w0 w1 wh
                yield { wp = w0; p = uniform.ViewProjTrafo * w0; n = n; c = c; ldir = V3d.Zero }
                yield { wp = w1; p = uniform.ViewProjTrafo * w1; n = n; c = c; ldir = V3d.Zero }
                yield { wp = wh; p = uniform.ViewProjTrafo * wh; n = n; c = c; ldir = V3d.Zero }
                restartStrip()

                let n = computeNormal w1 w2 wh
                yield { wp = w1; p = uniform.ViewProjTrafo * w1; n = n; c = c; ldir = V3d.Zero }
                yield { wp = w2; p = uniform.ViewProjTrafo * w2; n = n; c = c; ldir = V3d.Zero }
                yield { wp = wh; p = uniform.ViewProjTrafo * wh; n = n; c = c; ldir = V3d.Zero }
                restartStrip()

                let n = computeNormal w2 w0 wh
                yield { wp = w2; p = uniform.ViewProjTrafo * w2; n = n; c = c; ldir = V3d.Zero }
                yield { wp = w0; p = uniform.ViewProjTrafo * w0; n = n; c = c; ldir = V3d.Zero }
                yield { wp = wh; p = uniform.ViewProjTrafo * wh; n = n; c = c; ldir = V3d.Zero }
                restartStrip()


            }

        let withLightDir (v : Vertex) =
            vertex {
                return { v with ldir = V3d.Zero - (uniform.ViewTrafo * v.wp).XYZ |> Vec.normalize; n = (uniform.ViewTrafo * V4d(v.n, 0.0)).XYZ }
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
                "extrude", toEffect Shader.extrude
                "invert", toEffect Shader.gs2
            ]

        let combinations = 
            all available
                |> List.sortBy List.length
                |> List.map (fun l ->
                    let name = 
                        match l |> List.map fst with
                            | [] -> "empty"
                            | list -> list |> String.concat ", " |> sprintf "[ %s ]"
                    let effect = l |> List.map snd |> FShade.Effect.compose
                    name, effect
                )
                |> List.toArray

        let w = ceil (sqrt (float combinations.Length)) |> int
        let h = ceil (float combinations.Length / float w) |> int

        let font = Font("Consolas")

        win.RenderTask <-
            Sg.ofList [
                for i in 0 .. w - 1 do
                    for j in 0 .. h - 1 do
                        let id = i * h + j
                        if id < combinations.Length then
                            let (name, effect) = combinations.[id]
     
                            let label = 
                                Sg.text font C4b.White (Mod.constant name)
                                    |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Scale(0.1))
                                    |> Sg.translate 0.0 0.0 1.5

                            let inner = 
                                Sg.box' C4b.Red Box3d.Unit
                                    |> Sg.shader {
                                        do! DefaultSurfaces.trafo
                                        do! effect
                                        do! Shader.withLightDir
                                        do! DefaultSurfaces.constantColor C4f.Red
                                        do! DefaultSurfaces.stableHeadlight
                                        //do! DefaultSurfaces.simpleLighting
                                    }

                            yield 
                                Sg.ofList [
                                    inner
                                        |> Sg.cullMode (Mod.constant CullMode.Clockwise)

                                    label
                                ]
                                |> Sg.translate (2.0 * float i) 0.0 (-2.0 * float j)

            ]
            //|> Sg.fillMode (Mod.constant FillMode.Line)
            |> Sg.viewTrafo viewTrafo
            |> Sg.projTrafo projTrafo
            |> Sg.compile app.Runtime win.FramebufferSignature

        win.Run()
        win.Dispose()

