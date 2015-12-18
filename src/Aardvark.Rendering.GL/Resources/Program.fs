namespace Aardvark.Rendering.GL

#nowarn "44" //Obsolete warning

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations


[<AutoOpen>]
module private ShaderProgramCounters =
    let addProgram (ctx : Context) =
        Interlocked.Increment(&ctx.MemoryUsage.ShaderProgramCount) |> ignore

    let removeProgram (ctx : Context) =
        Interlocked.Decrement(&ctx.MemoryUsage.ShaderProgramCount) |> ignore


type ActiveUniform = { index : int; location : int; name : string; semantic : string; samplerState : Option<string>; size : int; uniformType : ActiveUniformType; offset : int; isRowMajor : bool } with
    member x.Interface =

        let name =
            if x.name = x.semantic then x.name
            else sprintf "%s : %s" x.name x.semantic

        match x.samplerState with
            | Some sam ->
                sprintf "%A %s; // sampler: %s" x.uniformType name sam
            | None ->
                sprintf "%A %s;" x.uniformType name
                
type UniformBlock = { name : string; index : int; binding : int; fields : list<ActiveUniform>; size : int; referencedBy : Set<ShaderStage> }
type ActiveAttribute = { attributeIndex : int; size : int; name : string; semantic : string; attributeType : ActiveAttribType }

type Shader =
    class 
        val mutable public Context : Context
        val mutable public Handle : int
        val mutable public Stage : ShaderStage
        val mutable public SupportedModes : Option<Set<IndexedGeometryMode>>
        new(ctx : Context, handle : int, stage : ShaderStage, tops) = { Context = ctx; Handle = handle; Stage = stage; SupportedModes = tops}
    end

[<StructuredFormatDisplay("{InterfaceBlock}")>]
type Program =
    {
       Context : Context
       Code : string
       Handle : int
       Shaders : list<Shader>
       UniformBlocks : list<UniformBlock>
       Uniforms : list<ActiveUniform>
       UniformGetters : SymbolDict<obj>
       SamplerStates : SymbolDict<SamplerStateDescription>
       Inputs : list<ActiveAttribute>
       Outputs : list<ActiveAttribute>
       SupportedModes : Option<Set<IndexedGeometryMode>>
    } with

    interface IBackendSurface with
        member x.Handle = x.Handle :> obj
        member x.UniformGetters = x.UniformGetters
        member x.SamplerStates = x.SamplerStates

        member x.Inputs = 
            x.Inputs |> List.map (fun a -> a.semantic, AttributeType.getExpectedType a.attributeType)

        member x.Outputs = 
            x.Outputs |> List.map (fun a -> a.semantic, AttributeType.getExpectedType a.attributeType)

        member x.Uniforms =
            let bu = x.UniformBlocks |> List.collect (fun b -> b.fields |> List.map (fun f -> ConversionTarget.ConvertForBuffer, f))
            let uu = x.Uniforms |> List.map (fun f -> ConversionTarget.ConvertForLocation, f)
            bu @ uu |> List.map (fun (target, u) -> 
                match u.uniformType with
                    | SamplerType ->
                        u.semantic, typeof<ITexture>
                    | _ ->
                        let t = UniformConverter.getExpectedType target u.uniformType
                        u.semantic, t
            )

    member x.InterfaceBlock =
        let uniformBlocks = 
            x.UniformBlocks |> List.map (fun b -> 
                let fields = b.fields |> List.map (fun f -> f.Interface)
                let fields = fields |> String.concat "\r\n" |> String.indent 1
                sprintf "uniform %s {\r\n%s\r\n};" b.name fields)
            |> String.concat "\r\n"

        let uniforms =
            x.Uniforms |> List.map (fun u -> sprintf "uniform %s" u.Interface) |> String.concat "\r\n"

        let inputs =
            x.Inputs |> List.map (fun i ->
                sprintf "%A %s;" i.attributeType i.name
            ) |> String.concat "\r\n" |> String.indent 1 |> sprintf "input\r\n{\r\n%s\r\n};"

        let outputs =
            x.Outputs |> List.map (fun i ->
                sprintf "%A %s;" i.attributeType i.name
            ) |> String.concat "\r\n" |> String.indent 1 |> sprintf "output\r\n{\r\n%s\r\n};"


        let body = sprintf "%s\r\n%s\r\n\r\n%s\r\n\r\n%s" uniforms uniformBlocks inputs outputs

        sprintf "interface {\r\n%s\r\n}" (String.indent 1 body)


