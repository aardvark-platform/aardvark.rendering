namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Expecto

module ``AListRenderTask Tests`` =

    type private MockFramebufferSignature(samples : int) =
        interface IFramebufferSignature with
            member _.Runtime = Unchecked.defaultof<IFramebufferRuntime>
            member _.Samples = samples
            member _.ColorAttachments = Map.empty
            member _.DepthStencilAttachment = None
            member _.LayerCount = 1
            member _.PerLayerUniforms = Set.empty
            member _.Dispose() = ()

    type private MockRenderTask(label : string, signature : IFramebufferSignature option, useLog : ResizeArray<string> option) =
        inherit ConstantObject()

        let id = RenderTaskId.New()
        let mutable name = null
        let mutable disposed = false
        let mutable disposeCount = 0
        let mutable signatureReadCount = 0
        let mutable failSignatureRead = false
        let mutable failDispose = false

        member _.DisposeCount = disposeCount
        member _.SignatureReadCount = signatureReadCount
        member _.FailSignatureRead with get() = failSignatureRead and set value = failSignatureRead <- value
        member _.FailDispose with get() = failDispose and set value = failDispose <- value
        member _.ResetSignatureReadCount() = signatureReadCount <- 0

        member private _.CheckDisposed() =
            if disposed then
                raise <| ObjectDisposedException(label)

        interface IRenderTask with
            member x.Id = id

            member x.FramebufferSignature =
                signatureReadCount <- signatureReadCount + 1
                x.CheckDisposed()
                if failSignatureRead then
                    raise <| InvalidOperationException($"Signature failure for {label}")
                signature

            member _.Runtime = None
            member _.FrameId = 0UL

            member x.Update(_, _) =
                x.CheckDisposed()

            member x.Run(_, _, _) =
                x.CheckDisposed()

            member x.Use f =
                x.CheckDisposed()
                useLog |> Option.iter (fun log -> log.Add label)
                f()

            member _.Dispose() =
                disposeCount <- disposeCount + 1
                disposed <- true
                if failDispose then
                    raise <| ApplicationException($"Disposal failure for {label}")

            member _.Name
                with get() = name
                and set value = name <- value

    let private task label signature =
        new MockRenderTask(label, signature, None)

    let private loggedTask label signature log =
        new MockRenderTask(label, signature, Some log)

    let private asRenderTask (task : MockRenderTask) =
        task :> IRenderTask

    let private createCombined (source : clist<IRenderTask>) =
        RenderTask.ofAList (source :> alist<_>)

    let private batchSwap() =
        let signature = new MockFramebufferSignature(1) :> IFramebufferSignature
        let log = ResizeArray<string>()
        let a = loggedTask "a" (Some signature) log
        let b = loggedTask "b" (Some signature) log
        let source = clist<IRenderTask>([| asRenderTask a; asRenderTask b |])
        let combined = createCombined source

        try
            combined.FramebufferSignature |> ignore

            transact (fun () ->
                source.[0] <- asRenderTask b
                source.[1] <- asRenderTask a
            )

            combined.FramebufferSignature |> ignore
            Expect.equal a.DisposeCount 0 "First task was disposed during the swap"
            Expect.equal b.DisposeCount 0 "Second task was disposed during the swap"

            combined.Use id
            Expect.equal (List.ofSeq log) ["b"; "a"] "Execution order does not match the final list"
        finally
            combined.Dispose()

    let private replacementUpdatesSignature() =
        let signatureA = new MockFramebufferSignature(1) :> IFramebufferSignature
        let signatureB = new MockFramebufferSignature(2) :> IFramebufferSignature
        let a = task "a" (Some signatureA)
        let b = task "b" (Some signatureB)
        let source = clist<IRenderTask>([| asRenderTask a; asRenderTask b |])
        let combined = createCombined source

        try
            Expect.isNone combined.FramebufferSignature "Initial incompatible signatures were combined"

            transact (fun () -> source.[0] <- asRenderTask b)

            match combined.FramebufferSignature with
            | Some signature ->
                Expect.isTrue (Object.ReferenceEquals(signature, signatureB)) "Signature was not recomputed from the final distinct task"
            | None ->
                failtest "Expected the remaining task's signature"

            Expect.equal a.DisposeCount 1 "Removed task was not disposed exactly once"
            Expect.equal b.DisposeCount 0 "Retained task was disposed"
        finally
            combined.Dispose()

    let private distinctChildrenDisposedOnce() =
        let a = task "a" None
        let b = task "b" None
        let c = task "c" None
        let source = clist<IRenderTask>([| asRenderTask a; asRenderTask a; asRenderTask b; asRenderTask b; asRenderTask c |])
        let combined = createCombined source

        try
            combined.FramebufferSignature |> ignore
            let first = source.TryGetIndex 0 |> Option.get
            let second = source.TryGetIndex 1 |> Option.get

            transact (fun () ->
                source.Remove first |> ignore
                source.Remove second |> ignore
            )

            combined.FramebufferSignature |> ignore
            Expect.equal a.DisposeCount 1 "Final removed child was not disposed exactly once"
            Expect.equal b.DisposeCount 0 "Retained duplicate child was disposed"
            Expect.equal c.DisposeCount 0 "Retained child was disposed"

            combined.Dispose()
            Expect.equal a.DisposeCount 1 "Removed child was disposed again during release"
            Expect.equal b.DisposeCount 1 "Distinct retained child was not disposed exactly once during release"
            Expect.equal c.DisposeCount 1 "Distinct retained child was not disposed exactly once during release"

            combined.Dispose()
            Expect.equal a.DisposeCount 1 "Repeated parent disposal reached a removed child"
            Expect.equal b.DisposeCount 1 "Repeated parent disposal reached a retained child"
            Expect.equal c.DisposeCount 1 "Repeated parent disposal reached a retained child"
        finally
            combined.Dispose()

    let private largeBatchReadsFinalSignaturesOnce() =
        let count = 256
        let initial = Array.init count (fun i -> task $"old-{i}" None)
        let final = Array.init count (fun i -> task $"new-{i}" None)
        let source = clist<IRenderTask>(initial |> Array.map asRenderTask)
        let combined = createCombined source

        try
            combined.FramebufferSignature |> ignore
            initial |> Array.iter (fun task -> task.ResetSignatureReadCount())
            final |> Array.iter (fun task -> task.ResetSignatureReadCount())

            transact (fun () ->
                for i = 0 to count - 1 do
                    source.[i] <- asRenderTask final.[i]
            )

            combined.FramebufferSignature |> ignore
            combined.FramebufferSignature |> ignore

            for task in initial do
                Expect.equal task.SignatureReadCount 0 "Removed task signature was read while combining the final set"
                Expect.equal task.DisposeCount 1 "Removed task was not disposed exactly once"

            for task in final do
                Expect.equal task.SignatureReadCount 1 "Final task signature was not read exactly once"
                Expect.equal task.DisposeCount 0 "Final task was disposed"
        finally
            combined.Dispose()

    let private signatureFailureRestoresState() =
        let signature = new MockFramebufferSignature(1) :> IFramebufferSignature
        let removed = task "removed-first" (Some signature)
        let removedAfterFailure = task "removed-second" (Some signature)
        let retained = task "retained" (Some signature)
        let failing = task "failing" (Some signature)
        let source = clist<IRenderTask>([| asRenderTask removed; asRenderTask removedAfterFailure; asRenderTask retained |])
        let combined = new AListRenderTask(source :> alist<_>)

        try
            combined.FramebufferSignature |> ignore
            failing.FailSignatureRead <- true
            removed.FailDispose <- true
            transact (fun () ->
                source.[0] <- asRenderTask failing
                source.[1] <- asRenderTask retained
            )
            combined.OutOfDate <- false

            Expect.throwsT<InvalidOperationException>
                (fun () -> combined.FramebufferSignature |> ignore)
                "Expected signature recomputation to fail"

            Expect.isFalse combined.OutOfDate "OutOfDate was not restored after signature failure"
            Expect.equal removed.DisposeCount 1 "Deferred removal was not disposed after signature failure"
            Expect.equal removedAfterFailure.DisposeCount 1 "A disposal failure skipped another deferred removal"
            Expect.equal retained.DisposeCount 0 "Retained task was disposed after signature failure"
            Expect.equal failing.DisposeCount 0 "Final task was disposed after signature failure"

            let readsAfterFailure = failing.SignatureReadCount
            failing.FailSignatureRead <- false
            Expect.isSome combined.FramebufferSignature "Signature recomputation was not retried"
            Expect.equal failing.SignatureReadCount (readsAfterFailure + 1) "Retry did not read the final signature exactly once"

            combined.FramebufferSignature |> ignore
            Expect.equal failing.SignatureReadCount (readsAfterFailure + 1) "Clean signature access recomputed the signature"
            Expect.equal removed.DisposeCount 1 "Signature retry disposed the removed task again"
            Expect.isFalse combined.OutOfDate "Signature retry did not preserve OutOfDate"
        finally
            combined.Dispose()

    let private releaseFailureDisposesAllChildren() =
        let first = task "first" None
        let second = task "second" None
        let source = clist<IRenderTask>([| asRenderTask first; asRenderTask second |])
        let combined = createCombined source

        combined.FramebufferSignature |> ignore
        first.FailDispose <- true

        Expect.throwsT<ApplicationException>
            (fun () -> combined.Dispose())
            "Expected child disposal to fail"

        Expect.equal first.DisposeCount 1 "Failing child was not disposed exactly once"
        Expect.equal second.DisposeCount 1 "A disposal failure skipped a later child"

        combined.Dispose()
        Expect.equal first.DisposeCount 1 "Repeated release reached the failing child"
        Expect.equal second.DisposeCount 1 "Repeated release reached the successfully disposed child"

    [<Tests>]
    let tests =
        testList "Rendering.AListRenderTask" [
            testCase "Batch swap keeps children alive and ordered" batchSwap
            testCase "Replacement by existing task updates signature" replacementUpdatesSignature
            testCase "Distinct children are disposed once" distinctChildrenDisposedOnce
            testCase "Large batch reads final signatures once" largeBatchReadsFinalSignaturesOnce
            testCase "Signature failure restores lifecycle state" signatureFailureRestoresState
            testCase "Release failure still disposes all children" releaseFailureDisposesAllChildren
        ]
