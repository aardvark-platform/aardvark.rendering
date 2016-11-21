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
            | None -> { flags = FragmentFlags.None; discard = false }

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

    let uniformValues = original.Uniforms |> SymDict.map (fun _ v -> v :> obj)
    
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
    member x.SamplerStates = original.SamplerStates

    member x.HasTessellation = Option.isSome tessInfo
    member x.HasDiscard = fragInfo.discard
    member x.FragmentFlags = fragInfo.flags
    member x.ShaderCreateInfos = createInfos

    interface IBackendSurface with
        member x.Handle = x :> obj
        member x.Inputs = inputs |> List.map (fun p -> p.name, p.hostType)
        member x.Outputs = outputs |> List.map (fun p -> p.name, p.hostType)
        member x.Uniforms = failf "not implemented"
        member x.SamplerStates = original.SamplerStates
        member x.UniformGetters = uniformValues

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
    let private versionRx = System.Text.RegularExpressions.Regex @"\#version[ \t][0-9]+[\r\n]*"

    let ofBackendSurface (renderPass : RenderPass) (surface : BackendSurface) (device : Device) =
        let codes =
            surface.EntryPoints
                |> Dictionary.toArray
                |> Array.sortBy fst
                |> Array.map (fun (stage, entry) ->
                    let define =
                        match stage with
                            | ShaderStage.Vertex -> "Vertex"
                            | ShaderStage.Pixel -> "Pixel"
                            | ShaderStage.Geometry -> "Geometry"
                            | ShaderStage.TessControl -> "TessControl"
                            | ShaderStage.TessEval -> "TessEval"
                            | _ -> failwithf "unsupported shader stage: %A" stage

                    let code = surface.Code.Replace(sprintf "%s(" entry, "main(")
                    stage, versionRx.Replace(code, "#version 150\r\n" + (sprintf "#define %s\r\n" define))
                )

        printfn "%s" surface.Code

        let shaders = Array.zeroCreate codes.Length
        let mutable program = Unchecked.defaultof<_>

        let mutable index = 0
        for (stage, code) in codes do
            match GLSLang.GLSLang.tryCreateShader (ShaderModule.glslangStage stage) code with
                | Success shader ->
                    shaders.[index] <- shader
                | Error err ->
                    Log.error "[Vulkan] %A shader compilation failed: %A" stage err
                    failf "%A shader compilation failed: %A" stage err
            index <- index + 1

        match GLSLang.GLSLang.tryCreateProgram shaders with
            | Success prog ->
                try
                    let shaders = 
                        codes |> Array.map (fun (stage,_) ->
                            match prog.TryGetSpirVForStage (ShaderModule.glslangStage stage) with
                                | Some spirv ->
                                    let m = device.CreateShaderModule(stage, spirv)
                                    m.[stage]
                                | _ ->
                                    failf "could not get spirv for stage: %A" stage
                        )

                    let pipelineLayout = device.CreatePipelineLayout(shaders)
                    new ShaderProgram(device, renderPass, shaders, pipelineLayout, surface)
                    //ShaderProgram(device, map, pipelineLayout, renderPass, inputs, outputs, uniforms, surface.SamplerStates, surface.Uniforms |> SymDict.map (fun _ v -> v :> obj), surface, List.toArray first.Interface.inputs)

                finally
                    shaders |> Array.iter (fun s -> s.Dispose())
                    prog.Dispose()

            | Error err ->
                Log.error "[Vulkan] program compilation failed: %A" err
                failf "program compilation failed: %A" err

    let delete (program : ShaderProgram) (device : Device) =
        program.Dispose()


[<AbstractClass; Sealed; Extension>]
type ContextShaderProgramExtensions private() =
    [<Extension>]
    static member inline CreateShaderProgram(this : Device, pass : RenderPass, surface : ISurface) =
        match surface with
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