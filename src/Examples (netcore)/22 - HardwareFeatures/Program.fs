(*

This example shows various variants of rendering lots of objects ranging including:
 * simple huge scene graph
 * custom render objects generated in a flat manner
 * instanced geometries
 * indirect buffer based rendering

The purpose is to show performance and startup cost tradeoffs. For real usage see <FlexibleDrawCommands>.
 
*)

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.SceneGraph.Semantics
open Aardvark.Application.Slim
open Aardvark.Rendering.Text

type IRenderTask with
    member x.PrepareForRender() =
        match x.FramebufferSignature with
            | Some signature ->
                let runtime = signature.Runtime
                let tempFbo = runtime.CreateFramebuffer(signature, AVal.constant(V2i(16,16)))
                tempFbo.Acquire()
                x.Run(RenderToken.Empty, tempFbo.GetValue())
                tempFbo.Release()
            | None ->
                ()


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

let createNaive (runtime : IRuntime) (signature : IFramebufferSignature)
                (viewTrafo : aval<Trafo3d>) (projTrafo : aval<Trafo3d>) 
                (geometry : IndexedGeometry) (trafos : Trafo3d[]) =

    let object = 
        geometry |> Sg.ofIndexedGeometry

    let objects = 
        [| for t in trafos do 
            yield Sg.trafo (AVal.constant t) object 
        |] |> Sg.ofSeq
  
    let sg = 
        objects
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Magenta
                do! DefaultSurfaces.simpleLighting
            }
            |> Sg.viewTrafo viewTrafo
            |> Sg.projTrafo projTrafo

    Log.startTimed "[naive] compile scene"
    let r = runtime.CompileRender(signature, sg)
    r.PrepareForRender()
    Log.stop()
    r

let createInstanced (runtime : IRuntime) (signature : IFramebufferSignature)
                    (viewTrafo : aval<Trafo3d>) (projTrafo : aval<Trafo3d>) 
                    (geometry : IndexedGeometry) (trafos : Trafo3d[]) =

    // here we use low level draw call construction. Sg.instanced would work as well of course.
    let call = 
        DrawCallInfo(
            FaceVertexCount = geometry.IndexedAttributes.[DefaultSemantic.Positions].Length, 
            InstanceCount = trafos.Length,
            FirstInstance = 0,
            BaseVertex = 0
        )
  
    let sg = 
        Sg.render IndexedGeometryMode.TriangleList call
            // apply vertex attributes as usual
            |> Sg.vertexArray DefaultSemantic.Positions geometry.IndexedAttributes.[DefaultSemantic.Positions]
            |> Sg.vertexArray DefaultSemantic.Normals   geometry.IndexedAttributes.[DefaultSemantic.Normals]
            // remember to use M44f's for your matrices
            |> Sg.instanceArray DefaultSemantic.InstanceTrafo (trafos |> Array.map (fun t -> t.Forward |> M44f.op_Explicit))
            |> Sg.shader {
                do! Shader.orthoInstanceTrafo
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Green
                do! DefaultSurfaces.simpleLighting
            }
            |> Sg.viewTrafo viewTrafo
            |> Sg.projTrafo projTrafo

    Log.startTimed "[instanced] compile scene"
    let r = runtime.CompileRender(signature, sg)
    r.PrepareForRender()
    Log.stop()
    r


let createIndirect (runtime : IRuntime) (signature : IFramebufferSignature)
                   (viewTrafo : aval<Trafo3d>) (projTrafo : aval<Trafo3d>) 
                   (geometry : IndexedGeometry) (trafos : Trafo3d[]) =

    // for the sake of demonstration, we create an array of draw call infos which use firstInstance 
    // variable to offset the instance buffer.
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
                  
    // simply wrap the drawcall infos array into buffers
    let indirect = IndirectBuffer.ofArray drawCallInfos
  
    let sg = 
        Sg.indirectDraw IndexedGeometryMode.TriangleList (AVal.constant indirect)
            |> Sg.vertexArray DefaultSemantic.Positions geometry.IndexedAttributes.[DefaultSemantic.Positions]
            |> Sg.vertexArray DefaultSemantic.Normals geometry.IndexedAttributes.[DefaultSemantic.Normals]
            // apply instance array as usual
            |> Sg.instanceArray DefaultSemantic.InstanceTrafo (trafos |> Array.map (fun t -> t.Forward |> M44f.op_Explicit))
            |> Sg.shader {
                do! Shader.orthoInstanceTrafo
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Yellow
                do! DefaultSurfaces.simpleLighting
            }
            |> Sg.viewTrafo viewTrafo
            |> Sg.projTrafo projTrafo
        
    Log.startTimed "[custom indirect buffer] compile scene"
    let r = runtime.CompileRender(signature, sg)
    r.PrepareForRender()
    Log.stop()
    r

