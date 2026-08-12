namespace Aardvark.Rendering.Tests

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Expecto
open FSharp.Data.Adaptive

module ``ConcurrentDeltaPriorityQueue Tests`` =

    module private Helpers =

        type Queue =
            {
                enqueue : SetOperation<int> -> unit
                enqueueMany : seq<SetOperation<int>> -> unit
                dequeue : unit -> SetOperation<int>
                count : unit -> int
                validate : Dictionary<int, int> -> unit
            }

        let inline operation value count = SetOperation(value, count)

        let renderingQueue (getPriority : SetOperation<int> -> int) =
            let queue = Aardvark.Rendering.ConcurrentDeltaPriorityQueue<int, int>(getPriority)
            {
                enqueue = queue.Enqueue
                enqueueMany = queue.EnqueueMany
                dequeue = queue.Dequeue
                count = fun () -> queue.Count
                validate = fun model ->
                    let mutable adds = 0
                    let mutable rems = 0
                    for count in model.Values do
                        if count > 0 then adds <- adds + 1
                        elif count < 0 then rems <- rems + 1

                    Expect.equal queue.AddCount adds "add count must match the positive reference entries"
                    Expect.equal queue.RemoveCount rems "remove count must match the negative reference entries"
                    Expect.equal (queue.AddCount + queue.RemoveCount) queue.Count "count partitions must cover the queue"
            }

        let sceneGraphQueue (getPriority : SetOperation<int> -> int) =
            let queue = Aardvark.SceneGraph.ConcurrentDeltaPriorityQueue<int, int>(getPriority)
            {
                enqueue = queue.Enqueue
                enqueueMany = queue.EnqueueMany
                dequeue = queue.Dequeue
                count = fun () -> queue.Count
                validate = ignore
            }

        let apply (model : Dictionary<int, int>) (operation : SetOperation<int>) =
            if operation.Count <> 0 then
                let mutable oldCount = 0
                model.TryGetValue(operation.Value, &oldCount) |> ignore
                let newCount = oldCount + operation.Count
                if newCount = 0 then
                    model.Remove operation.Value |> ignore
                else
                    model.[operation.Value] <- newCount

        let dequeueExpected priority (queue : Queue) (model : Dictionary<int, int>) =
            let mutable expectedValue = 0
            let mutable expectedCount = 0
            let mutable expectedPriority = Int32.MaxValue

            for KeyValue(value, count) in model do
                let currentPriority = priority (operation value count)
                if currentPriority < expectedPriority then
                    expectedValue <- value
                    expectedCount <- count
                    expectedPriority <- currentPriority

            let actual = queue.dequeue()
            Expect.equal actual.Value expectedValue "dequeue must return the minimum-priority value"
            Expect.equal actual.Count expectedCount "dequeue must return the coalesced reference count"
            model.Remove expectedValue |> ignore

        let checkState (queue : Queue) (model : Dictionary<int, int>) =
            Expect.equal (queue.count()) model.Count "queue count must match the reference model"
            queue.validate model

        let drainValues (queue : Queue) =
            Array.init (queue.count()) (fun _ -> queue.dequeue().Value)

        let ordering (createQueue : (SetOperation<int> -> int) -> Queue) =
            let queue = createQueue (fun operation -> operation.Value)
            queue.enqueueMany [for value in [1; 5; 2; 6; 7; 3; 4] -> operation value 1]

            let actual = drainValues queue
            Expect.sequenceEqual actual [| 1; 2; 3; 4; 5; 6; 7 |] "dequeue must maintain min-heap order"

        let equalPriority (createQueue : (SetOperation<int> -> int) -> Queue) =
            let priorities = [| 0; 1; 1; 2 |]
            let queue = createQueue (fun operation -> priorities.[operation.Value])
            queue.enqueueMany [for value in 0 .. 3 -> operation value 1]

            let actual = drainValues queue
            Expect.sequenceEqual actual [| 0; 2; 1; 3 |] "equal children must retain the existing right-child selection"

        let zeroCount (createQueue : (SetOperation<int> -> int) -> Queue) =
            let mutable priorityCalls = 0
            let queue = createQueue (fun operation ->
                priorityCalls <- priorityCalls + 1
                operation.Value
            )
            let model = Dictionary<int, int>()

            queue.enqueueMany [operation 10 0; operation 11 0]
            checkState queue model
            Expect.equal priorityCalls 0 "empty zero-count batches must not evaluate priority"

            for item in [operation 3 1; operation 1 1] do
                queue.enqueue item
                apply model item

            queue.enqueueMany [operation 0 0; operation 3 0; operation 99 0]
            checkState queue model
            Expect.equal priorityCalls 2 "populated zero-count batches must not evaluate priority"
            Expect.sequenceEqual (drainValues queue) [| 1; 3 |] "zero-count batches must not disturb populated queues"

        let randomized (createQueue : (SetOperation<int> -> int) -> Queue) seed =
            let random = Random(seed)
            let priorities = Array.init 32 id
            for i = priorities.Length - 1 downto 1 do
                let j = random.Next(i + 1)
                let t = priorities.[i]
                priorities.[i] <- priorities.[j]
                priorities.[j] <- t

            let priority (operation : SetOperation<int>) =
                abs operation.Count * priorities.Length + priorities.[operation.Value]

            let queue = createQueue priority
            let model = Dictionary<int, int>()

            let enqueue item =
                queue.enqueue item
                apply model item

            let enqueueMany (items : SetOperation<int> array) =
                queue.enqueueMany items
                Array.iter (apply model) items

            let duplicates = [| operation 7 2; operation 7 2; operation 8 -3 |]
            enqueueMany duplicates
            checkState queue model
            enqueue (operation 7 -4)
            enqueue (operation 8 3)
            checkState queue model
            enqueue (operation 9 -2)
            enqueue (operation 9 5)
            enqueue (operation 9 -6)
            enqueue (operation 9 3)
            checkState queue model

            let randomOperation() =
                let value = random.Next priorities.Length
                let count = random.Next(-4, 5)
                operation value count

            for _ = 1 to 4000 do
                match random.Next 100 with
                | action when action < 45 ->
                    let item = randomOperation()
                    enqueue item

                | action when action < 80 ->
                    let batch = Array.init (random.Next(1, 9)) (fun _ -> randomOperation())
                    enqueueMany batch

                | _ when model.Count > 0 ->
                    dequeueExpected priority queue model

                | _ ->
                    let item = randomOperation()
                    enqueue item

                checkState queue model

            while model.Count > 0 do
                dequeueExpected priority queue model
                checkState queue model

    module private Cases =

        open Helpers

        let renderingOrdering() = ordering renderingQueue
        let sceneGraphOrdering() = ordering sceneGraphQueue
        let renderingEqualPriority() = equalPriority renderingQueue
        let sceneGraphEqualPriority() = equalPriority sceneGraphQueue

        let renderingIncreasedPriority() =
            let priorities = [| 1; 5; 2; 6; 7; 3; 4 |]
            let queue = Aardvark.Rendering.ConcurrentDeltaPriorityQueue<int, int>(fun operation ->
                if operation.Value = 0 then operation.Count else priorities.[operation.Value]
            )

            queue.EnqueueMany [for value in 0 .. 6 -> operation value 1]
            queue.Enqueue (operation 0 7)

            let actual = Array.init queue.Count (fun _ -> queue.Dequeue().Value)
            Expect.sequenceEqual actual [| 2; 5; 6; 1; 3; 4; 0 |] "an increased priority must push the entry below the smaller right child"

        let sceneGraphIncreasedPriority() =
            let priorities = [| 1; 5; 2; 6; 7; 3; 4 |]
            let queue = Aardvark.SceneGraph.ConcurrentDeltaPriorityQueue<int, int>(fun operation -> priorities.[operation.Value])

            queue.EnqueueMany [for value in 0 .. 6 -> operation value 1]
            priorities.[0] <- 10
            queue.UpdatePriorities() |> ignore

            let actual = Array.init queue.Count (fun _ -> queue.Dequeue().Value)
            Expect.sequenceEqual actual [| 2; 5; 6; 1; 3; 4; 0 |] "reprioritization must push the entry below the smaller right child"

        let renderingZeroCount() = zeroCount renderingQueue
        let sceneGraphZeroCount() = zeroCount sceneGraphQueue

        let renderingCounts() =
            let queue = Aardvark.Rendering.ConcurrentDeltaPriorityQueue<int, int>(fun operation -> abs operation.Count)

            let check count adds rems =
                Expect.equal queue.Count count "total count"
                Expect.equal queue.AddCount adds "add count"
                Expect.equal queue.RemoveCount rems "remove count"
                Expect.equal (queue.AddCount + queue.RemoveCount) queue.Count "count invariant"

            queue.Enqueue(operation 1 2)
            check 1 1 0
            queue.Enqueue(operation 1 -5)
            check 1 0 1
            queue.Enqueue(operation 1 3)
            check 0 0 0
            queue.Enqueue(operation 1 -2)
            check 1 0 1
            queue.Enqueue(operation 1 5)
            check 1 1 0
            queue.Enqueue(operation 2 -1)
            check 2 1 1

            let first = queue.Dequeue()
            Expect.equal first.Value 2 "minimum-priority remove must dequeue first"
            check 1 1 0
            queue.Dequeue() |> ignore
            check 0 0 0

        let renderingRandomized() = randomized renderingQueue 12345
        let sceneGraphRandomized() = randomized sceneGraphQueue 67890

        let concurrentSceneGraphReprioritization() =
            use shouldBlock = new ManualResetEventSlim(false)
            use updateEntered = new ManualResetEventSlim(false)
            use releaseUpdate = new ManualResetEventSlim(false)
            use enqueueStarted = new ManualResetEventSlim(false)

            let priorities = Dictionary<int, int>([KeyValuePair(1, 1); KeyValuePair(2, 2)])
            let mutable monitorTarget : obj = null
            let mutable updateHeldMonitor = false
            let queue = Aardvark.SceneGraph.ConcurrentDeltaPriorityQueue<int, int>(fun operation ->
                if operation.Value = 1 && shouldBlock.IsSet then
                    updateHeldMonitor <- Monitor.IsEntered monitorTarget
                    updateEntered.Set()
                    releaseUpdate.Wait()
                priorities.[operation.Value]
            )

            monitorTarget <- queue :> obj
            queue.Enqueue(operation 1 1)
            shouldBlock.Set()

            let updateTask = Task.Run(fun () -> queue.UpdatePriorities() |> ignore)
            let mutable enqueueTask : Task = null

            try
                Expect.isTrue (updateEntered.Wait(TimeSpan.FromSeconds 5.0)) "priority update must reach the coordinated callback"
                Expect.isTrue updateHeldMonitor "priority callback must run while holding the queue monitor"

                enqueueTask <- Task.Run(fun () ->
                    enqueueStarted.Set()
                    queue.Enqueue(operation 2 1)
                )

                Expect.isTrue (enqueueStarted.Wait(TimeSpan.FromSeconds 5.0)) "enqueue task must start"
                Expect.isFalse (enqueueTask.Wait(TimeSpan.FromMilliseconds 250.0)) "enqueue must wait for the reprioritization monitor"
            finally
                releaseUpdate.Set()

            Expect.isTrue (updateTask.Wait(TimeSpan.FromSeconds 5.0)) "priority update must finish"
            if not (isNull enqueueTask) then
                Expect.isTrue (enqueueTask.Wait(TimeSpan.FromSeconds 5.0)) "enqueue must finish after reprioritization"
            Expect.equal queue.Count 2 "both entries must remain queued"
            Expect.sequenceEqual (Array.init queue.Count (fun _ -> queue.Dequeue().Value)) [| 1; 2 |] "queue must remain ordered"

    [<Tests>]
    let tests =
        testList "Utilities.ConcurrentDeltaPriorityQueue" [
            testCase "Rendering dequeue ordering" Cases.renderingOrdering
            testCase "SceneGraph dequeue ordering" Cases.sceneGraphOrdering
            testCase "Rendering equal-priority ordering" Cases.renderingEqualPriority
            testCase "SceneGraph equal-priority ordering" Cases.sceneGraphEqualPriority
            testCase "Rendering increased priority" Cases.renderingIncreasedPriority
            testCase "SceneGraph increased priority" Cases.sceneGraphIncreasedPriority
            testCase "Rendering zero-count batches" Cases.renderingZeroCount
            testCase "SceneGraph zero-count batches" Cases.sceneGraphZeroCount
            testCase "Rendering count invariants" Cases.renderingCounts
            testCase "Rendering randomized model" Cases.renderingRandomized
            testCase "SceneGraph randomized model" Cases.sceneGraphRandomized
            testCase "SceneGraph concurrent reprioritization" Cases.concurrentSceneGraphReprioritization
        ]
