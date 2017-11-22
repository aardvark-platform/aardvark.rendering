namespace Examples


open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO

module Eigi =
    
    // defines functions for loading the model
    module Loader = 
        [<AutoOpen>]
        module LoaderExtensions = 
            open Loader

            type Node with
                member x.SubstituteMaterial (f : Material -> Option<Material>) =
                    match x with
                        | Trafo(t,n) ->
                            Trafo(t, n.SubstituteMaterial f)
                        | Material(m,n) ->
                            let n = n.SubstituteMaterial f
                            match f m with
                                | Some m -> Material(m, n)
                                | None -> Material(m,n)
                        | Leaf m ->
                            Leaf m
                        | Group nodes ->
                            nodes |> List.map (fun n -> n.SubstituteMaterial f) |> Group
                        | Empty ->
                            Empty

            type Scene with
                member x.SubstituteMaterial (f : Material -> Option<Material>) =
                    { x with root = x.root.SubstituteMaterial f }

                member x.AddTextures (repl : list<Symbol * Texture>) =
                    let map = Map.ofList repl
                    x.SubstituteMaterial (fun m -> Some { m with textures = Map.union  m.textures map })
                
        

        let loadEigi (basePath : string) =
            let flags = 
                Assimp.PostProcessSteps.CalculateTangentSpace ||| 
                //Assimp.PostProcessSteps.FixInFacingNormals ||| 
                Assimp.PostProcessSteps.FindDegenerates ||| 
                Assimp.PostProcessSteps.FlipUVs ||| 
                Assimp.PostProcessSteps.Triangulate

            let (~~) n = Path.Combine(basePath, n)
            let (!) n = { Loader.coordIndex = 0; Loader.texture = FileTexture(Path.Combine(basePath, n), TextureParams.mipmapped) :> ITexture }

            let sBody = Loader.Assimp.Load(~~"body.obj", flags)
            let sEyes = Loader.Assimp.Load(~~"eyes.obj", flags)
            let sLowerTeeth = Loader.Assimp.Load(~~"lowerTeeth.obj", flags)
            let sUpperTeeth = Loader.Assimp.Load(~~"upperTeeth.obj", flags)
            let sDrool = Loader.Assimp.Load(~~"drool.obj", flags ||| Assimp.PostProcessSteps.FixInFacingNormals)

            let sBody = 
                sBody.AddTextures [
                    DefaultSemantic.DiffuseColorTexture, !"EigiBodyColor.jpg"
                    DefaultSemantic.NormalMapTexture, !"EigiBody_NORMAL.jpg"
                    DefaultSemantic.SpecularColorTexture, !"EigiBodySpec.jpg"
                ] 
            
            let sEyes = 
                sEyes.AddTextures [
                    DefaultSemantic.DiffuseColorTexture, !"EigiEye_COLOR.jpg"
                    DefaultSemantic.NormalMapTexture, !"EigiEye_NORMAL.jpg"
                ] 

            let sLowerTeeth = 
                sLowerTeeth.AddTextures [
                    DefaultSemantic.DiffuseColorTexture, !"EigiTeeth_COLOR.jpg"
                    DefaultSemantic.NormalMapTexture, !"EigiTeeth_NORMAL.jpg"
                    DefaultSemantic.SpecularColorTexture, !"EigiTeethSpec.jpg"
                ] 
            
            let sUpperTeeth = 
                sUpperTeeth.AddTextures [
                    DefaultSemantic.DiffuseColorTexture, !"EigiTeeth_COLOR.jpg"
                    DefaultSemantic.NormalMapTexture, !"EigiTeeth_NORMAL.jpg"
                    DefaultSemantic.SpecularColorTexture, !"EigiTeethSpec.jpg"
                ] 
    
            let sDrool = 
                sDrool.AddTextures [
                    DefaultSemantic.DiffuseColorTexture, !"EigiDrool_COLOR.png"
                    DefaultSemantic.NormalMapTexture, !"EigiDrool_NORMAL.jpg"
                ] 


            for m in sDrool.meshes do
                match m.geometry.IndexedAttributes.TryGetValue DefaultSemantic.Normals with
                    | (true, (:? array<V3f> as n)) ->
                        m.geometry.IndexedAttributes.[ DefaultSemantic.Normals] <- Array.map ((~-)) n
                    | _ -> 
                        ()

            Sg.ofList [
                Sg.adapter sBody 
                Sg.adapter sEyes 
                Sg.adapter sLowerTeeth 
                Sg.adapter sUpperTeeth 
                Sg.adapter sDrool    
            ]

    // defines all shaders used
    module Shader =
        open FShade

        type Vertex =
            {
                [<Position>]    pos : V4d
                [<Normal>]      n : V3d
                [<BiNormal>]    b : V3d
                [<Tangent>]     t : V3d
                [<TexCoord>]    tc : V2d

                [<LightDir>]    l : V3d
                [<CamDir>]      c : V3d
                [<Color>]       color : V4d
                [<SpecularColor>] spec : V4d
                [<SamplePosition>] sp : V2d
            }

        // define some samplers
        let diffuseColor =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.Anisotropic
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let specularColor =
            sampler2d {
                texture uniform?SpecularColorTexture
                filter Filter.Anisotropic
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let normalMap =
            sampler2d {
                texture uniform?NormalMapTexture
                filter Filter.Anisotropic
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }


        // transform a vertex with all its attributes
        let transform (v : Vertex) =
            vertex {
                let nm = uniform.ModelViewTrafoInv.Transposed
                
                let light = uniform.LightLocation
                let wp = uniform.ModelTrafo.TransformPos(v.pos.XYZ)

                return {
                    pos = uniform.ModelViewProjTrafo * v.pos
                    n = nm.TransformDir v.n
                    b = nm.TransformDir v.b
                    t = nm.TransformDir v.t
                    tc = v.tc
                    sp = v.sp

                    l = uniform.ViewTrafo.TransformDir(light - wp)
                    c = -uniform.ViewTrafo.TransformPos(wp)
                    color = uniform.DiffuseColor
                    spec = uniform.SpecularColor
                }

            }
    
        // change the per-fragment normal according to the NormalMap
        let normalMapping (v : Vertex) =
            fragment {
                let vn = normalMap.Sample(v.tc).XYZ
                let tn = vn * 2.0 - V3d.III |> Vec.normalize
                
                let n = Vec.normalize v.n
                let b = Vec.normalize v.b
                let t = Vec.normalize v.t

                return { v with n = b * tn.X + t * tn.Y +  n * tn.Z }
            }

        // change the per-fragment color using the DiffuseTexture
        let diffuseTexture (v : Vertex) =
            fragment {
                return diffuseColor.Sample(v.tc)
            }

        // change the per-fragment specularColor using the SpecularMap
        let specularTexture (v : Vertex) =
            fragment {
                return { v with spec = specularColor.Sample(v.tc) }
            }

        // apply per-fragment lighting
        let lighting (v : Vertex) =
            fragment {
                let n = Vec.normalize v.n
                let l = Vec.normalize v.l
                let c = Vec.normalize v.c

                let diffuse     = Vec.dot n l |> clamp 0.0 1.0
                let spec        = Vec.dot (Vec.reflect l n) (-c) |> clamp 0.0 1.0

                let diff    = v.color
                let specc   = V3d.III
                let shine   = 32.0


                let color = diff.XYZ * diffuse  +  specc * pow spec shine

                return V4d(color, v.color.W)
            }
    
        // per-fragment alpha test using the current color
        let alphaTest (v : Vertex) =
            fragment {
                let dummy = v.sp.X * 0.0000001
                if v.color.W < 0.05 + dummy then
                    discard()

                return v
            }

    module Skinning = 
        open FShade

        type Vertex =
            {
                [<Position>] pos : V4d
                [<Normal>] n : V3d
                [<Semantic("VertexBoneIndices")>] vbi : V4i
                [<Semantic("VertexBoneWeights")>] vbw : V4d

            }

        type UniformScope with
            member x.Bones : M44d[] = x?StorageBuffer?Bones

        [<ReflectedDefinition; Inline>]
        let boneTrafo (i : int) (w : float) (v : V4d) =
            if i < 0 then v
            else w * (uniform.Bones.[i] * v)
                
        [<ReflectedDefinition>]
        let boneTransform (i : V4i) (w : V4d) (vec : V4d) =
            let mutable res = V4d.Zero

            if i.X >= 0 then 
                res <- res + w.X * (uniform.Bones.[i.X] * vec)

            if i.Y >= 0 then 
                res <- res + w.Y * (uniform.Bones.[i.Y] * vec)

            if i.Z >= 0 then 
                res <- res + w.Z * (uniform.Bones.[i.Z] * vec)

            if i.W >= 0 then 
                res <- res + w.W * (uniform.Bones.[i.W] * vec)

            res

        let skinning (v : Vertex) =
            vertex {
                let m = boneTransform v.vbi v.vbw v.pos
                //let n = boneTransform v.vbi v.vbw (V4d(v.n, 0.0)) |> Vec.xyz
                return { v with pos = m }

            }
                

    module Animation =
        open Loader

        module Shader =
            open FShade

            type Vertex =
                {
                    [<Position>] pos : V4d
                    [<Semantic("InstanceOffset")>] off : V3d
                }

            let instanceOffset (scale : float) (v : Vertex) =
                vertex {
                    return { v with pos = V4d(scale * v.pos.XYZ, v.pos.W) + V4d(v.off, 0.0) }
                }


        type IndexedGeometry with
            member x.FaceVertexCount =
                match x.IndexArray with
                    | null -> (Seq.head x.IndexedAttributes.Values).Length
                    | i -> i.Length

        let sg (frame : IMod<int>) (relevant : Set<string>) (a : Animation) =

            let getPositions (frame : int) =
                let positions = System.Collections.Generic.List<V3d>()
                
                let rec traverse (frame : int) (trafo : M44d) (t : AnimTree) =
                    let frameTrafo =
                        if frame < t.frames.Length then t.frames.[frame]
                        else t.trafo
                    let trafo = trafo * frameTrafo //(t.trafo * frameTrafo)

                    if Set.contains t.name relevant then
                        positions.Add (trafo.TransformPos(V3d.Zero))

                    for c in t.children do
                        traverse frame trafo c

                traverse frame M44d.Identity a.root
                positions.MapToArray(fun v -> V3f v)

            let lineIndices =
                let lines = System.Collections.Generic.List<int>()
                let index = ref 0
                let rec traverse  (parent : int) (t : AnimTree) =
                    
                    let me = 
                        if Set.contains t.name relevant then
                            let me = !index
                            index := !index + 1
                            if parent >= 0 then
                                lines.Add(parent)
                                lines.Add me
                            me
                        else
                            -1
                    
                    for c in t.children do
                        traverse me c

                traverse -1 a.root
                lines.ToArray()

            let positions = 
                frame |> Mod.map (fun frame ->
                    frame |> getPositions |> ArrayBuffer :> IBuffer
                )

            let pos0 = getPositions 0
            if lineIndices.Max() >= pos0.Length then
                failwith "index problem"

            let count = pos0.Length 

            let sphere = Primitives.unitSphere 5
            let call = DrawCallInfo(FaceVertexCount = sphere.FaceVertexCount, InstanceCount = count)

            let joints = 
                Sg.render IndexedGeometryMode.TriangleList call
                    |> Sg.vertexArray DefaultSemantic.Positions sphere.IndexedAttributes.[DefaultSemantic.Positions]
                    |> Sg.vertexArray DefaultSemantic.Normals sphere.IndexedAttributes.[DefaultSemantic.Normals]
                    |> Sg.vertexBufferValue DefaultSemantic.Colors (Mod.constant V4f.IOOI)
                    |> Sg.instanceBuffer (Symbol.Create "InstanceOffset") (BufferView(positions, typeof<V3f>))
                    |> Sg.shader {
                        do! Shader.instanceOffset 0.0005
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.simpleLighting
                    }

            let call = DrawCallInfo(FaceVertexCount = lineIndices.Length, InstanceCount = 1)
            let lines =
                Sg.render IndexedGeometryMode.LineList call
                    |> Sg.vertexBuffer DefaultSemantic.Positions (BufferView(positions, typeof<V3f>))
                    |> Sg.index' lineIndices
                    |> Sg.shader {
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.constantColor C4f.Green
                    }

            Sg.ofList [ joints; lines ]
             
        let getTransformations (a : Animation) (frame : int) =
            let transformations = System.Collections.Generic.Dictionary<string, M44d>()
            let modelTrafos = System.Collections.Generic.Dictionary<string, M44d>()
            let rec traverse (frame : int) (model : M44d) (trafo : M44d) (t : AnimTree) =
                let model = model * t.trafo //(t.trafo * frameTrafo)

                let frameTrafo =
                    if frame < t.frames.Length then t.frames.[frame]
                    else M44d.Identity

                let trafo = trafo *  frameTrafo //(t.trafo * frameTrafo)

                transformations.[t.name] <- trafo
                modelTrafos.[t.name] <- model

                for c in t.children do
                    traverse frame model trafo c

            traverse frame M44d.Identity M44d.Identity a.root
            modelTrafos, transformations


    type Loader.Mesh with
        member x.Transformed(trafo : Trafo3d) =
            let g = x.geometry
            let res = IndexedGeometry()

            res.Mode <- g.Mode
            res.IndexArray <- g.IndexArray
            res.IndexedAttributes <- 
                res.IndexedAttributes |> SymDict.map (fun name value ->
                    if name = DefaultSemantic.Positions then
                        value |> unbox<V3f[]> |> Array.map (fun v -> trafo.Forward.TransformPos (V3d v) |> V3f) :> Array
                    elif name = DefaultSemantic.Normals || name = DefaultSemantic.DiffuseColorUTangents || name = DefaultSemantic.DiffuseColorVTangents then
                        let m = trafo.Backward.Transposed
                        value |> unbox<V3f[]> |> Array.map (fun v -> m.TransformDir (V3d v) |> V3f) :> Array
                    else
                        value
                )

            { x with geometry = res }
    


    // shows the model using a composed shader
    let run() =
        // load the model
        let scene = Loader.Assimp.loadFrom @"C:\Users\Schorsch\Desktop\raptor\test2.dae" (Loader.Assimp.defaultFlags ||| Assimp.PostProcessSteps.FlipUVs)
//
//        let trafo =
//            Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) *
//            Trafo3d.Scale 20.0
//
//        let scene =
//            { scene with meshes = scene.meshes |> Array.map (fun m -> m.Transformed(trafo)) }

        let frame = Mod.init 0
        

        let relevant = scene.meshes |> Seq.collect (fun m -> m.boneNames) |> Set.ofSeq

        let animScene = 
            scene.animantions 
                |> Map.toSeq 
                |> Seq.map (fun (name, tree) -> Animation.sg frame relevant tree) 
                |> Seq.tryHead
                |> Option.defaultValue Sg.empty

        let animation = Mod.init "combinedAnim_0"


        // adjust the bone indices in the meshes
        let mutable globalIndices = HMap.empty
        let mutable currentGlobalIndex = 0

        let getIndex (name : string) (offset : M44d) =
            let key = (name, offset)
            match HMap.tryFind key globalIndices with
                | Some index -> 
                    index
                | None ->
                    let index = currentGlobalIndex
                    currentGlobalIndex <- index + 1
                    globalIndices <- HMap.add key index globalIndices
                    index

        for m in scene.meshes do
            let globalIds = Array.create m.boneNames.Length -1

            let ivec4 (v : int[]) =
                match v.Length with
                    | 0 -> V4i(-1,-1,-1,-1)
                    | 1 -> V4i(v.[0],-1,-1,-1)
                    | 2 -> V4i(v.[0],v.[1],-1,-1)
                    | 3 -> V4i(v.[0],v.[1],v.[2],-1)
                    | _ -> V4i(v.[0],v.[1],v.[2],v.[3])
                    
            let vec4 (v : float32[]) =
                match v.Length with
                    | 0 -> V4f(0.0f, 0.0f, 0.0f, 0.0f)
                    | 1 -> V4f(v.[0], 0.0f, 0.0f, 0.0f)
                    | 2 -> V4f(v.[0], v.[1], 0.0f, 0.0f)
                    | 3 -> V4f(v.[0], v.[1], v.[2], 0.0f)
                    | _ -> V4f(v.[0], v.[1], v.[2], v.[3])

            for bi in 0 .. m.boneNames.Length - 1 do
                let name = m.boneNames.[bi]
                let offset = m.boneOffsets.[bi]

                let gid = getIndex name offset
                globalIds.[bi] <- gid

            match m.geometry.IndexedAttributes.TryGetValue Loader.Mesh.VertexBoneIndices, m.geometry.IndexedAttributes.TryGetValue Loader.Mesh.VertexBoneWeights with
                | (true, (:? array<int[]> as indices)), (true, (:? array<float32[]> as weights)) ->
                    let fourIndices =
                        indices |> Array.map (fun bis ->
                            bis |> Array.filter (fun v -> v >= 0) |> Array.map (fun i -> globalIds.[i]) |> ivec4
                        )

                    let fourWeights =
                        weights |> Array.map (fun ws ->
                            let r = vec4 ws
                            r //r / r.Norm1
                        )

                    m.geometry.IndexedAttributes.[Loader.Mesh.VertexBoneIndices] <- fourIndices
                    m.geometry.IndexedAttributes.[Loader.Mesh.VertexBoneWeights] <- fourWeights

                | _ ->
                    ()




            
            ()

            


            

        // sample the animation at the given frame
        let getBoneTransformations (a : Loader.Animation) (frame : int) =
            let count = currentGlobalIndex
            let arr = Array.create count M44f.Identity

            let model, trafos = Animation.getTransformations a frame

            for ((name, off), index) in HMap.toSeq globalIndices do

                let current =
                    match trafos.TryGetValue name with
                        | (true, current) -> current
                        | _ -> M44d.Identity

                let model = 
                    match model.TryGetValue name with
                        | (true, m) -> m
                        | _ -> M44d.Identity

                let overall = current * off
                arr.[index] <- M44f.op_Explicit overall

            arr

        let boneTrafos =
            adaptive {
                let! anim = animation
                match Map.tryFind anim scene.animantions with
                    | Some a ->
                        return! frame |> Mod.map (getBoneTransformations a)
                    | None ->
                        return [||]
            }
        

        let sg = 
            scene 
                |> Sg.adapter
                |> Sg.uniform "Bones" boneTrafos

                |> Sg.andAlso (Sg.translate 0.0 0.0 0.05 animScene)
                // transform the model (has Y up GL style coords)

                |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Scale 20.0)

                // apply all shaders we have
                |> Sg.shader {
                    do! Skinning.skinning
                    do! Shader.transform
                    do! Shader.diffuseTexture
                    do! Shader.alphaTest
                    //do! Shader.specularTexture
                    //do! Shader.normalMapping
                    //do! Shader.lighting
                }


        let tick =
            async {
                while true do
                    do! Async.Sleep 30
                    transact (fun () -> frame.Value <- (frame.Value + 1) % 36)
                    Log.line "frame: %A" frame.Value
            }

        Async.Start tick


        // run a window showing the scene
        show {
            display Display.Mono
            samples 8
            backend Backend.Vulkan
            debug false
            scene sg
        }

