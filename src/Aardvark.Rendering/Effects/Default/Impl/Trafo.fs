namespace Aardvark.Base.Rendering.Effects

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade

module Trafo = 
    
    let internal trafo (v : Vertex) =
        vertex {
            let wp = uniform.ModelTrafo * v.pos
            return {
                pos = uniform.ViewProjTrafo * wp
                wp = wp
                n = uniform.ModelTrafoInv.TransposedTransformDir v.n
                b = uniform.ModelTrafo.TransformDir v.b
                t = uniform.ModelTrafo.TransformDir v.t
                c = v.c
                tc = v.tc
            }
        }
    
    let Effect = 
        toEffect trafo
