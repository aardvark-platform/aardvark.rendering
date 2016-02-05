
#load "RenderingSetup.fsx"
open RenderingSetup

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

open Default // makes viewTrafo and other tutorial specicific default creators visible

let rand = Random()
let randomPoints (bounds : Box3d) (pointCount : int) =
    let size = bounds.Size
    let randomV3f() = V3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()) * size + bounds.Min |> V3f.op_Explicit
    let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

    IndexedGeometry(
        Mode = IndexedGeometryMode.PointList,
        IndexedAttributes = 
            SymDict.ofList [
                 DefaultSemantic.Positions, Array.init pointCount (fun _ -> randomV3f()) :> Array
                 DefaultSemantic.Colors, Array.init pointCount (fun _ -> randomColor()) :> Array
            ]
    )
                                    
module ASet =
    type private UpdateReader<'a>(r : IReader<'a>, m : IMod<unit>) =
        inherit ASetReaders.AbstractReader<'a>()

        override x.ComputeDelta() =
            m.GetValue x
            r.GetDelta x

        override x.Release() =
            m.RemoveOutput x
            r.RemoveOutput x

        override x.Inputs = Seq.ofList [m :> IAdaptiveObject; r :> IAdaptiveObject]

    let withUpdater (m : IMod<unit>) (s : aset<'a>) : aset<'a> =
        ASet.AdaptiveSet(fun () -> new UpdateReader<'a>(s.GetReader(), m) :> IReader<_>) :> aset<_>

let threshold = Mod.init 0.4

let boxes (view : IMod<CameraView>) (proj : IMod<Frustum>) =
    let set = cset []

    let changer =
        Mod.custom (fun self ->
            let view = view.GetValue self
            let proj = proj.GetValue self
            let threshold = threshold.GetValue self

            let decide _ (area : float) (distance : float) =
                let f = area / (distance * distance)
                f > threshold


            let cells = HashSet(GridCell.raster decide view proj)

            let added = cells |> Seq.filter (set.Contains >> not) |> Seq.toList
            let removed = set |> Seq.filter (cells.Contains >> not) |> Seq.toList

            match added, removed with
                | [], [] -> ()
                | added, [] -> transact (fun () -> set.UnionWith added)
                | [], removed -> transact (fun () -> set.ExceptWith removed)
                | added, removed -> 
                    transact (fun () -> 
                        set.UnionWith added
                        set.ExceptWith removed
                    )
        )

    set |> ASet.withUpdater changer

let box (box : Box3d) =

    let randomColor = C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, 255uy)

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


let view = viewTrafo   () 
let proj = perspective () 

let vv = CameraView.lookAt (V3d(3.0,3.0, 3.0)) V3d.Zero V3d.OOI |> Mod.init
let pp = Frustum.perspective 10.0 0.1 100.0 1.0 |> Mod.init

let boxSg =
    let boxes = 
        boxes vv pp
            |> ASet.map (GridCell.box >> box)

    let attibutes =
        Map.ofList [
            DefaultSemantic.Positions, typeof<V3f>
            DefaultSemantic.Normals, typeof<V3f>
            DefaultSemantic.Colors, typeof<C4b>
        ]

    Sg.GeometrySet(boxes, IndexedGeometryMode.TriangleList, attibutes) :> ISg
        |> Sg.fillMode (Mod.constant FillMode.Line)
let camSg =
    box (Box3d.FromCenterAndSize(vv.GetValue().Location, V3d(0.1,0.1,0.1)))
        |> Sg.ofIndexedGeometry
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect                  
            DefaultSurfaces.constantColor C4f.Red  |> toEffect 
          ]
        |> Sg.fillMode (Mod.constant FillMode.Fill)


let sg = Sg.group' [boxSg; camSg]

let final =
    sg |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect                  
            DefaultSurfaces.vertexColor  |> toEffect 
           ]
        // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
        |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo ) 
        // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
        // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
        |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo    )
        |> Sg.trafo (Mod.constant (Trafo3d.Scale 0.1))
setSg final


let setThreshold v = transact (fun () -> Mod.change threshold v)
let setFov fov = transact (fun () -> Mod.change pp (Frustum.perspective fov pp.Value.near pp.Value.far 1.0))
let setFar far = transact (fun () -> Mod.change pp (Frustum.perspective (Frustum.horizontalFieldOfViewInDegrees pp.Value) pp.Value.near far 1.0))