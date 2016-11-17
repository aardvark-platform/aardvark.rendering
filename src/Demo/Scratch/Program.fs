// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open FShade

module Shader =
    open FShade

    type Vertex = {
        [<Position>]        pos     : V4d
        [<Semantic("Urdar")>] m : M44d
        [<WorldPosition>]   wp      : V4d
        [<Normal>]          n       : V3d
        [<BiNormal>]        b       : V3d
        [<Tangent>]         t       : V3d
        [<Color>]           c       : V4d
        [<TexCoord>]        tc      : V2d
    }

    let trafo (v : Vertex) =
        vertex {
            let wp = v.m * v.pos
            return { v with
                        pos = uniform.ViewProjTrafo * wp
                        wp = wp 
                   }
        }
    let tcColor (v : Vertex) =
        fragment {
            return V4d(v.tc.X, v.tc.Y, 1.0, 1.0)
        }

[<Demo("Simple Sphere Demo")>]
[<Description("simply renders a red sphere with very simple lighting")>]
let bla() =
    Sg.sphere' 5 C4b.Red 1.0
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            DefaultSurfaces.simpleLighting |> toEffect
        ]


[<Demo("Simple Cube Demo")>]
let blubber() =
    Sg.box' C4b.Red Box3d.Unit
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            DefaultSurfaces.simpleLighting |> toEffect
        ]

[<Demo("Quad Demo")>]
let quad() =
    Sg.fullScreenQuad
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
        ]

[<Demo("Textured Quad Demo")>]
let quadTexture() =
    Sg.fullScreenQuad
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.constantColor C4f.Red |> toEffect
            DefaultSurfaces.diffuseTexture |> toEffect
           ]
        |> Sg.diffuseFileTexture' @"E:\Development\WorkDirectory\DataSVN\pattern.jpg" true



[<Demo("Super naive LoD")>]
let naiveLoD() =


    let highest = Sg.sphere' 5 C4b.Red 1.0      
    let middle  = Sg.sphere' 3 C4b.Blue 1.0     
    let low     = Sg.box' C4b.Green Box3d.Unit  

    let dist threshhold (s : NaiveLod.LodScope)= 
        (s.cameraPosition - s.trafo.Forward.C3.XYZ).Length < threshhold

    let scene = 
        NaiveLod.Sg.loD 
            low 
            (NaiveLod.Sg.loD middle highest (dist 5.0)) 
            (dist 8.0)

    let many =
        [
            for x in -10.0 .. 2.0 .. 10.0 do 
                for y in -10.0 .. 2.0 .. 10.0 do
                    for z in -10.0 .. 2.0 .. 10.0 do 
                        yield scene |> Sg.translate x y z 
                        //yield scene |> Sg.uniform "Urdar" (Mod.constant (M44d.Translation(x,y,z)))
        ] |> Sg.ofSeq

    let sg = 
        many
            |> Sg.effect [
               // Shader.trafo |> toEffect
                DefaultSurfaces.trafo  |> toEffect
                DefaultSurfaces.vertexColor    |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect
               ]
            |> App.WithCam

    let objs = 
        sg 
        |> Aardvark.SceneGraph.Semantics.RenderObjectSemantics.Semantic.renderObjects 
        |> Aardvark.Rendering.Optimizer.optimize App.Runtime App.FramebufferSignature
       

    App.Runtime.CompileRender(App.FramebufferSignature, objs) |> DefaultOverlays.withStatistics




[<EntryPoint>]
let main argv = 

    Ag.initialize()
    Aardvark.Init()

    App.Config <- { BackendConfiguration.Default with useDebugOutput = true }
    App.run()

    0 // return an integer exit code
