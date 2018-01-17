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

        let divide (v : Triangle<Vertex>) =
            triangle {
                
                let ccDiv = v.P0.c
                let nDiv = v.P0.n
                let cpDiv = (v.P0.wp + v.P1.wp + v.P2.wp) / 3.0
                //let cc = (v.P0.c + v.P1.c + v.P2.c) / 3.0


                // 0 1 c   c 1 2   c 2 0
                yield { v.P0 with n = nDiv }
                yield { v.P1 with n = nDiv }
                yield { p = uniform.ViewProjTrafo * cpDiv; wp = cpDiv; c = V4d.IIII; n = nDiv; ldir = V3d.Zero}
                yield { v.P2 with n = nDiv }
                yield { v.P0 with n = nDiv }
            }

        let shrink (v : Triangle<Vertex>) =
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

        let nop (v : Triangle<Vertex>) =
            triangle {
                yield v.P0
                yield v.P1
                yield v.P2
            }

        let invert (v : Triangle<Vertex>) =
            triangle {
                
                let p01 = (v.P0.wp + v.P1.wp) / 2.0
                let p12 = (v.P1.wp + v.P2.wp) / 2.0
                let p20 = (v.P2.wp + v.P0.wp) / 2.0
                
                yield { v.P0 with p = uniform.ViewProjTrafo * p01; wp = p01 } 
                yield { v.P1 with p = uniform.ViewProjTrafo * p12; wp = p12 } 
                yield { v.P2 with p = uniform.ViewProjTrafo * p20; wp = p20 } 

            }
            
        let tricolor (v : Triangle<Vertex>) =
            triangle {
                yield { v.P0 with c = V4d.OIII }
                yield { v.P1 with c = V4d.IOII }
                yield { v.P2 with c = V4d.IIOI }
            }

        let tritri (v : Triangle<Vertex>) =
            triangle {
                
                let p01 = (v.P0.wp + v.P1.wp) / 2.0
                let p12 = (v.P1.wp + v.P2.wp) / 2.0
                let p20 = (v.P2.wp + v.P0.wp) / 2.0

                yield v.P0
                yield { v.P0 with p = uniform.ViewProjTrafo * p01; wp = p01 } 
                yield { v.P2 with p = uniform.ViewProjTrafo * p20; wp = p20 } 
                restartStrip()

                yield { v.P0 with p = uniform.ViewProjTrafo * p01; wp = p01 } 
                yield { v.P1 with p = uniform.ViewProjTrafo * p12; wp = p12 } 
                yield { v.P2 with p = uniform.ViewProjTrafo * p20; wp = p20 } 
                restartStrip()

                yield { v.P2 with p = uniform.ViewProjTrafo * p20; wp = p20 } 
                yield { v.P1 with p = uniform.ViewProjTrafo * p12; wp = p12 }
                yield v.P2
                restartStrip()

                yield { v.P0 with p = uniform.ViewProjTrafo * p01; wp = p01 } 
                yield v.P1
                yield { v.P1 with p = uniform.ViewProjTrafo * p12; wp = p12 } 
                restartStrip()

                let p01 = p01 + V4d.OOIO
                let p12 = p12 + V4d.OOIO
                let p20 = p20 + V4d.OOIO
                yield { v.P0 with p = uniform.ViewProjTrafo * p01; wp = p01 } 
                yield { v.P1 with p = uniform.ViewProjTrafo * p12; wp = p12 } 
                yield { v.P2 with p = uniform.ViewProjTrafo * p20; wp = p20 } 
                restartStrip()
            }


        [<ReflectedDefinition>]
        let computeNormal (p0 : V4d) (p1 : V4d) (p2 : V4d) =
            Vec.cross (p1.XYZ - p0.XYZ) (p2.XYZ - p0.XYZ) |> Vec.normalize

        let extrude (v : Triangle<Vertex>) =
            triangle {
                let p0Ext = v.P0.wp.XYZ
                let p1Ext = v.P1.wp.XYZ
                let p2Ext = v.P2.wp.XYZ

                let cExt = (p0Ext + p1Ext + p2Ext) / 3.0
                
                let nExt = Vec.cross (p1Ext - p0Ext) (p2Ext - p1Ext)
                let lnExt = Vec.length nExt
                let areaExt = 0.5 * lnExt
                let nExt = nExt / lnExt

                let phExt = cExt + nExt * areaExt


                let w0Ext = V4d(p0Ext, 1.0)
                let w1Ext = V4d(p1Ext, 1.0)
                let w2Ext = V4d(p2Ext, 1.0)
                let whExt = V4d(phExt, 1.0)


                yield { wp = w0Ext; p = uniform.ViewProjTrafo * w0Ext; n = nExt; c = v.P0.c; ldir = V3d.Zero }
                yield { wp = w1Ext; p = uniform.ViewProjTrafo * w1Ext; n = nExt; c = v.P1.c; ldir = V3d.Zero }
                yield { wp = w2Ext; p = uniform.ViewProjTrafo * w2Ext; n = nExt; c = v.P2.c; ldir = V3d.Zero }
                restartStrip()

                let nExt = computeNormal w0Ext w1Ext whExt
                yield { wp = w0Ext; p = uniform.ViewProjTrafo * w0Ext; n = nExt; c = v.P0.c; ldir = V3d.Zero }
                yield { wp = w1Ext; p = uniform.ViewProjTrafo * w1Ext; n = nExt; c = v.P1.c; ldir = V3d.Zero }
                yield { wp = whExt; p = uniform.ViewProjTrafo * whExt; n = nExt; c = V4d.IOII; ldir = V3d.Zero }
                restartStrip()

                let nExt = computeNormal w1Ext w2Ext whExt
                yield { wp = w1Ext; p = uniform.ViewProjTrafo * w1Ext; n = nExt; c = v.P1.c; ldir = V3d.Zero }
                yield { wp = w2Ext; p = uniform.ViewProjTrafo * w2Ext; n = nExt; c = v.P2.c; ldir = V3d.Zero }
                yield { wp = whExt; p = uniform.ViewProjTrafo * whExt; n = nExt; c = V4d.IOII; ldir = V3d.Zero }
                restartStrip()

                let nExt = computeNormal w2Ext w0Ext whExt
                yield { wp = w2Ext; p = uniform.ViewProjTrafo * w2Ext; n = nExt; c = v.P2.c; ldir = V3d.Zero }
                yield { wp = w0Ext; p = uniform.ViewProjTrafo * w0Ext; n = nExt; c = v.P0.c; ldir = V3d.Zero }
                yield { wp = whExt; p = uniform.ViewProjTrafo * whExt; n = nExt; c = V4d.IOII; ldir = V3d.Zero }
                restartStrip()


            }

        let withLightDir (v : Vertex) =
            vertex {
                return { v with ldir = (uniform.ViewTrafo * (V4d(50.0, 60.0, 70.0, 1.0) - v.wp)).XYZ |> Vec.normalize; n = (uniform.ViewTrafo * V4d(v.n, 0.0)).XYZ |> Vec.normalize }
            }

    open FShade
    open Aardvark.Application.OpenVR

    let run() =
        //FShade.EffectDebugger.attach()
