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
module ComputeProgramExtensions =
    open FShade

    type Context with

        // [<Obsolete>] ??
        member x.TryCompileKernel (id : string, code : string, iface : FShade.GLSL.GLSLProgramInterface, localSize : V3i) =
            x.TryCompileKernel(id, lazy((code, iface)), localSize)

        member x.TryCompileKernel (id : string, codeAndInterface : Lazy<string * FShade.GLSL.GLSLProgramInterface>, localSize : V3i) =
            use __ = x.ResourceLock

            match x.TryCompileComputeProgram(id, codeAndInterface) with
            | Success program ->
                let kernel = { Program = program; LocalSize = localSize }
                Success kernel

            | Error err ->
                Error err

        member x.TryCompileKernel (shader : FShade.ComputeShader) =
            let codeAndInterface =
                lazy (
                    let glsl =
                        if x.Driver.glsl >= Version(4,3,0) then
                            shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL430
                        elif
                            x.Driver.extensions |> Set.contains "GL_ARB_compute_shader" &&
                            x.Driver.extensions |> Set.contains "GL_ARB_shading_language_420pack" &&
                            x.Driver.glsl >= Version(4,1,0) then
                            let be =
                                FShade.GLSL.Backend.Create
                                    { glsl410.Config with
                                        enabledExtensions =
                                            glsl410.Config.enabledExtensions
                                            |> Set.add "GL_ARB_compute_shader"
                                            |> Set.add "GL_ARB_shading_language_420pack"
                                    }
                            shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL be
                        else
                            failf "compute shader not supported: GLSL version = %A" x.Driver.glsl

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
                    (glsl.code, iface)
                )

            let localSize =
                if shader.csLocalSize.AllGreater 0 then shader.csLocalSize
                else failf "compute shader has no local size"

            x.TryCompileKernel(shader.csId, codeAndInterface, localSize)

        member x.CompileKernel (shader : FShade.ComputeShader) : ComputeProgram =
            match x.TryCompileKernel shader with
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

    type HookedComputeProgram(context : Context, input : aval<ComputeShader>) as this =
        inherit AdaptiveObject()

        let mutable layout = Unchecked.defaultof<GLSLProgramInterface>
        let mutable current : ValueOption<ComputeShader * ComputeProgram> = ValueNone

        let map f =
            match current with
            | ValueSome (_, p) -> f p
            | _ -> failf "HookedComputeProgram has invalid state"

        do this.Update AdaptiveToken.Top |> ignore

        member x.Handle = map (fun p -> p.Handle)
        member x.GroupSize = map (fun p -> p.LocalSize)

        // Note: This is not thread safe, since multiple
        // compute tasks may use the same program concurrently.
        // This disposes the old program, while it may still be in use.
        member x.Update(token : AdaptiveToken) : bool =
            x.EvaluateIfNeeded token false (fun token ->
                let shader = input.GetValue token

                match current with
                | ValueNone ->
                    let program = context.CompileKernel shader
                    layout <- program.Interface
                    current <- ValueSome (shader, program)
                    true

                | ValueSome (s, p) when s <> shader ->
                    let program = context.CompileKernel shader

                    if program.Interface <> layout then
                        Log.warn "[GL] Interface of compute shader has changed, ignoring..."
                        program.Dispose()
                        false
                    else
                        current <- ValueSome (shader, program)
                        p.Dispose()
                        true

                | _ ->
                    false
            )

        member x.Dispose() =
            match current with
            | ValueSome (_, p) ->
                p.Dispose()
                current <- ValueNone

            | _ -> ()

        interface IComputeShader with
            member x.Runtime = context.Runtime
            member x.LocalSize = x.GroupSize
            member x.Interface = layout
            member x.Dispose() = x.Dispose()


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
                x.CompileKernel shader