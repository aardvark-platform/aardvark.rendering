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

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmInit()

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern FragmentPtr vmCreate()

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmDelete(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern bool vmHasNext(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern FragmentPtr vmGetNext(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmLink(FragmentPtr left, FragmentPtr right)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmUnlink(FragmentPtr left)


    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern int vmNewBlock(FragmentPtr left)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmClearBlock(FragmentPtr left, int block)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmAppend1(FragmentPtr left, int block, InstructionCode code, nativeint arg0)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmAppend2(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmAppend3(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1, nativeint arg2)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmAppend4(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1, nativeint arg2, nativeint arg3)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmAppend5(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1, nativeint arg2, nativeint arg3, nativeint arg4)
    
    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmAppend6(FragmentPtr left, int block, InstructionCode code, nativeint arg0, nativeint arg1, nativeint arg2, nativeint arg3, nativeint arg4, nativeint arg5)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmClear(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmRunSingle(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern void vmRun(FragmentPtr frag, VMMode mode, VMStats& stats)

