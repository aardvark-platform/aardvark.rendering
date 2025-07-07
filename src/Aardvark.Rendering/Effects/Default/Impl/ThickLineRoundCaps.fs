namespace Aardvark.Rendering.Effects

open Aardvark.Base
open Aardvark.Rendering
open FShade
open ThickLine

module ThickLineRoundCaps = 
    
    let internal thickLineRoundCaps (v : ThickLineVertex) =
        fragment {
            if v.lc.Y < 0.0f then
                let tc = v.lc / V2f(1.0f, v.w)
                if tc.Length > 1.0f then discard()

            elif v.lc.Y >= 1.0f then
                let tc = (v.lc - V2f.OI) / V2f(1.0f, v.w)
                if tc.Length > 1.0f then discard()


            return v.c
        }

    let Effect = 
        toEffect thickLineRoundCaps
