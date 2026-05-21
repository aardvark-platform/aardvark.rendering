namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

type ShaderModule =
    class
        inherit Resource<VkShaderModule>
        val public Slot : FShade.ShaderSlot
        val public SpirV : byte[]

        member x.Stage =
            ShaderStage.ofFShade x.Slot.Stage

        override x.Destroy() =
            if x.Handle.IsValid then
                VkRaw.vkDestroyShaderModule(x.Device.Handle, x.Handle, NativePtr.zero)
                x.Handle <- VkShaderModule.Null

        new(device : Device, handle : VkShaderModule, slot : FShade.ShaderSlot, spirv : byte[]) =
            { inherit Resource<_>(device, handle)
              Slot = slot
              SpirV = spirv }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderModule =

    let private nl = System.Environment.NewLine

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

    // FShade cannot emit the fragment-stage execution-mode layout that
    // fragment-shader interlock requires, so inject it into the generated GLSL
    // for the fragment slot before handing it to glslang. Inserted after the
    // interlock #extension line so GLSL ordering stays valid.
    let private interlockExtRx =
        System.Text.RegularExpressions.Regex @"(#extension[ \t]+GL_(?:ARB|EXT)_fragment_shader_interlock[ \t]*:[^\n]*\n)"

    let private injectFragmentInterlock (slot : FShade.ShaderSlot) (code : string) =
        if slot = FShade.ShaderSlot.Fragment && code.Contains "beginInvocationInterlockARB" && interlockExtRx.IsMatch code then
            let decl = "layout(pixel_interlock_unordered) in;\nlayout(early_fragment_tests) in;\n"
            interlockExtRx.Replace(code, System.Text.RegularExpressions.MatchEvaluator(fun m -> m.Groups.[1].Value + decl), 1)
        else
            code

    let ofGLSLWithTarget (target : GLSLang.Target) (slot : FShade.ShaderSlot) (info : FShade.GLSL.GLSLShader) (device : Device) =
        let siface = info.iface.shaders.[slot]
        let defines = [slot.Conditional]
        let config = device.DebugConfig
        let code = injectFragmentInterlock slot info.code

        match GLSLang.GLSLang.tryCompileWithTarget target (glslangStage slot) siface.shaderEntry config.GenerateShaderDebugInfo defines code with
        | Some binary, wrn ->
            if config.PrintShaderCode && not <| System.String.IsNullOrWhiteSpace wrn then
                let wrn = wrn |> String.indent 1
                Report.Line $"{slot} shader compilation succeeded with warnings:{nl}{nl}{wrn}"

            let binary =
                if not config.OptimizeShaders then binary
                else GLSLang.GLSLang.optimizeDefault binary

            let handle = device |> createRaw binary
            let result = new ShaderModule(device, handle, slot, binary)
            result

        | None, err ->
            if not config.PrintShaderCode then
                ShaderCodeReporting.logLines "Failed to compile shader" code

            let err = String.normalizeLineEndings err
            failf $"{slot} shader compilation failed:{nl}{nl}{err}"

    let ofGLSL (slot : FShade.ShaderSlot) (info : FShade.GLSL.GLSLShader) (device : Device) =
        ofGLSLWithTarget GLSLang.Target.SPIRV_1_0 slot info device

    let ofBinary (device : Device) (slot : FShade.ShaderSlot) (binary : byte[]) =
        let handle = device |> createRaw binary
        let result = new ShaderModule(device, handle, slot, binary)
        result

[<AbstractClass; Sealed; Extension>]
type ContextShaderModuleExtensions private() =

    [<Extension>]
    static member inline CreateShaderModule(this : Device, slot : FShade.ShaderSlot, spirv : byte[]) =
        spirv |> ShaderModule.ofBinary this slot