namespace Aardvark.Rendering

open Aardvark.Base
open Aardvark.Rendering
open FShade
open System.Collections.Concurrent
open System.Runtime.CompilerServices
open System.Collections.Generic
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open FSharp.Data.Adaptive
open System



type PositionAttribute() = inherit SemanticAttribute(DefaultSemantic.Positions.ToString())
type TexCoordAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorCoordinates.ToString())
type WorldPositionAttribute() = inherit SemanticAttribute("WorldPosition")
type NormalAttribute() = inherit SemanticAttribute(DefaultSemantic.Normals.ToString())
type BiNormalAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorUTangents.ToString())
type TangentAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorVTangents.ToString())
type ColorAttribute() = inherit SemanticAttribute(DefaultSemantic.Colors.ToString())
type InstanceTrafoAttribute() = inherit SemanticAttribute(DefaultSemantic.InstanceTrafo.ToString())
type InstanceTrafoInvAttribute() = inherit SemanticAttribute(DefaultSemantic.InstanceTrafoInv.ToString())

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

    let private toSamplerState (state : SamplerState) : Aardvark.Rendering.SamplerState =

        let def =
            { SamplerState.Default with
                MaxAnisotropy = match state.Filter with Some Filter.Anisotropic -> 16 | _ -> 1 }

        let filter =
            LookupTable.lookupTable [
                Filter.Anisotropic,                  Aardvark.Rendering.TextureFilter.Anisotropic
                Filter.MinLinearMagMipPoint,         Aardvark.Rendering.TextureFilter.MinLinearMagMipPoint
                Filter.MinLinearMagPointMipLinear,   Aardvark.Rendering.TextureFilter.MinLinearMagPointMipLinear
                Filter.MinMagLinearMipPoint,         Aardvark.Rendering.TextureFilter.MinMagLinearMipPoint
                Filter.MinMagMipLinear,              Aardvark.Rendering.TextureFilter.MinMagMipLinear
                Filter.MinMagMipPoint,               Aardvark.Rendering.TextureFilter.MinMagMipPoint
                Filter.MinMagPointMipLinear,         Aardvark.Rendering.TextureFilter.MinMagPointMipLinear
                Filter.MinPointMagLinearMipPoint,    Aardvark.Rendering.TextureFilter.MinPointMagLinearMipPoint
                Filter.MinPointMagMipLinear,         Aardvark.Rendering.TextureFilter.MinPointMagMipLinear
                Filter.MinMagPoint,                  Aardvark.Rendering.TextureFilter.MinMagPoint
                Filter.MinMagLinear,                 Aardvark.Rendering.TextureFilter.MinMagLinear
                Filter.MinPointMagLinear,            Aardvark.Rendering.TextureFilter.MinPointMagLinear
                Filter.MinLinearMagPoint,            Aardvark.Rendering.TextureFilter.MinLinearMagPoint
            ]

        let wrap =
            LookupTable.lookupTable [
                WrapMode.Wrap,         Aardvark.Rendering.WrapMode.Wrap
                WrapMode.Mirror,       Aardvark.Rendering.WrapMode.Mirror
                WrapMode.Clamp,        Aardvark.Rendering.WrapMode.Clamp
                WrapMode.Border,       Aardvark.Rendering.WrapMode.Border
                WrapMode.MirrorOnce,   Aardvark.Rendering.WrapMode.MirrorOnce
            ]

        let cmp =
            LookupTable.lookupTable [
                ComparisonFunction.Greater,        Aardvark.Rendering.ComparisonFunction.Greater
                ComparisonFunction.GreaterOrEqual, Aardvark.Rendering.ComparisonFunction.GreaterOrEqual
                ComparisonFunction.Less,           Aardvark.Rendering.ComparisonFunction.Less
                ComparisonFunction.LessOrEqual,    Aardvark.Rendering.ComparisonFunction.LessOrEqual
                ComparisonFunction.Equal,          Aardvark.Rendering.ComparisonFunction.Equal
                ComparisonFunction.NotEqual,       Aardvark.Rendering.ComparisonFunction.NotEqual
                ComparisonFunction.Never,          Aardvark.Rendering.ComparisonFunction.Never
                ComparisonFunction.Always,         Aardvark.Rendering.ComparisonFunction.Always
            ]

        {
            Filter        = state.Filter        |> Option.map filter   |> Option.defaultValue def.Filter
            BorderColor   = state.BorderColor                          |> Option.defaultValue def.BorderColor
            AddressU      = state.AddressU      |> Option.map wrap     |> Option.defaultValue def.AddressU
            AddressV      = state.AddressV      |> Option.map wrap     |> Option.defaultValue def.AddressV
            AddressW      = state.AddressW      |> Option.map wrap     |> Option.defaultValue def.AddressW
            Comparison    = state.Comparison    |> Option.map cmp      |> Option.defaultValue def.Comparison
            MaxAnisotropy = state.MaxAnisotropy                        |> Option.defaultValue def.MaxAnisotropy
            MinLod        = state.MinLod        |> Option.map float32  |> Option.defaultValue def.MinLod
            MaxLod        = state.MaxLod        |> Option.map float32  |> Option.defaultValue def.MaxLod
            MipLodBias    = state.MipLodBias    |> Option.map float32  |> Option.defaultValue def.MipLodBias
        }

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

    [<GLSLIntrinsic("gl_DeviceIndex", "GL_EXT_device_group")>]
    let private deviceIndex() : int = onlyInShaderCode "deviceIndex"

    open FShade.Imperative
    let private withDeviceIndex (deviceCount : int) (e : Effect) =
        if deviceCount = 1 then 
            e 
        else
            match e.GeometryShader with
                | Some gs ->
                    if gs.shaderInvocations % deviceCount <> 0 then
                        failwithf "[FShade] multi gpu setup with %d shader invocations and %d devices is not implemented" gs.shaderInvocations deviceCount
                    let newInvocations = gs.shaderInvocations / deviceCount

                    let gs = 
                        gs |> Shader.substituteReads (fun kind typ name index ->
                            match kind, index with
                                | ParameterKind.Input, None when name = Intrinsics.InvocationId ->
                                    if newInvocations = 1 then
                                        Some <@@ deviceIndex() @@>
                                    else
                                        let iid = Expr.ReadInput<int>(kind, name)
                                        let did = <@ deviceIndex() @>

                                        Some <@@ %did * newInvocations + %iid @@>
                                | _ ->
                                    None
                        )

                    Effect.add { gs with shaderInvocations = newInvocations } e
                | None -> e

    [<StructuredFormatDisplay("{AsString}")>]
    type FramebufferRange = 
        { 
            frMin : V2i
            frMax : V2i; 
            frLayers : Range1i 
        } with

        override x.ToString() =
            sprintf "{ min = %A; size = %A; layers = %A }" x.frMin (V2i.II + x.frMax - x.frMin) x.frLayers
    
        member private x.AsString = x.ToString()
        
        member x.Split(deviceCount : int) =
            if deviceCount = 1 then
                [| x |]
            else
                let layerCount = (1 + x.frLayers.Max - x.frLayers.Min)
                if layerCount = 1 then
                    let size = V2i.II + x.frMax - x.frMin
                    let perDevice = size.X / deviceCount

                    Array.init deviceCount (fun di ->
                        let offset = V2i(perDevice * di, 0)
                        let size = 
                            if di = deviceCount - 1 then size - offset
                            else V2i(perDevice, size.Y)
                        { x with frMin = x.frMin + offset; frMax = x.frMin + offset + size - V2i.II }
                    )
                elif layerCount % deviceCount = 0 then
                    let perDevice = layerCount / deviceCount
                    let firstlayer = x.frLayers.Min
                    Array.init deviceCount (fun di -> { x with frLayers = Range1i(firstlayer + perDevice * di, firstlayer + perDevice * di - 1) })
                else
                    failwithf "[FShade] cannot render to %d layers using %d devices" layerCount deviceCount



    type AttachmentSignature with
        member x.GetType(name : Symbol) =
            match builtInTypes.TryGetValue name with
                | (true, t) -> t
                | _ -> formatToType x.format
                        
        static member ofType (t : Type) =
            { format = typeToFormat t; samples = 1 }

    type SamplerState with
        member x.SamplerState = toSamplerState x

    let toInputTopology =
        LookupTable.lookupTable [
            IndexedGeometryMode.PointList, InputTopology.Point
            IndexedGeometryMode.LineList, InputTopology.Line
            IndexedGeometryMode.LineStrip, InputTopology.Line
            IndexedGeometryMode.LineAdjacencyList, InputTopology.LineAdjacency
            IndexedGeometryMode.TriangleList, InputTopology.Triangle
            IndexedGeometryMode.TriangleStrip, InputTopology.Triangle
            IndexedGeometryMode.TriangleAdjacencyList, InputTopology.TriangleAdjacency
            IndexedGeometryMode.QuadList, InputTopology.Patch 4
        ]

    type IFramebufferSignature with

        member x.EffectConfig(depthRange : Range1d, flip : bool) =
            let outputs = 
                x.ColorAttachments 
                    |> Map.toList 
                    |> List.map (fun (slot, (name, att)) ->
                        match builtInTypes.TryGetValue name with
                            | (true, t) -> (string name, t, slot)
                            | _ -> (string name, formatToType att.format, slot)
                        
                       )
            { EffectConfig.ofList outputs with
                depthRange = depthRange
                flipHandedness = flip
            }


        member x.Link(effect : Effect, depthRange : Range1d, flip : bool, top : IndexedGeometryMode) =
            let outputs = 
                x.ColorAttachments 
                    |> Map.toList 
                    |> List.map (fun (slot, (name, att)) ->
                        match builtInTypes.TryGetValue name with
                            | (true, t) -> (string name, t, slot)
                            | _ -> (string name, formatToType att.format, slot)
                        
                       )

            let top = toInputTopology top

            let config = 
                { EffectConfig.ofList outputs with
                    depthRange = depthRange
                    flipHandedness = flip
                }


            let deviceCount = x.Runtime.DeviceCount
            if deviceCount > 1 then
                if x.LayerCount > 1 then
                    effect 
                        // TODO: other topologies????
                        |> Effect.toLayeredEffect x.LayerCount (x.PerLayerUniforms |> Seq.map (fun n -> n, n) |> Map.ofSeq) top
                        |> withDeviceIndex deviceCount
                        |> Effect.toModule config
                else 
                    effect 
                        // TODO: other topologies????
                        |> Effect.toMultiViewportEffect deviceCount Map.empty top
                        |> withDeviceIndex deviceCount
                        |> Effect.toModule config
            else
                
                if x.LayerCount > 1 then
                    effect 
                        // TODO: other topologies????
                        |> Effect.toLayeredEffect x.LayerCount (x.PerLayerUniforms |> Seq.map (fun n -> n, n) |> Map.ofSeq) top
                        |> Effect.toModule config
                else
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

        interface ISurface

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
    static member PrepareEffect (this : IRuntime, signature : IFramebufferSignature, l : aval<list<FShadeEffect>>) =
        let mutable current = None
        l |> AVal.map (fun l ->
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


