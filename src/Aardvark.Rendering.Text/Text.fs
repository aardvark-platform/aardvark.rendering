namespace Aardvark.Rendering.Text


open System
open System.Linq
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering


[<Struct>]
type ConcreteShape =
    {
        trafo       : M33d
        color       : C4b
        z           : int
        shape       : Shape
    }

    member x.bounds =
        x.shape.Path.bounds.Transformed(x.trafo)
        //Box2d.FromMinAndSize(b.Min + x.offset, b.Size * x.scale)


module ConcreteShape =

    type TrafoConverter () =
        static member ToM33d(v : M33d) : M33d = v
        static member ToM33d(v : M23d) : M33d = M33d.op_Explicit v
        static member ToM33d(v : Trafo2d) : M33d = v.Forward
        static member ToM33d(v : M44d) : M33d = v.UpperLeftM33()
        static member ToM33d(v : M33f) : M33d = M33d.op_Explicit v
        static member ToM33d(v : M23f) : M33d = M33d.op_Explicit v
        static member ToM33d(v : M44f) : M33d = M33d.op_Explicit v

    let inline private conv (d : ^d) (a : ^a) =
        ((^d or ^a) : (static member ToM33d : ^a -> M33d) (a))

    let ofPath (trafo : M33d) (color : C4b) (path : Path) =
        {
            trafo = trafo
            color = color
            z = 0
            shape = Shape path
        }

    let ofList (trafo : M33d) (color : C4b) (segments: list<PathSegment>) =
        ofPath trafo color (Path.ofList segments)

    let inline transform (trafo : ^a) (shape : ConcreteShape) =
        let t = conv Unchecked.defaultof<TrafoConverter> trafo
        { shape with
            trafo = t * shape.trafo
        }

    let inline trafo (shape : ConcreteShape) = shape.trafo
    let inline color (shape : ConcreteShape) = shape.color
    let inline shape (shape : ConcreteShape) = shape.shape
    let inline bounds (shape : ConcreteShape) = shape.bounds

    let fillRectangle (color : C4b) (bounds : Box2d) =
        let path =
            Path.build {
                start   (V2d(bounds.Min.X, bounds.Min.Y))
                lineTo  (V2d(bounds.Max.X, bounds.Min.Y))
                lineTo  (V2d(bounds.Max.X, bounds.Max.Y))
                lineTo  (V2d(bounds.Min.X, bounds.Max.Y))
                close
            }
        ofPath M33d.Identity color path

    let rectangle (color : C4b) (lineWidth : float) (bounds : Box2d) =
        let small   = bounds.ShrunkBy     (lineWidth / 2.0)
        let bounds  = bounds.EnlargedBy   (lineWidth / 2.0)

        let path =
            Path.build {
                start   (V2d(bounds.Min.X, bounds.Min.Y))
                lineTo  (V2d(bounds.Max.X, bounds.Min.Y))
                lineTo  (V2d(bounds.Max.X, bounds.Max.Y))
                lineTo  (V2d(bounds.Min.X, bounds.Max.Y))
                close
                
                start   (V2d(small.Min.X, small.Min.Y))
                lineTo  (V2d(small.Min.X, small.Max.Y))
                lineTo  (V2d(small.Max.X, small.Max.Y))
                lineTo  (V2d(small.Max.X, small.Min.Y))
                close

            }
        ofPath M33d.Identity color path


    let fillRoundedRectangle (color : C4b)  (radius : float) (bounds : Box2d) =
        if radius <= Constant.PositiveTinyValue then
            fillRectangle color bounds
        else
            let radius = min radius (0.5 * min bounds.SizeX bounds.SizeY)
            let pc00 = V2d(bounds.Min.X, bounds.Min.Y)
            let pc01 = V2d(bounds.Min.X, bounds.Max.Y)
            let pc10 = V2d(bounds.Max.X, bounds.Min.Y)
            let pc11 = V2d(bounds.Max.X, bounds.Max.Y)
        
            let rx = V2d(radius, 0.0)
            let ry = V2d(0.0, radius)

            let inline e c = Ellipse2d(c, rx, ry)

            let path = 
                Path.build {
                    start   (pc11 - ry)
                    lineTo  (pc10 + ry)
                    arc     pc10 (pc10 - rx)
                    lineTo  (pc00 + rx)
                    arc     pc00 (pc00 + ry)
                    lineTo  (pc01 - ry)
                    arc     pc01 (pc01 + rx)
                    lineTo  (pc11 - rx)
                    arc     pc11 (pc11 - ry)
                }

            ofPath M33d.Identity color path

    let roundedRectangle (color : C4b) (lineWidth : float) (radius : float) (bounds : Box2d) =
        if radius <= Constant.PositiveTinyValue then
            rectangle color lineWidth bounds
        else
            let outer = bounds.EnlargedBy(lineWidth / 2.0)
            let inner = bounds.ShrunkBy(lineWidth / 2.0)
            let ro = radius + 0.5 * lineWidth |> clamp 0.0 (0.5 * min outer.SizeX outer.SizeY)
            let ri = radius - 0.5 * lineWidth |> clamp 0.0 (0.5 * min inner.SizeX inner.SizeY)

            let po00 = V2d(outer.Min.X, outer.Min.Y)
            let po01 = V2d(outer.Min.X, outer.Max.Y)
            let po10 = V2d(outer.Max.X, outer.Min.Y)
            let po11 = V2d(outer.Max.X, outer.Max.Y)
            let rox = V2d(ro, 0.0)
            let roy = V2d(0.0, ro)

        
            let pi00 = V2d(inner.Min.X, inner.Min.Y)
            let pi01 = V2d(inner.Min.X, inner.Max.Y)
            let pi10 = V2d(inner.Max.X, inner.Min.Y)
            let pi11 = V2d(inner.Max.X, inner.Max.Y)
            let rix = V2d(ri, 0.0)
            let riy = V2d(0.0, ri)


            let inline e r c = Ellipse2d(c, V2d(r, 0.0), V2d(0.0, r))

            let path = 
                Path.build {
                    start   (po11 - roy)
                    lineTo  (po10 + roy)
                    arc     po10 (po10 - rox)// (e ro (po10 + roy - rox))
                    lineTo  (po00 + rox)
                    arc     po00 (po00 + roy) //(e ro (po00 + roy + rox))
                    lineTo  (po01 - roy)
                    arc     po01 (po01 + rox) // (e ro (po01 - roy + rox))
                    lineTo  (po11 - rox)
                    arc     po11 (po11 - roy) //(e ro (po11 - roy - rox))

                    start   (pi11 - riy)
                    arc     pi11 (pi11 - rix)// (e ri (pi11 - rix - riy))
                    lineTo  (pi01 + rix)
                    arc     pi01 (pi01 - riy) // (e ri (pi01 + rix - riy))
                    lineTo  (pi00 + riy) 
                    arc     pi00 (pi00 + rix) // (e ri (pi00 + rix + riy))
                    lineTo  (pi10 - rix) 
                    arc     pi10 (pi10 + riy)// (e ri (pi10 - rix + riy))
                    lineTo  (pi11 - riy)

                }

            ofPath M33d.Identity color path


    let fillEllipse (color : C4b) (e : Ellipse2d) =
        let a0 = e.Axis0
        let a1 = e.Axis1
        let z = a0.X * a1.Y - a0.Y * a1.X |> sign

        let e = 
            if z < 0 then Ellipse2d(e.Center, e.Axis0, -e.Axis1)
            else e
            
        let c = e.Center
        let x = e.Axis0
        let y = e.Axis1
        let path =
            Path.ofList [
                PathSegment.arc 0.0 -Constant.PiTimesTwo e
            ]
 
        ofPath M33d.Identity color path

    let ellipse (color : C4b) (lineWidth : float) (e : Ellipse2d) =
        let a0 = e.Axis0
        let a1 = e.Axis1
        let z = a0.X * a1.Y - a0.Y * a1.X |> sign
        let e = 
            if z < 0 then Ellipse2d(e.Center, e.Axis0, -e.Axis1)
            else e
            
        let l0 = e.Axis0.Length
        let l1 = e.Axis1.Length

        let fx = (1.0 - lineWidth / (2.0 * l0))
        let fy = (1.0 - lineWidth / (2.0 * l1))
        let outer = Ellipse2d(e.Center, e.Axis0 * (1.0 + lineWidth / (2.0 * l0)), e.Axis1 * (1.0 + lineWidth / (2.0 * l1)))

        if fx <= 1E-9 || fy <= 1E-9 then
            fillEllipse color outer
        else
            let inner = Ellipse2d(e.Center, e.Axis0 * fx, e.Axis1 * fy)

            let c = outer.Center
            let ox = outer.Axis0
            let oy = outer.Axis1
            let ix = inner.Axis0
            let iy = inner.Axis1
            let path =
            
                Path.ofList [

                    PathSegment.arc 0.0 -Constant.PiTimesTwo outer
                    PathSegment.arc 0.0 Constant.PiTimesTwo inner
                    //PathSegment.arc 0.0 -Constant.PiHalf outer
                    //PathSegment.arc -Constant.PiHalf -Constant.PiHalf outer
                    //PathSegment.arc -Constant.Pi -Constant.PiHalf outer
                    //PathSegment.arc -(1.5*Constant.Pi) -Constant.PiHalf outer

                    //PathSegment.arc 0.0 Constant.PiHalf inner
                    //PathSegment.arc Constant.PiHalf Constant.PiHalf inner
                    //PathSegment.arc Constant.Pi Constant.PiHalf inner
                    //PathSegment.arc (1.5*Constant.Pi) Constant.PiHalf inner
                ]

                //Path.build {
                //    start   (c + ox)
                //    arc     (c - oy) outer
                //    arc     (c - ox) outer
                //    arc     (c + oy) outer
                //    arc     (c + ox) outer

                
                //    start   (c + ix)
                //    arc     (c + iy) inner
                //    arc     (c - ix) inner
                //    arc     (c - iy) inner
                //    arc     (c + ix) inner

                //}
            ofPath M33d.Identity color path


    let fillCircle (color : C4b) (c : Circle2d) =
        fillEllipse color (Ellipse2d(c.Center, V2d(c.Radius, 0.0), V2d(0.0, c.Radius)))
        
    let circle (color : C4b) (lineWidth : float) (c : Circle2d) =
        ellipse color lineWidth (Ellipse2d(c.Center, V2d(c.Radius, 0.0), V2d(0.0, c.Radius)))





    let private fillPathAux (f : V2d -> V2d -> V2d -> PathSegment) (color : C4b) (points : list<V2d>) =
        let points = List.toArray points

        let poly = Polygon2d points
        let points = poly.WithoutMultiplePoints(1E-8).GetPointArray()


        if points.Length >= 3 then
            let getNormal (i : int) =
                let pl = points.[(i + points.Length - 1) % points.Length]
                let pc = points.[i]
                let pn = points.[(i+1) % points.Length]
                let e01 = pc - pl |> Vec.normalize
                let e12 = pn - pc |> Vec.normalize
                let d = e01 + e12 |> Vec.normalize
                V2d(-d.Y, d.X)

            let normals = Array.init points.Length getNormal
            
            let controlPoints =
                Array.init points.Length (fun i ->
                    let i1 = (i + 1) % normals.Length
                    let p0 = points.[i]
                    let n0 = normals.[i]
                    let p1 = points.[i1]
                    let n1 = normals.[i1]

                    let p0 = Plane2d(n0, p0)
                    let p1 = Plane2d(n1, p1)

                    let mutable pc = V2d.Zero
                    p0.Intersects(p1, &pc) |> ignore
                    
                    pc
                )

            Array.init points.Length (fun i ->
                let p0 = points.[i]
                let p1 = controlPoints.[i]
                let p2 = points.[(i + 1) % points.Length]

                f p0 p1 p2
            )
            |> Path.ofArray
            |> ofPath M33d.Identity color

        else
            Path.empty
            |> ofPath M33d.Identity color

    let private pathAux (f : V2d -> V2d -> V2d -> PathSegment) (color : C4b) (lineWidth : float) (points : list<V2d>) =
        let points = List.toArray points

        let poly = Polygon2d points
        let points = poly.WithoutMultiplePoints(1E-8).GetPointArray()


        if points.Length >= 3 then
            let normals = 
                Array.init points.Length (fun (i : int) ->
                    let pl = points.[(i + points.Length - 1) % points.Length]
                    let pc = points.[i]
                    let pn = points.[(i+1) % points.Length]
                    let e01 = pc - pl |> Vec.normalize
                    let e12 = pn - pc |> Vec.normalize
                    let d = e01 + e12 |> Vec.normalize
                    V2d(-d.Y, d.X)
                )
                
            let count = points.Length
            let pointsOuter = points |> Array.mapi (fun i p -> p + normals.[i] * lineWidth * 0.5)
            let pointsInner = points |> Array.mapi (fun i p -> p - normals.[i] * lineWidth * 0.5)
            let points = ()

            let controlPointsOuter =
                Array.init count (fun i ->
                    let i1 = (i + 1) % normals.Length
                    let p0 = pointsOuter.[i]
                    let n0 = normals.[i]
                    let p1 = pointsOuter.[i1]
                    let n1 = normals.[i1]
                    let p0 = Plane2d(n0, p0)
                    let p1 = Plane2d(n1, p1)
                    let mutable pc = V2d.Zero
                    p0.Intersects(p1, &pc) |> ignore
                    pc
                )

            let controlPointsInner =
                Array.init count (fun i ->
                    let i1 = (i + 1) % normals.Length
                    let p0 = pointsInner.[i]
                    let n0 = normals.[i]
                    let p1 = pointsInner.[i1]
                    let n1 = normals.[i1]
                    let p0 = Plane2d(n0, p0)
                    let p1 = Plane2d(n1, p1)
                    let mutable pc = V2d.Zero
                    p0.Intersects(p1, &pc) |> ignore
                    pc
                )

            let outer = 
                Array.init count (fun i ->
                    let p0 = pointsOuter.[i]
                    let p1 = controlPointsOuter.[i]
                    let p2 = pointsOuter.[(i + 1) % count]

                    f p0 p1 p2
                )

            let inner = 
                Array.init count (fun i ->
                    let i = count - 1 - i

                    let p0 = pointsInner.[i]
                    let p1 = controlPointsInner.[i]
                    let p2 = pointsInner.[(i + 1) % count]
                    f p2 p1 p0
                )


            Array.append outer inner
            |> Path.ofArray
            |> ofPath M33d.Identity color

        else
            Path.empty
            |> ofPath M33d.Identity color
             
    let fillBezierPath (color : C4b) (points : list<V2d>) = fillPathAux PathSegment.bezier2 color points
    let bezierPath (color : C4b) (lineWidth : float) (points : list<V2d>) = pathAux PathSegment.bezier2 color lineWidth points
    
    let fillArcPath (color : C4b) (points : list<V2d>) = fillPathAux PathSegment.arcSegment color points
    let arcPath (color : C4b) (lineWidth : float) (points : list<V2d>) = pathAux PathSegment.arcSegment color lineWidth points

