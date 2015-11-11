namespace Aardvark.SceneGraph

[<AutoOpen>]
module DefaultSems =
    open FShade
    open Aardvark.Base
    open Aardvark.Base.Rendering

    type PositionAttribute() = inherit SemanticAttribute(DefaultSemantic.Positions.ToString())
    type TexCoordAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorCoordinates.ToString())
    type WorldPositionAttribute() = inherit SemanticAttribute("WorldPosition")
    type NormalAttribute() = inherit SemanticAttribute(DefaultSemantic.Normals.ToString())
    type BiNormalAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorUTangents.ToString())
    type TangentAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorVTangents.ToString())
    type ColorAttribute() = inherit SemanticAttribute(DefaultSemantic.Colors.ToString())
    type InstanceTrafoAttribute() = inherit SemanticAttribute(DefaultSemantic.InstanceTrafo.ToString())


    type FShade.Parameters.Uniforms.UniformScope with
        member x.ModelTrafo : M44d = x?PerModel?ModelTrafo
        member x.ViewTrafo : M44d = x?PerView?ViewTrafo
        member x.ProjTrafo : M44d = x?PerView?ProjTrafo
        member x.ViewProjTrafo : M44d = x?PerView?ViewProjTrafo
        member x.ModelViewTrafo : M44d = x?PerModel?ModelViewTrafo
        member x.ModelViewProjTrafo : M44d = x?PerModel?ModelViewProjTrafo
        member x.NormalMatrix : M33d = x?PerModel?NormalMatrix

        member x.ModelTrafoInv : M44d = x?PerModel?ModelTrafoInv
        member x.ViewTrafoInv : M44d = x?PerView?ViewTrafoInv
        member x.ProjTrafoInv : M44d = x?PerView?ProjTrafoInv
        member x.ViewProjTrafoInv : M44d = x?PerView?ViewProjTrafoInv
        member x.ModelViewTrafoInv : M44d = x?PerModel?ModelViewTrafoInv
        member x.ModelViewProjTrafoInv : M44d = x?PerModel?ModelViewProjTrafoInv

        member x.CameraLocation : V3d = x?PerView?CameraLocation
        member x.LightLocation : V3d = x?PerLight?LightLocation



        member x.LineWidth : float = x?LineWidth
        member x.LineColor : V4d = x?LineColor

        member x.PointSize : float = x?PointSize
        member x.PointColor : V4d = x?PointColor

        member x.ViewportSize : V2i = x?PerView?ViewportSize

