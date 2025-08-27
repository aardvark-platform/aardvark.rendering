open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

// This example illustrates how to render to texture in a dependency aware manner :)

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    
    Aardvark.Init()

    use win = 
        window {
            display Display.Mono
            samples 8
            backend Backend.Vulkan
            debug true
        }
    let runtime = win.Runtime

    // same as in 03-Animation
    let dynamicTrafo =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        win.Time |> AVal.map (fun _ ->
            let t = sw.Elapsed.TotalSeconds
            Trafo3d.RotationZ (0.5 * t)
        )

    // in order to render something to texture, we need to specify how the framebuffer should look like
    use signature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, TextureFormat.Rgba8
            DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
        ]

    // our render target needs a size. Since aardvark is cool this size can be dynamic of course
    let size = V2i(1024,1024) |> AVal.init 

    // create a scenegraph for the offscreen render passt
    use offscreenTask = 
        Sg.box' C4b.Red Box3d.Unit
            |> Sg.translate -0.5 -0.5 0.0
            |> Sg.trafo dynamicTrafo
            |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.simpleLighting
                }
            // attach a constant view trafo (which makes our box visible)
            |> Sg.viewTrafo (
                    CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI 
                     |> CameraView.viewTrafo 
                     |> AVal.constant
               )
            // since our render target size is dynamic, we compute a proj trafo using standard techniques
            |> Sg.projTrafo (size |> AVal.map (fun actualSize -> 
                    Frustum.perspective 60.0 0.01 10.0 (float actualSize.X / float actualSize.Y) |> Frustum.projTrafo
                  )
               )
            // next, we use Sg.compile in order to turn a scene graph into a render task (a nice composable alias for runtime.CompileRender)
            |> Sg.compile runtime signature 
    
    // next we use the renderTask API in order render to a (implicitly generated) framebuffer.
    // the function returns an aval<ITexture>, which can later be used.
    // Note that this whole process is depenency aware, i.e.
    //   - if the render to texture task is out of date, it gets automatically reexecuted
    //   - but only if the final render task is dirty as well (e.g. the main camera has moved)
    let offscreenTexture =
        RenderTask.renderToColor size offscreenTask

    win.Scene <- 
        // create a textured box
        Sg.box' C4b.White Box3d.Unit 
        // we use the diffuseTexture binding here
        |> Sg.diffuseTexture offscreenTexture
        // create a shader which uses the texture
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.diffuseTexture
            do! DefaultSurfaces.simpleLighting
        }
        // for demonstration purposes, we also add a wirebox - which does not use any texturing....
        |> Sg.andAlso (
            Sg.wireBox' C4b.White Box3d.Unit 
                |> Sg.shader { 
                    do! DefaultSurfaces.trafo; 
                    do! DefaultSurfaces.vertexColor 
                } 
            )
    
    win.Run()

    0
