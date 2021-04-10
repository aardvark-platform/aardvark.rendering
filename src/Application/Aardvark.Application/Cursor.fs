namespace Aardvark.Application

open Aardvark.Base

[<RequireQualifiedAccess>]
type Cursor =
    | None
    | Default
    | Arrow
    | Hand
    | HorizontalResize
    | VerticalResize
    | Text
    | Crosshair
    | Custom of PixImage<byte> * V2i


