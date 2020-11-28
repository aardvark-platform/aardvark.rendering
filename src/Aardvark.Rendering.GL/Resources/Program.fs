namespace Aardvark.Rendering.GL

//#nowarn "44" //Obsolete warning

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices

[<AutoOpen>]
module private ShaderProgramCounters =
    let addProgram (ctx : Context) =
        Interlocked.Increment(&ctx.MemoryUsage.ShaderProgramCount) |> ignore

    let removeProgram (ctx : Context) =
        Interlocked.Decrement(&ctx.MemoryUsage.ShaderProgramCount) |> ignore


type ActiveUniform = { slot : int; index : int; location : int; name : string; semantic : string; samplerState : Option<SamplerStateDescription>; size : int; uniformType : ActiveUniformType; offset : int; arrayStride : int; isRowMajor : bool } with
    member x.Interface =

        let name =
            if x.name = x.semantic then x.name
            else sprintf "%s : %s" x.name x.semantic

        match x.samplerState with
            | Some sam ->
                sprintf "%A %s; // sampler: %A" x.uniformType name sam
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
       HasTessellation : bool
       SupportedModes : Option<Set<IndexedGeometryMode>>
       Interface : FShade.GLSL.GLSLProgramInterface

       [<DefaultValue>]
       mutable _inputs : Option<list<string * Type>>
       [<DefaultValue>]
       mutable _outputs : Option<list<string * Type>>
       [<DefaultValue>]
       mutable _uniforms : Option<list<string * Type>>
    } with

    member x.WritesPointSize =
        FShade.GLSL.GLSLProgramInterface.usesPointSize x.Interface

    interface IBackendSurface with
        member x.Handle = x.Handle :> obj

