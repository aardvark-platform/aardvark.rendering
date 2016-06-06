namespace Aardvark.SceneGraph.IO

open Aardvark.Base
open Aardvark.Base.Rendering


module Loader =
    open System
    open System.IO

    type Texture =
        {
            coordIndex : int
            texture : ITexture
        }

    type Material =
        {
            name            : string
            ambient         : C4f
            diffuse         : C4f
            emissive        : C4f
            reflective      : C4f
            specular        : C4f
            transparent     : C4f
            shininess       : float
            blendMode       : BlendMode
            bumpScale       : float
            textures        : Map<Symbol, Texture>
        }

    type Mesh =
        {
            geometry : IndexedGeometry
            bounds   : Box3d
        }

    type Node =
        | Trafo     of Trafo3d * Node
        | Material  of Material * Node
        | Leaf      of Mesh
        | Group     of list<Node>
        | Empty

    type Scene = 
        {
            meshes : Mesh[]
            bounds : Box3d 
            root : Node 
        }

    module Assimp =
        open System.Runtime.CompilerServices
        open System.Collections.Generic
    
        

        type State =
            {
                trafo : Trafo3d
                meshes : Mesh[]
                meshMaterials : int[]
                materials : Material[]
                bounds : ref<Box3d>
            }

        [<AutoOpen>]
        module Conversions =
            let private additiveBlending = 
                BlendMode(
                    true, 
                    SourceFactor = BlendFactor.One, 
                    DestinationFactor = BlendFactor.One,
                    Operation = BlendOperation.Add,
                    SourceAlphaFactor = BlendFactor.One,
                    DestinationAlphaFactor = BlendFactor.One,
                    AlphaOperation = BlendOperation.Add
                )

            let toC4f (c : Assimp.Color4D) =
                C4f(c.R, c.G, c.B, c.A)

            let toBlendMode (m : Assimp.BlendMode) =
                match m with
                    | Assimp.BlendMode.Default -> BlendMode.None
                    | Assimp.BlendMode.Additive -> additiveBlending
                    | _ -> failwithf "[Assimp] unknown blend-mode: %A" m

            let toSemantic (t : Assimp.TextureType) =
                match t with
                    | Assimp.TextureType.Ambient -> DefaultSemantic.AmbientColorTexture
                    | Assimp.TextureType.Diffuse -> DefaultSemantic.DiffuseColorTexture
                    | Assimp.TextureType.Emissive -> DefaultSemantic.EmissiveColorTexture
                    | Assimp.TextureType.Specular -> DefaultSemantic.SpecularColorTexture
                    | Assimp.TextureType.Normals -> DefaultSemantic.NormalMapTexture
                    | Assimp.TextureType.Shininess -> DefaultSemantic.ShininessTexture
                    | Assimp.TextureType.Lightmap -> DefaultSemantic.LightMapTexture
                    | t -> failwithf "[Assimp] unknown texture type: %A" t


            let private knownSuffixes =
                Map.ofList [
                    DefaultSemantic.DiffuseColorTexture   , ["_color"]
                    DefaultSemantic.NormalMapTexture      , ["_normal"]
                    DefaultSemantic.SpecularColorTexture  , ["_spec"]
                ]

            let private imageExtensions =
                HashSet.ofList [
                    ".jpg"
                    ".png"
                    ".tga"
                    ".tif"
                    ".tiff"
                    ".gif"
                    ".bmp"
                ]

            let private (|Suffix|_|) (s : string) (test : string) =
                if test.ToLower().EndsWith s then
                    Some(test.Substring(0, test.Length - s.Length))
                else
                    None

            let getTextureTable (dir : string) : Map<string, Map<Symbol, string>> =
                let mutable res = Map.empty

                let textureFiles = 
                    Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                        |> Array.toList
                        |> List.filter (fun file ->
                            let ext = Path.GetExtension(file).ToLower()
                            imageExtensions.Contains ext
                        )
                        |> List.collect (fun f ->
                            let name = Path.GetFileNameWithoutExtension(f).ToLower()

                            let specific = 
                                match name with
                                    | Suffix "_color" name -> [name, (DefaultSemantic.DiffuseColorTexture, f)]
                                    | Suffix "_spec" name -> [name, (DefaultSemantic.SpecularColorTexture, f)]
                                    | Suffix "_normal" name -> [name, (DefaultSemantic.NormalMapTexture, f)]
                                    | _ -> [] 
                                
                            (name, (Symbol.Empty, f)) :: specific

                        )
                        |> Seq.groupBy fst
                        |> Seq.map (fun (k,g) -> k, g |> Seq.map snd |> Map.ofSeq)
                        |> Map.ofSeq



                textureFiles
                

            let toTextures (table : Map<string, Map<Symbol, string>>) (m : Assimp.TextureSlot[]) : Map<Symbol, Texture> =
                let slots = 
                    m |> Seq.toList |> List.map (fun slot -> 
                        let path = slot.FilePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                        let name = Path.GetFileNameWithoutExtension(path).ToLower()
                        name, slot
                    )

                let textures = 
                    slots |> List.map (fun (name, slot) ->
                            let sem = toSemantic slot.TextureType

                            match Map.tryFind name table with
                                | Some map ->
                                    let file = 
                                        match Map.tryFind sem map with
                                            | Some file -> file
                                            | None -> map.[Symbol.Empty]

                                    sem, { texture = FileTexture(file, true) :> ITexture; coordIndex = slot.UVIndex }

                                | None ->
                                    sem, { texture = NullTexture() :> ITexture; coordIndex = slot.UVIndex }
                         )
                      |> Map.ofList

                
                let names = slots |> List.map fst |> Set.ofList
                Log.warn "names: %A" names


                textures

                

            let toMesh (materials : Material[]) (m : Assimp.Mesh) : Mesh =
                let mat = materials.[m.MaterialIndex]

                let attributes = SymDict.empty
                let res =
                    IndexedGeometry(
                        Mode = IndexedGeometryMode.TriangleList,
                        IndexedAttributes = attributes
                    )


                if m.HasFaces then
                    let mutable bounds = Box3d.Invalid
                    if m.HasVertices then
                        attributes.[DefaultSemantic.Positions] <- m.Vertices.MapToArray(fun v -> bounds.ExtendBy(V3d(v.X, v.Y, v.Z)); V3f(v.X, v.Y, v.Z))

                    if m.HasNormals then
                        attributes.[DefaultSemantic.Normals] <- m.Normals.MapToArray(fun v -> V3f(v.X, v.Y, v.Z))

                    if m.HasTangentBasis then
                        attributes.[DefaultSemantic.DiffuseColorUTangents] <- m.Tangents.MapToArray(fun v -> V3f(v.X, v.Y, v.Z))
                        attributes.[DefaultSemantic.DiffuseColorVTangents] <- m.BiTangents.MapToArray(fun v -> V3f(v.X, v.Y, v.Z))


                    let semantics = mat.textures |> Map.toSeq |> Seq.map (fun (k,v) -> v.coordIndex, k) |> Map.ofSeq
                    for c in 0..m.TextureCoordinateChannelCount-1 do
                        let att = m.TextureCoordinateChannels.[c]
                        match Map.tryFind c semantics with
                            | Some sem ->
                                let coordSem = 
                                    match string sem with
                                        | "DiffuseColorTexture" -> DefaultSemantic.DiffuseColorCoordinates
                                        | _ -> failwithf "[Assimp] no coords for semantic: %A" sem
                                
                                attributes.[coordSem] <- att.MapToArray(fun v -> V2f(v.X, v.Y))

                            | None ->
                                ()

                    let mode =
                        match m.PrimitiveType with
                            | Assimp.PrimitiveType.Point -> IndexedGeometryMode.PointList
                            | Assimp.PrimitiveType.Line -> IndexedGeometryMode.LineList
                            | Assimp.PrimitiveType.Triangle -> IndexedGeometryMode.TriangleList
                            | t -> failwithf "[Assimp] bad primitive type: %A" t


                    res.Mode <- mode
                    let indices = m.GetIndices()
                    let identity = indices |> Seq.indexed |> Seq.forall(fun (a,b) -> a = b)
                    if not identity then
                        res.IndexArray <- indices

                    { geometry = res; bounds = bounds }
                else
                    failwith "[Assimp] mesh has no faces"



            let toMaterial (textures : Map<string, Map<Symbol, string>>) (m : Assimp.Material) : Material =
                {
                    name            = m.Name
                    ambient         = toC4f m.ColorAmbient
                    diffuse         = toC4f m.ColorDiffuse
                    emissive        = toC4f m.ColorEmissive
                    reflective      = toC4f m.ColorReflective
                    specular        = toC4f m.ColorSpecular
                    transparent     = toC4f m.ColorTransparent
                    shininess       = float m.Shininess
                    blendMode       = toBlendMode m.BlendMode
                    bumpScale       = float m.BumpScaling
                    textures        = toTextures textures (m.GetAllMaterialTextures())
                }

            let toM44d (m : Assimp.Matrix4x4) : M44d =
                M44d(
                    float m.A1, float m.A2, float m.A3, float m.A4,
                    float m.B1, float m.B2, float m.B3, float m.B4,
                    float m.C1, float m.C2, float m.C3, float m.C4,
                    float m.D1, float m.D2, float m.D3, float m.D4
                )

        let rec traverse (state : State) (n : Assimp.Node) : Node =
            
            let trafo =
                let fw = toM44d n.Transform
                if fw.IsIdentity(Constant.PositiveTinyValue) then
                    Trafo3d.Identity
                else
                    Trafo3d(fw, fw.Inverse)

            let children = 
                if n.HasChildren then
                    let children = n.Children |> Seq.map (traverse { state with trafo = trafo * state.trafo }) |> Seq.toList

                    match children with
                        | [c] -> c
                        | c -> c |> List.choose (fun n -> match n with | Empty -> None | _ -> Some n) |> Group
                else 
                    Empty

            let leaves =
                if n.HasMeshes then
                    let leaves = 
                        n.MeshIndices |> Seq.map (fun i -> 
                            let mesh = state.meshes.[i]
                            let mat = state.materials.[state.meshMaterials.[i]]

                            match mesh.geometry.IndexedAttributes.TryGetValue DefaultSemantic.Positions with
                                | (true, (:? array<V3f> as pos)) -> 
                                    let bb = Box3d(pos |> Seq.map (fun p -> state.trafo.Forward.TransformPos(V3d(p))))
                                    state.bounds.contents.ExtendBy(bb)

                                | _ ->
                                    ()

                            Material(mat, Leaf mesh)
                        ) |> Seq.toList 
                    match leaves with
                        | [l] -> l
                        | ls -> Group ls
                else
                    Empty

            let node = 
                match children, leaves with
                    | Empty, r -> r
                    | l, Empty -> l
                    | l,r -> Group [l;r]

            let node =
                match node with
                    | Empty -> Empty
                    | node -> 
                        let fw = toM44d n.Transform
                        if fw.IsIdentity(Constant.PositiveTinyValue) then
                            node
                        else
                            Trafo(Trafo3d(fw, fw.Inverse), node)

            node



        let load (file : string) =
            use ctx = new Assimp.AssimpContext()
            let dir = Path.GetDirectoryName(file)

            let scene = ctx.ImportFile(file)

            let textureTable = getTextureTable dir
            let materials = scene.Materials.MapToArray(fun m -> toMaterial textureTable m)
            let state =
                {
                    trafo = Trafo3d.Identity
                    materials = materials
                    meshMaterials = scene.Meshes.MapToArray(fun m -> m.MaterialIndex)
                    meshes = scene.Meshes.MapToArray(fun m -> toMesh materials m)
                    bounds = ref Box3d.Invalid
                }

            let root = traverse state scene.RootNode

            {
                root = root
                bounds = !state.bounds
                meshes = state.meshes
            }
