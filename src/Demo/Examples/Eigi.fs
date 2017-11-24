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
                let light = uniform.LightLocation
                let wp = uniform.ModelTrafo.TransformPos(v.pos.XYZ)

                return {
                    pos = uniform.ModelViewProjTrafo * v.pos
                    n = uniform.ModelViewTrafoInv.TransposedTransformDir v.n
                    b = uniform.ModelViewTrafo.TransformDir v.b
                    t = uniform.ModelViewTrafo.TransformDir v.t
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
                let specc   = v.spec.XYZ
                let shine   = uniform.Shininess


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
                [<Position>]    pos : V4d
                [<Normal>]      n : V3d
                [<BiNormal>]    b : V3d
                [<Tangent>]     t : V3d

                [<Semantic("VertexBoneIndices4")>] vbi : V4i
                [<Semantic("VertexBoneWeights4")>] vbw : V4d

            }

        type UniformScope with
            member x.Bones : M44d[] = x?StorageBuffer?Bones
            member x.MeshTrafoBone : int = x?MeshTrafoBone

        [<ReflectedDefinition>]
        let getBoneTransform (i : V4i) (w : V4d) =
            let mutable res = M44d.Zero
            let mutable wSum = 0.0

            if i.X >= 0 then 
                res <- res + w.X * uniform.Bones.[i.X]
                wSum <- wSum + w.X

            if i.Y >= 0 then 
                res <- res + w.Y * uniform.Bones.[i.Y]
                wSum <- wSum + w.Y

            if i.Z >= 0 then 
                res <- res + w.Z * uniform.Bones.[i.Z]
                wSum <- wSum + w.Z

            if i.W >= 0 then 
                res <- res + w.W * uniform.Bones.[i.W]
                wSum <- wSum + w.W


            let id = M44d.Identity
            if wSum >= 1.0 then res
            elif wSum <= 0.0 then id
            else (1.0 - wSum) * id + wSum * res

        let skinning (v : Vertex) =
            vertex {
                let model = uniform.Bones.[uniform.MeshTrafoBone]
                let skin = getBoneTransform v.vbi v.vbw
                let mat = model * skin

                return { 
                    pos = mat * v.pos
                    n = mat.TransformDir(v.n)
                    b = mat.TransformDir(v.b)
                    t = mat.TransformDir(v.t) 
                    vbi = V4i(-1,-1,-1,-1)
                    vbw = V4d.Zero
                }
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

        let sg (frame : IMod<int>) (a : Animation) =

            let getPositions (frame : int) =
                let positions = System.Collections.Generic.List<V3d>()
                
                let rec traverse (frame : int) (trafo : M44d) (t : AnimTree) =
                    let frameTrafo =
                        if frame < t.frames.Length then t.frames.[frame]
                        else t.trafo
                    let trafo = trafo * frameTrafo //(t.trafo * frameTrafo)

                    positions.Add (trafo.TransformPos(V3d.Zero))

                    for c in t.children do
                        traverse frame trafo c

                traverse frame M44d.Identity a.root
                positions.MapToArray(fun v -> V3f v)

            let lineIndices =
                let lines = System.Collections.Generic.List<int>()
                let index = ref 0
                let rec traverse  (parent : int) (t : AnimTree) =
                    
                    let me = !index
                    index := !index + 1
                    if parent >= 0 then
                        lines.Add(parent)
                        lines.Add me
                    
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

        let time = Mod.init 0.0
        

//        let relevant = scene.meshes |> Seq.collect (fun m -> m.boneNames) |> Set.ofSeq
//      

     

        let idle    = Range1d(50.0, 100.0)
        let walk    = Range1d(0.0, 36.0)
        let attack  = Range1d(150.0, 180.0)
        let die     = Range1d(200.0, 230.0)

        let arr = [| idle; walk; attack; die |] //; walk; attack; die |]
        let mutable index = 0

        let animation = Mod.init ("combinedAnim_0", arr.[index])


        let nextAnimation() =
            index <- (index + 1) % arr.Length
            let range = arr.[index]
            transact (fun () -> animation.Value <- ("combinedAnim_0", range))

        let frame = 
            Mod.map2 (fun time (name : string, range : Range1d) -> 
                match Map.tryFind name scene.animantions with
                    | Some a ->
                        let frame = (time * a.framesPerSecond) % (range.Max - range.Min) + range.Min
                        int frame
                    | None ->
                        0
            ) time animation

        let animScene = 
            scene.animantions 
                |> Map.toSeq 
                |> Seq.map (fun (name, tree) -> Animation.sg frame tree) 
                |> Seq.tryHead
                |> Option.defaultValue Sg.empty

        let getBoneTransformations (a : Loader.Animation) (range : Range1d) (time : float) =
            let frame = (time * a.framesPerSecond) % (range.Max - range.Min) + range.Min
            let time = frame / a.framesPerSecond
            //Log.line "time: %.3f" time
            a.interpolate time |> Array.map M44f.op_Explicit

        let distanceToLine (p0 : V3d) (p1 : V3d) (v : V3d) =
            let dir = p1 - p0
            sqrt ( Vec.lengthSquared (Vec.cross dir (v - p0)) / Vec.lengthSquared dir )

        let boneTrafos =
            adaptive {
                let! (name, range) = animation
                match Map.tryFind name scene.animantions with
                    | Some a ->
                        return! time |> Mod.map (getBoneTransformations a range)
                    | None ->
                        return [||]
            }

        let rec removeTrafos (n : Loader.Node) =
            match n with
                | Loader.Empty -> Loader.Empty
                | Loader.Group ns -> Loader.Group (List.map removeTrafos ns)
                | Loader.Leaf _ -> n
                | Loader.Material(m,n) -> Loader.Material(m, removeTrafos n)
                | Loader.Trafo(_,n) -> removeTrafos n
        
        //let scene = { scene with root = removeTrafos scene.root }

        let trafos =
            let s = V2i(12, 50)
            [|
                for x in -s.X .. s.X do
                    for y in -s.Y .. s.Y do
                            yield Trafo3d.Translation(float x, float y, 0.0)
            |]

        let sg = 
            scene 
                |> Sg.adapter
                |> Sg.uniform "Bones" boneTrafos

                //|> Sg.andAlso (Sg.translate 0.0 0.0 0.05 animScene)
                // transform the model (has Y up GL style coords)

                |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Scale 20.0)
                |> Sg.instanced (Mod.constant trafos)
                // apply all shaders we have
                |> Sg.shader {
                    do! Skinning.skinning
                    do! Shader.transform
                    do! Shader.diffuseTexture
                    do! Shader.alphaTest
                    do! Shader.specularTexture
                    do! Shader.normalMapping
                    do! Shader.lighting
                }
