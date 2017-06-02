namespace Aardvark.Base

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Incremental

type ColoredLockStatus<'a> =
    | Exclusive
    | Colored of 'a
    | NotEntered

type ColoredLock<'a when 'a : equality and 'a : comparison>() =
    let mutable color = Unchecked.defaultof<'a>
    let mutable count = 0
    let lockObj = obj()
    let myCount = new ThreadLocal<ref<int> * ref<'a>>(fun _ -> ref 0, ref Unchecked.defaultof<'a>)
    //let stashColor = new ThreadLocal<'a>(fun _ -> Unchecked.defaultof<'a>)

    member __.Status =
        if Monitor.IsEntered lockObj then Exclusive
        else
            let (local,_) = myCount.Value
            if !local > 0 then Colored color
            else NotEntered

    member __.HasExclusiveLock =
        Monitor.IsEntered lockObj

    member __.Enter(onLock : Option<'a> -> unit) =
        let local, stash = myCount.Value

        if Monitor.IsEntered lockObj then
            count <- count - 1
        else
            Monitor.Enter lockObj

            if !local > 0 then
                // give away all readers acquired by the current thread
                count <- count - !local
                stash := color

            // wait until the count gets zero
            while not (count = 0) do
                Monitor.Wait lockObj |> ignore

            // set it to -1 and call onLock
            count <- -1
            onLock None

    member __.Enter(c : 'a, onLock : Option<'a> -> unit) =
        let local, stash = myCount.Value
        if Monitor.IsEntered lockObj then
            count <- count - 1
        else
            Monitor.Enter lockObj
            while not (count = 0 || (count > 0 && color = c)) do
                Monitor.Wait lockObj |> ignore

            if count = 0 then
                color <- c
                count <- 1
                local := 1
                onLock (Some c)

            elif color = c then
                count <- count + 1
                local := !local + 1
        
            Monitor.Exit lockObj

    member __.Exit(onUnlock : Option<'a> -> unit) =
        let local,stash = myCount.Value

        if Monitor.IsEntered lockObj then
            count <- count + 1
            if count = 0 then
                count <- !local
                color <- !stash
                Monitor.PulseAll lockObj
                onUnlock None
                Monitor.Exit lockObj
        else
            Monitor.Enter lockObj
            count <- count - 1
            local := !local - 1

            if count = 0 then
                Monitor.PulseAll lockObj
                onUnlock (Some color)
                color <- Unchecked.defaultof<_>

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
