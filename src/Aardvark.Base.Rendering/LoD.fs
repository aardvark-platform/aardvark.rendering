#if COMPILED
namespace Aardvark.Base.Rendering
#else
#I @"../../bin/Debug"
#r "Aardvark.Base.dll"
#r "Aardvark.Base.FSharp.dll"
#r "Aardvark.Base.Rendering.dll"
open Aardvark.Base.Rendering
#endif
open Aardvark.Base

type GridCell =
    struct
        val mutable public Id : V3l
        val mutable public Exponent : int


        member x.Box =
            let size = pow 2.0 (float x.Exponent)
            let center = V3d(x.Id) * size
            Box3d.FromMinAndSize(center, V3d(size, size, size))

        member x.Children =
            let baseId = 2L * x.Id
            let exp = x.Exponent - 1
            [
                GridCell(baseId + V3l.OOO, exp)
                GridCell(baseId + V3l.OOI, exp)
                GridCell(baseId + V3l.OIO, exp)
                GridCell(baseId + V3l.OII, exp)
                GridCell(baseId + V3l.IOO, exp)
                GridCell(baseId + V3l.IOI, exp)
                GridCell(baseId + V3l.IIO, exp)
                GridCell(baseId + V3l.III, exp)
            ]

        member x.Parent =
            let fp = V3d.op_Explicit x.Id / 2.0

            let id =
                V3l(
                    (if fp.X < 0.0 then floor fp.X else ceil fp.X),
                    (if fp.Y < 0.0 then floor fp.Y else ceil fp.Y),
                    (if fp.Z < 0.0 then floor fp.Z else ceil fp.Z)
                )

            GridCell(id, x.Exponent + 1)

        override x.ToString() =
            sprintf "{ id = %A; exponent = %d; box = %A }" x.Id x.Exponent x.Box

        new(id : V3l, exp : int) = { Id = id; Exponent = exp }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GridCell =
    
    let inline parent (c : GridCell) = c.Parent
    let inline children (c : GridCell) = c.Children
    let inline box (c : GridCell) = c.Box

    type V3l with
        static member Floor(v : V3d) = V3l(floor v.X, floor v.Y, floor v.Z)
        static member Round(v : V3d) = V3l(round v.X, round v.Y, round v.Z)
        static member Ceil(v : V3d) = V3l(ceil v.X, ceil v.Y, ceil v.Z)

    let containingCells (b : Box3d) =
        let exp = Fun.Log2 b.Size.NormMax |> ceil
        let mutable size = pow 2.0 exp
        let mutable exp = int exp
        let mutable minId = (b.Min + 10.0 * Constant<float>.PositiveTinyValue) / size |> V3l.Floor
        let mutable maxId = (b.Max - 10.0 * Constant<float>.PositiveTinyValue) / size |> V3l.Floor
                                 
        [ for x in minId.X..maxId.X do
            for y in minId.Y..maxId.Y do
                for z in minId.Z..maxId.Z do
                    yield GridCell(V3l(x,y,z), exp)
        ]

    let inline px (c : GridCell) = GridCell(c.Id + V3l.IOO, c.Exponent)
    let inline py (c : GridCell) = GridCell(c.Id + V3l.OIO, c.Exponent)
    let inline pz (c : GridCell) = GridCell(c.Id + V3l.OOI, c.Exponent)
    let inline nx (c : GridCell) = GridCell(c.Id - V3l.IOO, c.Exponent)
    let inline ny (c : GridCell) = GridCell(c.Id - V3l.OIO, c.Exponent)
    let inline nz (c : GridCell) = GridCell(c.Id - V3l.OOI, c.Exponent)

    let viewVolumeCells (viewProj : Trafo3d) =
        Box3d(-V3d.III, V3d.III).ComputeCorners() 
            |> Array.map (fun p -> viewProj.Backward.TransformPosProj(p))
            |> Box3d
            |> containingCells

    let shouldSplit (decider : GridCell -> float -> float -> bool) (viewProj : Trafo3d) (b : GridCell) =
        
        let viewSpacePoints =
            b.Box.ComputeCorners()
                |> Array.map viewProj.Forward.TransformPosProj

        let poly =
            viewSpacePoints 
                |> Array.map Vec.xy
                |> Polygon2d

        let convexHull = 
            poly.ComputeConvexHullIndexPolygon().ToPolygon2d().ConvexClipped(Box2d(-V2d.II, V2d.II))

        let zrange = viewSpacePoints |> Array.map (fun v -> v.Z) |> Range1d

        let area = convexHull.ComputeArea()
        let distance = 1.0 + max 0.0 zrange.Min //max 0.001 (min (abs zrange.Min) (abs zrange.Max))


        

        //printfn "{ area = %A; distance = %A }" area distance
        //printfn "%A, %A -> %A" distance area f

        decider b area distance
        


    let raster (split : GridCell -> float -> float -> bool) (view : CameraView) (proj : Frustum) =
        let viewProj = CameraView.viewTrafo view * Frustum.projTrafo proj

        let rec recurse (current : list<GridCell>) =
            match current with
                | [] -> []
                | _ ->
                    let split, keep = 
                        current |> List.partition (shouldSplit split viewProj)

                    let nested = recurse (split |> List.collect children |> List.filter (fun c -> c.Box.IntersectsFrustum(viewProj.Forward)))
                    keep @ nested

        viewProj 
            |> viewVolumeCells 
            |> recurse
            |> List.filter (fun c -> c.Box.IntersectsFrustum(viewProj.Forward))



    let test() =
        let view = CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI
        let proj = Frustum.perspective 60.0 0.1 1000.0 1.0

        let decide _ (area : float) (distance : float) =
            let f = area / (distance * distance)
            f > 0.4

        let mutable cnt = 0
        let mutable bounds = Box3d.Invalid
        let cells = raster decide view proj

        let iter = 100
        let sw = System.Diagnostics.Stopwatch()
        sw.Start()
        for i in 1..iter do
            let cells = raster decide view proj
            ()
        sw.Stop()

        for c in cells do
            printfn "%A" c
            cnt <- cnt + 1
            bounds <- Box3d.Union(bounds, c.Box)

        printfn "took %.3f ms" (sw.Elapsed.TotalMilliseconds / float iter)
        printfn "count = %A" cnt
        printfn "box = %A" bounds



//module Lod =
//    
//    let getGridCells (view : CameraView) (proj : Frustum) =
//        














