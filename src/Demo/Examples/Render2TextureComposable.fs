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
open Aardvark.Rendering.Interactive

open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators // loads operators such as ~~ and %+ for conveniently creating and modifying mods

open Default // makes viewTrafo and other tutorial specicific default creators visible

module Render2TextureComposable = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])


    let runtime = win.Runtime // the runtime instance provides functions for creating resources (lower abstraction than sg)

    let animatedViewTrafo = 
        // note that this not aardvark idiomatic, but is sufficient for the purpose of this tutorial.
        // A tutorial on awesome functional reactivity is out of the scope of this example
        let initial = V3d(3,3,3)
        let startTime = ref DateTime.Now
        win.Time |> Mod.map (fun t -> 
            let dt = t - !startTime
            let p = Trafo3d.RotationZ(dt.TotalSeconds * 0.8).Forward.TransformPos(initial)
            CameraView.lookAt p V3d.OOO V3d.OOI |> CameraView.viewTrafo
        )

    let size = V2i(1024,768)
    let render2TextureSg =
        quadSg
            |> Sg.viewTrafo animatedViewTrafo
            |> Sg.projTrafo ~~(Frustum.perspective 60.0 0.01 10.0 (float size.X / float size.Y) |> Frustum.projTrafo    )
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

    let signature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
            DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
        ]
        
    let task = runtime.CompileRender(signature, render2TextureSg)
    let clear = runtime.CompileClear(signature, ~~C4f.Red, ~~1.0)

    // render2color does all the magick!
    let preprocessingResult = RenderTask.renderToColor ~~size (RenderTask.ofList [clear; task])

    let sg = 
        quadSg 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture preprocessingResult
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
            |> Sg.viewTrafo (viewTrafo   () |> Mod.map CameraView.viewTrafo )
            |> Sg.projTrafo (perspective () |> Mod.map Frustum.projTrafo    )

    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        setSg sg
        win.Run()

open Render2TextureComposable

#if INTERACTIVE
setSg sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#endif
