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
        let outputs = signature.ColorAttachments |> Map.toList |> List.map (fun (i, (name, si)) -> string name, i) |> Map.ofList
        match ctx.TryCompileProgramCode(outputs, b.ExpectsRowMajorMatrices, b.Code) with
            | Success s ->
                let tryGetSamplerDescription (samplerName : string) (index : int) =
                    match b.Samplers.TryGetValue ((samplerName, index)) with
                        | (true, desc) -> Some desc
                        | _ -> None

                let builtIns (kind : FShade.Imperative.ParameterKind) =
                    b.BuiltIns 
                        |> Map.toSeq 
                        |> Seq.choose (fun (stage, vs) -> 
                            match Map.tryFind kind vs with
                                | Some vs -> Some(stage, vs)
                                | None -> None
                        )
                        |> Map.ofSeq
                        
                Success { 
                    s with 
                        TextureInfo = b.Samplers |> Dictionary.toMap  
                        Interface = 
                            { s.Interface with 
                                UsedBuiltInInputs = builtIns FShade.Imperative.ParameterKind.Input
                                UsedBuiltInOutputs = builtIns FShade.Imperative.ParameterKind.Output
                            }
                }

            | Error e -> 
                Error e

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