//
//
//
//
//        let sg =
//            Sg.ofList [
//                //Sg.sphere' 5 C4b.Red 0.5
//                Sg.cylinder' 16 C4b.Red 0.04 0.8
//                    |> Sg.transform (Trafo3d.FromBasis(V3d.OIO, V3d.OOI, V3d.IOO, V3d.Zero))
//
//                Sg.cylinder' 16 C4b.Green 0.04 0.8
//                    |> Sg.transform (Trafo3d.FromBasis(V3d.OOI, V3d.IOO, V3d.OIO, V3d.Zero))
//
//                Sg.cylinder' 16 C4b.Blue 0.04 0.8
//                    |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OIO, V3d.OOI, V3d.Zero))
//
//                Sg.cone' 16 C4b.Blue 0.08 0.2
//                    |> Sg.translate 0.0 0.0 0.8
//
//                Sg.cone' 16 C4b.Red 0.08 0.2
//                    |> Sg.translate 0.0 0.0 0.8
//                    |> Sg.transform (Trafo3d.FromBasis(V3d.OIO, V3d.OOI, V3d.IOO, V3d.Zero))
//
//                Sg.cone' 16 C4b.Green 0.08 0.2
//                    |> Sg.translate 0.0 0.0 0.8
//                    |> Sg.transform (Trafo3d.FromBasis(V3d.OOI, V3d.IOO, V3d.OIO, V3d.Zero))
//            ]
//            |> Sg.scale 0.3
//            |> Sg.instanced (Mod.constant trafos)
//            |> Sg.shader  {
//                do! DefaultSurfaces.trafo
//                do! DefaultSurfaces.simpleLighting
//            }


        let sw = System.Diagnostics.Stopwatch.StartNew()

        

        let tick =
            async {
                while true do
                    do! Async.Sleep 30
                    let t = sw.Elapsed.TotalSeconds
                    transact (fun () -> time.Value <- t)

                    if t > 15.0 then
                        nextAnimation()
                        sw.Restart()
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

