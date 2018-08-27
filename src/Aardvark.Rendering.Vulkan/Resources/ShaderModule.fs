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
        val mutable public Interface : Map<ShaderStage, FShade.GLSL.GLSLShaderInterface>
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
        val mutable public Interface : FShade.GLSL.GLSLShaderInterface

        //member x.ResolveSamplerDescriptions (resolve : ShaderTextureInfo -> list<SamplerDescription>) =
        //    Shader(x.Module, x.Stage, x.Interface |> ShaderInfo.resolveSamplerDescriptions resolve)

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
        
    let ofGLSL (stage : ShaderStage) (info : FShade.GLSL.GLSLShader) (device : Device) =
        let siface = info.iface.shaders.[ShaderStage.toFShade stage]
        match GLSLang.GLSLang.tryCompile (glslangStage stage) siface.shaderEntry [string stage] info.code with
            | Some binary, _ ->
                let binary = GLSLang.GLSLang.optimizeDefault binary
                let iface = Map.ofList [stage, siface]
                let handle = device |> createRaw binary
                let result = ShaderModule(device, handle, stage, iface, binary)
                result
            | None, err ->
                Log.error "[Vulkan] %A shader compilation failed: %A" stage err
                failf "%A shader compilation failed: %A" stage err

    let ofBinaryWithInfo (stage : ShaderStage) (info : FShade.GLSL.GLSLShaderInterface) (binary : byte[]) (device : Device) =
        let iface = Map.ofList [stage, info]
        let handle = device |> createRaw binary
        let result = ShaderModule(device, handle, stage, iface, binary)
        result

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




[<AbstractClass; Sealed; Extension>]
type ContextShaderModuleExtensions private() =
    //[<Extension>]
    //static member inline CreateShaderModule(this : Device, stage : ShaderStage, glsl : string) =
    //    this |> ShaderModule.ofGLSL stage glsl

    //[<Extension>]
    //static member inline CreateShaderModule(this : Device, stage : ShaderStage, spirv : byte[]) =
    //    this |> ShaderModule.ofBinary stage spirv
        
    [<Extension>]
    static member inline CreateShaderModule(this : Device, stage : ShaderStage, spirv : byte[], info : FShade.GLSL.GLSLShaderInterface) =
        this |> ShaderModule.ofBinaryWithInfo stage info spirv
        
    [<Extension>]
    static member inline Delete(this : Device, shader : ShaderModule) =
        this |> ShaderModule.delete shader
        