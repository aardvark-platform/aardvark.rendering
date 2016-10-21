(*
Render2TexturePrimitiveChangeableSizes.fsx

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
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive

open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators // loads operators such as ~~ and %+ for conveniently creating and modifying mods
open Aardvark.SceneGraph.Semantics

module Render2TexturePrimiviteChangeableSize = 

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])

    let win = Interactive.Window
    let runtime = win.Runtime // the runtime instance provides functions for creating resources (lower abstraction than sg)

    let size = V2i(1024,768)
    let sizeM = Mod.init size
    let color = 
        ChangeableResources.createTexture runtime ~~1 sizeM ~~TextureFormat.Rgba8
    let depth = 
        ChangeableResources.createRenderbuffer runtime ~~1 sizeM ~~RenderbufferFormat.Depth24Stencil8

    // Signatures are required to compile render tasks. Signatures can be seen as the `type` of a framebuffer
    // It describes the instances which can be used to exectute the render task (in other words
    // the signature describes the formats and of all render targets which are subsequently used for rendering)
    let signature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
            DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
        ]

    // Create a framebuffer matching signature and capturing the render to texture targets
    let fbo = 
        ChangeableResources.createFramebufferFromTexture runtime signature color depth None

  
    // Default scene graph setup with static camera
    let render2TextureSg =
        Sg.fullScreenQuad
            |> Sg.viewTrafo ~~(CameraView.lookAt (V3d(3,3,3)) V3d.OOO V3d.OOI                   |> CameraView.viewTrafo )
            |> Sg.projTrafo ~~(Frustum.perspective 60.0 0.01 10.0 (float size.X / float size.Y) |> Frustum.projTrafo    )
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

    // Create render tasks given the signature and concrete buffers        
    let task = runtime.CompileRender(signature, render2TextureSg.RenderObjects())
    let clear = runtime.CompileClear(signature, ~~C4f.Red, ~~1.0)

    // Run the render task imperatively
    clear.Run(null, Mod.force fbo |> OutputDescription.ofFramebuffer) |> ignore
    task.Run(null,  Mod.force fbo |> OutputDescription.ofFramebuffer) |> ignore

    // this module demonstrates how to read back textures. In order to see the result,
    // a form containing the readback result is shown
    module DemonstrateReadback = 
        open System.Windows.Forms
        open System.IO

        let pi = PixImage<byte>(Col.Format.BGRA, size) 
        runtime.Download(color |> Mod.force, 0, 0, pi)
        let tempFileName = Path.ChangeExtension( Path.combine [__SOURCE_DIRECTORY__;  Path.GetTempFileName() ], ".bmp" )
        pi.SaveAsImage tempFileName
    
        // download is done, do winforms magic in order to present the result
        let f = new Form()
        f.Size <- Drawing.Size(1024,768)
        let pb = new PictureBox()
        f.Text <- sprintf "Readback: %s" tempFileName
        pb.Image <- System.Drawing.Bitmap.FromFile(tempFileName)
        pb.Dock <- DockStyle.Fill
        f.Controls.Add(pb)
        f.Show()

    // The render to texture texture can also be used in another render pass (here we again render to our main window)
    let sg = 
        Sg.fullScreenQuad 
            |> Sg.texture DefaultSemantic.DiffuseColorTexture (color |> Mod.map (fun s -> s :> ITexture))
            |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
            |> Sg.viewTrafo Interactive.DefaultViewTrafo
            |> Sg.projTrafo Interactive.DefaultProjTrafo


    let run () =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        Interactive.SceneGraph <- sg
        win.Run()

    
open Render2TexturePrimiviteChangeableSize

#if INTERACTIVE
Interactive.SceneGraph <- sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#endif

[<AutoOpen>]
module Test = 
    // check out how to change render targets imperatively
    let mkSmall () = transact (fun () -> Mod.change sizeM (V2i(16,16)))
    let renderAgain () = 
        clear.Run(Mod.force fbo) |> ignore
        task.Run(Mod.force fbo) |> ignore
        printfn "move camera in order to see new result"

