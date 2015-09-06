namespace Demo

open System
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.Rendering.GL
open Aardvark.SceneGraph
open Aardvark.SceneGraph.CSharp
open Aardvark.SceneGraph.Semantics
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.CSharp
open Assimp
open Assimp.Unmanaged
open System.Runtime.CompilerServices
open System.Collections.Generic
open System.Runtime.InteropServices
open System.IO


module AssimpExporter =
 
    let private toArrayOf<'a when 'a : struct> (a : Array) =
        let t = a.GetType().GetElementType()
        let ts = Marshal.SizeOf t

        let size = a.Length * ts
        let targetSize = sizeof<'a>
        let result : 'a[] = Array.zeroCreate (size / targetSize)
        let gc = GCHandle.Alloc(result, GCHandleType.Pinned)

        a.UnsafeCoercedApply(fun (b : byte[]) ->
            Marshal.Copy(b, 0, gc.AddrOfPinnedObject(), b.Length)
        )

        gc.Free()
        result
    
    type SceneEnv(outputPath : string) =
        let textureFolder = Path.Combine(Path.GetDirectoryName(outputPath), "textures")
        let folder = Path.GetDirectoryName outputPath

        member x.Folder = folder
        member x.OutputPath = outputPath
        member x.TextureFolder = textureFolder


    [<AutoOpen>]
    module private Extensions =
        type ISg with
            member x.AssimpMeshes() : HashSet<Mesh> = x?AssimpMeshes()
            member x.AssimpMaterials() : HashSet<Material> = x?AssimpMaterials()
            member x.AssimpNode() : Node = x?AssimpNode()
            member x.SceneEnv : SceneEnv = x?SceneEnv

        type SceneEnv with
            member x.AssimpScene() : Scene = x?AssimpScene()
            member x.AssimpMeshes() : HashSet<Mesh> = x?AssimpMeshes()
            member x.AssimpMaterials() : HashSet<Material> = x?AssimpMaterials()

    let meshCache = Dictionary()
    let materialCache = Dictionary()
    let emptyMaterial = Material()

    let toAssimpMode (m : IndexedGeometryMode) =
        match m with
            | IndexedGeometryMode.PointList -> Assimp.PrimitiveType.Point
            | IndexedGeometryMode.LineList -> Assimp.PrimitiveType.Line
            | IndexedGeometryMode.TriangleList -> Assimp.PrimitiveType.Triangle
            | _ -> failwithf "unsupported primtive mode: %A" m

    let createMesh (materialIndex : int) (info : DrawCallInfo) (indices : Array) (data : list<Symbol * Array>) =
        let key = (materialIndex, info,indices,data)
        match meshCache.TryGetValue key with
            | (true, v) -> v
            | _ ->
                let mesh = Assimp.Mesh(toAssimpMode info.Mode)
                let mutable pos = null

                for (s,array) in data do
                    match s.ToString() with
                        | "Positions" -> 
                            pos <- array
                            mesh.Vertices.AddRange(toArrayOf array)
                        | "Normals" -> mesh.Normals.AddRange(toArrayOf array)
                        | "DiffuseColorCoordinates" -> mesh.TextureCoordinateChannels.[0].AddRange(toArrayOf<Vector2D> array |> Array.map (fun a -> Vector3D(a.X, a.Y, 0.0f)))
                        | _ -> ()

                if indices <> null then
                    for i in 0..3..indices.Length-1 do
                        let i0 = indices.GetValue(i+0) |> Convert.ToInt32
                        let i1 = indices.GetValue(i+1) |> Convert.ToInt32
                        let i2 = indices.GetValue(i+2) |> Convert.ToInt32
                        mesh.Faces.Add(Face [|i0;i1;i2|])
                else
                    for i in 0..3..pos.Length do
                        mesh.Faces.Add(Face [|i+0;i+1;i+2|])

                mesh.MaterialIndex <- materialIndex

                meshCache.[key] <- mesh
                mesh

    let createMaterial (e : SceneEnv) (u : IUniformProvider) : Material = 
        match u.TryGetUniform(Ag.emptyScope, DefaultSemantic.DiffuseColorTexture) with
            | Some (:? IMod<ITexture> as t) ->
                match materialCache.TryGetValue t with
                    | (true, r) -> r
                    | _ ->
                        let res = Material()
 
                        if not <| Directory.Exists e.Folder then
                            Directory.CreateDirectory e.Folder |> ignore

                        if not <| Directory.Exists e.TextureFolder then
                            Directory.CreateDirectory e.TextureFolder |> ignore


                        match t.GetValue() with
                            | :? FileTexture as f ->
                                let name = Path.ChangeExtension(Path.GetFileName f.FileName, ".png")
                                let outputPath = Path.Combine(e.TextureFolder, name)

                                let pi = PixImage.Create(f.FileName)
                                pi.SaveAsImage(outputPath)
                                //File.Copy(f.FileName, outputPath, true)
                                res.AddProperty(MaterialProperty("$tex.file", "textures/" + name, TextureType.Diffuse, 0)) |> ignore

                                //res.TextureDiffuse <- TextureSlot(f.FileName, TextureType.Diffuse, 0, TextureMapping.FromUV, 0, 1.0f, TextureOperation.Add, TextureWrapMode.Clamp, TextureWrapMode.Clamp, 0)
                            | :? PixTexture2d as p ->
                                let name = Guid.NewGuid().ToString() + ".png"
                                let outputPath = Path.Combine(e.TextureFolder, name)

                                p.PixImageMipMap.[0].SaveAsImage(outputPath)
                                res.AddProperty(MaterialProperty("$tex.file", "textures/" + name, TextureType.Diffuse, 0)) |> ignore

                            | _ -> ()
                    
                        materialCache.[t] <- res
                        res
            | _ -> 
                emptyMaterial


