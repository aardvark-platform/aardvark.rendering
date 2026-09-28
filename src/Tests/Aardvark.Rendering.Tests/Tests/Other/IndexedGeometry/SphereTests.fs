namespace Aardvark.Rendering.Tests.IndexedGeometry

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Expecto

module Sphere =

    module Cases =

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

        let clampedLow =
            test "Phi/theta sphere clamps low levels without malformed poles" {
                let sphere = Sphere3d(V3d.Zero, 1.0)
                let lowSolid = validateGeometry sphere -4 false
                let lowWire = validateGeometry sphere -4 true
                let minimumSolid = IndexedGeometryPrimitives.solidPhiThetaSphere sphere 3 (C4b(17uy, 63uy, 129uy, 255uy))
                let minimumWire = IndexedGeometryPrimitives.wireframePhiThetaSphere sphere 3 (C4b(17uy, 63uy, 129uy, 255uy))

                Expect.equal (lowSolid.IndexArray :?> int[]) (minimumSolid.IndexArray :?> int[]) "Solid level clamping indices"
                Expect.equal (lowSolid.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]) (minimumSolid.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]) "Solid level clamping positions"
                Expect.equal (lowWire.IndexArray :?> int[]) (minimumWire.IndexArray :?> int[]) "Wire level clamping indices"
                Expect.equal (lowWire.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]) (minimumWire.IndexedAttributes.[DefaultSemantic.Positions] :?> V3f[]) "Wire level clamping positions"
            }

        let translatedAndScaled =
            test "Phi/theta sphere has symmetric interior rings and valid topology" {
                let sphere = Sphere3d(V3d(1.25, -2.5, 3.75), 4.5)
                validateGeometry sphere 8 false |> ignore
                validateGeometry sphere 8 true |> ignore
            }

    let tests =
        testList "Sphere" [
            Cases.clampedLow
            Cases.translatedAndScaled
        ]