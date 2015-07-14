namespace Aardvark.Rendering.GL

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering

type MetaInstruction =
    | FixedInstruction of list<Instruction>
    | AdaptiveInstruction of IMod<list<Instruction>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MetaInstruction =
    let private noMod = Mod.constant ()

    let single (i : Instruction) =
        FixedInstruction [i]

    let ofList (l : list<Instruction>) =
        FixedInstruction l

    let ofModList (l : IMod<list<Instruction>>) =
        if l.IsConstant then
            FixedInstruction (Mod.force l)
        else
            AdaptiveInstruction l

    let ofMod (l : IMod<Instruction>) =
        if l.IsConstant then
            FixedInstruction [Mod.force l]
        else
            AdaptiveInstruction (Mod.map (fun i -> [i]) l)

    let appendTo (i : MetaInstruction) (f : IDynamicFragment) =
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

