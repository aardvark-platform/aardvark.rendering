namespace HeapSpike

// Scoping the bindless-buffer route: dump the GLSL FShade emits for a storage
// buffer ARRAY (V4f[][]) indexed by a per-draw handle, i.e. vertex-pulling from
// one of many GPU buffers referenced by handle. Proves the FShade primitive.

open Aardvark.Base
open FShade
open FShade.GLSL

module GlslDump =

    type UniformScope with
        // array of storage buffers (bindless): Geom[handle].data[i]
        member x.Geom : V4f[][] = uniform?StorageBuffer?Geom

    type V = { [<Position>] pos : V4f }

    let private shade (v : V) =
        vertex {
            let h : int = uniform?Handle
            let i : int = uniform?Idx
            return { v with pos = uniform.Geom.[h].[i] }
        }

    let private frag (v : V) =
        fragment { return V4f.IIII }

    let run () =
        let e = Effect.compose [ Effect.ofFunction shade; Effect.ofFunction frag ]
        let m =
            e |> Effect.toModule {
                depthRange = Range1f(-1.0f, 1.0f)
                flipHandedness = false
                lastStage = ShaderStage.Fragment
                outputs = Map.ofList [ "Colors", (typeof<V4f>, 0) ]
            }
        let glsl = ModuleCompiler.compileGLSLVulkan m
        printfn "=== GLSL (bindless V4f[][] storage buffer) ===\n%s\n=== END ===" glsl.code
