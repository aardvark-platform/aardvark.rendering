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



type ShaderProgram(device : Device, renderPass : RenderPass, shaders : array<Shader>, layout : PipelineLayout, original : BackendSurface) =
    static let allTopologies = Enum.GetValues(typeof<IndexedGeometryMode>) |> unbox<IndexedGeometryMode[]> |> Set.ofArray

    // get in-/outputs
    let inputs  = shaders.[0].Interface.inputs
    let outputs = shaders.[shaders.Length - 1].Interface.outputs

    let mutable geometryInfo = None
    let mutable tessInfo = None
    let mutable fragInfo = None

    do for shader in shaders do
        let iface = shader.Interface
        match iface.kind with
            | Geometry info -> geometryInfo <- Some info
            | TessControl info -> tessInfo <- Some info
            | Fragment info -> fragInfo <- Some info
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
                CStr.malloc shader.Interface.entryPoint,
                NativePtr.zero
            )
        )

    member x.Device = device
    member x.RenderPass = renderPass
    member x.Shaders = shaders
    member x.PipelineLayout = layout

    member x.Inputs = inputs
    member x.Outputs = outputs
    member x.UniformBlocks = layout.UniformBlocks
    member x.Textures = layout.Textures

    member x.Surface = original
    member x.UniformGetters = original.Uniforms
    member x.Samplers = original.Samplers

    member x.HasTessellation = Option.isSome tessInfo
    member x.HasDiscard = fragInfo.discard
    member x.FragmentFlags = fragInfo.flags
    member x.SampleShading = fragInfo.sampleShading
    member x.ShaderCreateInfos = createInfos

    interface IBackendSurface with
        member x.Handle = x :> obj
        member x.Inputs = inputs |> List.map (fun p -> p.name, p.hostType)
        member x.Outputs = outputs |> List.map (fun p -> p.name, p.hostType)
        member x.Uniforms = failf "not implemented"
        member x.Samplers = original.Samplers |> Dictionary.toList |> List.map (fun ((a,b),c) -> (a,b,c))
        member x.UniformGetters = original.Uniforms

    member x.Dispose() =
        for s in shaders do device.Delete(s.Module)
        device.Delete(layout)

        for i in 0 .. createInfos.Length-1 do
            let ptr = createInfos.[i].pName
            if not (NativePtr.isNull ptr) then
                NativePtr.free ptr
                createInfos.[i] <- Unchecked.defaultof<_>

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderProgram =
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

    let ofBackendSurface (renderPass : RenderPass) (surface : BackendSurface) (device : Device) =
        let code = 
            layoutRx.Replace(surface.Code, fun m ->
                let set = m.Groups.["set"].Value
                let binding = m.Groups.["binding"].Value
                let name = m.Groups.["name"].Value

                sprintf "layout(set = %s, binding = %s, std140)\r\nuniform %s\r\n{" set binding name
            )
        
        let code = 
            versionRx.Replace(code, "#version 450 core")

        logLines code

        let logs = System.Collections.Generic.Dictionary<ShaderStage, string>()

        let tryGetSamplerDescription (info : ShaderTextureInfo) =
            List.init info.count (fun index ->
                match surface.Samplers.TryGetValue((info.name, index)) with
                    | (true, sam) -> sam
                    | _ -> 
                        Log.warn "[Vulkan] could not resolve sampler/texture for %s[%d]" info.name index
                        { textureName = Symbol.Create(info.name + string index); samplerState = SamplerStateDescription() }
            )

        let binaries =
            surface.EntryPoints
                |> Dictionary.toMap
                |> Map.map (fun stage entry ->
                    let define =
                        match stage with
                            | ShaderStage.Vertex -> "Vertex"
                            | ShaderStage.Fragment -> "Fragment"
                            | ShaderStage.Geometry -> "Geometry"
                            | ShaderStage.TessControl -> "TessControl"
                            | ShaderStage.TessEval -> "TessEval"
                            | ShaderStage.Compute -> "Compute"
                            | _ -> failwithf "unsupported shader stage: %A" stage

                    let gStage = ShaderModule.glslangStage stage 

                    match GLSLang.GLSLang.tryCompile gStage entry [define] code with
                        | Some binary, log ->
                            logs.[stage] <- log
                            binary
                        | None, err ->
                            Log.error "[Vulkan] %A shader compilation failed: %A" stage err
                            failf "%A shader compilation failed: %A" stage err
                )

        let shaders = 
            binaries
                |> Map.toArray
                |> Array.map (fun (stage, binary) ->
                    let shaderModule = device.CreateShaderModule(stage, binary)
                    let shader = shaderModule.[stage]
                    shader.ResolveSamplerDescriptions tryGetSamplerDescription
                )
                
        let pipelineLayout = device.CreatePipelineLayout(shaders)
        new ShaderProgram(device, renderPass, shaders, pipelineLayout, surface)

    let delete (program : ShaderProgram) (device : Device) =
        program.Dispose()


[<AbstractClass; Sealed; Extension>]
type ContextShaderProgramExtensions private() =
    [<Extension>]
    static member CreateShaderProgram(this : Device, pass : RenderPass, surface : ISurface) =
        match surface with
            | :? SignaturelessBackendSurface as s -> s.Get pass |> unbox<ShaderProgram>
            | :? ShaderProgram as p -> p
            | :? BackendSurface as bs -> this |> ShaderProgram.ofBackendSurface pass bs
            | :? IGeneratedSurface as gs ->
                let bs = gs.Generate(this.Runtime, pass) 
                this |> ShaderProgram.ofBackendSurface pass bs
            | _ ->
                failf "bad surface type: %A" surface

    [<Extension>]
    static member inline Delete(this : Device, program : ShaderProgram) =
        this |> ShaderProgram.delete program       