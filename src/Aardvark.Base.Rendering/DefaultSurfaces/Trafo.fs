namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade
open DefaultSurfaceVertex

module Trafo = 
    
    let internal trafo (v : Vertex) =
        vertex {
            let wp = uniform.ModelTrafo * v.pos
            return {
                pos = uniform.ViewProjTrafo * wp
                wp = wp
                n = uniform.NormalMatrix * v.n
                b = uniform.NormalMatrix * v.b
                t = uniform.NormalMatrix * v.t
                c = v.c
                tc = v.tc
            }
        }
    
    let Effect = 
        toEffect trafo
