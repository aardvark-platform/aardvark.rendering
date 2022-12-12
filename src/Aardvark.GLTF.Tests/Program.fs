open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.GLTF
open Aardvark.Application
open FSharp.Data.Adaptive

[<EntryPoint>]
let main args =
    
    let initialModels =
        args |> Array.choose GLTF.tryLoad
    
    Aardvark.Init()
   
    let app = new Aardvark.Application.Slim.OpenGlApplication(DebugLevel.None)
    let win = app.CreateGameWindow(4)
    
    let view =
        CameraView.lookAt (V3d(4,5,6)) V3d.Zero V3d.OOI
        |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
        |> AVal.map CameraView.viewTrafo
        
    let proj =
        win.Sizes
        |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
        |> AVal.map Frustum.projTrafo
        
    let enableTask =
        RenderTask.custom (fun _ ->
            OpenTK.Graphics.OpenGL4.GL.Enable(OpenTK.Graphics.OpenGL4.EnableCap.TextureCubeMapSeamless)
        )

    let testScene =
        let mutable materials = HashMap.empty
        let mutable geometries = HashMap.empty
        let mutable nodes = []
            
        let geometry =
            let prim = IndexedGeometryPrimitives.solidPhiThetaSphere (Sphere3d(V3d.Zero, 0.3)) 36 C4b.White
            let pos = prim.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
            
            let idx = prim.IndexArray :?> int[]
            // for i in 0 .. 3 .. idx.Length - 2 do
            //     Fun.Swap(&idx.[i], &idx.[i+2])
            
            {
                Name            = Some "Sphere"
                BoundingBox     = Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.6)
                Mode            = IndexedGeometryMode.TriangleList
                Index           = Some idx
                Positions       = pos
                Normals         = Some (pos |> Array.map Vec.normalize)
                Tangents        = None
                TexCoords       = []
                Colors          = None
            }
            
        let gid = MeshId.New()
        geometries <- HashMap.add gid geometry geometries
        
        
        let steps = 8
        for ri in 0 .. steps - 1 do
            let mutable roughness = float ri / float (steps - 1)
            for mi in 0 .. steps - 1 do
                let mutable metalness = float mi / float (steps - 1)
                let offset = Trafo3d.Translation(float ri, float mi, 0.0)
                
                let mid = MaterialId.New()
                
                let material =  
                    {
                        Name                = Some (sprintf "%.3f_%.3f" roughness metalness)
                        
                        DoubleSided         = true
                        Opaque              = true
                            
                        BaseColorTexture       = None
                        BaseColor         = C4f.Beige
                            
                        Roughness           = roughness
                        RoughnessTexture    = None
                        RoughnessTextureComponent = 0
                        
                        Metallicness        = metalness
                        MetallicnessTexture = None
                        MetallicnessTextureComponent = 0
                        
                        EmissiveColor       = C4f.Black
                        EmissiveTexture     = None
                        
                        NormalTexture       = None
                        NormalTextureScale  = 1.0
                    }
                
                materials <- HashMap.add mid material materials
                nodes <- { Name = None; Trafo = Some offset; Meshes = [ { Mesh = gid; Material = Some mid } ]; Children = [] } :: nodes
        
        {
            Materials = materials
            Meshes = geometries
            ImageData = HashMap.empty
            RootNode = { Name = None; Trafo = None; Meshes = []; Children = nodes }
        }
        
        
    testScene |> GLTF.save "/Users/schorsch/Desktop/test.glb"
        
    let models = clist (if initialModels.Length > 0 then Array.toList initialModels else [ testScene ])
        
    win.DropFiles.Add (fun paths ->
        let ms = paths |> Array.choose GLTF.tryLoad
        if ms.Length > 0 then
            transact (fun () ->
                models.UpdateTo models |> ignore
            )
    )
   
    let centerTrafo1 =
        models |> AList.toAVal |> AVal.map (fun models ->
            let bb = models |> Seq.map (fun m -> m.BoundingBox) |> Box3d
            Trafo3d.Translation(-bb.Center) *
            Trafo3d.Scale(5.0 / bb.Size.NormMax)
        )
 

    let renderTask =
        Sg.ofList [
            models
            |> AList.toAVal
            |> AVal.map (fun scenes -> SceneSg.toSimpleSg win.Runtime scenes)
            |> Sg.dynamic
            |> Sg.trafo centerTrafo1
            |> Sg.trafo' (Trafo3d.RotationX Constant.PiHalf)
            
        ]
        //|> Sg.uniform' "LightLocation" (V3d(10,20,30))
        |> Sg.shader {
            do! Shader.trafo
            do! Shader.shade
        }
        //|> Sg.trafo rot
        |> Sg.viewTrafo view
        |> Sg.projTrafo proj
        |> Sg.compile win.Runtime win.FramebufferSignature
    win.RenderTask <- RenderTask.ofList [enableTask; renderTask]
    win.Run()
    
    0