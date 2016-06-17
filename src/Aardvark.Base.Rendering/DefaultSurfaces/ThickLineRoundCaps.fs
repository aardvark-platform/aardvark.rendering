namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.Quotations
open ThickLine

module ThickLineRoundCaps = 
    
    let thickLineRoundCaps (v : ThickLineVertex) =
        fragment {
            if v.lc.Y < 0.0 then
                let tc = v.lc / V2d(1.0, v.w)
                if tc.Length > 1.0 then discard()

            elif v.lc.Y >= 1.0 then
                let tc = (v.lc - V2d.OI) / V2d(1.0, v.w)
                if tc.Length > 1.0 then discard()


            return v.c
        }

    let Effect = 
        toEffect thickLineRoundCaps
