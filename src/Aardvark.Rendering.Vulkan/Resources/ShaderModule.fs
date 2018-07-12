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

type ShaderModule =
    class
        inherit Resource<VkShaderModule>
        val mutable public Stage : ShaderStage
        val mutable public Interface : Map<ShaderStage, ShaderInfo>
        val mutable public SpirV : byte[]

        member x.TryGetShader(stage : ShaderStage, [<Out>] shader : byref<Shader>) =
            match Map.tryFind stage x.Interface with
                | Some i -> 
                    shader <- Shader(x, stage, i)
                    true
                | _ ->
                    false

        member x.Item
            with get(stage : ShaderStage) =
                match Map.tryFind stage x.Interface with
                    | Some i -> Shader(x, stage, i)
                    | _ -> failf "cannot get %A-Shader from module %A" stage x.Interface

        new(device : Device, handle : VkShaderModule, stage, iface, spv) = { inherit Resource<_>(device, handle); Stage = stage; Interface = iface; SpirV = spv }
    end

and Shader =
    class
        val mutable public Module : ShaderModule
        val mutable public Stage : ShaderStage
        val mutable public Interface : ShaderInfo

        member x.ResolveSamplerDescriptions (resolve : ShaderTextureInfo -> list<SamplerDescription>) =
            Shader(x.Module, x.Stage, x.Interface |> ShaderInfo.resolveSamplerDescriptions resolve)

        override x.GetHashCode() = HashCode.Combine(x.Module.GetHashCode(), x.Stage.GetHashCode())
        override x.Equals o =
            match o with
                | :? Shader as o -> x.Module = o.Module && x.Stage = o.Stage
                | _ -> false

        internal new(m,s,i) = { Module = m; Stage = s; Interface = i }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderModule =
    
    let internal glslangStage =
        LookupTable.lookupTable [
            ShaderStage.Vertex, GLSLang.ShaderStage.Vertex
            ShaderStage.TessControl, GLSLang.ShaderStage.TessControl
            ShaderStage.TessEval, GLSLang.ShaderStage.TessEvaluation
            ShaderStage.Geometry, GLSLang.ShaderStage.Geometry
            ShaderStage.Fragment, GLSLang.ShaderStage.Fragment
            ShaderStage.Compute, GLSLang.ShaderStage.Compute
        ]

    let private createRaw (binary : byte[]) (device : Device) =
        binary |> NativePtr.withA (fun pBinary ->
            let mutable info =
                VkShaderModuleCreateInfo(
                    VkStructureType.ShaderModuleCreateInfo, 0n, 
                    VkShaderModuleCreateFlags.MinValue,
                    uint64 binary.LongLength,
                    NativePtr.cast pBinary
                )

            let mutable handle = VkShaderModule.Null
            VkRaw.vkCreateShaderModule(device.Handle, &&info, NativePtr.zero, &&handle)
                |> check "could not create shader module"

            handle
        )

    let ofBinary (stage : ShaderStage) (binary : byte[]) (device : Device) =
        let iface = ShaderInfo.ofBinary binary
        let handle = device |> createRaw binary
        let result = ShaderModule(device, handle, stage, iface, binary)
        result

    let ofBinaryWithInfo (stage : ShaderStage) (info : ShaderInfo) (binary : byte[]) (device : Device) =
        let iface = Map.ofList [stage, info]
        let handle = device |> createRaw binary
        let result = ShaderModule(device, handle, stage, iface, binary)
        result

    let ofGLSL (stage : ShaderStage) (code : string) (device : Device) =
        match GLSLang.GLSLang.tryCompile (glslangStage stage) "main" [string stage] code with
            | Some binary, _ ->
                let handle = device |> createRaw binary
                let iface = ShaderInfo.ofBinary binary
                let result = ShaderModule(device, handle, stage, iface, binary)
                result
            | None, err ->
                Log.error "%s" err
                failf "shader compiler returned errors %A" err

    


    let delete (shader : ShaderModule) (device : Device) =
        if shader.Handle.IsValid then
            VkRaw.vkDestroyShaderModule(device.Handle, shader.Handle, NativePtr.zero)
            shader.Handle <- VkShaderModule.Null
    
    let get (stage : ShaderStage) (m : ShaderModule) =
        m.[stage]

    let tryGetShader (stage : ShaderStage) (m : ShaderModule) =
        match Map.tryFind stage m.Interface with
            | Some i -> Some (Shader(m, stage, i))
            | _ -> None

    let private effectCache = System.Collections.Concurrent.ConcurrentDictionary<string, Error<Map<ShaderStage, ShaderModule>>>()
    
    open FShade
    open FShade.Imperative
    open FShade.GLSL

    let rec private (|LSuccess|LError|) (l : list<'x * Error<'a>>) =
        match l with
            | [] -> LSuccess []
            | (k,h) :: t ->
                match h with
                    | Error e -> 
                        match t with
                            | LSuccess t -> LError [k, e]
                            | LError t -> LError ((k,e) :: t)
                    | Success v ->
                        match t with 
                            | LSuccess t -> LSuccess ((k,v) :: t)
                            | LError t -> LError t

    let private toBaseShaderStage =
        LookupTable.lookupTable [
            FShade.ShaderStage.Vertex, Aardvark.Base.ShaderStage.Vertex
            FShade.ShaderStage.TessControl, Aardvark.Base.ShaderStage.TessControl
            FShade.ShaderStage.TessEval, Aardvark.Base.ShaderStage.TessEval
            FShade.ShaderStage.Geometry, Aardvark.Base.ShaderStage.Geometry
            FShade.ShaderStage.Fragment, Aardvark.Base.ShaderStage.Fragment
            FShade.ShaderStage.Compute, Aardvark.Base.ShaderStage.Compute
        ]

    module private String =
        open System.Text.RegularExpressions

        let private lineBreak = Regex @"\r\n"
        
        let lines (str : String) = lineBreak.Split str

        
    let private shaderCachePath = ""




    let tryOfModule (module_ : Module) (device : Device) =
        effectCache.GetOrAdd(module_.hash, fun _ ->
            let effect = module_.userData |> unbox<Effect>
        
            let glsl = 
                module_ 
                |> ModuleCompiler.compile PipelineInfo.fshadeBackend
                |> Assembler.assemble PipelineInfo.fshadeBackend

            let code = glsl.code
            
            let stages = 
                module_.entries |> List.choose (fun e ->
                    e.decorations |> List.tryPick (
                        function 
                        | EntryDecoration.Stages { self = s } -> Some (toBaseShaderStage s) 
                        | _ -> None
                    )
                )

            let results = 
                stages |> List.map (fun stage ->
                    match GLSLang.GLSLang.tryCompile (glslangStage stage) "main" [string stage] code with
                        | Some binary, _ ->
                            let handle = device |> createRaw binary
                            let iface = ShaderInfo.ofBinary binary
                            let result = ShaderModule(device, handle, stage, iface, binary)
                            
                            stage, Success result
                        | None, err ->
                            stage, Error err
                )

            match results with
                | LSuccess modules ->
                    modules
                    |> Map.ofList
                    |> Success
                | LError errors ->
                    errors |> Seq.map (fun (stage, error) ->
                        let error = String.lines error |> Array.map (sprintf "    %s") |> String.concat "\r\n"
                        sprintf "%A:\r\n%s" stage error
                    )
                    |> String.concat "\r\n"
                    |> Error

            
        )
        



[<AbstractClass; Sealed; Extension>]
type ContextShaderModuleExtensions private() =
    [<Extension>]
    static member inline CreateShaderModule(this : Device, stage : ShaderStage, glsl : string) =
        this |> ShaderModule.ofGLSL stage glsl

    [<Extension>]
    static member inline CreateShaderModule(this : Device, stage : ShaderStage, spirv : byte[]) =
        this |> ShaderModule.ofBinary stage spirv
        
    [<Extension>]
    static member inline CreateShaderModule(this : Device, stage : ShaderStage, spirv : byte[], info : ShaderInfo) =
        this |> ShaderModule.ofBinaryWithInfo stage info spirv
        
    [<Extension>]
    static member inline Delete(this : Device, shader : ShaderModule) =
        this |> ShaderModule.delete shader
        