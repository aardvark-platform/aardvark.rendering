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

module private FShadeAdapter =
    open FShade
    open FShade.GLSL
    open Aardvark.Base.MultimethodTest


    let private toGeometryFlags (d : GLSLShaderDecoration) =
        match d with
            | GLSLInputTopology t ->
                match t with
                    | InputTopology.Point -> GeometryFlags.InputPoints
                    | InputTopology.Line -> GeometryFlags.InputLines
                    | InputTopology.LineAdjacency -> GeometryFlags.InputLinesAdjacency
                    | InputTopology.Triangle -> GeometryFlags.InputTriangles
                    | InputTopology.TriangleAdjacency -> GeometryFlags.InputTrianglesAdjacency
                    | InputTopology.Patch 1 -> GeometryFlags.InputPoints
                    | InputTopology.Patch 2 -> GeometryFlags.InputLines
                    | InputTopology.Patch 3 -> GeometryFlags.InputTriangles
                    | InputTopology.Patch 4 -> GeometryFlags.InputLinesAdjacency
                    | InputTopology.Patch 6 -> GeometryFlags.InputTrianglesAdjacency
                    | InputTopology.Patch _ -> GeometryFlags.None

            | GLSLOutputTopology t ->
                match t with
                    | OutputTopology.Points -> GeometryFlags.OutputPoints
                    | OutputTopology.LineStrip -> GeometryFlags.OutputLineStrip
                    | OutputTopology.TriangleStrip -> GeometryFlags.OutputTriangleStrip

            | _ ->
                GeometryFlags.None
    
    let private toTessellationFlags (d : GLSLShaderDecoration) =
        match d with
            | GLSLOutputTopology t ->
                match t with
                    | OutputTopology.Points -> TessellationFlags.OutputPoints
                    | OutputTopology.LineStrip -> TessellationFlags.OutputIsolines
                    | OutputTopology.TriangleStrip -> TessellationFlags.OutputTriangles

            | GLSLSpacing s ->
                match s with
                    | GLSLSpacing.Equal -> TessellationFlags.SpacingEqual
                    | GLSLSpacing.FractionalEven -> TessellationFlags.SpacingFractionalEven
                    | GLSLSpacing.FractionalOdd -> TessellationFlags.SpacingFractionalOdd
                    
            | GLSLWinding w ->
                match w with
                    | GLSLWinding.CCW -> TessellationFlags.VertexOrderCcw
                    | GLSLWinding.CW -> TessellationFlags.VertexOrderCw

            | _ ->
                TessellationFlags.None
    
    let geometryInfo (iface : GLSLShaderInterface) =
        {
            outputVertices =
                iface.shaderDecorations 
                |> List.tryPick (function GLSLMaxVertices v -> Some v | _ -> None)
                |> Option.defaultValue 1
        
            invocations =
                iface.shaderDecorations 
                |> List.tryPick (function GLSLInvocations v -> Some v | _ -> None)
                |> Option.defaultValue 1

            flags =
                iface.shaderDecorations
                |> List.fold (fun f d -> f ||| toGeometryFlags d) GeometryFlags.None
                
        }

    let tessControlInfo (iface : GLSLShaderInterface) =
        {
            
            outputVertices =
                iface.shaderDecorations 
                |> List.tryPick (function GLSLMaxVertices v -> Some v | _ -> None)
                |> Option.defaultValue 1
        
            flags =
                iface.shaderDecorations
                |> List.fold (fun f d -> f ||| toTessellationFlags d) TessellationFlags.None
                
        }

    let fragmentInfo (iface : GLSLShaderInterface) =
        {
            flags = FragmentFlags.None
            discard = GLSLShaderInterface.usesDiscard iface
            sampleShading = MapExt.containsKey "gl_SampleLocation" iface.shaderBuiltInInputs
        }

