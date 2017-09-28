namespace Aardvark.Base.Rendering

open Aardvark.Base
open Aardvark.Base.Rendering
open FShade
open System.Collections.Concurrent
open System.Runtime.CompilerServices
open System.Collections.Generic
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Aardvark.Base.Incremental
open System



type PositionAttribute() = inherit SemanticAttribute(DefaultSemantic.Positions.ToString())
type TexCoordAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorCoordinates.ToString())
type WorldPositionAttribute() = inherit SemanticAttribute("WorldPosition")
type NormalAttribute() = inherit SemanticAttribute(DefaultSemantic.Normals.ToString())
type BiNormalAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorUTangents.ToString())
type TangentAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorVTangents.ToString())
type ColorAttribute() = inherit SemanticAttribute(DefaultSemantic.Colors.ToString())
type InstanceTrafoAttribute() = inherit SemanticAttribute(DefaultSemantic.InstanceTrafo.ToString())

type FShadeEffect = Effect

[<AutoOpen>]
module FShadeInterop =

    type UniformScope with
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


        member x.DiffuseColor : V4d = x?PerMaterial?DiffuseColor
        member x.AmbientColor : V4d = x?PerMaterial?DiffuseColor
        member x.EmissiveColor : V4d = x?PerMaterial?EmissiveColor
        member x.SpecularColor : V4d = x?PerMaterial?SpecularColor
        member x.Shininess : float = x?PerMaterial?Shininess

        member x.DiffuseColorTexture : ShaderTextureHandle = x?DiffuseColorTexture
        member x.AmbientColorTexture : ShaderTextureHandle = x?AmbientColorTexture
        member x.EmissiveColorTexture : ShaderTextureHandle = x?EmissiveColorTexture
        member x.SpecularColorTexture : ShaderTextureHandle = x?SpecularColorTexture
        member x.ShininessTexture : ShaderTextureHandle = x?HasShininessTexture
        member x.LightMapTexture : ShaderTextureHandle = x?LightMapTexture
        member x.NormalMapTexture : ShaderTextureHandle = x?NormalMapTexture

        member x.HasDiffuseColorTexture : bool = x?PerMaterial?HasDiffuseColorTexture
        member x.HasAmbientColorTexture : bool = x?PerMaterial?HasAmbientColorTexture
        member x.HasEmissiveColorTexture : bool = x?PerMaterial?HasEmissiveColorTexture
        member x.HasSpecularColorTexture : bool = x?PerMaterial?HasSpecularColorTexture
        member x.HasShininessTexture : bool = x?PerMaterial?HasShininessTexture
        member x.HasLightMapTexture : bool = x?PerMaterial?HasLightMapTexture
        member x.HasNormalMapTexture : bool = x?PerMaterial?HasNormalMapTexture


    let private backendSurfaceCache = ConcurrentDictionary<string, BackendSurface>()

    type private AStage = Aardvark.Base.ShaderStage

    let private getOrCreateSurface (code : string) =
        backendSurfaceCache.GetOrAdd(code, fun (code : string) ->
            let entries = Dictionary()

            if code.Contains "#ifdef Vertex" then entries.Add(AStage.Vertex, "main")
            if code.Contains "#ifdef Geometry" then entries.Add(AStage.Geometry, "main")
            if code.Contains "#ifdef Fragment" then entries.Add(AStage.Fragment, "main")
            if code.Contains "#ifdef TessControl" then entries.Add(AStage.TessControl, "main")
            if code.Contains "#ifdef TessEval" then entries.Add(AStage.TessEval, "main")

            BackendSurface(code, entries, Map.empty, null)
        ) 

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

    let private toCompareFunction (f : ComparisonFunction) =
        match f with
            | ComparisonFunction.Never -> SamplerComparisonFunction.Never
            | ComparisonFunction.Less -> SamplerComparisonFunction.Less
            | ComparisonFunction.LessOrEqual -> SamplerComparisonFunction.LessOrEqual
            | ComparisonFunction.Greater -> SamplerComparisonFunction.Greater
            | ComparisonFunction.GreaterOrEqual -> SamplerComparisonFunction.GreaterOrEqual
            | ComparisonFunction.NotEqual -> SamplerComparisonFunction.NotEqual
            | ComparisonFunction.Always -> SamplerComparisonFunction.Always
            | _ -> failwithf "unknown compare mode: %A" f

    let private toSamplerStateDescription (state : SamplerState) =

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
        state.Comparison |> Option.iter (fun b -> r.ComparisonFunction <- toCompareFunction b)
        r

    let private builtInTypes =
        Dictionary.ofList [
            DefaultSemantic.Colors, typeof<V4d>
            DefaultSemantic.Normals, typeof<V3d>
            DefaultSemantic.Positions, typeof<V4d>
        ]

    let private typeToFormat =
        LookupTable.lookupTable [
            typeof<int>, RenderbufferFormat.R32i
            typeof<V2i>, RenderbufferFormat.Rg32i
            typeof<V3i>, RenderbufferFormat.Rgb32i
            typeof<V4i>, RenderbufferFormat.Rgba32i

            typeof<C3f>, RenderbufferFormat.Rgb32f
            typeof<C4f>, RenderbufferFormat.Rgba32f
            
            typeof<C3b>, RenderbufferFormat.Rgb8
            typeof<C4b>, RenderbufferFormat.Rgba8

            typeof<C3us>, RenderbufferFormat.Rgb16
            typeof<C4us>, RenderbufferFormat.Rgba16

            typeof<float>, RenderbufferFormat.R32f
            typeof<V2d>, RenderbufferFormat.Rg32f
            typeof<V3d>, RenderbufferFormat.Rgb32f
            typeof<V4d>, RenderbufferFormat.Rgba32f
        ]
        

    let private formatToType =
        LookupTable.lookupTable [
            RenderbufferFormat.DepthComponent, typeof<float>
            RenderbufferFormat.R3G3B2, typeof<V3d>
            RenderbufferFormat.Rgb4, typeof<V3d>
            RenderbufferFormat.Rgb5, typeof<V3d>
            RenderbufferFormat.Rgb8, typeof<V3d>
            RenderbufferFormat.Rgb10, typeof<V3d>
            RenderbufferFormat.Rgb12, typeof<V3d>
            RenderbufferFormat.Rgb16, typeof<V3d>
            RenderbufferFormat.Rgba2, typeof<V4d>
            RenderbufferFormat.Rgba4, typeof<V4d>
            RenderbufferFormat.Rgba8, typeof<V4d>
            RenderbufferFormat.Rgb10A2, typeof<V4d>
            RenderbufferFormat.Rgba12, typeof<V4d>
            RenderbufferFormat.Rgba16, typeof<V4d>
            RenderbufferFormat.DepthComponent16, typeof<float>
            RenderbufferFormat.DepthComponent24, typeof<float>
            RenderbufferFormat.DepthComponent32, typeof<float>
            RenderbufferFormat.R8, typeof<float>
            RenderbufferFormat.R16, typeof<float>
            RenderbufferFormat.Rg8, typeof<V2d>
            RenderbufferFormat.Rg16, typeof<V2d>
            RenderbufferFormat.R16f, typeof<float>
            RenderbufferFormat.R32f, typeof<float>
            RenderbufferFormat.Rg16f, typeof<V2d>
            RenderbufferFormat.Rg32f, typeof<V2d>
            RenderbufferFormat.R8i, typeof<float>
            RenderbufferFormat.R8ui, typeof<float>
            RenderbufferFormat.R16i, typeof<float>
            RenderbufferFormat.R16ui, typeof<float>
            RenderbufferFormat.R32i, typeof<float>
            RenderbufferFormat.R32ui, typeof<float>
            RenderbufferFormat.Rg8i, typeof<V2d>
            RenderbufferFormat.Rg8ui, typeof<V2d>
            RenderbufferFormat.Rg16i, typeof<V2d>
            RenderbufferFormat.Rg16ui, typeof<V2d>
            RenderbufferFormat.Rg32i, typeof<V2d>
            RenderbufferFormat.Rg32ui, typeof<V2d>
            RenderbufferFormat.DepthStencil, typeof<float>
            RenderbufferFormat.Rgba32f, typeof<V4d>
            RenderbufferFormat.Rgb32f, typeof<V3d>
            RenderbufferFormat.Rgba16f, typeof<V4d>
            RenderbufferFormat.Rgb16f, typeof<V3d>
            RenderbufferFormat.Depth24Stencil8, typeof<float>
            RenderbufferFormat.R11fG11fB10f, typeof<V3d>
            RenderbufferFormat.Rgb9E5, typeof<V3d>
            RenderbufferFormat.Srgb8, typeof<V3d>
            RenderbufferFormat.Srgb8Alpha8, typeof<V4d>
            RenderbufferFormat.DepthComponent32f, typeof<float>
            RenderbufferFormat.Depth32fStencil8, typeof<float>
            RenderbufferFormat.StencilIndex1, typeof<int>
            RenderbufferFormat.StencilIndex4, typeof<int>
            RenderbufferFormat.StencilIndex8, typeof<int>
            RenderbufferFormat.StencilIndex16, typeof<int>
            RenderbufferFormat.Rgba32ui, typeof<V4d>
            RenderbufferFormat.Rgb32ui, typeof<V3d>
            RenderbufferFormat.Rgba16ui, typeof<V4d>
            RenderbufferFormat.Rgb16ui, typeof<V3d>
            RenderbufferFormat.Rgba8ui, typeof<V4d>
            RenderbufferFormat.Rgb8ui, typeof<V3d>
            RenderbufferFormat.Rgba32i, typeof<V4d>
            RenderbufferFormat.Rgb32i, typeof<V3d>
            RenderbufferFormat.Rgba16i, typeof<V4d>
            RenderbufferFormat.Rgb16i, typeof<V3d>
            RenderbufferFormat.Rgba8i, typeof<V4d>
            RenderbufferFormat.Rgb8i, typeof<V3d>
            RenderbufferFormat.Rgb10A2ui, typeof<V4d>
        ]

    type AttachmentSignature with
        member x.GetType(name : Symbol) =
            match builtInTypes.TryGetValue name with
                | (true, t) -> t
                | _ -> formatToType x.format
                        
        static member ofType (t : Type) =
            { format = typeToFormat t; samples = 1 }

    type SamplerState with
        member x.SamplerStateDescription = toSamplerStateDescription x

    type IFramebufferSignature with
        member x.Link(effect : Effect, depthRange : Range1d, flip : bool) =
            let outputs = 
                x.ColorAttachments 
                    |> Map.toList 
                    |> List.map (fun (slot, (name, att)) ->
                        match builtInTypes.TryGetValue name with
                            | (true, t) -> (string name, t, slot)
                            | _ -> (string name, formatToType att.format, slot)
                        
                       )

            let config = 
                { EffectConfig.ofList outputs with
                    depthRange = depthRange
                    flipHandedness = flip
                }

            effect |> Effect.toModule config

        member x.ExtractSemantics() =
            let colors = x.ColorAttachments |> Map.toSeq |> Seq.map (fun (k,(i,s)) -> (k,i)) |> Seq.toList
            match x.DepthAttachment with
                | None -> 
                    colors
                | Some d -> 
                    (-1,DefaultSemantic.Depth) :: colors


    type FShadeSurface private(effect : FShadeEffect) =
        static let surfaceCache = System.Collections.Concurrent.ConcurrentDictionary<string, FShadeSurface>()

        let cache = Dict<IFramebufferSignature, BackendSurface>()
        let uniforms = SymDict.empty
        let samplerStates = SymDict.empty

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

        static member Get(e : FShadeEffect) =
            surfaceCache.GetOrAdd(e.Id, fun _ -> FShadeSurface(e))

        member x.Effect = effect

        interface IGeneratedSurface with
            member x.Generate (r : IRuntime, signature : IFramebufferSignature) =
                r.AssembleEffect(effect, signature) 

    let toFShadeSurface (e : FShadeEffect) = FShadeSurface.Get e :> ISurface

    let inline toEffect a = Effect.ofFunction a


