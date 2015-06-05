namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Rendering

type private FragmentPtr = nativeint

[<Flags>]
type VMMode =
    | None                        = 0x00000
    | RuntimeRedundancyChecks     = 0x00001
    | RuntimeStateSorting         = 0x00002

type VMStats =
    struct
        val mutable public TotalInstructions : int
        val mutable public RemovedInstructions : int
    end

module GLVM =
    open System.Runtime.InteropServices
    open System.Runtime.CompilerServices

    [<Literal>]
    let lib = "glvm"

    [<DllImport(lib)>]
    extern void vmInit()

    [<DllImport(lib)>]
    extern FragmentPtr vmCreate()

    [<DllImport(lib)>]
    extern void vmDelete(FragmentPtr frag)

    [<DllImport(lib)>]
    extern bool vmHasNext(FragmentPtr frag)

    [<DllImport(lib)>]
    extern FragmentPtr vmGetNext(FragmentPtr frag)

    [<DllImport(lib)>]
    extern void vmLink(FragmentPtr left, FragmentPtr right)

    [<DllImport(lib)>]
    extern void vmUnlink(FragmentPtr left)


    [<DllImport(lib)>]
    extern int vmNewBlock(FragmentPtr left)

    [<DllImport(lib)>]
    extern void vmClearBlock(FragmentPtr left, int block)

    [<DllImport(lib)>]
    extern void vmAppend1(FragmentPtr left, int block, InstructionCode code, nativeint arg0)

    [<DllImport(lib)>]
    extern void vmAppend2(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1)

    [<DllImport(lib)>]
    extern void vmAppend3(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1, nativeint arg2)

    [<DllImport(lib)>]
    extern void vmAppend4(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1, nativeint arg2, nativeint arg3)

    [<DllImport(lib)>]
    extern void vmAppend5(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1, nativeint arg2, nativeint arg3, nativeint arg4)

    [<DllImport(lib)>]
    extern void vmClear(FragmentPtr frag)

    [<DllImport(lib)>]
    extern void vmRunSingle(FragmentPtr frag)

    [<DllImport(lib)>]
    extern void vmRun(FragmentPtr frag, VMMode mode, VMStats& stats)

[<AllowNullLiteral>]
type SwitchFragment(mode : VMMode) =
    let frag = GLVM.vmCreate()
    let mutable next : SwitchFragment = null
    let mutable prev : SwitchFragment = null

    let getArgs (o : Instruction) =
        o.Arguments |> Array.map (fun arg ->
            match arg with
                | :? int as i -> nativeint i
                | :? nativeint as i -> i
                | _ -> failwith "invalid argument"
        )

    let appendToBlock (id : int) (instructions : seq<Instruction>) =
        for i in instructions do
            match getArgs i with
                | [| a |] -> GLVM.vmAppend1(frag, id, i.Operation, a)
                | [| a; b |] -> GLVM.vmAppend2(frag, id, i.Operation, a, b)
                | [| a; b; c |] -> GLVM.vmAppend3(frag, id, i.Operation, a, b, c)
                | [| a; b; c; d |] -> GLVM.vmAppend4(frag, id, i.Operation, a, b, c, d)
                | [| a; b; c; d; e |] -> GLVM.vmAppend5(frag, id, i.Operation, a, b, c, d, e)
                | _ -> failwithf "invalid instruction: %A" i

    member x.NativeFragment = frag

    member x.Next
        with get() = next
        and set (v : SwitchFragment) = 
            if v <> null then GLVM.vmLink(frag, v.NativeFragment)
            else GLVM.vmUnlink(frag)
            next <- v

    member x.Prev
        with get() = prev
        and set (v : SwitchFragment) = 
            if v <> null then
                GLVM.vmLink(v.NativeFragment, frag)
            next <- v

    member x.Append(instructions : seq<Instruction>) =
        let id = GLVM.vmNewBlock(frag)
        appendToBlock id instructions
        id

    member x.Update(id : int, instructions : seq<Instruction>) =
        GLVM.vmClearBlock(frag, id)
        appendToBlock id instructions

    member x.Clear() =
        GLVM.vmClear(frag)

    member x.RunSelf() =
        GLVM.vmRunSingle frag

    member x.RunAll() =
        let mutable stats = VMStats()
        GLVM.vmRun(frag, mode, &stats)

    member x.Dispose() =
        GLVM.vmDelete frag
        next <- null
        prev <- null

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IDynamicFragment<SwitchFragment> with
        member x.Next
            with get() = x.Next
            and set v = x.Next <- v

        member x.Prev
            with get() = x.Prev
            and set v = x.Prev <- v

        member x.Append(i : seq<Instruction>) =
            x.Append i

        member x.Update (id : int) (value : seq<Instruction>) =
            x.Update(id, value)

        member x.Clear() =
            x.Clear()

        member x.RunAll() =
            let mutable stats = VMStats()
            GLVM.vmRun(frag, mode, &stats)

    new() = new SwitchFragment(VMMode.None)

