namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.Vulkan
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open FShade
open FShade.Imperative

module CommandTest =
    let run() =
        use app = new VulkanApplication(true)
        let win = app.CreateSimpleRenderWindow(8)
        let runtime = app.Runtime
        let device = runtime.Device



        let cameraView  = DefaultCameraController.control win.Mouse win.Keyboard win.Time (CameraView.LookAt(3.0 * V3d.III, V3d.OOO, V3d.OOI))    
        let frustum     = win.Sizes    |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))       
        let viewTrafo   = cameraView    |> Mod.map CameraView.viewTrafo
        let projTrafo   = frustum       |> Mod.map Frustum.projTrafo        

        

        let sg1 =
            Sg.box' C4b.Red Box3d.Unit
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.Red
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo viewTrafo
                |> Sg.projTrafo projTrafo
                
        let sg2 =
            Sg.unitSphere' 5 C4b.Red
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.constantColor C4f.Green
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo viewTrafo
                |> Sg.projTrafo projTrafo

        let condition = Mod.init true

        win.Keyboard.DownWithRepeats.Values.Add (fun k ->
            match k with    
                | Keys.Space -> transact (fun () -> condition.Value <- not condition.Value)
                | _ -> ()
        )


        let pos = Array.map (fun v -> v / 4.0f) [| V3f(-1.0f, -1.0f, 0.0f); V3f(1.0f, -1.0f, 0.0f); V3f(1.0f, 1.0f, 0.0f); V3f(-1.0f, 1.0f, 0.0f) |]
        let n = Array.create 4 V3f.OOI
        let index = [| 0;1;2; 0;2;3|]

        let box = Primitives.unitBox
        let pos = box.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]> |> Array.map (fun v -> v / 2.0f)
        let n = box.IndexedAttributes.[DefaultSemantic.Normals] |> unbox<V3f[]>
        let tc = box.IndexedAttributes.[DefaultSemantic.DiffuseColorCoordinates] |> unbox<V2f[]>
        let index, fvc =
            match box.IndexArray with
                | null ->
                    None, pos.Length
                | index -> 
                    let idx = Aardvark.Base.BufferView(Mod.constant (ArrayBuffer index :> IBuffer), typeof<int>)
                    Some idx, index.Length


        let quadGeometry (offset : V3d) =
            {
                vertexAttributes    = Map.ofList [DefaultSemantic.Positions, Mod.constant (ArrayBuffer pos :> IBuffer); DefaultSemantic.Normals, Mod.constant (ArrayBuffer n :> IBuffer)]
                indices             = index
                uniforms            = Map.ofList ["ModelTrafo", Mod.constant (Trafo3d.Translation(offset)) :> IMod ]
                call                = Mod.constant [DrawCallInfo(FaceVertexCount = fvc, InstanceCount = 1)]
            }

        let state =
            {
                depthTest           = Mod.constant DepthTestMode.LessOrEqual
                cullMode            = Mod.constant CullMode.None
                blendMode           = Mod.constant BlendMode.None
                fillMode            = Mod.constant FillMode.Fill
                stencilMode         = Mod.constant StencilMode.Disabled
                multisample         = Mod.constant true
                writeBuffers        = None
                globalUniforms      = 
                    UniformProvider.ofList [
                        "ViewTrafo", viewTrafo :> IMod
                        "ProjTrafo", projTrafo :> IMod
                        "LightLocation", viewTrafo |> Mod.map (fun v -> v.Backward.C3.XYZ) :> IMod
                        "CameraLocation", viewTrafo |> Mod.map (fun v -> v.Backward.C3.XYZ) :> IMod
                    ]

                geometryMode        = IndexedGeometryMode.TriangleList
                vertexInputTypes    = Map.ofList [ DefaultSemantic.Positions, typeof<V3f>; DefaultSemantic.Normals, typeof<V3f> ]
                perGeometryUniforms = Map.ofList [ "ModelTrafo", typeof<Trafo3d> ]
            }


        let effects =
            [|
                FShade.Effect.compose [
                    toEffect <| DefaultSurfaces.trafo
                    toEffect <| DefaultSurfaces.constantColor C4f.White
                ]

                FShade.Effect.compose [
                    toEffect <| DefaultSurfaces.trafo
                    toEffect <| DefaultSurfaces.constantColor C4f.White
                    toEffect <| DefaultSurfaces.simpleLighting
                ]
            |]


        let geometries =
            let size = 5
            ASet.ofList [
                for x in -size .. size do
                    for y in -size .. size do
                        for z in -size .. size do
                            yield quadGeometry (V3d(x,y,z))
                    
            ]

        let current = Mod.init 0

        win.Keyboard.DownWithRepeats.Values.Add (fun k ->
            match k with
                | Keys.Space ->
                    transact (fun () -> current.Value <- (1 + current.Value) % effects.Length)
                | _ ->
                    ()
        )


        let cmd = RuntimeCommand.Geometries(effects, current, state, geometries)
//
//        let objects1 = sg1.RenderObjects()
//        let objects2 = sg2.RenderObjects()
//        let cmd = 
//            RuntimeCommand.IfThenElse(
//                condition,
//                RuntimeCommand.Render objects1,
//                RuntimeCommand.Render objects2
//            )


        win.RenderTask <- new RenderTask.CommandTask(device, unbox win.FramebufferSignature, cmd)
        win.Run()
  


