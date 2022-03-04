namespace Aardvark.Assembler

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Runtime

#nowarn "9"

type internal FragmentProgramState<'a> =
    {
        differential : bool
        toWriteJump : System.Collections.Generic.HashSet<Fragment<'a>>
        toUpdate : System.Collections.Generic.HashSet<Fragment<'a>>
        manager : MemoryManager
        mutable prolog : Fragment<'a>
        mutable epilog : Fragment<'a>
    }

and FragmentProgram<'a> internal(differential : bool, compile : option<'a> -> 'a -> IAssemblerStream -> unit) =
    static let initialCapacity = 64n <<< 10
    static let config = 
        {
            MemoryManagerConfig.malloc = fun size -> JitMem.Alloc size
            MemoryManagerConfig.mfree = fun ptr size -> JitMem.Free(ptr, size)
            MemoryManagerConfig.mcopy = fun src dst size -> JitMem.Copy(src, dst, size)
        }
    
    static let toMemory (action : IAssemblerStream -> unit) : Memory<byte> =
        use ms = new SystemMemoryStream()
        use ass = AssemblerStream.create ms
        action ass
        ass.Jump 0
        ms.ToMemory()

    let compile = OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt compile
    let manager = new MemoryManager(initialCapacity, config)

    let wrapLock = obj()
    let mutable pAction = 0n
    let mutable entry = NativePtr.alloc<nativeint> 1
    let mutable action = Unchecked.defaultof<System.Action>

    let toUpdate = System.Collections.Generic.HashSet<Fragment<'a>>()
    let toWriteJump = System.Collections.Generic.HashSet<Fragment<'a>>()

    
    let state =
        {
            toWriteJump = toWriteJump
            toUpdate = toUpdate
            manager = manager
            differential = differential
            prolog = null
            epilog = null
        }

    let mutable first, last =
        let prolog = toMemory (fun ass -> ass.BeginFunction())
        let epilog = toMemory (fun ass -> ass.EndFunction(); ass.Ret())

        let pProlog = 
            let block = manager.Alloc(prolog.Length)
            JitMem.Copy(prolog, block)
            block
            
        let pEpilog = 
            let block = manager.Alloc(epilog.Length)
            JitMem.Copy(epilog, block)
            block

        let fProlog = new Fragment<'a>(state, Unchecked.defaultof<'a>, pProlog)
        let fEpilog = new Fragment<'a>(state, Unchecked.defaultof<'a>, pEpilog)
        state.prolog <- fProlog
        state.epilog <- fEpilog

        fProlog.Next <- fEpilog
        fEpilog.Prev <- fProlog
        fProlog.WriteJump()
        fProlog, fEpilog

    member x.InsertAfter(ref : Fragment<'a>, tag : 'a) =
        if not (isNull ref) && ref.IsDisposed then raise <| ObjectDisposedException "FragmentProgram.InsertAfter reference is disposed" 
        let mutable ref = ref
        let mutable prevTag = None

        if isNull ref then ref <- first
        else prevTag <- Some ref.Tag
        let next = ref.Next

        let code = 
            toMemory (fun s ->
                compile.Invoke(prevTag, tag, s)
                s.Jump(0)
            )

        let block = manager.Alloc(nativeint code.Length)
        JitMem.Copy(code, block)
        let frag = new Fragment<'a>(state, tag, block)

        frag.Next <- next
        frag.Prev <- ref
        next.Prev <- frag
        ref.Next <- frag
        toWriteJump.Add frag |> ignore
        toWriteJump.Add ref |> ignore

        if differential && not (Object.ReferenceEquals(next, last)) then
            toUpdate.Add(next) |> ignore

        frag

    member x.InsertBefore(ref : Fragment<'a>, tag : 'a) =
        if not (isNull ref) && ref.IsDisposed then raise <| ObjectDisposedException "FragmentProgram.InsertBefore reference is disposed" 
        let ref = if isNull ref then last else ref
        x.InsertAfter(ref.Prev, tag)

    member x.Append(tag : 'a) =
        x.InsertBefore(null, tag)

    member x.Prepend(tag : 'a) =
        x.InsertAfter(null, tag)

    member x.Prolog = first
    member x.Epilog = last

    member x.Dispose() =
        if not (isNull first) then
            first <- null
            last <- null
            manager.Dispose()
            action <- Unchecked.defaultof<_>
            pAction <- 0n
            NativePtr.free entry
            entry <- NativePtr.zero

    member x.Clear() =
        let mutable f = first.Next
        while not (Object.ReferenceEquals(f, last)) do
            let n = f.Next
            f.Dispose()
            f <- n

    member x.Update() =
        for u in toUpdate do u.Update(compile)
        toUpdate.Clear()

        for j in toWriteJump do j.WriteJump()
        toWriteJump.Clear()

        lock wrapLock (fun () ->
            let ptr = manager.Pointer + first.Offset
            if ptr <> pAction then
                NativePtr.write entry ptr
                pAction <- ptr
                action <- Marshal.GetDelegateForFunctionPointer<System.Action>(ptr)
        )


    member x.Run() =
        x.Update()
        action.Invoke()

    member x.EntryPointer = entry

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new(compile : option<'a> -> 'a -> IAssemblerStream -> unit) = new FragmentProgram<'a>(true, compile)
    new(compile : 'a -> IAssemblerStream -> unit) = new FragmentProgram<'a>(false, fun _ t s -> compile t s)

and [<AllowNullLiteral>] Fragment<'a> internal(state : FragmentProgramState<'a>, tag : 'a, ptr : managedptr) =
    static let toMemory (action : System.IO.Stream -> IAssemblerStream -> unit) : Memory<byte> =
        use ms = new SystemMemoryStream()
        use ass = AssemblerStream.create ms
        action ms ass
        ass.Jump 0
        ms.ToMemory()

    let mutable ptr = ptr
    let mutable prev : Fragment<'a> = null
    let mutable next : Fragment<'a> = null



    let writeJump(offset : int) =  
        let code = 
            use ms = new SystemMemoryStream()
            use ass = AssemblerStream.create ms
            ass.Jump offset
            ms.ToMemory()

        JitMem.Copy(code, ptr, ptr.Size - nativeint code.Length)


    member x.Prev
        with get() : Fragment<'a> = prev
        and internal set (p : Fragment<'a>) = prev <- p

    member x.Next
        with get() : Fragment<'a> = next
        and internal set (n : Fragment<'a>) = next <- n

    member x.IsDisposed : bool = ptr.Free

    member x.Offset : nativeint = ptr.Offset

    member x.WriteJump() : unit =
        if isNull next then 
            writeJump 0
        else 
            let ref = ptr.Offset + ptr.Size
            writeJump (int (next.Offset - ref))

    member private x.Write(data : Memory<byte>) =
        let size = nativeint data.Length
        if size = ptr.Size then
            JitMem.Copy(data, ptr)
        else
            let old = ptr
            let n = state.manager.Alloc(size)
            JitMem.Copy(data, n)
            ptr <- n
            if not (isNull prev) then state.toWriteJump.Add prev |> ignore
            state.manager.Free old

        state.toWriteJump.Add x |> ignore
            
    member x.Tag : 'a = tag

    member internal x.Update(compile : OptimizedClosures.FSharpFunc<option<'a>, 'a, IAssemblerStream, unit>) : unit =
        let prevTag = 
            if Object.ReferenceEquals(prev, state.prolog) then None
            else Some prev.Tag

        let code = 
            toMemory (fun s ass -> 
                compile.Invoke(prevTag, tag, ass)
                ass.Jump(0)
            )

        x.Write code


    member x.Dispose() : unit =
        let p = prev
        let n = next

        if not (isNull n) then n.Prev <- p
        if not (isNull p) then p.Next <- n

        state.manager.Free ptr
        if not (isNull p) then state.toWriteJump.Add p |> ignore
        if state.differential then
            state.toUpdate.Remove x |> ignore
            if not (isNull n) && not (Object.ReferenceEquals(n, state.epilog)) then state.toUpdate.Add n |> ignore

        state.toWriteJump.Remove x |> ignore
        prev <- null
        next <- null
        


