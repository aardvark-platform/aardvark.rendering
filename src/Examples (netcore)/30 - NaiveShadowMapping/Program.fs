open System
open FSharp.Data.Adaptive

open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.Application


module Shader =
    open FShade

    type Vertex = {
        [<Position>]        pos     : V4d
        [<WorldPosition>]   wp      : V4d
        [<Color>]           c       : V4d
        [<Normal>]          n       : V3d
    }

    type UniformScope with
        member x.LightViewProj : M44d = uniform?LightViewProj
        member x.LightLocation : V3d = uniform?LightLocation
        
    let private shadowSampler =
        sampler2dShadow {
            texture uniform?ShadowTexture
            filter Filter.MinMagLinear
            addressU WrapMode.Border
            addressV WrapMode.Border
            borderColor C4f.White
            comparison ComparisonFunction.LessOrEqual
        }


    let shadowShader (v : Vertex) =
        fragment {
            let np = uniform.LightViewProj * v.wp
            let p = np.XYZ / np.W
            let tc = V3d(0.5, 0.5,0.5) + V3d(0.5, 0.5, 0.5) * p.XYZ
            let d = max 0.3 (shadowSampler.Sample(tc.XY, tc.Z - 0.000017))
            return V4d(v.c.XYZ * d, v.c.W)
        }


    let lighting (v : Vertex) =
        fragment {
            let n = v.n |> Vec.normalize
            let c = uniform.LightLocation - v.wp.XYZ |> Vec.normalize

            let ambient = 0.0
            let diffuse = Vec.dot (uniform.ViewTrafo * V4d(c,0.0)).XYZ n |> max 0.0

            return V4d(v.c.XYZ * diffuse + V3d(ambient,ambient,ambient), v.c.W)
        }


    type InstanceVertex = { 
        [<Position>]      pos   : V4d 
        [<InstanceTrafo>] trafo : M44d
    }

    let instanceTrafo (v : InstanceVertex) =
        vertex {
            return { v with pos = v.trafo * v.pos }
        }

    let trafo (v : Vertex) =
           vertex {
               let wp = uniform.ModelTrafo * v.pos
               return {
                   pos = uniform.ViewProjTrafo * wp
                   wp = wp
                   n = v.n.XYZ
                   c = v.c
               }
           }
           


[<EntryPoint>]
let main argv = 
    Aardvark.Init()

    let win =
         window {
             backend Backend.GL
             display Display.Mono
             debug false
             samples 8
         }

    let shadowMapSize = V2i(4096, 4096) |> AVal.init
    let initialLightPos = V3d.III * 2.0

    let lightPos = 
        let sw = System.Diagnostics.Stopwatch.StartNew()
        win.Time |> AVal.map (fun _ -> 
            let angle = sw.Elapsed.TotalSeconds * 0.8
            Trafo3d.RotationZ(angle).Forward.TransformPos initialLightPos 
        )

    let lightProj = Frustum.perspective 60.0 0.1 10.0 1.0 |> Frustum.projTrafo |> AVal.constant
    let lightView =
        lightPos |> AVal.map (fun pos -> 
            CameraView.lookAt pos V3d.Zero V3d.OOI |> CameraView.viewTrafo
        )

    let pointCount = 1000

    let box (color : C4b) (box : Box3d) = 
        let randomColor = color 

        let indices =
            [|
                1;2;6; 1;6;5
                2;3;7; 2;7;6
                4;5;6; 4;6;7
                3;0;4; 3;4;7
                0;1;5; 0;5;4
                0;3;2; 0;2;1
            |]

        let positions = 
            [|
                V3f(box.Min.X, box.Min.Y, box.Min.Z)
                V3f(box.Max.X, box.Min.Y, box.Min.Z)
                V3f(box.Max.X, box.Max.Y, box.Min.Z)
                V3f(box.Min.X, box.Max.Y, box.Min.Z)
                V3f(box.Min.X, box.Min.Y, box.Max.Z)
                V3f(box.Max.X, box.Min.Y, box.Max.Z)
                V3f(box.Max.X, box.Max.Y, box.Max.Z)
                V3f(box.Min.X, box.Max.Y, box.Max.Z)
            |]

        let normals = 
            [| 
                V3f.IOO;
                V3f.OIO;
                V3f.OOI;

                -V3f.IOO;
                -V3f.OIO;
                -V3f.OOI;
            |]

        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,

            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                    DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                    DefaultSemantic.Colors, indices |> Array.map (fun _ -> randomColor) :> Array
                ]

        )

    let quadSg (color : C4b) = 
        let index = [|0;1;2; 0;2;3|]
        let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] |> Array.map ((*)3.0f)

        IndexedGeometry(IndexedGeometryMode.TriangleList, index, 
            SymDict.ofList [
                DefaultSemantic.Positions, positions :> Array
                DefaultSemantic.Colors,  Array.init positions.Length (constF color  ) :> Array
                DefaultSemantic.Normals, Array.init positions.Length (constF V3f.OOI) :> Array
            ], SymDict.empty) |> Sg.ofIndexedGeometry

    let pointSg = 
        let rand = Random()
        let randomV3f() = V3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
        let trafos = Array.init pointCount (fun _ -> randomV3f() |> V3d.op_Explicit |> Trafo3d.Translation)
        //let box = Sg.box' C4b.Red (Box3d.FromCenterAndSize(V3d.OOO, 0.04 * V3d.III))
        //Sg.instanced (AVal.constant trafos) box
        Sg.instancedGeometry (AVal.constant <| Array.init pointCount (fun _ -> randomV3f() |> V3d.op_Explicit |> Trafo3d.Translation)) (box C4b.Red (Box3d.FromCenterAndSize(V3d.OOO, 0.04 * V3d.III)))


    let sceneSg (fragmentShader : list<FShadeEffect>) =
        quadSg C4b.Green 
        |> Sg.effect ( 
            (Shader.trafo |> toEffect) :: fragmentShader 
           )
        |> Sg.andAlso ( 
            pointSg |> Sg.effect [
                yield Shader.instanceTrafo |> toEffect
                yield Shader.trafo         |> toEffect
                yield! fragmentShader
            ]
           )
        |> Sg.uniform "LightViewProj" (AVal.map2 (*) lightView lightProj)
        |> Sg.trafo ( Trafo3d.Translation(V3d(0.0,0.0,0.3)) |> AVal.constant )

    let signature = 
           win.Runtime.CreateFramebufferSignature [
               DefaultSemantic.Depth, { format = RenderbufferFormat.DepthComponent32; samples = 1 }
           ]

    let shadowDepth =
        sceneSg [ DefaultSurfaces.constantColor C4f.White |> toEffect; DefaultSurfaces.vertexColor |> toEffect ]
            |> Sg.viewTrafo lightView
            |> Sg.projTrafo lightProj 
            |> Sg.compile win.Runtime signature   
            |> RenderTask.renderToDepth shadowMapSize

    let sg =
        sceneSg [ Shader.lighting  |> toEffect; Shader.shadowShader |> toEffect;   ]
            |> Sg.uniform "LightLocation" lightPos
            |> Sg.texture (Sym.ofString "ShadowTexture") shadowDepth
            |> Sg.andAlso (
                Sg.cone' 32 C4b.Red 0.1 0.5 |> Sg.trafo (lightPos |> AVal.map Trafo3d.Translation)
                |> Sg.effect [ 
                    DefaultSurfaces.trafo |> toEffect; 
                    DefaultSurfaces.constantColor C4f.Red |> toEffect; 
                    DefaultSurfaces.simpleLighting |> toEffect 
                  ]
            )

    win.Scene <- sg
    win.Run()


    0
