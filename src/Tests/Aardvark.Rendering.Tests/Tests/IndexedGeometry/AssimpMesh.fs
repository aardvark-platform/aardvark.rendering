namespace Aardvark.Rendering.Tests.IndexedGeometry

open System
open System.Numerics
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph.Assimp
open Expecto

module AssimpMesh =

    module private Cases =

        let private topologies =
            [ Assimp.PrimitiveType.Point, IndexedGeometryMode.PointList, 1
              Assimp.PrimitiveType.Line, IndexedGeometryMode.LineList, 2
              Assimp.PrimitiveType.Triangle, IndexedGeometryMode.TriangleList, 3 ]

        let private material coordinate name : Loader.Material =
            { name = name; ambient = C4f.Black; diffuse = C4f.White; emissive = C4f.Black
              reflective = C4f.Black; specular = C4f.White; transparent = C4f.Black
              shininess = 17.0; blendMode = BlendMode.None; bumpScale = 2.0
              textures = Map.ofList [DefaultSemantic.DiffuseColorTexture, {coordIndex = coordinate; texture = nullTexture}] }

        let private create primitive arity vertexCount (indices : int[]) bones =
            let mesh = Assimp.Mesh("managed identity fixture", primitive, MaterialIndex = 1)
            for i in 0 .. vertexCount - 1 do
                mesh.Vertices.Add(Vector3(float32 i, float32 (i % 3), float32 (i % 5)))
                mesh.Normals.Add(Vector3.UnitZ)
                mesh.Tangents.Add(Vector3.UnitX)
                mesh.BiTangents.Add(Vector3.UnitY)
                mesh.TextureCoordinateChannels.[0].Add(Vector3(float32 i, -1.0f, 0.0f))
                mesh.TextureCoordinateChannels.[1].Add(Vector3(float32 i + 0.25f, 7.0f, 0.0f))
            mesh.UVComponentCount.[0] <- 2
            mesh.UVComponentCount.[1] <- 2
            if indices.Length = 0 then mesh.Faces.Add(Assimp.Face([||]))
            else
                for face in Array.chunkBySize arity indices do mesh.Faces.Add(Assimp.Face(face))
            if bones then
                for name, weight, offset in ["A", 0.25f, Matrix4x4.Identity; "B", 0.75f, Matrix4x4.CreateTranslation(2.0f, 3.0f, 4.0f)] do
                    let bone = Assimp.Bone(Name = name, OffsetMatrix = offset)
                    for i in 0 .. vertexCount - 1 do bone.VertexWeights.Add(Assimp.VertexWeight(i, weight))
                    mesh.Bones.Add bone
            mesh

        let private check (errors : ResizeArray<string>) context mode retainIndices (source : Assimp.Mesh) =
            let indexValues = source.GetIndices() |> Seq.toArray
            let positions, normals = source.Vertices.ToArray(), source.Normals.ToArray()
            let tangents, bitangents = source.Tangents.ToArray(), source.BiTangents.ToArray()
            let coordinates = source.TextureCoordinateChannels |> Array.map (fun c -> c.ToArray())
            let components = Array.copy source.UVComponentCount
            let faces = source.Faces |> Seq.map (fun f -> f.Indices.ToArray()) |> Seq.toArray
            let bones = source.Bones |> Seq.map (fun b -> b.Name, b.OffsetMatrix, b.VertexWeights.ToArray()) |> Seq.toArray
            let materials = [| material 0 "decoy"; material 1 "selected" |]
            let boneIndices = Dict<string * string, M44d * int>()
            let converted = Loader.Assimp.Conversions.toMesh 17 boneIndices materials source
            let geometry = converted.geometry
            if geometry.IsIndexed <> retainIndices then errors.Add $"{context}: indexed expected={retainIndices}, actual={geometry.IsIndexed}"
            if retainIndices && (isNull geometry.IndexArray || unbox<int[]> geometry.IndexArray <> indexValues) then
                errors.Add $"{context}: expected retained indices {indexValues}"
            if geometry.FaceVertexCount <> indexValues.Length then
                errors.Add $"{context}: FaceVertexCount expected={indexValues.Length}, actual={geometry.FaceVertexCount}"
            Expect.equal geometry.Mode mode (context + ": topology")
            Expect.equal geometry.VertexCount positions.Length (context + ": all vertices retained")
            let vector (v : Vector3) = V3f(v.X, v.Y, v.Z)
            Expect.equal (unbox<V3f[]> geometry.IndexedAttributes.[DefaultSemantic.Positions]) (Array.map vector positions) (context + ": untrimmed positions")
            Expect.equal (unbox<V3f[]> geometry.IndexedAttributes.[DefaultSemantic.Normals]) (Array.map vector normals) (context + ": untrimmed normals")
            Expect.equal (unbox<V3f[]> geometry.IndexedAttributes.[DefaultSemantic.DiffuseColorUTangents]) (Array.map vector tangents) (context + ": tangents")
            Expect.equal (unbox<V3f[]> geometry.IndexedAttributes.[DefaultSemantic.DiffuseColorVTangents]) (Array.map vector bitangents) (context + ": bitangents")
            Expect.equal (unbox<V2f[]> geometry.IndexedAttributes.[DefaultSemantic.DiffuseColorCoordinates]) (coordinates.[1] |> Array.map (fun v -> V2f(v.X, v.Y))) (context + ": selected material UV channel")
            Expect.equal converted.index 17 (context + ": mesh index")
            Expect.equal converted.meshTrafoId 0 (context + ": mesh transform bone id")
            Expect.equal converted.bounds (Box3d(positions |> Array.map (fun v -> V3d(v.X, v.Y, v.Z)))) (context + ": bounds include trailing vertices")
            Expect.equal boneIndices.[source.Name, ""] (M44d.Identity, 0) (context + ": mesh transform entry")
            if source.HasBones then
                Expect.equal boneIndices.Count 3 (context + ": bone table size")
                Expect.equal boneIndices.[source.Name, "A"] (M44d.Identity, 1) (context + ": first bone offset and id")
                let offset, id = boneIndices.[source.Name, "B"]
                Expect.equal (offset.M30, offset.M31, offset.M32, id) (2.0, 3.0, 4.0, 2) (context + ": second bone offset and id")
                let rawIndices = unbox<int[][]> geometry.IndexedAttributes.[Loader.Mesh.VertexBoneIndices]
                let rawWeights = unbox<float32[][]> geometry.IndexedAttributes.[Loader.Mesh.VertexBoneWeights]
                let compactIndices = unbox<V4i[]> geometry.IndexedAttributes.[Loader.Mesh.VertexBoneIndices4]
                let compactWeights = unbox<V4f[]> geometry.IndexedAttributes.[Loader.Mesh.VertexBoneWeights4]
                Expect.equal (rawIndices.Length, rawWeights.Length, compactIndices.Length, compactWeights.Length) (positions.Length, positions.Length, positions.Length, positions.Length) (context + ": untrimmed bone attributes")
                for i in 0 .. positions.Length - 1 do
                    Expect.equal rawIndices.[i] [|2; 1|] $"{context}: vertex {i} sorted bone indices"
                    Expect.equal rawWeights.[i] [|0.75f; 0.25f|] $"{context}: vertex {i} bone weights"
                    Expect.equal compactIndices.[i] (V4i(2, 1, -1, -1)) $"{context}: vertex {i} compact bone indices"
                    Expect.equal compactWeights.[i] (V4f(0.75f, 0.25f, 0.0f, 0.0f)) $"{context}: vertex {i} compact bone weights"
            else
                Expect.equal boneIndices.Count 1 (context + ": mesh-only bone table")
                Expect.equal geometry.SingleAttributes.[Loader.Mesh.VertexBoneIndices4] (box (V4i(-1, -1, -1, -1))) (context + ": no-bone index fallback")
                Expect.equal geometry.SingleAttributes.[Loader.Mesh.VertexBoneWeights4] (box V4f.Zero) (context + ": no-bone weight fallback")
            Expect.equal (source.Vertices.ToArray(), source.Normals.ToArray(), source.Tangents.ToArray(), source.BiTangents.ToArray()) (positions, normals, tangents, bitangents) (context + ": source vertex attributes unchanged")
            Expect.equal (source.TextureCoordinateChannels |> Array.map (fun c -> c.ToArray())) coordinates (context + ": source UV channels unchanged")
            Expect.equal source.UVComponentCount components (context + ": source UV components unchanged")
            Expect.equal (source.Faces |> Seq.map (fun f -> f.Indices.ToArray()) |> Seq.toArray) faces (context + ": source faces unchanged")
            Expect.equal (source.Bones |> Seq.map (fun b -> b.Name, b.OffsetMatrix, b.VertexWeights.ToArray()) |> Seq.toArray) bones (context + ": source bones unchanged")
            Expect.equal (source.Name, source.MaterialIndex) ("managed identity fixture", 1) (context + ": source identity and material unchanged")
            Expect.equal (materials.[1].name, materials.[1].shininess, materials.[1].bumpScale) ("selected", 17.0, 2.0) (context + ": material remains unchanged")

        let identityPrefixes() =
            let errors = ResizeArray<string>()
            for primitive, mode, arity in topologies do
                for faceCount in [0; 1; 2] do
                    for trailing in [arity; 2 * arity] do
                        for bones in [false; true] do
                            let indices = Array.init (faceCount * arity) id
                            let source = create primitive arity (indices.Length + trailing) indices bones
                            check errors $"{primitive}, prefix faces={faceCount}, trailing={trailing}, bones={bones}" mode true source
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

        let fullIdentity() =
            let errors = ResizeArray<string>()
            for primitive, mode, arity in topologies do
                for faceCount in [1; 2] do
                    for bones in [false; true] do
                        let indices = Array.init (faceCount * arity) id
                        let source = create primitive arity indices.Length indices bones
                        check errors $"{primitive}, full identity faces={faceCount}, bones={bones}" mode false source
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

        let nonIdentity() =
            let errors = ResizeArray<string>()
            for primitive, mode, arity in topologies do
                for trailing in [-arity; 0; arity] do
                    for repeated in [false; true] do
                        for bones in [false; true] do
                            let count = 2 * arity
                            let vertexCount = count + trailing
                            let indices = if repeated then Array.zeroCreate count else Array.init count (fun i -> vertexCount - 1 - i % vertexCount)
                            let source = create primitive arity vertexCount indices bones
                            check errors $"{primitive}, nonidentity trailing={trailing}, repeated={repeated}, bones={bones}" mode true source
            Expect.isTrue (errors.Count = 0) (String.concat "\n" errors)

    let tests (target : TestTarget) =
        [
            "Identity prefixes retain indices",          Cases.identityPrefixes
            "Full identity remains non-indexed",         Cases.fullIdentity
            "Non-identity preserves conversion data",    Cases.nonIdentity
        ]
        |> prepareCasesCpu "Assimp mesh conversion" target