[<AutoOpen>]
module ProgramExtensions =
    //type UniformField = Aardvark.Rendering.GL.UniformField
    open System.Text.RegularExpressions
    open System

    module private FShadeBackend = 
        open FShade.GLSL

        let private backendCache = System.Collections.Concurrent.ConcurrentDictionary<Context, Backend>()

        let get (ctx : Context) =   
            backendCache.GetOrAdd(ctx, fun ctx ->
                let mutable enabledGLSLExts = Set.empty

                let bindingMode = 
                    if ctx.Driver.glsl >= Version(4,3) then BindingMode.PerKind
                    else BindingMode.None

                let conservativeDepth =
                    if ctx.Driver.glsl >= Version(4,2) then true
                    elif Set.contains "GL_ARB_conservative_depth" ctx.Driver.extensions then 
                        enabledGLSLExts <- Set.add "GL_ARB_conservative_depth" enabledGLSLExts
                        true
                    else
                        false
                    
                let uniformBuffers =
                    if ctx.Driver.version >= Version(3,1) then true
                    elif Set.contains "GL_ARB_uniform_buffer_object" ctx.Driver.extensions then 
                        enabledGLSLExts <- Set.add "GL_ARB_uniform_buffer_object" enabledGLSLExts
                        true
                    else
                        false

                let locations =
                    ctx.Driver.glsl >= Version(3,3) 
                    
                let inout =
                    ctx.Driver.glsl >= Version(1,3) 

                let cfg = 
                    { 
                        version = GLSLVersion(ctx.Driver.glsl.Major, ctx.Driver.glsl.Minor, 0)
                        enabledExtensions = enabledGLSLExts
                        createUniformBuffers = uniformBuffers
                        bindingMode = bindingMode
                        createDescriptorSets = false
                        stepDescriptorSets = false
                        createInputLocations = locations
                        createPerStageUniforms = false
                        reverseMatrixLogic = true
                        createOutputLocations = locations
                        createPassingLocations = locations
                        depthWriteMode = conservativeDepth
                        useInOut = inout
                    }
                Backend.Create cfg
            )

    type Context with
        member x.FShadeBackend = FShadeBackend.get x



    let private getShaderType (stage : ShaderStage) =
        match stage with
            | ShaderStage.Vertex -> ShaderType.VertexShader
            | ShaderStage.TessControl -> ShaderType.TessControlShader
            | ShaderStage.TessEval -> ShaderType.TessEvaluationShader
            | ShaderStage.Geometry -> ShaderType.GeometryShader
            | ShaderStage.Fragment -> ShaderType.FragmentShader
            | ShaderStage.Compute -> ShaderType.ComputeShader
            | _ -> failwithf "unknown shader-stage: %A" stage

    let private versionRx = System.Text.RegularExpressions.Regex @"#version[ \t]+(?<version>.*)"
    let private addPreprocessorDefine (define : string) (code : string) =
        let replaced = ref false
        let def = sprintf "#define %s\r\n" define
        let layout = "" //"layout(row_major) uniform;\r\n"

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


    let private outputSuffixes = ["{0}"; "{0}Out"; "{0}Frag"; "Pixel{0}"; "{0}Pixel"; "{0}Fragment"]
    let private geometryOutputSuffixes = ["{0}"; "{0}Out"; "{0}Geometry"; "{0}TessControl"; "{0}TessEval"; "Geometry{0}"; "TessControl{0}"; "TessEval{0}"]


    module ShaderCompiler = 
        let tryCompileShader (stage : ShaderStage) (code : string) (entryPoint : string) (x : Context) =
            Operators.using x.ResourceLock (fun _ ->
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

                                    | "lines" ->
                                        [IndexedGeometryMode.LineList; IndexedGeometryMode.LineStrip] |> Set.ofList |> Some

                                    | "lines_adjacency" ->
                                        [IndexedGeometryMode.LineAdjacencyList; IndexedGeometryMode.LineStrip] |> Set.ofList |> Some

                                    | "triangles"  ->
                                        [IndexedGeometryMode.TriangleList; IndexedGeometryMode.TriangleStrip] |> Set.ofList |> Some
                                    
                                    | "triangles_adjacency" ->
                                        [IndexedGeometryMode.TriangleAdjacencyList] |> Set.ofList |> Some
                                    
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

        let tryCompileCompute (code : string) (x : Context) =
            use t = x.ResourceLock
            match tryCompileShader ShaderStage.Compute code "main" x with
                | Success shader ->
                    Success [shader]
                | Error err ->
                    Error err

        let withLineNumbers (code : string) : string =
            let lineCount = String.lineCount code
            let lineColumns = 1 + int (Fun.Log10 lineCount)
            let lineFormatLen = lineColumns + 3
            let sb = new System.Text.StringBuilder(code.Length + lineFormatLen * lineCount + 10)
            
            let fmtStr = "{0:" + lineColumns.ToString() + "} : "
            let mutable lineEnd = code.IndexOf('\n')
            let mutable lineStart = 0
            let mutable lineCnt = 1
            while (lineEnd >= 0) do
                let line = code.Substring(lineStart, lineEnd - lineStart + 1)
                sb.Append(lineCnt.ToString().PadLeft(lineColumns)) |> ignore
                sb.Append(": ")  |> ignore
                sb.Append(line) |> ignore
                lineStart <- lineEnd + 1
                lineCnt <- lineCnt + 1
                lineEnd <- code.IndexOf('\n', lineStart)
                ()

            let lastLine = code.Substring(lineStart)
            if lastLine.Length > 0 then
                sb.Append(lineCnt.ToString()) |> ignore
                sb.Append(": ")  |> ignore
                sb.Append(lastLine) |> ignore

            sb.ToString()

        let tryCompileShaders (withFragment : bool) (code : string) (x : Context) =
            let vs = code.Contains "#ifdef Vertex"
            let tcs = code.Contains "#ifdef TessControl"
            let tev = code.Contains "#ifdef TessEval"
            let gs = code.Contains "#ifdef Geometry"
            let fs = withFragment && code.Contains "#ifdef Fragment"

            let stages =
                [
                    if vs then yield "Vertex", "main", ShaderStage.Vertex
                    if tcs then yield "TessControl", "main", ShaderStage.TessControl
                    if tev then yield "TessEval", "main", ShaderStage.TessEval
                    if gs then yield "Geometry", "main", ShaderStage.Geometry
                    if fs then yield "Fragment", "main", ShaderStage.Fragment
                ]

            let code = if (code.Contains("layout(location = 0) out vec4 Colors2Out")) then 
                            code.Replace("out vec4 Colors2Out", "out vec4 Colors3Out")
                                .Replace("out vec4 ColorsOut", "out vec4 Colors2Out")
                                .Replace("out vec4 Colors3Out", "out vec4 ColorsOut")
                       else code

            if RuntimeConfig.PrintShaderCode then
                let codeWithDefine = addPreprocessorDefine "__SHADER_STAGE__" code
                let numberdLines = withLineNumbers codeWithDefine
                Report.Line("Compiling shader:\n{0}", numberdLines)

            Operators.using x.ResourceLock (fun _ ->
                let results =
                    stages |> List.map (fun (def, entry, stage) ->
                        let codeWithDefine = addPreprocessorDefine def code
                        stage, tryCompileShader stage codeWithDefine entry x
                    )

                let errors = results |> List.choose (fun (stage,r) -> match r with | Error e -> Some(stage,e) | _ -> None)
                if List.isEmpty errors then
                    let shaders = results |> List.choose (function (_,Success r) -> Some r | _ -> None)
                    Success shaders
                else
                    let codeWithDefine = addPreprocessorDefine "__SHADER_STAGE__" code
                    let numberdLines = withLineNumbers codeWithDefine
                    Report.Line("Failed to compile shader:\n{0}", numberdLines)
                    let err = errors |> List.map (fun (stage, e) -> sprintf "%A:\r\n%s" stage (String.indent 1 e)) |> String.concat "\r\n\r\n" 
                    Error err
            
            )

        let setFragDataLocations (fboSignature : Map<string, int>) (handle : int) (x : Context) =
            Operators.using x.ResourceLock (fun _ ->
                fboSignature 
                    |> Map.toList
                    |> List.map (fun (name, location) ->
                        let outputNameAndIndex = 
                            outputSuffixes |> List.tryPick (fun s ->
                                let outputName = String.Format(s, name)
                                let index = GL.GetFragDataIndex(handle, outputName)
                                GL.Check "could not get FragDataIndex"
                                if index >= 0 then Some (outputName, index)
                                else None
                            )

                            

                        match outputNameAndIndex with
                            | Some (outputName, index) ->
                                GL.BindFragDataLocation(handle, location, name)
                                GL.Check "could not bind FragData location"

                                { attributeIndex = location; size = 1; name = outputName; semantic = name; attributeType = ActiveAttribType.FloatVec4 }
                            | None ->
                                failwithf "could not get desired program-output: %A" name
                    )
            )


        let tryLinkProgram (expectsRowMajorMatrices : bool) (handle : int) (code : string) (shaders : list<Shader>) (firstTexture : int) (findOutputs : int -> Context -> list<ActiveAttribute>) (x : Context) =
            GL.LinkProgram(handle)
            GL.Check "could not link program"

         
            let status = GL.GetProgram(handle, GetProgramParameterName.LinkStatus)
            let log = GL.GetProgramInfoLog(handle)
            GL.Check "could not get program log"


            if status = 1 then
                let outputs = findOutputs handle x

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

                    try
                        try
                            Success {
                                Context = x
                                Code = code
                                Handle = handle
                                HasTessellation = shaders |> List.exists (fun s -> s.Stage = ShaderStage.TessControl || s.Stage = ShaderStage.TessEval)
                                SupportedModes = supported
                                Interface =
                                    {
                                        inputs          = []
                                        outputs         = []
                                        samplers        = MapExt.empty
                                        images          = MapExt.empty
                                        storageBuffers  = MapExt.empty
                                        uniformBuffers  = MapExt.empty
                                        shaders         = MapExt.empty
                                    }
                            }

                        finally 
                            GL.UseProgram(0)
                            GL.Check "could not unbind program"

                        
                    with 
                    | e -> 
                             let codeWithDefine = addPreprocessorDefine "__SHADER_STAGE__" code
                             let numberdLines = withLineNumbers codeWithDefine
                             Report.Line("Failed to build shader interface of:\n{0}", numberdLines)
                             reraise()
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

    open FShade.Imperative
    open FShade
  
    // NOTE: shader caches no longer depending on Context. shaders objects can be shared between contexts, even if a context is using a different GL profile
    let private codeCache = ConcurrentDictionary<string * IFramebufferSignature, Error<Program>>()
    
    let private staticShaderCache = ConcurrentDictionary<FShade.Effect * IFramebufferSignature, Error<GLSL.GLSLProgramInterface * aval<Program>>>()
    let private dynamicShaderCache = ConditionalWeakTable<(FShade.EffectConfig -> FShade.EffectInputLayout * aval<FShade.Imperative.Module>), Error<GLSL.GLSLProgramInterface * aval<Program>>>()
    let private shaderPickler = MBrace.FsPickler.FsPickler.CreateBinarySerializer()

//    let private useDiskCache = true
//    let private cachePath =
//        if useDiskCache then
//            let temp = Path.combine [System.IO.Path.GetTempPath(); "aardvark-gl-shadercache"]
//            if not (System.IO.Directory.Exists temp) then System.IO.Directory.CreateDirectory temp |> ignore
//            temp
//        else
//            ""

    type private OutputSignature =
        {
            device      : string
            id          : string
            outputs     : Map<string, int * RenderbufferFormat>
            layered     : Set<string>
            layerCount  : int
        }

    type private ShaderCacheEntry =
        {
            iface       : FShade.GLSL.GLSLProgramInterface
            format      : BinaryFormat
            binary      : byte[]
            code        : string
            hasTess     : bool
            modes       : Option<Set<IndexedGeometryMode>>
        }

        override x.ToString() = x.code // NOTE: x.binary or the complexity if x.iface seems to crash the VS 2019 debugger with the default ToString() implementation

    type Aardvark.Rendering.GL.Context with


        member x.TryCompileShader(stage : Aardvark.Base.ShaderStage, code : string, entryPoint : string) =
            x |> ShaderCompiler.tryCompileShader stage code entryPoint


        member x.TryCompileCompute(expectsRowMajorMatrices : bool, code : string) =
            use t = x.ResourceLock

            match x |> ShaderCompiler.tryCompileCompute code with
                | Success shaders ->
                    addProgram x
                    let handle = GL.CreateProgram()
                    GL.Check "could not create program"

                    for s in shaders do
                        GL.AttachShader(handle, s.Handle)
                        GL.Check "could not attach shader to program"

                    match x |> ShaderCompiler.tryLinkProgram expectsRowMajorMatrices handle code shaders 0 (fun _ _ -> []) with
                        | Success program ->
                            Success program
                        | Error err ->
                            Error err

                | Error err ->
                    Error err
                    
        member x.TryGetProgramBinary(prog : Program) =
            use __ = x.ResourceLock
            GL.GetError() |> ignore

            let mutable length = 0
            GL.GetProgram(prog.Handle, unbox 0x8741, &length)
            let err = GL.GetError()
            if err <> ErrorCode.NoError then 
                None
            else
                let data : byte[] = Array.zeroCreate length
                let mutable format = Unchecked.defaultof<BinaryFormat>
                GL.GetProgramBinary(prog.Handle, length, &length, &format, data)
                let err = GL.GetError()
                if err <> ErrorCode.NoError then 
                    None
                else
                    Some (format, data)



        member x.TryCompileProgramCode(fboSignature : Map<string, int>, expectsRowMajorMatrices : bool, code : string) =
            Operators.using x.ResourceLock (fun _ ->
                match x |> ShaderCompiler.tryCompileShaders true code with
                    | Success shaders ->
                        let firstTexture = 0
                        addProgram x
                        let handle = GL.CreateProgram()
                        GL.Check "could not create program"

                        for s in shaders do
                            GL.AttachShader(handle, s.Handle)
                            GL.Check "could not attach shader to program"

                        match x |> ShaderCompiler.tryLinkProgram expectsRowMajorMatrices handle code shaders firstTexture (ShaderCompiler.setFragDataLocations fboSignature) with
                            | Success program ->
                                Success program
                            | Error err ->
                                let numberdLines = ShaderCompiler.withLineNumbers code
                                Report.Line("Failed to link shader:\n{0}", numberdLines)
                                Error err

                    
                    | Error err ->
                        Error err
            )

        member x.CompileProgramCode(fboSignature : Map<string, int>, expectsRowMajorMatrices : bool, code : string) =
            match x.TryCompileProgramCode(fboSignature, expectsRowMajorMatrices, code) with
                | Success p -> p
                | Error e ->
                    failwithf "[GL] shader compiler returned errors: %s" e

        member x.Delete(p : Program) =
            Operators.using x.ResourceLock (fun _ ->
                removeProgram x
                GL.DeleteProgram(p.Handle)
                GL.Check "could not delete program"
            )

        member x.TryCompileProgram(id : string, signature : IFramebufferSignature, code : Lazy<GLSL.GLSLShader>) : Error<_> =
            codeCache.GetOrAdd((id, signature), fun (id, signature) ->
                

                let fixBindings (p : Program) (iface : FShade.GLSL.GLSLProgramInterface) = 
                    if p.Context.FShadeBackend.Config.bindingMode = FShade.GLSL.BindingMode.None then
                        let uniformBuffers =
                            let mutable b = 0
                            iface.uniformBuffers |> MapExt.map (fun name ub ->
                                let bi = GL.GetUniformBlockIndex(p.Handle, name)
                                let binding = b
                                b <- b + 1
                                GL.UniformBlockBinding(p.Handle, bi, binding)
                                { ub with ubBinding = binding }
                            )

                        let samplers =
                            let mutable b = 0
                            iface.samplers |> MapExt.map (fun name sam ->
                                let l = GL.GetUniformLocation(p.Handle, name)
                                let binding = b
                                b <- b + 1
                                GL.ProgramUniform1(p.Handle, l, binding)
                                { sam with samplerBinding = binding }
                            )
                            
                        let images =
                            let mutable b = 0
                            iface.images |> MapExt.map (fun name img ->
                                let l = GL.GetUniformLocation(p.Handle, name)
                                let binding = b
                                b <- b + 1
                                GL.ProgramUniform1(p.Handle, l, binding)
                                { img with imageBinding = binding }
                            )

                        let storageBuffers =
                            let mutable b = 0
                            iface.storageBuffers |> MapExt.map (fun name sb ->
                                let bi = GL.GetProgramResourceIndex(p.Handle, ProgramInterface.ShaderStorageBlock, name)
                                let binding = b
                                b <- b + 1
                                GL.ShaderStorageBlockBinding(p.Handle, bi, binding)
                                { sb with ssbBinding = binding }
                            )



                        { iface with 
                            uniformBuffers = uniformBuffers 
                            samplers = samplers
                            storageBuffers = storageBuffers
                            images = images
                        }
                    else
                        iface

                let (file : string, content : Option<ShaderCacheEntry>) =
                    match x.ShaderCachePath with    
                        | Some cachePath ->

                            // NOTE: contex.Diver represent information obtained by primary context -> possible that resource context have been created differently -> use driver information from actual context
                            let driver = match x.CurrentContextHandle with
                                            | Some ch -> ch.Driver
                                            | _ -> Log.warn "context not current!!"
                                                   x.Driver
                            
                            let key = 
                                {   // NOTE: Profile mask can be None, Core or Compatibility, shaders are not necessary compatible between those
                                    device      = driver.vendor + "_" + driver.renderer + "_" + driver.versionString + "/" + driver.profileMask.ToString() 
                                    id          = id
                                    outputs     = signature.ColorAttachments |> Map.toList |> List.map (fun (id,(name, s)) -> string name, (id, s.format)) |> Map.ofList
                                    layered     = signature.PerLayerUniforms
                                    layerCount  = signature.LayerCount
                                }

                            let hash = shaderPickler.ComputeHash(key).Hash |> System.Guid
                            let file = System.IO.Path.Combine(cachePath, string hash + ".bin")

                            if System.IO.File.Exists file then
                                try file, shaderPickler.UnPickle (File.readAllBytes file) |> Some
                                with _ -> file, None
                            else
                                file, None
                        | _ ->
                            "", None

                match content with
                    | Some c ->
                        
                        let prog = GL.CreateProgram()
                        addProgram x
                        GL.ProgramBinary(prog, c.format, c.binary, c.binary.Length)
                        GL.Check "could not create program from binary"
                        
                        let linkStatus = GL.GetProgram(prog, GetProgramParameterName.LinkStatus)
                        if linkStatus = 0 then // GL_False
                            let info = GL.GetProgramInfoLog(prog)
                            GL.DeleteProgram(prog)
                            Log.warn "Error Loading Program Binary: Format=%A\nInfo: %s" c.format info

                            Log.warn "Fallback: compiling shader from code"
                            let code = code.Value
                            let outputs = code.iface.outputs |> List.map (fun p -> p.paramName, p.paramLocation) |> Map.ofList
                            match x.TryCompileProgramCode(outputs, true, code.code) with
                            | Success prog ->
                                let iface = fixBindings prog code.iface
                                let prog = { prog with Interface = iface }
                                Success prog
                            | Error e ->
                                Error e

                        else
                            let program = 
                                {
                                    Context = x
                                    Code = c.code
                                    Handle = prog
                                    HasTessellation = c.hasTess
                                    SupportedModes = c.modes
                                    Interface = c.iface
                                }
                                
                            let iface = fixBindings program c.iface
                            Success { program with Interface = iface }

                    | _ -> 
                        let code = code.Value
                        let outputs = code.iface.outputs |> List.map (fun p -> p.paramName, p.paramLocation) |> Map.ofList
                        match x.TryCompileProgramCode(outputs, true, code.code) with
                            | Success prog ->
                                let iface = fixBindings prog code.iface
                                let prog = { prog with Interface = iface }
                                
                                //for (name, b) in MapExt.toSeq code.iface.uniformBuffers do
                                //    b.ubBinding

                                match x.ShaderCachePath with    
                                    | Some cachePath ->
                                        match x.TryGetProgramBinary prog with
                                            | Some (format, binary) ->
                                                let entry =
                                                    shaderPickler.Pickle {
                                                        hasTess = prog.HasTessellation
                                                        iface = code.iface
                                                        format = format
                                                        binary = binary
                                                        code = code.code
                                                        modes = prog.SupportedModes
                                                    }
                                                File.writeAllBytes file entry

                                                #if PICKLERTEST
                                                let test = shaderPickler.UnPickle (File.readAllBytes file)

                                                Log.line "TEST"

                                                let file = test.iface.samplers |> MapExt.toArray |> Array.choose (fun (x, y) ->
                                                                if y.samplerTextures |> List.length > 1 then
                                                                    let sam0 = y.samplerTextures |> List.head |> snd
                                                                    let samEqual = y.samplerTextures |> List.skip 1 |> List.forall (fun s -> Object.ReferenceEquals(sam0, snd s))
                                                                    Log.line "ArraySampler Equal=%A (Deserialize)" samEqual
                                                                    Some samEqual
                                                                else
                                                                    None
                                                            )

                                                let orig = code.iface.samplers |> MapExt.toArray |> Array.choose (fun (x, y)->
                                                                if y.samplerTextures |> List.length > 1 then
                                                                    let sam0 = y.samplerTextures |> List.head |> snd
                                                                    let samEqual = y.samplerTextures |> List.skip 1 |> List.forall (fun s -> Object.ReferenceEquals(sam0, snd s))
                                                                    Log.line "ArraySampler Equal=%A (Original)" samEqual
                                                                    Some samEqual
                                                                else
                                                                    None
                                                    )

                                                if orig.Length <> file.Length then
                                                    failwith "FAILFAIL"
                                                else
                                                    let passt = Array.compareWith (fun x y -> if x = y then 0 else 1) orig file
                                                    if passt <> 0 then
                                                        Log.warn "No longer same reference equality !!"

                                                #endif

                                                ()

                                            | None ->
                                                ()
                                    | _ ->
                                        ()

                                Success prog
                            | Error e ->
                                Error e
            )

        member x.TryCreateProgram(signature : IFramebufferSignature, surface : Surface, topology : IndexedGeometryMode) : Error<GLSL.GLSLProgramInterface * aval<Program>> =
            match surface with
                | Surface.FShadeSimple effect ->
                    staticShaderCache.GetOrAdd((effect, signature), fun (effect, signature) ->
                        let glsl = 
                            lazy (
                                let module_ = signature.Link(effect, Range1d(-1.0, 1.0), false, topology)
                                ModuleCompiler.compileGLSL x.FShadeBackend module_
                            )

                        match x.TryCompileProgram(effect.Id, signature, glsl) with
                            | Success (prog) ->
                                Success (prog.Interface, AVal.constant prog)
                            | Error e ->
                                Error e
                        )

                | Surface.FShade create ->
                    lock dynamicShaderCache (fun () ->
                        
                        match dynamicShaderCache.TryGetValue(create) with
                        | (true, b) -> b
                        | _ ->
                            let (inputLayout,b) = create (signature.EffectConfig(Range1d(-1.0, 1.0), false))
                    
                            let initial = AVal.force b
                            let effect = initial.userData |> unbox<Effect>
                            let layoutHash = shaderPickler.ComputeHash(inputLayout).Hash |> Convert.ToBase64String
                            
                            let iface =
                                match x.TryCompileProgram(effect.Id + layoutHash, signature, lazy (ModuleCompiler.compileGLSL x.FShadeBackend initial)) with  
                                    | Success prog -> 
                                        let iface = prog.Interface
                                        { iface with
                                            samplers = iface.samplers |> MapExt.map (fun _ sam ->
                                                match MapExt.tryFind sam.samplerName inputLayout.eTextures with
                                                    | Some infos -> { sam with samplerTextures = infos }
                                                    | None -> sam
                                            )
                                        }
                                    | Error e ->
                                        failwithf "[GL] shader compiler returned errors: %s" e

                            let changeableProgram = 
                                b |> AVal.map (fun m ->
                                    let effect = m.userData |> unbox<Effect>
                                    match x.TryCompileProgram(effect.Id + layoutHash, signature, lazy (ModuleCompiler.compileGLSL x.FShadeBackend m)) with
                                        | Success p -> p
                                        | Error e ->
                                            Log.error "[GL] shader compiler returned errors: %A" e
                                            failwithf "[GL] shader compiler returned errors: %A" e
                                )

                            let res = Success (iface, changeableProgram)
                            dynamicShaderCache.Add(create, res)
                            res
                    )

                | Surface.None ->
                    Error "[GL] empty shader"

                | Surface.Backend surface ->
                    match surface with
                        | :? Program as p -> 
                            Success (p.Interface, AVal.constant p)
                        | _ ->
                            Error (sprintf "[GL] bad surface: %A (hi lui)" surface)

        member x.CreateProgram(signature : IFramebufferSignature, surface : Surface, topology : IndexedGeometryMode) : GLSL.GLSLProgramInterface * aval<Program> =
            match x.TryCreateProgram(signature, surface, topology) with
                | Success t -> t
                | Error e ->
                    Log.error "[GL] shader compiler returned errors: %A" e
                    failwithf "[GL] shader compiler returned errors: %A" e
                
