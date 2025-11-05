namespace Aardvark.Application

open Aardvark.Base

[<RequireQualifiedAccess>]
type Cursor =
    | None
    | Default
    | Arrow
    | Hand
    | ResizeH
    | ResizeV
    | ResizeNESW
    | ResizeNWSE
    | ResizeAll
    | NotAllowed
    | Wait
    | Text
    | Crosshair
    | Custom of PixImage<byte> * V2i


