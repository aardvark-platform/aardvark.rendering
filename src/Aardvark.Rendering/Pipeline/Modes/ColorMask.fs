﻿namespace Aardvark.Rendering

open System
open Aardvark.Base

[<Flags>]
type ColorMask =
    | Red   = 0x1
    | Green = 0x2
    | Blue  = 0x4
    | Alpha = 0x8
    | None  = 0x0
    | All   = 0xF

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ColorMask =
    let r = ColorMask.Red
    let g = ColorMask.Green
    let b = ColorMask.Blue
    let a = ColorMask.Alpha
    let rgba = ColorMask.All
    let rgb = r ||| g ||| b

    let enable (enabled : bool) =
        if enabled then ColorMask.All else ColorMask.None

    let ofWriteSet (enabled : Set<Symbol>) =
        enabled |> Set.map (fun s -> s, ColorMask.All) |> Map.ofSeq