//        use app = new VulkanApplication(false)
//        let win = app.CreateSimpleRenderWindow(8) 
//        let run() = win.Run()
//        let cameraView  = DefaultCameraController.control win.Mouse win.Keyboard win.Time (CameraView.LookAt(V3d(2.5, -3.0, 1.5), V3d(2.5, 0.0, 0.0), V3d.OOI))    
//        let frustum     = win.Sizes     |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))       
//        let viewTrafo   = cameraView    |> Mod.map CameraView.viewTrafo :> IMod
//        let projTrafo   = frustum       |> Mod.map Frustum.projTrafo :> IMod
//        let win = win:> IRenderTarget


//        let app = new VulkanVRApplicationLayered(false)
//        let win = app :> IRenderTarget
//        let viewTrafo = app.Info.viewTrafos :> IMod
//        let projTrafo = app.Info.projTrafos :> IMod
//        let run () = app.Run()
        //app.ShowWindow()


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
                "divide", toEffect Shader.divide
                "shrink", toEffect Shader.shrink 
                "extrude", toEffect Shader.extrude
//                "invert", toEffect Shader.invert
//                "tricolor", toEffect Shader.tricolor
            ]

        let combinations = 
            [available]
            //all available
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

        let print (name : string) (l : list<FShade.Effect>) =
            let badForComposition =
                FShade.Effect.compose l

            printfn "%s:" name
            printfn "  vertices:   %A" badForComposition.GeometryShader.Value.shaderOutputVertices
            printfn "  primitives: %A" badForComposition.GeometryShader.Value.shaderOutputPrimitives.Value
  
        let w = 1 + (ceil (sqrt (float combinations.Length)) |> int)
        let h = ceil (float combinations.Length / float w) |> int

        let font = Font("Consolas")

        let u (n : String) (m : IMod) (s : ISg) =
            Sg.UniformApplicator(n, m, s) :> ISg

        let sg =
            Sg.ofList [
                for j in 0 .. h - 1 do
                    for i in 0 .. w - 1 do
                        let id = i + w * j
                        if id < combinations.Length then
                            let (name, effect) = combinations.[id]
     
                            let label = 
                                Sg.text font C4b.White (Mod.constant name)
                                    |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Scale(0.1))
                                    |> Sg.translate 0.0 0.0 1.0

                            let mutable blend = BlendMode(true)
                            blend.SourceFactor <- BlendFactor.One
                            blend.DestinationFactor <- BlendFactor.One
                            blend.Operation <- BlendOperation.Add
                            blend.SourceAlphaFactor <- BlendFactor.One
                            blend.DestinationAlphaFactor <- BlendFactor.One
                            blend.AlphaOperation <- BlendOperation.Add

                            let inner = 
                                Sg.ofList [
                                    Sg.sphere' 3 C4b.Red 0.5
                                        |> Sg.translate 0.5 0.5 0.5
                                        |> Sg.uniform "Color" (Mod.constant V4d.IOOI)

                                    Sg.box' C4b.Blue Box3d.Unit
                                        |> Sg.uniform "Color" (Mod.constant V4d.IIOI)
                                        |> Sg.translate 1.6 0.0 0.0
                                ]   
                                    |> Sg.scale 0.5
                                    |> Sg.shader {
                                        do! DefaultSurfaces.trafo
//                                        do! fun (v : Effects.Vertex) ->
//                                            vertex {
//                                                return (uniform?Color : V4d)
//                                            }
                                        do! effect
                                        do! Shader.withLightDir
                                        //do! DefaultSurfaces.constantColor C4f.Red


                                        do! DefaultSurfaces.stableHeadlight
                                    }
                                    //|> Sg.fillMode (Mod.constant FillMode.Line)
//                                    |> Sg.cullMode (Mod.constant CullMode.Clockwise)
                            yield 
                                Sg.ofList [
                                    inner
                                    label
                                ]
                                //|> Sg.depthTest (Mod.constant DepthTestMode.None)
                                |> Sg.translate (2.0 * float i) 0.0 (1.5 * float j)

            ]
            |> Sg.scale 0.5
            //|> Sg.fillMode (Mod.constant FillMode.Line)
//            |> u "ViewTrafo" viewTrafo
//            |> u "ProjTrafo" projTrafo
//            |> Sg.compile app.Runtime win.FramebufferSignature

        show {
            display Display.Mono
            samples 8
            backends [Backend.GL; Backend.Vulkan]
            debug false

            scene sg
        }
        System.Environment.Exit 0
        //win.Dispose()

