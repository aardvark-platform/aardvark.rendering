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
    open System.Security

    [<Literal>]
    let lib = "glvm"
    
    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void hglCleanup(void* ctx)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmInit()

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern FragmentPtr vmCreate()

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmDelete(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern bool vmHasNext(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern FragmentPtr vmGetNext(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmLink(FragmentPtr left, FragmentPtr right)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmUnlink(FragmentPtr left)


    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern int vmNewBlock(FragmentPtr left)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmClearBlock(FragmentPtr left, int block)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmAppend1(FragmentPtr left, int block, int code, nativeint arg0)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmAppend2(FragmentPtr left, int block, int code, nativeint arg0, nativeint arg1)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmAppend3(FragmentPtr left, int block, int code, nativeint arg0, nativeint arg1, nativeint arg2)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmAppend4(FragmentPtr left, int block, int code, nativeint arg0, nativeint arg1, nativeint arg2, nativeint arg3)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmAppend5(FragmentPtr left, int block, int code, nativeint arg0, nativeint arg1, nativeint arg2, nativeint arg3, nativeint arg4)
    
    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmAppend6(FragmentPtr left, int block, int code, nativeint arg0, nativeint arg1, nativeint arg2, nativeint arg3, nativeint arg4, nativeint arg5)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmClear(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmRunSingle(FragmentPtr frag)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl); SuppressUnmanagedCodeSecurity>]
    extern void vmRun(FragmentPtr frag, VMMode mode, VMStats& stats)

