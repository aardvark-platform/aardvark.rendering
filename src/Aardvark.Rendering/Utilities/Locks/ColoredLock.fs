namespace Aardvark.Rendering

open System
open System.Threading

/// Describes the ownership held by the calling thread for a colored lock.
type ColoredLockStatus<'a> =
    /// The calling thread holds exclusive ownership.
    | Exclusive
    /// The calling thread shares ownership with readers of the given color.
    | Colored of 'a
    /// The calling thread does not own the lock.
    | NotEntered

type private ColoredLockGenerationState =
    | Initializing = 0
    | Active = 1
    | Failed = 2

[<AllowNullLiteral>]
type private ColoredLockGeneration<'a>() =
    let mutable state = int ColoredLockGenerationState.Initializing

    member val Total = 0 with get, set
    member val CallbackColor : Option<'a> = None with get, set
    member val Next : ColoredLockGeneration<'a> = null with get, set

    member _.State
        with get() = enum<ColoredLockGenerationState> (Volatile.Read &state)
        and set(value) = Volatile.Write(&state, int value)

[<AllowNullLiteral>]
type private ColoredLockSuspension<'a>() =
    member val Count = 0 with get, set
    member val Color = Unchecked.defaultof<'a> with get, set
    member val Generation : ColoredLockGeneration<'a> = null with get, set
    member val Parent : ColoredLockSuspension<'a> = null with get, set
    member val Next : ColoredLockSuspension<'a> = null with get, set

type private ColoredLockLocalState<'a> =
    class
        val mutable public Count : int
        val mutable public Stash : ColoredLockSuspension<'a>

        new() = { Count = 0; Stash = null }
    end