module ProgramReflector =


    let getActiveUniformBlockName (p : int) (i : int) =
        let builder = System.Text.StringBuilder(1024)
        let mutable l = 0
        GL.GetActiveUniformBlockName(p, i, 1024, &l, builder)
        GL.Check "could not get uniform block name"
        builder.ToString()

    let getActiveUniformBlockReferences (p : int) (i : int) =
        let vs = GL.GetActiveUniformBlock(p, i, ActiveUniformBlockParameter.UniformBlockReferencedByVertexShader)
        let tcs = GL.GetActiveUniformBlock(p, i, ActiveUniformBlockParameter.UniformBlockReferencedByTessControlShader)
        let tev = GL.GetActiveUniformBlock(p, i, ActiveUniformBlockParameter.UniformBlockReferencedByTessEvaluationShader)
        let gs = GL.GetActiveUniformBlock(p, i, ActiveUniformBlockParameter.UniformBlockReferencedByGeometryShader)
        let fs = GL.GetActiveUniformBlock(p, i, ActiveUniformBlockParameter.UniformBlockReferencedByFragmentShader)

        Set.ofList [
            if vs = 1 then yield ShaderStage.Vertex
            if tcs = 1 then yield ShaderStage.TessControl
            if tev = 1 then yield ShaderStage.TessEval
            if gs = 1 then yield ShaderStage.Geometry
            if fs = 1 then yield ShaderStage.Pixel
        ]

    let samplerHackRegex = System.Text.RegularExpressions.Regex("_samplerState[0-9]+$")

    let getActiveUniform (p : int) (index : int) =
        let mutable length = 0
        let mutable size = 0
        let mutable uniformType = ActiveUniformType.Float
        let builder = System.Text.StringBuilder(1024)
        GL.GetActiveUniform(p, index, 1024, &length, &size, &uniformType, builder)
        let name = builder.ToString()

        GL.Check "could not get active uniform"

        let location = GL.GetUniformLocation(p, name)

        let semantic = samplerHackRegex.Replace(name,"")
        if semantic <> name 
        then Log.warn "replaced uniform semantic value (%s -> %s), this might be an error or due to usage of lins." name semantic

        { index = index; location = location; name = name; semantic = semantic; samplerState = None; size = -1; uniformType = uniformType; offset = -1; isRowMajor = false }

    let getActiveUniformBlocks (p : int) =
        [
            let blockCount = GL.GetProgram(p, GetProgramParameterName.ActiveUniformBlocks)
            GL.Check "could not get active uniforms"

            for b in 0..blockCount-1 do
                let name = getActiveUniformBlockName p b

                let size = GL.GetActiveUniformBlock(p, b, ActiveUniformBlockParameter.UniformBlockDataSize)
                GL.Check "coult not get uniform block size"

                let fieldCount = GL.GetActiveUniformBlock(p, b, ActiveUniformBlockParameter.UniformBlockActiveUniforms)
                GL.Check "coult not get uniform block field-count"

                let uniformIndices = Array.create fieldCount -1
                GL.GetActiveUniformBlock(p, b, ActiveUniformBlockParameter.UniformBlockActiveUniformIndices, uniformIndices)
                GL.Check "coult not get uniform block field-indices"

                let binding = GL.GetActiveUniformBlock(p, b, ActiveUniformBlockParameter.UniformBlockBinding)
                GL.Check "coult not get uniform block binding"

                let referencedBy = getActiveUniformBlockReferences p b


                let fields = uniformIndices |> Array.map (getActiveUniform p)

                let offsets = Array.create fieldCount -1
                GL.GetActiveUniforms(p, fieldCount, uniformIndices, ActiveUniformParameter.UniformOffset, offsets);
                GL.Check "could not get field offsets for uniform block"

                let sizes = Array.create fieldCount -1
                GL.GetActiveUniforms(p, fieldCount, uniformIndices, ActiveUniformParameter.UniformSize, sizes);
                GL.Check "could not get field offsets for uniform block"

                let rowMajor = Array.create fieldCount 0
                GL.GetActiveUniforms(p, fieldCount, uniformIndices, ActiveUniformParameter.UniformIsRowMajor, rowMajor);
                GL.Check "could not get field majorities for uniform block"

                for i in 0..fields.Length-1 do
                    fields.[i] <- { fields.[i] with offset = offsets.[i]; size = sizes.[i]; isRowMajor = (rowMajor.[i] = 1) }

                GL.UniformBlockBinding(p, b, b)
                GL.Check "could not set uniform buffer binding"

                yield { name = name; index = b; binding = binding; fields = fields |> Array.toList; size = size; referencedBy = referencedBy }

        ]

    let getActiveUniforms (p : int) =
        let count = GL.GetProgram(p, GetProgramParameterName.ActiveUniforms)
        GL.Check "could not get active uniform count"
        let binding = ref 0

        [ for i in 0..count-1 do
            let u = getActiveUniform p i
            
            if u.location >= 0 then
                match u.uniformType with
                    | SamplerType  ->
                        GL.Uniform1(u.location, !binding)
                        GL.Check "could not set texture-location"
                        yield { u with index = !binding }
                        binding := !binding + 1
                    | _ ->
                        yield u
        ]
    
    let getActiveInputs (p : int) =
        let active = GL.GetProgram(p, GetProgramParameterName.ActiveAttributes)
        GL.Check "could not get active attribute count"

        [ for a in 0..active-1 do

            let mutable length = 0
            let mutable t = ActiveAttribType.None
            let mutable size = 1
            let builder = System.Text.StringBuilder(1024)
            GL.GetActiveAttrib(p, a, 1024, &length, &size, &t, builder)
            let name = builder.ToString()
            
            let location = GL.GetAttribLocation(p, name)

            yield { attributeIndex = location; size = size; name = name; semantic = name; attributeType = t }
        ]

    let getActiveOutputs (p : int) =
        try
            let outputCount = GL.GetProgramInterface(p, ProgramInterface.ProgramOutput, ProgramInterfaceParameter.ActiveResources)
            GL.Check "could not get active-output count"

            let r = [ for i in 0..outputCount-1 do
                            let mutable l = 0
                            let builder = System.Text.StringBuilder(1024)
                            GL.GetProgramResourceName(p, ProgramInterface.ProgramOutput, i, 1024, &l, builder)
                            GL.Check "could not get program resource name"
                            let name = builder.ToString()

                            let mutable prop = ProgramProperty.Type
                            let _,p = GL.GetProgramResource(p, ProgramInterface.ProgramOutput, i, 1, &prop, 1)
                            GL.Check "could not get program resource"
                            let outputType = p |> unbox<ActiveAttribType>

//                            let mutable prop = ProgramProperty.ArraySize
//                            let _,size = GL.GetProgramResource(p, ProgramInterface.ProgramOutput, i, 1, &prop, 1)
//                            GL.Check "could not get program resource"

                            let location = GL.GetFragDataLocation(p, name)
                            GL.GetError() |> ignore
                            //GL.Check "could not get frag data location"

                            yield { attributeIndex = i; size = 1; name = name; semantic = name; attributeType = outputType }

            ]
            GL.Check "could not get active outputs"
            r
        with e ->
            []

