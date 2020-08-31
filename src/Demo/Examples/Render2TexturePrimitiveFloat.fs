#nowarn "9"
#nowarn "51"
(*
Render2TexturePrimitive.fsx

This examples demonstrates how to render to textures in an imperative style. No dependencies are tracked here between the
processing steps. In contrast to the next tutorial we use a rather low level API and construct render tasks manually.
After creation, we run the render tasks by executing the side effecting function task.Run(null, fbo)...

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

open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive.Operators 
open Aardvark.Rendering

module Render2TexturePrimitiveFloat = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window
    let runtime = win.Runtime // the runtime instance provides functions for creating resources (lower abstraction than sg)

    let size = V2i(256,256)
    let color = runtime.CreateTexture(size, TextureFormat.Rgba32f, 1, 1)
    let depth = runtime.CreateRenderbuffer(size, RenderbufferFormat.Depth24Stencil8, 1)

    // Signatures are required to compile render tasks. Signatures can be seen as the `type` of a framebuffer
    // It describes the instances which can be used to exectute the render task (in other words
    // the signature describes the formats and of all render targets which are subsequently used for rendering)
    let signature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba32f; samples = 1 }
            DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
        ]

    // Create a framebuffer matching signature and capturing the render to texture targets
    let fbo = 
        runtime.CreateFramebuffer(
            signature, 
            Map.ofList [
                DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                DefaultSemantic.Depth, (depth :> IFramebufferOutput)
            ]
        )
  
    let blendMode =
        { BlendMode.Blend with
            SourceColorFactor       = BlendFactor.One
            DestinationColorFactor  = BlendFactor.One
            SourceAlphaFactor       = BlendFactor.Zero
            DestinationAlphaFactor  = BlendFactor.Zero }

    let cnt = AVal.init 300

    // Default scene graph setup with static camera
    let render2TextureSg =
        aset {
            let! cnt = cnt
            for i in 0 .. cnt - 1 do 
                yield Sg.fullScreenQuad 
                    |> Sg.trafo (AVal.constant Trafo3d.Identity)
        } |> Sg.set
            |> Sg.viewTrafo ~~(CameraView.lookAt (V3d(3,3,3)) V3d.OOO V3d.OOI                   |> CameraView.viewTrafo )
            |> Sg.projTrafo ~~(Frustum.perspective 60.0 0.01 10.0 (float size.X / float size.Y) |> Frustum.projTrafo    )
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]
            |> Sg.blendMode (AVal.constant blendMode)
            |> Sg.depthTest (AVal.constant DepthTest.None)

    // Create render tasks given the signature and concrete buffers        
    let task = runtime.CompileRender(signature, render2TextureSg)
    let clear = runtime.CompileClear(signature, ~~C4f(0.0,0.0,0.0,0.0), ~~0.0)

    // Run the render task imperatively
    clear.Run(null, fbo |> OutputDescription.ofFramebuffer) |> ignore
    task.Run(null, fbo |> OutputDescription.ofFramebuffer) |> ignore

    // this module demonstrates how to read back textures. In order to see the result,
    // a form containing the readback result is shown
    module DemonstrateReadback = 
        open System.Windows.Forms
        open System.IO

     

        let pi = PixImage<float32>(Col.Format.RGBA, size) 
        
        runtime.Download(color, 0, 0, pi)

        pi.Volume.Data |> Array.maxBy float32 |> printfn "max pixel %f"

        //let file = "test16"
        //let tempFileName = Path.ChangeExtension( Path.combine [__SOURCE_DIRECTORY__;  file ], ".tif" )
        //pi.SaveAsImage tempFileName
        //printfn "saved image to: %s" tempFileName
   

    let sg = 
        Sg.fullScreenQuad
            |> Sg.texture DefaultSemantic.DiffuseColorTexture ~~(color :> ITexture)
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
            |> Sg.viewTrafo Interactive.DefaultViewTrafo
            |> Sg.projTrafo Interactive.DefaultProjTrafo

    win.Keyboard.KeyDown(Keys.Add).Values.Subscribe(fun _ ->
        transact (fun _ -> cnt.Value <- cnt.Value + 1)
        printfn "%A" cnt
    ) |> ignore

    win.Keyboard.KeyDown(Keys.OemMinus).Values.Subscribe(fun _ ->
        transact (fun _ -> cnt.Value <- cnt.Value - 1)
        printfn "%A" cnt
    ) |> ignore

    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        Interactive.SceneGraph <- sg
        Interactive.RunMainLoop()

open Render2TexturePrimitiveFloat


#if INTERACTIVE
Interactive.SceneGraph <- sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#endif