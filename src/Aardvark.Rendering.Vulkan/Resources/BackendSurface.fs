namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type PipelineInfo =
    {
        pInputs         : list<ShaderIOParameter>
        pOutputs        : list<ShaderIOParameter>
        pUniformBlocks  : list<ShaderUniformBlock>
        pStorageBlocks  : list<ShaderUniformBlock>
        pTextures       : list<ShaderTextureInfo>
        pEffectLayout   : Option<FShade.EffectInputLayout>
    }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PipelineInfo =
    open FShade
    open FShade.Imperative

    let fshadeBackend =
        FShade.GLSL.Backend.Create {
            version                 = Version(4,5)
            enabledExtensions       = Set.ofList [ "GL_ARB_tessellation_shader"; "GL_ARB_separate_shader_objects"; "GL_ARB_shading_language_420pack" ]
            createUniformBuffers    = true
            bindingMode             = GLSL.BindingMode.Global
            createDescriptorSets    = true
            stepDescriptorSets      = false
            createInputLocations    = true
            createPerStageUniforms  = false
            reverseMatrixLogic      = true
        }

    let fshadeConfig =
        {
            depthRange = Range1d(0.0, 1.0)
            flipHandedness = false
            outputs = Map.empty
            lastStage = ShaderStage.Fragment
        }
           
    module ShaderType =
        open Aardvark.Base.TypeInfo.Patterns

        let private primitiveTypes =
            Dictionary.ofList [
                typeof<unit>, ShaderType.Void
                typeof<bool>, ShaderType.Bool

                typeof<int8>, ShaderType.Int(32, true)
                typeof<int16>, ShaderType.Int(32, true)
                typeof<int32>, ShaderType.Int(32, true)
                typeof<int64>, ShaderType.Int(64, true)
                typeof<uint8>, ShaderType.Int(32, false)
                typeof<uint16>, ShaderType.Int(32, false)
                typeof<uint32>, ShaderType.Int(32, false)
                typeof<uint64>, ShaderType.Int(64, false)

                typeof<float16>, ShaderType.Float(16)
                typeof<float32>, ShaderType.Float(32)
                typeof<float>, ShaderType.Float(32)
                typeof<decimal>, ShaderType.Float(32)

            ]

        let private toImageFormat =
            LookupTable.lookupTable [
                typeof<Formats.rgba32f>, SpirV.ImageFormat.Rgba32f
                typeof<Formats.rgba16f>, SpirV.ImageFormat.Rgba16f
                typeof<Formats.rg32f>, SpirV.ImageFormat.Rg32f
                typeof<Formats.rg16f>, SpirV.ImageFormat.Rg16f
                typeof<Formats.r11g11b10f>, SpirV.ImageFormat.R11fG11fB10f
                typeof<Formats.r32f>, SpirV.ImageFormat.R32f
                typeof<Formats.r16f>, SpirV.ImageFormat.R16f

                typeof<Formats.rgba16>, SpirV.ImageFormat.Rgba16
                typeof<Formats.rgb10a2>, SpirV.ImageFormat.Rgb10A2
                typeof<Formats.rgba8>, SpirV.ImageFormat.Rgba8
                typeof<Formats.rg16>, SpirV.ImageFormat.Rg16
                typeof<Formats.rg8>, SpirV.ImageFormat.Rg8
                typeof<Formats.r16>, SpirV.ImageFormat.R16
                typeof<Formats.r8>, SpirV.ImageFormat.R8

                typeof<Formats.rgba16_snorm>, SpirV.ImageFormat.Rgba16Snorm
                typeof<Formats.rgba8_snorm>, SpirV.ImageFormat.Rgba8Snorm
                typeof<Formats.rg16_snorm>, SpirV.ImageFormat.Rg16Snorm
                typeof<Formats.rg8_snorm>, SpirV.ImageFormat.Rg8Snorm
                typeof<Formats.r16_snorm>, SpirV.ImageFormat.R16Snorm
                typeof<Formats.r8_snorm>, SpirV.ImageFormat.R8Snorm

                typeof<Formats.rgba32ui>, SpirV.ImageFormat.Rgba32ui
                typeof<Formats.rgba16ui>, SpirV.ImageFormat.Rgba16ui
                typeof<Formats.rgb10a2ui>, SpirV.ImageFormat.Rgb10a2ui
                typeof<Formats.rgba8ui>, SpirV.ImageFormat.Rgba8ui
                typeof<Formats.rg32ui>, SpirV.ImageFormat.Rg32ui
                typeof<Formats.rg16ui>, SpirV.ImageFormat.Rg16ui
                typeof<Formats.rg8ui>, SpirV.ImageFormat.Rg8ui
                typeof<Formats.r32ui>, SpirV.ImageFormat.R32ui
                typeof<Formats.r16ui>, SpirV.ImageFormat.R16ui
                typeof<Formats.r8ui>, SpirV.ImageFormat.R8ui

                typeof<Formats.rgba32i>, SpirV.ImageFormat.Rgba32i
                typeof<Formats.rgba16i>, SpirV.ImageFormat.Rgba16i
                typeof<Formats.rgba8i>, SpirV.ImageFormat.Rgba8i
                typeof<Formats.rg32i>, SpirV.ImageFormat.Rg32i
                typeof<Formats.rg16i>, SpirV.ImageFormat.Rg16i
                typeof<Formats.rg8i>, SpirV.ImageFormat.Rg8i
                typeof<Formats.r32i>, SpirV.ImageFormat.R32i
                typeof<Formats.r16i>, SpirV.ImageFormat.R16i
                typeof<Formats.r8i>, SpirV.ImageFormat.R8i
            ]

        let private toDim =
            LookupTable.lookupTable [
                SamplerDimension.Sampler1d, SpirV.Dim.Dim1D
                SamplerDimension.Sampler2d, SpirV.Dim.Dim2D
                SamplerDimension.Sampler3d, SpirV.Dim.Dim3D
                SamplerDimension.SamplerCube, SpirV.Dim.Cube
            ]

        module private List =
            let rec mapError (f : 'a -> Error<'b>) (l : list<'a>) =
                match l with
                    | [] -> Success []
                    | h :: rest ->
                        match f h, mapError f rest with
                            | Success h, Success t -> Success (h :: t)
                            | Error a, Error b -> Error (a + " " + b)
                            | Error a, _ -> Error a
                            | _, Error b -> Error b
        module Error =
            let map (f : 'a -> 'b) (m : Error<'a>) =
                match m with
                    | Success v -> Success(f v)
                    | Error e -> Error e

        let rec ofCType (ct : CType) =
            match ct with
                | CType.CBool -> 
                    Success ShaderType.Bool

                | CType.CVoid -> 
                    Success ShaderType.Void
                    
                | CType.CInt(s,(8 | 16)) -> 
                    ShaderType.Int(32, s) |> Success

                | CType.CInt(s,w) -> 
                    ShaderType.Int(w, s) |> Success
                    
                | CType.CFloat(64) ->
                    ShaderType.Float(32) |> Success

                | CType.CFloat(w) -> 
                    ShaderType.Float(w) |> Success

                | CType.CVector(e,d) -> 
                    match ofCType e with
                        | Success e -> ShaderType.Vector(e, d) |> Success
                        | Error e -> Error e
                        
                | CType.CMatrix(e,r,c) -> 
                    match ofCType e with
                        | Success e -> ShaderType.Matrix(ShaderType.Vector(e, r), c) |> Success
                        | Error e -> Error e

                | CType.CArray(e, l) -> 
                    match ofCType e with
                        | Success e -> ShaderType.Array(e, l) |> Success
                        | Error e -> Error e

                | CType.CPointer(_,e) -> 
                    match ofCType e with
                        | Success e -> ShaderType.RuntimeArray(e) |> Success
                        | Error e -> Error e

                | CType.CStruct(name, fields, _) ->
                    match fields |> List.mapError (fun (t,n) -> t |> ofCType |> Error.map (fun t -> t, n, [])) with
                        | Success fields ->
                            ShaderType.Struct(name, fields) |> Success
                        | Error e ->
                            Error e
                | CType.CIntrinsic { intrinsicTypeName = name } ->
                    Error name
                    
        let rec ofType (t : Type) : ShaderType =
            match t with
                | ImageType(fmt, dim, isArr, isMS, valueType) ->
                    let valueType = ofType valueType
                    let dim = toDim dim
                    let fmt = toImageFormat fmt

                    ShaderType.Image(valueType, dim, 0, isArr, (if isMS then 1 else 0), false, fmt)

                | SamplerType(dim, isArray, isShadow, isMS, valueType) ->
                    let dim = toDim dim
                    let valueType = ofType valueType
                    ShaderType.SampledImage(
                        ShaderType.Image(valueType, dim, (if isShadow then 1 else 0), isArray, (if isMS then 1 else 0), true, SpirV.ImageFormat.Unknown)
                    )

                | t ->
                    let ct = CType.ofType fshadeBackend t
                    match ofCType ct with
                        | Success t -> t
                        | Error e -> failwithf "[Vulkan] bad anarchy motherf***er 666: %A" e

    let ofEffectLayout (layout : EffectInputLayout) (outputs : Map<int, Symbol * AttachmentSignature>) =
            
        let inputs = 
            layout.eInputs
                |> MapExt.toList
                |> List.mapi (fun i (name, t) ->
                    let st, count =
                        match ShaderType.ofType t with
                            | ShaderType.Array(t, len) -> t, len
                            | t -> t, 1

                    let st = PrimitiveType.ofShaderType st

                    {
                        location = i
                        name = name
                        semantic = Symbol.Create name
                        shaderType = st
                        hostType = t
                        count = count
                        isRowMajor = false
                    }
                )

        let outputs =
            outputs
                |> Map.toList
                |> List.map (fun (i,(name,att)) ->
                    let t = att.GetType name
                    let st, count =
                        match ShaderType.ofType t with
                            | ShaderType.Array(t, len) -> t, len
                            | t -> t, 1

                    let st = PrimitiveType.ofShaderType st
                    {
                        location = i
                        name = name.ToString()
                        semantic = name
                        shaderType = st
                        hostType = t
                        count = count
                        isRowMajor = false
                    }
                ) 

        let eImages, eUniforms =
            layout.eUniforms
                |> MapExt.partition (fun name t ->
                    match t with
                        | ArrOf (_, (ImageType _ | SamplerType _))
                        | ArrayOf (ImageType _ | SamplerType _)
                        | ImageType _ 
                        | SamplerType _ -> 
                            true
                        | _ -> 
                            false
                        
                )
                
        let eUniformBuffers =
            if MapExt.isEmpty eUniforms then
                layout.eUniformBuffers
            else
                MapExt.add "Global" eUniforms layout.eUniformBuffers

        let mutable currentBinding = 0
        let newBinding() =
            let b = currentBinding
            currentBinding <- currentBinding + 1
            b

        let uniformBuffers = 
            eUniformBuffers
                |> MapExt.toList
                |> List.map (fun (bufferName, fields) ->
                    if bufferName = "StorageBuffer" then
                        let bindings = 
                            fields |> MapExt.toList |> List.map (fun (name, typ) ->
                                let t = ShaderType.ofType typ
                                {
                                    set = 0
                                    binding = newBinding()
                                    name = name + "SSB"
                                    layout = UniformBufferLayoutStd140.structLayout [t, name, []]
                                }
                            )
                        Choice1Of2 bindings
                    else
                        let fields =
                            fields
                                |> MapExt.toList
                                |> List.map (fun (name, typ) ->
                                    let t = ShaderType.ofType typ
                                    (t, name, [])
                                )

                        Choice2Of2 {
                            set = 0
                            binding = newBinding()
                            name = bufferName
                            layout = UniformBufferLayoutStd140.structLayout fields
                        }
                )

        let textures =
            eImages
                |> MapExt.toList
                |> List.mapi (fun bi (name, typ) ->
                    let set = 0
                    let binding = newBinding()
                    let typ = ShaderType.ofType typ

                    match typ with
                        | ShaderType.SampledImage(ShaderType.Image(resultType,dim,isDepth,isArray,isMS,isSampled,fmt))
                        | ShaderType.Image(resultType,dim,isDepth,isArray,isMS,isSampled,fmt) ->

                            let description = 
                                match MapExt.tryFind name layout.eTextures with
                                    | Some l -> l |> List.map (fun (n,s) -> { textureName = Symbol.Create n; samplerState = s.SamplerStateDescription })
                                    | None -> []

                            {
                                set             = set
                                binding         = binding
                                name            = name
                                count           = 1
                                description     = description
                                resultType      = PrimitiveType.ofShaderType resultType
                                isDepth         = (match isDepth with | 0 -> Some false | 1 -> Some true | _ -> None)
                                isSampled       = isSampled
                                format          = ShaderUniformParameter.ImageFormat.toTextureFormat fmt
                                samplerType =
                                    {
                                        dimension       = ShaderUniformParameter.Dim.toTextureDimension dim
                                        isArray         = isArray
                                        isMultisampled  = (if isMS = 0 then false else true)
                                    }
                            }

                        | ShaderType.Array((ShaderType.SampledImage(ShaderType.Image(resultType,dim,isDepth,isArray,isMS,isSampled,fmt)) | ShaderType.Image(resultType,dim,isDepth,isArray,isMS,isSampled,fmt)), len) ->
                            let description = 
                                match MapExt.tryFind name layout.eTextures with
                                    | Some l -> l |> List.map (fun (n,s) -> { textureName = Symbol.Create n; samplerState = s.SamplerStateDescription })
                                    | None -> []

                            
                            {
                                set             = set
                                binding         = binding
                                name            = name
                                count           = len
                                description     = description
                                resultType      = PrimitiveType.ofShaderType resultType
                                isDepth         = (match isDepth with | 0 -> Some false | 1 -> Some true | _ -> None)
                                isSampled       = isSampled
                                format          = ShaderUniformParameter.ImageFormat.toTextureFormat fmt
                                samplerType =
                                    {
                                        dimension       = ShaderUniformParameter.Dim.toTextureDimension dim
                                        isArray         = isArray
                                        isMultisampled  = (if isMS = 0 then false else true)
                                    }
                            }
                        | _ ->
                            failwith "invalid texture uniform"
                                
                )
 
        { 
            pInputs          = inputs
            pUniformBlocks   = uniformBuffers |> List.choose (function Choice2Of2 b -> Some b | _ -> None)
            pStorageBlocks   = uniformBuffers |> List.collect (function Choice1Of2 b -> b | _ -> [])
            pTextures        = textures
            pOutputs         = outputs
            pEffectLayout    = Some layout
        }
       
  
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BackendSurface =     
    open FShade
    open FShade.Imperative

    // WTF????   
    let shaderStages =
        LookupTable.lookupTable [
            FShade.ShaderStage.Vertex, Aardvark.Base.ShaderStage.Vertex
            FShade.ShaderStage.TessControl, Aardvark.Base.ShaderStage.TessControl
            FShade.ShaderStage.TessEval, Aardvark.Base.ShaderStage.TessEval
            FShade.ShaderStage.Geometry, Aardvark.Base.ShaderStage.Geometry
            FShade.ShaderStage.Fragment, Aardvark.Base.ShaderStage.Fragment
        ]

    let ofModule (module_ : Module) = 
        let effect = unbox<FShade.Effect> module_.userData
          
        let glsl = 
            module_ |> ModuleCompiler.compileGLSL PipelineInfo.fshadeBackend
            
        let entries =
            module_.entries
                |> Seq.choose (fun e -> 
                    let stage = e.decorations |> List.tryPick (function Imperative.EntryDecoration.Stages { self = self } -> Some self | _ -> None)
                    match stage with
                        | Some stage ->
                            Some (shaderStages stage, "main")
                        | None ->
                            None
                ) 
                |> Dictionary.ofSeq


        let samplers = Dictionary.empty

        for KeyValue(k,v) in effect.Uniforms do
            match v.uniformValue with
                | UniformValue.Sampler(texName,sam) ->
                    samplers.[(k, 0)] <- { textureName = Symbol.Create texName; samplerState = sam.SamplerStateDescription }
                | UniformValue.SamplerArray semSams ->
                    for i in 0 .. semSams.Length - 1 do
                        let (sem, sam) = semSams.[i]
                        samplers.[(k, i)] <- { textureName = Symbol.Create sem; samplerState = sam.SamplerStateDescription }
                | _ ->
                    ()

        // TODO: gl_PointSize (builtIn)
        BackendSurface(glsl.code, entries, Map.empty, SymDict.empty, samplers, true)

    let ofEffectSimple (signature : IFramebufferSignature) (effect : FShade.Effect) (topology : IndexedGeometryMode) =
        let module_ = signature.Link(effect, Range1d(0.0, 1.0), false, topology)
        ofModule module_



