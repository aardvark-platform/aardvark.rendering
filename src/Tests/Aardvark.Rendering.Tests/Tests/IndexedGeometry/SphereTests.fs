namespace Aardvark.Rendering.Tests.IndexedGeometry

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Expecto

module Sphere =

    module private Cases =

        let private validateGeometry (sphere : Sphere3d) (level : int) (wireframe : bool) =
            let tessellation = max 3 level
            let horizontalSegments = 2 * tessellation
            let verticalSegments = tessellation
            let expectedVertexCount = horizontalSegments * verticalSegments + 2
            let expectedIndexCount =
                if wireframe then 8 * horizontalSegments * verticalSegments
                else 6 * horizontalSegments * verticalSegments
            let expectedMode =
                if wireframe then IndexedGeometryMode.LineList
                else IndexedGeometryMode.TriangleList
            let color = C4b(17uy, 63uy, 129uy, 255uy)
            let geometry =
                if wireframe then IndexedGeometryPrimitives.wireframePhiThetaSphere sphere level color
                else IndexedGeometryPrimitives.solidPhiThetaSphere sphere level color

            Expect.equal geometry.Mode expectedMode "Primitive mode"
            Expect.isTrue (geometry.IndexArray :? int[]) "Indices must remain Int32"

            let indices = geometry.IndexArray :?> int[]
            let positions = geometry.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
            let normals = geometry.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]
            let colors = geometry.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[]

            Expect.equal geometry.VertexCount expectedVertexCount "Vertex count"
            Expect.equal positions.Length expectedVertexCount "Position count"
            Expect.equal normals.Length expectedVertexCount "Normal count"
            Expect.equal colors.Length expectedVertexCount "Color count"
            Expect.equal indices.Length expectedIndexCount "Index count"
            Expect.equal geometry.FaceVertexCount expectedIndexCount "Face vertex count"
            Expect.isTrue (indices |> Array.forall (fun index -> index >= 0 && index < expectedVertexCount)) "Indices must stay in range"
            Expect.isTrue (colors |> Array.forall ((=) color)) "Vertex colors"

            let center = sphere.Center
            let radius = sphere.Radius
            let positionTolerance = max 1.0 radius * 2E-6
            let normalTolerance = 2E-6
            let southPole = center - radius * V3d.YAxis
            let northPole = center + radius * V3d.YAxis
            let toPosition index = V3d positions.[index]

            let near (a : V3d) (b : V3d) = (a - b).Length <= positionTolerance
            Expect.equal (positions |> Array.sumBy (fun p -> if near (V3d p) southPole then 1 else 0)) 1 "Exactly one south-pole vertex"
            Expect.equal (positions |> Array.sumBy (fun p -> if near (V3d p) northPole then 1 else 0)) 1 "Exactly one north-pole vertex"
            Expect.isTrue (near (toPosition 0) southPole) "First vertex must be the south pole"
            Expect.isTrue (near (toPosition (expectedVertexCount - 1)) northPole) "Last vertex must be the north pole"

            for index in 0 .. expectedVertexCount - 1 do
                let radial = toPosition index - center
                let normal = V3d normals.[index]
                Expect.isLessThanOrEqual (abs (radial.Length - radius)) positionTolerance $"Position {index} must lie on the sphere"
                Expect.isLessThanOrEqual (abs (normal.Length - 1.0)) normalTolerance $"Normal {index} must be unit length"
                Expect.isLessThanOrEqual (normal - radial / radius).Length normalTolerance $"Normal {index} must be radial"

            let latitudeStep = Constant.Pi / float (verticalSegments + 1)
            for ring in 0 .. verticalSegments - 1 do
                let latitude = float (ring + 1) * latitudeStep - Constant.PiHalf
                let expectedY = center.Y + radius * sin latitude
                let oppositeRing = verticalSegments - ring - 1

                for longitude in 0 .. horizontalSegments - 1 do
                    let index = 1 + ring * horizontalSegments + longitude
                    let oppositeIndex = 1 + oppositeRing * horizontalSegments + longitude
                    Expect.isGreaterThan (float positions.[index].Y) southPole.Y $"Ring {ring} must be above the south pole"
                    Expect.isLessThan (float positions.[index].Y) northPole.Y $"Ring {ring} must be below the north pole"
                    Expect.isLessThanOrEqual (abs (float positions.[index].Y - expectedY)) positionTolerance $"Ring {ring} latitude"
                    Expect.isLessThanOrEqual (abs (float positions.[index].Y + float positions.[oppositeIndex].Y - 2.0 * center.Y)) (2.0 * positionTolerance) $"Rings {ring} and {oppositeRing} must be symmetric"

            if wireframe then
                for edge in 0 .. indices.Length / 2 - 1 do
                    let p0 = toPosition indices.[2 * edge]
                    let p1 = toPosition indices.[2 * edge + 1]
                    Expect.isGreaterThan (p1 - p0).Length positionTolerance $"Wire segment {edge} must have nonzero length"
            else
                for triangle in 0 .. indices.Length / 3 - 1 do
                    let p0 = toPosition indices.[3 * triangle]
                    let p1 = toPosition indices.[3 * triangle + 1]
                    let p2 = toPosition indices.[3 * triangle + 2]
                    let faceNormal = Vec.cross (p1 - p0) (p2 - p0)
                    let centroidDirection = (p0 + p1 + p2) / 3.0 - center
                    Expect.isGreaterThan faceNormal.Length (positionTolerance * positionTolerance) $"Triangle {triangle} must be nondegenerate"
                    Expect.isGreaterThan (Vec.dot faceNormal centroidDirection) 0.0 $"Triangle {triangle} must face outward"

            geometry

        let clampedLow () =
            let sphere = Sphere3d(V3d.Zero, 1.0)
            let lowSolid = validateGeometry sphere -4 false
            let lowWire = validateGeometry sphere -4 true
            let minimumSolid = IndexedGeometryPrimitives.solidPhiThetaSphere sphere 3 (C4b(17uy, 63uy, 129uy, 255uy))
            let minimumWire = IndexedGeometryPrimitives.wireframePhiThetaSphere sphere 3 (C4b(17uy, 63uy, 129uy, 255uy))

            Expect.equal (lowSolid.IndexArray :?> int[]) (minimumSolid.IndexArray :?> int[]) "Solid level clamping indices"
            Expect.equal (lowSolid.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]) (minimumSolid.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]) "Solid level clamping positions"
            Expect.equal (lowWire.IndexArray :?> int[]) (minimumWire.IndexArray :?> int[]) "Wire level clamping indices"
            Expect.equal (lowWire.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]) (minimumWire.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]) "Wire level clamping positions"

        let translatedAndScaled() =
            let sphere = Sphere3d(V3d(1.25, -2.5, 3.75), 4.5)
            validateGeometry sphere 8 false |> ignore
            validateGeometry sphere 8 true |> ignore

    module private Subdivision =

        let private levels = [0; 1; 2; 5]
        let private builders = [
            "module", IndexedGeometryPrimitives.Sphere.wireframeSubdivisionSphere
            "alias",  IndexedGeometryPrimitives.wireframeSubdivisionSphere
        ]
        let private positions (g : IndexedGeometry) = g.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]
        let private normals (g : IndexedGeometry) = g.IndexedAttributes.[DefaultSemantic.Normals] :?> V3f[]
        let private coords (g : IndexedGeometry) = g.IndexedAttributes.[DefaultSemantic.DiffuseColorCoordinates] :?> V2f[]
        let private colors (g : IndexedGeometry) = g.IndexedAttributes.[DefaultSemantic.Colors] :?> C4b[]
        let private positionKey (p : V3f) = struct (p.X, p.Y, p.Z)
        let private edge a b =
            let a, b = positionKey a, positionKey b
            if a < b then struct (a, b) else struct (b, a)
        let private attributeKey p n (t : V2f) = struct (positionKey p, positionKey n, struct (t.X, t.Y))

        let private triangleEdges (g : IndexedGeometry) =
            let p = positions (g.ToNonIndexed())
            [ for i in 0 .. 3 .. p.Length - 1 do
                yield edge p.[i] p.[i + 1]
                yield edge p.[i + 1] p.[i + 2]
                yield edge p.[i + 2] p.[i] ] |> Set.ofList

        let private shape context level color (g : IndexedGeometry) =
            let count = 36 * (1 <<< level)
            Expect.equal g.Mode IndexedGeometryMode.LineList $"{context}: line mode"
            Expect.isNull g.IndexArray $"{context}: non-indexed layout"
            Expect.isTrue g.IsValid $"{context}: valid geometry"
            Expect.equal g.VertexCount count $"{context}: vertex count"
            Expect.equal g.FaceVertexCount count $"{context}: face vertex count"
            Expect.equal (positions g).Length count $"{context}: positions"
            Expect.equal (normals g).Length count $"{context}: normals"
            Expect.equal (coords g).Length count $"{context}: texture coordinates"
            Expect.equal (colors g) (Array.create count color) $"{context}: colors"

        let completeEdges() =
            for level in levels do
                for sphere in [Sphere3d(V3d.Zero, 1.0); Sphere3d(V3d(1.25, -2.5, 3.75), 4.5); Sphere3d(V3d(-8.0, 3.0, 0.5), 0.125)] do
                    for color in [C4b(17uy, 63uy, 129uy, 255uy); C4b(231uy, 41uy, 7uy, 93uy)] do
                        let solid = IndexedGeometryPrimitives.solidSubdivisionSphere sphere level color
                        let expected = triangleEdges solid
                        let sp, sn, st = positions solid, normals solid, coords solid
                        let attributes = Array.init sp.Length (fun i -> attributeKey sp.[i] sn.[i] st.[i]) |> Set.ofArray
                        for name, build in builders do
                            let context = $"level={level}, center={sphere.Center}, radius={sphere.Radius}, color={color}, builder={name}"
                            let wire = build sphere level color
                            shape context level color wire
                            let p, n, t = positions wire, normals wire, coords wire
                            let segments = Array.init (p.Length / 2) (fun i -> edge p.[2 * i] p.[2 * i + 1])
                            let actual = Set.ofArray segments
                            Expect.equal segments.Length (18 * (1 <<< level)) $"{context}: segment count"
                            Expect.equal actual.Count segments.Length $"{context}: every edge exactly once"
                            Expect.equal actual expected $"{context}: complete undirected triangle edge set"
                            for i in 0 .. segments.Length - 1 do
                                Expect.isGreaterThan (p.[2 * i + 1] - p.[2 * i]).Length 0.0f $"{context}, segment={i}: nonzero edge"
                            for i in 0 .. p.Length - 1 do
                                Expect.isTrue (attributes.Contains(attributeKey p.[i] n.[i] t.[i]))
                                    $"{context}, vertex={i}: position/normal/texture-coordinate alignment"
                                Expect.isLessThanOrEqual (abs ((V3d p.[i] - sphere.Center).Length - sphere.Radius)) (max 1.0 sphere.Radius * 2E-6)
                                    $"{context}, vertex={i}: transformed position"

        let zeroRadius() =
            let center = V3d(1.25, -2.5, 3.75)
            for level in levels do
                for name, build in builders do
                    let context = $"level={level}, builder={name}, radius=0"
                    let color = C4b(9uy, 101uy, 237uy, 43uy)
                    let reference = build (Sphere3d(V3d.Zero, 1.0)) level color
                    let collapsed = build (Sphere3d(center, 0.0)) level color
                    shape context level color collapsed
                    Expect.equal (positions collapsed) (Array.create (36 * (1 <<< level)) (V3f center)) $"{context}: collapsed positions"
                    Expect.equal (normals collapsed) (normals reference) $"{context}: unit normals retained"
                    Expect.equal (coords collapsed) (coords reference) $"{context}: texture coordinates retained"

        let cachedSolid() =
            for level in levels do
                let unit = SgPrimitives.Primitives.unitSphere level
                let p, n, t = positions unit, normals unit, coords unit
                let savedP, savedN, savedT = Array.copy p, Array.copy n, Array.copy t
                let indices, singles = unit.IndexArray, unit.SingleAttributes
                let sphere = Sphere3d(V3d(1.25, -2.5, 3.75), 4.5)
                let color = C4b(17uy, 63uy, 129uy, 255uy)
                let solid = IndexedGeometryPrimitives.solidSubdivisionSphere sphere level color
                let first = IndexedGeometryPrimitives.wireframeSubdivisionSphere sphere level color
                for repetition in 1 .. 4 do
                    for name, build in builders do
                        let context = $"level={level}, repetition={repetition}, builder={name}"
                        let next = build sphere level color
                        Expect.equal (positions next) (positions first) $"{context}: deterministic endpoints"
                        Expect.equal (normals next) (normals first) $"{context}: deterministic normals"
                        Expect.equal (coords next) (coords first) $"{context}: deterministic texture coordinates"
                        Expect.isTrue (obj.ReferenceEquals(normals next, normals first)) $"{context}: cached wire normals"
                        Expect.isTrue (obj.ReferenceEquals(coords next, coords first)) $"{context}: cached wire coordinates"
                        Expect.isFalse (obj.ReferenceEquals(positions next, positions first)) $"{context}: independent transformed positions"
                        Expect.isFalse (obj.ReferenceEquals(colors next, colors first)) $"{context}: independent colors"
                        let changed = build sphere level (C4b(231uy, 41uy, 7uy, 93uy))
                        Expect.equal (colors first) (Array.create first.VertexCount color) $"{context}: earlier colors unchanged"
                        Expect.isFalse (obj.ReferenceEquals(colors changed, colors first)) $"{context}: color storage isolation"
                    let current = SgPrimitives.Primitives.unitSphere level
                    let context = $"level={level}, repetition={repetition}"
                    Expect.isTrue (obj.ReferenceEquals(current, unit)) $"{context}: cached solid identity"
                    Expect.isTrue (obj.ReferenceEquals(positions current, p)) $"{context}: cached solid positions identity"
                    Expect.isTrue (obj.ReferenceEquals(normals current, n)) $"{context}: cached solid normals identity"
                    Expect.isTrue (obj.ReferenceEquals(coords current, t)) $"{context}: cached solid coordinates identity"
                    Expect.isTrue (obj.ReferenceEquals(current.IndexArray, indices)) $"{context}: cached indices identity"
                    Expect.isTrue (obj.ReferenceEquals(current.SingleAttributes, singles)) $"{context}: cached single attributes identity"
                    Expect.equal p savedP $"{context}: cached solid positions preserved"
                    Expect.equal n savedN $"{context}: cached solid normals preserved"
                    Expect.equal t savedT $"{context}: cached solid coordinates preserved"
                    for build in [IndexedGeometryPrimitives.Sphere.solidSubdivisionSphere; IndexedGeometryPrimitives.solidSubdivisionSphere] do
                        let next = build sphere level color
                        Expect.equal next.Mode IndexedGeometryMode.TriangleList $"{context}: solid mode"
                        Expect.equal (positions next) (positions solid) $"{context}: solid positions preserved"
                        Expect.equal (normals next) savedN $"{context}: solid normals preserved"
                        Expect.equal (coords next) savedT $"{context}: solid coordinates preserved"
                        Expect.equal next.VertexCount savedP.Length $"{context}: solid vertex count"

        let invalidLevels() =
            let rejection context action =
                try action(); failtestf "%s: negative level accepted" context
                with :? ArgumentException as error -> error.GetType(), error.ParamName, error.Message
            for level in [-1; -4; Int32.MinValue] do
                let context = $"level={level}"
                let expected = rejection context (fun () -> SgPrimitives.Primitives.unitSphere level |> ignore)
                for name, build in builders do
                    let actual = rejection $"{context}, builder={name}" (fun () -> build (Sphere3d(V3d.Zero, 1.0)) level C4b.White |> ignore)
                    Expect.equal actual expected $"{context}, builder={name}: invalid-level behavior"

    let tests (target: TestTarget) =
        [
            "Phi/theta sphere clamps low levels without malformed poles",       Cases.clampedLow
            "Phi/theta sphere has symmetric interior rings and valid topology", Cases.translatedAndScaled

            "Subdivision.Complete unique wire edges and aligned attributes", Subdivision.completeEdges
            "Subdivision.Zero-radius count compatibility",                   Subdivision.zeroRadius
            "Subdivision.Cached solid and deterministic wire templates",     Subdivision.cachedSolid
            "Subdivision.Invalid-level controls",                            Subdivision.invalidLevels
        ]
        |> prepareCasesCpu "Sphere" target