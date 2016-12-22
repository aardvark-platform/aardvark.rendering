namespace Aardvark.Rendering.GL.Compiler

#nowarn "9"

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL
open Microsoft.FSharp.NativeInterop

type MetaInstruction = IMod<list<Instruction>>


type CompilerInfo =
    {
        //stats : ref<FrameStatistics>
        contextHandle : nativeptr<nativeint>
        runtimeStats : nativeptr<V2i>
        currentContext : IMod<ContextHandle>
        drawBuffers : nativeint
        drawBufferCount : int
        
        structuralChange        : IMod<unit>
        usedTextureSlots        : ref<RefSet<int>>
        usedUniformBufferSlots  : ref<RefSet<int>>

    }


type CompilerState =
    {
        runtimeStats        : nativeptr<V2i>
        info                : CompilerInfo
        instructions        : list<MetaInstruction>
        disposeActions      : list<unit -> unit>
    }

type Compiled<'a> = { runCompile : CompilerState -> CompilerState * 'a }

[<AutoOpen>]
module ``Compiled Builder`` =
    let compilerState = { runCompile = fun s -> s, s }

    let useTextureSlot (slot : int) =
        { runCompile = fun s ->
            let slots = s.info.usedTextureSlots
            slots.Value <- RefSet.add slot slots.Value 

            let remove () =
                slots.Value <- RefSet.remove slot slots.Value 

            { s with disposeActions = remove :: s.disposeActions }, ()
        }

    let useUniformBufferSlot (slot : int) =
        { runCompile = fun s ->
            let slots = s.info.usedUniformBufferSlots
            slots.Value <- RefSet.add slot slots.Value 

            let remove () =
                slots.Value <- RefSet.remove slot slots.Value 

            { s with disposeActions = remove :: s.disposeActions }, ()
        }

    type CompiledBuilder() =
    
        member x.Bind(m : Compiled<'a>, f : 'a -> Compiled<'b>) =
            { runCompile  = fun s ->
                let (s,v) = m.runCompile s
                (f v).runCompile s
            }


        member x.Yield(i : MetaInstruction) =
            { runCompile = fun s -> {s with instructions = s.instructions @ [i]}, ()}


        member x.Yield(m : list<Instruction>) =
            { runCompile = fun s -> {s with instructions = s.instructions @ [Mod.constant m]}, ()}

//        member x.Yield(m : IMod<ContextHandle -> list<Instruction>>) =
//            { runCompile = fun s ->
//                let i = Mod.map2 (fun f ctx -> f ctx) m s.info.currentContext
//                { s with instructions = s.instructions @ [i] }, ()
//            }

        member x.Yield(m : IMod<Instruction>) =
            m |> Mod.map (fun i -> [i]) |> x.Yield



        member x.Yield(m : Instruction) =
            { runCompile = fun s -> {s with instructions = s.instructions @ [Mod.constant [m]]}, ()}

        member x.Return(u : unit) =
            { runCompile = fun s ->
                s,()
            }

//        member x.Yield(m : IMod<ContextHandle -> Instruction>) =
//            { runCompile = fun s ->
//                let i = Mod.map2 (fun f ctx -> [f ctx]) m s.info.currentContext
//                { s with instructions = s.instructions @ [i] }, ()
//            }

        member x.Zero() = { runCompile = fun s -> s, () }

        member x.Combine(l : Compiled<unit>, r : Compiled<'a>) =
            { runCompile = fun s ->
                let (s,()) = l.runCompile s
                r.runCompile s
            }

        member x.Delay(f : unit -> Compiled<'a>) =
            { runCompile = fun s -> (f ()).runCompile s }

        member x.For(seq : seq<'a>, f : 'a -> Compiled<unit>) =
            { runCompile = fun s ->
                let mutable c = s
                for e in seq do
                    let (s,()) = (f e).runCompile c
                    c <- s
                c, ()
            }


    let compiled = CompiledBuilder()
