namespace Aardvark.Base

open System.Threading

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