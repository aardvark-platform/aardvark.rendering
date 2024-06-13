namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open System

type ComputeProgram =
    {
        Program : Program
        LocalSize : V3i
    }

    member x.Context = x.Program.Context
    member x.Handle = x.Program.Handle
    member x.Interface = x.Program.Interface

    member x.Dispose() =
        x.Program.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IComputeShader with
        member x.LocalSize = x.LocalSize
        member x.Runtime = x.Context.Runtime :> IComputeRuntime
        member x.Interface = x.Interface

[<AutoOpen>]
module internal ComputeProgramExtensions =
    open FShade

    type Context with

        member x.TryCreateComputeProgram (shader : FShade.ComputeShader) =
            let compile() =
                let backend =
                    if x.Driver.glsl >= Version(4,3,0) then
                        glsl430
                    elif
                        x.Driver.extensions |> Set.contains "GL_ARB_compute_shader" &&
                        x.Driver.extensions |> Set.contains "GL_ARB_shading_language_420pack" &&
                        x.Driver.glsl >= Version(4,1,0) then
                        FShade.GLSL.Backend.Create
                            { glsl410.Config with
                                enabledExtensions =
                                    glsl410.Config.enabledExtensions
                                    |> Set.add "GL_ARB_compute_shader"
                                    |> Set.add "GL_ARB_shading_language_420pack"
                            }
                    else
                        failf "compute shader not supported: GLSL version = %A" x.Driver.glsl

                let glsl =
                    try
                        shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL backend
                    with exn ->
                        Log.error "%s" exn.Message
                        reraise()

                let adjust (s : GLSL.GLSLSampler) =
                    let textures =
                        List.init s.samplerCount (fun i ->
                            let texName =
                                match Map.tryFind (s.samplerName, i) shader.csTextureNames with
                                    | Some ti -> ti
                                    | _ -> s.samplerName
                            let samplerState =
                                match Map.tryFind (s.samplerName, i) shader.csSamplerStates with
                                    | Some sam -> sam
                                    | _ -> SamplerState.empty
                            texName, samplerState
                        )
                    { s with samplerTextures = textures }

                let iface = { glsl.iface with samplers = glsl.iface.samplers |> MapExt.map (constF adjust) }
                { glsl with iface = iface }

            let localSize =
                if shader.csLocalSize.AllGreater 0 then shader.csLocalSize
                else failf "compute shader has no local size"

            match x.TryGetOrCompileProgram(ShaderCacheKey.Compute shader.csId, compile) with
            | Success program ->
                let kernel = { Program = program; LocalSize = localSize }
                Success kernel

            | Error err ->
                Error err

        member x.CreateComputeProgram (shader : FShade.ComputeShader) : ComputeProgram =
            match x.TryCreateComputeProgram shader with
            | Success kernel ->
                kernel
            | Error err ->
                failf "shader compiler returned errors: %s" err

        member x.Delete(program : ComputeProgram) =
            program.Dispose()


[<AutoOpen>]
module internal ComputeProgramDebugExtensions =
    open FShade
    open FShade.GLSL
    open FSharp.Data.Adaptive

    type HookedComputeProgram(context : Context, input : aval<ComputeShader>) =
        inherit AdaptiveObject()

        let mutable shader = input.GetValue()
        let mutable program = context.CreateComputeProgram shader
        let layout = program.Interface

        let validateLayout (next : GLSLProgramInterface) =
            let mutable isValid = true

            let isUniformBufferCompatible (o : GLSLUniformBuffer) (n : GLSLUniformBuffer) =
                o.ubSet = n.ubSet &&
                o.ubBinding = n.ubBinding &&
                o.ubSize >= n.ubSize &&
                o.ubFields.Length >= n.ubFields.Length &&
                List.take n.ubFields.Length o.ubFields = n.ubFields

            let check (description : string) (isCompatible : 'V -> 'V -> bool) (old : MapExt<'K, 'V>) (next : MapExt<'K, 'V>) =
                for KeyValue(name, nv) in next do
                    match old |> MapExt.tryFind name with
                    | Some ov when not <| isCompatible ov nv ->
                        let nl = Environment.NewLine
                        Log.warn $"[GL] {description} '{name}'{nl}{nl}{nv}{nl}{nl}is incompatible with original interface{nl}{nl}{ov}{nl}"
                        isValid <- false

                    | None ->
                        Log.warn $"[GL] {description} '{name}' not found in original interface"
                        isValid <- false

                    | _ ->
                        ()

            (layout.images,         next.images)         ||> check "Image" (=)
            (layout.samplers,       next.samplers)       ||> check "Sampler" (=)
            (layout.storageBuffers, next.storageBuffers) ||> check "Storage buffer" (=)
            (layout.uniformBuffers, next.uniformBuffers) ||> check "Uniform buffer" isUniformBufferCompatible

            isValid

        member x.Handle = program.Handle
        member x.GroupSize = program.LocalSize

        member x.Update(token : AdaptiveToken) : bool =
            x.EvaluateIfNeeded token false (fun token ->
                let s = input.GetValue token

                if s <> shader then
                    let updated =
                        try
                            ValueSome <| context.CreateComputeProgram s
                        with _ ->
                            ValueNone

                    match updated with
                    | ValueSome p when validateLayout p.Interface ->
                        shader <- s
                        program <- p
                        true

                    | ValueSome _ ->
                        Log.warn "[GL] Interface of compute shader has changed and is incompatible with original interface, ignoring..."
                        false

                    | _ ->
                        Log.warn "[GL] Failed to update compute shader"
                        false
                else
                    false
            )

        interface IComputeShader with
            member x.Runtime = context.Runtime
            member x.LocalSize = x.GroupSize
            member x.Interface = layout
            member x.Dispose() = ()


    type IComputeShader with
        member inline x.Handle =
            match x with
            | :? ComputeProgram as p       -> p.Handle
            | :? HookedComputeProgram as p -> p.Handle
            | _ -> failf $"Invalid compute shader type {x.GetType()}"

    type Context with
        member x.CreateComputeShader(shader : FShade.ComputeShader) : IComputeShader =
            match ShaderDebugger.tryRegisterComputeShader shader with
            | Some hooked ->
                new HookedComputeProgram(x, hooked)

            | _ ->
                x.CreateComputeProgram shader