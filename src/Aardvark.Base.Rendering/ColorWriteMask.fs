namespace Aardvark.Base

open System

[<Flags>]
type ColorWriteMask =
    | Red = 0x1
    | Green = 0x2
    | Blue = 0x4
    | Alpha = 0x8
    | All = 0xf
    | None = 0x0