//    [<Semantic>]
//    type Semantics() =
//
//        member x.AssimpNode(e : Sg.RenderNode) =
//            let env = e.SceneEnv
//            let meshIndices = e.AssimpMeshes() |> Seq.map env.GetMeshIndex |> Seq.toArray
//            let res = Node(Ag.getContext().Path)
//            res.MeshIndices.AddRange meshIndices
//            res
//
//        member x.AssimpNode(a : IApplicator) =
//            a.Child.GetValue().AssimpNode()
//
//        member x.AssimpNode(g : Sg.Group) =
//            let content = g.ASet |> ASet.toList |> List.map (fun c -> c.AssimpNode()) |> List.toArray
//            let res = Node(Ag.getContext().Path)
//            res.Children.AddRange content
//            res
//
//        member x.AssimpNode(g : Sg.Set) =
//            let content = g.ASet |> ASet.toList |> List.map (fun c -> c.AssimpNode()) |> List.toArray
//            let res = Node(Ag.getContext().Path)
//            res.Children.AddRange content
//            res
//
//        member x.AssimpNode(e : Sg.Environment) =
//            let c = e.Scene.GetValue()
//            c.AssimpNode()
//
//        member x.AssimpNode(t : Sg.TrafoApplicator) =
//            let res = Node(Ag.getContext().Path)
//            res.Children.Add (t.Child.GetValue().AssimpNode())
//            let fw = t.Trafo.GetValue().Forward |> M44f.op_Explicit
//
//            res.Transform <- 
//                Matrix4x4(
//                    fw.M00, fw.M01, fw.M02, fw.M03,
//                    fw.M10, fw.M11, fw.M12, fw.M13,
//                    fw.M20, fw.M21, fw.M22, fw.M23,
//                    fw.M30, fw.M31, fw.M32, fw.M33
//                )
//
//            res
//
//        member x.SceneEnv(e : SceneEnv) =
//            e.Scene?SceneEnv <- e
//
//        member x.AssimpScene(e : SceneEnv) =
//            let scene = Scene()
//            scene.Meshes.AddRange e.Meshes
//            scene.Materials.AddRange e.Materials
//
//            let node = e.Scene.AssimpNode()
//            scene.RootNode <- node
//
//            scene


    type ExportFormat =
        { extension : string
          formatId : string
          description : string
        }    

    let private ctx = new AssimpContext()
    let supportedFormas =
        ctx.GetSupportedExportFormats()
            |> Seq.map (fun d -> { extension = d.FileExtension; formatId = d.FormatId; description = d.Description })
            |> Seq.map (fun d -> sprintf ".%s" d.extension, d)
            |> Map.ofSeq

    type Tree =
        | TrafoNode of Trafo3d * Tree
        | GroupNode of list<Tree>
        | LeafNode of RenderObject


    let buildTree (renderObjects : list<RenderObject>) : Tree =

        let rec build (nextLevel : Dictionary<Scope, Tree>) (finished : List<Tree>) =
            if nextLevel.Count = 0 then
                let all = finished |> Seq.toList
                match all with
                    | [a] -> a
                    | a -> GroupNode a
            else
                let parents = 
                    nextLevel 
                        |> Seq.choose (fun (KeyValue(s,t)) -> 
                            match s.parent with
                                | Some p -> Some (p,t)
                                | None -> None
                        ) 
                        |> Seq.groupBy fst
                        |> Seq.map (fun (g,e) -> g, e |> Seq.map snd |> Seq.toList)
                        |> Dictionary.ofSeq

                let trees =
                    parents
                        |> Dictionary.toSeq
                        |> Seq.map (fun (p,children) ->
                            let childTree =
                                match children with
                                    | [c] -> c
                                    | _ -> GroupNode children

                            match p.source with
                                | :? Sg.TrafoApplicator as t ->
                                    p, TrafoNode(t.Trafo.GetValue(), childTree)
                                | _ ->
                                    p, childTree
                        )

                let (finishedTrees, trees) = trees |> Seq.partition (fun (s,c) -> s.parent.IsSome)
                let finishedTrees = finishedTrees |> Seq.toList
                let trees = trees |> Seq.toList

                finished.AddRange (finishedTrees |> Seq.map snd)

                let treeDict = trees |> Dictionary.ofSeq
                build treeDict finished

        let leafs = renderObjects |> List.map (fun rj -> rj.AttributeScope |> unbox<Ag.Scope>, LeafNode rj) |> Dictionary.ofList

        build leafs (List())

    let saveRenderObjects (file : string) (renderObjects : list<RenderObject>) =
        let env = SceneEnv file

        let renderObjectMeshes = Dictionary<RenderObject, Mesh>()
        let renderObjectMaterials = Dictionary<RenderObject, Material>()
        
        let materials = 
            renderObjects 
                |> Seq.map (fun rj -> 
                    let mat = createMaterial env rj.Uniforms
                    renderObjectMaterials.[rj] <- mat
                    mat
                ) 
                |> Seq.distinct 
                |> Seq.toArray

        let materialIndices = materials |> Array.mapi (fun i m -> (m,i)) |> Dictionary.ofArray

        let meshes = 
            renderObjects
                |> Seq.map (fun rj -> 
                    let index =
                        if rj.Indices <> null then rj.Indices |> Mod.force
                        else null

                    let attributes = 
                        rj.VertexAttributes.All 
                            |> Seq.map (fun (s,b) ->
                                let array =
                                    match b.Buffer |> Mod.force with
                                        | :? ArrayBuffer as ab -> ab.Data
                                        | _ -> failwith "could not get buffer data"
                                s,array
                            )
                            |> Seq.toList

                    let info = rj.DrawCallInfo |> Mod.force

                    let matIndex = materialIndices.[renderObjectMaterials.[rj]]
                    let mesh = createMesh matIndex info index attributes
                    renderObjectMeshes.[rj] <- mesh
                    mesh
                )
                |> Seq.distinct 
                |> Seq.toArray

        let meshIndices = meshes |> Array.mapi (fun i m -> (m,i)) |> Dictionary.ofArray


        let tree = buildTree renderObjects

        let rec getNodes (currentTrafo : Trafo3d) (t : Tree) =
            match t with
                | LeafNode rj -> 
                    let meshIndex = meshIndices.[renderObjectMeshes.[rj]]
                    let node = Node(rj.CreationPath)

                    let trafo = 
                        match rj.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "ModelTrafo") with
                            | Some (:? IMod<Trafo3d> as t) -> t.GetValue()
                            | _ -> currentTrafo

                    let additionalTrafo = currentTrafo * trafo.Inverse

                    if not <| additionalTrafo.Forward.IsIdentity(Constant.PositiveTinyValue) then
                        let trafo = additionalTrafo.Forward |> M44f.op_Explicit
                        node.Transform <- Matrix4x4(
                            trafo.M00, trafo.M01, trafo.M02, trafo.M03,
                            trafo.M10, trafo.M11, trafo.M12, trafo.M13,
                            trafo.M20, trafo.M21, trafo.M22, trafo.M23,
                            trafo.M30, trafo.M31, trafo.M32, trafo.M33
                        )

                    node.MeshIndices.Add meshIndex

                    node
                | GroupNode children ->
                    let n = Node()
                    n.Children.AddRange (children |> List.map (getNodes currentTrafo) |> List.toArray)
                    n
                | TrafoNode(trafo, child) ->
                    let n = Node()
                    n.Children.Add (getNodes (currentTrafo * trafo) child)

                    let trafo = trafo.Forward |> M44f.op_Explicit
                    n.Transform <- Matrix4x4(
                        trafo.M00, trafo.M01, trafo.M02, trafo.M03,
                        trafo.M10, trafo.M11, trafo.M12, trafo.M13,
                        trafo.M20, trafo.M21, trafo.M22, trafo.M23,
                        trafo.M30, trafo.M31, trafo.M32, trafo.M33
                    )

                    n

        let root = getNodes Trafo3d.Identity tree

        let scene = Scene()
        scene.Meshes.AddRange meshes
        scene.Materials.AddRange materials
        scene.RootNode <- root


        if not <| Directory.Exists env.Folder then
            Directory.CreateDirectory env.Folder |> ignore

        let ext = Path.GetExtension file

        let format =
            match Map.tryFind ext supportedFormas with
                | Some d -> d.formatId
                | _ -> failwithf "unknown file extension: %A" ext

        let success = ctx.ExportFile(scene, file, format, PostProcessSteps.None)

        if not success then
            failwithf "could not save scene to %A" file

    let save (file : string) (sg : ISg) =
        saveRenderObjects file (sg.RenderObjects() |> ASet.toList)

    let test() =
        let ig = IndexedGeometry()
        ig.Mode <- IndexedGeometryMode.TriangleList

        ig.IndexedAttributes <- 
            SymDict.ofList [
                DefaultSemantic.Positions,[|V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO|] :> _
                DefaultSemantic.DiffuseColorCoordinates,[|V2f.OO; V2f.IO; V2f.II; V2f.OI|] :> _
            ]

        ig.IndexArray <- [|0;1;2; 0;2;3|] :> Array

        let sg = Sg.ofIndexedGeometry ig
        let sg = 
            [1..10] 
                |> List.map (fun i -> sg |> Sg.trafo (Mod.constant <| Trafo3d.Translation(float i, 0.0, 0.0))) 
                |> Sg.group
                |> Sg.effect []

        save "C:\\Users\\Schorsch\\Desktop\\quadScene\\scene.obj" sg
        System.Environment.Exit 0