let renderObjectBased (runtime : IRuntime) (signature : IFramebufferSignature)
                      (viewTrafo : aval<Trafo3d>) (projTrafo : aval<Trafo3d>) 
                      (geometry : IndexedGeometry) (trafos : Trafo3d[])  =

    // since it is painful to construct render objects from scratch we create a template render object which
    // we later use to create instances
    let template =
        geometry
            |> Sg.ofIndexedGeometry
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.Red
                do! DefaultSurfaces.simpleLighting

            }

    // extract the render object using the scene graph semantics
    let template =
        template.RenderObjects(Ag.Scope.Root).Content |> AVal.force |> HashSet.toList |> List.head |> unbox<RenderObject>

    // since we left the world of composable scene graphs we need to apply all typically automatically constructed
    // uniform values by hand. 
    let cam = viewTrafo |> AVal.map (fun v -> v.Backward.TransformPosProj V3d.Zero)

    let uniforms (t : Trafo3d) =
        UniformProvider.ofList [
            "ModelTrafo", AVal.constant t :> IAdaptiveValue
            "ViewTrafo", viewTrafo :> IAdaptiveValue
            "CameraLocation", cam :> IAdaptiveValue
            "LightLocation", cam :> IAdaptiveValue
            "ProjTrafo", projTrafo :> IAdaptiveValue
        ]

    // next instantiate the objects (using copy)
    let renderObjects =
        trafos |> Array.map (fun trafo -> 
            { template with
                Id = newId()
                Uniforms = uniforms trafo
            } :> IRenderObject
        )

    Log.startTimed "[custom render objects] compile scene"
    let r = runtime.CompileRender(signature, ASet.ofArray renderObjects)
    r.PrepareForRender()
    Log.stop()
    r




[<EntryPoint>]
let main argv = 
    
    
    Aardvark.Init()

    // uncomment/comment to switch between the backends
    //use app = new VulkanApplication() 
    use app = new OpenGlApplication()
    let runtime = app.Runtime :> IRuntime

    // create a game window (better for measuring fps)
    let win = app.CreateGameWindow(samples = 1)

    // disable incremental rendering
    win.RenderAsFastAsPossible <- true


    let initialView = CameraView.LookAt(V3d(10.0,10.0,10.0), V3d.Zero, V3d.OOI)
    let frustum = 
        win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 150.0 (float s.X / float s.Y))

    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    // adjust this value for different test sizes
    let size = 13
    let trafos =
        [|
            for x in -size .. size do
                for y in -size .. size do
                    for z in -size .. size do
                        yield Trafo3d.Scale(0.3) * Trafo3d.Translation(float x, float y, float z)
        |]

    // create render tasks for all previously mentioned variants
    let geometry = Primitives.unitBox 
    let viewTrafo = cameraView |> AVal.map CameraView.viewTrafo
    let projTrafo = frustum |> AVal.map Frustum.projTrafo

    let variants = 
        [|
            createInstanced runtime win.FramebufferSignature viewTrafo projTrafo geometry trafos
            createIndirect runtime win.FramebufferSignature viewTrafo projTrafo geometry trafos
            renderObjectBased runtime win.FramebufferSignature viewTrafo projTrafo geometry trafos
            // naive sg is slow for such big scenes:
            //createNaive app.Runtime win.FramebufferSignature BackendConfiguration.NativeOptimized viewTrafo projTrafo geometry trafos
        |]

    let variantNames =
        [|
            "instanced"
            "multidraw"
            "renderobj"
        |]



    // use this mutable to switch between render task variants.
    let variant = AVal.init 0
    let fps = AVal.init 0.0

    win.Keyboard.KeyDown(Keys.V).Values.Add(fun _ -> 
        transact (fun () -> 
            variant.Value <- (variant.Value + 1) % variants.Length
            fps.Value <- 0.0
        )
        Log.line "using: %s" variantNames.[variant.Value]
    )

    win.Keyboard.KeyDown(Keys.OemPlus).Values.Add(fun _ -> 
        transact (fun () -> 
            variant.Value <- (variant.Value + 1) % variants.Length
            fps.Value <- 0.0
        )
        Log.line "using: %s" variantNames.[variant.Value]
    )
    
    win.Keyboard.KeyDown(Keys.OemMinus).Values.Add(fun _ -> 
        transact (fun () -> 
            variant.Value <- (variant.Value + variants.Length - 1) % variants.Length
            fps.Value <- 0.0
        )
        Log.line "using: %s" variantNames.[variant.Value]
    )
    let task = 
        RenderTask.custom (fun (t,rt,desc) -> 
            variants.[variant.Value].Run(t,rt,desc)
        )

    let puller =
        async {
            while true do
                if not (Fun.IsTiny win.AverageFrameTime.TotalSeconds) then
                    transact (fun () -> fps.Value <- 1.0 / win.AverageFrameTime.TotalSeconds)
                do! Async.Sleep 200
        }
    Async.Start puller
    let overlayTask =
        let str =
            AVal.custom (fun t ->
                let variant = variant.GetValue t 
                let fps = fps.GetValue t
                let fps = if fps <= 0.0 then "" else sprintf "%.0ffps" fps
                let variant = variantNames.[variant]
                String.concat " " [variant; fps]
            )

        let trafo =
            win.Sizes |> AVal.map (fun size ->
                let px = 2.0 / V2d size
                Trafo3d.Scale(0.1) *
                Trafo3d.Scale(1.0, float size.X / float size.Y, 1.0) *
                Trafo3d.Translation(-1.0 + 20.0 * px.X, -1.0 + 25.0 * px.Y, 0.0)
            )

        Sg.text (Font("Consolas")) C4b.White str
            |> Sg.trafo trafo
            |> Sg.compile runtime win.FramebufferSignature
            


    win.RenderTask <- RenderTask.ofList [ task; overlayTask]
    win.Run()

    0
