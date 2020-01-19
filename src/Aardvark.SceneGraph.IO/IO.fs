namespace Aardvark.SceneGraph.IO

open Aardvark.Base
open Aardvark.Base.Sorting
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive

#nowarn "9"
#nowarn "51"

module Loader =
    open System
    open System.IO

    [<OnAardvarkInit>]
    let init() =
        Assimp.Unmanaged.AssimpLibraryImplementation.NativeLibraryPath <- Aardvark.NativeLibraryPath
        Assimp.Unmanaged.AssimpLibraryImplementation.SeparateLibraryDirectories <- Aardvark.SeparateLibraryDirectories



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

        interface IUniformProvider with
            member x.TryGetUniform(_, sem) =
                match Map.tryFind sem x.textures with
                    | Some tex -> AVal.constant tex.texture :> IAdaptiveValue |> Some
                    | _ ->
                        let singleValueTexture (c : C4f) =
                            let img = PixImage<float32>(Col.Format.RGBA, V2i.II)
                            
                            img.GetMatrix<C4f>().Set(c) |> ignore
                            let mip = PixImageMipMap [| img :> PixImage |]
                            PixTexture2d(mip, false) :> ITexture |> AVal.constant :> IAdaptiveValue |> Some

                        match string sem with
                            | "DiffuseColorTexture" -> singleValueTexture x.diffuse
                            | "SpecularColorTexture" -> singleValueTexture x.specular
                            | "NormalMapTexture" -> singleValueTexture (C4f(0.5f, 0.5f, 1.0f, 1.0f))

                            | "AmbientColor" -> AVal.constant x.ambient :> IAdaptiveValue |> Some
                            | "DiffuseColor" -> AVal.constant x.diffuse :> IAdaptiveValue |> Some
                            | "EmissiveColor" -> AVal.constant x.emissive :> IAdaptiveValue |> Some
                            | "ReflectiveColor" -> AVal.constant x.reflective :> IAdaptiveValue |> Some
                            | "SpecularColor" -> AVal.constant x.specular :> IAdaptiveValue |> Some
                            | "Shininess" -> AVal.constant x.shininess :> IAdaptiveValue |> Some
                            | "BumpScale" -> AVal.constant x.bumpScale :> IAdaptiveValue |> Some


                            | _ -> None

            member x.Dispose() = ()

    let private VertexBoneWeights = Symbol.Create "VertexBoneWeights"
    let private VertexBoneIndices = Symbol.Create "VertexBoneIndices"
    
    let private VertexBoneWeights4 = Symbol.Create "VertexBoneWeights4"
    let private VertexBoneIndices4 = Symbol.Create "VertexBoneIndices4"

    open System.Runtime.InteropServices
    open Microsoft.FSharp.NativeInterop

    let private bitsToV4f(v : obj) =
        let mutable res = V4f.Zero
        let gc = GCHandle.Alloc(v, GCHandleType.Pinned)
        try
            let size = min (Marshal.SizeOf v) sizeof<V4f>
            Marshal.Copy (gc.AddrOfPinnedObject(), NativePtr.toNativeInt &&res, size)
            res
        finally
            gc.Free()

    type Mesh =
        {
            index       : int
            geometry    : IndexedGeometry
            meshTrafoId : int
            bounds      : Box3d
        }

        interface IAttributeProvider with
            member x.TryGetAttribute(sem) =
                match x.geometry.IndexedAttributes.TryGetValue sem with
                    | (true, arr) ->
                        let b = arr |> ArrayBuffer :> IBuffer |> AVal.constant
                        Some (BufferView(b, arr.GetType().GetElementType()))
                    | _ ->
                        match x.geometry.SingleAttributes.TryGetValue sem with
                            | (true, value) ->
                                let v = bitsToV4f value
                                Some (BufferView(SingleValueBuffer(AVal.constant v), value.GetType()))

                            | _ -> 
                                Some (BufferView(SingleValueBuffer(AVal.constant V4f.Zero), typeof<V4f>))

            member x.All = Seq.empty
            member x.Dispose() = ()

        static member VertexBoneWeights = VertexBoneWeights
        static member VertexBoneIndices = VertexBoneIndices
        
        static member VertexBoneWeights4 = VertexBoneWeights4
        static member VertexBoneIndices4 = VertexBoneIndices4

    type Node =
        | Trafo     of Trafo3d * Node
        | Material  of Material * Node
        | Leaf      of Mesh
        | Group     of list<Node>
        | Empty


    type AnimTree = { name : string; trafo : M44d; frames : M44d[]; children : list<AnimTree>; meshNames : string[] }
    type Animation = { name : string; root : AnimTree; frames : int; framesPerSecond : float; interpolate : float -> M44d[] }


    

    type Scene = 
        {
            meshes      : Mesh[]
            animantions : Map<string, Animation>
            bounds      : Box3d 
            root        : Node 
            rootTrafo   : Trafo3d
        }

        member x.SubstituteMaterial (mapping : Material -> Option<Material>) =
            let rec traverse (n : Node) =
                match n with
                    | Empty -> 
                        Empty
                    | Group es ->
                        Node.Group (List.map traverse es)

                    | Leaf m ->
                        Node.Leaf m

                    | Material(m, n) ->
                        let newMat =
                            match mapping m with
                                | Some m -> m
                                | None -> m

                        Material(newMat, traverse n)

                    | Trafo(t, n) ->
                        Trafo(t, traverse n)

            { x with root = traverse x.root }

        member x.SubstituteMesh (mapping : Mesh -> Option<Mesh>) =
            let mapping o =
                match mapping o with    
                    | Some m -> { m with index = o.index }
                    | None -> o

            let newMeshes = x.meshes |> Array.map mapping

            let rec replaceMeshes (n : Node) =
                match n with
                    | Empty -> 
                        Empty
                    | Leaf m ->
                        Leaf newMeshes.[m.index]
                    | Material(m,n) ->
                        Material(m, replaceMeshes n)
                    | Trafo(t, n) ->
                        Trafo(t, replaceMeshes n)
                    | Group es ->
                        Group (List.map replaceMeshes es)
                

            { x with 
                meshes = newMeshes
                root = replaceMeshes x.root
            }





    module SgSems =
        open Aardvark.Base.Ag
        open Aardvark.SceneGraph
        open Aardvark.SceneGraph.Semantics

        type Node with      
            member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()
            member x.ModelTrafoStack 
                with get() : list<aval<Trafo3d>> = x?ModelTrafoStack
                and set v = x?ModelTrafoStack <- v

            
            member x.Uniforms 
                with get() : list<IUniformProvider> = x?Uniforms
                and set v = x?Uniforms <- v

            member x.ModelTrafo : aval<Trafo3d> = x?ModelTrafo()

        type Scene with      
            member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()
            member x.ModelTrafoStack : list<aval<Trafo3d>> = x?ModelTrafoStack
            member x.ModelTrafo : aval<Trafo3d> = x?ModelTrafo()

        [<Semantic>]
        type SceneSem() =

            member x.Uniforms(n : Node) =
                let parent = n.Uniforms
                match n with
                    | Material(m,c) ->
                        c.Uniforms <- (m :> IUniformProvider)::parent
                    | _ ->
                        n.AllChildren?Uniforms <- parent

            member x.ModelTrafoStack(n : Node) =
                let parent = n.ModelTrafoStack
                match n with
                    | Trafo(t,c) -> 
                        c.ModelTrafoStack <- (AVal.constant t)::parent

                    | Material(m,c) ->
                        c.ModelTrafoStack <- parent

                    | _ ->
                        n.AllChildren?ModelTrafoStack <- parent

            member x.RenderObjects(n : Node) =
                match n with
                    | Trafo(_,n) -> 
                        n.RenderObjects()
                    | Material(_,n) -> 
                        n.RenderObjects()
                    | Group(nodes) ->
                        nodes |> ASet.ofList |> ASet.collect (fun n -> n.RenderObjects())
                    | Empty ->
                        ASet.empty
                    | Leaf mesh ->

                        let faceVertexCount, indexed =
                            if isNull mesh.geometry.IndexArray then
                                let (KeyValue(_,v)) = mesh.geometry.IndexedAttributes |> Seq.head
                                v.Length, false
                            else
                                mesh.geometry.IndexArray.Length, true

                        let call =
                            DrawCallInfo(
                                FaceVertexCount = faceVertexCount,
                                InstanceCount = 1
                            )

                        let ro = RenderObject.create()
                        ro.VertexAttributes <- mesh
                        ro.Mode <- mesh.geometry.Mode
                        ro.DrawCallInfos <- AVal.constant [call]

                        let uniforms =
                            UniformProvider.ofList [
                                "MeshTrafoBone", AVal.constant mesh.meshTrafoId :> IAdaptiveValue
                            ]

                        ro.Uniforms <- UniformProvider.union uniforms ro.Uniforms

                        if indexed then 
                            let index = mesh.geometry.IndexArray
                            let t = index.GetType().GetElementType()
                            let b = AVal.constant (ArrayBuffer index :> IBuffer)
                            ro.Indices <- Some (BufferView(b, t))
                        else 
                            ro.Indices <- None

                        ASet.single (ro :> IRenderObject)

            member x.RenderObjects(s : Scene) =
                s.root.RenderObjects()

            member x.LocalBoundingBox(s : Scene) =
                AVal.constant s.bounds

            member x.GlobalBoundingBox(n : Node) : aval<Box3d> =
                match n with
                    | Trafo(_,n) -> 
                        n?GlobalBoundingBox()
                    | Material(_,n) -> 
                        n?GlobalBoundingBox()
                    | Group(nodes) ->
                        let bbs : list<aval<Box3d>> = nodes |> List.map (fun n -> n?GlobalBoundingBox()) 
                        AVal.custom (fun token ->
                            let bbs = bbs |> List.map (fun v -> v.GetValue token)
                            Box3d bbs
                        )
                    | Empty ->
                        AVal.constant Box3d.Invalid
                    | Leaf mesh ->
                        n.ModelTrafo |> AVal.map (fun t -> mesh.bounds.Transformed t)
                            
            member x.LocalBoundingBox(n : Node) : aval<Box3d> =
                match n with
                    | Trafo(t,n) -> 
                        let box : aval<Box3d> = n?LocalBoundingBox()
                        box |> AVal.map (fun b -> b.Transformed t)
                    | Material(_,n) -> 
                        n?LocalBoundingBox()
                    | Group(nodes) ->
                        let bbs : list<aval<Box3d>> = nodes |> List.map (fun n -> n?LocalBoundingBox()) 
                        AVal.custom (fun token ->
                            let bbs = bbs |> List.map (fun v -> v.GetValue token)
                            Box3d bbs
                        )
                    | Empty ->
                        AVal.constant Box3d.Invalid
                    | Leaf mesh ->
                        AVal.constant mesh.bounds
                            
            member x.GlobalBoundingBox(s : Scene) =
                s.ModelTrafo |> AVal.map (fun t -> s.bounds.Transformed t)


    type Assimp = class end

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
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
                    | t -> Symbol.Empty //failwithf "[Assimp] unknown texture type: %A" t


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
                                    | Suffix "_d" name -> [name, (DefaultSemantic.DiffuseColorTexture, f)]
                                    | Suffix "_s" name -> [name, (DefaultSemantic.SpecularColorTexture, f)]
                                    | Suffix "_n" name -> [name, (DefaultSemantic.NormalMapTexture, f)]
                                    | Suffix "_diff" name -> [name, (DefaultSemantic.DiffuseColorTexture, f)]
                                    | Suffix "_norm" name -> [name, (DefaultSemantic.NormalMapTexture, f)]
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
                        let path = slot.FilePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)
                        let name = Path.GetFileNameWithoutExtension(path).ToLower()
                        name, slot
                    )

                let mutable textures = 
                    slots |> List.map (fun (name, slot) ->
                            let sem = toSemantic slot.TextureType

                            match Map.tryFind name table with
                                | Some map ->
                                    let file = 
                                        match Map.tryFind sem map with
                                            | Some file -> file
                                            | None -> map.[Symbol.Empty]

                                    sem, { texture = FileTexture(file, { wantCompressed = false; wantMipMaps = true; wantSrgb = false }) :> ITexture; coordIndex = slot.UVIndex }

                                | None ->
                                    sem, { texture = NullTexture() :> ITexture; coordIndex = slot.UVIndex }
                         )
                      |> Map.ofList

                
                let names = 
                    slots |> List.map (fun (n,s) ->
                        match n with
                            | Suffix "_color" name -> name
                            | Suffix "_spec" name -> name
                            | Suffix "_normal" name -> name
                            | Suffix "_d" name -> name
                            | Suffix "_s" name -> name
                            | Suffix "_n" name -> name
                            | Suffix "_diff" name -> name
                            | Suffix "_norm" name -> name
                            | _ -> n
                    )
                    |> Set.ofList

                if Set.count names = 1 then
                    let name = Seq.head names
                    match Map.tryFind name table with
                        | Some map ->
                            for (k,v) in Map.toSeq map do
                                if not (Map.containsKey k textures) then
                                    textures <- Map.add k { texture = FileTexture(v, true) :> ITexture; coordIndex = 0 } textures
                        | None ->
                            ()

                textures

            let toM44d (m : Assimp.Matrix4x4) : M44d =
                M44d(
                    float m.A1, float m.A2, float m.A3, float m.A4,
                    float m.B1, float m.B2, float m.B3, float m.B4,
                    float m.C1, float m.C2, float m.C3, float m.C4,
                    float m.D1, float m.D2, float m.D3, float m.D4
                )

            let toV3d (v : Assimp.Vector3D) =
                V3d(v.X, v.Y, v.Z)

            let toV2d (v : Assimp.Vector2D) =
                V2d(v.X, v.Y)
               
            let toRot3d (v : Assimp.Quaternion) =
                Rot3d(float v.W, float v.X, float v.Y, float v.Z)

            let private toV4i (arr : int[]) =
                match arr.Length with
                    | 0 -> V4i(-1,-1,-1,-1)
                    | 1 -> V4i(arr.[0], -1, -1, -1)
                    | 2 -> V4i(arr.[0], arr.[1], -1, -1)
                    | 3 -> V4i(arr.[0], arr.[1], arr.[2], -1)
                    | _ -> V4i(arr.[0], arr.[1], arr.[2], arr.[3])
                    
            let private toV4f (arr : float32[]) =
                match arr.Length with
                    | 0 -> V4f(0.0f, 0.0f, 0.0f, 0.0f)
                    | 1 -> V4f(1.0f, 0.0f, 0.0f, 0.0f)
                    | 2 -> V4f(arr.[0], arr.[1], 0.0f, 0.0f) / (arr.[0] + arr.[1])
                    | 3 -> V4f(arr.[0], arr.[1], arr.[2], 0.0f) / (arr.[0] + arr.[1] + arr.[2])
                    | _ -> V4f(arr.[0], arr.[1], arr.[2], arr.[3]) / (arr.[0] + arr.[1] + arr.[2] + arr.[3])

            let toMesh (index : int) (boneIndices : Dict<string * string, M44d * int>) (materials : Material[]) (m : Assimp.Mesh) : Mesh =
                let mat = materials.[m.MaterialIndex]

                let attributes = SymDict.empty
                let single = SymDict.empty
                let res =
                    IndexedGeometry(
                        Mode = IndexedGeometryMode.TriangleList,
                        IndexedAttributes = attributes,
                        SingleAttributes = single
                    )




                if m.HasFaces then
                    
                    let mutable bounds = Box3d.Invalid
                    if m.HasVertices then
                        attributes.[DefaultSemantic.Positions] <- m.Vertices.MapToArray(fun v -> bounds.ExtendBy(V3d(v.X, v.Y, v.Z)); V3f(v.X, v.Y, v.Z))
                    
                    let getBoneIndex (meshName : string) (boneName : string) (off : M44d) = 
                        let key = meshName, boneName
                        let id = boneIndices.Count
                        boneIndices.GetOrCreate(key, fun _-> off, id) |> snd

                    let meshTrafoId = getBoneIndex m.Name "" M44d.Identity
                    let mesh = { geometry = res; bounds = bounds; index = index; meshTrafoId = meshTrafoId }


                    if m.HasNormals then
                        attributes.[DefaultSemantic.Normals] <- m.Normals.MapToArray(fun v -> V3f(v.X, v.Y, v.Z))

                    if m.HasTangentBasis then
                        attributes.[DefaultSemantic.DiffuseColorUTangents] <- m.Tangents.MapToArray(fun v -> V3f(v.X, v.Y, v.Z))
                        attributes.[DefaultSemantic.DiffuseColorVTangents] <- m.BiTangents.MapToArray(fun v -> V3f(v.X, v.Y, v.Z))


                    let mutable boneNames = [||]


                    if m.HasBones then
                        let weights = Array.create m.VertexCount [||]
                        let indices = Array.create m.VertexCount [||]
                        

                        boneNames <- Array.create m.BoneCount ""

                        for bi in 0 .. m.BoneCount - 1 do
                            let bone = m.Bones.[bi]
                            
                            // mesh -> bone
                            let off = toM44d bone.OffsetMatrix
                            let bi = getBoneIndex m.Name bone.Name off

                            for v in bone.VertexWeights do
                                let w = v.Weight
                                let vi = v.VertexID
                                weights.[vi] <- Array.append weights.[vi] [|w|]
                                indices.[vi] <- Array.append indices.[vi] [|bi|]

                        for i in 0 .. m.VertexCount - 1 do
                            let id = weights.[i].CreatePermutationQuickSortDescending()
                            weights.[i] <- id |> Array.map (Array.get weights.[i])
                            indices.[i] <- id |> Array.map (Array.get indices.[i])

                        attributes.[Mesh.VertexBoneIndices] <- indices
                        attributes.[Mesh.VertexBoneWeights] <- weights
                        attributes.[Mesh.VertexBoneIndices4] <- Array.map toV4i indices
                        attributes.[Mesh.VertexBoneWeights4] <- Array.map toV4f weights
                    else
                        single.[Mesh.VertexBoneIndices4] <- V4i(-1,-1,-1,-1)
                        single.[Mesh.VertexBoneWeights4] <- V4f.Zero