/// A reentrant lock whose equal-colored owners may enter concurrently while
/// different colors and exclusive owners exclude one another. Nested ownership
/// of another mode temporarily suspends and later restores the calling thread's
/// outer colored ownership.
type ColoredLock<'a when 'a : equality and 'a : comparison>() =
    let mutable color = Unchecked.defaultof<'a>
    let mutable count = 0
    let mutable generation : ColoredLockGeneration<'a> = null
    let lockObj = obj()
    let mutable waiters = 0
    let local = new ThreadLocal<ColoredLockLocalState<'a>>(fun _ -> ColoredLockLocalState())
    let mutable freeGenerations : ColoredLockGeneration<'a> = null
    let mutable freeSuspensions : ColoredLockSuspension<'a> = null

    let takeGeneration() =
        if isNull freeGenerations then
            ColoredLockGeneration<'a>()
        else
            let result = freeGenerations
            freeGenerations <- result.Next
            result.Next <- null
            result.State <- ColoredLockGenerationState.Initializing
            result

    let releaseGeneration (value : ColoredLockGeneration<'a>) =
        value.CallbackColor <- None
        value.Next <- freeGenerations
        freeGenerations <- value

    let takeSuspension count color generation parent next =
        let result =
            if isNull freeSuspensions then ColoredLockSuspension<'a>()
            else
                let result = freeSuspensions
                freeSuspensions <- result.Next
                result

        result.Count <- count
        result.Color <- color
        result.Generation <- generation
        result.Parent <- parent
        result.Next <- next
        result

    let releaseSuspension (value : ColoredLockSuspension<'a>) =
        value.Color <- Unchecked.defaultof<'a>
        value.Generation <- null
        value.Parent <- null
        value.Next <- freeSuspensions
        freeSuspensions <- value

    // Suspended counts remain logical owners. Entries are removed by identity,
    // while lookups deliberately use the same F# equality as active colors.
    // This preserves established semantics for structural and non-reflexive
    // colors and keeps restoration independent of hash/equality edge cases.
    let mutable suspended : ColoredLockSuspension<'a> = null

    let removeSuspended (entry : ColoredLockSuspension<'a>) =
        if Object.ReferenceEquals(suspended, entry) then
            suspended <- entry.Next
        else
            let mutable previous = suspended
            let mutable current = if isNull previous then null else previous.Next

            while not (isNull current) && not (Object.ReferenceEquals(current, entry)) do
                previous <- current
                current <- current.Next

            if isNull current then
                invalidOp "The colored lock's suspended ownership was not found."
            else
                previous.Next <- current.Next

    let findGeneration (requestedColor : 'a) (knownDifferent : ColoredLockSuspension<'a>) =
        let mutable result = if count > 0 && color = requestedColor then generation else null
        let mutable current =
            if not (isNull knownDifferent) && Object.ReferenceEquals(suspended, knownDifferent) then knownDifferent.Next
            else suspended

        while isNull result && not (isNull current) do
            if current.Color = requestedColor then
                result <- current.Generation
            current <- current.Next

        result

    let ownsGeneration (state : ColoredLockLocalState<'a>) (value : ColoredLockGeneration<'a>) =
        let mutable result = state.Count > 0 && Object.ReferenceEquals(generation, value)
        let mutable current = state.Stash

        while not result && not (isNull current) do
            result <- Object.ReferenceEquals(current.Generation, value)
            current <- current.Parent

        result

    let pulseAll() =
        if waiters > 0 then Monitor.PulseAll lockObj

    let wait() =
        waiters <- waiters + 1
        try Monitor.Wait lockObj |> ignore
        finally waiters <- waiters - 1

    let suspendCurrent (state : ColoredLockLocalState<'a>) =
        if state.Count > 0 then
            let owned = state.Count
            let ownedColor = color
            let ownedGeneration = generation
            count <- count - owned
            if count < 0 then invalidOp "The colored lock's active ownership became negative."

            let entry = takeSuspension owned ownedColor ownedGeneration state.Stash suspended
            suspended <- entry
            state.Stash <- entry
            state.Count <- 0

            if count = 0 then
                color <- Unchecked.defaultof<'a>
                generation <- null
                pulseAll()

            entry
        else
            null

    let restoreSuspended (state : ColoredLockLocalState<'a>) =
        let mutable interruption : exn = null
        let entry = state.Stash

        if not (isNull entry) then
            let owned = entry.Count
            let ownedColor = entry.Color
            let parent = entry.Parent

            while not (count = 0 || (count > 0 && color = ownedColor)) do
                try wait()
                with :? ThreadInterruptedException as e ->
                    if isNull interruption then interruption <- e

            removeSuspended entry
            if count = 0 then
                color <- ownedColor
                generation <- entry.Generation
            elif not (Object.ReferenceEquals(generation, entry.Generation)) then
                invalidOp "Equal-colored ownership belongs to different lock generations."
            count <- count + owned
            state.Count <- owned
            state.Stash <- parent
            releaseSuspension entry
            pulseAll()

        interruption

    // Called with lockObj entered. Keeping transition handling out of the public
    // member lets the JIT inline the uncontended equal-color path. A new logical
    // generation is published before OnLock runs, matching the historical
    // non-blocking callback behavior for same-colored entrants.
    member private _.EnterColoredSlow(state : ColoredLockLocalState<'a>, requestedColor : 'a, sameActiveColor : bool, onLock : Option<'a> -> unit) =
        let mutable acquiredGeneration : ColoredLockGeneration<'a> = null
        let mutable firstLogicalOwner = false

        try
            let knownDifferent =
                if state.Count > 0 && not sameActiveColor then suspendCurrent state
                else null

            let mutable ready = false
            while not ready do
                while not (count = 0 || (count > 0 && color = requestedColor)) do
                    wait()

                let candidate = findGeneration requestedColor knownDifferent
                if not (isNull candidate) &&
                   candidate.State = ColoredLockGenerationState.Failed &&
                   not (ownsGeneration state candidate) then
                    // Owners which already joined an initializing generation are
                    // allowed to unwind or reenter it. New owners wait until that
                    // failed generation drains so a fresh OnLock can establish
                    // the protected resource before ownership is published.
                    wait()
                else
                    acquiredGeneration <- candidate
                    ready <- true

            firstLogicalOwner <- isNull acquiredGeneration
            if firstLogicalOwner then
                acquiredGeneration <- takeGeneration()
                acquiredGeneration.CallbackColor <- Some requestedColor

            if count = 0 then
                color <- requestedColor
                generation <- acquiredGeneration
            elif not (Object.ReferenceEquals(generation, acquiredGeneration)) then
                invalidOp "Equal-colored ownership belongs to different lock generations."

            count <- count + 1
            state.Count <- state.Count + 1
            acquiredGeneration.Total <- acquiredGeneration.Total + 1
            Monitor.Exit lockObj
        with _ ->
            if state.Count = 0 then restoreSuspended state |> ignore
            pulseAll()
            Monitor.Exit lockObj
            reraise()

        if firstLogicalOwner then
            let mutable callbackError : exn = null
            try onLock acquiredGeneration.CallbackColor
            with e -> callbackError <- e

            if isNull callbackError then
                // Initializing and Active generations are both joinable, so a
                // successful callback only needs release publication. Failure
                // still takes the monitor to exclude admission during rollback.
                acquiredGeneration.State <- ColoredLockGenerationState.Active
            else
                Monitor.Enter lockObj
                try
                    acquiredGeneration.State <- ColoredLockGenerationState.Failed

                    // Balanced callback reentry restores this generation before
                    // returning. Retract only this provisional acquisition; any
                    // same-colored joiners drain the failed generation normally.
                    if state.Count > 0 && Object.ReferenceEquals(generation, acquiredGeneration) then
                        count <- count - 1
                        state.Count <- state.Count - 1
                        acquiredGeneration.Total <- acquiredGeneration.Total - 1

                        if count = 0 then
                            color <- Unchecked.defaultof<'a>
                            generation <- null
                            pulseAll()

                        if state.Count = 0 then
                            restoreSuspended state |> ignore

                    if acquiredGeneration.Total = 0 then
                        releaseGeneration acquiredGeneration

                    pulseAll()
                finally
                    Monitor.Exit lockObj

                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(callbackError).Throw()

    // Called with lockObj entered and the final local active count removed.
    member private _.ExitColoredSlow(state : ColoredLockLocalState<'a>, releasedGeneration : ColoredLockGeneration<'a>, onUnlock : Option<'a> -> unit) =
        let mutable callbackError : exn = null
        let mutable restorationError : exn = null

        try
            if releasedGeneration.Total = 0 && releasedGeneration.State = ColoredLockGenerationState.Active then
                try onUnlock releasedGeneration.CallbackColor
                with e -> callbackError <- e

            if count = 0 then
                color <- Unchecked.defaultof<'a>
                generation <- null
                if isNull state.Stash then pulseAll()

            restorationError <- restoreSuspended state

            if releasedGeneration.Total = 0 && releasedGeneration.State <> ColoredLockGenerationState.Initializing then
                releaseGeneration releasedGeneration
        finally
            Monitor.Exit lockObj

        let error = if isNull callbackError then restorationError else callbackError
        if not (isNull error) then
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw()

    /// Gets the ownership held by the calling thread.
    member _.Status =
        if Monitor.IsEntered lockObj then
            Exclusive
        else
            let state = local.Value
            if state.Count > 0 then Colored color
            else NotEntered

    /// Returns whether the calling thread currently holds exclusive ownership.
    member _.HasExclusiveLock =
        Monitor.IsEntered lockObj

    /// Enters the lock exclusively. The callback is invoked once when the
    /// outermost exclusive ownership starts and is paired with OnUnlock(None)
    /// when that ownership ends. Suspended colored ownership is restored before
    /// this method or the matching Exit returns, even if either callback throws.
    member _.Enter(onLock : Option<'a> -> unit) =
        let state = local.Value

        if Monitor.IsEntered lockObj then
            count <- count - 1
        else
            Monitor.Enter lockObj
            let mutable established = false

            try
                try
                    suspendCurrent state |> ignore

                    while count <> 0 do
                        wait()

                    count <- -1
                    established <- true
                    onLock None
                with _ ->
                    if established then count <- 0
                    restoreSuspended state |> ignore
                    pulseAll()
                    reraise()
            with _ ->
                Monitor.Exit lockObj
                reraise()

    /// Enters the lock with the given color. Equal-colored callers may share
    /// ownership. OnLock(Some color) runs exactly when the first logical owner
    /// enters. As in earlier versions, the generation is visible to same-colored
    /// entrants while that callback runs; suspension does not end its ownership.
    member x.Enter(requestedColor : 'a, onLock : Option<'a> -> unit) =
        let state = local.Value

        if Monitor.IsEntered lockObj then
            count <- count - 1
        else
            Monitor.Enter lockObj
            let sameActiveColor = count > 0 && color = requestedColor
            if sameActiveColor &&
               (generation.State <> ColoredLockGenerationState.Failed || state.Count > 0) then
                count <- count + 1
                state.Count <- state.Count + 1
                generation.Total <- generation.Total + 1
                Monitor.Exit lockObj
            else
                x.EnterColoredSlow(state, requestedColor, sameActiveColor, onLock)

    /// Leaves the innermost ownership. OnUnlock is paired with the matching
    /// successful OnLock when the final logical owner leaves. A suspended outer
    /// color is restored or merged before this method returns, including when
    /// the callback throws.
    member x.Exit(onUnlock : Option<'a> -> unit) =
        let state = local.Value

        if Monitor.IsEntered lockObj then
            count <- count + 1
            if count = 0 then
                let mutable callbackError : exn = null
                let mutable restorationError : exn = null
                try onUnlock None
                with e -> callbackError <- e

                try
                    restorationError <- restoreSuspended state
                    pulseAll()
                finally
                    Monitor.Exit lockObj

                let error = if isNull callbackError then restorationError else callbackError
                if not (isNull error) then
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw()
        else
            Monitor.Enter lockObj
            if state.Count <= 0 || count <= 0 then
                Monitor.Exit lockObj
                invalidOp "Cannot exit a colored lock that is not entered by the calling thread."
            else
                let releasedGeneration = generation
                count <- count - 1
                state.Count <- state.Count - 1
                releasedGeneration.Total <- releasedGeneration.Total - 1
                if releasedGeneration.Total < 0 then
                    invalidOp "The colored lock's logical ownership became negative."

                if state.Count > 0 then
                    Monitor.Exit lockObj
                else
                    x.ExitColoredSlow(state, releasedGeneration, onUnlock)

    /// Enters the lock with a color without lifecycle callbacks.
    member x.Enter(color : 'a) = x.Enter(color, ignore)

    /// Enters the lock exclusively without lifecycle callbacks.
    member x.Enter() = x.Enter(ignore)

    /// Leaves the innermost ownership without lifecycle callbacks.
    member x.Exit() = x.Exit(ignore)

    /// Runs the action with colored ownership and always releases it afterward.
    member inline x.Use(color : 'a, f : unit -> 'x) =
        x.Enter(color)
        try f()
        finally x.Exit()

    /// Runs the action with exclusive ownership and always releases it afterward.
    member inline x.Use(f : unit -> 'x) =
        x.Enter()
        try f()
        finally x.Exit()
