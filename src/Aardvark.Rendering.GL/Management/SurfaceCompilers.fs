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
                let remapSemantic (sem : string) =
                    match b.SemanticMap.TryGetValue (Sym.ofString sem) with
                        | (true, sem) -> sem.ToString()
                        | _ -> sem

                    
                let getSamplerState (sem : string) =
                    match b.SamplerStates.ContainsKey (Sym.ofString sem) with
                        | true -> Some sem
                        | _ -> None

                let ub = s.UniformBlocks |> List.map (fun b -> { b with fields = b.fields |> List.map (fun f -> { f with semantic = remapSemantic f.semantic }) })
                let u = s.Uniforms |> List.map (fun f -> let sem = remapSemantic f.semantic in { f with semantic = sem; samplerState = getSamplerState sem})

                let uniformGetters =
                    b.Uniforms 
                        |> SymDict.toSeq 
                        |> Seq.map (fun (k,v) -> (k, v :> obj)) 
                        |> SymDict.ofSeq

                Success { s with UniformBlocks = ub; Uniforms = u; SamplerStates = b.SamplerStates; UniformGetters = uniformGetters }

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
