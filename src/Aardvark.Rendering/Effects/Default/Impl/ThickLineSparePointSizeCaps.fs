namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade
open ThickLine

module ThickLineSparePointSizeCaps = 

    let internal thickLineSparePointSizeCaps (v : ThickLineVertex) =
        fragment {
            let r = uniform.PointSize / uniform.LineWidth
            if v.lc.Y < 0.5f then
                let tc = v.lc / V2f(1.0f, v.w)
                if v.lc.Y < 0.0f || tc.Length < r then discard()

            else
                let tc = (v.lc - V2f.OI) / V2f(1.0f, v.w)
                if v.lc.Y > 1.0f || tc.Length < r then discard()


            return v.c
        }

    let Effect =
        toEffect thickLineSparePointSizeCaps