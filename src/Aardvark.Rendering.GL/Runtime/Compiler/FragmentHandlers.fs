namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL


type IFragmentHandler<'f when 'f :> IDynamicFragment<'f>> =
    inherit IDisposable
    abstract member CreateProlog : unit -> 'f
    abstract member CreateEpilog : unit -> 'f
    abstract member Create : seq<Instruction> -> 'f
    abstract member Delete : 'f -> unit
    abstract member Compile : unit -> ('f -> unit)
    abstract member AdjustStatistics : FrameStatistics -> FrameStatistics

module FragmentHandlers =
    let native() =
        let manager = new MemoryManager()

        let prolog() =
            let f = new Fragment<unit>(manager, 0)
            f.Append (Assembler.functionProlog 6) |> ignore
            NativeDynamicFragment(f)

        let epilog() =
            let f = new Fragment<unit>(manager, 0)
            f.Append (Assembler.functionEpilog 6) |> ignore
            NativeDynamicFragment(f)

        let create (s : seq<Instruction>) =
            if not (Seq.isEmpty s) then
                failwith "cannot create non-empty fragment"

            let f = new Fragment<unit>(manager, 0)
            NativeDynamicFragment(f)

        { new IFragmentHandler<NativeDynamicFragment<unit>> with
            member x.Dispose() = manager.Dispose()
            member x.CreateProlog() = prolog()
            member x.CreateEpilog() = epilog()
            member x.Create s = create s
            member x.Delete f = f.Fragment.Dispose()
            member x.Compile() =
                let entryPtr = ref 0n
                let run = ref (fun () -> ())
                fun (f : NativeDynamicFragment<unit>) ->
                    let prolog = f.Fragment
                    if prolog.RealPointer <> !entryPtr then
                        entryPtr := prolog.RealPointer
                        run := UnmanagedFunctions.wrap !entryPtr
                    !run ()
            member x.AdjustStatistics s = s
        }

    let managed() =
        { new IFragmentHandler<ManagedDynamicFragment> with
            member x.Dispose() = ()
            member x.CreateProlog() = ManagedDynamicFragment()
            member x.CreateEpilog() = ManagedDynamicFragment()
            member x.Create s = ManagedDynamicFragment()
            member x.Delete f = f.Clear()
            member x.Compile() =
                fun (f : ManagedDynamicFragment) -> f.RunAll ()
            member x.AdjustStatistics s = s
        }

    let glvm() =
        { new IFragmentHandler<SwitchFragment> with
            member x.Dispose() = ()
            member x.CreateProlog() = new SwitchFragment()
            member x.CreateEpilog() = new SwitchFragment()
            member x.Create s = new SwitchFragment()
            member x.Delete f = f.Dispose()
            member x.Compile() =
                fun (f : SwitchFragment) -> f.RunAll ()
            member x.AdjustStatistics s = s
        }

    let glvmRuntimeRedundancyChecks() =
        let lastStats = ref (VMStats())
        { new IFragmentHandler<SwitchFragment> with
            member x.Dispose() = ()
            member x.CreateProlog() = new SwitchFragment()
            member x.CreateEpilog() = new SwitchFragment()
            member x.Create s = new SwitchFragment()
            member x.Delete f = f.Dispose()
            member x.Compile() =
                fun (f : SwitchFragment) -> 
                    lastStats := f.RunAll (VMMode.RuntimeRedundancyChecks)
            member x.AdjustStatistics s = 
                { s with ActiveInstructionCount = s.ActiveInstructionCount - float lastStats.Value.RemovedInstructions }
        }