[<AutoOpen>]
module FShadeSceneGraph =
    open Aardvark.Base
    open FShade.Compiler
    open FShade

    open System.Collections.Concurrent
    open System.Collections.Generic
    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns
    open Aardvark.Base.Incremental

    type FShadeEffect = Compiled<Effect, ShaderState>



    let private backendSurfaceCache = ConcurrentDictionary<string, BackendSurface>()

    let private getOrCreateSurface (code : string) =
        backendSurfaceCache.GetOrAdd(code, fun (code : string) ->
            let entries = Dictionary()

            if code.Contains "VS(" then entries.Add(ShaderStage.Vertex, "VS")
            if code.Contains "GS(" then entries.Add(ShaderStage.Geometry, "GS")
            if code.Contains "PS(" then entries.Add(ShaderStage.Pixel, "PS")
            if code.Contains "TCS(" then entries.Add(ShaderStage.TessControl, "TCS")
            if code.Contains "TEV(" then entries.Add(ShaderStage.TessEval, "TEV")

            BackendSurface(code, entries, null)
        ) 

    let (!!) (m : IMod<'a>) : 'a =
        failwith "mod-splicing can only be used inside shaders"

    let (|SplicedMod|_|) (e : Expr) =
        match e with
            | Call(None, mi, [Value(target,ModOf(_))]) when mi.Name = "op_BangBang" ->
                let t = mi.ReturnType
                SplicedMod(t, target |> unbox<IMod>) |> Some 
            | _ -> None

    do FShade.Parameters.Uniforms.uniformDetectors <- [fun e ->
            match e with
                | SplicedMod(t, m) -> UserUniform(t, m :> obj) |> Some
                | _ -> None
       ]

    let private toWrapMode (mode : WrapMode) =
        match mode with
            | WrapMode.Border -> Aardvark.Base.Rendering.WrapMode.Border
            | WrapMode.Clamp -> Aardvark.Base.Rendering.WrapMode.Clamp
            | WrapMode.Mirror -> Aardvark.Base.Rendering.WrapMode.Mirror
            | WrapMode.MirrorOnce -> Aardvark.Base.Rendering.WrapMode.MirrorOnce
            | WrapMode.Wrap -> Aardvark.Base.Rendering.WrapMode.Wrap
            | _ -> failwithf "unknown address mode %A" mode

    let private toTextureFilter (mode : Filter) =
        match mode with
            | Filter.Anisotropic -> Aardvark.Base.Rendering.TextureFilter.Anisotropic
            | Filter.MinLinearMagMipPoint -> Aardvark.Base.Rendering.TextureFilter.MinLinearMagMipPoint
            | Filter.MinLinearMagPointMipLinear -> Aardvark.Base.Rendering.TextureFilter.MinLinearMagPointMipLinear
            | Filter.MinMagLinearMipPoint -> Aardvark.Base.Rendering.TextureFilter.MinMagLinearMipPoint
            | Filter.MinMagMipLinear -> Aardvark.Base.Rendering.TextureFilter.MinMagMipLinear
            | Filter.MinMagMipPoint -> Aardvark.Base.Rendering.TextureFilter.MinMagMipPoint
            | Filter.MinMagPointMipLinear -> Aardvark.Base.Rendering.TextureFilter.MinMagPointMipLinear
            | Filter.MinPointMagLinearMipPoint -> Aardvark.Base.Rendering.TextureFilter.MinPointMagLinearMipPoint
            | Filter.MinPointMagMipLinear -> Aardvark.Base.Rendering.TextureFilter.MinPointMagMipLinear
            | Filter.MinMagPoint -> Aardvark.Base.Rendering.TextureFilter.MinMagPoint
            | Filter.MinMagLinear -> Aardvark.Base.Rendering.TextureFilter.MinMagLinear
            | Filter.MinPointMagLinear -> Aardvark.Base.Rendering.TextureFilter.MinPointMagLinear
            | Filter.MinLinearMagPoint -> Aardvark.Base.Rendering.TextureFilter.MinLinearMagPoint
            | _ -> failwithf "unknown filter mode: %A" mode

    let toSamplerStateDescription (state : SamplerState) =

        let r = Aardvark.Base.Rendering.SamplerStateDescription()
        let a = r.AddressU
        state.AddressU |> Option.iter (fun a -> r.AddressU <- toWrapMode a)
        state.AddressV |> Option.iter (fun a -> r.AddressV <- toWrapMode a)
        state.AddressW |> Option.iter (fun a -> r.AddressW <- toWrapMode a)
        state.Filter |> Option.iter (fun f -> r.Filter <- toTextureFilter f)

        state.BorderColor |> Option.iter (fun b -> r.BorderColor <- b)
        state.MaxAnisotropy |> Option.iter (fun b -> r.MaxAnisotropy <- b)
        state.MaxLod |> Option.iter (fun b -> r.MinLod <- float32 b)
        state.MinLod |> Option.iter (fun b -> r.MaxLod <- float32 b)
        state.MipLodBias |> Option.iter (fun b -> r.MipLodBias <- float32 b)

        r

    open System
    open FShade.GLSL

    type FShadeSurface(effect : FShadeEffect) =
        let mutable cache = None
        let uniforms = SymDict.empty
        let samplerStates = SymDict.empty

        static let glslConfigCache = ConcurrentDict<_,_>(Dict())


        static let glsl410 = 
            {
                languageVersion = Version(4,1)
                enabledExtensions = Set.empty
                createUniformBuffers = true
                createGlobalUniforms = false
                createBindings = false
                createDescriptorSets = false
                createInputLocations = true
                createRowMajorMatrices = false
                createPerStageUniforms = false
                flipHandedness = false
                depthRange = Range1d(-1.0,1.0)
            }

        static let glsl120 = 
            {
                languageVersion = Version(1,2)
                enabledExtensions = Set.empty
                createUniformBuffers = false
                createGlobalUniforms = true
                createBindings = false
                createDescriptorSets = false
                createInputLocations = false
                createRowMajorMatrices = false
                createPerStageUniforms = false
                flipHandedness = false
                depthRange = Range1d(-1.0,1.0)
            }

        static let vulkan =
            {
                languageVersion = Version(1,4)
                enabledExtensions = Set.ofList [ "GL_ARB_separate_shader_objects"; "GL_ARB_shading_language_420pack"; "GL_KHR_vulkan_GLSL" ]
                createUniformBuffers = true
                createGlobalUniforms = false
                createBindings = true
                createDescriptorSets = true
                createInputLocations = true
                createRowMajorMatrices = true
                createPerStageUniforms = true
                flipHandedness = true
                depthRange = Range1d(0.0,1.0)
            }

        static let tryGetGlslConfig (r : IRuntime) =
            glslConfigCache.GetOrCreate(r, Func<_,_>(fun r ->
                let t = r.GetType()
                let n = t.FullName.ToLower()

                if n.Contains ".gl" then 
                    let supportsUniformBuffers = t.GetProperty("SupportsUniformBuffers").GetValue(r) |> unbox<bool>
                    if supportsUniformBuffers then Some glsl410
                    else Some glsl120

                elif n.Contains "vulkan" then
                    Some vulkan

                else
                    None
            ))

        static let formatToExpectedType (format : RenderbufferFormat) : Type =
            let cf = RenderbufferFormat.toColFormat format

            match cf with
                | Col.Format.Gray -> typeof<float>
                | Col.Format.NormalUV -> typeof<V2d>
                | Col.Format.RGB -> typeof<V3d>
                | Col.Format.RGBA -> typeof<V4d>
                | _ -> failwithf "unsupported Col.Format: %A" cf

        static let defaultSemanticTypes =
            Dict.ofList [   
                DefaultSemantic.Colors, typeof<V4d>
                DefaultSemantic.Depth, typeof<float>
                DefaultSemantic.Normals, typeof<V3d>
                DefaultSemantic.DiffuseColorUTangents, typeof<V3d>
                DefaultSemantic.DiffuseColorVTangents, typeof<V3d>
                DefaultSemantic.DiffuseColorCoordinates, typeof<V2d>
                DefaultSemantic.Positions, typeof<V4d>
            ]

        member x.Effect = effect

        interface IGeneratedSurface with
            member x.Generate (r : IRuntime, signature : IFramebufferSignature) =
                match cache with
                    | Some c -> c
                    | None ->
                        match tryGetGlslConfig r with
                            | Some glslConfig ->

                                let compileEffect =
                                    GLSL.compileEffect glslConfig

                                let needed =
                                    signature.ColorAttachments 
                                        |> Map.toList
                                        |> List.map (fun (_,(s,f)) ->
                                            match defaultSemanticTypes.TryGetValue s with
                                                | (true, t) -> s.ToString(), t
                                                | _ -> s.ToString(), formatToExpectedType f.format
                                           )
                                        |> Map.ofList
                                match effect |> compileEffect needed with
                                    | Success(map, code) ->
                                        let semanticMap = SymDict.empty

                                        for KeyValue(k,v) in map do
                                            if not v.IsSamplerUniform then
                                                uniforms.[Symbol.Create(k)] <- (v.Value |> unbox<IMod>)
                                            else
                                                let sem, sam = v.Value |> unbox<string * SamplerState>
                                                semanticMap.[Sym.ofString k] <- Sym.ofString sem
                                                samplerStates.[Sym.ofString sem] <- toSamplerStateDescription sam
                                                ()

                                        let bs = getOrCreateSurface code 
                                        let result = BackendSurface(bs.Code, bs.EntryPoints, uniforms, samplerStates, semanticMap) 
                                        cache <- Some result
                                        result
    
                                    | Error e -> 
                                        failwithf "could not compile shader for GLSL: %A" e
                            | None ->
                                failwithf "unsupported runtime type: %A" r     
                    

    let toFShadeSurface (e : FShadeEffect) =
        FShadeSurface(e) :> ISurface

    let (~~) (f : 'a -> Expr<'b>) : FShadeEffect =
        toEffect f

    let inline toEffect a = toEffect a

    module Sg =
        let private constantSurfaceCache = MemoCache(false)

        let effect (s : #seq<FShadeEffect>) (sg : ISg) =
            let e = FShade.SequentialComposition.compose s
            let s = constantSurfaceCache.Memoized1 (fun e -> Mod.constant (FShadeSurface(e) :> ISurface)) e
            Sg.SurfaceApplicator(s, sg) :> ISg

        let effect' (e : IMod<FShadeEffect>) (sg : ISg) =
            let s = constantSurfaceCache.Memoized1 (fun e -> e |> Mod.map (fun e -> (FShadeSurface(e) :> ISurface))) e
            Sg.SurfaceApplicator(s, sg) :> ISg
