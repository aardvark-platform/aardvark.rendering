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
        offset  : V2d
        scale   : V2d
        color   : C4b
        shape   : Shape
    }

    member x.bounds =
        let b = x.shape.Path.bounds
        Box2d.FromMinAndSize(b.Min + x.offset, b.Size * x.scale)


module ConcreteShape =

    let ofPath (offset : V2d) (scale : V2d) (color : C4b) (path : Path) =
        {
            offset = offset
            scale = scale
            color = color
            shape = Shape path
        }

    let inline offset (shape : ConcreteShape) = shape.offset
    let inline scale (shape : ConcreteShape) = shape.scale
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
        ofPath V2d.Zero V2d.II color path

        
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
        ofPath V2d.Zero V2d.II color path

    //let fillRoundedRectangle (color : C4b)  (radius : float) (bounds : Box2d) =
    //    let pc00 = V2d(bounds.Min.X, bounds.Min.Y)
    //    let pc01 = V2d(bounds.Min.X, bounds.Max.Y)
    //    let pc10 = V2d(bounds.Max.X, bounds.Min.Y)
    //    let pc11 = V2d(bounds.Max.X, bounds.Max.Y)
        
    //    let rx = V2d(radius, 0.0)
    //    let ry = V2d(0.0, radius)

    //    let path =
    //        Path.build {
    //            start       (pc00 + rx)
    //            lineTo      (pc10 - rx)
    //            arc         (pc10 + ry) (pc10 - rx + ry)
    //            lineTo      (pc11 - ry)
    //            arc         (pc11 - rx) (pc11 - rx - ry)
    //            lineTo      (pc01 + rx)
    //            arc         (pc01 - ry) (pc01 + rx - ry)
    //            lineTo      (pc00 + ry)
    //            arc         (pc00 + rx) (pc00 + rx + ry)
    //        }
    //    let path = Path.reverse path
    //    ofPath V2d.Zero V2d.II color path


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
            Path.build {
                start   (c + x)
                arc     (c - y) e
                arc     (c - x) e
                arc     (c + y) e
                arc     (c + x) e
            }
        ofPath V2d.Zero V2d.II color path

    //let ellipse (color : C4b) (lineWidth : float) (e : Ellipse2d) =
    //    let c = e.Center
    //    let xo = e.Axis0 + (e.Axis0.Normalized * lineWidth / 2.0)
    //    let yo = e.Axis1 + (e.Axis1.Normalized * lineWidth / 2.0)
    //    let xi = e.Axis0 - (e.Axis0.Normalized * lineWidth / 2.0)
    //    let yi = e.Axis1 - (e.Axis1.Normalized * lineWidth / 2.0)
    //    let path =
    //        Path.build {
    //            start       (c + xo)
    //            arcTo       (c + xo - yo) (c - yo)
    //            arcTo       (c - xo - yo) (c - xo)
    //            arcTo       (c - xo + yo) (c + yo)
    //            arcTo       (c + xo + yo) (c + xo)
   
    //            start       (c + xi)
    //            arcTo       (c + xi + yi) (c + yi)
    //            arcTo       (c - xi + yi) (c - xi)
    //            arcTo       (c - xi - yi) (c - yi)
    //            arcTo       (c + xi - yi) (c + xi)
    //        }
    //    ofPath V2d.Zero V2d.II color path


    //let roundedRectangle (color : C4b) (lineWidth : float)  (radius : float) (bounds : Box2d) =
    //    let path (radius : float) (bounds : Box2d) =
    //        let pc00 = V2d(bounds.Min.X, bounds.Min.Y)
    //        let pc01 = V2d(bounds.Min.X, bounds.Max.Y)
    //        let pc10 = V2d(bounds.Max.X, bounds.Min.Y)
    //        let pc11 = V2d(bounds.Max.X, bounds.Max.Y)
    //        let rx = V2d(radius, 0.0)
    //        let ry = V2d(0.0, radius)
    //        Path.build {
    //            start       (pc00 + rx)
    //            lineTo      (pc10 - rx)
    //            arcTo       pc10    (pc10 + ry)
    //            lineTo      (pc11 - ry)
    //            arcTo       pc11    (pc11 - rx)
    //            lineTo      (pc01 + rx)
    //            arcTo       pc01    (pc01 - ry)
    //            lineTo      (pc00 + ry)
    //            arcTo       pc00    (pc00 + rx)
    //        }

    //    let half = lineWidth / 2.0
        
    //    let outer = path (radius + half) (bounds.EnlargedBy(half))
    //    let inner = path (radius - half) (bounds.ShrunkBy(half))

    //    let path = Path.append (Path.reverse outer) inner
    //    ofPath V2d.Zero V2d.II color path

