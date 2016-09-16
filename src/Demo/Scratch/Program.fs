// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open Aardvark.Application
open Aardvark.Application.WinForms
open FShade


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

[<EntryPoint>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()

    App.run()

    0 // return an integer exit code