[<AbstractClass; Sealed; Extension>]
type FShadeRuntimeExtensions private() =

    static let toSurface (l : list<FShadeEffect>) =
        match l with
            | [s] -> FShadeSurface.Get s
            | l -> FShadeSurface.Get (FShade.Effect.compose l)

    [<Extension>]
    static member PrepareEffect (this : IRuntime, signature : IFramebufferSignature, l : list<FShadeEffect>) =
        this.PrepareSurface(
            signature,
            toSurface l
        )

    [<Extension>]
    static member PrepareEffect (this : IRuntime, signature : IFramebufferSignature, [<ParamArray>] effects : array<FShadeEffect>) =
        let l = List.ofArray(effects)
        this.PrepareSurface(
            signature,
            toSurface l
        )

    [<Extension>]
    static member PrepareEffect (this : IRuntime, l : list<FShadeEffect>) =
        this.PrepareSurface(
            toSurface l
        )

    [<Extension>]
    static member PrepareEffect (this : IRuntime, [<ParamArray>] effects : array<FShadeEffect>) =
        let l = List.ofArray(effects)
        this.PrepareSurface(
            toSurface l
        )

    [<Extension>]
    static member PrepareEffect (this : IRuntime, signature : IFramebufferSignature, l : IMod<list<FShadeEffect>>) =
        let mutable current = None
        l |> Mod.map (fun l ->
            let newPrep = 
                this.PrepareSurface(
                    signature,
                    toSurface l
                )
            match current with
                | Some c -> this.DeleteSurface c
                | None -> ()
            current <- Some newPrep
            newPrep :> ISurface
        )

