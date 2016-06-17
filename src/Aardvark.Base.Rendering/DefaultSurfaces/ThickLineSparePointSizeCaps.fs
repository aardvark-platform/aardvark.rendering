namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open ThickLine

module ThickLineSparePointSizeCaps = 

    let thickLineSparePointSizeCaps (v : ThickLineVertex) =
        fragment {
            let r = uniform.PointSize / uniform.LineWidth
            if v.lc.Y < 0.5 then
                let tc = v.lc / V2d(1.0, v.w)
                if v.lc.Y < 0.0 || tc.Length < r then discard()

            else
                let tc = (v.lc - V2d.OI) / V2d(1.0, v.w)
                if v.lc.Y > 1.0 || tc.Length < r then discard()


            return v.c
        }

    let Effect =
        toEffect thickLineSparePointSizeCaps