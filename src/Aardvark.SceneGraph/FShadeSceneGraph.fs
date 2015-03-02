namespace Aardvark.SceneGraph

[<AutoOpen>]
module DefaultSems =
    open FShade
    open Aardvark.Base

    type PositionAttribute() = inherit SemanticAttribute(DefaultSemantic.Positions.ToString())
    type TexCoordAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorCoordinates.ToString())
    type WorldPositionAttribute() = inherit SemanticAttribute("WorldPosition")
    type NormalAttribute() = inherit SemanticAttribute(DefaultSemantic.Normals.ToString())
    type BiNormalAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorUTangents.ToString())
    type TangentAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorVTangents.ToString())
    type ColorAttribute() = inherit SemanticAttribute(DefaultSemantic.Colors.ToString())


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

            printfn "CODE: %A" code

            BackendSurface(code, entries, null)
        ) 

    let (!!) (m : IMod<'a>) : 'a =
        failwith "mod-splicing can only be used inside shaders"

    let (|SplicedMod|_|) (e : Expr) =
        match e with
            | Call(None, mi, [target]) when mi.Name = "op_BangBang" ->
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
            | WrapMode.Border -> Aardvark.Base.WrapMode.Border
            | WrapMode.Clamp -> Aardvark.Base.WrapMode.Clamp
            | WrapMode.Mirror -> Aardvark.Base.WrapMode.Mirror
            | WrapMode.MirrorOnce -> Aardvark.Base.WrapMode.MirrorOnce
            | WrapMode.Wrap -> Aardvark.Base.WrapMode.Wrap
            | _ -> failwithf "unknown address mode %A" mode

    let private toTextureFilter (mode : Filter) =
        match mode with
            | Filter.Anisotropic -> TextureFilter.Anisotropic
            | Filter.MinLinearMagMipPoint -> TextureFilter.MinLinearMagMipPoint
            | Filter.MinLinearMagPointMipLinear -> TextureFilter.MinLinearMagPointMipLinear
            | Filter.MinMagLinearMipPoint -> TextureFilter.MinMagLinearMipPoint
            | Filter.MinMagMipLinear -> TextureFilter.MinMagMipLinear
            | Filter.MinMagMipPoint -> TextureFilter.MinMagMipPoint
            | Filter.MinMagPointMipLinear -> TextureFilter.MinMagPointMipLinear
            | Filter.MinPointMagLinearMipPoint -> TextureFilter.MinPointMagLinearMipPoint
            | Filter.MinPointMagMipLinear -> TextureFilter.MinPointMagMipLinear
            | Filter.MinMagPoint -> TextureFilter.MinMagPoint
            | Filter.MinMagLinear -> TextureFilter.MinMagLinear
            | Filter.MinPointMagLinear -> TextureFilter.MinPointMagLinear
            | Filter.MinLinearMagPoint -> TextureFilter.MinLinearMagPoint
            | _ -> failwithf "unknown filter mode: %A" mode

    let toSamplerStateDescription (state : SamplerState) =

        let r = SamplerStateDescription()
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


    type FShadeSurface(effect : FShadeEffect) =
        let mutable cache = None
        let uniforms = SymDict.empty
        let samplerStates = SymDict.empty

        member x.Effect = effect

        interface IGeneratedSurface with
            member x.Generate (r : IRuntime) =
                match cache with
                    | Some c -> c
                    | None ->
                        let t = r.GetType()
                        if t.FullName.Contains "GL" then
                            let supportsUniformBuffers = t.GetProperty("SupportsUniformBuffers").GetValue(r) |> unbox<bool>

                            let compileEffect =
                                if supportsUniformBuffers then GLSL.compileEffect
                                else GLSL.compileEffect120

                            match effect |> compileEffect with
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
                        else
                            failwithf "unsupported runtime type: %A" r     
                    

    let toFShadeSurface (e : FShadeEffect) =
        FShadeSurface(e) :> ISurface

    module Sg =
        let private constantSurfaceCache = MemoCache(false)

        let effect (s : #seq<FShadeEffect>) (sg : ISg) =
            let e = FShade.SequentialComposition.compose s
            let s = constantSurfaceCache.Memoized1 (fun e -> Mod.initConstant (FShadeSurface(e) :> ISurface)) e
            Sg.SurfaceApplicator(s, sg) :> ISg

        let effect' (e : IMod<FShadeEffect>) (sg : ISg) =
            let s = constantSurfaceCache.Memoized1 (fun e -> e |> Mod.map (fun e -> (FShadeSurface(e) :> ISurface))) e
            Sg.SurfaceApplicator(s, sg) :> ISg