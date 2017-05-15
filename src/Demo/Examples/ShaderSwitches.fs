namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open FShade
open FShade.Imperative

type EffectSignature =
    {
        inputs : Map<string, Type>
        outputs : Map<string, Type>
        uniforms : Map<string, Type>
        uniformBuffers : Map<string, list<string * Type>>
    }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EffectSignature =

    let ofModule (m : Module) =

        let mutable inputs = Map.empty
        let mutable outputs = Map.empty
        let mutable uniforms = Map.empty
        let mutable uniformBuffers = Map.empty

        for e in m.entries do
            for u in e.uniforms do
                match u.uniformBuffer with
                    | Some b -> 
                        let o = Map.tryFind b uniformBuffers |> Option.defaultValue Map.empty
                        uniformBuffers <- Map.add b (Map.add u.uniformName u.uniformType o) uniformBuffers
                    | None ->
                        uniforms <- Map.add u.uniformName u.uniformType uniforms

            let stage = e.decorations |> List.pick (function EntryDecoration.Stages { self = stage } -> Some stage | _ -> None)

            match stage with
                | ShaderStage.Vertex ->
                    for p in e.inputs do
                        inputs <- Map.add p.paramSemantic p.paramType inputs
                        
                | ShaderStage.Fragment ->
                    for p in e.outputs do
                        outputs <- Map.add p.paramSemantic p.paramType outputs
                | _ -> 
                    ()

        {
            inputs = inputs
            outputs = outputs
            uniforms = uniforms 
            uniformBuffers = uniformBuffers |> Map.map (fun _ -> Map.toList)
        }

    let ofEffect (signature : IFramebufferSignature) (e : Effect) =
        let m = signature.Link(e, Range1d(-1.0, 1.0), false)
        ofModule m

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Module =
    let withSignature (signature : EffectSignature) (m : Module) =
        
        let entries = 
            m.entries |> List.map (fun entry ->
                let mutable oldUniforms = entry.uniforms |> List.map (fun u -> u.uniformName, u) |> Map.ofList

                let newBufferUniforms =
                    signature.uniformBuffers |> Map.toList |> List.collect (fun (b,u) ->
                        u |> List.map (fun (n,t) -> 
                            match Map.tryFind n oldUniforms with
                                | Some u -> 
                                    oldUniforms <- Map.remove n oldUniforms
                                    n, { u with uniformBuffer = Some b }
                                | None -> 
                                    n, { uniformName = n; uniformType = t; uniformDecorations = []; uniformBuffer = Some b }
                        )
                    )
                    |> Map.ofList

                let newUniforms =
                    signature.uniforms |> Map.map (fun n t ->
                        match Map.tryFind n oldUniforms with
                            | Some u ->
                                oldUniforms <- Map.remove n oldUniforms
                                { u with uniformBuffer = None }
                            | None ->
                                { uniformName = n; uniformType = t; uniformDecorations = []; uniformBuffer = None }
                                
                    )
                    
                if not (Map.isEmpty oldUniforms) then
                    let all = oldUniforms |> Map.toSeq |> Seq.map snd |> Seq.toList
                    failwithf "[FShade] cannot apply signature to Effect (requesting unknown uniforms %A)" all

                let allUniforms = Map.union newBufferUniforms newUniforms |> Map.toList |> List.map snd

                let entry = { entry with uniforms = allUniforms }

                let stage = entry.decorations |> List.pick (function EntryDecoration.Stages { self = stage } -> Some stage | _ -> None)
            
                match stage with
                    | ShaderStage.Vertex ->
                        let oldInputs = entry.inputs |> List.map (fun p -> p.paramSemantic, p) |> Map.ofList
                        let newInputs = 
                            signature.inputs 
                                |> Map.toList
                                |> List.map (fun (name,t) ->
                                    match Map.tryFind name oldInputs with
                                        | Some ip -> ip
                                        | None -> { paramType = t; paramSemantic = name; paramName = name; paramDecorations = Set.empty }
                                )

                        { entry with inputs = newInputs }

                    | ShaderStage.Fragment ->
                        let oldOutputs = entry.outputs |> List.map (fun p -> p.paramSemantic, p) |> Map.ofList
                        let newOutputs = 
                            signature.outputs 
                                |> Map.toList
                                |> List.map (fun (name,t) ->
                                    match Map.tryFind name oldOutputs with
                                        | Some ip -> ip
                                        | None -> { paramType = t; paramSemantic = name; paramName = name; paramDecorations = Set.empty }
                                )

                        { entry with outputs = newOutputs }

                    | _ ->
                        entry
            )

        { m with entries = entries }


module ShaderSignatureTest =
    
    let run() =
        
        let a = Effect.compose [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect ]
        let b = Effect.compose [ DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.simpleLighting |> toEffect ]
       
        let am = a |> Effect.toModule { EffectConfig.empty with outputs = Map.ofList ["Colors", (typeof<V4d>, 0)] }
        let bm = b |> Effect.toModule { EffectConfig.empty with outputs = Map.ofList ["Colors", (typeof<V4d>, 0)] }
        
        let s = EffectSignature.ofModule bm
        
        let bm' = bm // |> Module.withSignature s
        let am' = am |> Module.withSignature s
        let glslA = am' |> ModuleCompiler.compileGLSL410
        let glslB = bm' |> ModuleCompiler.compileGLSL410

        printfn "%s" glslA
        printfn ""
        printfn "%s" glslB
