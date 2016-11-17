
(* SEE THIS DEMO IN ACTION https://www.youtube.com/watch?v=QjVRJworUOw


*)

#I @"../../../bin/Debug"
#load "LoadReferences.fsx"
#r "Examples.exe"


open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Rendering.Interactive
open Aardvark.SceneGraph.IO


FsiSetup.initFsi (Path.combine [BinDirectory; "Examples.exe"])
Aardvark.SceneGraph.IO.Loader.Assimp.initialize ()

let fillMode = Mod.init FillMode.Fill

let demoScene =
    let win = Interactive.Window

    let cameraView = 
        CameraView.lookAt (V3d(6.0, 6.0, 6.0)) V3d.Zero V3d.OOI
            |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
            |> Mod.map CameraView.viewTrafo

    let projection = 
        win.Sizes 
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
            |> Mod.map Frustum.projTrafo


    let lodDecider (threshhold : float) (scope : LodScope) =
        (scope.bb.Center - scope.cameraPosition).Length < threshhold

    let modelPath = Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "data"; "aardvark"; "aardvark.obj" ]

    let aardvark = 
        Loader.Assimp.load modelPath
         |> Sg.adapter
         |> Sg.normalizeTo (Box3d(-V3d.III, V3d.III))
         |> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO,V3d.OIO,-V3d.OOI))
         |> Sg.shader {
               do! DefaultSurfaces.trafo
               do! DefaultSurfaces.constantColor C4f.White
               //do! DefaultSurfaces.diffuseTexture
               do! DefaultSurfaces.normalMap
               do! DefaultSurfaces.simpleLighting
           }
         //|> Sg.fileTexture DefaultSemantic.NormalMapTexture 
         //|> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO,V3d.OOI,-V3d.OIO))


    let spheres =
        [
            for x in -3.0 .. 3.0 do
                for y in -3.0 .. 3.0 do
                    //for z in -5.0 .. 5.0 do
                        let highDetail = Sg.lod (lodDecider 2.0) (Sg.unitSphere 3 ~~C4b.Red) aardvark
                        yield 
                            Sg.lod (lodDecider 5.0) (Sg.box ~~C4b.Red ~~(Box3d.FromCenterAndSize(V3d.OOO,V3d.III*2.0))) highDetail
                            //|> Sg.diffuseFileTexture' @"C:\Aardwork\pattern.jpg" true // use this line to load texture from file
                            |> Sg.diffuseTexture DefaultTextures.checkerboard
                            |> Sg.scale 0.4
                            |> Sg.translate x y 0.0
        ] |> Sg.ofSeq

    spheres 
        |> Sg.andAlso (Sg.onOff ~~false aardvark)
        |> Sg.shader {
               do! DefaultSurfaces.trafo
               do! DefaultSurfaces.vertexColor
               do! DefaultSurfaces.diffuseTexture
               do! DefaultSurfaces.simpleLighting
           }
        |> Sg.fillMode fillMode
        |> Sg.viewTrafo cameraView
        |> Sg.projTrafo projection

Interactive.SceneGraph <- demoScene





