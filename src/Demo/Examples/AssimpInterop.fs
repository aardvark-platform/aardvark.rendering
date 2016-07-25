#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#r "AssimpNet.dll"
#else
namespace Examples
#endif


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive

open Default 

open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application


// this module demonstrates how to extend a given scene graph system (here the one provided by assimp) with
// semantic functions to be used in our rendering framework
module Assimp =
    open Assimp
    open System.Runtime.CompilerServices
    open System.Collections.Generic

    open Aardvark.Base.Ag
    open Aardvark.SceneGraph.Semantics

    
    // for convenience we define some extension
    // functions accessing Attributes type-safely
    type Node with
        member x.Scene : Scene = x?AssimpScene

        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()

        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()

    type Scene with
        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()


    // in order to integrate Assimo's scene structure we
    // need to define several attributes for it (namely RenderObjects, ModelTrafo, etc.)
    // which can be done using s single or multiple semantic-types. 
    // Since the implementation is relatively dense we opted for only one type here.
    [<Semantic>]
    type AssimpSemantics() =

        // semantics may define local fields/functions as needed

        // since caching is desirable for multiple occurances of the
        // identical mesh we use a ConditionalWeakTable here.
        // NOTE that the ISg representations will be kept alive as
        //      long as the input Mesh is alive.
        let cache = ConditionalWeakTable<Mesh, ISg>()


        let textureCache = Dictionary<string, IMod<ITexture>>()

        // mapAttribute takes attributes as defined by assimp and converts 
        // them using a given function. Furthermore the result is upcasted to
        // System.Array and wrapped up in a constant mod-cell.
        // Note that the conversion is applied lazily
        let mapAttribute (f : 'a -> 'b) (l : List<'a>) =
            let data = 
                Mod.delay(fun () ->
                    let arr = Array.zeroCreate l.Count
                    for i in 0..l.Count-1 do
                        arr.[i] <- f l.[i]

                    arr :> Array
                )
            BufferView(data |> Mod.map (fun data -> ArrayBuffer data :> IBuffer), typeof<'b>)

        // try to find the Mesh's diffuse texture
        // and convert it into a file-texture.
        // Finally cache texture-mods per path helping the
        // backend to identify equal textures
        let tryFindDiffuseTexture (m : Mesh) =

            let scene : Scene = m?AssimpScene
            let workDirectory : string = scene?WorkDirectory

            if m.MaterialIndex >= 0 then 
                let mat = scene.Materials.[m.MaterialIndex]
                if mat.HasTextureDiffuse then
                    let file = mat.TextureDiffuse.FilePath

                    let file =
                        file.Replace('/', System.IO.Path.DirectorySeparatorChar)
                            .Replace('\\', System.IO.Path.DirectorySeparatorChar)

                    let file =
                        if file.Length > 0 && file.[0] = System.IO.Path.DirectorySeparatorChar then
                            file.Substring(1)
                        else
                            file

                    let path = System.IO.Path.Combine(workDirectory, file)
                                
                    match textureCache.TryGetValue path with
                        | (true, tex) -> 
                            Some tex
                        | _ ->
                            let tex = FileTexture(path, true)
                            let m = Mod.constant (tex :> ITexture)
                            textureCache.[path] <- m
                            Some m
                else
                    None
            else 
                None


        
        // toSg is used by toRenderObjects in order to simplify 
        // things here.
        // Note that it would also be possible here to create RenderObjects 
        //      directly. However this is very wordy and we therefore create
        //      a partial SceneGraph for each mesh
        let toSg (m : Mesh) =
            match cache.TryGetValue m with
                | (true, sg) -> 
                    sg
                | _ ->
                    if m.HasFaces && m.FaceCount > 0 && m.PrimitiveType = PrimitiveType.Triangle then
                        let scene : Scene  = m?AssimpScene
                        let indexArray = m.GetIndices()

                        let vertexCount = m.Vertices.Count

                        // convert all attributes present in the mesh
                        // Note that all conversions are performed lazily here
                        //      meaning that attributes which are not required for rendering
                        //      will not be converted
                        let attributes =
                            SymDict.ofList [
                        
                                if m.Vertices <> null then
                                    let bb = Box3d(m.Vertices |> Seq.map (fun v -> V3d(v.X, v.Y, v.Z)))
                                    yield DefaultSemantic.Positions, m.Vertices |> mapAttribute (fun v -> V3f(v.X, v.Y, v.Z))
                        
                                if m.Normals <> null then
                                    if m.Normals.Count = m.Vertices.Count then
                                        yield DefaultSemantic.Normals, m.Normals |> mapAttribute (fun v -> V3f(v.X, v.Y, v.Z))
                                    else
                                        yield DefaultSemantic.Normals, BufferView(Mod.constant (NullBuffer(V4f.OOOO) :> IBuffer), typeof<V4f>)
                                
                                if m.TextureCoordinateChannelCount > 0 then
                                    let tc = m.TextureCoordinateChannels.[0]
                                    yield DefaultSemantic.DiffuseColorCoordinates, tc |> mapAttribute (fun v -> V2f(v.X, v.Y))

                            ]

                        // try to find the Mesh's diffuse texture
                        let diffuseTexture = tryFindDiffuseTexture m

                        
                        // if the mesh is indexed use its index for determinig the
                        // total face-vertex-count. otherwise use any 
                        let faceVertexCount =
                            if indexArray <> null then
                                indexArray.Length
                            else
                                vertexCount

                        if faceVertexCount = 0 || faceVertexCount % 3 <> 0 then
                            let sg = Sg.group' []
                            cache.Add(m, sg)
                            sg
                        else

                            // create a partial SceneGraph containing only the information
                            // provided by the Mesh itself. Note that this SceneGraph does not 
                            // include Surfaces/Textures/etc. but will automatically inherit those
                            // attributes from its containing scope.
                            let sg = 
                                Sg.VertexAttributeApplicator(attributes,
                                    Sg.RenderNode(
                                        DrawCallInfo(
                                            FaceVertexCount = faceVertexCount,
                                            InstanceCount = 1
                                        ),
                                        IndexedGeometryMode.TriangleList
                                    )
                                ) :> ISg

                            let sg =
                                if indexArray <> null then
                                    Sg.VertexIndexApplicator(BufferView.ofArray indexArray, sg) :> ISg
                                else
                                    sg

                            // if a diffuse texture was found apply it using the
                            // standard SceneGraph
                            let sg = 
                                match diffuseTexture with
                                    | Some tex ->
                                        sg |> Sg.texture DefaultSemantic.DiffuseColorTexture tex
                                    | None ->
                                        sg

                            cache.Add(m, sg)
                            sg
                    else
                        let sg = Sg.group' []
                        cache.Add(m, sg)
                        sg

        // since Meshes need to be converted to RenderObjects somehow we
        // define a utility-function performing this transformation.
        // Note that RenderObjects cannot be cached per Mesh since they
        //      can differ when seen in different paths
        let toRenderObjects (m : Mesh) =
            (toSg m).RenderObjects()

        // another utility function for converting
        // transformation matrices
        let toTrafo (m : Matrix4x4) =
            let m = 
                M44d(
                    float m.A1, float m.A2, float m.A3, float m.A4,
                    float m.B1, float m.B2, float m.B3, float m.B4,
                    float m.C1, float m.C2, float m.C3, float m.C4,
                    float m.D1, float m.D2, float m.D3, float m.D4
                )

            Trafo3d(m, m.Inverse)

        // when the attribute is not defined for the scene itself
        // we simply use the current directory as working directory
        member x.WorkDirectory(scene : Scene) =
            scene.AllChildren?WorkDirectory <- System.Environment.CurrentDirectory

        // since the Assimp-Nodes need to be aware of their
        // enclosing Scene (holding Meshes/Textures/etc.) we
        // simply define an inherited attribute for passing it down
        // the tree
        member x.AssimpScene(scene : Scene) =
            scene.AllChildren?AssimpScene <- scene

        // the inherited attribute ModelTrafo will be modified
        // by some of Assimp's nodes and must therefore be defined here.
        // Note that this code is quite compilcated since we want to efficiently
        //      filter out identity trafos here and also want to be aware of the system's 
        //      overall root-trafo (which is Identity too)
        member x.ModelTrafoStack (n : Node) =
            let p : list<IMod<Trafo3d>> = n?ModelTrafoStack
            let mine = n.Transform |> toTrafo

            // in general the following code would be sufficient (but not optimal):
            // n.AllChildren?ModelTrafo <- Mod.map (fun t -> t * mine) p

            if mine.Forward.IsIdentity(Constant.PositiveTinyValue) then
                n.AllChildren?ModelTrafoStack <- p
            else
                n.AllChildren?ModelTrafoStack <- (Mod.constant mine)::p

        member x.ModelTrafo(e : Node) : IMod<Trafo3d> =
            let sg = Sg.set (ASet.empty)
            sg?ModelTrafo()

        // here we define the RenderObjects semantic for the Assimp-Scene
        // which directly queries RenderObjects from its contained Scene-Root
        member x.RenderObjects(scene : Scene) : aset<IRenderObject> =
            scene.RootNode?RenderObjects()

        // here we define the RenderObjects semantic for Assimp's Nodes
        // which basically enumerates all directly contained 
        // Geometries and recursively yields all child-renderjobs
        member x.RenderObjects(n : Node) : aset<IRenderObject> =
            aset {
                // get the inherited Scene attribute (needed for Mesh lookups here)
                let scene = n.Scene

                // enumerate over all meshes and yield their 
                // RenderObjects (according to the current scope)
                for i in n.MeshIndices do
                    let mesh = scene.Meshes.[i]
                    
                    yield! toRenderObjects mesh

                // recursively yield all child-renderjobs
                for c in n.Children do
                    yield! c.RenderObjects()

            }

        member x.LocalBoundingBox(s : Scene) : IMod<Box3d> =
            s.RootNode.LocalBoundingBox()

        member x.LocalBoundingBox(n : Node) : IMod<Box3d> =
            adaptive {
                let scene = n.Scene
                let meshes = n.MeshIndices |> Seq.map (fun i -> scene.Meshes.[i]) |> Seq.toList
                let trafo = toTrafo n.Transform

                let box = Box3d(meshes |> Seq.collect (fun m -> m.Vertices |> Seq.map (fun v -> V3d(v.X, v.Y, v.Z))))
                let childBoxes = Box3d(n.Children |> Seq.map (fun c -> c.LocalBoundingBox().GetValue()))

                let overall = Box3d [box; childBoxes]
                return overall.Transformed(trafo)
            }

    [<AutoOpen>]
    module Conversion =
        type Trafo3d with
            static member ChangeYZ =
                let fw =
                    M44d(1.0,0.0,0.0,0.0,
                         0.0,0.0,-1.0,0.0,
                         0.0,1.0,0.0,0.0,
                         0.0,0.0,0.0,1.0)

                Trafo3d(fw, fw)

    let private ctx =  new AssimpContext()

    // define a default checkerboard texture
    let private defaultTexture =
        let image = PixImage<byte>(Col.Format.RGBA, 128L, 128L, 4L)

        image.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 32L
            if (c.X + c.Y) % 2L = 0L then
                C4b.VRVisGreen
            else
                C4b.White
        ) |> ignore

        let tex =
            PixTexture2d(PixImageMipMap [|image :> PixImage|], true) :> ITexture

        Mod.constant tex


    // a scene can simply be loaded using assimp.
    // Due to our semantic-definitions above we may simply use
    // it in our SceneGraph (which allows extensibility through its AdapterNode)
    let load (file : string) =
        let scene = ctx.ImportFile(file, PostProcessSteps.Triangulate ||| PostProcessSteps.FixInFacingNormals ||| 
                                         PostProcessSteps.GenerateSmoothNormals ||| PostProcessSteps.ImproveCacheLocality ||| 
                                         PostProcessSteps.JoinIdenticalVertices ||| PostProcessSteps.SplitLargeMeshes)

        // the attribute system can also be used to extend objects
        // with "properties" wich are scope independent.
        scene?WorkDirectory <- System.IO.Path.GetDirectoryName(file)

        Sg.AdapterNode(scene) |> Sg.diffuseTexture defaultTexture



module AssimpInterop = 
    open Aardvark.SceneGraph.Semantics
    open Assimp

    FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])
    System.Environment.CurrentDirectory <- Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"]
    DynamicLinker.tryUnpackNativeLibrary "Assimp" |> ignore

    let model = Assimp.load (Path.combine [__SOURCE_DIRECTORY__;"..";"Demo";"eigi";"eigi.dae" ])


    let sg =
        model 
            |> Helpers.normalizeTo ( Box3d(-V3d.III, V3d.III) )
            |> Sg.trafo (Mod.constant Trafo3d.ChangeYZ)
            |> Sg.blendMode (Mod.constant Rendering.BlendMode.Blend)
            |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect                  
                    DefaultSurfaces.constantColor C4f.Red |> toEffect  
                    DefaultSurfaces.simpleLighting |> toEffect
                    DefaultSurfaces.diffuseTexture |> toEffect
                ]
            |> Sg.camera ( defaultCamera  () ) 

    let run () =
        FsiSetup.initFsi (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug";"Examples.exe"])
        setSg sg
        win.Run()

open AssimpInterop

#if INTERACTIVE
setSg sg
printfn "Done. Modify sg and call setSg again in order to see the modified rendering result."
#else
#endif