[<AllowNullLiteral>]
type SwitchFragmentRedundancyRemoval() =
    let frag = GLVM.vmCreate()
    let mutable next : SwitchFragmentRedundancyRemoval = null
    let mutable prev : SwitchFragmentRedundancyRemoval = null
    let mutable stats = VMStats()

    let getArgs (o : Instruction) =
        o.Arguments |> Array.map (fun arg ->
            match arg with
                | :? int as i -> nativeint i
                | :? nativeint as i -> i
                | _ -> failwith "invalid argument"
        )

    let appendToBlock (id : int) (instructions : seq<Instruction>) =
        for i in instructions do
            match getArgs i with
                | [| a |] -> GLVM.vmAppend1(frag, id, i.Operation, a)
                | [| a; b |] -> GLVM.vmAppend2(frag, id, i.Operation, a, b)
                | [| a; b; c |] -> GLVM.vmAppend3(frag, id, i.Operation, a, b, c)
                | [| a; b; c; d |] -> GLVM.vmAppend4(frag, id, i.Operation, a, b, c, d)
                | [| a; b; c; d; e |] -> GLVM.vmAppend5(frag, id, i.Operation, a, b, c, d, e)
                | _ -> failwithf "invalid instruction: %A" i

    member x.NativeFragment = frag

    member x.Next
        with get() = next
        and set (v : SwitchFragmentRedundancyRemoval) = 
            if v <> null then GLVM.vmLink(frag, v.NativeFragment)
            else GLVM.vmUnlink(frag)
            next <- v

    member x.Prev
        with get() = prev
        and set (v : SwitchFragmentRedundancyRemoval) = 
            if v <> null then
                GLVM.vmLink(v.NativeFragment, frag)
            next <- v

    member x.Append(instructions : seq<Instruction>) =
        let id = GLVM.vmNewBlock(frag)
        appendToBlock id instructions
        id

    member x.Update(id : int, instructions : seq<Instruction>) =
        GLVM.vmClearBlock(frag, id)
        appendToBlock id instructions

    member x.Clear() =
        GLVM.vmClear(frag)

    member x.RunSelf() =
        GLVM.vmRunSingle frag

    member x.RunAll() =
        GLVM.vmRun(frag, VMMode.RuntimeRedundancyChecks, &stats)

    member x.Dispose() =
        GLVM.vmDelete frag
        next <- null
        prev <- null

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IDynamicFragment<SwitchFragmentRedundancyRemoval> with
        member x.Next
            with get() = x.Next
            and set v = x.Next <- v

        member x.Prev
            with get() = x.Prev
            and set v = x.Prev <- v

        member x.Append(i : seq<Instruction>) =
            x.Append i

        member x.Update (id : int) (value : seq<Instruction>) =
            x.Update(id, value)

        member x.Clear() =
            x.Clear()

        member x.RunAll() =
            GLVM.vmRun(frag, VMMode.RuntimeRedundancyChecks, &stats)
