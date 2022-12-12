namespace Aardvark.GLTF

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph


module Skybox =
    type Marker = Marker
    let getImage =
        let names = typeof<Marker>.Assembly.GetManifestResourceNames()
        let load (name : string) =
            let name = names |> Array.find (fun str -> str.EndsWith name)
            use s = typeof<Marker>.Assembly.GetManifestResourceStream(name)
            PixImage.Load(s)

        load

    let get (name : string) =
        
        AVal.custom (fun _ ->
            let env =
                // let names = typeof<Marker>.Assembly.GetManifestResourceNames()
                // let getMipMaps (img : PixImage) =
                //     use ii = PixImageSharp.ToImage img
                //     let res = Array.zeroCreate (1 + int (floor (log2 (float img.Size.X))))
                //     res.[0] <- img
                //     for l in 1 .. res.Length - 1 do
                //         ii.Mutate (fun ctx ->
                //             ctx.Resize(ii.Width/2, ii.Height/2)
                //             |> ignore
                //         )
                //         res.[l] <- PixImageSharp.ToPixImage ii
                //     res
                let trafo t (img : PixImage) =
                    img.Transformed t
                    //|> getMipMaps

                    
                PixImageCube [|
                    PixImageMipMap(
                        getImage (name.Replace("$", "rt"))
                        |> trafo ImageTrafo.Rot90
                    )
                    PixImageMipMap(
                        getImage (name.Replace("$", "lf"))
                        |> trafo ImageTrafo.Rot270
                    )
                
                    PixImageMipMap(
                        getImage (name.Replace("$", "bk"))
                    )
                    PixImageMipMap(
                        getImage (name.Replace("$", "ft"))
                        |> trafo ImageTrafo.Rot180
                    )
                
                    PixImageMipMap(
                        getImage (name.Replace("$", "up"))
                        |> trafo ImageTrafo.Rot90
                    )
                    PixImageMipMap(
                        getImage (name.Replace("$", "dn"))
                        |> trafo ImageTrafo.Rot90
                    )
                |]

            PixTextureCube(env, TextureParams.mipmapped) :> ITexture
        )


module Semantic =
    let RoughnessCoordinate = Symbol.Create "RoughnessCoordinate"
    let MetallicnessCoordinate = Symbol.Create "MetallicnessCoordinate"
    let EmissiveCoordinate = Symbol.Create "EmissiveCoordinate"
    let NormalCoordinate = DefaultSemantic.NormalMapCoordinates
    let Tangent = Symbol.Create "Tangent"

