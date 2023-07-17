namespace Aardvark.Rendering.GL

open System
open System.Threading
open Aardvark.Base
open Aardvark.Rendering

open Aardvark.Rendering.ShaderReflection
open FSharp.Data.Adaptive
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module private ShaderProgramCounters =

    module ResourceCounts =
        let addProgram (ctx : Context) =
            Interlocked.Increment(&ctx.MemoryUsage.ShaderProgramCount) |> ignore

        let removeProgram (ctx : Context) =
            Interlocked.Decrement(&ctx.MemoryUsage.ShaderProgramCount) |> ignore

type ActiveAttribute =
    {
        attributeIndex : int
        size : int
        name : string
        semantic : string
        attributeType : ActiveAttribType
    }

type Shader =
    {
        Context : Context
        Handle : int
        Stage : ShaderStage
        SupportedModes : Option<Set<IndexedGeometryMode>>
    }

type Program =
    {
        Context : Context
        Code : string
        Handle : int
        HasTessellation : bool
        SupportedModes : Option<Set<IndexedGeometryMode>>
        Interface : FShade.GLSL.GLSLProgramInterface
    }

    member x.WritesPointSize =
        FShade.GLSL.GLSLProgramInterface.usesPointSize x.Interface

    member x.Dispose() =
        using x.Context.ResourceLock (fun _ ->
            ResourceCounts.removeProgram x.Context
            GL.DeleteProgram(x.Handle)
            GL.Check "could not delete program"
        )

    interface IBackendSurface with
        member x.Handle = x.Handle :> obj
        member x.Dispose() = x.Dispose()

