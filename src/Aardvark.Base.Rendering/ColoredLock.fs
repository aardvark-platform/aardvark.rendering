namespace Aardvark.Base

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental

type ColoredLock<'a when 'a : equality and 'a : comparison>() =
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

    member x.Enter (c : 'a) =
        if Monitor.IsEntered isZero then
            x.Enter()
        else
            Monitor.Enter isZero
            if count = 0 then
                isZero.Reset()
                count <- 1
                color <- c
                addReader()
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
                        x.Enter c
                else
                    count <- count + 1
                    addReader()
                    Monitor.Exit isZero
            else
                assert (not x.HasColorLock)
                Monitor.Exit isZero
                waitZero()
                x.Enter c

    member x.Enter (candidates : Set<'a>) =
        if Monitor.IsEntered isZero then
            x.Enter()
        else
            Monitor.Enter isZero
            if count = 0 then
                isZero.Reset()
                count <- 1
                color <- Seq.head candidates
                addReader()
                Monitor.Exit isZero

            elif Set.contains color candidates then
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
                        x.Enter candidates
                else
                    count <- count + 1
                    addReader()
                    Monitor.Exit isZero
            else
                assert (not x.HasColorLock)
                Monitor.Exit isZero
                waitZero()
                x.Enter candidates


    member x.Enter() =
        Monitor.Enter isZero
        if count = 0 then
            isZero.Reset()
            count <- -1
        elif count < 0 then
            count <- count - 1
        else
            Monitor.Exit isZero
            waitZero()
            x.Enter()

    member x.Exit () =
        if Monitor.IsEntered isZero then
            if count = 0 then
                failwithf "cannot exit non-held lock"
            elif count > 0 then
                failwithf "crazy lock error %A" count
            elif count = -1 then
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
                    color <- Unchecked.defaultof<_>
                    isZero.Set()
                else
                    removeReader()
                    count <- count - 1
            )
  
    member inline x.Use(color : 'a, f : unit -> 'x) =
        x.Enter(color)
        try f()
        finally x.Exit()
          
    member inline x.Use(f : unit -> 'x) =
        x.Enter()
        try f()
        finally x.Exit()