[<ReferenceEquality; NoComparison>]
type ShapeList =
    {
        bounds              : Box2d
        concreteShapes      : list<ConcreteShape>
        renderTrafo         : Trafo3d
        flipViewDependent   : bool
    }
    
    member x.offsets = x.concreteShapes |> List.map ConcreteShape.offset
    member x.scales = x.concreteShapes |> List.map ConcreteShape.scale
    member x.colors = x.concreteShapes |> List.map ConcreteShape.color
    member x.shapes = x.concreteShapes |> List.map ConcreteShape.shape


module ShapeList =

    let ofList (shapes : list<ConcreteShape>) =

        let bounds = shapes |> Seq.map (fun s -> s.bounds) |> Box2d

        let cx = bounds.Center.X

        let shapes = shapes |> List.map (fun s -> { s with offset = V2d(s.offset.X - cx, s.offset.Y)})
        
        {
            bounds = bounds
            concreteShapes = shapes
            renderTrafo = Trafo3d.Translation(cx, 0.0, 0.0)
            flipViewDependent = false
        }

    let add (shape : ConcreteShape) (r : ShapeList) =
        let newBounds = Box2d.Union(shape.bounds, r.bounds)

        let oldCenter = r.bounds.Center
        let newCenter = newBounds.Center
        let shift = V2d(oldCenter.X - newCenter.X, 0.0)

        let shape = { shape with offset = shape.offset - V2d(newCenter.X, 0.0) }
        let concreteShapes = r.concreteShapes |> List.map (fun s -> { s with offset = s.offset + shift })

        {
            bounds = newBounds
            concreteShapes = concreteShapes @ [shape]
            renderTrafo = Trafo3d.Translation(newCenter.X, 0.0, 0.0)
            flipViewDependent = r.flipViewDependent
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
    }
    static member Default =
        {
            font = Font "Consolas"
            color = C4b.White
            align = TextAlignment.Center
            flipViewDependent = true
        }


[<AbstractClass; Sealed; Extension>]
type Text private() =

    static let lineBreak = System.Text.RegularExpressions.Regex @"\r?\n|\r"

    [<Extension>]
    static member Layout(font : Font, color : C4b, align : TextAlignment, bounds : Box2d, content : string) =
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
                    offset = pos
                    scale = V2d.II
                    color = color
                    shape = g
                }


            cy <- cy - font.LineHeight

        let realCenter = realBounds.Center.X
        
        let concrete =
            concrete |> CSharpList.toList |> List.map (fun shape ->
                { shape with offset = V2d(shape.offset.X - realCenter, shape.offset.Y) }
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
        }

    [<Extension>]
    static member Layout(font : Font, align : TextAlignment, bounds : Box2d, content : string) =
        Text.Layout(font, C4b.Black, align, bounds, content)

    [<Extension>]
    static member Layout(font : Font, content : string) =
        Text.Layout(font, C4b.Black, TextAlignment.Left, Box2d(0.0, 0.0, Double.MaxValue, Double.MaxValue), content)

    [<Extension>]
    static member Layout(font : Font, color : C4b, content : string) =
        Text.Layout(font, color, TextAlignment.Left, Box2d(0.0, 0.0, Double.MaxValue, Double.MaxValue), content)
        
    [<Extension>]
    static member Layout(config : TextConfig, content : string) =
        let bounds =
            match config.align with
                | TextAlignment.Center -> Box2d(V2d(-1.0, 0.0), V2d(1.0, 0.0))
                | TextAlignment.Left -> Box2d(V2d(0.0, 0.0), V2d(1.0, 0.0))
                | _ -> Box2d(V2d(-1.0, 0.0), V2d(0.0, 0.0))

        { Text.Layout(config.font, config.color, config.align, bounds, content) with flipViewDependent = config.flipViewDependent }


