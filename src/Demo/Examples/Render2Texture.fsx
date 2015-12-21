#load "RenderingSetup.fsx"
open RenderingSetup

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators // loads operators such as ~~ and %+ for conveniently creating and modifying mods

open Default // makes viewTrafo and other tutorial specicific default creators visible

let runtime = win.Runtime // the runtime instance provides functions for creating resources (lower abstraction than sg)

let size = V2i(1024,768)
let color = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)
let depth = runtime.CreateRenderbuffer(size, RenderbufferFormat.Depth24Stencil8, 1)


let signature =
    runtime.CreateFramebufferSignature [
        DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
        DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
    ]

let fbo = 
    runtime.CreateFramebuffer(
        signature, 
        Map.ofList [
            DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
            DefaultSemantic.Depth, (depth :> IFramebufferOutput)
        ]
    )
  
let render2TextureSg =
    quadSg
        |> Sg.viewTrafo ~~(CameraView.lookAt (V3d(3,3,3)) V3d.OOO V3d.OOI                   |> CameraView.viewTrafo )
        |> Sg.projTrafo ~~(Frustum.perspective 60.0 0.01 10.0 (float size.X / float size.Y) |> Frustum.projTrafo    )
        |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

        
let task = runtime.CompileRender(signature, render2TextureSg)
let clear = runtime.CompileClear(signature, ~~C4f.Red, ~~1.0)

clear.Run(null, fbo) |> ignore
task.Run(null, fbo) |> ignore


module DemonstrateReadback = 
    open System.Windows.Forms
    open System.IO

    let pi = PixImage<byte>(Col.Format.BGRA, size) //color.Download(0).[0].ToPixImage<byte>(Col.Format.BGRA)
    runtime.Download(color, 0, 0, pi)
    let tempFileName = Path.ChangeExtension( Path.combine [__SOURCE_DIRECTORY__;  Path.GetTempFileName() ], ".bmp" )
    pi.SaveAsImage tempFileName
    let f = new Form()
    f.Size <- Drawing.Size(1024,768)
    let pb = new PictureBox()
    f.Text <- sprintf "Readback: %s" tempFileName
    pb.Image <- System.Drawing.Bitmap.FromFile(tempFileName)
    pb.Dock <- DockStyle.Fill
    f.Controls.Add(pb)
    f.Show()

let sg = 
    quadSg 
        |> Sg.texture DefaultSemantic.DiffuseColorTexture ~~(color :> ITexture)
        |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.diffuseTexture |> toEffect]
        |> Sg.viewTrafo (viewTrafo   () |> Mod.map CameraView.viewTrafo )
        |> Sg.projTrafo (perspective () |> Mod.map Frustum.projTrafo    )


setSg sg