//                    else
//                        attributes.[Mesh.VertexBoneIndices] <- 
//                        attributes.[Mesh.VertexBoneWeights] <- weights
//                        attributes.[Mesh.VertexBoneIndices4] <- Array.map toV4i indices
//                        attributes.[Mesh.VertexBoneWeights4] <- Array.map toV4f weights


                    let semantics = mat.textures |> Map.toSeq |> Seq.map (fun (k,v) -> v.coordIndex, k) |> Map.ofSeq
                    for c in 0..m.TextureCoordinateChannelCount-1 do
                        let att = m.TextureCoordinateChannels.[c]
                        match Map.tryFind c semantics with
                            | Some sem ->
                                let coordSem = 
                                    match string sem with
                                        | "DiffuseColorTexture" -> DefaultSemantic.DiffuseColorCoordinates
                                        | _ -> 
                                            DefaultSemantic.DiffuseColorCoordinates
                                            //failwithf "[Assimp] no coords for semantic: %A" sem
                                
                                attributes.[coordSem] <- att.MapToArray(fun v -> V2f(v.X, v.Y))

                            | None ->
                                if c = 0 then
                                    attributes.[DefaultSemantic.DiffuseColorCoordinates] <- att.MapToArray(fun v -> V2f(v.X, v.Y))

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

                    
                    
                    mesh
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
                        n.MeshIndices |> Seq.choose (fun i -> 
                            let mesh = state.meshes.[i]
                            let mat = state.materials.[state.meshMaterials.[i]]

                            if mat.name.ToLower().Contains "16___default" then
                                None
                            else
                                match mesh.geometry.IndexedAttributes.TryGetValue DefaultSemantic.Positions with
                                    | (true, (:? array<V3f> as pos)) -> 
                                        let bb = Box3d(pos |> Seq.map (fun p -> state.trafo.Forward.TransformPos(V3d(p))))
                                        state.bounds.contents.ExtendBy(bb)

                                    | _ ->
                                        ()

                                Some (Material(mat, Leaf mesh))
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
            
        let rec toAnimTree (meshes : Assimp.Mesh[]) (keyFrames : Dict<string, array<M44d>>) (n : Assimp.Node) =
            let name = n.Name

            let frames =
                match keyFrames.TryGetValue name with
                    | (true, data) -> data
                    | _ -> [||]

            let children =
                n.Children |> Seq.map (toAnimTree meshes keyFrames) |> Seq.toList

            let meshNames = n.MeshIndices.MapToArray (fun i -> meshes.[i].Name)

            { name = name; trafo = toM44d n.Transform; frames = frames; children = children; meshNames = meshNames }
                        




        //[<CompiledName("Initialize")>]
        //let initialize () =
        //    Log.start "unpacking native dependencies for assimp"
        //    let r = 
        //        try
        //            DynamicLinker.tryUnpackNativeLibrary "Assimp"
        //        with e -> 
        //            Log.warn "failed to unpack native dependencies: %s" e.Message
        //            false
        //    Log.stop ()
        //    if r then Log.line "Assimp native dependencies successfully unpacked."
        //    else Log.line "Failed to unpack native assimp dependencies. Did you forget Aardvark.Init()? Make sure Aardvark.SceneGraph.IO.dll is in your output directory."

        let defaultFlags = 
            Assimp.PostProcessSteps.CalculateTangentSpace |||
            Assimp.PostProcessSteps.GenerateSmoothNormals |||
            //Assimp.PostProcessSteps.FixInFacingNormals ||| 
            //Assimp.PostProcessSteps.JoinIdenticalVertices |||
            Assimp.PostProcessSteps.FindDegenerates |||
            //Assimp.PostProcessSteps.FlipUVs |||
            //Assimp.PostProcessSteps.FlipWindingOrder |||
            Assimp.PostProcessSteps.MakeLeftHanded ||| 
            Assimp.PostProcessSteps.Triangulate |||
            Assimp.PostProcessSteps.CalculateTangentSpace

        let loadFrom (file : string) (postProcessingFlags : Assimp.PostProcessSteps) = 
            use ctx = new Assimp.AssimpContext()
            let dir = Path.GetDirectoryName(file)
            let scene = ctx.ImportFile(file, postProcessingFlags)

            let textureTable = getTextureTable dir
            //printfn "%A" textureTable
            let materials = scene.Materials.MapToArray(fun m -> toMaterial textureTable m)

            let boneIndices = Dict()

            let state =
                {
                    trafo = Trafo3d.Identity
                    materials = materials
                    meshMaterials = scene.Meshes.MapToArray(fun m -> m.MaterialIndex)
                    meshes = scene.Meshes.ToArray() |> Array.mapi (fun i m -> toMesh i boneIndices materials m)
                    bounds = ref Box3d.Invalid
                }

            let initialTrafosInv = 
                let store = Dict<string, M44d>()
                let rec trafos (currentTrafo : M44d) (tree : Assimp.Node) =
                    let own = toM44d tree.Transform
                    let currentTrafo = currentTrafo * own

                    for mi in tree.MeshIndices do
                        store.[scene.Meshes.[mi].Name] <- currentTrafo.Inverse

                    for c in tree.Children do
                        trafos currentTrafo c

                trafos M44d.Identity scene.RootNode
                store

            let animations = List<Animation>()

            for a in scene.Animations do
                let name = a.Name

                let frames =
                    if a.HasNodeAnimations then 
                        let na = a.NodeAnimationChannels.[0]
                        max na.PositionKeyCount (max na.ScalingKeyCount na.RotationKeyCount)
                    elif a.HasMeshAnimations then
                        let ma = a.MeshAnimationChannels.[0]
                        ma.MeshKeyCount
                    else
                        0


                let duration = a.DurationInTicks * a.TicksPerSecond
                let fps = float frames / duration

                let keyFrames = Dict.empty

                for na in a.NodeAnimationChannels do
                    let pos         = na.PositionKeys |> Seq.map (fun e -> e.Time, toV3d e.Value) |> MapExt.ofSeq
                    let rot         = na.RotationKeys |> Seq.map (fun e -> e.Time, toRot3d e.Value) |> MapExt.ofSeq
                    let scale       = na.ScalingKeys |> Seq.map (fun e -> e.Time, toV3d e.Value) |> MapExt.ofSeq

                    let position (t : float) =
                        let (l,s,r) = MapExt.neighbours t pos

                        let l = match s with | Some t -> Some t | None -> l

                        match l, r with
                            | Some(lt,lv), Some (rt,rv) -> 
                                let f = (t - lt) / (rt - lt)
                                lv * (1.0 - f) + rv * f

                            | Some(_,lv), None ->
                                lv
                            | None, Some(_,rv) ->
                                rv
                            | None, None ->
                                V3d.Zero

                    let scale (t : float) =
                        let (l,s,r) = MapExt.neighbours t scale

                        let l = match s with | Some t -> Some t | None -> l

                        match l, r with
                            | Some(lt,lv), Some (rt,rv) -> 
                                let f = (t - lt) / (rt - lt)
                                lv * (1.0 - f) + rv * f

                            | Some(_,lv), None ->
                                lv
                            | None, Some(_,rv) ->
                                rv
                            | None, None ->
                                V3d.Zero

                    let rotation (t : float) =
                        let (l,s,r) = MapExt.neighbours t rot

                        let l = match s with | Some t -> Some t | None -> l

                        match l, r with
                            | Some(lt,lv), Some (rt,rv) -> 
                                let f = (t - lt) / (rt - lt)
                                lv * (1.0 - f) + rv * f

                            | Some(_,lv), None ->
                                lv
                            | None, Some(_,rv) ->
                                rv
                            | None, None ->
                                Rot3d.Identity

                    let node = na.NodeName

                    let matrices =
                        Array.init frames (fun i ->
                            let t = float i / fps
                            let p = position t
                            let rot = rotation t

                            let r : M44d = rot |> Rot3d.op_Explicit
                            let s = scale t
                            M44d.Translation(p) * r * M44d.Scale(s) 
                        )

                    keyFrames.[na.NodeName] <- matrices

                let animTree = toAnimTree (Seq.toArray scene.Meshes) keyFrames scene.RootNode



                let offsets = Dict<string, List<M44d * M44d * int>>()
                for (KeyValue((meshName, boneName), (off, i))) in boneIndices do
                    let l = offsets.GetOrCreate(boneName, fun _ -> List())

                    let meshTrafoInv =
                        match initialTrafosInv.TryGetValue meshName with
                            | (true, initial) -> initial
                            | _ -> M44d.Identity

                    l.Add((meshTrafoInv, off, i))


                let interpolate (t : float) =
                    let t = (duration + (t % duration)) % duration

                    let frame = t * fps
                    let f0 = int (floor frame)
                    let f1 = (f0 + 1) % frames
                    let t = (frame - float f0)

                    let rec traverse (result : M44d[]) (currentTrafo : M44d) (f0 : int) (f1 : int) (t : float) (tree : AnimTree) =

                        let targets =
                            match offsets.TryGetValue tree.name with
                                | (true, targets) -> targets :> seq<_>
                                | _ -> Seq.empty

                        let frameTrafo = 
                            if f0 < tree.frames.Length && f1 < tree.frames.Length then 
                                let t0 = tree.frames.[f0]
                                let t1 = tree.frames.[f1]
                                let frame = t0 * (1.0 - t) + t1 * t
                                frame
                            else 
                                tree.trafo

                        let currentTrafo = currentTrafo * frameTrafo

                        for meshName in tree.meshNames do
                            match boneIndices.TryGetValue((meshName, "")) with
                                | (true, (_,i)) ->
                                    let meshTrafoInv =
                                        match initialTrafosInv.TryGetValue meshName with
                                            | (true, initial) -> initial
                                            | _ -> M44d.Identity
                                    result.[i] <- meshTrafoInv * currentTrafo

                                | _ ->
                                    ()

                        for (meshTrafoInv, off, index) in targets do
                            result.[index] <- meshTrafoInv * currentTrafo * off
                        
                        for c in tree.children do
                            traverse result currentTrafo f0 f1 t c
                    
                    let result = Array.create boneIndices.Count M44d.Identity
                    traverse result M44d.Identity f0 f1 t animTree
                    result

                animations.Add {
                    name = name
                    frames = frames
                    root = animTree
                    framesPerSecond = fps
                    interpolate = interpolate
                }

            let root = traverse state scene.RootNode
            let rootTrafo = 
                let m = toM44d scene.RootNode.Transform
                Trafo3d(m, m.Inverse)

            {
                root = root
                rootTrafo = rootTrafo
                bounds = !state.bounds
                meshes = state.meshes
                animantions = animations |> Seq.map (fun a -> a.name, a) |> Map.ofSeq
            }

        let load (file : string) =
            loadFrom file defaultFlags

    type Assimp with
        static member Load(fileName : string, ?flags : Assimp.PostProcessSteps) =
            let flags = defaultArg flags Assimp.defaultFlags
            Assimp.loadFrom fileName flags