type ShaderProgram(device : Device, shaders : array<Shader>, layout : PipelineLayout, original : string, iface : FShade.GLSL.GLSLProgramInterface) =
    inherit RefCountedResource()

    static let allTopologies = Enum.GetValues(typeof<IndexedGeometryMode>) |> unbox<IndexedGeometryMode[]> |> Set.ofArray

    // get in-/outputs
    let inputs  = shaders.[0].Interface.shaderInputs
    let outputs = shaders.[shaders.Length - 1].Interface.shaderOutputs

    let mutable geometryInfo = None
    let mutable tessInfo = None
    let mutable fragInfo = None

    do for shader in shaders do
        let iface = shader.Interface
        match iface.shaderStage with
            | FShade.ShaderStage.Geometry -> geometryInfo <- Some <| FShadeAdapter.geometryInfo iface
            | FShade.ShaderStage.TessControl -> tessInfo <- Some <| FShadeAdapter.tessControlInfo iface
            | FShade.ShaderStage.Fragment-> fragInfo <- Some <| FShadeAdapter.fragmentInfo iface
            | _ -> ()

    let acceptedTopologies =
        match tessInfo, geometryInfo with
            | Some i, _ ->
                let flags = i.flags
                match i.outputVertices with
                    | 1 -> Set.singleton IndexedGeometryMode.PointList
                    | 2 -> Set.ofList [ IndexedGeometryMode.LineList; IndexedGeometryMode.LineStrip ]
                    | 3 -> Set.ofList [ IndexedGeometryMode.TriangleList; IndexedGeometryMode.TriangleStrip ]
                    | 4 -> Set.ofList [ IndexedGeometryMode.LineAdjacencyList; IndexedGeometryMode.QuadList ]
                    | 6 -> Set.ofList [ IndexedGeometryMode.TriangleAdjacencyList ]
                    | _ -> failf "bad tess-control vertex-count: %A" i.outputVertices

            | None, Some i ->
                let flags = i.flags
                if flags.HasFlag GeometryFlags.InputPoints then Set.ofList [ IndexedGeometryMode.PointList ]
                elif flags.HasFlag GeometryFlags.InputLines then Set.ofList [ IndexedGeometryMode.LineList; IndexedGeometryMode.LineStrip ]
                elif flags.HasFlag GeometryFlags.InputLinesAdjacency then Set.ofList [ IndexedGeometryMode.LineAdjacencyList ]
                elif flags.HasFlag GeometryFlags.InputTriangles then Set.ofList [ IndexedGeometryMode.TriangleList; IndexedGeometryMode.TriangleStrip ]
                elif flags.HasFlag GeometryFlags.InputTrianglesAdjacency then Set.ofList [ IndexedGeometryMode.TriangleAdjacencyList ]
                else Set.empty

            | None, None ->
                allTopologies

    let fragInfo =
        match fragInfo with
            | Some i -> i
            | None -> { flags = FragmentFlags.None; discard = false; sampleShading = false }

    let createInfos =
        shaders |> Array.map (fun shader ->
            VkPipelineShaderStageCreateInfo(
                VkStructureType.PipelineShaderStageCreateInfo, 0n,
                VkPipelineShaderStageCreateFlags.MinValue,
                VkShaderStageFlags.ofShaderStage shader.Stage,
                shader.Module.Handle,
                CStr.malloc shader.Interface.shaderEntry,
                NativePtr.zero
            )
        )

    let mutable cacheName = Symbol.Empty

    member x.Interface = iface

    member internal x.CacheName
        with get() = cacheName
        and set n = cacheName <- n

    member x.Device = device
    member x.Shaders = shaders
    member x.PipelineLayout = layout

    member x.GLSL = original
    member x.Inputs = inputs
    member x.Outputs = outputs
    //member x.UniformBlocks = layout.UniformBlocks
    //member x.Textures = layout.Textures

    member x.Surface = original
    //member x.UniformGetters = original.Uniforms
    //member x.Samplers = original.Samplers

    member x.HasTessellation = Option.isSome tessInfo
    member x.HasDiscard = fragInfo.discard
    member x.FragmentFlags = fragInfo.flags
    member x.SampleShading = fragInfo.sampleShading
    member x.ShaderCreateInfos = createInfos
    member x.TessellationPatchSize = 
        match tessInfo with
            | Some i -> i.outputVertices
            | None -> 0

    override x.Destroy() =
        for s in shaders do device.Delete(s.Module)
        device.Delete(layout)

        for i in 0 .. createInfos.Length-1 do
            let ptr = createInfos.[i].pName
            if not (NativePtr.isNull ptr) then
                NativePtr.free ptr
                createInfos.[i] <- Unchecked.defaultof<_>

    interface IBackendSurface with
        member x.Handle = x :> obj
        member x.Inputs = failwith "obsolete" //inputs |> List.map (fun p -> p.paramName, p.paramType)
        member x.Outputs = failwith "obsolete" //outputs |> List.map (fun p -> p.paramName, p.paramType)
        member x.Uniforms = failf "not implemented"
        member x.Samplers = failf "not implemented" //original.Samplers |> Dictionary.toList |> List.map (fun ((a,b),c) -> (a,b,c))
        member x.UniformGetters = failf "not implemented" //original.Uniforms

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderProgram =
    open System.IO
    open FShade.GLSL

    let private versionRx = System.Text.RegularExpressions.Regex @"\#version.*$"
    let private layoutRx = System.Text.RegularExpressions.Regex @"layout[ \t]*\([ \t]*set[ \t]*\=[ \t]*(?<set>[0-9]+),[ \t]*binding[ \t]*\=[ \t]*(?<binding>[0-9]+)[ \t]*\)[ \t\r\n]*uniform[ \t]+(?<name>[_a-zA-Z0-9]+)[ \t\r\n]*\{"
    

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
    
    let logLines (code : string) =
        let lineCount = String.lineCount code
        let lineColumns = 1 + int (Fun.Log10 lineCount)
        let lineFormatLen = lineColumns + 3
        let sb = new System.Text.StringBuilder(code.Length + lineFormatLen * lineCount + 10)
            
        let fmtStr = "{0:" + lineColumns.ToString() + "} : "
        let mutable lineEnd = code.IndexOf('\n')
        let mutable lineStart = 0
        let mutable lineCnt = 1
        while (lineEnd >= 0) do
            sb.Clear() |> ignore
            let line = code.Substring(lineStart, lineEnd - lineStart)
            sb.Append(lineCnt.ToString().PadLeft(lineColumns)) |> ignore
            sb.Append(": ")  |> ignore
            sb.Append(line) |> ignore
            Report.Line("{0}", sb.ToString())
            lineStart <- lineEnd + 1
            lineCnt <- lineCnt + 1
            lineEnd <- code.IndexOf('\n', lineStart)
            ()

        let lastLine = code.Substring(lineStart)
        if lastLine.Length > 0 then
            sb.Clear() |> ignore
            sb.Append(lineCnt.ToString()) |> ignore
            sb.Append(": ")  |> ignore
            sb.Append(lastLine) |> ignore
            Report.Line("{0}", sb.ToString())

    let private ofGLSLInteral (layout : Option<PipelineLayout>) (iface : FShade.GLSL.GLSLProgramInterface) (code : string) (device : Device) =
        let code = 
            layoutRx.Replace(code, fun m ->
                let set = m.Groups.["set"].Value
                let binding = m.Groups.["binding"].Value
                let name = m.Groups.["name"].Value

                sprintf "layout(set = %s, binding = %s, std140)\r\nuniform %s\r\n{" set binding name
            )

        let code = code.Replace("gl_InstanceID", "gl_InstanceIndex").Replace("gl_VertexID", "gl_VertexIndex")
        
        let code = 
            versionRx.Replace(code, "#version 450 core")

        logLines code

        let logs = System.Collections.Generic.Dictionary<ShaderStage, string>()

        let binaries =
            iface.shaders
                |> MapExt.toArray
                |> Array.map (fun (fshadeStage, shader) ->
                    let entry = shader.shaderEntry
                    let stage = ShaderStage.ofFShade fshadeStage
                    let define =
                        match fshadeStage with
                            | FShade.ShaderStage.Vertex -> "Vertex"
                            | FShade.ShaderStage.Fragment -> "Fragment"
                            | FShade.ShaderStage.Geometry -> "Geometry"
                            | FShade.ShaderStage.TessControl -> "TessControl"
                            | FShade.ShaderStage.TessEval -> "TessEval"
                            | FShade.ShaderStage.Compute -> "Compute"
                            | _ -> failwithf "unsupported shader stage: %A" stage


                    let gStage = ShaderModule.glslangStage stage 

                    match GLSLang.GLSLang.tryCompile gStage entry [define] code with
                        | Some binary, log ->
                            logs.[stage] <- log
                            stage, binary, iface.shaders.[fshadeStage]
                        | None, err ->
                            Log.error "[Vulkan] %A shader compilation failed: %A" stage err
                            failf "%A shader compilation failed: %A" stage err
                )

        let shaders = 
            binaries
                |> Array.map (fun (stage, binary, iface) ->
                    let shaderModule = device.CreateShaderModule(stage, binary, iface)
                    let shader = shaderModule.[stage]
                    shader //.ResolveSamplerDescriptions tryGetSamplerDescription
                )

        let layout = 
            match layout with
                | Some l -> l.AddRef(); l
                | None -> device.CreatePipelineLayout(shaders, 1, Set.empty)

        new ShaderProgram(device, shaders, layout, code, iface)


    let private effectCache = Symbol.Create "effectCache"
    let private moduleCache = Symbol.Create "moduleCache"

    let ofGLSL (code : FShade.GLSL.GLSLShader) (device : Device) =
        ofGLSLInteral None code.iface code.code device

    let private pickler = MBrace.FsPickler.FsPickler.CreateBinarySerializer()

    let private shaderCachePath =
        let path = 
            Path.combine [
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                "Aardvark"
                "VulkanShaderCache"
            ]
        if not (Directory.Exists path) then Directory.CreateDirectory path |> ignore
        path

    type private ShaderProgramData =
        {
            glsl    : string
            code    : Map<ShaderStage, byte[]>
            iface   : GLSLProgramInterface
        }

    let toByteArray (program : ShaderProgram) =
        pickler.Pickle {
            glsl = program.GLSL
            code = program.Shaders |> Seq.map (fun s -> s.Module.Stage, s.Module.SpirV) |> Map.ofSeq
            iface = program.Interface
        }

    let tryOfByteArray (data : byte[]) (device : Device) =
        try
            let data : ShaderProgramData = pickler.UnPickle data

            let shaders = 
                data.code 
                |> Map.toArray 
                |> Array.map (fun (stage, arr) -> 
                    let iface = data.iface.shaders.[ShaderStage.toFShade stage]
                    let module_ = device.CreateShaderModule(stage, arr, iface)
                    new Shader(module_, stage, iface)
                    
                )
            let layout = device.CreatePipelineLayout(shaders, 1, Set.empty)

            let program = new ShaderProgram(device, shaders, layout, data.glsl, data.iface)
            Some program
        with _ ->
            None
            
    let ofByteArray (data : byte[]) (device : Device) =
        match tryOfByteArray data device with
            | Some p -> p
            | None -> failf "could not load program"

    let private hashFileName (value : obj) : string =
        let hash = pickler.ComputeHash value
        hash.Hash |> Guid |> string

    let private tryRead (file : string) (device : Device) : Option<ShaderProgram> =
        try
            if File.Exists file then
                let data = File.ReadAllBytes file
                match tryOfByteArray data device with
                    | Some c -> 
                        Some c
                    | None ->
                        Log.warn "[Vulkan] bad shader cache %s" (Path.GetFileName file)
                        None
            else
                None
        with _ ->
            None
    
    let private write (file : string) (program : ShaderProgram) =
        try
            let data = toByteArray program
            File.WriteAllBytes(file, data)
        with _ ->
            ()

    let ofModule (module_ : FShade.Imperative.Module) (device : Device) =
        device.GetCached(moduleCache, module_, fun module_ ->
            let fileName = hashFileName (module_.hash)
        
            let cacheFile = Path.Combine(shaderCachePath, fileName + ".module")
            match tryRead cacheFile device with
                | Some p ->
                    p.CacheName <- moduleCache
                    p.RefCount <- 1 // leak
                    p

                | None ->

                    let glsl = 
                        module_ 
                        |> FShade.Imperative.ModuleCompiler.compile PipelineInfo.fshadeBackend
                        |> FShade.GLSL.Assembler.assemble PipelineInfo.fshadeBackend
                
                    let res = ofGLSL glsl device
                    write cacheFile res
                    res.CacheName <- moduleCache
                    res.RefCount <- 1 // leak
                    res
        )
    
    let ofEffect (effect : FShade.Effect) (mode : IndexedGeometryMode) (pass : RenderPass) (device : Device) =
        device.GetCached(effectCache, (effect, mode, pass), fun (effect, mode, cfg) ->
            let fileName = 
                let colors = pass.ColorAttachments |> Map.map (fun _ (a,b) -> a.ToString())
                let depth = pass.DepthStencilAttachment |> Option.map (fun (a,b) -> a)
                if pass.LayerCount > 1 then
                    hashFileName (effect.Id, mode, colors, depth, pass.LayerCount, pass.PerLayerUniforms)
                else
                    hashFileName (effect.Id, colors, depth)
                    
            let cacheFile = Path.Combine(shaderCachePath, fileName + ".effect")
            match tryRead cacheFile device with
                | Some p ->
                    p.CacheName <- effectCache
                    p.RefCount <- 1 // leak
                    p

                | None ->
                    let glsl = 
                        pass.Link(effect, PipelineInfo.fshadeConfig.depthRange, PipelineInfo.fshadeConfig.flipHandedness, mode)
                        |> FShade.Imperative.ModuleCompiler.compile PipelineInfo.fshadeBackend
                        |> FShade.GLSL.Assembler.assemble PipelineInfo.fshadeBackend
                
                    let res = ofGLSL glsl device
                    write cacheFile res
                    res.CacheName <- effectCache
                    res.RefCount <- 1 // leak
                    res
        )

    let delete (program : ShaderProgram) (device : Device) =
        if program.CacheName <> Symbol.Empty then
            device.RemoveCached(program.CacheName, program)
        else
            program.Shaders |> Array.iter (fun s -> device.Delete s.Module)
            device.Delete program.PipelineLayout




[<AbstractClass; Sealed; Extension>]
type ContextShaderProgramExtensions private() =
    [<Extension>]
    static member CreateShaderProgram(this : Device, pass : RenderPass, effect : FShade.Effect, top : IndexedGeometryMode) =
        ShaderProgram.ofEffect effect top pass this

    [<Extension>]
    static member CreateShaderProgram(this : Device, module_ : FShade.Imperative.Module) =
        ShaderProgram.ofModule module_ this

    [<Extension>]
    static member CreateShaderProgram(this : Device, surface : ISurface) =
        match surface with
            | :? ShaderProgram as p ->
                Interlocked.Increment(&p.RefCount) |> ignore
                p
            | _ ->
                failf "unknown surface %A" surface
             

    [<Extension>]
    static member inline Delete(this : Device, program : ShaderProgram) =
        this |> ShaderProgram.delete program       