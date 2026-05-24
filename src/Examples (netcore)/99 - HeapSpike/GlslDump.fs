namespace HeapSpike

// Dump the REWRITTEN bindless vertex-pull GLSL for the BH effect (Positions/Normals
// inputs rewritten to HeapPositions[drawId].data[HeapIndex[drawId].data[vid]]), to
// diagnose the heap bindless path.

open Aardvark.Base
open Aardvark.SceneGraph
open FShade
open FShade.GLSL
open FShade.Imperative
open Microsoft.FSharp.Quotations

module GlslDump =

    let private vidExpr    : Expr = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.VertexId)
    let private handleExpr : Expr = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.InstanceId)
    let private pullPositions : Expr = <@@ let h = (%%handleExpr : int) in (uniform.HeapPositions.[h].[ (%%vidExpr : int) ]).XYZ @@>
    let private pullNormals   : Expr = <@@ let h = (%%handleExpr : int) in (uniform.HeapNormals.[h].[ (%%vidExpr : int) ]).XYZ @@>

    let run () =
        let e =
            Effect.compose [ Effect.ofFunction Golden.BH.shade; Effect.ofFunction Golden.BH.frag ]
            |> Effect.map (fun s ->
                s |> Shader.substituteReads (fun kind _ name _ _ ->
                    match kind, name with
                    | ParameterKind.Input, "Positions" -> Some pullPositions
                    | ParameterKind.Input, "Normals"   -> Some pullNormals
                    | _ -> None))
        let m =
            e |> Effect.toModule {
                depthRange = Range1f(-1.0f, 1.0f)
                flipHandedness = false
                lastStage = ShaderStage.Fragment
                outputs = Map.ofList [ "Colors", (typeof<V4f>, 0) ]
            }
        let glsl = ModuleCompiler.compileGLSLVulkan m
        printfn "=== REWRITTEN bindless GLSL ===\n%s\n=== END ===" glsl.code
        glsl.iface.storageBuffers |> Seq.iter (fun kv ->
            printfn "=== IFACE: SSB '%s' ssbCount=%d" kv.Key kv.Value.ssbCount)

        // for comparison: the CLEAN hand-written pull shader (SAvp) that DOES render
        let mClean =
            Effect.compose [ Effect.ofFunction Golden.SAvp.shade; Effect.ofFunction Golden.SAvp.frag ]
            |> Effect.toModule {
                depthRange = Range1f(-1.0f, 1.0f)
                flipHandedness = false
                lastStage = ShaderStage.Fragment
                outputs = Map.ofList [ "Colors", (typeof<V4f>, 0) ]
            }
        let glslClean = ModuleCompiler.compileGLSLVulkan mClean
        printfn "=== CLEAN SAvp GLSL (works) ===\n%s\n=== END ===" glslClean.code
        glslClean.iface.storageBuffers |> Seq.iter (fun kv ->
            printfn "=== CLEAN IFACE: SSB '%s' ssbCount=%d" kv.Key kv.Value.ssbCount)