type RenderStyle =
    | Normal
    | NoBoundary
    | Billboard

[<ReferenceEquality; NoComparison>]
type ShapeList =
    {
        bounds              : Box2d
        concreteShapes      : list<ConcreteShape>
        zRange              : Range1i
        renderTrafo         : Trafo3d
        flipViewDependent   : bool
        renderStyle         : RenderStyle
    }
    
    member x.trafos = x.concreteShapes |> List.map ConcreteShape.trafo
    member x.colors = x.concreteShapes |> List.map ConcreteShape.color
    member x.shapes = x.concreteShapes |> List.map ConcreteShape.shape

module ShapeList =

    let ofList (shapes : list<ConcreteShape>) =

        let bounds = shapes |> Seq.map (fun s -> s.bounds) |> Box2d

        let cx = bounds.Center.X

        let shapes = 
            shapes |> List.map (fun s -> 
                { s with 
                    trafo =
                        M33d.Translation(-cx, 0.0) *
                        s.trafo
                }
            )
        
        let range = shapes |> List.map (fun s -> s.z) |> Range1i

        {
            bounds = bounds
            concreteShapes = shapes
            renderTrafo = Trafo3d.Translation(cx, 0.0, 0.0)
            zRange = range
            flipViewDependent = false
            renderStyle = RenderStyle.Normal
        }

    let ofListWithRenderStyle (renderStyle:RenderStyle) (shapes : list<ConcreteShape>) = 

        let res = ofList shapes

        { res with renderStyle = renderStyle }    

    let prepend (shape : ConcreteShape) (r : ShapeList) =

        let shape = { shape with z = r.zRange.Min - 1 }

        let newBounds = Box.Union(shape.bounds, r.bounds)

        let oldCenter = r.bounds.Center
        let newCenter = newBounds.Center
        let shift = V2d(oldCenter.X - newCenter.X, 0.0)

        let shape = { shape with trafo = M33d.Translation(-newCenter.X, 0.0) * shape.trafo } //shape.offset - V2d(newCenter.X, 0.0) }
        let concreteShapes = r.concreteShapes |> List.map (fun s -> { s with trafo = M33d.Translation(shift) * s.trafo })

        {
            bounds = newBounds
            concreteShapes = shape :: concreteShapes
            renderTrafo = Trafo3d.Translation(newCenter.X, 0.0, 0.0)
            flipViewDependent = r.flipViewDependent
            zRange = Range1i(r.zRange.Min - 1, r.zRange.Max)
            renderStyle = r.renderStyle
        }
        
    let add (shape : ConcreteShape) (r : ShapeList) =
        
        let shape = { shape with z = r.zRange.Max + 1 }

        let newBounds = Box.Union(shape.bounds, r.bounds)

        let oldCenter = r.bounds.Center
        let newCenter = newBounds.Center
        let shift = V2d(oldCenter.X - newCenter.X, 0.0)

        let shape = { shape with trafo = M33d.Translation(-newCenter.X, 0.0) * shape.trafo }
        let concreteShapes = r.concreteShapes |> List.map (fun s -> { s with trafo = M33d.Translation(shift) * s.trafo })

        {
            bounds = newBounds
            concreteShapes = shape :: concreteShapes
            renderTrafo = Trafo3d.Translation(newCenter.X, 0.0, 0.0)
            flipViewDependent = r.flipViewDependent
            zRange = Range1i(r.zRange.Min, r.zRange.Max + 1)
            renderStyle = r.renderStyle
        }
        

