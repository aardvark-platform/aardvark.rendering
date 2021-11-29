namespace Examples


open System
open System.IO
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open FSharp.Data.Adaptive.Operators
open Aardvark.Rendering
open Aardvark.Rendering.ShaderReflection
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
            member x.NumFrames : int = x?NumFrames
            member x.NumBones : int = x?NumBones
            member x.Framerate : float = x?Framerate
            member x.Time : float = x?Time
            member x.FrameRange : V2d = x?FrameRange
            member x.TimeOffset : float = x?TimeOffset
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
            
        [<ReflectedDefinition>]
        let lerp (i : int) (f0 : int) (f1 : int) (t : float) =
            uniform.Bones.[f0 + i] * (1.0 - t) + uniform.Bones.[f1 + i] * t

        [<ReflectedDefinition>]
        let getBoneTransformFrame (i : V4i) (w : V4d) =
            let mutable res = M44d.Zero
            let mutable wSum = 0.0

            let iid = FShade.Imperative.ExpressionExtensions.ShaderIO.ReadInput<int>(Imperative.ParameterKind.Input, FShade.Intrinsics.InstanceId, None)

            let frame = (uniform.Time + uniform.TimeOffset) * uniform.Framerate
            let range = uniform.FrameRange
            let l = range.Y - range.X
            let frame = (l + (frame % l)) % l + range.X

            let f0 = int (floor frame)
            let f1 = ((f0 + 1) % uniform.NumFrames)
            let t = frame - float f0
            let b0 = f0 * uniform.NumBones
            let b1 = f1 * uniform.NumBones


            if i.X >= 0 then 
                res <- res + w.X * lerp i.X b0 b1 t
                wSum <- wSum + w.X

            if i.Y >= 0 then 
                res <- res + w.Y * lerp i.Y b0 b1 t
                wSum <- wSum + w.Y

            if i.Z >= 0 then 
                res <- res + w.Z * lerp i.Z b0 b1 t
                wSum <- wSum + w.Z

            if i.W >= 0 then 
                res <- res + w.W * lerp i.W b0 b1 t
                wSum <- wSum + w.W

            let meshTrafo =
                uniform.Bones.[b0 + uniform.MeshTrafoBone] * (1.0 - t) + uniform.Bones.[b1 + uniform.MeshTrafoBone] * t

            if wSum <= 0.0 then meshTrafo
            else meshTrafo * res

        let skinning (v : Vertex) =
            vertex {
                //let model = uniform.Bones.[uniform.MeshTrafoBone]
                let mat = getBoneTransformFrame v.vbi v.vbw
                //let mat = model * skin

                return { 
                    pos = mat * v.pos
                    n = mat.TransformDir(v.n)
                    b = mat.TransformDir(v.b)
                    t = mat.TransformDir(v.t) 
                    vbi = V4i(-1,-1,-1,-1)
                    vbw = V4d.Zero
                }
            }
                

    // shows the model using a composed shader
    let run() =

        let win = 
            window {
                display Display.Mono
                samples 8
                backend Backend.GL
                debug true
            }

        // load the model
        let scene = Loader.Assimp.loadFrom @"C:\Users\Schorsch\Desktop\raptor\raptor.dae" (Loader.Assimp.defaultFlags)// ||| Assimp.PostProcessSteps.FlipUVs)

        let sw = System.Diagnostics.Stopwatch()
        sw.Start()
        let time = win.Time |> AVal.map (fun _ -> sw.Elapsed.TotalSeconds) //AVal.init 0.0

        let idle    = Range1d(50.0, 100.0)
        let walk    = Range1d(0.0, 36.0)
        let attack  = Range1d(150.0, 180.0)
        let die     = Range1d(200.0, 230.0)

        let arr = [| idle; walk; attack; die |] //; walk; attack; die |]

        let animation = scene.animantions |> Map.toSeq |> Seq.head |> snd

        let allBones =
            [|
                let dt = 1.0 / animation.framesPerSecond
                let mutable t = 0.0
                for f in 0 .. animation.frames - 1 do
                    let trafos = animation.interpolate t
                    yield! trafos |> Array.map M44f.op_Explicit
                    t <- t + dt
            |]

        let numFrames = animation.frames // |> AVal.map (fun (a,_) -> a.frames)
        let numBones = animation.interpolate 0.0 |> Array.length // |> AVal.map (fun (a,_) -> a.interpolate 0.0 |> Array.length)
        let fps = animation.framesPerSecond // |> AVal.map (fun (a,_) -> a.framesPerSecond)

        let trafos =
            let s = V2i(5, 5)
            [|
                for x in -s.X .. s.X do
                    for y in -s.Y .. s.Y do
                        yield Trafo3d.Translation(float x, float y, 0.0)
            |]

        let timeOffsets =
            let rand = RandomSystem()
            Array.init trafos.Length (fun i -> rand.UniformFloat() * 3.0f) :> System.Array |> AVal.constant

        let frameRanges =
            let rand = RandomSystem()
            Array.init trafos.Length (fun i -> 
                let range = arr.[rand.UniformInt(arr.Length)]
                V2f(float32 range.Min, float32 range.Max)
            ) :> System.Array |> AVal.constant

        let instanced (trafos : aval<Trafo3d[]>) (sg : ISg) =
            let trafos = trafos |> AVal.map (fun t -> t :> System.Array)
            let uniforms =
                Map.ofList [
                    "ModelTrafo", (typeof<Trafo3d>, trafos)
                    "TimeOffset", (typeof<float32>, timeOffsets)
                    "FrameRange", (typeof<V2f>, frameRanges)
                ]
            Sg.instanced' uniforms sg

        let sg = 
            scene 
                |> Sg.adapter
                |> Sg.uniform "Bones" ~~allBones
                |> Sg.uniform "NumFrames" ~~numFrames
                |> Sg.uniform "NumBones" ~~numBones
                |> Sg.uniform "Framerate" ~~fps
                |> Sg.uniform "Time" time
                |> Sg.uniform "FrameRange" ~~(V2d(0.0, 36.0))
                |> Sg.uniform "TimeOffset" ~~0.0

                |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) * Trafo3d.Scale 20.0)

                //|> instanced (AVal.constant trafos)

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

        win.Scene <- sg
        win.Run()


