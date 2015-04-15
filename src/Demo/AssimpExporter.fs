namespace Demo

open System
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.Rendering.GL
open Aardvark.SceneGraph
open Aardvark.SceneGraph.CSharp
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.CSharp
open Assimp
open Assimp.Unmanaged
open System.Runtime.CompilerServices
open System.Collections.Generic
open System.Runtime.InteropServices


module AssimpExporter =
 
    let toArrayOf<'a when 'a : struct> (a : Array) =
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
    
    type SceneEnv(meshes : Mesh[], materials : Material[], sg : ISg) =
        let meshDict = Dictionary.ofSeq (meshes |> Seq.mapi (fun i m -> m,i))
        let materialDict = Dictionary.ofSeq (materials |> Seq.mapi (fun i m -> m,i))

        member x.GetMeshIndex m =
            meshDict.[m]

        member x.GetMaterialIndex m =
            materialDict.[m]

        member x.Meshes = meshes
        member x.Materials = materials
        member s.Scene = sg

    type ISg with
        member x.AssimpMeshes() : HashSet<Mesh> = x?AssimpMeshes()
        member x.AssimpMaterials() : HashSet<Material> = x?AssimpMaterials()
        member x.AssimpNode() : Node = x?AssimpNode()
        member x.SceneEnv : SceneEnv = x?SceneEnv

    type SceneEnv with
        member x.AssimpScene() : Scene = x?AssimpScene()

    [<Semantic>]
    type Semantics() =
        let meshCache = Dictionary()
        let materialCache = Dictionary()
        let emptyMaterial = Material()

        let toAssimpMode (m : IndexedGeometryMode) =
            match m with
                | IndexedGeometryMode.PointList -> Assimp.PrimitiveType.Point
                | IndexedGeometryMode.LineList -> Assimp.PrimitiveType.Line
                | IndexedGeometryMode.TriangleList -> Assimp.PrimitiveType.Triangle
                | _ -> failwithf "unsupported primtive mode: %A" m

        let createMesh (info : DrawCallInfo) (indices : Array) (data : list<Symbol * Array>) =
            let key = (info,indices,data)
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

                    meshCache.[key] <- mesh
                    mesh

        let createMaterial (u : IUniformProvider) : Material = 
            match u.TryGetUniform DefaultSemantic.DiffuseColorTexture with
                | (true, (:? IMod<ITexture> as t)) ->
                    let res = Material()

                    match t.GetValue() with
                        | :? FileTexture as f ->
                           res.TextureDiffuse <- TextureSlot(f.FileName, TextureType.Diffuse, 0, TextureMapping.FromUV, 0, 1.0f, TextureOperation.Add, TextureWrapMode.Clamp, TextureWrapMode.Clamp, 0)
                        | :? PixTexture2d as p ->
                            failwith "sadasd"
                        | _ -> ()
                    
                    
                    res
                | _ -> 
                    emptyMaterial

        member x.AssimpMeshes(r : Sg.RenderNode) =
            let renderJobs = r.RenderJobs() |> ASet.toList

            let meshes =
                renderJobs |> List.map (fun rj ->
                    let index =
                        if rj.Indices <> null then rj.Indices |> Mod.force
                        else null

                    let attributes = 
                        rj.VertexAttributes.All 
                            |> Seq.map (fun (s,b) ->
                                let array =
                                    match b.Buffer with
                                        | :? ArrayBuffer as ab -> ab.Data |> Mod.force
                                        | _ -> failwith "could not get buffer data"
                                s,array
                            )
                            |> Seq.toList

                    let info = rj.DrawCallInfo |> Mod.force

                    createMesh info index attributes
                )
            HashSet meshes

        member x.AssimpMeshes(a : IApplicator) =
            let c = a.Child |> Mod.force
            c.AssimpMeshes()

        member x.AssimpMeshes(g : Sg.Group) =
            let children = g.ASet |> ASet.toSeq
            HashSet (children |> Seq.collect (fun c -> c.AssimpMeshes()))

        member x.AssimpMeshes(g : Sg.Set) =
            let children = g.ASet |> ASet.toSeq
            HashSet (children |> Seq.collect (fun c -> c.AssimpMeshes()))

        member x.AssimpMeshes(e : Sg.Environment) =
            let c = e.Scene.GetValue()
            c.AssimpMeshes()


        member x.AssimpMaterials (e : Sg.RenderNode) =
            let renderJobs = e.RenderJobs() |> ASet.toList
            
            let materials =
                renderJobs |> List.map (fun rj ->
                    createMaterial rj.Uniforms
                )
            HashSet materials

        member x.AssimpMaterials(a : IApplicator) =
            let c = a.Child |> Mod.force
            c.AssimpMaterials()

        member x.AssimpMaterials(g : Sg.Group) =
            let children = g.ASet |> ASet.toSeq
            HashSet (children |> Seq.collect (fun c -> c.AssimpMaterials()))

        member x.AssimpMaterials(g : Sg.Set) =
            let children = g.ASet |> ASet.toSeq
            HashSet (children |> Seq.collect (fun c -> c.AssimpMaterials()))

        member x.AssimpMaterials(e : Sg.Environment) =
            let c = e.Scene.GetValue()
            c.AssimpMaterials()


        member x.AssimpNode(e : Sg.RenderNode) =
            let env = e.SceneEnv
            let meshIndices = e.AssimpMeshes() |> Seq.map env.GetMeshIndex |> Seq.toArray
            let res = Node()
            res.MeshIndices.AddRange meshIndices
            res

        member x.AssimpNode(a : IApplicator) =
            a.Child.GetValue().AssimpNode()

        member x.AssimpNode(g : Sg.Group) =
            let content = g.ASet |> ASet.toList |> List.map (fun c -> c.AssimpNode()) |> List.toArray
            let res = Node()
            res.Children.AddRange content
            res

        member x.AssimpNode(g : Sg.Set) =
            let content = g.ASet |> ASet.toList |> List.map (fun c -> c.AssimpNode()) |> List.toArray
            let res = Node()
            res.Children.AddRange content
            res

        member x.AssimpNode(e : Sg.Environment) =
            let c = e.Scene.GetValue()
            c.AssimpNode()

        member x.AssimpNode(t : Sg.TrafoApplicator) =
            let res = Node()
            res.Children.Add (t.Child.GetValue().AssimpNode())
            let fw = t.Trafo.GetValue().Forward |> M44f.op_Explicit

            res.Transform <- 
                Matrix4x4(
                    fw.M00, fw.M01, fw.M02, fw.M03,
                    fw.M10, fw.M11, fw.M12, fw.M13,
                    fw.M20, fw.M21, fw.M22, fw.M23,
                    fw.M30, fw.M31, fw.M32, fw.M33
                )

            res

        member x.SceneEnv(e : SceneEnv) =
            e.Scene?SceneEnv <- e

        member x.AssimpScene(e : SceneEnv) =
            let scene = Scene()
            scene.Meshes.AddRange e.Meshes
            scene.Materials.AddRange e.Materials

            let node = e.Scene.AssimpNode()
            scene.RootNode <- node

            scene



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
                |> List.map (fun i -> sg |> Sg.trafo (Mod.initConstant <| Trafo3d.Translation(float i, 0.0, 0.0))) 
                |> Sg.group
                |> Sg.effect []

        let meshes = sg.AssimpMeshes() |> Seq.toArray
        let materials = sg.AssimpMaterials() |> Seq.toArray
        let env = SceneEnv(meshes, materials, sg)

        let scene = env.AssimpScene()

//        let scene = Scene()
//        scene.Meshes.AddRange(meshes)
//        scene.Materials.AddRange(materials)

//        let root = Node("sadasd")
//        root.MeshIndices.Add 0
//        scene.RootNode <- root

        use ctx = new AssimpContext()
        let success = ctx.ExportFile(scene, "C:\\Users\\Schorsch\\Desktop\\test.dae", "collada", PostProcessSteps.None)



        printfn "save: %A" success


        for m in meshes do
            printfn "%A" m
        
        ()



