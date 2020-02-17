open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.SceneGraph.IO

open System.IO

[<ReflectedDefinition>]
module Shader =
    open FShade

    let volumeTexture =
        sampler3d {
            texture uniform?VolumeTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
            addressW WrapMode.Clamp
        }

    let pickRay (p : V2d) =
        let pn = uniform.ViewProjTrafoInv * V4d(p.X, p.Y, 0.0, 1.0)
        let nearPlanePoint = pn.XYZ / pn.W
        Vec.normalize nearPlanePoint

    type Vertex =
        {
            [<Position>]
            pos : V4d

            [<Semantic("RayDirection")>]
            dir : V3d

            [<Semantic("CubeCoord")>]
            cubeCoord : V3d

        }

    let vertex (v : Vertex) =
        vertex {
            let cameraPosition = uniform.ViewTrafoInv.C3.XYZ
            let cameraInModel = uniform.ModelTrafoInv.TransformPos cameraPosition
            let wp = uniform.ModelTrafo * v.pos
            return {
                pos = uniform.ViewProjTrafo * wp
                dir = v.pos.XYZ - cameraInModel
                cubeCoord = v.pos.XYZ
            }
        }

    let fragment (v : Vertex) =
        fragment {
            let size = volumeTexture.Size
            let mutable color = V3d.Zero
                
            let mutable sampleLocation = v.cubeCoord

            let steps = 100

            let dir = -Vec.normalize v.dir / float steps

            do
                while sampleLocation.X >= 0.0 && sampleLocation.X <= 1.0 && sampleLocation.Y >= 0.0 && sampleLocation.Y <= 1.0 && sampleLocation.Z >= 0.0 && sampleLocation.Z <= 1.0 do
                    let rho = volumeTexture.SampleLevel(sampleLocation, 0.0).X
                    if rho > 0.4 then color <- V3d.III * Vec.dot sampleLocation V3d.III
                    color <- color + V3d.III * volumeTexture.SampleLevel(sampleLocation, 0.0).X
                    sampleLocation <- sampleLocation + dir

            return V4d(2.0 * color / float steps, 1.0)
        }


