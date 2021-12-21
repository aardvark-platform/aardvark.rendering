(*
Render2TextureComposable.fsx

This examples demonstrates how to render a scene graph to texture and map the resulting texture to another scene graph.

*)

#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Interactive

open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive.Operators // loads operators such as ~~ and %+ for conveniently creating and modifying mods


module Render2TextureComposable = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window
    let runtime = win.Runtime // the runtime instance provides functions for creating resources (lower abstraction than sg)

    let animatedViewTrafo = 
        // note that this not aardvark idiomatic, but is sufficient for the purpose of this tutorial.
        // A tutorial on awesome functional reactivity is out of the scope of this example
        let initial = V3d(3,3,3)
        let startTime = ref DateTime.Now
        win.Time |> AVal.map (fun t -> 
            let dt = t - !startTime
            let p = Trafo3d.RotationZ(dt.TotalSeconds * 0.8).Forward.TransformPos(initial)
            CameraView.lookAt p V3d.OOO V3d.OOI |> CameraView.viewTrafo
        )

    let size = V2i(1024,768)
    let render2TextureSg =
        Sg.fullScreenQuad
            |> Sg.viewTrafo animatedViewTrafo
            |> Sg.projTrafo ~~(Frustum.perspective 60.0 0.01 10.0 (float size.X / float size.Y) |> Frustum.projTrafo    )
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

    let signature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, TextureFormat.Rgba8
            DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
        ]
        
    let task = runtime.CompileRender(signature, render2TextureSg)
    let clear = runtime.CompileClear(signature, ~~C4f.Red, ~~1.0)

    // render2color does all the magick!
    let preprocessingResult = RenderTask.renderToColor ~~size (RenderTask.ofList [clear; task])

    let sg = 
        Sg.fullScreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture preprocessingResult
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
            |> Sg.viewTrafo Interactive.DefaultViewTrafo
            |> Sg.projTrafo Interactive.DefaultProjTrafo

    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        Interactive.SceneGraph <- sg
        Interactive.RunMainLoop()

open Render2TextureComposable

#if INTERACTIVE
Interactive.SceneGraph <- sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#endif
