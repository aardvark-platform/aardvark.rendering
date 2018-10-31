open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.SceneGraph.Semantics
open Aardvark.Application.Slim

module Shader =
    open FShade

    type InstanceVertex = { 
        [<Position>]            pos   : V4d 
        [<Normal>]              n     : V3d 
        [<BiNormal>]            b     : V3d 
        [<Tangent>]             t     : V3d 
        [<InstanceTrafo>]       trafo : M44d
    }

    let orthoInstanceTrafo (v : InstanceVertex) =
        vertex {
            return 
                { v with 
                    pos = v.trafo * v.pos 
                    n = v.trafo.TransformDir(v.n)
                    b = v.trafo.TransformDir(v.b)
                    t = v.trafo.TransformDir(v.t)
                }
        }

let createInstanced (runtime : IRuntime) (signature : IFramebufferSignature) (backendConfiguration : BackendConfiguration) 
                    (viewTrafo : IMod<Trafo3d>) (projTrafo : IMod<Trafo3d>) 
                    (geometry : IndexedGeometry) (trafos : Trafo3d[]) =

    let call = 
        DrawCallInfo(
            FaceVertexCount = geometry.IndexedAttributes.[DefaultSemantic.Positions].Length, 
            InstanceCount = trafos.Length,
            FirstInstance = 0,
            BaseVertex = 0
        )
  
    Sg.render IndexedGeometryMode.TriangleList call
        |> Sg.vertexArray DefaultSemantic.Positions geometry.IndexedAttributes.[DefaultSemantic.Positions]
        |> Sg.vertexArray DefaultSemantic.Normals   geometry.IndexedAttributes.[DefaultSemantic.Normals]
        |> Sg.instanceArray DefaultSemantic.InstanceTrafo (trafos |> Array.map (fun t -> t.Forward |> M44f.op_Explicit))
        |> Sg.shader {
            do! Shader.orthoInstanceTrafo
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor C4f.Green
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.viewTrafo viewTrafo
        |> Sg.projTrafo projTrafo
        |> Sg.compile' runtime signature backendConfiguration

let createIndirect (runtime : IRuntime) (signature : IFramebufferSignature) (backendConfiguration : BackendConfiguration)  
                   (viewTrafo : IMod<Trafo3d>) (projTrafo : IMod<Trafo3d>) 
                   (geometry : IndexedGeometry) (trafos : Trafo3d[]) =

    let drawCallInfos =
        trafos 
        |> Array.mapi (fun i _ -> 
                DrawCallInfo(
                    FaceVertexCount = geometry.IndexedAttributes.[DefaultSemantic.Positions].Length, 
                    InstanceCount = 1, 
                    FirstInstance = i,
                    BaseVertex = 0
                )
           )
                  

    let indirect = IndirectBuffer(ArrayBuffer drawCallInfos, drawCallInfos.Length) :> IIndirectBuffer
  
    Sg.indirectDraw IndexedGeometryMode.TriangleList (Mod.constant indirect)
        |> Sg.vertexArray DefaultSemantic.Positions geometry.IndexedAttributes.[DefaultSemantic.Positions]
        |> Sg.vertexArray DefaultSemantic.Normals geometry.IndexedAttributes.[DefaultSemantic.Normals]
        |> Sg.instanceArray DefaultSemantic.InstanceTrafo (trafos |> Array.map (fun t -> t.Forward |> M44f.op_Explicit))
        |> Sg.shader {
            do! Shader.orthoInstanceTrafo
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor C4f.Yellow
            do! DefaultSurfaces.simpleLighting
        }
        |> Sg.viewTrafo viewTrafo
        |> Sg.projTrafo projTrafo
        |> Sg.compile' runtime signature backendConfiguration

let renderObjectBased (runtime : IRuntime) (signature : IFramebufferSignature) (backendConfiguration : BackendConfiguration) 
                      (viewTrafo : IMod<Trafo3d>) (projTrafo : IMod<Trafo3d>) 
                      (geometry : IndexedGeometry) (trafos : Trafo3d[])  =

    let template =
        geometry
            |> Sg.ofIndexedGeometry
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Red
                do! DefaultSurfaces.simpleLighting

            }

    let template =
        template.RenderObjects() |> ASet.toList |> List.head |> unbox<RenderObject>

    let cam = viewTrafo |> Mod.map (fun v -> v.Backward.TransformPosProj V3d.Zero)

    let uniforms (t : Trafo3d) =
        UniformProvider.ofList [
            "ModelTrafo", Mod.constant t :> IMod
            "ViewTrafo", viewTrafo :> IMod
            "CameraLocation", cam :> IMod
            "LightLocation", cam :> IMod
            "ProjTrafo", projTrafo :> IMod
        ]

    let renderObjects =
        trafos |> Array.map (fun trafo -> 
            { template with
                Id = newId()
                Uniforms = uniforms trafo
            } :> IRenderObject
        )

    runtime.CompileRender(signature, backendConfiguration, ASet.ofArray renderObjects)




[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    use app = new VulkanApplication()
    //use app = new OpenGlApplication()
    let win = app.CreateGameWindow(samples = 1)
    win.RenderAsFastAsPossible <- true

    let initialView = CameraView.LookAt(V3d(10.0,10.0,10.0), V3d.Zero, V3d.OOI)
    let frustum = 
        win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    let size = 22
    let trafos =
        [|
            for x in -size .. size do
                for y in -size .. size do
                    for z in -size .. size do
                        yield Trafo3d.Scale(0.3) * Trafo3d.Translation(float x, float y, float z)
        |]


    let config = BackendConfiguration.Default
    let geometry = Primitives.unitBox //IndexedGeometryPrimitives.solidPhiThetaSphere (Sphere3d.FromRadius(1.0)) 10 C4b.Green
    let viewTrafo = cameraView |> Mod.map CameraView.viewTrafo
    let projTrafo = frustum |> Mod.map Frustum.projTrafo

    let variants = 
        [|
            createInstanced app.Runtime win.FramebufferSignature config viewTrafo projTrafo geometry trafos
            createIndirect app.Runtime win.FramebufferSignature config viewTrafo projTrafo geometry trafos
            renderObjectBased app.Runtime win.FramebufferSignature config viewTrafo projTrafo geometry trafos
            renderObjectBased app.Runtime win.FramebufferSignature BackendConfiguration.UnmanagedOptimized viewTrafo projTrafo geometry trafos
        |]

    let mutable variant = 0

    win.Keyboard.KeyDown(Keys.V).Values.Add(fun _ -> 
        variant <- (variant + 1) % variants.Length
        printfn "using variant: %d" variant
    )

    let task = 
        RenderTask.custom (fun (task,token,desc) -> 
            variants.[variant].Run(token,desc)
        )

    win.RenderTask <- task
    win.Run()

    0
