namespace Aardvark.Rendering.Text

open Aardvark.Base
open Aardvark.Rendering

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Path =

    module Attributes =
        let KLMKind = Symbol.Create "KLMKind"
        let ShapeTrafoR0 = Symbol.Create "ShapeTrafoR0"
        let ShapeTrafoR1 = Symbol.Create "ShapeTrafoR1"
        let PathColor = Symbol.Create "PathColor"
        let TrafoOffsetAndScale = Symbol.Create "PathTrafoOffsetAndScale"

    type KLMKindAttribute() = inherit FShade.SemanticAttribute(Attributes.KLMKind |> string)
    type ShapeTrafoR0Attribute() = inherit FShade.SemanticAttribute(Attributes.ShapeTrafoR0 |> string)
    type ShapeTrafoR1Attribute() = inherit FShade.SemanticAttribute(Attributes.ShapeTrafoR1 |> string)
    type PathColorAttribute() = inherit FShade.SemanticAttribute(Attributes.PathColor |> string)
    type TrafoOffsetAndScaleAttribute() = inherit FShade.SemanticAttribute(Attributes.TrafoOffsetAndScale |> string)
    type KindAttribute() = inherit FShade.SemanticAttribute("Kind")
    type KLMAttribute() = inherit FShade.SemanticAttribute("KLM")
    type DepthLayerAttribute() = inherit FShade.SemanticAttribute("DepthLayer")

    [<ReflectedDefinition>]
    module Shader =
        open FShade

        type UniformScope with
            member x.FillGlyphs : bool = uniform?FillGlyphs
            member x.Antialias : bool = uniform?Antialias
            member x.BoundaryColor : V4d = uniform?BoundaryColor
            member x.DepthBias : float = uniform?DepthBias

        type Vertex =
            {
                [<Position>] p : V4d
                [<Interpolation(InterpolationMode.Sample); KLMKind>] klmKind : V4d
                [<ShapeTrafoR0>] tr0 : V4d
                [<ShapeTrafoR1>] tr1 : V4d
                [<PathColor>] color : V4d

                [<SamplePosition>] samplePos : V2d

                [<DepthLayer>] layer : float


                [<TrafoOffsetAndScale>] instanceTrafo : M34d
            }

        type VertexNoSampleShading =
            {
                [<Position>] p : V4d
                [<KLMKind>] klmKind : V4d
                [<ShapeTrafoR0>] tr0 : V4d
                [<ShapeTrafoR1>] tr1 : V4d
                [<PathColor>] color : V4d

                [<SamplePosition>] samplePos : V2d

                [<DepthLayer>] layer : float


                [<TrafoOffsetAndScale>] instanceTrafo : M34d
            }

        let eps = 0.00001
        [<Inline>]
        let keepsWinding (isOrtho : bool) (t : M44d) =
            if isOrtho then
                t.M00 > 0.0
            else
                let c = V3d(t.M03, t.M13, t.M23)
                let z = V3d(t.M02, t.M12, t.M22)
                Vec.dot c z < 0.0

        [<Inline>]
        let isOrtho (proj : M44d) =
            abs proj.M30 < eps &&
            abs proj.M31 < eps &&
            abs proj.M32 < eps

        let pathVertex (v : Vertex) =
            vertex {
                let trafo = uniform.ModelViewTrafo

                let mutable p = V4d.Zero

                let flip = v.tr0.W < 0.0
                let pm =
                    V2d(
                        Vec.dot v.tr0.XYZ (V3d(v.p.XY, 1.0)),
                        Vec.dot v.tr1.XYZ (V3d(v.p.XY, 1.0))
                    )

                if flip then
                    if keepsWinding (isOrtho uniform.ProjTrafo) trafo then
                        p <- trafo * V4d( pm.X, pm.Y, v.p.Z, v.p.W)
                    else
                        p <- trafo * V4d(-pm.X, pm.Y, v.p.Z, v.p.W)
                else
                    p <- trafo * V4d(pm.X, pm.Y, v.p.Z, v.p.W)

                return {
                    v with
                        p = uniform.ProjTrafo * p
                        //kind = v.klmKind.W
                        layer = 0.0
                        //klm = v.klmKind.XYZ
                        color = v.color
                    }
            }

        let pathVertexInstanced (v : Vertex) =
            vertex {
                let instanceTrafo = M44d.op_Explicit v.instanceTrafo //M44d.FromRows(v.instanceTrafo.R0, v.instanceTrafo.R1, v.instanceTrafo.R2, V4d.OOOI)
                let trafo = uniform.ModelViewTrafo * instanceTrafo

                let flip = v.tr0.W < 0.0
                let pm =
                    V2d(
                        Vec.dot v.tr0.XYZ (V3d(v.p.XY, 1.0)),
                        Vec.dot v.tr1.XYZ (V3d(v.p.XY, 1.0))
                    )

                let mutable p = V4d.Zero

                if flip then
                    if keepsWinding (isOrtho uniform.ProjTrafo) trafo then
                        p <- trafo * V4d( pm.X, pm.Y, v.p.Z, v.p.W)
                    else
                        p <- trafo * V4d(-pm.X, pm.Y, v.p.Z, v.p.W)
                else
                    p <- trafo * V4d(pm.X, pm.Y, v.p.Z, v.p.W)

                return {
                    v with
                        p = uniform.ProjTrafo * p
                        //kind = v.klmKind.W
                        layer = v.color.W
                        //klm = v.klmKind.XYZ
                        color = V4d(v.color.XYZ, 1.0)
                }
            }

        let pathVertexBillboard (v : Vertex) =
            vertex {
                let trafo = uniform.ModelViewTrafo

                let mvi = trafo.Transposed
                let right = mvi.C0.XYZ |> Vec.normalize
                let up = mvi.C1.XYZ |> Vec.normalize

                let pm =
                    V2d(
                        Vec.dot v.tr0.XYZ (V3d(v.p.XY, 1.0)),
                        Vec.dot v.tr1.XYZ (V3d(v.p.XY, 1.0))
                    )

                let mutable p = V4d.Zero


                let pm = right * pm.X + up * pm.Y + V3d(0.0, 0.0, v.p.Z)

                p <- trafo * V4d(pm, 1.0)

                return {
                    v with
                        p = uniform.ProjTrafo * p
                        layer = 0.0
                        color = v.color
                    }
            }

        let pathVertexInstancedBillboard (v : Vertex) =
            vertex {
                let instanceTrafo = M44d.op_Explicit v.instanceTrafo
                let trafo = uniform.ModelViewTrafo * instanceTrafo

                let mvi = trafo.Transposed
                let right = mvi.C0.XYZ |> Vec.normalize
                let up = mvi.C1.XYZ |> Vec.normalize



                let flip = v.tr0.W < 0.0
                let pm =
                    V2d(
                        Vec.dot v.tr0.XYZ (V3d(v.p.XY, 1.0)),
                        Vec.dot v.tr1.XYZ (V3d(v.p.XY, 1.0))
                    )

                let p = trafo * V4d(right * pm.X + up * pm.Y + V3d(0.0, 0.0, v.p.Z), v.p.W)

                return {
                    v with
                        p = uniform.ProjTrafo * p
                        layer = v.color.W
                        color = V4d(v.color.XYZ, 1.0)
                }
            }

        let depthBiasVs(v : Vertex) =
            vertex {
                let bias = 255.0 * v.layer * uniform.DepthBias
                let p = v.p - V4d(0.0, 0.0, bias, 0.0)
                return { v with p = p }
            }

        let pathFragment(v : Vertex) =
            fragment {
                let kind = v.klmKind.W + 0.001 * v.samplePos.X

                let mutable color = v.color

                if uniform.FillGlyphs then
                    if kind > 1.5 && kind < 3.5 then
                        // bezier2
                        let ci = v.klmKind.XYZ
                        let f = (ci.X * ci.X - ci.Y) * ci.Z
                        if f > 0.0 then
                            discard()

                    elif kind > 3.5 && kind < 5.5 then
                        // arc
                        let ci = v.klmKind.XYZ
                        let f = ((ci.X * ci.X + ci.Y*ci.Y) - 1.0) * ci.Z

                        if f > 0.0 then
                            discard()

                     elif kind > 5.5  then
                        let ci = v.klmKind.XYZ
                        let f = ci.X * ci.X * ci.X - ci.Y * ci.Z
                        if f > 0.0 then
                            discard()
                else
                    if kind > 1.5 && kind < 3.5 then
                        color <- V4d.IOOI
                    elif kind > 3.5 && kind < 5.5 then
                        color <- V4d.OIOI
                    elif kind > 5.5  then
                        color <- V4d.OOII

                return color

            }

        let pathFragmentNoSampleShading(v : VertexNoSampleShading) =
            fragment {
                let kind = v.klmKind.W

                let mutable color = v.color

                if uniform.FillGlyphs then
                    if kind > 1.5 && kind < 3.5 then
                        // bezier2
                        let ci = v.klmKind.XYZ
                        let f = (ci.X * ci.X - ci.Y) * ci.Z
                        if f > 0.0 then
                            discard()

                    elif kind > 3.5 && kind < 5.5 then
                        // arc
                        let ci = v.klmKind.XYZ
                        let f = ((ci.X * ci.X + ci.Y*ci.Y) - 1.0) * ci.Z

                        if f > 0.0 then
                            discard()

                     elif kind > 5.5  then
                        let ci = v.klmKind.XYZ
                        let f = ci.X * ci.X * ci.X - ci.Y * ci.Z
                        if f > 0.0 then
                            discard()
                else
                    if kind > 1.5 && kind < 3.5 then
                        color <- V4d.IOOI
                    elif kind > 3.5 && kind < 5.5 then
                        color <- V4d.OIOI
                    elif kind > 5.5  then
                        color <- V4d.OOII

                return color

            }

        let boundaryVertex (v : Vertex) =
            vertex {
                return { v with p = uniform.ModelViewProjTrafo * v.p }
            }

        let boundary (v : Vertex) =
            fragment {
                return uniform.BoundaryColor
            }