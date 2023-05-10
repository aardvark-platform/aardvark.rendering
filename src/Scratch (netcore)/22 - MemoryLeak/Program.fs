open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

open FSharp.Data.Adaptive
open System.Threading


[<EntryPoint>]
let main argv = 

    Aardvark.Init()

    use app = new OpenGlApplication()

    let win = app.CreateGameWindow(samples = 4)

    let initialView = CameraView.LookAt(V3d(3.0), V3d.Zero, V3d.OOI)

    let frustum = 
        win.Sizes 
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let boxSg = Sg.ofIndexedGeometry Primitives.unitBox // SOLUTION: never build static "geometry leafs" that are then instaciated with temporary objects -> move "Sg.ofIndexedGeometry" to Sg creation
                
    let instances = cset<aval<int[]>>()

    let sg = 
        instances |> ASet.map (fun inst -> 
                                // what I think happens:
                                // this creates a new RenderObject that then queries all its attributes
                                // the sg looks like: Effect -> Trafo -> boxSg
                                //  - new AgScopes will be created for each when querying the attributes
                                //  - the scope of the static boxSg (Sg.VertexAttributeApplicator) -> will live forever
                                //  - this has a strong reference to its parent which is the AgScope of the instance Sg.TrafoApplicator -> will also live forever
                                boxSg // SOLUTION: move "Sg.ofIndexedGeometry" to here
                                    |> Sg.trafo (inst |> AVal.map(fun x -> Trafo3d.Identity))
                                    |> Sg.effect [
                                            DefaultSurfaces.trafo                 |> toEffect
                                            DefaultSurfaces.constantColor C4f.Red |> toEffect
                                        ]
                        )

    let sg = Sg.Set(sg)
                |> Sg.viewTrafo (cameraView |> AVal.map CameraView.viewTrafo )
                |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo )

    let changer () = 
        System.Threading.Thread.Sleep 2000
        while true do
            System.Threading.Thread.Sleep 10 // 100 MB/s
            transact (fun _ -> 
                instances.Clear() 
                instances.Add(AVal.init (Array.zeroCreate 1000000)) |> ignore // 1MB each
            )

    let t = Thread(ThreadStart changer)
    t.IsBackground <- true
    t.Start()

    let renderTask = 
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    win.RenderTask <- renderTask
    win.Run()
    0
