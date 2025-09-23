namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open FShade
open FShade.GLSL

type ComputeProgram =
    class
        inherit CachedResource

        val public Pipeline : VkPipeline
        val public PipelineLayout : PipelineLayout
        val public ShaderModule : ShaderModule
        val public GroupSize : V3i
        val public Shader : GLSLShader

        override x.Destroy() =
            VkRaw.vkDestroyPipeline(x.Device.Handle, x.Pipeline, NativePtr.zero)
            x.PipelineLayout.Dispose()
            x.ShaderModule.Dispose()

        interface IComputeShader with
            member x.Runtime = x.Device.Runtime
            member x.LocalSize = x.GroupSize
            member x.Interface = x.Shader.iface

        new (device : Device, pipeline : VkPipeline, layout : PipelineLayout, module_ : ShaderModule, groupSize : V3i, shader : GLSLShader) =
            { inherit CachedResource(device)
              Pipeline       = pipeline
              PipelineLayout = layout
              ShaderModule   = module_
              GroupSize      = groupSize
              Shader         = shader }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ComputeProgram =
    open System.IO

    type private ComputeProgramData =
        {
            shader    : GLSLShader
            binary    : byte[]
            groupSize : V3i
        }

    module private ComputeProgramData =
        open ShaderProgram.ShaderProgramData

        type BinaryWriter with
            member inline x.Write(value : V3i) =
                x.Write value.X
                x.Write value.Y
                x.Write value.Z

        type BinaryReader with
            member inline x.ReadV3i() =
                V3i(x.ReadInt32(), x.ReadInt32(), x.ReadInt32())

        let serialize (dst : Stream) (data : ComputeProgramData) =
            use w = new BinaryWriter(dst, System.Text.Encoding.UTF8, true)

            w.WriteType data

            GLSLShader.serialize dst data.shader

            w.WriteType data.binary
            w.Write data.binary.Length
            w.Write data.binary

            w.WriteType data.groupSize
            w.Write data.groupSize

        let deserialize (src : Stream) =
            use r = new BinaryReader(src, System.Text.Encoding.UTF8, true)

            r.ReadType<ComputeProgramData>()

            let shader = GLSLShader.deserialize src

            r.ReadType<byte[]>()

            let binary =
                let count = r.ReadInt32()
                r.ReadBytes count

            r.ReadType<V3i>()
            let groupSize = r.ReadV3i()

            { shader    = shader
              binary    = binary
              groupSize = groupSize }

        let pickle (data : ComputeProgramData) =
            use ms = new MemoryStream()
            serialize ms data
            ms.ToArray()

        let unpickle (data : byte[]) =
            use ms = new MemoryStream(data)
            deserialize ms

    let private cache = Symbol.Create "ComputeShaderCache"

    let toByteArray (program : ComputeProgram) =
        ComputeProgramData.pickle {
            shader    = program.Shader
            binary    = program.ShaderModule.SpirV
            groupSize = program.GroupSize
        }

    let private ofModule (glsl : GLSLShader) (groupSize : V3i) (module_ : ShaderModule) =
        let device = module_.Device

        let layout =
            device.CreatePipelineLayout(glsl.iface, 1, Set.empty)

        native {
            let! pMain = "main"

            let shaderInfo =
                VkPipelineShaderStageCreateInfo(
                    VkPipelineShaderStageCreateFlags.None,
                    VkShaderStageFlags.ComputeBit,
                    module_.Handle,
                    pMain,
                    NativePtr.zero
                )

            let! pPipelineInfo =
                VkComputePipelineCreateInfo(
                    VkPipelineCreateFlags.None,
                    shaderInfo,
                    layout.Handle,
                    VkPipeline.Null,
                    0
                )

            let! pPipeline = VkPipeline.Null
            VkRaw.vkCreateComputePipelines(device.Handle, VkPipelineCache.Null, 1u, pPipelineInfo, NativePtr.zero, pPipeline)
                |> check "could not create compute pipeline"

            return new ComputeProgram(device, !!pPipeline, layout, module_, groupSize, glsl)
        }

    let private ofByteArray (data : byte[]) (device : Device) =
        let data = ComputeProgramData.unpickle data

        let module_ =
            device.CreateShaderModule(FShade.ShaderSlot.Compute, data.binary)

        module_ |> ofModule data.shader data.groupSize

    let private tryRead (file : string) (device : Device) =
        try
            if File.Exists file then
                let data = File.ReadAllBytes file
                Some <| ofByteArray data device
            else
                None
        with exn ->
            Log.warn "[Vulkan] Failed to read from compute shader file cache '%s': %s" file exn.Message
            None

    let private write (file : string) (program : ComputeProgram) =
        try
            let data = toByteArray program
            data |> File.writeAllBytesSafe file
        with exn ->
            Log.warn "[Vulkan] Failed to write to shader program file cache '%s': %s" file exn.Message

    let private ofFShadeInternal (shader : FShade.ComputeShader) (device : Device) =
        let glsl =
            let backend = FShadeConfig.backend device
            try
                shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL backend
            with exn ->
                Log.error "%s" exn.Message
                reraise()

        if device.DebugConfig.PrintShaderCode then
            glsl.code |> ShaderCodeReporting.logLines "Compiling shader"

        let module_ = ShaderModule.ofGLSL FShade.ShaderSlot.Compute glsl device
        module_ |> ofModule glsl shader.csLocalSize

    let ofFShade (shader : FShade.ComputeShader) (device : Device) =
        device.GetCached(cache, shader, fun shader ->
            match device.ShaderCachePath with
            | Some shaderCachePath ->
                let fileName = ShaderProgram.FileCacheName.ofEffectId device false shader.csId
                let file = Path.Combine(shaderCachePath, fileName + ".compute")

                match tryRead file device with
                | Some loaded ->
                    if device.DebugConfig.VerifyShaderCacheIntegrity then
                        let temp = ofFShadeInternal shader device
                        let real = toByteArray loaded
                        let should = toByteArray temp
                        temp.Destroy()

                        if real <> should then
                            let tmp = Path.GetTempFileName()
                            let tmpReal = tmp + ".real"
                            let tmpShould = tmp + ".should"
                            File.writeAllBytesSafe tmpReal real
                            File.writeAllBytesSafe tmpShould should
                            failf "invalid cache for ComputeShader: real: %s vs. should: %s" tmpReal tmpShould
                    loaded
                | _ ->
                    let shader = ofFShadeInternal shader device
                    write file shader
                    shader

            | None ->
                ofFShadeInternal shader device
        )

    let ofFunction (f : 'a -> 'b) (device : Device) =
        let shader = FShade.ComputeShader.ofFunction (V3i device.PhysicalDevice.Limits.Compute.MaxWorkGroupSize) f
        ofFShade shader device


