namespace Aardvark.Rendering.GL

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering

type AdaptiveInstruction =
    | FixedInstruction of list<Instruction>
    | AdaptiveInstruction of IMod<list<Instruction>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveInstruction =
    let private noMod = Mod.constant ()

    let create (l : list<Instruction>) =
        FixedInstruction l

    let ofMod (l : IMod<list<Instruction>>) =
        if l.IsConstant then
            FixedInstruction (Mod.force l)
        else
            AdaptiveInstruction l

    let writeTo (i : AdaptiveInstruction) (f : IDynamicFragment<'f>) =
        match i with
            | FixedInstruction l -> 
                f.Append l |> ignore
                noMod
            | AdaptiveInstruction l ->
                let id = ref -1
                let res =
                    l |> Mod.map (fun l ->
                        if !id >= 0 then
                            f.Update !id l
                        else
                            id := f.Append l
                    )
                
                res |> Mod.force
                res



type CompilerState =
    {
        currentContext : IMod<ContextHandle>
        instructions : list<AdaptiveInstruction>
        resources : list<IChangeableResource>
        manager : ResourceManager
    }

type Compiled<'a> = { runCompile : CompilerState -> CompilerState * 'a }

module Compiled =
    
    let manager = { runCompile = fun s -> s,s.manager }

[<AutoOpen>]
module ``Compiled Builder`` =
    type CompiledBuilder() =
    
        member x.Bind(m : Compiled<'a>, f : 'a -> Compiled<'b>) =
            { runCompile  = fun s ->
                let (s,v) = m.runCompile s
                (f v).runCompile s
            }

        member x.Bind(r : 'a, f : 'a -> Compiled<'b>) =
            { runCompile = fun s ->
                (f r).runCompile { s with resources = (r :> IChangeableResource)::s.resources }
            }

        member x.Yield(i : AdaptiveInstruction) =
            { runCompile = fun s -> {s with instructions = s.instructions @ [i]}, ()}

        member x.Yield(m : IMod<list<Instruction>>) =
            m |> AdaptiveInstruction.ofMod |> x.Yield

        member x.Yield(m : list<Instruction>) =
            m |> AdaptiveInstruction.create |> x.Yield

        member x.Yield(m : ContextHandle -> list<Instruction>) =
            { runCompile = fun s ->
                let i = s.currentContext |> Mod.map m |> AdaptiveInstruction.ofMod
                { s with instructions = s.instructions @ [i] }, ()
            }

        member x.Yield(m : IMod<Instruction>) =
            m |> Mod.map (fun i -> [i]) |> AdaptiveInstruction.ofMod |> x.Yield

        member x.Yield(m : Instruction) =
            [m] |> AdaptiveInstruction.create |> x.Yield

        member x.Yield(m : ContextHandle -> Instruction) =
            { runCompile = fun s ->
                let i = s.currentContext |> Mod.map (fun v -> [m v]) |> AdaptiveInstruction.ofMod
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
