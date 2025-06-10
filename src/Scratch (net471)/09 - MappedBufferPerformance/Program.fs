open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Diagnostics


module AdaptiveGeometryCustom =

    let references = Dict<obj, int>()

    let add (key : obj) =
        lock references (fun _ ->
            let value = references.GetOrDefault(key)
            references.[key] <- value + 1
        )

    let remove (key : obj) =
        lock references (fun _ ->
            let value = references.Get(key)
            references.[key] <- value - 1
        )

    let allReleased() =
        lock references (fun _ ->
            references |> Seq.forall (fun (KeyValue(_, value)) -> value = 0)
        )

    type ArrayResource(array : Array) =
        inherit AdaptiveResource<IBuffer>()

        let buffer = ArrayBuffer(array) :> IBuffer

        override x.Create() =
            add array

        override x.Destroy() =
            remove array

        override x.Compute(t, rt) =
            buffer

    let ofIndexedGeometry (instanceAttributes : list<Symbol * IAdaptiveValue>) (ig : IndexedGeometry) =
        let indexBuffer =
            if ig.IsIndexed then BufferView.ofArray ig.IndexArray
            else Unchecked.defaultof<_>

        let vertexAttributes =
            ig.IndexedAttributes |> SymDict.map (fun _ arr ->
                let res = ArrayResource(arr)
                BufferView(res, arr.GetType().GetElementType())
            )

        AdaptiveGeometry(
            ig.FaceVertexCount, ig.VertexCount, indexBuffer,
            vertexAttributes, Dictionary.ofList instanceAttributes
        )

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
    
    
    Aardvark.Init()

    use app = new OpenGlApplication()
    let win = app.CreateSimpleRenderWindow(samples = 8)
    //use app = new VulkanApplication(debug = true)
    //let win = app.CreateSimpleRenderWindow(8)

    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    let frustum = 
        win.Sizes 
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let rnd = Random(1123)


    let pool =
        app.Runtime.CreateManagedPool {
            IndexType = typeof<int>
            VertexAttributeTypes = 
                Map.ofList [ 
                    DefaultSemantic.Positions, typeof<V3f>
                    DefaultSemantic.Normals, typeof<V3f> 
                    DefaultSemantic.DiffuseColorCoordinates, typeof<V3f> 
                ]
            InstanceAttributeTypes = 
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
            
        Report.BeginTimed("add to pool: vc={0}", ag.VertexCount)
        let mdc = pool.Add ag
        Report.End() |> ignore
        mdc

    let disp, pooledGeometries = 
        geometries |> ASet.map (fun (g, t) -> 
                                        let ntr = t|> AVal.map(fun t -> t.Backward.Transposed |> M33d.op_Explicit)
                                        g |> AdaptiveGeometryCustom.ofIndexedGeometry [ (Sem.InstanceTrafo, (t :> IAdaptiveValue)); (Sem.InstanceNormalTrafo, (ntr :> IAdaptiveValue)) ])
                    |> ASet.mapUse (fun ag -> addToPool ag)
                    
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

    use renderTask = 
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    let sw = Stopwatch.StartNew()

    let renderTask = 
        
        RenderTask.custom (fun (self,token,outputDesc) -> 
            win.Time.GetValue self |> ignore

            transact(fun () -> 

                //let subset = geometrySet.RandomOrder().TakeToArray(geometrySet.Length - 1)
                //geometries.Clear()
                //geometries.AddRange(subset) |> ignore
                
                let rotation = sw.Elapsed.TotalSeconds * 360.0 // 360° per second
                sw.Restart()

                geometries |> ASet.force |> Seq.take 100 |> Seq.iter (fun (g, t) -> 
                        t.Value <- Trafo3d.RotationZInDegrees(rotation) * t.Value)
                )
            
            renderTask.Run(self, token, outputDesc)
        )
        
    win.RenderTask <- renderTask
    win.Run()

    // either dispose the pool directly, or dispose all the draw calls
    disp.Dispose()

    assert (AdaptiveGeometryCustom.allReleased())
    
    0
