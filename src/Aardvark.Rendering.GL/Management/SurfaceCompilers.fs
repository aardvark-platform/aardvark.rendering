namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL


module SurfaceCompilers =
    open System
    open System.Collections.Generic

    let compilers = Dictionary<Type,Context -> IFramebufferSignature -> ISurface -> Error<Program>>()

    let registerShaderCompiler (compiler : Context -> IFramebufferSignature -> 'a -> Error<Program>) =
        compilers.Add ( typeof<'a>, fun ctx signature s -> compiler ctx signature (unbox<'a> s) )

    let compileBackendSurface (ctx : Context) (signature : IFramebufferSignature) (b : BackendSurface) =
        match ctx.TryCompileProgram(signature, b.ExpectsRowMajorMatrices, b.Code) with
            | Success s ->
                let tryGetSamplerDescription (samplerName : string) (index : int) =
                    match b.Samplers.TryGetValue ((samplerName, index)) with
                        | (true, desc) -> Some desc
                        | _ -> None

                let ub = s.UniformBlocks // |> List.map (fun b -> { b with fields = b.fields |> List.map (fun f -> { f with semantic = remapSemantic f.semantic }) })
                let topLevelUniforms = 
                    s.Uniforms |> List.map (fun f -> 
                        match tryGetSamplerDescription f.name f.index with
                            | Some sampler -> 
                                { f with semantic = string sampler.textureName; samplerState = Some sampler.samplerState }
                            | None -> 
                                f
                    )

                Success { s with UniformBlocks = ub; Uniforms = topLevelUniforms; UniformGetters = b.Uniforms }

            | Error e -> Error e

    do registerShaderCompiler compileBackendSurface

    do registerShaderCompiler (fun (ctx : Context) (signature : IFramebufferSignature) (g : IGeneratedSurface) -> 
        let b = g.Generate(ctx.Runtime, signature)
        compileBackendSurface ctx signature b
        )

    let compile (ctx : Context) (signature : IFramebufferSignature) (s : ISurface) =   
        match s with
            | :? SignaturelessBackendSurface as s -> 
                s.Get signature |> unbox<Program> |> Success

            | _ -> 
                match compilers |> Seq.tryPick (fun ( KeyValue(k,v) ) -> 
                    if k.IsAssignableFrom (s.GetType()) then Some <| v ctx signature s
                    else None) with
                    | Some k -> k
                    | None -> Error "Unknown surface type. "
