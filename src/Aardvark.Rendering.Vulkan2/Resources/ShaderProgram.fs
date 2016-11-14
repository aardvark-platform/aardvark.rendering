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

type ShaderProgram =
    class
        val mutable public Device : Device
        val mutable public Shaders : Map<ShaderStage, ShaderModule>
        val mutable public PipelineLayout : PipelineLayout
        val mutable public Inputs : list<string * Type>
        val mutable public Outputs : list<string * Type>
        val mutable public Uniforms : list<string * Type>
        val mutable public SamplerStates : SymbolDict<SamplerStateDescription>
        val mutable public UniformGetters : SymbolDict<obj>
        val mutable public RenderPass : RenderPass

        interface IBackendSurface with
            member x.Handle = x.Shaders :> obj
            member x.Inputs = x.Inputs
            member x.Outputs = x.Outputs
            member x.Uniforms = x.Uniforms
            member x.SamplerStates = x.SamplerStates
            member x.UniformGetters = x.UniformGetters
        
       
        new(device : Device, shaders : Map<ShaderStage, ShaderModule>, pipelineLayout : PipelineLayout, pass : RenderPass, inputs, outputs, uniforms, sam, getters) = { Device = device; Shaders = shaders; PipelineLayout = pipelineLayout; RenderPass = pass; Inputs = inputs; Outputs = outputs; Uniforms = uniforms; SamplerStates = sam; UniformGetters = getters }

    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderProgram =
    let private versionRx = System.Text.RegularExpressions.Regex @"\#version[ \t][0-9]+[\r\n]*"

    let ofBackendSurface (renderPass : RenderPass) (surface : BackendSurface) (device : Device) =
        let codes =
            surface.EntryPoints
                |> Dictionary.toArray
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
                    stage, versionRx.Replace(code, "#version 140\r\n" + (sprintf "#define %s\r\n" define))
                )

        let shaders = Array.zeroCreate codes.Length
        let mutable program = Unchecked.defaultof<_>

        let mutable index = 0
        for (stage, code) in codes do
            match GLSLang.GLSLang.tryCreateShader (SpirVReflector.glslangStage stage) code with
                | Success shader ->
                    shaders.[index] <- shader
                | Error err ->
                    Log.error "[Vulkan] %A shader compilation failed: %A" stage err
                    failf "%A shader compilation failed: %A" stage err
            
        match GLSLang.GLSLang.tryCreateProgram shaders with
            | Success prog ->
                try
                    let shaders = 
                        codes |> Array.map (fun (stage,_) ->
                            match prog.TryGetSpirVForStage (SpirVReflector.glslangStage stage) with
                                | Some spirv ->
                                    device.CreateShaderModule(stage, spirv)
                                | _ ->
                                    failf "could not get spirv for stage: %A" stage
                        )


                    let map = shaders |> Array.map (fun s -> s.Stage, s) |> Map.ofArray

                    let bs : IBackendSurface = failwith ""

                    let first   = shaders.[0]
                    let last    = shaders.[shaders.Length - 1]

                    let inputs = first.Interface.inputs |> List.map (fun i -> i.paramName, ShaderType.toType i.paramType)
                    let outputs = last.Interface.outputs |> List.map (fun i -> i.paramName, ShaderType.toType i.paramType)
                    let uniforms =
                        shaders |> Seq.collect (fun s ->
                            s.Interface.uniforms |> List.collect (fun p ->
                                match p.paramType with
                                    | ShaderType.Ptr(_,ShaderType.Struct(_,fields)) ->
                                        fields |> List.map (fun (t,n,_) -> n, ShaderType.toType  t)
                                    | _ ->
                                        // TODO: textures???
                                        [] //[p.paramName, ShaderType.toType p.paramType]
                            )
                        )
                        |> Seq.toList

                    let pipelineLayout =
                        device.CreatePipelineLayout(Array.toList shaders)

                    ShaderProgram(device, map, pipelineLayout, renderPass, inputs, outputs, uniforms, surface.SamplerStates, surface.Uniforms |> SymDict.map (fun _ v -> v :> obj))

                finally
                    shaders |> Array.iter (fun s -> s.Dispose())
                    prog.Dispose()

            | Error err ->
                Log.error "[Vulkan] program compilation failed: %A" err
                failf "program compilation failed: %A" err

    let delete (program : ShaderProgram) (device : Device) =
        for (_,s) in Map.toSeq program.Shaders do
            device.Delete(s)

        device.Delete(program.PipelineLayout)
        program.Shaders <- Map.empty
        program.SamplerStates <- SymDict.empty
        program.UniformGetters <- SymDict.empty


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
    static member inline Delete(this : Device, layout : PipelineLayout) =
        this |> PipelineLayout.delete layout       