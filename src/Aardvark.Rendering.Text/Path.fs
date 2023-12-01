namespace Aardvark.Rendering.Text

open Aardvark.Base
open Aardvark.Rendering

[<RequireQualifiedAccess>]
type WindingRule =
    | NonZero
    | Positive
    | Negative
    | EvenOdd
    | AbsGreaterEqualTwo

type Path = private { bounds : Box2d; outline : PathSegment[] }

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
                [<Interpolation(InterpolationMode.Centroid); KLMKind>] klmKind : V4d
                [<ShapeTrafoR0>] tr0 : V4d
                [<ShapeTrafoR1>] tr1 : V4d
                [<PathColor>] color : V4d
                [<InstanceId; Interpolation(InterpolationMode.Flat)>] iid : int
                
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
            
        let offsets =
            [|
                V2d(0.7725118886316242, 0.7883251404963023)
                V2d(0.2868388261436524, 0.2507721982510638)
                V2d(0.7563800184247547, 0.25588371564343604)
                V2d(0.22473087378023382, 0.7162724551399098)
                V2d(0.4894844887720847, 0.9628606479378644)
                V2d(0.07185988197120041, 0.010886328495570696)
                V2d(0.5230029374367309, 0.5283938036989403)
                V2d(0.9527946972009785, 0.506092367067542)
                V2d(0.8256396153216994, 0.018310920557855437)
                V2d(0.5106754013547861, 0.2133290956201137)
                V2d(0.03461780820379934, 0.24064706484261567)
                V2d(0.9911584825114134, 0.7897931075891483)
                V2d(0.1922640348682304, 0.457478947949576)
                V2d(0.4329221922079425, 0.7147760153877587)
                V2d(0.27934121132194967, 0.9214674914765698)
                V2d(0.7177230393526967, 0.5751597544811645)
                //
                // V2d(0.7986169484292236, 0.010608453386841687)
                // V2d(0.2855507095172959, 0.4991263180765608)
                // V2d(0.7983998610664692, 0.48954740181904977)
                // V2d(0.2237664766378905, 0.9993042233065683)
                // V2d(0.5409630963388372, 0.25379167037792605)
                // V2d(0.011369496704124127, 0.7269507798346976)
                // V2d(0.04010761926379225, 0.2825056476644877)
                // V2d(0.5462796510887019, 0.7603523694022137)
                // V2d(0.4887047716852594, 0.9994008409430492)
                // V2d(0.28813772449412345, 0.7658774537418986)
                // V2d(0.5151390474583393, 0.5274298305148808)
                // V2d(0.27231129555639966, 0.24141392451682786)
                // V2d(0.8011096906397096, 0.2430986036295334)
                // V2d(0.7893492714704615, 0.7337433177993399)
                // V2d(0.025093250244428655, 0.9998996553213046)
                // V2d(0.07073406485377287, 0.5250728033828246)
                // V2d(0.6607055189488857, 0.8964269900193832)
                // V2d(0.6559404234110494, 0.6260828587940539)
                // V2d(0.6705576298694615, 0.3820244057019344)
                // V2d(0.18150034988536412, 0.3775386104292431)
                // V2d(0.35767516303790425, 0.08954114526291013)
                // V2d(0.09421346735430514, 0.8426167246766335)
                // V2d(0.6446525181546917, 0.09845125522611653)
                // V2d(0.9393073819794282, 0.15564939327185745)
                // V2d(0.39043638239192247, 0.38928656502078984)
                // V2d(0.9001069517175591, 0.8697130422737763)
                // V2d(0.3861795369260955, 0.6279533839624495)
                // V2d(0.9411079961940554, 0.40290209065863136)
                // V2d(0.14596857573931055, 0.6595315261204832)
                // V2d(0.36262511713227785, 0.9109534575956297)
                // V2d(0.9227041934277488, 0.6156371518551891)
                // V2d(0.08471160926459764, 0.13335171937458623)
            |] |> Array.map (fun v -> (2.0 * v - V2d.II))
            
        let samplesOffsets : V2d[] =
            Array.map (fun v -> v / 4.0) [|
                V2d(1,1); V2d(-1,-3); V2d(-3,2); V2d(4,1);
                V2d(-5,-2); V2d(2,5); V2d(5,3); V2d(3,-5);
                V2d(-2,6); V2d(0,-7); V2d(-4,-6); V2d(-6,4);
                V2d(-8,0); V2d(7,-4); V2d(6,7); V2d(-7,8)
            |]

            
        let pathVertexGS (t : Triangle<Vertex>) =
            triangle {
                
                let size = uniform.ViewportSize
                
                let cnt = 16
                for i in 0 .. cnt - 1 do
                    let o = 2.0 * offsets.[i] / V2d size
                    
                    
                    yield { t.P0 with p = t.P0.p + V4d(o.X * t.P0.p.W, o.Y * t.P0.p.W, 0.0, 0.0); color = V4d(t.P0.color.XYZ, t.P0.color.W / float cnt) }
                    yield { t.P1 with p = t.P1.p + V4d(o.X * t.P1.p.W, o.Y * t.P1.p.W, 0.0, 0.0); color = V4d(t.P1.color.XYZ, t.P1.color.W / float cnt) }
                    yield { t.P2 with p = t.P2.p + V4d(o.X * t.P2.p.W, o.Y * t.P2.p.W, 0.0, 0.0); color = V4d(t.P2.color.XYZ, t.P2.color.W / float cnt) }
                    restartStrip()
                
                
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
                let kind = v.klmKind.W //+ 0.001 * v.samplePos.X
   
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
                        // bezier3
                        let ci = v.klmKind.XYZ
                        let f = ci.X * ci.X * ci.X - ci.Y * ci.Z
                        if f > 0.0 then
                            discard()
                     
                     else
                         // solid
                        if v.klmKind.X > 0 then
                            let dx = ddx v.klmKind.X
                            let dy = ddy v.klmKind.X
                            
                            let step = -v.klmKind.X * V2d(dy, dx) / (dx*dx + dy*dy) |> Vec.length
                            if step < 10.0 then
                                color <- lerp color V4d.IOOI (step / 10.0)
                            // F = v.klmKind.X
                            
                            // F + a*dx + b*dy = 0
                            // a^2 + b^2 = min
                            
                            // a = -(F+b*dy)/dx
                            
                            
                            // (F+b*dy)^2/dx^2 + b^2 = min
                            
                            // (F+b*dy)^2 + dx^2*b^2 = min
                            // 2*(F+b*dy)*dy + 2*dx^2*b = 0
                            // 2*F*dy + 2*b*dy^2 + 2*b*dx^2 = 0
                            // b = -F*dy / (dx^2 * dy^2)
                            else
                                discard()
                else
                    if kind >= 1.5 && kind < 2.5 then
                        color <- V4d.IOOI
                    if kind >= 2.5 && kind < 3.5 then
                        color <- V4d.OOII
                    elif kind >= 3.5 && kind < 5.5 then
                        color <- V4d.OIOI
                    elif kind >= 5.5  then
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
        let overlay (v : Vertex) =
            fragment {
                let alpha = float (v.iid + 1) / 16.0
                return V4d(1.0, 1.0, 1.0, alpha)
            }
            


    let empty =
        { bounds = Box2d.Invalid; outline = [||] }

    /// create a path using a single segment
    let single (seg : PathSegment) =
        { bounds = PathSegment.bounds seg; outline = [| seg |] }

    /// creates a path using the given segments
    let ofSeq (segments : seq<PathSegment>) =
        let arr = Seq.toArray segments
        let bounds = arr |> Array.fold (fun l r -> Box.Union(l,PathSegment.bounds r)) Box2d.Invalid
        { bounds = bounds; outline = arr }

    /// creates a path using the given segments
    let ofList (segments : list<PathSegment>) =
        let arr = List.toArray segments
        let bounds = arr |> Array.fold (fun l r -> Box.Union(l,PathSegment.bounds r)) Box2d.Invalid
        { bounds = bounds; outline = arr }

    /// creates a path using the given segments
    let ofArray (segments : PathSegment[]) =
        let bounds = segments |> Array.fold (fun l r -> Box.Union(l,PathSegment.bounds r)) Box2d.Invalid
        { bounds = bounds; outline = segments }

    /// returns all path segments
    let toSeq (p : Path) =
        p.outline :> seq<_>

    /// returns all path segments
    let toList (p : Path) =
        p.outline |> Array.toList

    /// returns all path segments
    let toArray (p : Path) =
        p.outline |> Array.copy

    /// concatenates two paths
    let append (l : Path) (r : Path) =
        { bounds = Box.Union(l.bounds, r.bounds); outline = Array.append l.outline r.outline }

    /// concatenates a sequence paths
    let concat (l : seq<Path>) =
        let bounds = l |> Seq.fold (fun l r -> Box.Union(l, r.bounds)) Box2d.Invalid
        let arr = l |> Seq.collect toArray |> Seq.toArray
        { bounds = bounds; outline = arr }

    /// reverses the entrie path
    let reverse (p : Path) =
        { bounds = p.bounds; outline = p.outline |> Array.map PathSegment.reverse |> Array.rev }

    /// gets an axis-aligned bounding box for the path
    let bounds (p : Path) =
        p.outline |> Seq.map PathSegment.bounds |> Box2d

    /// gets the segment count for the path
    let count (p : Path) =
        p.outline.Length

    /// gets the i-th segment from the path
    let item (i : int) (p : Path) =
        p.outline.[i]

    /// applies the given transformation to all points used by the path
    let transform (f : V2d -> V2d) (p : Path) =
        p.outline |> Array.map (PathSegment.transform f) |> ofArray
        
    type PathBuilderState =
        {
            currentStart : Option<V2d>
            current : Option<V2d>
            segments : list<PathSegment>
        }

    type PathBuilder() =
        member x.Yield(()) =
            {
                currentStart = None
                current = None
                segments = []
            }

        [<CustomOperation("start")>]
        member x.Start(s : PathBuilderState, pt : V2d) = 
            { s with current = Some pt; currentStart = Some pt }
            
        [<CustomOperation("lineTo")>]
        member x.LineTo(s : PathBuilderState, p1 : V2d) = 
            match s.current with
                | Some p0 ->
                    match PathSegment.tryLine p0 p1 with
                        | Some seg -> 
                            { s with current = Some p1; segments = seg :: s.segments }
                        | None ->
                            s
                | None ->
                    failwith "cannot use lineTo without starting the path"

        [<CustomOperation("bezierTo")>]
        member x.BezierTo(s : PathBuilderState, pc : V2d, p1 : V2d) = 
            match s.current with
                | Some p0 ->
                    match PathSegment.tryBezier2 p0 pc p1 with
                        | Some seg -> 
                            { s with current = Some p1; segments = seg :: s.segments }
                        | None ->
                            s
                | None ->
                    failwith "cannot use lineTo without starting the path"
            
        [<CustomOperation("arc")>]
        member x.Arc(s : PathBuilderState, p1 : V2d, p2 : V2d) = 
            match s.current with
                | Some p0 ->
                    match PathSegment.tryArcSegment p0 p1 p2 with
                        | Some seg -> 
                            { s with current = Some p2; segments = seg :: s.segments }
                        | None ->
                            s
                | None ->
                    failwith "cannot use lineTo without starting the path"
                            
        [<CustomOperation("bezierTo3")>]
        member x.BezierTo3(s : PathBuilderState, pc0 : V2d, pc1 : V2d, p1 : V2d) = 
            match s.current with
                | Some p0 ->
                    match PathSegment.tryBezier3 p0 pc0 pc1 p1 with
                        | Some seg -> 
                            { s with current = Some p1; segments = seg :: s.segments }
                        | None ->
                            s
                | None ->
                    failwith "cannot use lineTo without starting the path"
                    
        [<CustomOperation("stop")>]
        member x.Stop(s : PathBuilderState) =
            { s with current = None; currentStart = None }
            
        [<CustomOperation("close")>]
        member x.CloseLine(s : PathBuilderState) =
            match s.current, s.currentStart with
                | Some current, Some start ->
                    let s = { s with current = None; currentStart = None }     
                    
                    match PathSegment.tryLine current start with
                        | Some seg -> { s with segments = seg :: s.segments }
                        | None -> s
                | _ ->
                    failwith "cannot close without starting the path"
      
        member x.Run(s : PathBuilderState) =
            ofList (List.rev s.segments)

    let build = PathBuilder()
   
    /// creates a geometry using the !!closed!! path which contains the left-hand-side of
    /// the outline.
    /// The returned geometry contains Positions and a 4-dimensional vector (KLMKind) describing the
    /// (k,l,m) coordinates for boundary triangles in its xyz components and
    /// the kind of the triangle (inner = 0, boundary = 1) in its w component
    let toGeometry (rule : WindingRule) (p : Path) =
        let rule =
            match rule with
            | WindingRule.Positive -> LibTessDotNet.Double.WindingRule.Positive
            | WindingRule.Negative -> LibTessDotNet.Double.WindingRule.Negative
            | WindingRule.NonZero -> LibTessDotNet.Double.WindingRule.NonZero
            | WindingRule.EvenOdd -> LibTessDotNet.Double.WindingRule.EvenOdd
            | WindingRule.AbsGreaterEqualTwo -> LibTessDotNet.Double.WindingRule.AbsGeqTwo

        Tessellator.toGeometry rule p.bounds p.outline
       
