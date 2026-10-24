namespace Aardvark.Rendering.Tests.Rendering

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module RenderTokens =

    module private Cases =

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private raiseBeginError (error : exn) = raise error

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private raiseEndError (error : exn) = raise error

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private raiseBodyError (error : exn) = raise error

        type private Query(id : int, events : ResizeArray<string>) =
            let mutable begins = 0
            let mutable ends = 0
            let mutable active = 0
            member val OnBegin : int -> unit = ignore with get, set
            member val BeginError : exn option = None with get, set
            member val EndError : exn option = None with get, set
            member _.Begins = begins
            member _.Ends = ends
            member _.Active = active
            interface IQuery with
                member _.Reset() = failtest "Scopes must not reset queries"
                member this.Begin() =
                    events.Add $"begin {id}"
                    begins <- begins + 1
                    this.OnBegin begins
                    this.BeginError |> Option.iter raiseBeginError
                    active <- active + 1
                member this.End() =
                    events.Add $"end {id}"
                    ends <- ends + 1
                    active <- active - 1
                    this.EndError |> Option.iter raiseEndError

        let private fixture count =
            let events = ResizeArray<string>()
            let queries = Array.init count (fun i -> Query(i, events))
            let entries = queries |> Array.map (fun q -> q :> IQuery) |> Array.toList
            events, queries, { RenderToken.Empty with Queries = entries }

        let private caught action =
            try action(); None
            with error -> Some error

        let private same expected actual context =
            Expect.isTrue (Object.ReferenceEquals(expected, actual)) context

        let private errors (expected : exn list) (actual : exn option) context =
            match expected, actual with
            | [], None -> ()
            | [expected], Some actual -> same expected actual $"{context}: lone exception identity"
            | expected, Some (:? AggregateException as aggregate) when expected.Length > 1 ->
                Expect.equal aggregate.InnerExceptions.Count expected.Length $"{context}: aggregate count without flattening"
                for i, error in List.indexed expected do
                    same error aggregate.InnerExceptions.[i] $"{context}: inner exception {i} identity and order"
            | _ -> failtestf "%s: expected %d failures, got %A" context expected.Length actual

        let private stack (error : exn) site context =
            Expect.stringContains error.StackTrace site $"{context}: original throwing callback stack"

        let private callbacks kind count = [for i in 0 .. count - 1 -> $"{kind} {i}"]

        let startupFailures() =
            for count in [1; 2; 8; 32] do
                for failed in 0 .. count - 1 do
                    let events, queries, token = fixture count
                    let error = InvalidOperationException($"begin {failed}")
                    queries.[failed].BeginError <- Some error
                    let context = $"count={count}, failing begin={failed}"
                    errors [error] (caught (fun () -> token.Use() |> ignore)) context
                    Expect.sequenceEqual events (callbacks "begin" (failed + 1) @ callbacks "end" failed) $"{context}: rollback only successful prefix in original order"
                    for i, query in Array.indexed queries do
                        Expect.equal query.Begins (if i <= failed then 1 else 0) $"{context}, entry={i}: attempted Begin"
                        Expect.equal query.Ends (if i < failed then 1 else 0) $"{context}, entry={i}: failing and unattempted entries never ended"
                        Expect.equal query.Active 0 $"{context}, entry={i}: successful entries retired"
                    stack error "raiseBeginError" context

        let rollbackFailures() =
            for count in [2; 8; 32] do
                for failed in [1; count - 1] |> List.distinct do
                    for cleanup in [[0]; [failed - 1]; [0 .. failed - 1]] |> List.distinct do
                        let events, queries, token = fixture count
                        let beginError = AggregateException("begin aggregate", InvalidOperationException("begin inner")) :> exn
                        queries.[failed].BeginError <- Some beginError
                        let endErrors = cleanup |> List.map (fun i -> i, AggregateException($"end aggregate {i}", InvalidOperationException("end inner")) :> exn)
                        for i, error in endErrors do queries.[i].EndError <- Some error
                        let context = $"count={count}, begin={failed}, cleanup failures={cleanup}"
                        errors (beginError :: (endErrors |> List.map snd)) (caught (fun () -> token.Use() |> ignore)) context
                        Expect.sequenceEqual events (callbacks "begin" (failed + 1) @ callbacks "end" failed) $"{context}: rollback continues after every failure"
                        for i, query in Array.indexed queries do
                            Expect.equal query.Ends (if i < failed then 1 else 0) $"{context}, entry={i}: exact rollback attempts"
                        stack beginError "raiseBeginError" context
                        for _, error in endErrors do stack error "raiseEndError" context

        let disposalFailures() =
            for count in [1; 2; 8; 32] do
                for interfaceDispose in [false; true] do
                    for failed in [[0]; [count - 1]; [0 .. count - 1]; [0 .. 2 .. count - 1]] |> List.distinct do
                        for shared in [false; true] do
                            let events, queries, token = fixture count
                            let common = InvalidOperationException("shared end error") :> exn
                            let expected = failed |> List.map (fun i -> if shared then common else AggregateException($"end {i}", InvalidOperationException("nested")) :> exn)
                            for i, error in List.zip failed expected do queries.[i].EndError <- Some error
                            let scope = token.Use()
                            let context = $"count={count}, interface={interfaceDispose}, failures={failed}, shared={shared}"
                            let result = caught (fun () -> if interfaceDispose then (scope :> IDisposable).Dispose() else scope.Dispose())
                            errors expected result context
                            Expect.sequenceEqual events (callbacks "begin" count @ callbacks "end" count) $"{context}: all End entries attempted in order"
                            for i, query in Array.indexed queries do
                                Expect.equal (query.Ends, query.Active) (1, 0) $"{context}, entry={i}: no skipped cleanup"
                            for error in expected do stack error "raiseEndError" context

        let capturedListsAndDuplicates() =
            let events, queries, token = fixture 2
            let replacement = Query(99, events)
            let captured = token.Queries
            let replacementList = [replacement :> IQuery]
            queries.[0].OnBegin <- fun _ -> typeof<RenderToken>.GetProperty("Queries").SetValue(token, replacementList)
            let scope = token.Use()
            same captured scope.Queries "Scope retains the once-captured list even when Begin replaces token Queries"
            same replacementList token.Queries "Callback replaced the token list"
            scope.Dispose()
            Expect.sequenceEqual events ["begin 0"; "begin 1"; "end 0"; "end 1"] "Captured list controls both startup and cleanup"
            Expect.equal (replacement.Begins, replacement.Ends) (0, 0) "Replacement list was neither begun nor ended"

            for interfaceDispose in [false; true] do
                let events, queries, token = fixture 2
                let entries = [queries.[0] :> IQuery; queries.[1] :> IQuery; queries.[0] :> IQuery]
                let token = { token with Queries = entries }
                let scope = token.Use()
                same entries scope.Queries "Duplicate list identity"
                for _ in 1 .. 2 do
                    if interfaceDispose then (scope :> IDisposable).Dispose() else scope.Dispose()
                Expect.sequenceEqual events ["begin 0"; "begin 1"; "begin 0"; "end 0"; "end 1"; "end 0"; "end 0"; "end 1"; "end 0"] "Repeated disposal retains per-entry callback behavior"
                Expect.equal (queries.[0].Begins, queries.[0].Ends, queries.[1].Ends) (2, 4, 2) "No deduplication or one-shot disposal"

            let events, queries, token = fixture 2
            let entries = [queries.[0] :> IQuery; queries.[0] :> IQuery; queries.[1] :> IQuery]
            let error = InvalidOperationException("second occurrence")
            queries.[0].OnBegin <- fun occurrence -> if occurrence = 2 then raiseBeginError error
            errors [error] (caught (fun () -> { token with Queries = entries }.Use() |> ignore)) "Duplicate Begin failure"
            Expect.sequenceEqual events ["begin 0"; "begin 0"; "end 0"] "Rollback counts entries, never ends the failed duplicate or unattempted query"

            let events, queries, token = fixture 2
            for query in token.Queries do query.Begin()
            let scope = new QueryUseDisposable(token.Queries)
            same token.Queries scope.Queries "Public struct constructor and Queries retain the exact list"
            scope.Dispose()
            Expect.sequenceEqual events ["begin 0"; "begin 1"; "end 0"; "end 1"] "Directly constructed scope cleanup"
            for query in queries do Expect.equal query.Active 0 "Constructor does not begin entries itself"

        let nestingAndBodyFailures() =
            for count in [0; 1; 8] do
                let events, queries, token = fixture count
                let bodyError = InvalidOperationException("body")
                let failure = caught (fun () -> use _scope = token.Use() in raiseBodyError bodyError)
                errors [bodyError] failure $"count={count}: body error"
                Expect.sequenceEqual events (callbacks "begin" count @ callbacks "end" count) $"count={count}: body exception still ends all queries"
                stack bodyError "raiseBodyError" "Body exception"
                events.Clear()
                let outer = token.Use()
                let innerError = InvalidOperationException("inner body")
                errors [innerError] (caught (fun () -> use _inner = token.Use() in raiseBodyError innerError)) $"count={count}: nested body"
                for query in queries do Expect.equal query.Active 1 "Inner cleanup retains the outer lifetime"
                outer.Dispose()
                Expect.sequenceEqual events (callbacks "begin" count @ callbacks "begin" count @ callbacks "end" count @ callbacks "end" count) $"count={count}: nested order unchanged"
                for query in queries do Expect.equal query.Active 0 "Nested lifetimes balanced"
            let empty = new QueryUseDisposable([])
            empty.Dispose()
            (empty :> IDisposable).Dispose()
            RenderToken.Empty.Use().Dispose()

        type private Task(events : ResizeArray<string>, perform : RenderToken -> unit) =
            inherit AbstractRenderTask()
            override _.FramebufferSignature = None
            override _.Runtime = None
            override _.Use f = f()
            override _.Release() = events.Add "release"
            override _.PerformUpdate(_, token) = events.Add "update"; perform token
            override _.Perform(_, token, output) =
                Expect.equal output.Viewport (Box2i(V2i.Zero, V2i(7, 9))) "Output remains unchanged"
                events.Add "run"
                perform token

        let renderTaskBoundaries() =
            for update in [false; true] do
                for count in [0; 1; 8] do
                    for scenario in ["success"; "begin"; "body"; "end"] do
                        if count > 0 || (scenario <> "begin" && scenario <> "end") then
                            let events, queries, token = fixture count
                            let error = InvalidOperationException(scenario)
                            let endFailures = if scenario = "end" then [0; count - 1] |> List.distinct else []
                            let endErrors = endFailures |> List.map (fun i -> i, InvalidOperationException($"task End {i}") :> exn)
                            if scenario = "begin" then queries.[count / 2].BeginError <- Some error
                            for i, error in endErrors do queries.[i].EndError <- Some error
                            use task = new Task(events, fun actual ->
                                same token actual "Render task forwards the token unchanged"
                                for query in queries do Expect.equal query.Active 1 "Task body runs inside the query scope"
                                if scenario = "body" then raiseBodyError error)
                            let output = { Framebuffer = Unchecked.defaultof<IFramebuffer>; Viewport = Box2i(V2i.Zero, V2i(7, 9)); Scissor = Box2i.Invalid }
                            let actual = caught (fun () -> if update then task.Update(AdaptiveToken.Top, token) else task.Run(AdaptiveToken.Top, token, output))
                            let context = $"update={update}, count={count}, scenario={scenario}"
                            let expected = if scenario = "begin" || scenario = "body" then [error :> exn] else endErrors |> List.map snd
                            errors expected actual context
                            let order =
                                if scenario = "begin" then callbacks "begin" (count / 2 + 1) @ callbacks "end" (count / 2)
                                else callbacks "begin" count @ [if update then "update" else "run"] @ callbacks "end" count
                            Expect.sequenceEqual events order $"{context}: exact automatic scope boundary"
                            Expect.equal task.FrameId (if not update && (scenario = "success" || scenario = "end") then 1UL else 0UL) $"{context}: frame counter behavior unchanged"
                            for query in queries do Expect.equal query.Active 0 $"{context}: no active query left behind"

    let tests (target : TestTarget) =
        [
            "Startup failures unwind the successful prefix", Cases.startupFailures
            "Rollback failures retain startup precedence",   Cases.rollbackFailures
            "Disposal attempts every entry after failures",  Cases.disposalFailures
            "Captured lists duplicates and repeated lives",  Cases.capturedListsAndDuplicates
            "Nested scopes and body exception cleanup",      Cases.nestingAndBodyFailures
            "Render task Run and Update scope boundaries",   Cases.renderTaskBoundaries
        ]
        |> prepareCasesCpu "RenderToken" target