[<AutoOpen>]
module internal ComputeProgramDebugExtensions =
    open System
    open FSharp.Data.Adaptive

    type HookedComputeProgram(device : Device, input : aval<ComputeShader>) =
        inherit AdaptiveObject()

        let mutable shader = input.GetValue()
        let mutable program = ComputeProgram.ofFShade shader device
        let layout = program.PipelineLayout

        let validateLayout (next : PipelineLayout) =
            let mutable isValid = true

            let isUniformBufferCompatible (o : GLSLUniformBuffer) (n : GLSLUniformBuffer) =
                o.ubSet = n.ubSet &&
                o.ubBinding = n.ubBinding &&
                o.ubSize >= n.ubSize &&
                o.ubFields.Length >= n.ubFields.Length &&
                List.take n.ubFields.Length o.ubFields = n.ubFields

            let check (description : string) (getName : 'V -> string) (isCompatible : 'V -> 'V -> bool) (old : 'V list) (next : 'V list) =
                if old.Length <> next.Length then
                    Log.warn $"[Vulkan] {description} count has changed from {old.Length} to {next.Length}"
                    isValid <- false
                else
                    (old, next) ||> List.iter2 (fun ov nv ->
                        if not <| isCompatible ov nv then
                            let nl = Environment.NewLine
                            let name = getName nv
                            Log.warn $"[Vulkan] {description} '{name}'{nl}{nl}{nv}{nl}{nl}is incompatible with original interface{nl}{nl}{ov}{nl}"
                            isValid <- false
                    )

            let po, pn = layout.PipelineInfo, next.PipelineInfo
            (po.pImages,        pn.pImages)        ||> check "Image" (fun i -> i.imageName) (=)
            (po.pTextures,      pn.pTextures)      ||> check "Sampler" (fun s -> s.samplerName) (=)
            (po.pStorageBlocks, pn.pStorageBlocks) ||> check "Storage buffer" (fun b -> b.ssbName) (=)
            (po.pUniformBlocks, pn.pUniformBlocks) ||> check "Uniform buffer" (fun b -> b.ubName) isUniformBufferCompatible

            isValid

        member x.Interface = program.Shader.iface
        member x.GroupSize = program.GroupSize
        member x.Pipeline = program.Pipeline
        member x.PipelineLayout = layout

        member x.Update(token : AdaptiveToken) : bool =
            x.EvaluateIfNeeded token false (fun token ->
                let s = input.GetValue token

                if s <> shader then
                    let updated =
                        try
                            ValueSome <| ComputeProgram.ofFShade s device
                        with _ ->
                            ValueNone

                    match updated with
                    | ValueSome p when validateLayout p.PipelineLayout ->
                        program.Dispose()
                        program <- p
                        shader <- s
                        true

                    | ValueSome p ->
                        Log.warn "[Vulkan] Pipeline layout of compute shader has changed and is incompatible with original layout, ignoring..."
                        p.Dispose()
                        false

                    | _ ->
                        Log.warn "[Vulkan] Failed to update compute shader"
                        false
                else
                    false
            )

        member x.Dispose() =
            program.Dispose()

        interface IComputeShader with
            member x.Runtime = device.Runtime
            member x.LocalSize = x.GroupSize
            member x.Interface = x.Interface
            member x.Dispose() = x.Dispose()


    type IComputeShader with
        member inline x.Pipeline =
            match x with
            | :? ComputeProgram as p       -> p.Pipeline
            | :? HookedComputeProgram as p -> p.Pipeline
            | _ -> failf $"Invalid compute shader type {x.GetType()}"

        member inline x.PipelineLayout =
            match x with
            | :? ComputeProgram as p       -> p.PipelineLayout
            | :? HookedComputeProgram as p -> p.PipelineLayout
            | _ -> failf $"Invalid compute shader type {x.GetType()}"

    type Device with
        member x.CreateComputeShader(shader : FShade.ComputeShader) : IComputeShader =
            match ShaderDebugger.tryRegisterComputeShader shader with
            | Some hooked ->
                new HookedComputeProgram(x, hooked)

            | _ ->
                ComputeProgram.ofFShade shader x