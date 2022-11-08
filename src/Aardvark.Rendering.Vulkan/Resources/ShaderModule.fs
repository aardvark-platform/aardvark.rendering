namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

type ShaderModule =
    class
        inherit Resource<VkShaderModule>
        val public Slot : FShade.ShaderSlot
        val public Interface : FShade.GLSL.GLSLShaderInterface
        val public SpirV : byte[]

        member x.Stage =
            ShaderStage.ofFShade x.Slot.Stage

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyShaderModule(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkShaderModule.Null

        new(device : Device, handle : VkShaderModule, slot, iface, spv) =
            { inherit Resource<_>(device, handle); Slot = slot; Interface = iface; SpirV = spv }
    end

type internal ShaderModuleBinary =
    {
        Slot      : FShade.ShaderSlot
        Interface : FShade.GLSL.GLSLShaderInterface
        SpirV     : byte[]
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderModule =

    let internal glslangStage = function
        | FShade.ShaderSlot.Vertex ->         GLSLang.ShaderStage.Vertex
        | FShade.ShaderSlot.TessControl ->    GLSLang.ShaderStage.TessControl
        | FShade.ShaderSlot.TessEval ->       GLSLang.ShaderStage.TessEvaluation
        | FShade.ShaderSlot.Geometry ->       GLSLang.ShaderStage.Geometry
        | FShade.ShaderSlot.Fragment ->       GLSLang.ShaderStage.Fragment
        | FShade.ShaderSlot.Compute ->        GLSLang.ShaderStage.Compute
        | FShade.ShaderSlot.RayGeneration ->  GLSLang.ShaderStage.RayGen
        | FShade.ShaderSlot.Intersection _ -> GLSLang.ShaderStage.Intersect
        | FShade.ShaderSlot.AnyHit _ ->       GLSLang.ShaderStage.AnyHit
        | FShade.ShaderSlot.ClosestHit _ ->   GLSLang.ShaderStage.ClosestHit
        | FShade.ShaderSlot.Miss _ ->         GLSLang.ShaderStage.Miss
        | FShade.ShaderSlot.Callable _ ->     GLSLang.ShaderStage.Callable

    let private createRaw (binary : byte[]) (device : Device) =
        native {
            let! pBinary = binary
            let! pInfo =
                VkShaderModuleCreateInfo(
                    VkShaderModuleCreateFlags.None,
                    uint64 binary.LongLength,
                    NativePtr.cast pBinary
                )

            let! pHandle = VkShaderModule.Null
            VkRaw.vkCreateShaderModule(device.Handle, pInfo, NativePtr.zero, pHandle)
                |> check "could not create shader module"

            return !!pHandle
        }

    let ofGLSLWithTarget (target : GLSLang.Target) (slot : FShade.ShaderSlot) (info : FShade.GLSL.GLSLShader) (device : Device) =
        let siface = info.iface.shaders.[slot]
        let defines = [slot.Conditional]

        match GLSLang.GLSLang.tryCompileWithTarget target (glslangStage slot) siface.shaderEntry defines info.code with
        | Some binary, _ ->
            let binary = GLSLang.GLSLang.optimizeDefault binary
            let handle = device |> createRaw binary
            let result = new ShaderModule(device, handle, slot, siface, binary)
            result
        | None, err ->
            Log.error "[Vulkan] %A shader compilation failed: %A" slot err
            failf "%A shader compilation failed: %A" slot err

    let ofGLSL (slot : FShade.ShaderSlot) (info : FShade.GLSL.GLSLShader) (device : Device) =
        ofGLSLWithTarget GLSLang.Target.SPIRV_1_0 slot info device

    let ofBinaryWithInfo (slot  : FShade.ShaderSlot) (info : FShade.GLSL.GLSLShaderInterface) (binary : byte[]) (device : Device) =
        let handle = device |> createRaw binary
        let result = new ShaderModule(device, handle, slot, info, binary)
        result

    let internal toBinary (shader : ShaderModule) =
        { Slot      = shader.Slot
          Interface = shader.Interface
          SpirV     = shader.SpirV }

    let internal ofBinary (device : Device) (binary : ShaderModuleBinary) =
        device |> ofBinaryWithInfo binary.Slot binary.Interface binary.SpirV


[<AbstractClass; Sealed; Extension>]
type ContextShaderModuleExtensions private() =

    [<Extension>]
    static member inline CreateShaderModule(this : Device, slot : FShade.ShaderSlot, spirv : byte[], info : FShade.GLSL.GLSLShaderInterface) =
        this |> ShaderModule.ofBinaryWithInfo slot info spirv