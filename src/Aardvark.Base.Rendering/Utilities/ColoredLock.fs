namespace Aardvark.Base

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open FSharp.Data.Adaptive

type ColoredLockStatus<'a> =
    | Exclusive
    | Colored of 'a
    | NotEntered

type private ColoredLockLocalState<'a> =
    class
        val mutable public Count : int
        val mutable public Stash : list<int * 'a>

        new() = { Count = 0; Stash = [] }
    end

type ColoredLock<'a when 'a : equality and 'a : comparison>() =
    let mutable color = Unchecked.defaultof<'a>
    let mutable count = 0
    let lockObj = obj()
    let local = new ThreadLocal<ColoredLockLocalState<'a>>(fun _ -> ColoredLockLocalState())

    member __.Status =
        if Monitor.IsEntered lockObj then Exclusive
        else
            let local = local.Value
            if local.Count > 0 then Colored color
            else NotEntered

    member __.HasExclusiveLock =
        Monitor.IsEntered lockObj

    member __.Enter(onLock : Option<'a> -> unit) =
        let local = local.Value

        if Monitor.IsEntered lockObj then
            count <- count - 1
        else
            Monitor.Enter lockObj

            if local.Count > 0 then
                // give away all readers acquired by the current thread
                count <- count - local.Count
                local.Stash <- (local.Count, color) :: local.Stash
                local.Count <- 0

            // wait until the count gets zero
            while not (count = 0) do
                Monitor.Wait lockObj |> ignore

            // set it to -1 and call onLock
            count <- -1
            onLock None

    member __.Enter(c : 'a, onLock : Option<'a> -> unit) =
        let local = local.Value
        if Monitor.IsEntered lockObj then
            count <- count - 1
        else
            Monitor.Enter lockObj

            if color <> c then
                count <- count - local.Count
                local.Stash <- (local.Count, color) :: local.Stash
                local.Count <- 0

            while not (count = 0 || (count > 0 && color = c)) do
                Monitor.Wait lockObj |> ignore

            if count = 0 then
                color <- c
                count <- 1
                local.Count <- 1
                Monitor.Exit lockObj
                onLock (Some c)

            elif color = c then
                count <- count + 1
                local.Count <- local.Count + 1
                Monitor.Exit lockObj
        
            

    member __.Exit(onUnlock : Option<'a> -> unit) =
        let local = local.Value

        if Monitor.IsEntered lockObj then
            count <- count + 1
            if count = 0 then
                match local.Stash with
                    | (cnt, col) :: rest ->
                        local.Stash <- rest
                        count <- cnt
                        color <- col
                        local.Count <- cnt
                    | _ ->
                        color <- Unchecked.defaultof<'a>
                        local.Count <- 0

                Monitor.PulseAll lockObj
                onUnlock None
                Monitor.Exit lockObj
        else
            Monitor.Enter lockObj
            count <- count - 1
            local.Count <- local.Count - 1

            if count = 0 then
                match local.Stash with
                    | (cnt, col) :: rest ->
                        local.Stash <- rest
                        count <- cnt
                        color <- col
                        local.Count <- cnt
                    | _ ->
                        onUnlock (Some color)
                        color <- Unchecked.defaultof<'a>
                        local.Count <- 0

                Monitor.PulseAll lockObj

            Monitor.Exit lockObj
            ()
        
    member x.Enter (c : 'a) = x.Enter(c, ignore)
    member x.Enter() = x.Enter(ignore)
    member x.Exit() = x.Exit(ignore)

    member inline x.Use(color : 'a, f : unit -> 'x) =
        x.Enter(color)
        try f()
        finally x.Exit()
          
    member inline x.Use(f : unit -> 'x) =
        x.Enter()
        try f()
        finally x.Exit()

type ColoredLockOld<'a when 'a : equality and 'a : comparison>() =
    let isZero = new ManualResetEventSlim(true)
    let mutable color = Unchecked.defaultof<'a>
    let mutable count = 0
    let mutable readers = Map.empty
    let mutable waiting = 0

    let waitZero() =
        Interlocked.Increment(&waiting) |> ignore
        isZero.Wait()
        Interlocked.Decrement(&waiting) |> ignore
        

    let addReader() =
        let id = Thread.CurrentThread.ManagedThreadId
        Interlocked.Change(&readers, fun m ->
            match Map.tryFind id m with
                | Some cnt -> Map.add id (cnt + 1) m
                | None -> Map.add id 1 m
        ) |> ignore

    let removeReader() =
        let id = Thread.CurrentThread.ManagedThreadId
        Interlocked.Change(&readers, fun m ->
            match Map.tryFind id m with
                | Some cnt -> 
                    if cnt = 1 then Map.remove id m
                    else Map.add id (cnt - 1) m
                | None -> m
        ) |> ignore

    member x.HasColorLock =
        Map.containsKey Thread.CurrentThread.ManagedThreadId readers

    member x.HasExclusiveLock =
        Monitor.IsEntered isZero

    member x.Enter (c : 'a, onLock : Option<'a> -> unit) =
        if Monitor.IsEntered isZero then
            x.Enter(onLock)
        else
            Monitor.Enter isZero
            if count = 0 then
                isZero.Reset()
                count <- 1
                color <- c
                addReader()
                onLock (Some c)
                Monitor.Exit isZero

            elif color = c then
                if waiting > 0 then
                    // other threads wait for zero
                    let amINested = x.HasColorLock
                    if amINested then
                        count <- count + 1
                        addReader()
                        Monitor.Exit isZero
                    else
                        Monitor.Exit isZero
                        waitZero()
                        x.Enter(c, onLock)
                else
                    count <- count + 1
                    addReader()
                    Monitor.Exit isZero
            else
                assert (not x.HasColorLock)
                Monitor.Exit isZero
                waitZero()
                x.Enter(c, onLock)

    member x.Enter(onLock : Option<'a> -> unit) =
        Monitor.Enter isZero
        if count = 0 then
            isZero.Reset()
            onLock None
            count <- -1
        elif count < 0 then
            count <- count - 1
        else
            Monitor.Exit isZero
            waitZero()
            x.Enter(onLock)

    member x.Exit (onUnlock : Option<'a> -> unit) =
        if Monitor.IsEntered isZero then
            if count = 0 then
                failwithf "cannot exit non-held lock"
            elif count > 0 then
                failwithf "crazy lock error %A" count
            elif count = -1 then
                onUnlock None
                count <- 0
                isZero.Set()
                Monitor.Exit isZero
            else 
                count <- count + 1
                Monitor.Exit isZero

        else
            lock isZero (fun () ->
                if count = 0 then
                    failwithf "cannot exit non-held lock"
                elif count < 0 then
                    failwithf "crazy lock error %A" count
                elif count = 1 then
                    removeReader()
                    count <- 0
                    onUnlock (Some color)
                    color <- Unchecked.defaultof<_>
                    isZero.Set()
                else
                    removeReader()
                    count <- count - 1
            )
  
    member x.Enter (c : 'a) = x.Enter(c, ignore)
    member x.Enter() = x.Enter(ignore)
    member x.Exit() = x.Exit(ignore)

    member inline x.Use(color : 'a, f : unit -> 'x) =
        x.Enter(color)
        try f()
        finally x.Exit()
          
    member inline x.Use(f : unit -> 'x) =
        x.Enter()
        try f()
        finally x.Exit()