[<AutoOpen>]
module ProgramExtensions =
    //type UniformField = Aardvark.Rendering.GL.UniformField
    open System.Text.RegularExpressions
    open System

    [<AutoOpen>]
    module private RuntimeExtensions =
        type IRuntime with
            member x.PrintShaderCode =
                match x.DebugConfig with
                | :? DebugConfig as cfg -> cfg.PrintShaderCode
                | _ -> false

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
                        reverseTessellationWinding = false
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
        | _ -> failf "unknown shader-stage: %A" stage

    let private nl = Environment.NewLine

    let private versionRx = System.Text.RegularExpressions.Regex @"#version[ \t]+(?<version>.*)"
    let private addPreprocessorDefine (define : string) (code : string) =
        let mutable replaced = false
        let def = $"#define {define}{nl}"

        let newCode =
            versionRx.Replace(code, System.Text.RegularExpressions.MatchEvaluator(fun m ->
                let v = m.Groups.["version"].Value
                replaced <- true
                $"#version {v}{nl}{def}"
            ))

        if replaced then newCode
        else def + newCode

    let private outputSuffixes = ["{0}"; "{0}Out"; "{0}Frag"; "Pixel{0}"; "{0}Pixel"; "{0}Fragment"]
    let private geometryOutputSuffixes = ["{0}"; "{0}Out"; "{0}Geometry"; "{0}TessControl"; "{0}TessEval"; "Geometry{0}"; "TessControl{0}"; "TessEval{0}"]


    module ShaderCompiler =
        let private tryCompileShader (stage : ShaderStage) (code : string) (entryPoint : string) (x : Context) =
            use __ = x.ResourceLock

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
                            [IndexedGeometryMode.PointList] |> Set.ofList |> Some |> Success

                        | "lines" ->
                            [IndexedGeometryMode.LineList; IndexedGeometryMode.LineStrip] |> Set.ofList |> Some |> Success

                        | "lines_adjacency" ->
                            [IndexedGeometryMode.LineAdjacencyList; IndexedGeometryMode.LineStrip] |> Set.ofList |> Some |> Success

                        | "triangles"  ->
                            [IndexedGeometryMode.TriangleList; IndexedGeometryMode.TriangleStrip] |> Set.ofList |> Some |> Success

                        | "triangles_adjacency" ->
                            [IndexedGeometryMode.TriangleAdjacencyList] |> Set.ofList |> Some |> Success

                        | v ->
                            Error $"unknown geometry shader input topology: {v}"
                    else
                        Error "could not determine geometry shader input topology"

                | _ ->
                    Success None

            match topologies with
            | Success topologies when status = 1 ->
                Success {
                    Context = x
                    Handle = handle
                    Stage = stage
                    SupportedModes = topologies
                }

            | Success _ ->
                let log =
                    if String.IsNullOrEmpty log then $"shader did not compile but error log was empty{nl}"
                    else ShaderCodeReporting.normalizeLineEndings log

                Error log

            | Error err ->
                Error (err + nl)

        let tryCompileCompute (code : string) (x : Context) =
            use __ = x.ResourceLock

            match tryCompileShader ShaderStage.Compute code "main" x with
            | Success shader ->
                Success [shader]
            | Error err ->
                Error $"failed to compile compute program{nl}{nl}{err}"

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

            Operators.using x.ResourceLock (fun _ ->
                let results =
                    stages |> List.map (fun (def, entry, stage) ->
                        let codeWithDefine = addPreprocessorDefine def code
                        stage, tryCompileShader stage codeWithDefine entry x
                    )

                let errors = results |> List.choose (fun (stage, r) -> match r with | Error e -> Some(stage, e) | _ -> None)

                if List.isEmpty errors then
                    let shaders = results |> List.choose (function (_,Success r) -> Some r | _ -> None)
                    Success shaders
                else
                    let err = errors |> List.map (fun (stage, e) -> $"{stage}:{nl}{e}") |> String.concat nl
                    Error $"failed to compile program{nl}{nl}{err}"
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
                        failf "could not get desired program-output: %A" name
                )
            )

        let tryLinkProgram (handle : int) (code : string) (shaders : list<Shader>) (findOutputs : int -> Context -> list<ActiveAttribute>) (x : Context) =
            GL.LinkProgram(handle)
            GL.Check "could not link program"

            let status = GL.GetProgram(handle, GetProgramParameterName.LinkStatus)
            let log = GL.GetProgramInfoLog(handle)
            GL.Check "could not get program log"

            if status = 1 then
                let _outputs = findOutputs handle x

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
                                        inputs                      = []
                                        outputs                     = []
                                        samplers                    = MapExt.empty
                                        images                      = MapExt.empty
                                        storageBuffers              = MapExt.empty
                                        uniformBuffers              = MapExt.empty
                                        shaders                     = FShade.GLSL.GLSLProgramShaders.Graphics { stages = MapExt.empty }
                                        accelerationStructures      = MapExt.empty
                                    }
                            }

                        finally
                            GL.UseProgram(0)
                            GL.Check "could not unbind program"

                    with
                    | e ->
                        Error $"failed to build shader interface: {e.Message}{nl}"
                else
                    let log =
                        if String.IsNullOrEmpty log then $"program could not be linked but error log was empty{nl}"
                        else ShaderCodeReporting.normalizeLineEndings log

                    Error log

            else
                let log =
                    if String.IsNullOrEmpty log then $"program could not be linked but error log was empty{nl}"
                    else ShaderCodeReporting.normalizeLineEndings log

                Error log

    module private ProgramCompiler =

        let fixBindings (program : Program) (iface : FShade.GLSL.GLSLProgramInterface) =
            if program.Context.FShadeBackend.Config.bindingMode = FShade.GLSL.BindingMode.None then
                let uniformBuffers =
                    let mutable b = 0
                    iface.uniformBuffers |> MapExt.map (fun name ub ->
                        let bi = GL.GetUniformBlockIndex(program.Handle, name)
                        let binding = b
                        b <- b + 1
                        GL.UniformBlockBinding(program.Handle, bi, binding)
                        { ub with ubBinding = binding }
                    )

                let samplers =
                    let mutable b = 0
                    iface.samplers |> MapExt.map (fun name sam ->
                        let l = GL.GetUniformLocation(program.Handle, name)
                        let binding = b
                        b <- b + 1
                        GL.ProgramUniform1(program.Handle, l, binding)
                        { sam with samplerBinding = binding }
                    )

                let images =
                    let mutable b = 0
                    iface.images |> MapExt.map (fun name img ->
                        let l = GL.GetUniformLocation(program.Handle, name)
                        let binding = b
                        b <- b + 1
                        GL.ProgramUniform1(program.Handle, l, binding)
                        { img with imageBinding = binding }
                    )

                let storageBuffers =
                    let mutable b = 0
                    iface.storageBuffers |> MapExt.map (fun name sb ->
                        let bi = GL.GetProgramResourceIndex(program.Handle, ProgramInterface.ShaderStorageBlock, name)
                        let binding = b
                        b <- b + 1
                        GL.ShaderStorageBlockBinding(program.Handle, bi, binding)
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

        let private tryLink (context : Context) (findOutputs : int -> Context -> ActiveAttribute list) (code : string) (shaders : Shader list) =
            let handle = GL.CreateProgram()
            GL.Check "could not create program"

            try
                for s in shaders do
                    GL.AttachShader(handle, s.Handle)
                    GL.Check "could not attach shader to program"

                match context |> ShaderCompiler.tryLinkProgram handle code shaders findOutputs with
                | Success program ->
                    ResourceCounts.addProgram context
                    Success program

                | Error err ->
                    Error $"failed to link program{nl}{nl}{err}"
            with
            | exn ->
                GL.DeleteProgram handle
                Error exn.Message

        let private printCode (message : string) (withDefine : bool) (code : string) =
            let code = if withDefine then addPreprocessorDefine "__SHADER_STAGE__" code else code
            ShaderCodeReporting.logLines message code

        let private tryCompileInternal (context : Context) (printCode : string -> unit) (tryCompile : unit -> Error<Program>) =
            if context.Runtime.PrintShaderCode then
                printCode "Compiling shader"

            let result = tryCompile()

            match result with
            | Error _ when not context.Runtime.PrintShaderCode ->
                printCode "Failed to compile shader"
            | _ ->
                ()

            result

        let tryCompileCode (context : Context) (framebufferSignature : Map<string, int>) (code : string) =
            let printCode message =
                code |> printCode message true

            tryCompileInternal context printCode (fun _ ->
                match context |> ShaderCompiler.tryCompileShaders true code with
                | Success shaders ->
                    let findOutputs = ShaderCompiler.setFragDataLocations framebufferSignature
                    shaders |> tryLink context findOutputs code

                | Error err ->
                    Error err
            )

        let tryCompile (context : Context) (framebufferSignature : Map<string, int>) (shader : FShade.GLSL.GLSLShader) =
            match shader.code |> tryCompileCode context framebufferSignature with
            | Success program ->
                let iface = fixBindings program shader.iface
                let program = { program with Interface = iface }
                Success program

            | Error e ->
                Error e

        let tryCompileComputeCode (context : Context) (code : string) =
            let printCode message =
                code |> printCode message false

            tryCompileInternal context printCode (fun _ ->
                match context |> ShaderCompiler.tryCompileCompute code with
                | Success shaders ->
                    shaders |> tryLink context (fun _ _ -> []) code

                | Error err ->
                    Error err
            )

        let tryCompileCompute (context : Context) (iface : FShade.GLSL.GLSLProgramInterface) (code : string)  =
            match code |> tryCompileComputeCode context with
            | Success program -> Success { program with Interface = iface }
            | res -> res

    open FShade.Imperative
    open FShade
    open FShade.GLSL

    let private pickler = MBrace.FsPickler.FsPickler.CreateBinarySerializer()

    type private OutputSignature =
        {
            device      : string
            id          : string
            outputs     : Map<string, int * TextureFormat>
            layered     : Set<string>
            layerCount  : int
        }

    type private ShaderCacheEntry =
        {
            shader  : GLSLShader
            format  : BinaryFormat
            binary  : byte[]
            hasTess : bool
            modes   : Option<Set<IndexedGeometryMode>>
        }

        override x.ToString() = x.shader.code

    module private ShaderCacheEntry =
        open System.IO

        type BinaryWriter with
            member inline x.WriteType<'T>(_value : 'T) =
                x.Write(typeof<'T>.FullName)

        type BinaryReader with
            member inline x.ReadType<'T>() =
                let expected = typeof<'T>.FullName
                let value = x.ReadString()

                if value <> expected then
                    raise <| InvalidDataException($"Expected value of type {expected} but encountered value of type {value}.")

        let serialize (dst : Stream) (entry : ShaderCacheEntry) =
            use w = new BinaryWriter(dst, System.Text.Encoding.UTF8, true)

            w.WriteType entry

            GLSLShader.serialize dst entry.shader

            w.WriteType entry.format
            w.Write (int entry.format)

            w.WriteType entry.binary
            w.Write entry.binary.Length
            w.Write entry.binary

            w.WriteType entry.hasTess
            w.Write entry.hasTess

            w.WriteType entry.modes
            match entry.modes with
            | Some modes ->
                w.Write true
                w.Write modes.Count

                for m in modes do
                    w.Write (int m)
            | _ ->
                w.Write false

        let deserialize (src : Stream) =
            use r = new BinaryReader(src, System.Text.Encoding.UTF8, true)

            r.ReadType<ShaderCacheEntry>()

            let shader = GLSLShader.deserialize src

            r.ReadType<BinaryFormat>()
            let format = r.ReadInt32() |> unbox<BinaryFormat>

            r.ReadType<byte[]>()
            let binary =
                let count = r.ReadInt32()
                r.ReadBytes(count)

            r.ReadType<bool>()
            let hasTess = r.ReadBoolean()

            r.ReadType<Option<Set<IndexedGeometryMode>>>()

            let modes =
                if r.ReadBoolean() then
                    let count = r.ReadInt32()

                    List.init count (fun _ ->
                        r.ReadInt32() |> unbox<IndexedGeometryMode>
                    )
                    |> Set.ofList
                    |> Some
                else
                    None

            { shader  = shader
              format  = format
              binary  = binary
              hasTess = hasTess
              modes   = modes }

        let pickle (entry : ShaderCacheEntry) =
            use ms = new MemoryStream()
            serialize ms entry
            ms.ToArray()

        let unpickle (data : byte[]) =
            use ms = new MemoryStream(data)
            deserialize ms


    [<AutoOpen>]
    module private Binary =

        module Program =

            let tryGetBinary (program : Program) =
                if GL.ARB_get_program_binary then
                    GL.GetError() |> ignore

                    let mutable length = 0
                    GL.GetProgram(program.Handle, GetProgramParameterName.ProgramBinaryLength, &length)

                    let err = GL.GetError()
                    if err <> ErrorCode.NoError then
                        Log.warn "[GL] Failed to query program binary length: %A" err
                        None
                    else
                        let data : byte[] = Array.zeroCreate length
                        let mutable format = Unchecked.defaultof<BinaryFormat>
                        GL.GetProgramBinary(program.Handle, length, &length, &format, data)

                        let err = GL.GetError()
                        if err <> ErrorCode.NoError then
                            Log.warn "[GL] Failed to retrieve program binary: %A" err
                            None
                        else
                            Some (format, data)
                else
                    Report.Line(4, "[GL] Cannot read shader cache because GL_ARB_get_program_binary is not supported")
                    None

            let ofShaderCacheEntry (context : Context) (fixBindings : bool) (entry : ShaderCacheEntry) =
                let program = GL.CreateProgram()

                try
                    GL.ProgramBinary(program, entry.format, entry.binary, entry.binary.Length)
                    GL.Check "could not create program from binary"

                    let status = GL.GetProgram(program, GetProgramParameterName.LinkStatus)
                    if status = 0 then
                        failf "linking failed"

                    let program =
                        { Context = context
                          Code = entry.shader.code
                          Handle = program
                          HasTessellation = entry.hasTess
                          SupportedModes = entry.modes
                          Interface = entry.shader.iface }

                    let iface =
                        if fixBindings then
                            ProgramCompiler.fixBindings program entry.shader.iface
                        else
                            entry.shader.iface

                    ResourceCounts.addProgram context
                    { program with Interface = iface }

                with
                | _ ->
                    GL.DeleteProgram program
                    reraise()

    module private FileCache =
        open System.IO

        module private Pickling =

            let tryGetByteArray (program : Program) =
                program |> Program.tryGetBinary |> Option.map (fun (format, binary) ->
                    ShaderCacheEntry.pickle {
                        shader  = { code = program.Code; iface = program.Interface }
                        hasTess = program.HasTessellation
                        format  = format
                        binary  = binary
                        modes   = program.SupportedModes
                    }
                )

            let ofByteArray (context : Context) (fixBindings : bool) (data : byte[]) =
                let entry = ShaderCacheEntry.unpickle data
                Program.ofShaderCacheEntry context fixBindings entry

        let private tryGetCacheFile (context : Context) (key : CodeCacheKey) =
            context.ShaderCachePath |> Option.map (fun prefix ->
                // NOTE: context.Diver represents information obtained by primary context
                // -> possible that resource context have been created differently
                // -> use driver information from actual context
                let driver =
                    match ContextHandle.Current with
                    | ValueSome handle -> handle.Driver
                    | _ ->
                        Log.warn "[GL] No context current, using information of primary context to determine shader cache file name"
                        context.Driver

                let key =
                    {
                        device     = driver.vendor + "_" + driver.renderer + "_" + driver.versionString + "/" + driver.profileMask.ToString()
                        id         = key.id
                        outputs    = key.layout.ColorAttachments |> Map.toList |> List.map (fun (id, att) -> string att.Name, (id, att.Format)) |> Map.ofList
                        layered    = key.layout.PerLayerUniforms
                        layerCount = key.layout.LayerCount
                    }

                let hash = pickler.ComputeHash(key).Hash |> System.Guid
                Path.combine [prefix; string hash + ".bin"]
            )

        let write (key : CodeCacheKey) (program : Program) =
            tryGetCacheFile program.Context key
            |> Option.iter (fun file ->
                try
                    let binary = Pickling.tryGetByteArray program
                    binary |> Option.iter (File.writeAllBytesSafe file)
                with
                | exn ->
                    Log.warn "[GL] Failed to write to shader program file cache '%s': %s" file exn.Message
            )

        let tryRead (context : Context) (fixBindings : bool) (key : CodeCacheKey) =
            tryGetCacheFile context key
            |> Option.bind (fun file ->
                if File.Exists file then
                    try
                        let data = File.readAllBytes file
                        Some <| Pickling.ofByteArray context fixBindings data
                    with
                    | exn ->
                        Log.warn "[GL] Failed to read from shader program file cache '%s': %s" file exn.Message
                        None
                else
                    None
            )

    type Aardvark.Rendering.GL.Context with

        member x.TryGetProgramBinary(prog : Program) =
            use __ = prog.Context.ResourceLock
            Program.tryGetBinary prog

        member x.TryCompileProgramCode(fboSignature : Map<string, int>, code : string) =
            use __ = x.ResourceLock
            ProgramCompiler.tryCompileCode x fboSignature code

        member x.CompileProgramCode(fboSignature : Map<string, int>, code : string) =
            match x.TryCompileProgramCode(fboSignature, code) with
            | Success p -> p
            | Error e ->
                failf "shader compiler returned errors: %s" e

        member x.Delete(p : Program) =
            p.Dispose()

        // [<Obsolete>] ??
        member x.TryCompileComputeProgram(id : string, code : string, iface : GLSL.GLSLProgramInterface) =
            x.TryCompileComputeProgram(id, lazy(code, iface))

        member x.TryCompileComputeProgram(id : string, codeAndInterface : Lazy<string * GLSL.GLSLProgramInterface>) =
            use __ = x.ResourceLock

            let layout =
                { Samples = 0
                  ColorAttachments = Map.empty
                  DepthStencilAttachment = None
                  LayerCount = 0
                  PerLayerUniforms = Set.empty }

            let key : CodeCacheKey =
                { id = id; layout = layout }

            x.ShaderCache.GetOrAdd(key, fun key ->
                match key |> FileCache.tryRead x false with
                | Some program ->
                    Success program

                | _ ->
                    let (code, iface) = codeAndInterface.Value
                    match code |> ProgramCompiler.tryCompileCompute x iface with
                    | Success program ->
                        program |> FileCache.write key
                        Success program

                    | res ->
                        res
            )

        member x.TryCompileProgram(id : string, signature : IFramebufferSignature, code : Lazy<GLSL.GLSLShader>) =
            x.TryCompileProgram(id, signature.Layout, code)

        member x.TryCompileProgram(id : string, layout : FramebufferLayout, shader : Lazy<GLSL.GLSLShader>) : Error<_> =
            let key : CodeCacheKey =
                { id = id; layout = layout }

            x.ShaderCache.GetOrAdd(key, fun key ->
                match key |> FileCache.tryRead x true with
                | Some program ->
                    Success program

                | _ ->
                    let shader = shader.Value
                    let outputs = shader.iface.outputs |> List.map (fun p -> p.paramName, p.paramLocation) |> Map.ofList

                    match shader |> ProgramCompiler.tryCompile x outputs with
                    | Success program ->
                        program |> FileCache.write key
                        Success program

                    | res ->
                        res
            )

        member x.TryCreateProgram(signature : IFramebufferSignature, surface : Surface, topology : IndexedGeometryMode) : Error<GLSL.GLSLProgramInterface * aval<Program>> =
            match surface with
            | Surface.FShadeSimple effect ->
                let key : StaticShaderCacheKey =
                    {
                        effect = effect
                        layout = signature.Layout
                        topology = topology
                        deviceCount = signature.Runtime.DeviceCount
                    }

                x.ShaderCache.GetOrAdd(key, fun key ->
                    let glsl =
                        lazy (
                            try
                                let module_ = key.layout.Link(key.effect, key.deviceCount, Range1d(-1.0, 1.0), false, key.topology)
                                ModuleCompiler.compileGLSL x.FShadeBackend module_
                            with exn ->
                                Log.error "%s" exn.Message
                                reraise()
                        )

                    match x.TryCompileProgram(key.effect.Id, key.layout, glsl) with
                    | Success (prog) ->
                        Success (prog.Interface, AVal.constant prog)
                    | Error e ->
                        Error e
                )

            | Surface.FShade create ->
                x.ShaderCache.GetOrAdd(create, fun _ ->
                    let (inputLayout,b) = create (signature.EffectConfig(Range1d(-1.0, 1.0), false))

                    let initial = AVal.force b
                    let layoutHash = inputLayout.ComputeHash()

                    let compile (m : Module) =
                        try
                            ModuleCompiler.compileGLSL x.FShadeBackend m
                        with exn ->
                            Log.error "%s" exn.Message
                            reraise()

                    let iface =
                        match x.TryCompileProgram(initial.Hash + layoutHash, signature, lazy (compile initial)) with
                        | Success prog -> prog.Interface
                        | Error e ->
                            failf "shader compiler returned errors: %s" e

                    let changeableProgram =
                        b |> AVal.map (fun m ->
                            match x.TryCompileProgram(m.Hash + layoutHash, signature, lazy (compile m)) with
                            | Success p -> p
                            | Error e ->
                                failf "shader compiler returned errors: %s" e
                        )

                    Success (iface, changeableProgram)
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
                failf "shader compiler returned errors: %s" e
