namespace Aardvark.Rendering.NanoVg

open System
open Aardvark.Base

type LineCap =
    | ButtCap
    | RoundCap
    | SquareCap

type LineJoin =
    | MiterJoin of float
    | RoundJoin
    | BevelJoin

[<Flags>]
type TextAlign =
    | Left      = 0x01
    | Center    = 0x02
    | Right     = 0x04
    | Top       = 0x08
    | Middle    = 0x10
    | Bottom    = 0x20
    | BaseLine  = 0x40
    | Block     = 0x80

type PathSegment =
    | MoveTo of target : V2d
    | LineTo of target : V2d
    | BezierTo of c0 : V2d * c1 : V2d * target : V2d
    | QuadraticTo of control : V2d * target : V2d
    | ArcTo of p1 : V2d * target : V2d * radius : float
    | ClosePath

type Path = list<PathSegment>

type Primitive =
    | Path of path : Path
    | Arc of center : V2d * r : float * angle : Range1d * direction : int
    | Rectangle of box : Box2d
    | RoundedRectangle of box : Box2d * radius : float
    | Ellipse of center : V2d * radius : V2d
    | Circle of center : V2d * radius : float
    
type PrimitiveMode =
    | FillPrimitive
    | StrokePrimitive   


type FontStyle =
    | Regular = 0
    | Bold = 1
    | Italic = 2

type Font =
    | FileFont of string
    | SystemFont of string * FontStyle