[<ReflectedDefinition>]
module Shader =
    open FShade
    
    type ViewPositionAttribute() = inherit SemanticAttribute("ViewPosition")
    type RoughnessCoordinateAttribute() = inherit SemanticAttribute("RoughnessCoordinate")
    type MetallicnessCoordinateAttribute() = inherit SemanticAttribute("MetallicnessCoordinate")
    type EmissiveCoordinateAttribute() = inherit SemanticAttribute("EmissiveCoordinate")
    type NormalCoordinateAttribute() = inherit SemanticAttribute(string DefaultSemantic.NormalMapCoordinates)
    type TangentAttribute() = inherit SemanticAttribute("Tangent")
    type ViewTangentAttribute() = inherit SemanticAttribute("ViewTangent")
    type ViewBiTangentAttribute() = inherit SemanticAttribute("ViewBiTangent")
    type ViewLightDirectionAttribute() = inherit SemanticAttribute("ViewLightDirection")
    
    let skyboxSpecular =
        samplerCube {
            texture uniform?SkyboxSpecular
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            addressW WrapMode.Wrap
        }
        
    let skyboxDiffuse =
        samplerCube {
            texture uniform?SkyboxDiffuse
            filter Filter.MinMagMipLinear
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            addressW WrapMode.Wrap
        }
    
    let diffuseColorTex =
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }
    
    let roughnessTexture =
        sampler2d {
            texture uniform?RoughnessTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }
    
    let metallicnessTexture =
        sampler2d {
            texture uniform?MetallicnessTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }
        
    let emissiveTexture =
        sampler2d {
            texture uniform?EmissiveTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }
        
    let normalTexture =
        sampler2d {
            texture uniform?(string DefaultSemantic.NormalMapTexture)
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }
        
    type UniformScope with
        member x.DiffuseColor : V4d = uniform?Material?DiffuseColor
        member x.Roughness : float = uniform?Material?Roughness
        member x.Metallicness : float = uniform?Material?Metallicness
        member x.EmissiveColor : V4d = uniform?Material?EmissiveColor
        member x.NormalTextureScale : float = uniform?Material?NormalTextureScale
        
        member x.HasDiffuseColorTexture : bool = uniform?Material?HasDiffuseColorTexture
        member x.HasRoughnessTexture : bool = uniform?Material?HasRoughnessTexture
        member x.HasMetallicnessTexture : bool = uniform?Material?HasMetallicnessTexture
        member x.HasEmissiveTexture : bool = uniform?Material?HasEmissiveTexture
        member x.HasNormalTexture : bool = uniform?Material?("Has" + string DefaultSemantic.NormalMapTexture)
        
        member x.RoughnessTextureComponent : int = uniform?Material?RoughnessTextureComponent
        member x.MetallicnessTextureComponent : int = uniform?Material?MetallicnessTextureComponent
        
        member x.HasNormals : bool = uniform?Mesh?HasNormals
        member x.HasTangents : bool = uniform?Mesh?HasTangents
        member x.HasColors : bool = uniform?Mesh?HasColors
        member x.LevelCount : int = uniform?LevelCount
        
    type Vertex =
        {
            [<Position>]                pos             : V4d
            [<ViewPosition>]            viewPos         : V4d
            [<Normal>]                  normal          : V3d
            [<TexCoord>]                texCoord        : V2d
            [<RoughnessCoordinate>]     roughCoord      : V2d
            [<MetallicnessCoordinate>]  metalCoord      : V2d
            [<EmissiveCoordinate>]      emissCoord      : V2d
            [<NormalCoordinate>]        normCoord       : V2d
            [<Tangent>]                 tangent         : V4d
            [<ViewTangent>]             viewTangent     : V3d
            [<ViewBiTangent>]           viewBiTangent   : V3d
            [<ViewLightDirection>]      viewLightDir    : V3d
        }
        
    let trafo (v : Vertex) =
        vertex {
            
            let vp = uniform.ModelViewTrafo * v.pos
            let vld = (uniform.ViewTrafo * V4d(uniform.LightLocation, 1.0) - vp).XYZ |> Vec.normalize
            let vn = uniform.ModelViewTrafoInv.Transposed.TransformDir v.normal |> Vec.normalize
            let vt = uniform.ModelViewTrafoInv.Transposed.TransformDir v.tangent.XYZ |> Vec.normalize
            let vb = v.tangent.W * Vec.cross vn vt
            
            return
                { v with
                    pos = uniform.ProjTrafo * vp
                    viewPos = vp
                    normal = uniform.ModelViewTrafoInv.Transposed.TransformDir v.normal |> Vec.normalize
                    viewTangent = vt
                    viewBiTangent = vb
                    viewLightDir = vld
                }
        }
        
    let samples24 =
        [|
            V2d( -0.4612850228120782, -0.8824263018037591 )
            V2d( 0.2033539719528926, 0.9766070232577696 )
            V2d( 0.8622755945065503, -0.4990552917715807 )
            V2d( -0.8458406529500018, 0.4340626564690164 )
            V2d( 0.9145341241356336, 0.40187426079092753 )
            V2d( -0.8095919285224212, -0.2476471278659192 )
            V2d( 0.2443597793708885, -0.8210571365841042 )
            V2d( -0.29522102954593127, 0.6411496844366571 )
            V2d( 0.4013698454531175, 0.47134750051312063 )
            V2d( -0.1573158341083741, -0.48548502348882533 )
            V2d( 0.5674301785250454, -0.1052346781436156 )
            V2d( -0.4929375319230899, 0.09422383038685558 )
            V2d( 0.967785465127825, -0.06868225365333279 )
            V2d( 0.2267967507441493, -0.40237871966279687 )
            V2d( -0.7200979001122771, -0.6248240905561527 )
            V2d( -0.015195608523765971, 0.35623701723070667 )
            V2d( -0.11428925675805125, -0.963723441683084 )
            V2d( 0.5482105069441386, 0.781847612911249 )
            V2d( -0.6515264455787967, 0.7473765703131305 )
            V2d( 0.5826875031269089, -0.6956573112908789 )
            V2d( -0.8496230198638387, 0.09209564840857346 )
            V2d( 0.38289808661249414, 0.15269522898022844 )
            V2d( -0.4951171173546325, -0.2654758742352245 )
        |]
        
    let linearToSrgb (v : V4d) =
        let e = 1.0 / 2.2
        V4d(v.X ** e, v.Y ** e, v.Z ** e, v.W)
        
    [<ReflectedDefinition>]
    let srgbToLinear (v : V4d) =
        let e = 2.2
        V4d(v.X ** e, v.Y ** e, v.Z ** e, v.W)
        
    let trowbridgeReitzNDF (roughness : float) (nDotH : float) =
        let a = roughness * roughness
        let a2 = a * a
        let nDotH2 = nDotH * nDotH
        let denom = nDotH2 * (a2 - 1.0) + 1.0
        a2 / (Constant.Pi * denom * denom)
        
    let fresnel (f0 : V3d) (nv : float) (roughness : float) =
        let a = V3d.III * (1.0 - roughness)
        f0 + (max f0 a - f0) * nv ** 5.0
        
    let schlickBeckmannGAF (d : float) (roughness : float) =
        let a = roughness * roughness
        let k = a * 0.797884560803
        d / (d * (1.0 - k) + k)
        
    let sampleEnvDiffuse (viewDir : V3d) =
        let worldDir = uniform.ViewTrafoInv.TransformDir viewDir |> Vec.normalize
        skyboxDiffuse.Sample(worldDir) |> srgbToLinear |> Vec.xyz
        
    let sampleEnv (viewDir : V3d) (roughness : float) =
        let worldDir = uniform.ViewTrafoInv.TransformDir viewDir |> Vec.normalize
        skyboxSpecular.SampleLevel(worldDir, roughness * float (uniform.LevelCount - 1)) |> srgbToLinear |> Vec.xyz
        
    [<ReflectedDefinition>] [<Inline>]
    let getR0 (reflectivity : float, metalness : float, baseColor : V3d) =
        V3d.III * reflectivity * (1.0 - metalness) + baseColor * metalness
        
    let shade (v : Vertex) =
        fragment {
            let eps = 0.00001
            
            let baseColor =
                if uniform.HasDiffuseColorTexture then
                    let tex = diffuseColorTex.Sample(v.texCoord) |> srgbToLinear
                    tex * uniform.DiffuseColor
                else
                    uniform.DiffuseColor
            
            let roughness =
                if uniform.HasRoughnessTexture then
                    let tv = roughnessTexture.Sample(v.roughCoord).[uniform.RoughnessTextureComponent]
                    let uv = eps + uniform.Roughness
                    uv * tv |> saturate
                else
                    uniform.Roughness + eps |> clamp 0.0 0.99
            
            let metalness =
                if uniform.HasMetallicnessTexture then
                    let tv = metallicnessTexture.Sample(v.metalCoord).[uniform.MetallicnessTextureComponent]
                    let uv = eps + uniform.Metallicness
                    uv * tv |> saturate
                else
                    uniform.Metallicness + eps |> saturate
            
            let occlusion = 1.0
            
            if baseColor.W < 0.01 then discard()
            
            let vn = v.normal |> Vec.normalize
            let vld = v.viewLightDir |> Vec.normalize
            let vcd = -v.viewPos.XYZ |> Vec.normalize
            
            let half =
                let v = vld + vcd
                let l = Vec.Length v
                if l > eps then v / l
                else V3d.Zero
            
            let vn =
                if uniform.HasNormalTexture then
                    let vt = Vec.normalize v.viewTangent
                    let vb = Vec.normalize v.viewBiTangent
                    
                    let v = normalTexture.Sample(v.normCoord).XYZ
                    let nn = (v * 2.0 - 1.0) * V3d(V2d.II * uniform.NormalTextureScale * 0.5, 1.0)
                    
                    let newNormal = vn * nn.Z + vt * nn.X + vb * nn.Y |> Vec.normalize
                    if newNormal.Z < 0.0 then vn
                    else newNormal
                else
                    vn
                    
                    
            let refl = -Vec.reflect vn vcd

            let nl = Vec.dot vn vld |> max 0.0
            let nh = Vec.dot vn half |> max 0.0
            //let hv = Vec.dot half vcd |> max 0.0
            let nv = Vec.dot vn vcd |> max 0.0
            
            
            
            let f0 = getR0(0.04, metalness, V3d.III) * baseColor.XYZ
            let d = trowbridgeReitzNDF nh roughness
            
            let f = fresnel f0 nv roughness
            let g = schlickBeckmannGAF nv roughness * schlickBeckmannGAF nl roughness
            
            let lambert = nl
            let dr = V3d.III * occlusion
            
            let diffuseIrradiance = sampleEnvDiffuse vn * occlusion
            let specularIrradiance = sampleEnv refl roughness * occlusion
            
            let diffuseDirectTerm = (baseColor.XYZ / Constant.Pi) * (V3d.III - f) * (1.0 - metalness)
            
            let specularDirectTerm =
                (f * g * d) / (4.0 * nl * nv + eps)
            
            let brdfDirectOutput = (diffuseDirectTerm + specularDirectTerm) * lambert * dr 
            let ambientDiffuse = diffuseIrradiance * (baseColor.XYZ / Constant.Pi) * (1.0 - f) * (1.0 - metalness) 
            
            let ambientSpecular = specularIrradiance * f 
            
            let color = brdfDirectOutput + ambientDiffuse + ambientSpecular
            
            return V4d(saturate color, 1.0) |> linearToSrgb
        }
    
    let environment (v : Effects.Vertex) =
        fragment {
            let ndc = v.pos.XY / v.pos.W
            
            let p04 = uniform.ProjTrafoInv * V4d(ndc, -1.0, 1.0)
            let p14 = uniform.ProjTrafoInv * V4d(ndc, -0.8, 1.0)
            
            let dir = p14.XYZ / p14.W - p04.XYZ / p04.W
            
            let res = V4d(sampleEnv dir 0.2, 1.0)
            return linearToSrgb res
            //return skybox.Sample(dir)
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SceneSg =
    
    let private white =
        let img = PixImage<byte>(Col.Format.RGBA, V2i.II)
        img.GetMatrix<C4b>().Set(C4b.White) |> ignore
        PixTexture2d(PixImageMipMap [| img :> PixImage  |], TextureParams.empty) :> ITexture |> AVal.constant
        
        
    let private meshSg (m : Mesh) =
        let fvc =
            match m.Index with
            | Some i -> i.Length
            | None -> m.Positions.Length
                
        let mutable sg = 
            Sg.render m.Mode (DrawCallInfo(FaceVertexCount = fvc, InstanceCount = 1))
            |> Sg.vertexAttribute' DefaultSemantic.Positions m.Positions
                
        match m.Index with
        | Some idx -> sg <- sg |> Sg.indexArray idx
        | None -> ()
        
        match m.Normals with
        | Some ns -> sg <- sg |> Sg.vertexAttribute' DefaultSemantic.Normals ns
        | None -> ()
        
        match m.Tangents with
        | Some ns -> sg <- sg |> Sg.vertexAttribute' Semantic.Tangent ns
        | None -> ()
        
        match m.Colors with
        | Some cs -> sg <- sg |> Sg.vertexAttribute' DefaultSemantic.Colors cs
        | None -> ()
        
        for data, sems in m.TexCoords do
            let view = BufferView.ofArray data
            for sem in sems do
                let semantic =
                    match sem with
                    | TextureSemantic.BaseColor -> DefaultSemantic.DiffuseColorCoordinates
                    | TextureSemantic.Roughness -> Semantic.RoughnessCoordinate
                    | TextureSemantic.Emissive -> Semantic.EmissiveCoordinate
                    | TextureSemantic.Metallicness -> Semantic.MetallicnessCoordinate
                    | TextureSemantic.Normal -> Semantic.NormalCoordinate
                    
                sg <- sg |> Sg.vertexBuffer semantic view
                
        let uniforms =
            UniformProvider.ofList [
                "HasNormals", AVal.constant (Option.isSome m.Normals) :> IAdaptiveValue
                "HasTangents", AVal.constant (Option.isSome m.Tangents)
                "HasColors", AVal.constant (Option.isSome m.Colors)
            ]
        
        
        Sg.UniformApplicator(uniforms, sg) :> ISg
        
    let toSimpleSg (runtime : IRuntime) (scenes : seq<Scene>) =
           
        let defaultMaterial =
            UniformProvider.ofList [
                "DiffuseColor", AVal.constant C4f.White :> IAdaptiveValue
                "Roughness", AVal.constant 0.0
                "Metallicness", AVal.constant 0.0
                "EmissiveColor", AVal.constant C4f.Black
                "NormalTextureScale", AVal.constant 1.0
                
                "HasDiffuseColorTexture", AVal.constant false
                "HasRoughnessTexture", AVal.constant false
                "HasMetallicnessTexture", AVal.constant false
                "HasEmissiveTexture", AVal.constant false
                "Has" + string DefaultSemantic.NormalMapTexture, AVal.constant false
                
                "DiffuseColorTexture", white
                "RoughnessTexture", white
                "RoughnessTextureComponent", AVal.constant 0
                "MetallicnessTexture", white
                "MetallicnessTextureComponent", AVal.constant 0
                string DefaultSemantic.NormalMapTexture, white
                "EmissiveTexture", white
            ]
             
        let sceneSgs =
            scenes |> Seq.toList |> List.map (fun scene ->
                let textures =
                    scene.ImageData |> HashMap.map (fun _ data ->
                        try
                            use ms = new System.IO.MemoryStream(data.Data)
                            let img = PixImage.Load ms
                            
                            let img = 
                                if HashSet.contains TextureSemantic.BaseColor data.Semantics && img.Format = Col.Format.Gray then
                                    img.Visit {
                                        new IPixImageVisitor<PixImage> with
                                            member x.Visit(img : PixImage<'a>) =
                                                img.ToFormat(Col.Format.RGBA) :> PixImage
                                    }
                                else
                                    img
                            
                            
                            PixTexture2d(PixImageMipMap [|img|], TextureParams.mipmapped) :> ITexture |> AVal.constant
                        with e ->
                            white
                    )
         
                let meshes =
                    scene.Meshes |> HashMap.map (fun _ m -> meshSg m)
                     
        
          
                let rec traverse (node : Node) =
                    
                    
                    let cs =
                        match node.Children with
                        | [] -> None
                        | _ -> node.Children |> Seq.map traverse |> Sg.ofSeq |> Some
                        
                    let ms =
                        node.Meshes |> List.choose (fun mi ->
                            match HashMap.tryFind mi.Mesh meshes with
                            | Some mesh ->
                                match mi.Material |> Option.bind (fun mid -> HashMap.tryFind mid scene.Materials) with
                                | Some mat ->
                                    let uniforms = 
                                        let baseColorTexture =
                                            match mat.BaseColorTexture |> Option.bind (fun id -> HashMap.tryFind id textures) with
                                            | Some t -> t
                                            | None -> white
                                            
                                        let roughnessTexture =
                                            match mat.RoughnessTexture |> Option.bind (fun id -> HashMap.tryFind id textures) with
                                            | Some t -> t
                                            | None -> white
                                            
                                        let metallicnessTexture =
                                            match mat.MetallicnessTexture |> Option.bind (fun id -> HashMap.tryFind id textures) with
                                            | Some t -> t
                                            | None -> white
                                        
                                        let normalTexture =
                                            match mat.NormalTexture |> Option.bind (fun id -> HashMap.tryFind id textures) with
                                            | Some t -> t
                                            | None -> white
                                        
                                        let emissiveTexture =
                                            match mat.EmissiveTexture |> Option.bind (fun id -> HashMap.tryFind id textures) with
                                            | Some t -> t
                                            | None -> white
                                            
                                        UniformProvider.ofList [
                                            "DiffuseColor", AVal.constant mat.BaseColor :> IAdaptiveValue
                                            "Roughness", AVal.constant mat.Roughness
                                            "Metallicness", AVal.constant mat.Metallicness
                                            "EmissiveColor", AVal.constant mat.EmissiveColor
                                            "NormalTextureScale", AVal.constant mat.NormalTextureScale
                                            
                                            "HasDiffuseColorTexture", AVal.constant (Option.isSome mat.BaseColorTexture) 
                                            "HasRoughnessTexture", AVal.constant (Option.isSome mat.RoughnessTexture) 
                                            "HasMetallicnessTexture", AVal.constant (Option.isSome mat.MetallicnessTexture) 
                                            "HasEmissiveTexture", AVal.constant (Option.isSome mat.EmissiveTexture) 
                                            "Has" + string DefaultSemantic.NormalMapTexture, AVal.constant (Option.isSome mat.NormalTexture) 
                                            
                                            "DiffuseColorTexture", baseColorTexture
                                            "RoughnessTexture", roughnessTexture
                                            "MetallicnessTexture", metallicnessTexture
                                            "RoughnessTextureComponent", AVal.constant mat.RoughnessTextureComponent
                                            "MetallicnessTextureComponent", AVal.constant mat.MetallicnessTextureComponent
                                            string DefaultSemantic.NormalMapTexture, normalTexture
                                            "EmissiveTexture", emissiveTexture
                                        ]
                                    Some (Sg.UniformApplicator(uniforms, mesh) :> ISg)
                                | None ->
                                    Some mesh
                            | None ->
                                None
                        ) |> Sg.ofList |> Some
                            
                    let sg =
                        match cs with
                        | Some cs ->
                            match ms with
                            | Some ms -> Sg.ofList [cs; ms]
                            | None -> cs
                        | None ->
                            match ms with
                            | Some ms -> ms
                            | None -> Sg.empty
                    
                    match node.Trafo with
                    | Some t -> Sg.trafo' t sg
                    | None -> sg
                    
                traverse scene.RootNode
            )
            
        let specular, diffuse =
            // FileTexture("/Users/schorsch/Desktop/Forest.png", TextureParams.empty)
            // |> EnvironmentMap.ofPanorama runtime
            Skybox.get "miramar_$.png"
            |> AVal.force
            |> EnvironmentMap.prepare runtime
        
        let specular = specular :> ITexture |> AVal.constant
        let diffuse = diffuse :> ITexture |> AVal.constant
            
        Sg.ofList [
            Sg.UniformApplicator(defaultMaterial, Sg.ofList sceneSgs)
            |> Sg.vertexBufferValue' DefaultSemantic.Normals V3f.OOI
            |> Sg.vertexBufferValue' Semantic.Tangent V4f.IOOI
            |> Sg.vertexBufferValue' DefaultSemantic.DiffuseColorCoordinates V2f.Zero
            |> Sg.vertexBufferValue' Semantic.RoughnessCoordinate V2f.Zero
            |> Sg.vertexBufferValue' Semantic.EmissiveCoordinate V2f.Zero
            |> Sg.vertexBufferValue' Semantic.MetallicnessCoordinate V2f.Zero
            |> Sg.vertexBufferValue' Semantic.NormalCoordinate V2f.Zero
            |> Sg.texture "SkyboxSpecular" specular
            |> Sg.texture "SkyboxDiffuse" diffuse
            
            Sg.farPlaneQuad
            |> Sg.texture "SkyboxSpecular" specular
            |> Sg.texture "SkyboxDiffuse" diffuse
            |> Sg.shader {
                do! Shader.environment
            }
            
               
        ]
        |> Sg.uniform' "LevelCount" EnvironmentMap.levelCount
            
            