type TextAlignment =
    | Left = 0
    | Right = 1
    | Center = 2

type TextConfig =
    {
        font                : Font
        color               : C4b
        align               : TextAlignment
        flipViewDependent   : bool
        renderStyle         : RenderStyle
    }
    static member Default =
        {
            font = Font "Consolas"
            color = C4b.White
            align = TextAlignment.Center
            flipViewDependent = true
            renderStyle = RenderStyle.Normal
        }


[<AbstractClass; Sealed; Extension>]
type Text private() =

    static let lineBreak = System.Text.RegularExpressions.Regex @"\r?\n|\r"

    [<Extension>]
    static member Layout(font : Font, color : C4b, align : TextAlignment, bounds : Box2d, content : string, renderStyle : RenderStyle) =
        let chars = List<float * Glyph>()
        //let offsets = List<V2d>()
        //let scales = List<V2d>()
        //let colors = List<C4b>()
        //let glyphs = List<Shape>()
        let concrete = List<ConcreteShape>()

        let mutable cy = 0.0
        let allLines = lineBreak.Split content

        let mutable realBounds = Box2d.Invalid

        for l in allLines do
            let mutable cx = 0.0
            let mutable last = '\n'
            chars.Clear()

            let mutable minX = 0.0
            let mutable i = 0
            for c in l do
                let kerning = font.GetKerning(last, c)
                match c with
                    | ' ' -> cx <- cx + font.Spacing
                    | '\t' -> cx <- cx + 4.0 + font.Spacing
                    | c ->
                        let g = font |> Font.glyph c
                        chars.Add(cx + g.Before + kerning, g)
                        cx <- cx + kerning + g.Advance
                        if i = 0 then minX <- g.Before + kerning
                        elif i = l.Length - 1 then cx <- cx + g.Before
                i <- i + 1
                last <- c
                
            let size = cx - minX

            
            let shift = 
                match align with
                    | TextAlignment.Left -> 0.0
                    | TextAlignment.Right -> bounds.Max.X - cx
                    | TextAlignment.Center -> bounds.Center.X - (cx + minX) / 2.0
                    | _ -> failwithf "[Text] bad align: %A" align

            let y = cy

            realBounds.ExtendBy(Box2d(V2d(minX + shift, y - font.Descent - font.InternalLeading), V2d(cx + shift, y + font.Ascent + font.InternalLeading)))
            for (x,g) in chars do
                let pos = V2d(shift + x,y)
                //realBounds.ExtendBy (Box2d()) //(g.Bounds.Translated(pos))
                //offsets.Add(pos)
                //scales.Add(V2d(1.0, 1.0))
                //colors.Add(color)
                //glyphs.Add(g)

                concrete.Add {
                    trafo = M33d.Translation(pos)
                    color = color
                    z = 0
                    shape = g
                }


            cy <- cy - font.LineHeight

        let realCenter = realBounds.Center.X
        
        let concrete =
            concrete |> CSharpList.toList |> List.map (fun shape ->
                { shape with trafo = M33d.Translation(-realCenter, 0.0) * shape.trafo } 
            )

        {
            bounds              = realBounds
            concreteShapes      = concrete
            //offsets             = offsets |> CSharpList.toList |> List.map (fun o -> V2d(o.X - realCenter, o.Y))
            //scales              = scales |> CSharpList.toList
            //colors              = colors |> CSharpList.toList
            //shapes              = glyphs |> CSharpList.toList
            renderTrafo         = Trafo3d.Translation(realCenter, 0.0, 0.0)
            flipViewDependent   = true
            zRange = Range1i(0,0)
            renderStyle = renderStyle
        }

    [<Extension>]
    static member Layout(font : Font, align : TextAlignment, bounds : Box2d, content : string) =
        Text.Layout(font, C4b.Black, align, bounds, content, RenderStyle.Normal)

    [<Extension>]
    static member Layout(font : Font, content : string) =
        Text.Layout(font, C4b.Black, TextAlignment.Left, Box2d(0.0, 0.0, Double.MaxValue, Double.MaxValue), content, RenderStyle.Normal)

    [<Extension>]
    static member Layout(font : Font, color : C4b, content : string) =
        Text.Layout(font, color, TextAlignment.Left, Box2d(0.0, 0.0, Double.MaxValue, Double.MaxValue), content, RenderStyle.Normal)
        
    [<Extension>]
    static member Layout(config : TextConfig, content : string) =
        let bounds =
            match config.align with
                | TextAlignment.Center -> Box2d(V2d(-1.0, 0.0), V2d(1.0, 0.0))
                | TextAlignment.Left -> Box2d(V2d(0.0, 0.0), V2d(1.0, 0.0))
                | _ -> Box2d(V2d(-1.0, 0.0), V2d(0.0, 0.0))

        { Text.Layout(config.font, config.color, config.align, bounds, content, config.renderStyle) with flipViewDependent = config.flipViewDependent }