[<AutoOpen>]
module ProgramExtensions =
    //type UniformField = Aardvark.Rendering.GL.UniformField

    let private parsePath (path : string) =
        // TODO: really parse path here
        Aardvark.Rendering.GL.ValuePath path

    let private activeUniformToField (u : ActiveUniform) =
        { UniformField.semantic = u.semantic; UniformField.path = parsePath u.name; UniformField.offset = u.offset; UniformField.uniformType = u.uniformType; UniformField.count = u.size }

    type ActiveUniform with
        member x.UniformField = activeUniformToField x

    let private getShaderType (stage : ShaderStage) =
        match stage with
            | ShaderStage.Vertex -> ShaderType.VertexShader
            | ShaderStage.TessControl -> ShaderType.TessControlShader
            | ShaderStage.TessEval -> ShaderType.TessEvaluationShader
            | ShaderStage.Geometry -> ShaderType.GeometryShader
            | ShaderStage.Pixel -> ShaderType.FragmentShader
            | _ -> failwithf "unknown shader-stage: %A" stage

    let private versionRx = System.Text.RegularExpressions.Regex @"#version[ \t]+(?<version>.*)"
    let private addPreprocessorDefine (define : string) (code : string) =
        let replaced = ref false
        let def = sprintf "#define %s\r\n" define
        let layout = "layout(row_major) uniform;\r\n"

        let newCode = 
            versionRx.Replace(code, System.Text.RegularExpressions.MatchEvaluator(fun m ->
                let v = m.Groups.["version"].Value
                replaced := true
                match Int32.TryParse v with
                    | (true, vers) when vers > 120 ->
                        sprintf "#version %s\r\n%s%s" v def layout
                    | _ ->
                        sprintf "#version %s\r\n%s" v def
            ))

        if !replaced then newCode
        else def + newCode


    let private outputSuffixes = [""; "Out"; "Frag"; "Pixel"; "Fragment"]

    type Aardvark.Rendering.GL.Context with

        member x.CreateUniformBuffer(block : UniformBlock) =
            let fields = block.fields |> List.map activeUniformToField
            x.CreateUniformBuffer(block.size, fields)

        member x.TryCompileShader(stage : ShaderStage, code : string, entryPoint : string) =
            using x.ResourceLock (fun _ ->
                let code = code.Replace(sprintf "%s(" entryPoint, "main(")
                
                let handle = GL.CreateShader(getShaderType stage)
                GL.Check "could not create shader"

                GL.ShaderSource(handle, code)
                GL.Check "could not attach shader source"

                GL.CompileShader(handle)
                GL.Check "could not compile shader"

                let status = GL.GetShader(handle, ShaderParameter.CompileStatus)
                GL.Check "could not get shader status"

                let log = GL.GetShaderInfoLog handle

                let topologies =
                    match stage with
                        | ShaderStage.Geometry ->
                            
                            let inRx = System.Text.RegularExpressions.Regex @"layout\((?<top>[a-zA-Z_]+)\)[ \t]*in[ \t]*;"
                            let m = inRx.Match code
                            if m.Success then
                                match m.Groups.["top"].Value with
                                    | "points" -> 
                                        [IndexedGeometryMode.PointList] |> Set.ofList |> Some
                                    | "lines" | "lines_adjacency" ->
                                        [IndexedGeometryMode.LineList; IndexedGeometryMode.LineStrip] |> Set.ofList |> Some
                                    | "triangles" | "triangles_adjacency" ->
                                        [IndexedGeometryMode.TriangleStrip; IndexedGeometryMode.TriangleStrip] |> Set.ofList |> Some
                                    | v ->
                                       failwithf "unknown geometry shader input topology: %A" v 
                            else
                                failwith "could not determine geometry shader input topology"

                        | _ ->
                            None

                if status = 1 then
                    Success(Shader(x, handle, stage, topologies))
                else
                    let log =
                        if String.IsNullOrEmpty log then "ERROR: shader did not compile but log was empty"
                        else log

                    Error log

            )

        member x.TryCompileProgram(fboSignature : IFramebufferSignature, code : string) =
            let vs = code.Contains "void VS("
            let tcs = code.Contains "void TCS("
            let tev = code.Contains "void TEV"
            let gs = code.Contains "void GS("
            let fs = code.Contains "void PS("

            let stages =
                [
                    if vs then yield "Vertex", "VS", ShaderStage.Vertex
                    if tcs then yield "TessControl", "TCS", ShaderStage.TessControl
                    if tev then yield "TessEval", "TEV", ShaderStage.TessEval
                    if gs then yield "Geometry", "GS", ShaderStage.Geometry
                    if fs then yield "Pixel", "PS", ShaderStage.Pixel
                ]

            let codeWithDefine = addPreprocessorDefine "__SHADER_STAGE__" code
            printfn "CODE: %s" codeWithDefine

            using x.ResourceLock (fun _ ->
                let results =
                    stages |> List.map (fun (def, entry, stage) ->
                        let codeWithDefine = addPreprocessorDefine def code
                        stage, x.TryCompileShader(stage, codeWithDefine, entry)
                    )

                let errors = results |> List.choose (fun (stage,r) -> match r with | Error e -> Some(stage,e) | _ -> None)
                if List.isEmpty errors then
                    let shaders = results |> List.choose (function (_,Success r) -> Some r | _ -> None)

                    addProgram x
                    let handle = GL.CreateProgram()
                    GL.Check "could not create program"

                    for s in shaders do
                        GL.AttachShader(handle, s.Handle)
                        GL.Check "could not attach shader to program"

                    GL.LinkProgram(handle)
                    GL.Check "could not link program"

                    let status = GL.GetProgram(handle, GetProgramParameterName.LinkStatus)
                    let log = GL.GetProgramInfoLog(handle)
                    GL.Check "could not get program log"


                    if status = 1 then
                        let outputs =
                            fboSignature.ColorAttachments 
                                |> Map.toList
                                |> List.map (fun (location, (semantic, signature)) ->
                                    let name = semantic.ToString()
                                    let suffixes = ["{0}"; "{0}Out"; "{0}Frag"; "Pixel{0}"; "{0}Pixel"; "{0}Fragment"]
                            
                                    let outputNameAndIndex = 
                                        suffixes |> List.tryPick (fun s ->
                                            let outputName = String.Format(s, name)
                                            let index = GL.GetFragDataIndex(handle, outputName)
                                            GL.Check "could not get FragDataIndex"
                                            if index >= 0 then Some (outputName, index)
                                            else None
                                        )

                            

                                    match outputNameAndIndex with
                                        | Some (outputName, index) ->
                                            GL.BindFragDataLocation(handle, location, semantic.ToString())
                                            GL.Check "could not bind FragData location"

                                            { attributeIndex = location; size = 1; name = outputName; semantic = semantic.ToString(); attributeType = ActiveAttribType.FloatVec4 }
                                        | None ->
                                            failwithf "could not get desired program-output: %A" semantic
                                )

                        // after modifying the frag-locations the program needs to be linked again
                        GL.LinkProgram(handle)
                        GL.Check "could not link program"

                        let status = GL.GetProgram(handle, GetProgramParameterName.LinkStatus)
                        let log = GL.GetProgramInfoLog(handle)
                        GL.Check "could not get program log"

                        if status = 1 then

                            GL.UseProgram(handle)
                            GL.Check "could not bind program"

                            let supported = 
                                shaders |> List.tryPick (fun s -> s.SupportedModes)


                            let result = {
                                Context = x
                                Code = code
                                Handle = handle
                                Shaders = shaders
                                UniformBlocks = ProgramReflector.getActiveUniformBlocks handle
                                Uniforms = ProgramReflector.getActiveUniforms handle
                                UniformGetters = SymDict.empty
                                SamplerStates = SymDict.empty
                                Inputs = ProgramReflector.getActiveInputs handle
                                Outputs = outputs
                                SupportedModes = supported
                            }

                            GL.UseProgram(0)
                            GL.Check "could not unbind program"

                            Success result
                        else
                            let log =
                                if String.IsNullOrEmpty log then "ERROR: program could not be linked but log was empty"
                                else log

                            Error log

                    else
                        let log =
                            if String.IsNullOrEmpty log then "ERROR: program could not be linked but log was empty"
                            else log

                        Error log
                else
                    let err = errors |> List.map (fun (stage, e) -> sprintf "%A:\r\n%s" stage (String.indent 1 e)) |> String.concat "\r\n\r\n" 
                    Error err
            )

        member x.CompileProgram(fboSignature : IFramebufferSignature, code : string) =
            match x.TryCompileProgram(fboSignature, code) with
                | Success p -> p
                | Error e ->
                    failwithf "Shader compiler returned errors: %s" e

        member x.Delete(p : Program) =
            using x.ResourceLock (fun _ ->
                removeProgram x
                GL.DeleteProgram(p.Handle)
                GL.Check "could not delete program"
            )



