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
        let glsl = shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSLVulkan

        if device.DebugConfig.PrintShaderCode then
            glsl.code |> ShaderCodeReporting.logLines "Compiling shader"

        let module_ = ShaderModule.ofGLSL FShade.ShaderSlot.Compute glsl device
        module_ |> ofModule glsl shader.csLocalSize

    let ofFShade (shader : FShade.ComputeShader) (device : Device) =
        device.GetCached(cache, shader, fun shader ->
            match device.ShaderCachePath with
            | Some shaderCachePath ->
                let fileName = ShaderProgram.FileCacheName.ofString shader.csId
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
        let shader = FShade.ComputeShader.ofFunction device.PhysicalDevice.Limits.Compute.MaxWorkGroupSize f
        ofFShade shader device


[<AutoOpen>]
module internal ComputeProgramDebugExtensions =
    open FSharp.Data.Adaptive

    type HookedComputeProgram(device : Device, input : aval<ComputeShader>) as this =
        inherit AdaptiveObject()

        let mutable current : ValueOption<ComputeShader * ComputeProgram * PipelineLayout> = ValueNone

        let map f =
            match current with
            | ValueSome (_, p, l) -> f (struct(p, l))
            | _ -> failf "HookedComputeProgram has invalid state"

        do this.Update AdaptiveToken.Top |> ignore

        member x.Interface =
            map (fun struct(p, _) -> p.Shader.iface)

        member x.GroupSize =
            map (fun struct(p, _) -> p.GroupSize)

        member x.Pipeline =
            map (fun struct(p, _) -> p.Pipeline)

        member x.PipelineLayout =
            map (fun struct(_, l) -> l)

        // Note: This is not thread safe, since multiple
        // compute tasks may use the same program concurrently.
        // This disposes the old program, while it may still be in use.
        member x.Update(token : AdaptiveToken) : bool =
            x.EvaluateIfNeeded token false (fun token ->
                let shader = input.GetValue token

                match current with
                | ValueNone ->
                    let program = ComputeProgram.ofFShade shader device

                    // The pipeline layout is static.
                    // Make sure it does not get disposed when the program changes.
                    program.PipelineLayout.AddReference()
                    current <- ValueSome (shader, program, program.PipelineLayout)

                    true

                | ValueSome (s, p, l) when s <> shader ->
                    let program = ComputeProgram.ofFShade shader device

                    if program.PipelineLayout.PipelineInfo <> l.PipelineInfo then
                        Log.warn "[Vulkan] Pipeline layout of compute shader has changed, ignoring..."
                        program.Dispose()
                        false

                    else
                        current <- ValueSome (shader, program, l)
                        p.Dispose()
                        true
                | _ ->
                    false
            )

        member x.Dispose() =
            match current with
            | ValueSome (_, p, l) ->
                p.Dispose()
                l.Dispose()
                current <- ValueNone

            | _ -> ()

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