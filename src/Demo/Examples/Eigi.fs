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

    module Shader =
        open FShade

        type LightDirAttribute() = inherit FShade.SemanticAttribute("LightDirection")
        type CamDirAttribute() = inherit FShade.SemanticAttribute("CameraDirection")

        type UniformScope with
            member x.AmbientColor : V4d = x?Material?AmbientColor
            member x.DiffuseColor : V4d = x?Material?DiffuseColor
            member x.EmissiveColor : V4d = x?Material?EmissiveColor
            member x.ReflectiveColor : V4d = x?Material?ReflectiveColor
            member x.SpecularColor : V4d = x?Material?SpecularColor
            member x.Shininess : float = x?Material?Shininess
            member x.BumpScale : float = x?Material?BumpScale


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

                [<SamplePosition>] sp : V2d
            }

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
                }

            }
    

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

        let normalMapping (v : Vertex) =
            fragment {
                let vn = normalMap.Sample(v.tc).XYZ
                let tn = vn * 2.0 - V3d.III |> Vec.normalize
                
                let n = Vec.normalize v.n
                let b = Vec.normalize v.b
                let t = Vec.normalize v.t

                return { v with n = b * tn.X + t * tn.Y +  n * tn.Z }
            }

        let diffuseTexture (v : Vertex) =
            fragment {
                return diffuseColor.Sample(v.tc)
            }

        let lighting (v : Vertex) =
            fragment {
                let n = Vec.normalize v.n
                let l = Vec.normalize v.l
                let c = Vec.normalize v.c

                let diffuse     = Vec.dot n l |> clamp 0.0 1.0
                let spec        = Vec.dot (Vec.reflect l n) (-c) |> clamp 0.0 1.0

                let diff    = v.color
                let specc   = specularColor.Sample(v.tc).XYZ
                let shine   = uniform.Shininess


                let color = diff.XYZ * diffuse  +  specc * pow spec 32.0

                return V4d(color, v.color.W)
            }
    
        let alphaTest (v : Vertex) =
            fragment {
                let dummy = v.sp.X * 0.0000001
                if v.color.W < 0.05 + dummy then
                    discard()

                return v
            }

    let run() =
        FShade.EffectDebugger.attach()
        let eigi = loadEigi @"E:\Development\WorkDirectory\Eigi"

        let sg =
            loadEigi @"E:\Development\WorkDirectory\Eigi"
                |> Sg.transform (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero))
                |> Sg.shader {
                    do! Shader.transform
                    do! Shader.diffuseTexture
                    do! Shader.normalMapping
                    do! Shader.lighting
                    do! Shader.alphaTest
                }

        show {
            display Display.Mono
            samples 8
            backend Backend.Vulkan
            debug false

            scene sg
            
        }

