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

    [<GLSLIntrinsic("gl_DrawID", "GL_ARB_shader_draw_parameters")>]
    let private getDrawId () : int = onlyInShaderCode "getDrawId"

    let private vidExpr : Expr = Expr.ReadInput<int>(ParameterKind.Input, Intrinsics.VertexId)
    let private pullPositions : Expr = <@@ let h = getDrawId() in (uniform.HeapPositions.[h].[ uniform.HeapIndex.[h].[ (%%vidExpr : int) ] ]).XYZ @@>
    let private pullNormals   : Expr = <@@ let h = getDrawId() in (uniform.HeapNormals.[h].[ uniform.HeapIndex.[h].[ (%%vidExpr : int) ] ]).XYZ @@>

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
