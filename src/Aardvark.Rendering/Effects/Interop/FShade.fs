namespace Aardvark.Rendering

open Aardvark.Base
open Aardvark.Rendering
open FShade
open System.Runtime.CompilerServices
open Microsoft.FSharp.Quotations
open FSharp.Data.Adaptive
open System

type PositionAttribute() = inherit SemanticAttribute(DefaultSemantic.Positions.ToString())
type TexCoordAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorCoordinates.ToString())
type WorldPositionAttribute() = inherit SemanticAttribute("WorldPosition")
type NormalAttribute() = inherit SemanticAttribute(DefaultSemantic.Normals.ToString())
type BiNormalAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorVTangents.ToString())
type TangentAttribute() = inherit SemanticAttribute(DefaultSemantic.DiffuseColorUTangents.ToString())
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

    module private ImageFormat =

        let toTextureFormat =
            LookupTable.lookupTable [
                ImageFormat.Rgba32f,      TextureFormat.Rgba32f
                ImageFormat.Rgba16f,      TextureFormat.Rgba16f
                ImageFormat.Rg32f,        TextureFormat.Rg32f
                ImageFormat.Rg16f,        TextureFormat.Rg16f
                ImageFormat.R11fG11fB10f, TextureFormat.R11fG11fB10f
                ImageFormat.R32f,         TextureFormat.R32f
                ImageFormat.R16f,         TextureFormat.R16f

                ImageFormat.Rgba16,       TextureFormat.Rgba16
                ImageFormat.Rgb10A2,      TextureFormat.Rgb10A2
                ImageFormat.Rgba8,        TextureFormat.Rgba8
                ImageFormat.Rg16,         TextureFormat.Rg16
                ImageFormat.Rg8,          TextureFormat.Rg8
                ImageFormat.R16,          TextureFormat.R16
                ImageFormat.R8,           TextureFormat.R8

                ImageFormat.Rgba16Snorm,  TextureFormat.Rgba16Snorm
                ImageFormat.Rgba8Snorm,   TextureFormat.Rgba8Snorm
                ImageFormat.Rg16Snorm,    TextureFormat.Rg16Snorm
                ImageFormat.Rg8Snorm,     TextureFormat.Rg8Snorm
                ImageFormat.R16Snorm,     TextureFormat.R16Snorm
                ImageFormat.R8Snorm,      TextureFormat.R8Snorm

                ImageFormat.Rgba32ui,     TextureFormat.Rgba32ui
                ImageFormat.Rgba16ui,     TextureFormat.Rgba16ui
                ImageFormat.Rgb10A2ui,    TextureFormat.Rgb10A2ui
                ImageFormat.Rgba8ui,      TextureFormat.Rgba8ui
                ImageFormat.Rg32ui,       TextureFormat.Rg32ui
                ImageFormat.Rg16ui,       TextureFormat.Rg16ui
                ImageFormat.Rg8ui,        TextureFormat.Rg8ui
                ImageFormat.R32ui,        TextureFormat.R32ui
                ImageFormat.R16ui,        TextureFormat.R16ui
                ImageFormat.R8ui,         TextureFormat.R8ui

                ImageFormat.Rgba32i,      TextureFormat.Rgba32i
                ImageFormat.Rgba16i,      TextureFormat.Rgba16i
                ImageFormat.Rgba8i,       TextureFormat.Rgba8i
                ImageFormat.Rg32i,        TextureFormat.Rg32i
                ImageFormat.Rg16i,        TextureFormat.Rg16i
                ImageFormat.Rg8i,         TextureFormat.Rg8i
                ImageFormat.R32i,         TextureFormat.R32i
                ImageFormat.R16i,         TextureFormat.R16i
                ImageFormat.R8i,          TextureFormat.R8i
        ]

    module private GLSLType =

        let rec isIntegerType = function
            | GLSL.GLSLType.Int _ -> true
            | GLSL.GLSLType.Vec (_, t)
            | GLSL.GLSLType.Mat (_, _, t)
            | GLSL.GLSLType.Array (_, t, _)
            | GLSL.GLSLType.DynamicArray (t, _) -> isIntegerType t
            | GLSL.GLSLType.Sampler s -> isIntegerType s.valueType
            | GLSL.GLSLType.Image i -> isIntegerType i.valueType
            | _ -> false

    module private SamplerDimension =

        let toTextureDimension = function
            | SamplerDimension.Sampler1d   -> TextureDimension.Texture1D
            | SamplerDimension.Sampler2d   -> TextureDimension.Texture2D
            | SamplerDimension.Sampler3d   -> TextureDimension.Texture3D
            | SamplerDimension.SamplerCube -> TextureDimension.TextureCube
            | d -> failwithf "Invalid sampler dimension %A" d

    module private GLSLSampler =

        let private (|BaseName|_|) (name : string) =
            if name.Length <= 1 || name.[name.Length - 1] <> '0' then None
            else Some <| name.Substring(0, name.Length - 1)

        let private areElementNames (baseName : string) (baseIndex : int) (names : string list) =
            names |> List.indexed |> List.forall (fun (i, n) ->
                n = baseName + string (baseIndex + i)
            )

        let (|Array|_|) (sampler : GLSL.GLSLSampler) =
            match sampler.samplerTextures with
            | [] | _::[] ->
                None
            | (BaseName baseName, state)::xs ->
                let names, samplers = List.unzip xs
                if names |> areElementNames baseName 1 &&
                   samplers |> List.forall ((=) state) then
                    Some (baseName, state)
                else
                    None
            | _ ->
                None

    let private toSamplerState (state : SamplerState) : Aardvark.Rendering.SamplerState =

        let def =
            { SamplerState.Default with
                MaxAnisotropy = match state.Filter with Some Filter.Anisotropic -> 16 | _ -> 1 }

        let filter =
            LookupTable.lookupTable [
                Filter.Anisotropic,                  Aardvark.Rendering.TextureFilter.MinMagMipLinear
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

    // Output types for color-renderable texture formats
    let private colorFormatToType =
        LookupTable.lookupTable' [
            TextureFormat.Bgr8,         typeof<V3d>
            TextureFormat.Bgra8,        typeof<V4d>
            TextureFormat.R3G3B2,       typeof<V3d>
            TextureFormat.Rgb4,         typeof<V3d>
            TextureFormat.Rgb5,         typeof<V3d>
            TextureFormat.Rgb8,         typeof<V3d>
            TextureFormat.Rgb10,        typeof<V3d>
            TextureFormat.Rgb12,        typeof<V3d>
            TextureFormat.Rgb16,        typeof<V3d>
            TextureFormat.Rgba2,        typeof<V4d>
            TextureFormat.Rgba4,        typeof<V4d>
            TextureFormat.Rgb5A1,       typeof<V4d>
            TextureFormat.Rgba8,        typeof<V4d>
            TextureFormat.Rgb10A2,      typeof<V4d>
            TextureFormat.Rgba12,       typeof<V4d>
            TextureFormat.Rgba16,       typeof<V4d>
            TextureFormat.R8,           typeof<float>
            TextureFormat.R16,          typeof<float>
            TextureFormat.Rg8,          typeof<V2d>
            TextureFormat.Rg16,         typeof<V2d>
            TextureFormat.R16f,         typeof<float>
            TextureFormat.R32f,         typeof<float>
            TextureFormat.Rg16f,        typeof<V2d>
            TextureFormat.Rg32f,        typeof<V2d>
            TextureFormat.R8i,          typeof<int>
            TextureFormat.R8ui,         typeof<uint>
            TextureFormat.R16i,         typeof<int>
            TextureFormat.R16ui,        typeof<uint>
            TextureFormat.R32i,         typeof<int>
            TextureFormat.R32ui,        typeof<uint>
            TextureFormat.Rg8i,         typeof<V2i>
            TextureFormat.Rg8ui,        typeof<V2ui>
            TextureFormat.Rg16i,        typeof<V2i>
            TextureFormat.Rg16ui,       typeof<V2ui>
            TextureFormat.Rg32i,        typeof<V2i>
            TextureFormat.Rg32ui,       typeof<V2ui>
            TextureFormat.Rgba32f,      typeof<V4d>
            TextureFormat.Rgb32f,       typeof<V3d>
            TextureFormat.Rgba16f,      typeof<V4d>
            TextureFormat.Rgb16f,       typeof<V3d>
            TextureFormat.R11fG11fB10f, typeof<V3d>
            TextureFormat.Rgb9E5,       typeof<V3d>
            TextureFormat.Srgb8,        typeof<V3d>
            TextureFormat.Srgb8Alpha8,  typeof<V4d>
            TextureFormat.Rgba32ui,     typeof<V4ui>
            TextureFormat.Rgb32ui,      typeof<V3ui>
            TextureFormat.Rgba16ui,     typeof<V4ui>
            TextureFormat.Rgb16ui,      typeof<V3ui>
            TextureFormat.Rgba8ui,      typeof<V4ui>
            TextureFormat.Rgb8ui,       typeof<V3ui>
            TextureFormat.Rgba32i,      typeof<V4i>
            TextureFormat.Rgb32i,       typeof<V3i>
            TextureFormat.Rgba16i,      typeof<V4i>
            TextureFormat.Rgb16i,       typeof<V3i>
            TextureFormat.Rgba8i,       typeof<V4i>
            TextureFormat.Rgb8i,        typeof<V3i>
            TextureFormat.R8Snorm,      typeof<float>
            TextureFormat.Rg8Snorm,     typeof<V2d>
            TextureFormat.Rgb8Snorm,    typeof<V3d>
            TextureFormat.Rgba8Snorm,   typeof<V4d>
            TextureFormat.R16Snorm,     typeof<float>
            TextureFormat.Rg16Snorm,    typeof<V2d>
            TextureFormat.Rgb16Snorm,   typeof<V3d>
            TextureFormat.Rgba16Snorm,  typeof<V4d>
            TextureFormat.Rgb10A2ui,    typeof<V4ui>
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
                        gs |> Shader.substituteReads (fun kind typ name index slot ->
                            match kind, index with
                                | ParameterKind.Input, None when name = Intrinsics.InvocationId ->
                                    if newInvocations = 1 then
                                        Some <@@ deviceIndex() @@>
                                    else
                                        let iid = Expr.ReadInput<int>(kind, name, slot)
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
        member x.Type =
            match colorFormatToType x.Format with
            | Some typ -> typ
            | _ ->
                failwithf "%A is not a supported color-renderable format" x.Format

    type SamplerState with
        member x.SamplerState = toSamplerState x

    type SamplerDimension with
        member x.TextureDimension = SamplerDimension.toTextureDimension x

    type GLSL.GLSLSamplerType with
        member x.IsInteger = GLSLType.isIntegerType x.valueType

    type GLSL.GLSLSampler with

        /// Base name and state of array of samplers.
        /// Returns None if not an array, elements do not follow the naming convention, or elements have varying sampler states.
        member x.Array =
            match x with
            | GLSLSampler.Array name -> Some name
            | _ -> None

        member x.Dimension =
            x.samplerType.dimension.TextureDimension

    type ImageFormat with
        member x.TextureFormat = ImageFormat.toTextureFormat x

    type GLSL.GLSLImageType with
        member x.IsInteger = GLSLType.isIntegerType x.valueType

    type GLSL.GLSLImage with

        member x.Dimension =
            x.imageType.dimension.TextureDimension

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

    // Used as part of the key in shader caches
    type FramebufferLayout =
        {
            Samples : int
            ColorAttachments : Map<int, AttachmentSignature>
            DepthStencilAttachment : Option<TextureFormat>
            LayerCount : int
            PerLayerUniforms : Set<string>
        }

        member x.EffectConfig(depthRange : Range1d, flip : bool) =
            let outputs =
                x.ColorAttachments
                |> Map.toList
                |> List.map (fun (slot, att) -> string att.Name, att.Type, slot)

            { EffectConfig.ofList outputs with
                depthRange = depthRange
                flipHandedness = flip
            }


        member x.Link(effect : Effect, deviceCount : int, depthRange : Range1d, flip : bool, top : IndexedGeometryMode) =
            let outputs =
                x.ColorAttachments
                |> Map.toList
                |> List.map (fun (slot, att) -> string att.Name, att.Type, slot)

            let top = toInputTopology top

            let config =
                { EffectConfig.ofList outputs with
                    depthRange = depthRange
                    flipHandedness = flip
                }

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

    type IFramebufferSignature with
        member x.Layout : FramebufferLayout =
            {
                Samples = x.Samples
                ColorAttachments = x.ColorAttachments
                DepthStencilAttachment = x.DepthStencilAttachment
                LayerCount = x.LayerCount
                PerLayerUniforms = x.PerLayerUniforms
            }

        member x.EffectConfig(depthRange : Range1d, flip : bool) = x.Layout.EffectConfig(depthRange, flip)
        member x.Link(effect : Effect, depthRange : Range1d, flip : bool, top : IndexedGeometryMode) = x.Layout.Link(effect, x.Runtime.DeviceCount, depthRange, flip, top)


    type FShadeSurface private(effect : FShadeEffect) =
        static let surfaceCache = System.Collections.Concurrent.ConcurrentDictionary<string, FShadeSurface>()

        static member Get(e : FShadeEffect) =
            surfaceCache.GetOrAdd(e.Id, fun _ -> FShadeSurface(e))

        member x.Effect = effect

        interface ISurface

    let toFShadeSurface (e : FShadeEffect) = FShadeSurface.Get e :> ISurface

    let inline toEffect a = Effect.ofFunction a

    module Surface =
        let effectPool (effects : FShade.Effect[]) (active : aval<int>) =
            let compile (cfg : FShade.EffectConfig) =
                let modules1 = effects |> Array.map (FShade.Effect.toModule cfg)
                let layout = FShade.EffectInputLayout.ofModules modules1
                let modules = modules1 |> Array.map (FShade.EffectInputLayout.apply layout)
                let current = active |> AVal.map (fun i -> modules.[i % modules.Length])
                layout, current

            Surface.FShade compile


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


