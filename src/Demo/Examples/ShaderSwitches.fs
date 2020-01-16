namespace Examples


open System
open Aardvark.Base
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open FShade
open FShade.Imperative


module ShaderSignatureTest =
    
    let run() =
        
        let a = Effect.compose [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect ]
        let b = Effect.compose [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.simpleLighting |> toEffect ]
       
        let am = a |> Effect.toModule { EffectConfig.empty with outputs = Map.ofList ["Colors", (typeof<V4d>, 0)] }
        let bm = b |> Effect.toModule { EffectConfig.empty with outputs = Map.ofList ["Colors", (typeof<V4d>, 0)] }
        
        let s = EffectInputLayout.ofModule bm
        
        let bm' = bm // |> Module.withSignature s
        let am' = am |> EffectInputLayout.apply s
        let glslA = am' |> ModuleCompiler.compileGLSL410
        let glslB = bm' |> ModuleCompiler.compileGLSL410

        printfn "%s" glslA.code
        printfn ""
        printfn "%s" glslB.code
