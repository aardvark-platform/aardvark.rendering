
#load "RenderingSetup.fsx"
open RenderingSetup

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Default // makes viewTrafo and other tutorial specicific default creators visible

let rand = Random()
let randomPoints pointCount =
    let randomV3f() = V3f(rand.NextDouble(), rand.NextDouble(), rand.NextDouble())
    let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

    IndexedGeometry(
        Mode = IndexedGeometryMode.PointList,
        IndexedAttributes = 
            SymDict.ofList [
                 DefaultSemantic.Positions, Array.init pointCount (fun _ -> randomV3f()) :> Array
                 DefaultSemantic.Colors, Array.init pointCount (fun _ -> randomColor()) :> Array
            ]
    )
                                    
let geometries = Array.init 100 (fun _ -> randomPoints 100) |> ASet.ofArray 
let sg = Sg.GeometrySet( geometries, IndexedGeometryMode.PointList, 
                            Map.ofList [ DefaultSemantic.Positions, typeof<V3f>
                                         DefaultSemantic.Colors, typeof<C4b> ] ) :> ISg



let final =
    sg |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect                  
            DefaultSurfaces.vertexColor |> toEffect 
            ]
        // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
        |> Sg.viewTrafo (viewTrafo   () |> Mod.map CameraView.viewTrafo ) 
        // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
        // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
        |> Sg.projTrafo (perspective () |> Mod.map Frustum.projTrafo    )

setSg final
