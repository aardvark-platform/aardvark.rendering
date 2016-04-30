namespace Aardvark.Rendering.GL.Compiler

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

type MetaInstruction = IMod<list<Instruction>>

type CompilerState =
    {
        manager             : ResourceManager
        currentContext      : IMod<ContextHandle>
        instructions        : list<MetaInstruction>
    }

type Compiled<'a> = { runCompile : CompilerState -> CompilerState * 'a }

[<AutoOpen>]
module ``Compiled Builder`` =
    type CompiledBuilder() =
    
        member x.Bind(m : Compiled<'a>, f : 'a -> Compiled<'b>) =
            { runCompile  = fun s ->
                let (s,v) = m.runCompile s
                (f v).runCompile s
            }


        member x.Yield(i : MetaInstruction) =
            { runCompile = fun s -> {s with instructions = s.instructions @ [i]}, ()}


        member x.Yield(m : list<Instruction>) =
            m |> Mod.constant |> x.Yield

        member x.Yield(m : IMod<ContextHandle -> list<Instruction>>) =
            { runCompile = fun s ->
                let i = Mod.map2 (fun f ctx -> f ctx) m s.currentContext
                { s with instructions = s.instructions @ [i] }, ()
            }

        member x.Yield(m : IMod<Instruction>) =
            m |> Mod.map (fun i -> [i]) |> x.Yield

        member x.Yield(m : Instruction) =
            [m] |> Mod.constant |> x.Yield

        member x.Return(u : unit) =
            { runCompile = fun s ->
                s,()
            }

        member x.Yield(m : IMod<ContextHandle -> Instruction>) =
            { runCompile = fun s ->
                let i = Mod.map2 (fun f ctx -> [f ctx]) m s.currentContext
                { s with instructions = s.instructions @ [i] }, ()
            }

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