[<EntryPoint>]
let main argv = 

    Super.run ()
    System.Environment.Exit 0
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()


    // create an OpenGL/Vulkan application. Use the use keyword (using in C#) in order to
    // properly dipose resources on shutdown...
    use app = new OpenGlApplication()
    // SimpleRenderWindow is a System.Windows.Forms.Form which contains a render control
    // of course you can a custum form and add a control to it.
    // Note that there is also a WPF binding for OpenGL. For more complex GUIs however,
    // we recommend using aardvark-media anyways..

    let win = new Aardvark.Application.Slim.GameWindow(app.Runtime, false, 4, true)
    

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

    let eyeSeparation = V3d(0.04, 0.0, 0.0)

    let stereoViews =
        let half = eyeSeparation * 0.5
        cameraView  |> Mod.map (fun v -> 
            let t = CameraView.viewTrafo v
            [|
                t * Trafo3d.Translation(-half)
                t * Trafo3d.Translation(half)
            |]
        )

    let stereoProjs =
        win.Sizes 
        // construct a standard perspective frustum (60 degrees horizontal field of view,
        // near plane 0.1, far plane 50.0 and aspect ratio x/y.
        |> Mod.map (fun s -> 
            let ac = 30.0
            let ao = 30.0
            let near = 0.01
            let far = 10.0
            let aspect = float s.X / float s.Y
            let sc = tan (Conversion.RadiansFromDegrees ac) * near
            let so = tan (Conversion.RadiansFromDegrees ao) * near
            let sv = tan (0.5 * Conversion.RadiansFromDegrees (ac + ao)) * near

            let leftEye = { left = -sc; right = +so; bottom = -sv / aspect; top = +sv / aspect; near = near; far = far; isOrtho = false }
            let rightEye = { left = -so; right = +sc; bottom = -sv / aspect; top = +sv / aspect; near = near; far = far; isOrtho = false }
            [|
                Frustum.perspective 90.0 0.01 100.0 aspect   |> Frustum.projTrafo
                Frustum.perspective 90.0 0.01 100.0 aspect   |> Frustum.projTrafo
                //Frustum.projTrafo leftEye
                //Frustum.projTrafo rightEye
            |]
        )


    let sg =
        Sg.box' C4b.White Box3d.Unit 
            // here we use fshade to construct a shader: https://github.com/aardvark-platform/aardvark.docs/wiki/FShadeOverview
            |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                ]

            |> Sg.uniform "ViewTrafo" stereoViews
            |> Sg.uniform "ProjTrafo" stereoProjs

            //// extract our viewTrafo from the dynamic cameraView and attach it to the scene graphs viewTrafo 
            |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
            //// compute a projection trafo, given the frustum contained in frustum
            |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo    )

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let folder = @"C:\Users\steinlechner\Desktop\hechtkopfsalamander male - Copy"
    let files = Directory.GetFiles folder

    let images = files |> Array.map (fun p -> PixImage.Create(p).ToPixImage<byte>(Col.Format.Gray))

    let s2d = images.[0].Size
    let volume = PixVolume<byte>(s2d.X, s2d.Y, files.Length, 1)
    for layer in 0 .. images.Length - 1 do
        volume.Tensor4.SubImageAtZ(int64 layer).Set(images.[layer].Volume) |> ignore


    let texture = PixTexture3d(volume, false) :> ITexture
    let texture = app.Runtime.PrepareTexture(texture) :> ITexture |> Mod.constant


    let fvc = int64 volume.Size.X * int64 volume.Size.Y * int64 volume.Size.Z
    let factor = fvc / (256L * 256L * 256L)

    let drawCall = DrawCallInfo(FaceVertexCount = (fvc / factor |> int), InstanceCount = 1)

    let blendMode = 
        BlendMode(
            Operation = BlendOperation.Add,
            AlphaOperation = BlendOperation.Add,
            SourceFactor = BlendFactor.One,
            DestinationFactor = BlendFactor.One,
            SourceAlphaFactor =BlendFactor.One,
            DestinationAlphaFactor = BlendFactor.One,
            Enabled = true
        )

    let signature =
        app.Runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, RenderbufferFormat.R32f
        ]




    let size = V3d volume.Size / float volume.Size.NormMax

    let loc = cameraView |> Mod.map CameraView.location

    let sg = 
        Sg.box' C4b.Red (Box3d(-size, size))
        |> Sg.uniform "VolumeTexture" texture
        |> Sg.shader {
            do! Shader.vertex
            do! Shader.fragment
            }
        |> Sg.uniform "ViewTrafo" stereoViews
        |> Sg.uniform "ProjTrafo" stereoProjs
        //|> Sg.uniform "CameraLocations" locs
        |> Sg.cullMode (Mod.constant CullMode.Front)

    let sg = 
        // load the scene and wrap it in an adapter
        Loader.Assimp.load (Path.combine [__SOURCE_DIRECTORY__; "..";"..";"..";"data";"aardvark";"aardvark.obj"])
            |> Sg.adapter

            // flip the z coordinates (since the model is upside down)
            |> Sg.transform (Trafo3d.Scale(1.0, 1.0, -1.0))
            |> Sg.scale 3.0

            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                //do! DefaultSurfaces.normalMap
                //do! DefaultSurfaces.simpleLighting
            }
            |> Sg.uniform "ViewTrafo" stereoViews
            |> Sg.uniform "ProjTrafo" stereoProjs
            |> Sg.uniform "CameraLocation" loc



    let renderTask = 
        // compile the scene graph into a render task
        RenderTask.ofList [
            app.Runtime.CompileClear(win.FramebufferSignature, Mod.constant C4f.Gray)
            app.Runtime.CompileRender(win.FramebufferSignature, sg)
        ]

    // assign the render task to our window...
    win.RenderTask <- renderTask
    win.Run()
    0
