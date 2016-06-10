namespace Aardvark.Base


[<AutoOpen>]
module private Utils =
    let inline div2Floor (v : int64) =
        if v % 2L = 0L then v / 2L
        else (v - 1L) / 2L

    let inline div2Ceil (v : int64) =
        if v % 2L = 0L then v / 2L
        else (v + 1L) / 2L


    let inline floor3d (v : V3d) =
        V3l(v.X |> floor |> int64, v.Y |> floor |> int64, v.Z |> floor |> int64)

[<CustomEquality; NoComparison>]
type GridCell =
    struct
        val mutable public X : int64
        val mutable public Y : int64
        val mutable public Z : int64
        val mutable public Exp : int

        static member Containing (box : Box3d) =
            let s = box.Size
            let e = s.NormMax |> Fun.Log2 |> ceil |> int

            let size = pown 2.0 e

            let shift = 
                let ehalf = floor (float (e + 1) * 0.5) |> int
                -((pown 4.0 ehalf) - 1.0) / 3.0

            let i = (box.Min - shift) / size |> floor3d
            let mutable cell = GridCell(i.X, i.Y, i.Z, e)

            while not (cell.Contains box) do
                cell <- cell.Parent

            cell

        member x.BoundingBox =
            let size = pown 2.0 x.Exp

            let shift = 
                let ehalf = floor (float (x.Exp + 1) * 0.5) |> int
                let shift = -((pown 4.0 ehalf) - 1.0) / 3.0
                shift / size

            Box3d(
                (shift + float x.X) * size          |> float,
                (shift + float x.Y) * size          |> float,
                (shift + float x.Z) * size          |> float,
                (shift + float x.X + 1.0) * size    |> float,
                (shift + float x.Y + 1.0) * size    |> float,
                (shift + float x.Z + 1.0) * size    |> float
            )

        member x.Center =
            let size = pown 2.0 x.Exp

            let shift = 
                let ehalf = floor (float (x.Exp + 1) * 0.5) |> int
                let shift = -((pown 4.0 ehalf) - 1.0) / 3.0
                shift / size

            V3d(
                (shift + float x.X + 0.5) * size    |> float,
                (shift + float x.Y + 0.5) * size    |> float,
                (shift + float x.Z + 0.5) * size    |> float
            )          

        member x.Contains (p : V3d) =
            x.BoundingBox.Contains p

        member x.Contains (p : Box3d) =
            x.BoundingBox.Contains p

        member x.Parent =
            if x.Exp % 2 = 0 then GridCell(div2Ceil x.X, div2Ceil x.Y, div2Ceil x.Z, x.Exp + 1)
            else GridCell(div2Floor x.X, div2Floor x.Y, div2Floor x.Z, x.Exp + 1)

        member x.IndexInParent =
            if x.Exp % 2 = 0 then 
                ((abs(int x.X + 1) % 2) <<< 2) |||
                ((abs(int x.Y + 1) % 2) <<< 1) |||
                ((abs(int x.Z + 1) % 2) <<< 0)
            else
                ((abs(int x.X) % 2) <<< 2) |||
                ((abs(int x.Y) % 2) <<< 1) |||
                ((abs(int x.Z) % 2) <<< 0)
                    

        member x.Children =
            let e = x.Exp - 1
            let l = if x.Exp % 2 = 0 then 0L else -1L
            let h = if x.Exp % 2 = 0 then 1L else 0L

            let z = x.Z * 2L
            let y = x.Y * 2L
            let x = x.X * 2L
            [|
                GridCell(x+l,    y+l,    z+l,    e)
                GridCell(x+l,    y+l,    z+h,    e)
                GridCell(x+l,    y+h,    z+l,    e)
                GridCell(x+l,    y+h,    z+h,    e)
                GridCell(x+h,    y+l,    z+l,    e)
                GridCell(x+h,    y+l,    z+h,    e)
                GridCell(x+h,    y+h,    z+l,    e)
                GridCell(x+h,    y+h,    z+h,    e)
            |]

        member x.GetChild (index : int) =
            let xc = (index >>> 2) &&& 0x1 |> int64
            let yc = (index >>> 1) &&& 0x1 |> int64
            let zc = (index >>> 0) &&& 0x1 |> int64
            let l = if x.Exp % 2 = 0 then 0L else -1L

            GridCell(2L * x.X + xc + l, 2L * x.Y + yc + l, 2L * x.Z + zc + l, x.Exp - 1)

        member x.Index = V3l(x.X, x.Y, x.Z)

        override x.ToString() =
            sprintf "{ Index = (%d, %d, %d); Exp = %d }" x.X x.Y x.Z x.Exp

        override x.GetHashCode() =
            HashCode.Combine(x.X.GetHashCode(), x.Y.GetHashCode(), x.Z.GetHashCode(), x.Exp)

        override x.Equals o =
            match o with
                | :? GridCell as o -> x.X = o.X && x.Y = o.Y && x.Z = o.Z && x.Exp = o.Exp
                | _ -> false

        new(x,y,z,e) = { X = x; Y = y; Z = z; Exp = e }
        new(x : int,y : int,z : int,e : int) = { X = int64 x; Y = int64 y; Z = int64 z; Exp = e }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module GridCell =
    let inline ofBox (b : Box3d) = GridCell.Containing b
    let inline parent (c : GridCell) = c.Parent
    let inline children (c : GridCell) = c.Children
    let inline child (i : int) (c : GridCell) = c.GetChild i
    let inline bounds (c : GridCell) = c.BoundingBox