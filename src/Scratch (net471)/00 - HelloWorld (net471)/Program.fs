open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Application.WinForms.Vulkan

// This example illustrates how to create a simple render window. 
// In contrast to the rest of the examples (beginning from 01-Triangle), we use
// the low level application API for creating a window and attaching 
// mouse and keyboard inputs.
// In your application you most likely want to use this API instead of the more abstract
// show/window computation expression builders (which reduces code duplication
// in this case) to setup applications.

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    // create an OpenGL/Vulkan application. Use the use keyword (using in C#) in order to
    // properly dipose resources on shutdown...
    use app = new VulkanApplication()
    // SimpleRenderWindow is a System.Windows.Forms.Form which contains a render control
    // of course you can a custum form and add a control to it.
    // Note that there is also a WPF binding for OpenGL. For more complex GUIs however,
    // we recommend using aardvark-media anyways..
    let win = app.CreateSimpleRenderWindow(samples = 8)
    //win.Title <- "Hello Aardvark"

    // Given eye, target and sky vector we compute our initial camera pose
    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    // the class Frustum describes camera frusta, which can be used to compute a projection matrix.
    let frustum = 
        // the frustum needs to depend on the window size (in oder to get proper aspect ratio)
        win.Sizes 
            // construct a standard perspective frustum (60 degrees horizontal field of view,
            // near plane 0.1, far plane 50.0 and aspect ratio x/y.
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    // create a controlled camera using the window mouse and keyboard input devices
    // the window also provides a so called time mod, which serves as tick signal to create
    // animations - seealso: https://github.com/aardvark-platform/aardvark.docs/wiki/animation
    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    // create a quad using low level primitives (IndexedGeometry is our base type for specifying
    // geometries using vertices etc)
    let quadSg =
        let quad =
            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,
                IndexArray = ([|0;1;2; 0;2;3|] :> System.Array),
                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions,                  [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                        DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                        DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                    ]
            )
                
        // create a scenegraph, given a IndexedGeometry instance...
        quad |> Sg.ofIndexedGeometry



    let lines = 
        Mod.init [|Line3d(V3d.OOO,V3d.III)|]

    let d = 
        Mod.init (Some lines)
    win.Keyboard.KeyDown(Keys.G).Values.Add(fun _ -> 
        transact (fun _ -> 
            d.Value <- 
                match d.Value with
                    | None -> Some lines
                    | Some v -> None
        )
    )

    win.Keyboard.KeyDown(Keys.E).Values.Add(fun _ -> 
        transact (fun _ -> 
            if lines.Value.Length = 0 then  
                lines.Value <- [|Line3d()|]
            else lines.Value <- [||]
        )
    )

    let sg = 
        d |> Mod.bind (fun e -> 
            match e with
                | None -> Mod.constant [|Line3d()|] :> IMod<_>
                | Some e -> e :> IMod<_>
        )

    let lines = 
        Sg.lines (Mod.constant C4b.White) sg
        |> Sg.uniform "LineWidth" (Mod.constant 5) 
        |> Sg.effect [
            toEffect DefaultSurfaces.trafo
            toEffect DefaultSurfaces.vertexColor
            toEffect DefaultSurfaces.thickLine
            ]

    let sg =
        lines
            // here we use fshade to construct a shader: https://github.com/aardvark-platform/aardvark.docs/wiki/FShadeOverview
            |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                ]
            // extract our viewTrafo from the dynamic cameraView and attach it to the scene graphs viewTrafo 
            |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
            // compute a projection trafo, given the frustum contained in frustum
            |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo    )


    let renderTask = 
        // compile the scene graph into a render task
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    // assign the render task to our window...
    win.RenderTask <- renderTask
    win.Run()
    0
