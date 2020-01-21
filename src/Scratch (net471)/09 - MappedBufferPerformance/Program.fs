open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Diagnostics

[<AutoOpen>]
module Shader =
    open FShade 

    module Sem =
        let InstanceTrafo = Symbol.Create "InstanceTrafo"
        let InstanceNormalTrafo = Symbol.Create "InstanceNormalTrafo"

    type InstanceVertex = 
        {
            [<Semantic("InstanceTrafo")>] mt : M44d
            [<Semantic("InstanceNormalTrafo")>] nt : M33d
            [<Position>] p : V4d
            [<Normal>] n : V3d
        }

    let instanceShade (v : InstanceVertex) =
        vertex {
            return { v 
                with 
                    p = v.mt * v.p 
                    n = v.nt * v.n
            }
        }

[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication()
    let win = app.CreateGameWindow(samples = 8)

    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    let frustum = 
        win.Sizes 
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let rnd = Random(1123)


    let pool =
        app.Runtime.CreateManagedPool {
            indexType = typeof<int>
            vertexBufferTypes = 
                Map.ofList [ 
                    DefaultSemantic.Positions, typeof<V3f>
                    DefaultSemantic.Normals, typeof<V3f> 
                    DefaultSemantic.DiffuseColorCoordinates, typeof<V3f> 
                ]
            uniformTypes = 
                Map.ofList [ 
                    Sem.InstanceTrafo, typeof<M44f> 
                    Sem.InstanceNormalTrafo, typeof<M33f> 
                ]
        }

    let rnd = Random()

    let geometrySet = Array.init 1000 (fun i -> 
            let pos = V3d( 
                        rnd.NextDouble() * 10.0 - 5.0, 
                        rnd.NextDouble() * 10.0 - 5.0,
                        rnd.NextDouble() * 10.0 - 5.0 )
            let trafo = Trafo3d.RotationZ(360.0 * rnd.NextDouble()) * Trafo3d.Scale 0.1 * Trafo3d.Translation pos
            let ig = IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.OOO, V3d.III)) C4b.Red
            let trafo = AVal.init(trafo)
            (ig, trafo)
        )

    let geometries = cset geometrySet
               
    let addToPool(ag : AdaptiveGeometry) = 
            
        Report.BeginTimed("add to pool: vc={0}", ag.vertexCount)
        let mdc = pool.Add ag
        Report.End() |> ignore
        mdc

    let pooledGeometries = 
        geometries |> ASet.map (fun (g, t) -> 
                                        let ntr = t|> AVal.map(fun t -> t.Backward.Transposed |> M33d.op_Explicit)
                                        g |> AdaptiveGeometry.ofIndexedGeometry [ (Sem.InstanceTrafo, (t :> IAdaptiveValue)); (Sem.InstanceNormalTrafo, (ntr :> IAdaptiveValue)) ])
                    
                    // TODO: what about use??????
                    |> ASet.map (fun ag -> addToPool ag)
                    
    let sg = 
        Sg.PoolNode(pool, pooledGeometries, IndexedGeometryMode.TriangleList)
            |> Sg.uniform "LightLocation" (AVal.constant (10.0 * V3d.III))
            |> Sg.effect [
                Shader.instanceShade |> toEffect
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.Red |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect
            ]
            |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo    )

    let renderTask = 
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    let sw = Stopwatch.StartNew()

    let renderTask = 
        
        RenderTask.custom (fun (self,token,outputDesc) -> 
            
            transact(fun () -> 

                //let subset = geometrySet.RandomOrder().TakeToArray(geometrySet.Length - 1)
                //geometries.Clear()
                //geometries.AddRange(subset) |> ignore
                
                let rotation = sw.Elapsed.TotalSeconds * 360.0 // 360° per second
                sw.Restart()

                geometries |> ASet.force |> Seq.take 100 |> Seq.iter (fun (g, t) -> 
                        t.Value <- Trafo3d.RotationZInDegrees(rotation) * t.Value)
                )
            
            renderTask.Run(token, outputDesc)
        )
        
    win.RenderTask <- renderTask
    win.Run()
    
    0
