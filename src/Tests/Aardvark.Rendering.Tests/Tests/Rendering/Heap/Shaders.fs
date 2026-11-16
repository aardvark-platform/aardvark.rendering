namespace Aardvark.Rendering.Tests.Rendering.Heap

open Aardvark.Base
open Aardvark.Rendering
open FShade

// Effects shared by the heap probes. An ordinary effect: the per-draw model
// trafo and color are plain uniforms and the camera is read normally (the heap
// rewrite redirects only the per-draw uniforms).
module Shaders =
    type Vertex =
        { [<Position>] pos : V4f
          [<Color>]    c   : V4f
          [<Normal>]   n   : V3f }

    let shade (v : Vertex) =
        vertex {
            let m   : M44f = uniform?HeapModelTrafo
            let col : V4f  = uniform?HeapColor
            let vp  : M44f = uniform?ViewProjTrafo
            return { v with pos = vp * (m * v.pos); c = col; n = m.TransformDir v.n }
        }

    // vertex-color variant: passes the Colors attribute through (the adaptive
    // geometry probe flips the attribute value between a full array and a singleton).
    let shadeVtx (v : Vertex) =
        vertex {
            let m   : M44f = uniform?HeapModelTrafo
            let vp  : M44f = uniform?ViewProjTrafo
            return { v with pos = vp * (m * v.pos); n = m.TransformDir v.n }
        }

    let shadeFrag (v : Vertex) =
        fragment {
            let l  = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
            let nn = Vec.normalize v.n
            let d  = 0.25f + 0.75f * max 0.0f (Vec.dot nn l)
            return V4f(v.c.XYZ * d, 1.0f)
        }

    // Validation of the heap's double-precision storage path: requests a uniform at
    // double precision (V3d Tint). The heap must store and gather it as a real double
    // (HeapDataD, 2 words per scalar, 8-byte aligned), matching the classic UBO path
    // pixel for pixel. A non-trivial tint makes any gather, alignment or bit error
    // visible.
    let shadeFragTint (v : Vertex) =
        fragment {
            let l  = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
            let nn = Vec.normalize v.n
            let d  = 0.25f + 0.75f * max 0.0f (Vec.dot nn l)
            let t  : V3d = uniform?Tint
            return V4f(v.c.XYZ * d * V3f(float32 t.X, float32 t.Y, float32 t.Z), 1.0f)
        }

    // a second, distinct effect (rim-lit) -> lands in its own bucket
    let shadeFragRim (v : Vertex) =
        fragment {
            let nn  = Vec.normalize v.n
            let rim = pow (1.0f - abs nn.Z) 2.0f
            return V4f(v.c.XYZ * 0.35f + V3f.III * rim, 1.0f)
        }
