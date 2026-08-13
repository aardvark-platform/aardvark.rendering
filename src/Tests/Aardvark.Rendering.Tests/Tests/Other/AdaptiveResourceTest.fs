namespace Aardvark.Rendering.Tests

open System
open System.Threading
open System.Threading.Tasks
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto

module ``AdaptiveResource Tests`` =

    [<AutoOpen>]
    module private Utils =

        type Base(token : RenderToken) =
            member x.Token = token

        type Derived(token : RenderToken) =
            inherit Base(token)

        type DummyResource() =
            inherit AdaptiveResource<Derived>()

            let mutable allocated = false
            member x.IsAllocated = allocated

            override x.Create() = allocated <- true
            override x.Destroy() = allocated <- false
            override x.Compute(t, rt) = Derived(rt)

        type ResourceHandle =
            {
                Name       : string
                Generation : int
                Token      : RenderToken
            }

        type CountingResource(name : string) =
            inherit AdaptiveResource<ResourceHandle>()

            let mutable allocated = false
            let mutable createCount = 0
            let mutable destroyCount = 0
            let mutable computeCount = 0

            member _.IsAllocated = allocated
            member _.CreateCount = createCount
            member _.DestroyCount = destroyCount
            member _.ComputeCount = computeCount

            override _.Create() =
                if allocated then failwith $"Resource {name} is already allocated"
                allocated <- true
                createCount <- createCount + 1

            override _.Destroy() =
                if not allocated then failwith $"Resource {name} is not allocated"
                allocated <- false
                destroyCount <- destroyCount + 1

            override _.Compute(_, rt) =
                if not allocated then failwith $"Resource {name} is not allocated"
                computeCount <- computeCount + 1
                { Name = name; Generation = createCount; Token = rt }

        type FailingAcquireResource<'T>(inner : IAdaptiveResource<'T>) =
            inherit AdaptiveObject()

            let mutable acquireAttemptCount = 0

            member _.AcquireAttemptCount = acquireAttemptCount

            member _.Acquire() =
                acquireAttemptCount <- acquireAttemptCount + 1
                if acquireAttemptCount = 1 then
                    failwith "Expected atomic acquisition failure"
                inner.Acquire()

            member x.GetValue(token : AdaptiveToken, renderToken : RenderToken) =
                x.EvaluateAlways token (fun token -> inner.GetValue(token, renderToken))

            interface IAdaptiveValue with
                member x.Accept(visitor : IAdaptiveValueVisitor<'R>) = visitor.Visit x
                member _.ContentType = typeof<'T>
                member x.GetValueUntyped(token) = x.GetValue(token, RenderToken.Empty) :> obj

            interface IAdaptiveValue<'T> with
                member x.GetValue(token) = x.GetValue(token, RenderToken.Empty)

            interface IAdaptiveResource with
                member x.Acquire() = x.Acquire()
                member _.Release() = inner.Release()
                member _.ReleaseAll() = inner.ReleaseAll()
                member x.GetValue(token, renderToken) = x.GetValue(token, renderToken) :> obj

            interface IAdaptiveResource<'T> with
                member x.GetValue(token, renderToken) = x.GetValue(token, renderToken)

        type ResourceFailure =
            | FailCompute
            | FailDestroy

        type FaultingResource(name : string, failure : ResourceFailure) =
            inherit AdaptiveResource<ResourceHandle>()

            let mutable allocated = false
            let mutable createCount = 0
            let mutable destroyAttemptCount = 0
            let mutable destroyCount = 0
            let mutable computeAttemptCount = 0
            let mutable computeCount = 0

            member _.IsAllocated = allocated
            member _.CreateCount = createCount
            member _.DestroyAttemptCount = destroyAttemptCount
            member _.DestroyCount = destroyCount
            member _.ComputeAttemptCount = computeAttemptCount
            member _.ComputeCount = computeCount

            override _.Create() =
                if allocated then failwith $"Resource {name} is already allocated"
                allocated <- true
                createCount <- createCount + 1

            override _.Destroy() =
                if not allocated then failwith $"Resource {name} is not allocated"
                allocated <- false
                destroyAttemptCount <- destroyAttemptCount + 1

                if failure = FailDestroy && destroyAttemptCount = 1 then
                    failwith $"Expected {name} destruction failure"

                destroyCount <- destroyCount + 1

            override _.Compute(_, rt) =
                if not allocated then failwith $"Resource {name} is not allocated"
                computeAttemptCount <- computeAttemptCount + 1

                if failure = FailCompute && computeAttemptCount = 1 then
                    failwith $"Expected {name} computation failure"

                computeCount <- computeCount + 1
                { Name = name; Generation = createCount; Token = rt }

        type MutableResource(name : string, initial : int) =
            inherit AdaptiveResource<int>()

            let mutable value = initial
            let mutable allocated = false
            let mutable destroyCount = 0
            let mutable computeCount = 0

            member _.DestroyCount = destroyCount
            member _.ComputeCount = computeCount

            member x.Set(value' : int) =
                transact (fun _ ->
                    value <- value'
                    x.MarkOutdated()
                )

            override _.Create() =
                if allocated then failwith $"Resource {name} is already allocated"
                allocated <- true

            override _.Destroy() =
                if not allocated then failwith $"Resource {name} is not allocated"
                allocated <- false
                destroyCount <- destroyCount + 1

            override _.Compute(_, _) =
                if not allocated then failwith $"Resource {name} is not allocated"
                computeCount <- computeCount + 1
                value

        type BlockingInputResource(destroyEntered : ManualResetEventSlim, continueDestroy : ManualResetEventSlim) =
            inherit AdaptiveResource<int>()

            let mutable allocated = false
            let mutable blockNextDestroy = false
            let mutable createCount = 0
            let mutable destroyCount = 0
            let mutable computeCount = 0

            member _.CreateCount = createCount
            member _.DestroyCount = destroyCount
            member _.ComputeCount = computeCount
            member _.BlockNextDestroy() = blockNextDestroy <- true

            override _.Create() =
                if allocated then failwith "Blocking input is already allocated"
                allocated <- true
                createCount <- createCount + 1

            override _.Destroy() =
                if not allocated then failwith "Blocking input is not allocated"
                destroyCount <- destroyCount + 1

                if blockNextDestroy then
                    blockNextDestroy <- false
                    destroyEntered.Set()

                    if not <| continueDestroy.Wait(TimeSpan.FromSeconds 10.0) then
                        failwith "Timed out waiting to continue input destruction"

                allocated <- false

            override _.Compute(_, _) =
                if not allocated then failwith "Blocking input is not allocated"
                computeCount <- computeCount + 1
                createCount

        type ReentrantDestroyResource<'T>(value : 'T) =
            inherit AdaptiveResource<'T>()

            let mutable allocated = false
            let mutable createCount = 0
            let mutable destroyCount = 0
            let mutable onDestroy = ignore

            member _.CreateCount = createCount
            member _.DestroyCount = destroyCount
            member _.SetDestroyAction(action : unit -> unit) = onDestroy <- action

            override _.Create() =
                if allocated then failwith "Reentrant-destroy resource is already allocated"
                allocated <- true
                createCount <- createCount + 1

            override _.Destroy() =
                if not allocated then failwith "Reentrant-destroy resource is not allocated"

                try onDestroy()
                finally
                    allocated <- false
                    destroyCount <- destroyCount + 1

            override _.Compute(_, _) =
                if not allocated then failwith "Reentrant-destroy resource is not allocated"
                value

        type ResourceSpy<'T>(name : string, inner : IAdaptiveResource<'T>, events : ResizeArray<string> option) =

            let mutable acquireCount = 0
            let mutable releaseCount = 0
            let mutable releaseAllCount = 0

            new(name, inner) = ResourceSpy(name, inner, None)

            member _.AcquireCount = acquireCount
            member _.ReleaseCount = releaseCount
            member _.ReleaseAllCount = releaseAllCount

            member private _.Log(operation : string) =
                match events with
                | Some events -> events.Add $"{name}.{operation}"
                | None -> ()

            member x.Acquire() =
                acquireCount <- acquireCount + 1
                x.Log "acquire"
                inner.Acquire()

            member x.Release() =
                releaseCount <- releaseCount + 1
                x.Log "release"
                inner.Release()

            member x.ReleaseAll() =
                releaseAllCount <- releaseAllCount + 1
                x.Log "releaseAll"
                inner.ReleaseAll()

            interface IAdaptiveObject with
                member _.AllInputsProcessed(a) = inner.AllInputsProcessed(a)
                member _.InputChanged(a, b) = inner.InputChanged(a, b)
                member _.Mark() = inner.Mark()
                member _.IsConstant = inner.IsConstant
                member _.Level
                    with get() = inner.Level
                    and set value = inner.Level <- value
                member _.OutOfDate
                    with get() = inner.OutOfDate
                    and set value = inner.OutOfDate <- value
                member _.Outputs = inner.Outputs
                member _.Tag
                    with get() = inner.Tag
                    and set value = inner.Tag <- value
                member _.Weak = inner.Weak

            interface IAdaptiveValue with
                member x.Accept(visitor : IAdaptiveValueVisitor<'R>) = visitor.Visit x
                member _.ContentType = typeof<'T>
                member _.GetValueUntyped(token) = inner.GetValue(token, RenderToken.Empty) :> obj

            interface IAdaptiveValue<'T> with
                member _.GetValue(token) = inner.GetValue(token, RenderToken.Empty)

            interface IAdaptiveResource with
                member x.Acquire() = x.Acquire()
                member x.Release() = x.Release()
                member x.ReleaseAll() = x.ReleaseAll()
                member _.GetValue(token, renderToken) = inner.GetValue(token, renderToken) :> obj

            interface IAdaptiveResource<'T> with
                member _.GetValue(token, renderToken) = inner.GetValue(token, renderToken)

        type AcquireSignalResource<'T>(inner : IAdaptiveResource<'T>, acquireStarted : ManualResetEventSlim) =

            inherit AdaptiveObject()

            member _.Acquire() =
                acquireStarted.Set()
                inner.Acquire()

            member x.GetValue(token : AdaptiveToken, renderToken : RenderToken) =
                x.EvaluateAlways token (fun token -> inner.GetValue(token, renderToken))

            interface IAdaptiveValue with
                member x.Accept(visitor : IAdaptiveValueVisitor<'R>) = visitor.Visit x
                member _.ContentType = typeof<'T>
                member x.GetValueUntyped(token) = x.GetValue(token, RenderToken.Empty) :> obj

            interface IAdaptiveValue<'T> with
                member x.GetValue(token) = x.GetValue(token, RenderToken.Empty)

            interface IAdaptiveResource with
                member x.Acquire() = x.Acquire()
                member _.Release() = inner.Release()
                member _.ReleaseAll() = inner.ReleaseAll()
                member x.GetValue(token, renderToken) = x.GetValue(token, renderToken) :> obj

            interface IAdaptiveResource<'T> with
                member x.GetValue(token, renderToken) = x.GetValue(token, renderToken)

        type LockingInputResource(acquireBlocked : ManualResetEventSlim, continueAcquire : ManualResetEventSlim) =
            inherit AdaptiveObject()

            let gate = obj()
            let mutable acquireCount = 0
            let mutable releaseCount = 0
            let mutable activeCount = 0
            let mutable onContinue = ignore

            member _.AcquireCount = acquireCount
            member _.ReleaseCount = releaseCount
            member _.ActiveCount = activeCount
            member _.SetContinuation(action : unit -> unit) = onContinue <- action

            member _.Acquire() =
                lock gate (fun _ ->
                    if acquireCount = 0 then
                        acquireBlocked.Set()

                        if not <| continueAcquire.Wait(TimeSpan.FromSeconds 10.0) then
                            failwith "Timed out waiting to continue the direct input acquisition"

                        onContinue()

                    acquireCount <- acquireCount + 1
                    activeCount <- activeCount + 1
                )

            member _.Release() =
                lock gate (fun _ ->
                    if activeCount <= 0 then failwith "Direct input released without an acquisition"
                    releaseCount <- releaseCount + 1
                    activeCount <- activeCount - 1
                )

            member x.GetValue(token : AdaptiveToken) =
                x.EvaluateAlways token (fun _ -> 1)

            interface IAdaptiveValue with
                member x.Accept(visitor : IAdaptiveValueVisitor<'R>) = visitor.Visit x
                member _.ContentType = typeof<int>
                member x.GetValueUntyped(token) = x.GetValue(token) :> obj

            interface IAdaptiveValue<int> with
                member x.GetValue(token) = x.GetValue(token)

            interface IAdaptiveResource with
                member x.Acquire() = x.Acquire()
                member x.Release() = x.Release()
                member x.ReleaseAll() = while activeCount > 0 do x.Release()
                member x.GetValue(token, _) = x.GetValue(token) :> obj

            interface IAdaptiveResource<int> with
                member x.GetValue(token, _) = x.GetValue(token)

        type InputDependentResource(input : IAdaptiveResource<int>, acquireStarted : ManualResetEventSlim) =
            inherit AdaptiveResource<int>()

            let mutable createCount = 0
            let mutable destroyCount = 0

            member _.CreateCount = createCount
            member _.DestroyCount = destroyCount

            override _.Create() =
                acquireStarted.Set()
                input.Acquire()
                createCount <- createCount + 1

            override _.Destroy() =
                try input.Release()
                finally destroyCount <- destroyCount + 1

            override _.Compute(_, _) = 42

        type JoinedReleaseInput(firstReleaseEntered : ManualResetEventSlim,
                                continueFirstRelease : ManualResetEventSlim,
                                secondReleaseFinished : ManualResetEventSlim,
                                evaluated : ManualResetEventSlim) =
            inherit AdaptiveObject()

            let mutable acquireCount = 0
            let mutable releaseCount = 0
            let mutable activeCount = 0

            member _.AcquireCount = acquireCount
            member _.ReleaseCount = releaseCount
            member _.ActiveCount = activeCount

            member _.Acquire() =
                Interlocked.Increment(&acquireCount) |> ignore
                Interlocked.Increment(&activeCount) |> ignore

            member _.Release() =
                let ordinal = Interlocked.Increment(&releaseCount)

                if ordinal = 1 then
                    firstReleaseEntered.Set()

                    if not <| continueFirstRelease.Wait(TimeSpan.FromSeconds 10.0) then
                        failwith "Timed out waiting to continue the first input release"

                Interlocked.Decrement(&activeCount) |> ignore
                if ordinal = 2 then secondReleaseFinished.Set()

            member x.GetValue(token : AdaptiveToken) =
                evaluated.Set()
                x.EvaluateAlways token (fun _ -> 1)

            interface IAdaptiveValue with
                member x.Accept(visitor : IAdaptiveValueVisitor<'R>) = visitor.Visit x
                member _.ContentType = typeof<int>
                member x.GetValueUntyped(token) = x.GetValue(token) :> obj

            interface IAdaptiveValue<int> with
                member x.GetValue(token) = x.GetValue(token)

            interface IAdaptiveResource with
                member x.Acquire() = x.Acquire()
                member x.Release() = x.Release()
                member x.ReleaseAll() = while activeCount > 0 do x.Release()
                member x.GetValue(token, _) = x.GetValue(token) :> obj

            interface IAdaptiveResource<int> with
                member x.GetValue(token, _) = x.GetValue(token)

        type InputSpy<'T>(initial : 'T) =
            inherit AdaptiveObject()

            let mutable value = initial
            let mutable acquireCount = 0
            let mutable releaseCount = 0
            let mutable releaseAllCount = 0
            let mutable activeCount = 0

            member _.AcquireCount = acquireCount
            member _.ReleaseCount = releaseCount
            member _.ReleaseAllCount = releaseAllCount
            member _.ActiveCount = activeCount

            member x.Value
                with get() = value
                and set value' =
                    value <- value'
                    x.MarkOutdated()

            member x.Set(value' : 'T) =
                transact (fun _ -> x.Value <- value')

            member x.GetValue(token : AdaptiveToken) =
                x.EvaluateAlways token (fun _ -> value)

            member _.Acquire() =
                acquireCount <- acquireCount + 1
                activeCount <- activeCount + 1

            member _.Release() =
                if activeCount <= 0 then failwith "Input released without a matching acquisition"
                releaseCount <- releaseCount + 1
                activeCount <- activeCount - 1

            member _.ReleaseAll() =
                releaseAllCount <- releaseAllCount + 1
                activeCount <- 0

            interface IAdaptiveValue with
                member x.Accept(visitor : IAdaptiveValueVisitor<'R>) = visitor.Visit x
                member _.ContentType = typeof<'T>
                member x.GetValueUntyped(token) = x.GetValue(token) :> obj

            interface IAdaptiveValue<'T> with
                member x.GetValue(token) = x.GetValue(token)

            interface IAdaptiveResource with
                member x.Acquire() = x.Acquire()
                member x.Release() = x.Release()
                member x.ReleaseAll() = x.ReleaseAll()
                member x.GetValue(token, _) = x.GetValue(token) :> obj

            interface IAdaptiveResource<'T> with
                member x.GetValue(token, _) = x.GetValue(token)

        let inline asResource (value : aval<'T>) =
            value :?> IAdaptiveResource<'T>

        let inline asAVal (value : IAdaptiveResource<'T>) =
            value :> aval<'T>

        let createResource (name : string) =
            let resource = CountingResource(name)
            let spy = ResourceSpy(name, resource)
            resource, spy, asAVal spy

        let createResourceWithEvents (events : ResizeArray<string>) (name : string) =
            let resource = CountingResource(name)
            let spy = ResourceSpy(name, resource, Some events)
            resource, spy, asAVal spy

    module private Cases =

        // Relevant for Sg.texture
        let castPreservesEquality() =

            let input = DummyResource()
            let m1 = input |> AdaptiveResource.map unbox<Base>
            let m2 = input |> AdaptiveResource.map unbox<Base>

            Expect.isFalse (m1 = m2) "mapped resources are equal"

            let c1 = input |> AdaptiveResource.cast<Base>
            let c2 = input |> AdaptiveResource.cast<Base>

            Expect.isTrue (c1 = c2) "cast resources are not equal"

        let castPreservesResourceSemantics() =

            let input = DummyResource()
            let output = input |> AdaptiveResource.cast<Base> 
            let token = RenderToken.Empty

            output.Acquire()
            Expect.isTrue input.IsAllocated "input not allocated"

            let result = output.GetValue(AdaptiveToken.Top, token)
            Expect.equal result.Token token "tokens are not equal"

            output.Release()
            Expect.isFalse input.IsAllocated "input still allocated"

        let bindRetainsInnerUntilFinalRelease() =

            let input = InputSpy(0)
            let inner, innerSpy, innerValue = createResource "inner"
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    innerValue
                )
                |> asResource

            let token = RenderToken.Zero

            output.Acquire()
            output.Acquire()

            Expect.equal input.AcquireCount 2 "input acquisition count"
            Expect.equal input.ActiveCount 2 "active input count"
            Expect.equal innerSpy.AcquireCount 0 "inner acquired before evaluation"

            let first = output.GetValue(AdaptiveToken.Top, token)
            Expect.equal first.Name "inner" "unexpected resource"
            Expect.equal first.Generation 1 "unexpected first generation"
            Expect.isTrue (Object.ReferenceEquals(first.Token, token)) "render token was not propagated"
            Expect.equal mappingCount 1 "mapping count after first evaluation"
            Expect.equal innerSpy.AcquireCount 1 "inner acquisition count"
            Expect.equal inner.CreateCount 1 "inner creation count"
            Expect.equal inner.ComputeCount 1 "inner computation count"

            output.Release()

            Expect.equal input.ReleaseCount 1 "input release count after partial release"
            Expect.equal input.ActiveCount 1 "active input count after partial release"
            Expect.equal innerSpy.ReleaseCount 0 "inner released by partial release"
            Expect.equal inner.DestroyCount 0 "inner destroyed by partial release"
            Expect.isTrue inner.IsAllocated "inner not retained after partial release"

            let retained = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)
            Expect.equal retained.Generation 1 "partial release recreated the handle"
            Expect.equal retained.Token token "partial release cleared the cached handle"
            Expect.equal mappingCount 1 "partial release reran the mapping"
            Expect.equal inner.ComputeCount 1 "partial release recomputed the inner value"

            output.Release()

            Expect.equal input.ReleaseCount 2 "input final release count"
            Expect.equal input.ActiveCount 0 "active input count after final release"
            Expect.equal innerSpy.ReleaseCount 1 "inner final release count"
            Expect.equal inner.DestroyCount 1 "inner final destruction count"
            Expect.isFalse inner.IsAllocated "inner retained after final release"

            output.Acquire()
            let second = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            Expect.equal input.AcquireCount 3 "input reacquisition count"
            Expect.equal mappingCount 2 "mapping did not rerun after reacquisition"
            Expect.equal innerSpy.AcquireCount 2 "inner reacquisition count"
            Expect.equal inner.CreateCount 2 "inner recreation count"
            Expect.equal inner.ComputeCount 2 "inner recomputation count"
            Expect.equal second.Generation 2 "reacquisition did not create a fresh handle"
            Expect.equal second.Token RenderToken.Empty "reacquired render token was not propagated"

            output.Release()
            Expect.equal input.ReleaseCount 3 "input release count after reacquisition"
            Expect.equal innerSpy.ReleaseCount 2 "inner release count after reacquisition"
            Expect.equal inner.DestroyCount 2 "inner destroy count after reacquisition"

        let bindAdoptsOwnerlessInnerOnFirstAcquire() =

            let input = InputSpy(0)
            let inner, innerSpy, innerValue = createResource "ownerless"
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    innerValue
                )
                |> asResource

            let token = RenderToken.Zero
            let first = output.GetValue(AdaptiveToken.Top, token)

            Expect.equal first.Name "ownerless" "unexpected ownerless resource"
            Expect.isTrue (Object.ReferenceEquals(first.Token, token)) "ownerless render token was not propagated"
            Expect.equal mappingCount 1 "ownerless mapping count"
            Expect.equal input.AcquireCount 0 "ownerless pull acquired the input"
            Expect.equal innerSpy.AcquireCount 1 "ownerless pull did not retain the inner"
            Expect.equal inner.CreateCount 1 "ownerless pull did not create the inner"

            output.Acquire()

            Expect.equal input.AcquireCount 1 "first owner acquisition did not acquire the input"
            Expect.equal input.ActiveCount 1 "first owner input count"
            Expect.equal innerSpy.AcquireCount 1 "first owner did not adopt the retained inner"
            Expect.equal inner.CreateCount 1 "first owner recreated the retained inner"

            let adopted = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)
            Expect.equal adopted.Generation 1 "first owner did not reuse the ownerless handle"
            Expect.isTrue (Object.ReferenceEquals(adopted.Token, token)) "first owner recomputed the ownerless handle"
            Expect.equal mappingCount 1 "first owner reran the ownerless mapping"

            output.Release()

            Expect.equal input.ReleaseCount 1 "adopting owner input release count"
            Expect.equal innerSpy.ReleaseCount 1 "adopted inner release count"
            Expect.equal inner.DestroyCount 1 "adopted inner destruction count"

        let bindReleaseAllPreservesSharedInnerOwner() =

            let input = InputSpy(0)
            let inner, innerSpy, innerValue = createResource "shared"
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    innerValue
                )
                |> asResource

            input.Acquire()
            innerSpy.Acquire()
            output.Acquire()
            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore

            Expect.equal input.AcquireCount 3 "shared input acquisition count"
            Expect.equal input.ActiveCount 3 "shared input active count"
            Expect.equal innerSpy.AcquireCount 2 "shared inner acquisition count"
            Expect.equal inner.CreateCount 1 "shared inner creation count"
            Expect.equal mappingCount 1 "mapping count before ReleaseAll"

            output.ReleaseAll()

            Expect.equal input.AcquireCount 3 "input acquisition count before ReleaseAll"
            Expect.equal input.ReleaseCount 2 "ReleaseAll did not balance captured input references"
            Expect.equal input.ReleaseAllCount 0 "bind reset input references owned by other users"
            Expect.equal input.ActiveCount 1 "external input ownership was not preserved"
            Expect.equal innerSpy.ReleaseCount 1 "bind did not release its inner reference"
            Expect.equal innerSpy.ReleaseAllCount 0 "bind reset references owned by other users"
            Expect.equal inner.DestroyCount 0 "shared inner was destroyed"
            Expect.isTrue inner.IsAllocated "shared inner allocation was not preserved"

            output.Acquire()
            let reacquired = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            Expect.equal reacquired.Name "shared" "ReleaseAll reacquisition returned an unexpected inner"
            Expect.equal mappingCount 2 "ReleaseAll did not invalidate the cached mapping"
            Expect.equal input.AcquireCount 4 "ReleaseAll input reacquisition count"
            Expect.equal input.ActiveCount 2 "ReleaseAll input active count after reacquisition"
            Expect.equal innerSpy.AcquireCount 3 "ReleaseAll inner reacquisition count"
            Expect.equal innerSpy.ReleaseCount 1 "ReleaseAll inner was released during reacquisition"
            Expect.equal inner.CreateCount 1 "externally owned inner was recreated during reacquisition"
            Expect.equal inner.ComputeCount 1 "externally owned inner was recomputed during reacquisition"

            output.Release()

            Expect.equal input.ReleaseCount 3 "reacquired bind input release count"
            Expect.equal input.ActiveCount 1 "reacquired bind did not preserve external input ownership"
            Expect.equal innerSpy.ReleaseCount 2 "reacquired bind inner release count"
            Expect.equal inner.DestroyCount 0 "reacquired bind destroyed the externally owned inner"

            input.Release()
            innerSpy.Release()
            Expect.equal input.ReleaseCount 4 "external input release count"
            Expect.equal input.ActiveCount 0 "external input reference was not balanced"
            Expect.equal innerSpy.ReleaseCount 3 "external inner release count"
            Expect.equal inner.DestroyCount 1 "shared inner final destruction count"

        let bindSwitchesOnceWhileMultiplyAcquired() =

            let events = ResizeArray()
            let input = InputSpy(0)
            let a, aSpy, aValue = createResourceWithEvents events "A"
            let b, bSpy, bValue = createResourceWithEvents events "B"
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun value ->
                    mappingCount <- mappingCount + 1
                    if value = 0 then aValue else bValue
                )
                |> asResource

            output.Acquire()
            output.Acquire()
            let first = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)
            Expect.equal first.Name "A" "unexpected initial resource"

            events.Clear()
            input.Set 1
            let switchToken = RenderToken.Zero
            let second = output.GetValue(AdaptiveToken.Top, switchToken)

            Expect.equal second.Name "B" "resource did not switch"
            Expect.isTrue (Object.ReferenceEquals(second.Token, switchToken)) "switch render token was not propagated"
            Expect.equal mappingCount 2 "mapping count after switch"
            Expect.sequenceEqual events ["B.acquire"; "A.release"] "inner switch was not acquire-before-release"
            Expect.equal aSpy.AcquireCount 1 "old inner acquisition count"
            Expect.equal aSpy.ReleaseCount 1 "old inner release count"
            Expect.equal a.DestroyCount 1 "old inner destruction count"
            Expect.equal bSpy.AcquireCount 1 "new inner acquisition count"
            Expect.equal b.CreateCount 1 "new inner creation count"

            output.Release()
            Expect.equal input.ReleaseCount 1 "input partial release count"
            Expect.equal bSpy.ReleaseCount 0 "new inner released by partial release"
            Expect.equal b.DestroyCount 0 "new inner destroyed by partial release"

            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            Expect.equal mappingCount 2 "unchanged switched resource reran mapping"
            Expect.equal b.ComputeCount 1 "unchanged switched resource recomputed"

            output.Release()
            Expect.equal input.ReleaseCount 2 "input final release count"
            Expect.equal bSpy.ReleaseCount 1 "new inner final release count"
            Expect.equal b.DestroyCount 1 "new inner final destruction count"

        let bindUnchangedInnerDoesNotChurn() =

            let input = InputSpy(0)
            let inner, innerSpy, innerValue = createResource "stable"
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    innerValue
                )
                |> asResource

            output.Acquire()
            output.Acquire()
            let first = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            input.Set 0
            let shallowEqual = output.GetValue(AdaptiveToken.Top, RenderToken.Zero)

            Expect.equal shallowEqual first "shallow-equal input returned a different handle"
            Expect.equal mappingCount 1 "shallow-equal input reran mapping"
            Expect.equal innerSpy.AcquireCount 1 "shallow-equal input reacquired the inner"
            Expect.equal innerSpy.ReleaseCount 0 "shallow-equal input released the inner"
            Expect.equal inner.CreateCount 1 "shallow-equal input recreated the inner"
            Expect.equal inner.DestroyCount 0 "shallow-equal input destroyed the inner"
            Expect.equal inner.ComputeCount 1 "shallow-equal input recomputed the inner"

            input.Set 1
            let second = output.GetValue(AdaptiveToken.Top, RenderToken.Zero)

            Expect.equal first second "unchanged inner returned a different handle"
            Expect.equal mappingCount 2 "input change did not rerun mapping"
            Expect.equal innerSpy.AcquireCount 1 "unchanged inner was reacquired"
            Expect.equal innerSpy.ReleaseCount 0 "unchanged inner was released"
            Expect.equal inner.CreateCount 1 "unchanged inner was recreated"
            Expect.equal inner.DestroyCount 0 "unchanged inner was destroyed"
            Expect.equal inner.ComputeCount 1 "unchanged inner was recomputed"

            output.Release()
            Expect.equal innerSpy.ReleaseCount 0 "unchanged inner released by partial release"
            output.Release()
            Expect.equal innerSpy.ReleaseCount 1 "unchanged inner final release count"
            Expect.equal inner.DestroyCount 1 "unchanged inner final destruction count"

        let bindMappingFailurePreservesInnerAndRetries() =

            let events = ResizeArray()
            let input = InputSpy(0)
            let a, aSpy, aValue = createResourceWithEvents events "A"
            let b, bSpy, bValue = createResourceWithEvents events "B"
            let mutable mappingCount = 0
            let mutable failNext = true

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun value ->
                    mappingCount <- mappingCount + 1
                    if value = 0 then
                        aValue
                    elif failNext then
                        failNext <- false
                        failwith "expected mapping failure"
                    else
                        bValue
                )
                |> asResource

            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            events.Clear()
            input.Set 1

            Expect.throws
                (fun _ -> output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore)
                "switch mapping did not fail"

            Expect.equal mappingCount 2 "mapping failure count"
            Expect.isEmpty events "mapping failure changed acquired resources"
            Expect.equal aSpy.ReleaseCount 0 "mapping failure released the current inner"
            Expect.equal a.DestroyCount 0 "mapping failure destroyed the current inner"
            Expect.isTrue a.IsAllocated "mapping failure lost the current inner"
            Expect.equal bSpy.AcquireCount 0 "mapping failure acquired the replacement"
            Expect.equal b.CreateCount 0 "mapping failure created the replacement"

            let retryToken = RenderToken.Zero
            let result = output.GetValue(AdaptiveToken.Top, retryToken)
            Expect.equal result.Name "B" "mapping retry did not switch"
            Expect.isTrue (Object.ReferenceEquals(result.Token, retryToken)) "retry render token was not propagated"
            Expect.equal mappingCount 3 "mapping retry count"
            Expect.sequenceEqual events ["B.acquire"; "A.release"] "retry switch order"
            Expect.equal aSpy.ReleaseCount 1 "retry did not release old inner"
            Expect.equal a.DestroyCount 1 "retry did not destroy old inner"
            Expect.equal bSpy.AcquireCount 1 "retry did not acquire replacement"
            Expect.equal b.CreateCount 1 "retry did not create replacement"

            output.Release()
            Expect.equal input.ReleaseCount 1 "input release count after retry"
            Expect.equal bSpy.ReleaseCount 1 "replacement release count"
            Expect.equal b.DestroyCount 1 "replacement destruction count"

        let bindCandidateAcquireFailurePreservesInnerAndRetries() =

            let input = InputSpy(0)
            let old, oldSpy, oldValue = createResource "old"
            let candidate = CountingResource("candidate")
            let failingCandidate = FailingAcquireResource(candidate :> IAdaptiveResource<_>)
            let candidateSpy = ResourceSpy("candidate", failingCandidate :> IAdaptiveResource<_>)
            let candidateValue = asAVal candidateSpy
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun value ->
                    mappingCount <- mappingCount + 1
                    if value = 0 then oldValue else candidateValue
                )
                |> asResource

            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            input.Set 1

            Expect.throws
                (fun _ -> output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore)
                "candidate acquisition did not fail"

            Expect.equal mappingCount 2 "failed candidate mapping count"
            Expect.equal candidateSpy.AcquireCount 1 "failed candidate acquisition count"
            Expect.equal failingCandidate.AcquireAttemptCount 1 "failed candidate acquisition attempt count"
            Expect.equal candidate.CreateCount 0 "failed candidate creation count"
            Expect.equal candidate.ComputeCount 0 "failed candidate computation count"
            Expect.equal candidateSpy.ReleaseCount 0 "failed candidate leaked a counted reference"
            Expect.equal candidate.DestroyCount 0 "failed candidate was spuriously destroyed"
            Expect.isFalse candidate.IsAllocated "failed candidate remained allocated"
            Expect.equal oldSpy.ReleaseCount 0 "failed candidate released the old inner"
            Expect.equal old.DestroyCount 0 "failed candidate destroyed the old inner"
            Expect.isTrue old.IsAllocated "failed candidate lost the old inner"

            let result = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            Expect.equal result.Name "candidate" "candidate retry did not switch"
            Expect.equal mappingCount 3 "candidate retry mapping count"
            Expect.equal candidateSpy.AcquireCount 2 "candidate retry acquisition count"
            Expect.equal failingCandidate.AcquireAttemptCount 2 "candidate retry acquisition attempt count"
            Expect.equal candidate.CreateCount 1 "candidate retry creation count"
            Expect.equal candidate.ComputeCount 1 "candidate retry computation count"
            Expect.equal candidateSpy.ReleaseCount 0 "candidate retry released the live candidate"
            Expect.equal candidate.DestroyCount 0 "candidate retry destroyed the live candidate"
            Expect.equal oldSpy.ReleaseCount 1 "candidate retry did not release the old inner"
            Expect.equal old.DestroyCount 1 "candidate retry did not destroy the old inner"

            output.Release()

            Expect.equal input.ReleaseCount 1 "candidate retry input release count"
            Expect.equal candidateSpy.ReleaseCount 1 "candidate final release count"
            Expect.equal candidate.DestroyCount 1 "candidate final destruction count"
            Expect.isFalse candidate.IsAllocated "candidate remained allocated after final release"

        let bindCandidateValueFailurePreservesInnerAndRetries() =

            let input = InputSpy(0)
            let old, oldSpy, oldValue = createResource "value-old"
            let candidate = FaultingResource("value-candidate", FailCompute)
            let candidateSpy = ResourceSpy("value-candidate", candidate)
            let candidateValue = asAVal candidateSpy
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun value ->
                    mappingCount <- mappingCount + 1
                    if value = 0 then oldValue else candidateValue
                )
                |> asResource

            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            input.Set 1

            Expect.throws
                (fun _ -> output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore)
                "candidate value computation did not fail"

            Expect.equal mappingCount 2 "failed candidate value mapping count"
            Expect.equal candidateSpy.AcquireCount 1 "failed candidate value acquisition count"
            Expect.equal candidateSpy.ReleaseCount 1 "failed candidate value cleanup release count"
            Expect.equal candidate.CreateCount 1 "failed candidate value creation count"
            Expect.equal candidate.ComputeAttemptCount 1 "failed candidate value computation attempt count"
            Expect.equal candidate.ComputeCount 0 "failed candidate value successful computation count"
            Expect.equal candidate.DestroyAttemptCount 1 "failed candidate value destruction attempt count"
            Expect.equal candidate.DestroyCount 1 "failed candidate value cleanup destruction count"
            Expect.isFalse candidate.IsAllocated "failed candidate value remained allocated"
            Expect.equal oldSpy.ReleaseCount 0 "failed candidate value released the old inner"
            Expect.equal old.DestroyCount 0 "failed candidate value destroyed the old inner"
            Expect.isTrue old.IsAllocated "failed candidate value lost the old inner"

            let retryToken = RenderToken.Zero
            let result = output.GetValue(AdaptiveToken.Top, retryToken)

            Expect.equal result.Name "value-candidate" "candidate value retry did not switch"
            Expect.equal result.Generation 2 "candidate value retry did not create a fresh generation"
            Expect.isTrue (Object.ReferenceEquals(result.Token, retryToken)) "candidate value retry render token was not propagated"
            Expect.equal mappingCount 3 "candidate value retry mapping count"
            Expect.equal candidateSpy.AcquireCount 2 "candidate value retry acquisition count"
            Expect.equal candidateSpy.ReleaseCount 1 "candidate value retry released the live candidate"
            Expect.equal candidate.CreateCount 2 "candidate value retry creation count"
            Expect.equal candidate.ComputeAttemptCount 2 "candidate value retry computation attempt count"
            Expect.equal candidate.ComputeCount 1 "candidate value retry successful computation count"
            Expect.equal candidate.DestroyAttemptCount 1 "candidate value retry destroyed the live candidate"
            Expect.equal candidate.DestroyCount 1 "candidate value retry cleanup count"
            Expect.isTrue candidate.IsAllocated "candidate value retry did not retain the candidate"
            Expect.equal oldSpy.ReleaseCount 1 "candidate value retry did not release the old inner"
            Expect.equal old.DestroyCount 1 "candidate value retry did not destroy the old inner"

            output.Release()

            Expect.equal input.ReleaseCount 1 "candidate value retry input release count"
            Expect.equal input.ActiveCount 0 "candidate value retry retained the input"
            Expect.equal candidateSpy.ReleaseCount 2 "candidate value final release count"
            Expect.equal candidate.DestroyAttemptCount 2 "candidate value final destruction attempt count"
            Expect.equal candidate.DestroyCount 2 "candidate value final destruction count"
            Expect.isFalse candidate.IsAllocated "candidate value remained allocated after final release"

        let bindRetirementFailureRestoresLifecycle() =

            let input = InputSpy(0)
            let old = FaultingResource("retirement-old", FailDestroy)
            let oldSpy = ResourceSpy("retirement-old", old)
            let oldValue = asAVal oldSpy
            let replacement, replacementSpy, replacementValue = createResource "retirement-new"
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun value ->
                    mappingCount <- mappingCount + 1
                    if value = 0 then oldValue else replacementValue
                )
                |> asResource

            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            input.Set 1

            Expect.throws
                (fun _ -> output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore)
                "old-inner destruction did not fail"

            Expect.equal mappingCount 2 "retirement failure mapping count"
            Expect.equal oldSpy.AcquireCount 1 "retirement old-inner acquisition count"
            Expect.equal oldSpy.ReleaseCount 1 "retirement old-inner release count"
            Expect.equal old.DestroyAttemptCount 1 "retirement old-inner destruction attempt count"
            Expect.equal old.DestroyCount 0 "retirement old-inner unexpectedly reported successful destruction"
            Expect.isFalse old.IsAllocated "retirement old-inner remained allocated after failing destruction"
            Expect.equal replacementSpy.AcquireCount 1 "retirement replacement acquisition count"
            Expect.equal replacementSpy.ReleaseCount 0 "retirement failure released the replacement"
            Expect.equal replacement.CreateCount 1 "retirement replacement creation count"
            Expect.equal replacement.ComputeCount 1 "retirement replacement computation count"
            Expect.equal replacement.DestroyCount 0 "retirement failure destroyed the replacement"
            Expect.isTrue replacement.IsAllocated "retirement failure lost the replacement"

            let recovered = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            Expect.equal recovered.Name "retirement-new" "retirement failure recovery returned an unexpected inner"
            Expect.equal mappingCount 2 "retirement failure recovery reran mapping"
            Expect.equal oldSpy.ReleaseCount 1 "retirement failure recovery released the old inner twice"
            Expect.equal old.DestroyAttemptCount 1 "retirement failure recovery retried old-inner destruction"
            Expect.equal replacementSpy.AcquireCount 1 "retirement failure recovery reacquired the replacement"
            Expect.equal replacementSpy.ReleaseCount 0 "retirement failure recovery released the replacement"
            Expect.equal replacement.ComputeCount 1 "retirement failure recovery recomputed the replacement"

            output.Release()

            Expect.equal input.ReleaseCount 1 "retirement failure input release count"
            Expect.equal input.ActiveCount 0 "retirement failure retained the input"
            Expect.equal oldSpy.ReleaseCount 1 "retirement final cleanup released the old inner twice"
            Expect.equal old.DestroyAttemptCount 1 "retirement final cleanup retried old-inner destruction"
            Expect.equal replacementSpy.ReleaseCount 1 "retirement replacement final release count"
            Expect.equal replacement.DestroyCount 1 "retirement replacement final destruction count"

        let bindFinalReleaseNotifiesNonResourceInputCallback() =

            let input = cval 0
            let resources = ResizeArray<struct (CountingResource * ResourceSpy<ResourceHandle>)>()
            let callbackValues = ResizeArray<ResourceHandle>()
            let mutable mappingCount = 0

            let output =
                input
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    let resource = CountingResource($"notification-{mappingCount}")
                    let spy = ResourceSpy($"notification-{mappingCount}", resource)
                    resources.Add(struct (resource, spy))
                    asAVal spy
                )
                |> asResource

            output.Acquire()
            let initial = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)
            let subscription = (output :> aval<ResourceHandle>).AddCallback(callbackValues.Add)

            Expect.equal initial.Name "notification-1" "notification callback initial value"
            Expect.equal callbackValues.Count 1 "notification callback was not initialized"
            Expect.equal callbackValues.[0] initial "notification callback initialization value"
            Expect.equal mappingCount 1 "notification callback registration reran mapping"

            output.Release()

            Expect.equal callbackValues.Count 2 "final release did not notify the callback"
            Expect.equal callbackValues.[1].Name "notification-2" "final release callback did not observe a fresh inner"
            Expect.equal mappingCount 2 "final release callback mapping count"
            Expect.equal resources.Count 2 "final release callback resource count"

            let struct (first, firstSpy) = resources.[0]
            let struct (second, secondSpy) = resources.[1]

            Expect.equal firstSpy.AcquireCount 1 "notification first inner acquisition count"
            Expect.equal firstSpy.ReleaseCount 1 "notification first inner release count"
            Expect.equal first.DestroyCount 1 "notification first inner destruction count"
            Expect.equal secondSpy.AcquireCount 1 "notification fresh inner acquisition count"
            Expect.equal secondSpy.ReleaseCount 0 "notification fresh inner was prematurely released"
            Expect.equal second.CreateCount 1 "notification fresh inner creation count"
            Expect.equal second.DestroyCount 0 "notification fresh inner was prematurely destroyed"

            subscription.Dispose()
            output.ReleaseAll()

            Expect.equal callbackValues.Count 2 "disposed notification callback was invoked during cleanup"
            Expect.equal mappingCount 2 "notification cleanup reran mapping"
            Expect.equal secondSpy.ReleaseCount 1 "notification fresh inner cleanup release count"
            Expect.equal second.DestroyCount 1 "notification fresh inner cleanup destruction count"

        let bindSwitchKeepsSharedInnerOutputEdge() =

            let selector = InputSpy(0)
            let backing = MutableResource("shared-output", 1)
            let backingResource = backing :> IAdaptiveResource<int>
            let first = ResourceSpy("first-wrapper", backingResource)
            let second = ResourceSpy("second-wrapper", backingResource)
            let mutable mappingCount = 0

            let output =
                selector
                |> asAVal
                |> AdaptiveResource.bind (fun value ->
                    mappingCount <- mappingCount + 1
                    if value = 0 then asAVal first else asAVal second
                )
                |> asResource

            output.Acquire()
            Expect.equal (output.GetValue(AdaptiveToken.Top, RenderToken.Empty)) 1 "shared-output initial value"

            selector.Set 1
            Expect.equal (output.GetValue(AdaptiveToken.Top, RenderToken.Empty)) 1 "shared-output switched value"
            Expect.equal mappingCount 2 "shared-output switch mapping count"
            Expect.equal first.AcquireCount 1 "first shared-output wrapper acquisition count"
            Expect.equal first.ReleaseCount 1 "first shared-output wrapper release count"
            Expect.equal second.AcquireCount 1 "second shared-output wrapper acquisition count"
            Expect.equal backing.DestroyCount 0 "shared backing was destroyed during wrapper switch"

            backing.Set 7
            Expect.equal (output.GetValue(AdaptiveToken.Top, RenderToken.Empty)) 7 "shared output edge was detached"
            Expect.equal mappingCount 2 "inner-only change reran the outer mapping"
            Expect.equal backing.ComputeCount 2 "shared backing did not recompute after invalidation"

            output.Release()
            Expect.equal second.ReleaseCount 1 "second shared-output wrapper release count"
            Expect.equal backing.DestroyCount 1 "shared backing final destruction count"

        let bindSwitchKeepsDirectInputOutputEdge() =

            let selector = InputSpy(0)
            let directInput = MutableResource("direct-input", 1)
            let directValue = asAVal (directInput :> IAdaptiveResource<int>)
            let replacement = MutableResource("replacement", 100)
            let replacementSpy = ResourceSpy("replacement", replacement)
            let replacementValue = asAVal replacementSpy
            let mutable mappingCount = 0

            let output =
                AdaptiveResource.bind2
                    (fun selection _ ->
                        mappingCount <- mappingCount + 1
                        if selection = 0 then directValue else replacementValue
                    )
                    (asAVal selector)
                    directValue
                |> asResource

            output.Acquire()
            Expect.equal (output.GetValue(AdaptiveToken.Top, RenderToken.Empty)) 1 "direct-input initial value"

            selector.Set 1
            Expect.equal (output.GetValue(AdaptiveToken.Top, RenderToken.Empty)) 100 "direct-input switched value"
            Expect.equal mappingCount 2 "direct-input switch mapping count"
            Expect.equal directInput.DestroyCount 0 "direct input was destroyed when deselected"
            Expect.equal replacementSpy.AcquireCount 1 "replacement acquisition count"

            directInput.Set 2
            Expect.equal (output.GetValue(AdaptiveToken.Top, RenderToken.Empty)) 100 "direct-input mutation changed selected value"
            Expect.equal mappingCount 3 "deselected direct input no longer invalidated the bind"
            Expect.equal directInput.ComputeCount 2 "direct input did not recompute after mutation"
            Expect.equal replacementSpy.AcquireCount 1 "unchanged replacement was reacquired"
            Expect.equal replacementSpy.ReleaseCount 0 "unchanged replacement was released"

            output.Release()
            Expect.equal replacementSpy.ReleaseCount 1 "replacement final release count"
            Expect.equal replacement.DestroyCount 1 "replacement final destruction count"
            Expect.equal directInput.DestroyCount 1 "direct input final destruction count"

        let bind2BalancesInputsAndCoalescesSwitch() =

            let input1 = InputSpy(0)
            let input2 = InputSpy(0)
            let a, aSpy, aValue = createResource "A"
            let b, bSpy, bValue = createResource "B"
            let mutable mappingCount = 0

            let output =
                AdaptiveResource.bind2
                    (fun value1 value2 ->
                        mappingCount <- mappingCount + 1
                        if value1 + value2 = 0 then aValue else bValue
                    )
                    (asAVal input1)
                    (asAVal input2)
                |> asResource

            output.Acquire()
            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore

            Expect.equal input1.AcquireCount 2 "first bind2 input acquisition count"
            Expect.equal input2.AcquireCount 2 "second bind2 input acquisition count"
            Expect.equal mappingCount 1 "bind2 initial mapping count"

            transact (fun _ ->
                input1.Value <- 1
                input2.Value <- 1
            )
            let result = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            Expect.equal result.Name "B" "bind2 did not switch"
            Expect.equal mappingCount 2 "bind2 did not coalesce the input transaction"
            Expect.equal aSpy.ReleaseCount 1 "bind2 old inner release count"
            Expect.equal a.DestroyCount 1 "bind2 old inner destruction count"
            Expect.equal bSpy.AcquireCount 1 "bind2 new inner acquisition count"
            Expect.equal b.CreateCount 1 "bind2 new inner creation count"

            output.Release()
            Expect.equal input1.ReleaseCount 1 "first bind2 input partial release count"
            Expect.equal input2.ReleaseCount 1 "second bind2 input partial release count"
            Expect.equal bSpy.ReleaseCount 0 "bind2 inner released by partial release"

            output.Release()
            Expect.equal input1.ReleaseCount 2 "first bind2 input final release count"
            Expect.equal input2.ReleaseCount 2 "second bind2 input final release count"
            Expect.equal input1.ActiveCount 0 "first bind2 input active count"
            Expect.equal input2.ActiveCount 0 "second bind2 input active count"
            Expect.equal bSpy.ReleaseCount 1 "bind2 inner final release count"
            Expect.equal b.DestroyCount 1 "bind2 inner final destruction count"

        let bind3TracksThirdInputAndUnchangedInner() =

            let input1 = InputSpy(0)
            let input2 = InputSpy(0)
            let input3 = InputSpy(0)
            let a, aSpy, aValue = createResource "A"
            let b, bSpy, bValue = createResource "B"
            let mutable mappingCount = 0

            let output =
                AdaptiveResource.bind3
                    (fun value1 value2 value3 ->
                        mappingCount <- mappingCount + 1
                        if value1 + value2 + value3 < 2 then aValue else bValue
                    )
                    (asAVal input1)
                    (asAVal input2)
                    (asAVal input3)
                |> asResource

            output.Acquire()
            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore

            Expect.equal input1.AcquireCount 2 "first bind3 input acquisition count"
            Expect.equal input2.AcquireCount 2 "second bind3 input acquisition count"
            Expect.equal input3.AcquireCount 2 "third bind3 input acquisition count"
            Expect.equal mappingCount 1 "bind3 initial mapping count"

            input3.Set 1
            let unchanged = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            Expect.equal unchanged.Name "A" "bind3 third input selected an unexpected inner"
            Expect.equal mappingCount 2 "bind3 did not observe the third input"
            Expect.equal aSpy.AcquireCount 1 "bind3 unchanged inner was reacquired"
            Expect.equal aSpy.ReleaseCount 0 "bind3 unchanged inner was released"
            Expect.equal a.CreateCount 1 "bind3 unchanged inner was recreated"
            Expect.equal a.DestroyCount 0 "bind3 unchanged inner was destroyed"

            input1.Set 1
            let switched = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            Expect.equal switched.Name "B" "bind3 did not switch"
            Expect.equal mappingCount 3 "bind3 switch mapping count"
            Expect.equal aSpy.ReleaseCount 1 "bind3 old inner release count"
            Expect.equal a.DestroyCount 1 "bind3 old inner destruction count"
            Expect.equal bSpy.AcquireCount 1 "bind3 new inner acquisition count"
            Expect.equal b.CreateCount 1 "bind3 new inner creation count"

            output.Release()
            Expect.equal input1.ReleaseCount 1 "first bind3 input partial release count"
            Expect.equal input2.ReleaseCount 1 "second bind3 input partial release count"
            Expect.equal input3.ReleaseCount 1 "third bind3 input partial release count"
            Expect.equal bSpy.ReleaseCount 0 "bind3 inner released by partial release"

            output.Release()
            Expect.equal input1.ReleaseCount 2 "first bind3 input final release count"
            Expect.equal input2.ReleaseCount 2 "second bind3 input final release count"
            Expect.equal input3.ReleaseCount 2 "third bind3 input final release count"
            Expect.equal input1.ActiveCount 0 "first bind3 input active count"
            Expect.equal input2.ActiveCount 0 "second bind3 input active count"
            Expect.equal input3.ActiveCount 0 "third bind3 input active count"
            Expect.equal bSpy.ReleaseCount 1 "bind3 inner final release count"
            Expect.equal b.DestroyCount 1 "bind3 inner final destruction count"

        let bindSerializesSwitchAndFinalRelease() =

            use mappingEntered = new ManualResetEventSlim(false)
            use continueMapping = new ManualResetEventSlim(false)
            use releaseStarted = new ManualResetEventSlim(false)
            use releaseFinished = new ManualResetEventSlim(false)

            let input = InputSpy(0)
            let a, aSpy, aValue = createResource "A"
            let b, bSpy, bValue = createResource "B"

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun value ->
                    if value = 0 then
                        aValue
                    else
                        mappingEntered.Set()
                        if not <| continueMapping.Wait(TimeSpan.FromSeconds 10.0) then
                            failwith "Timed out waiting to continue the mapping"
                        bValue
                )
                |> asResource

            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            input.Set 1

            let getTask =
                Task.Run(fun () -> output.GetValue(AdaptiveToken.Top, RenderToken.Empty))

            Expect.isTrue (mappingEntered.Wait(TimeSpan.FromSeconds 10.0)) "mapping did not start"

            let releaseTask =
                Task.Run(fun () ->
                    releaseStarted.Set()
                    try output.Release()
                    finally releaseFinished.Set()
                )

            try
                Expect.isTrue (releaseStarted.Wait(TimeSpan.FromSeconds 10.0)) "release did not start"
                Expect.isFalse (releaseFinished.Wait(TimeSpan.FromMilliseconds 250.0)) "release was not serialized with the active mapping"
            finally
                continueMapping.Set()

            let completed =
                Task.WaitAll(
                    [| getTask :> Task; releaseTask |],
                    TimeSpan.FromSeconds 10.0
                )

            Expect.isTrue completed "concurrent switch and release did not complete"
            Expect.equal getTask.Result.Name "B" "concurrent mapping returned an unexpected inner"
            Expect.equal aSpy.AcquireCount 1 "concurrent old inner acquisition count"
            Expect.equal aSpy.ReleaseCount 1 "concurrent old inner release count"
            Expect.equal a.DestroyCount 1 "concurrent old inner destruction count"
            Expect.equal bSpy.AcquireCount 1 "concurrent new inner acquisition count"
            Expect.equal bSpy.ReleaseCount 1 "concurrent new inner release count"
            Expect.equal b.DestroyCount 1 "concurrent new inner destruction count"
            Expect.equal input.ReleaseCount 1 "concurrent input release count"
            Expect.equal input.ActiveCount 0 "concurrent input reference was retained"

        let bindAcquireDoesNotInvertInputLock() =

            use destroyEntered = new ManualResetEventSlim(false)
            use continueDestroy = new ManualResetEventSlim(false)
            use acquireStarted = new ManualResetEventSlim(false)

            let actualInput = BlockingInputResource(destroyEntered, continueDestroy)
            let input = AcquireSignalResource<int>(actualInput, acquireStarted) :> IAdaptiveResource<int>
            let inner, innerSpy, innerValue = createResource "lock-inner"
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    innerValue
                )
                |> asResource

            actualInput.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            actualInput.BlockNextDestroy()

            let releaseTask = Task.Run(fun () -> actualInput.Release())

            Expect.isTrue (destroyEntered.Wait(TimeSpan.FromSeconds 10.0)) "input destruction did not start"

            let acquireTask = Task.Run(fun () -> output.Acquire())

            try
                Expect.isTrue (acquireStarted.Wait(TimeSpan.FromSeconds 10.0)) "bind acquisition did not reach the input"
            finally
                continueDestroy.Set()

            let completed =
                Task.WaitAll(
                    [| releaseTask; acquireTask |],
                    TimeSpan.FromSeconds 10.0
                )

            Expect.isTrue completed "input release and bind acquisition deadlocked"
            Expect.equal actualInput.CreateCount 2 "input recreation count after concurrent acquire"
            Expect.equal actualInput.DestroyCount 1 "input destruction count after concurrent acquire"

            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            Expect.equal mappingCount 2 "input recreation did not invalidate the bind"
            Expect.equal actualInput.ComputeCount 2 "recreated input computation count"
            Expect.equal innerSpy.AcquireCount 1 "lock test inner was reacquired"
            Expect.equal innerSpy.ReleaseCount 0 "lock test inner was prematurely released"

            output.Release()
            Expect.equal actualInput.DestroyCount 2 "input final destruction count after lock test"
            Expect.equal innerSpy.ReleaseCount 1 "lock test inner final release count"
            Expect.equal inner.DestroyCount 1 "lock test inner final destruction count"

        let bindSelectedAcquireDoesNotInvertDirectInputLock() =

            use inputAcquireBlocked = new ManualResetEventSlim(false)
            use continueInputAcquire = new ManualResetEventSlim(false)
            use resultAcquireStarted = new ManualResetEventSlim(false)

            let input = LockingInputResource(inputAcquireBlocked, continueInputAcquire)
            let inputResource = input :> IAdaptiveResource<int>
            let result = InputDependentResource(inputResource, resultAcquireStarted)
            let resultValue = asAVal (result :> IAdaptiveResource<int>)

            let output =
                inputResource
                |> asAVal
                |> AdaptiveResource.bind (fun _ -> resultValue)
                |> asResource

            input.SetContinuation(fun () -> lock output (fun _ -> ()))

            let acquireTask = Task.Run(fun () -> output.Acquire())

            Expect.isTrue (inputAcquireBlocked.Wait(TimeSpan.FromSeconds 10.0)) "direct input acquisition did not block"

            let getTask =
                Task.Run(fun () -> output.GetValue(AdaptiveToken.Top, RenderToken.Empty))

            try
                Expect.isTrue (resultAcquireStarted.Wait(TimeSpan.FromSeconds 10.0)) "selected result acquisition did not start while the outer input acquisition was blocked"
                Expect.isFalse acquireTask.IsCompleted "outer acquisition completed before its input was released"
                Expect.isFalse getTask.IsCompleted "selected result acquisition did not wait for its direct input"
            finally
                continueInputAcquire.Set()

            let completed =
                Task.WaitAll(
                    [| acquireTask; getTask :> Task |],
                    TimeSpan.FromSeconds 10.0
                )

            Expect.isTrue completed "selected-result acquisition deadlocked with its direct input"
            Expect.equal getTask.Result 42 "selected-result lock test value"
            Expect.equal input.AcquireCount 2 "selected-result dependent input acquisition count"
            Expect.equal input.ActiveCount 2 "selected-result dependent input active count"
            Expect.equal result.CreateCount 1 "selected-result creation count"

            output.Release()
            Expect.equal result.DestroyCount 1 "selected-result destruction count"
            Expect.equal input.ReleaseCount 2 "selected-result dependent input release count"
            Expect.equal input.ActiveCount 0 "selected-result dependent input reference leak"

        let bindDirtyGetValueSupportsCallerHeldMonitor() =

            let input = InputSpy(0)
            let a, aSpy, aValue = createResource "locked-dirty-A"
            let b, bSpy, bValue = createResource "locked-dirty-B"
            let mutable mappingCount = 0

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun value ->
                    mappingCount <- mappingCount + 1
                    if value = 0 then aValue else bValue
                )
                |> asResource

            output.Acquire()
            Expect.equal (output.GetValue(AdaptiveToken.Top, RenderToken.Empty)).Name "locked-dirty-A" "caller-lock initial value"
            input.Set 1

            let evaluation =
                Task.Run(fun () ->
                    lock output (fun () ->
                        output.GetValue(AdaptiveToken.Top, RenderToken.Empty)
                    )
                )

            Expect.isTrue (evaluation.Wait(TimeSpan.FromSeconds 10.0)) "dirty GetValue deadlocked while its caller held the bind monitor"
            Expect.equal evaluation.Result.Name "locked-dirty-B" "caller-lock dirty evaluation returned an unexpected inner"
            Expect.equal mappingCount 2 "caller-lock dirty evaluation mapping count"
            Expect.equal aSpy.ReleaseCount 1 "caller-lock dirty evaluation did not release the old inner"
            Expect.equal a.DestroyCount 1 "caller-lock dirty evaluation did not destroy the old inner"
            Expect.equal bSpy.AcquireCount 1 "caller-lock dirty evaluation did not acquire the replacement"
            Expect.equal b.CreateCount 1 "caller-lock dirty evaluation did not create the replacement"

            output.Release()

            Expect.equal input.ReleaseCount 1 "caller-lock input release count"
            Expect.equal input.ActiveCount 0 "caller-lock retained the input"
            Expect.equal bSpy.ReleaseCount 1 "caller-lock replacement final release count"
            Expect.equal b.DestroyCount 1 "caller-lock replacement final destruction count"

        let bindChildDestroyRejectsReentrantGetValue() =

            let input = InputSpy(0)
            let children = ResizeArray<ReentrantDestroyResource<int>>()
            let mutable mappingCount = 0
            let mutable reentrantError : exn = null

            let output =
                input
                |> asAVal
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    let child = ReentrantDestroyResource(mappingCount)
                    children.Add child
                    asAVal (child :> IAdaptiveResource<int>)
                )
                |> asResource

            output.Acquire()
            Expect.equal (output.GetValue(AdaptiveToken.Top, RenderToken.Empty)) 1 "child-destroy initial value"
            children.[0].SetDestroyAction(fun () ->
                try output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
                with e -> reentrantError <- e
            )

            let releaseTask = Task.Run(fun () -> output.Release())
            let completed = releaseTask.Wait(TimeSpan.FromSeconds 10.0)

            Expect.isTrue completed "selected child Destroy deadlocked during reentrant evaluation"
            Expect.isTrue (reentrantError :? InvalidOperationException) "selected child Destroy reentrant evaluation did not fail predictably"
            Expect.equal mappingCount 1 "selected child Destroy reentrant evaluation reran mapping"
            Expect.equal children.Count 1 "selected child Destroy reentrant evaluation materialized another child"
            Expect.equal children.[0].DestroyCount 1 "selected child destruction count"
            Expect.equal input.ReleaseCount 1 "selected child Destroy input release count"
            Expect.equal input.ActiveCount 0 "selected child Destroy retained the input"

        let bindInputDestroyRejectsReentrantGetValue() =

            let input = ReentrantDestroyResource(1)
            let children = ResizeArray<CountingResource>()
            let mutable mappingCount = 0
            let mutable reentrantError : exn = null

            let output =
                (input :> IAdaptiveResource<int>)
                |> asAVal
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    let child = CountingResource($"input-destroy-{mappingCount}")
                    children.Add child
                    asAVal (child :> IAdaptiveResource<ResourceHandle>)
                )
                |> asResource

            output.Acquire()
            let initial = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)
            Expect.equal initial.Name "input-destroy-1" "input-destroy initial value"

            input.SetDestroyAction(fun () ->
                try output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
                with e -> reentrantError <- e
            )

            let releaseTask = Task.Run(fun () -> output.Release())
            let completed = releaseTask.Wait(TimeSpan.FromSeconds 10.0)

            Expect.isTrue completed "input Destroy deadlocked during reentrant evaluation"
            Expect.isTrue (reentrantError :? InvalidOperationException) "input Destroy reentrant evaluation did not fail predictably"
            Expect.equal mappingCount 1 "input Destroy reentrant evaluation reran mapping"
            Expect.equal input.DestroyCount 1 "input Destroy invocation count"
            Expect.equal children.Count 1 "input Destroy reentrant evaluation materialized another child"
            Expect.equal children.[0].DestroyCount 1 "input Destroy old child cleanup count"

        let bindJoinsConcurrentReleaseCleanup() =

            use firstReleaseEntered = new ManualResetEventSlim(false)
            use continueFirstRelease = new ManualResetEventSlim(false)
            use secondReleaseFinished = new ManualResetEventSlim(false)
            use secondReleaseStarted = new ManualResetEventSlim(false)
            use inputEvaluated = new ManualResetEventSlim(false)
            use evaluationStarted = new ManualResetEventSlim(false)

            let input =
                JoinedReleaseInput(
                    firstReleaseEntered,
                    continueFirstRelease,
                    secondReleaseFinished,
                    inputEvaluated
                )

            let children = ResizeArray<CountingResource>()
            let mutable mappingCount = 0

            let output =
                (input :> IAdaptiveResource<int>)
                |> asAVal
                |> AdaptiveResource.bind (fun _ ->
                    mappingCount <- mappingCount + 1
                    let child = CountingResource($"joined-release-{mappingCount}")
                    children.Add child
                    asAVal (child :> IAdaptiveResource<ResourceHandle>)
                )
                |> asResource

            output.Acquire()
            output.Acquire()
            output.GetValue(AdaptiveToken.Top, RenderToken.Empty) |> ignore
            inputEvaluated.Reset()

            let firstRelease = Task.Run(fun () -> output.Release())

            Expect.isTrue (firstReleaseEntered.Wait(TimeSpan.FromSeconds 10.0)) "first input cleanup did not block"

            let secondRelease =
                Task.Run(fun () ->
                    secondReleaseStarted.Set()
                    output.Release()
                )

            let evaluation =
                Task.Run(fun () ->
                    evaluationStarted.Set()
                    output.GetValue(AdaptiveToken.Top, RenderToken.Empty)
                )

            try
                Expect.isTrue (secondReleaseStarted.Wait(TimeSpan.FromSeconds 10.0)) "second outer release did not start"
                Expect.isTrue (evaluationStarted.Wait(TimeSpan.FromSeconds 10.0)) "joined-release evaluation did not start"
                Expect.isFalse (secondReleaseFinished.Wait(TimeSpan.FromMilliseconds 250.0)) "second input cleanup escaped before the first cleanup finished"
                Expect.isFalse secondRelease.IsCompleted "second outer release escaped before the first cleanup finished"
                Expect.isFalse (inputEvaluated.Wait(TimeSpan.FromMilliseconds 250.0)) "evaluation escaped before the active release cleanup finished"
                Expect.isFalse evaluation.IsCompleted "evaluation returned cached state before the active release cleanup finished"
            finally
                continueFirstRelease.Set()

            let completed =
                Task.WaitAll(
                    [| firstRelease; secondRelease; evaluation :> Task |],
                    TimeSpan.FromSeconds 10.0
                )

            Expect.isTrue completed "joined release cleanup and evaluation did not complete"
            Expect.isTrue secondReleaseFinished.IsSet "second input cleanup did not finish"

            Expect.contains
                ["joined-release-1"; "joined-release-2"]
                evaluation.Result.Name
                "joined-release evaluation returned an invalid handle"

            Expect.equal input.AcquireCount 2 "joined-release input acquisition count"
            Expect.equal input.ReleaseCount 2 "joined-release input release count"
            Expect.equal input.ActiveCount 0 "joined-release retained input references"
            Expect.equal children.[0].DestroyCount 1 "joined-release old child cleanup count"

            let fresh = output.GetValue(AdaptiveToken.Top, RenderToken.Empty)

            Expect.equal fresh.Name "joined-release-2" "post-final ownerless evaluation did not return a fresh child"
            Expect.equal mappingCount 2 "joined-release mapping count"
            Expect.equal children.Count 2 "joined-release child count"
            Expect.equal children.[1].CreateCount 1 "joined-release ownerless child creation count"
            Expect.equal children.[1].DestroyCount 0 "joined-release ownerless child was prematurely destroyed"

            output.ReleaseAll()
            Expect.equal children.[1].DestroyCount 1 "joined-release ownerless child cleanup count"
            

    [<Tests>]
    let tests =
        testList "Adaptive.AdaptiveResource" [
            testCase "Cast preserves equality"              Cases.castPreservesEquality
            testCase "Cast preserves resource semantics"    Cases.castPreservesResourceSemantics
            testCase "Bind retains inner until final release and reacquires fresh" Cases.bindRetainsInnerUntilFinalRelease
            testCase "Bind adopts ownerless inner on first acquire" Cases.bindAdoptsOwnerlessInnerOnFirstAcquire
            testCase "Bind ReleaseAll preserves shared inner owner" Cases.bindReleaseAllPreservesSharedInnerOwner
            testCase "Bind switches once while multiply acquired" Cases.bindSwitchesOnceWhileMultiplyAcquired
            testCase "Bind unchanged inner does not churn" Cases.bindUnchangedInnerDoesNotChurn
            testCase "Bind mapping failure preserves inner and retries" Cases.bindMappingFailurePreservesInnerAndRetries
            testCase "Bind candidate Acquire failure preserves inner and retries" Cases.bindCandidateAcquireFailurePreservesInnerAndRetries
            testCase "Bind candidate value failure preserves inner and retries" Cases.bindCandidateValueFailurePreservesInnerAndRetries
            testCase "Bind retirement failure restores lifecycle" Cases.bindRetirementFailureRestoresLifecycle
            testCase "Bind final release notifies non-resource input callback" Cases.bindFinalReleaseNotifiesNonResourceInputCallback
            testCase "Bind switch keeps shared inner output edge" Cases.bindSwitchKeepsSharedInnerOutputEdge
            testCase "Bind switch keeps direct input output edge" Cases.bindSwitchKeepsDirectInputOutputEdge
            testCase "Bind2 balances inputs and coalesces switch" Cases.bind2BalancesInputsAndCoalescesSwitch
            testCase "Bind3 tracks third input and unchanged inner" Cases.bind3TracksThirdInputAndUnchangedInner
            testCase "Bind serializes switching and final release" Cases.bindSerializesSwitchAndFinalRelease
            testCase "Bind acquire does not invert input lock" Cases.bindAcquireDoesNotInvertInputLock
            testCase "Bind selected Acquire does not invert direct input lock" Cases.bindSelectedAcquireDoesNotInvertDirectInputLock
            testCase "Bind dirty GetValue supports caller-held monitor" Cases.bindDirtyGetValueSupportsCallerHeldMonitor
            testCase "Bind child Destroy rejects reentrant GetValue" Cases.bindChildDestroyRejectsReentrantGetValue
            testCase "Bind input Destroy rejects reentrant GetValue" Cases.bindInputDestroyRejectsReentrantGetValue
            testCase "Bind joins concurrent release cleanup" Cases.bindJoinsConcurrentReleaseCleanup
        ]
