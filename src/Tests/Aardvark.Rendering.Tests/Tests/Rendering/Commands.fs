namespace Aardvark.Rendering.Tests.Rendering

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Expecto
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

[<AutoOpen>]
module TestCommands =

    let inline private colorSg (color: C4f) =
        Sg.fullScreenQuad
        |> Sg.surface (Effects.ConstantColor.Effect (c4f color))

    type TestCommand =
        | Clear      of aval<C4f>
        | Render     of C4f
        | IfThenElse of aval<bool> * TestCommand * TestCommand
        | Ordered    of alist<TestCommand>

        member this.RenderCommand =
            match this with
            | Clear color                   -> RenderCommand.Clear color
            | Render color                  -> RenderCommand.Render (colorSg color)
            | IfThenElse (condition, a, b)  -> RenderCommand.IfThenElse(condition, a.RenderCommand, b.RenderCommand)
            | Ordered list                  -> RenderCommand.Ordered(list |> AList.map _.RenderCommand)

        member this.Scene =
            match this with
            | Clear color                   -> Sg.execute <| RenderCommand.Clear color
            | Render color                  -> Sg.execute <| RenderCommand.Render (colorSg color)
            | IfThenElse (condition, a, b)  -> Sg.execute <| RenderCommand.IfThenElse(condition, a.Scene, b.Scene)
            | Ordered list                  -> Sg.execute <| RenderCommand.Ordered(list |> AList.map _.Scene)

        member this.ExpectedColor =
            match this with
            | Clear color                   -> color |> AVal.map c4f
            | Render color                  -> ~~(c4f color)
            | IfThenElse (condition, a, b)  -> condition |> AVal.bind (fun c -> if c then a.ExpectedColor else b.ExpectedColor)
            | Ordered list                  -> list |> AList.tryLast |> AVal.bind (Option.map _.ExpectedColor >> Option.defaultValue (~~C4f.Black))

    type private TestCommandTreeState =
        {
            flags   : ResizeArray<cval<bool>>
            colors  : ResizeArray<cval<C4f>>
            ordered : ResizeArray<clist<TestCommand>>
        }

    type TestCommandTree private (root: TestCommand, state: TestCommandTreeState) =

        static let rec generate (state: TestCommandTreeState) (maxLevel: int) (level: int) =
            let leaf = level >= maxLevel || Rnd.float() < 0.01 * float level
            let next = Rnd.int32() % (if leaf then 2 else 4)

            match next with
            | 0 ->
                let c = AVal.init <| Rnd.c4f()
                state.colors.Add c
                Clear c

            | 1 ->
                Render <| Rnd.c4f()

            | 2 ->
                let cond = AVal.init <| Rnd.bool()
                let a = generate state maxLevel (level + 1)
                let b = generate state maxLevel (level + 1)
                state.flags.Add cond
                IfThenElse (cond, a, b)

            | _ ->
                let length = 2 + Rnd.int32() % 7
                let commands = clist (Array.init length (fun _ -> generate state maxLevel (level + 1)))
                state.ordered.Add commands
                Ordered commands

        member _.Root = root

        member _.Randomize() =
            for c in state.colors do c.Value  <- Rnd.c4f()
            for f in state.flags do f.Value   <- Rnd.bool()
            for o in state.ordered do o.Value <- IndexList.ofArray <| Rnd.shuffle o.Value

        static member Generate(maxLevel: int) =
            let state =
                {
                    flags   = ResizeArray()
                    colors  = ResizeArray()
                    ordered = ResizeArray()
                }

            let root = generate state maxLevel 0
            TestCommandTree(root, state)

module Commands =

    module private Sequences =
        type private Route = UnorderedScenes | OrderedCommands | OrderedScenes
        type private StreamKind = Drain | Once | Effects
        type private Fault = OnGet | OnMove of int | OnCurrent of int | OnDispose
        type private Item = { Scene : ISg; Object : IRenderObject; Values : aval<ClearValues>; Command : RenderCommand }

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private throwProducer (error : exn) = raise error

        type private Producer(items : Item[], kind : StreamKind) =
            let queue = Queue<Item>(items)
            let yielded = ResizeArray<Item>()
            member val Enumerations = 0 with get, set
            member val Disposals = 0 with get, set
            member val Poison = false with get, set
            member val Fault : Fault option = None with get, set
            member val Error : exn = InvalidOperationException("producer") with get, set
            member _.Yielded = yielded
            member _.Remaining = queue.Count
            member private x.Check() = if x.Poison then failwith "Source accessed after construction"
            interface IEnumerable<Item> with
                member x.GetEnumerator() =
                    x.Check()
                    x.Enumerations <- x.Enumerations + 1
                    if x.Fault = Some OnGet then throwProducer x.Error
                    if kind = Once && x.Enumerations > 1 then failwith "Source does not support a second enumeration"
                    let mutable index = -1
                    let mutable current = Unchecked.defaultof<Item>
                    let mutable disposed = false
                    let read() =
                        x.Check()
                        if x.Fault = Some(OnCurrent index) then throwProducer x.Error
                        current
                    { new IEnumerator<Item> with
                        member _.Current = read()
                      interface IEnumerator with
                        member _.Current = box (read())
                        member _.MoveNext() =
                            x.Check()
                            if disposed then failwith "MoveNext after disposal"
                            let next = index + 1
                            if x.Fault = Some(OnMove next) then throwProducer x.Error
                            let hasValue = if kind = Drain then queue.Count > 0 else next < items.Length
                            if hasValue then
                                index <- next
                                current <- if kind = Drain then queue.Dequeue() else items.[index]
                                yielded.Add current
                            hasValue
                        member _.Reset() = raise (NotSupportedException())
                      interface IDisposable with
                        member _.Dispose() =
                            x.Check()
                            if disposed then failwith "Enumerator disposed twice"
                            disposed <- true
                            x.Disposals <- x.Disposals + 1
                            if x.Fault = Some OnDispose then throwProducer x.Error }
            interface IEnumerable with
                member x.GetEnumerator() = (x :> IEnumerable<Item>).GetEnumerator() :> IEnumerator

        let private inputs count =
            let unique = Array.init 3 (fun i ->
                let ro = new RenderObject() :> IRenderObject
                let values = AVal.constant (ClearValues.ofColor (C4f(float32 i / 4.0f, 0.25f, 0.5f, 1.0f)))
                { Scene = Sg.renderObjectSet (ASet.single ro); Object = ro; Values = values; Command = RenderCommand.Clear values })
            match count with
            | 0 -> [||]
            | 1 -> [| unique.[0] |]
            | _ -> [| unique.[0]; unique.[1]; unique.[2]; unique.[1]; unique.[2] |]

        let private construct route (source : seq<Item>) =
            match route with
            | UnorderedScenes -> RenderCommand.Unordered(source |> Seq.map _.Scene)
            | OrderedCommands -> RenderCommand.Ordered(source |> Seq.map _.Command)
            | OrderedScenes -> RenderCommand.Ordered(source |> Seq.map _.Scene)

        let private runtime command =
            let objects = (Sg.execute command).RenderObjects(Ag.Scope.Root) |> ASet.force |> Seq.toArray
            Expect.equal objects.Length 1 "one public command render object"
            (objects.[0] :?> CommandRenderObject).Command

        let private checkOutput context route (expected : Item[]) command =
            match route, runtime command with
            | _, RuntimeCommand.EmptyCmd when expected.Length = 0 -> ()
            | UnorderedScenes, RuntimeCommand.RenderCmd objects ->
                let actual = ASet.force objects |> Seq.toArray
                let distinct = expected |> Array.map _.Object |> Array.distinct
                Expect.equal actual.Length distinct.Length (context + ": set deduplication")
                for item in distinct do
                    Expect.isTrue (actual |> Array.exists (fun value -> obj.ReferenceEquals(item, value))) (context + ": retained scene identity")
            | (OrderedCommands | OrderedScenes), RuntimeCommand.OrderedCmd commands ->
                let actual = AList.force commands |> IndexList.toArray
                Expect.equal actual.Length expected.Length (context + ": ordered count including duplicates")
                for i in 0 .. expected.Length - 1 do
                    match route, actual.[i] with
                    | OrderedCommands, RuntimeCommand.ClearCmd values ->
                        Expect.isTrue (obj.ReferenceEquals(expected.[i].Values, values)) $"{context}, item={i}: original command value identity/order"
                    | OrderedScenes, RuntimeCommand.RenderCmd objects ->
                        let item = ASet.force objects |> Seq.exactlyOne
                        Expect.isTrue (obj.ReferenceEquals(expected.[i].Object, item)) $"{context}, item={i}: original scene identity/order"
                    | _ -> failtestf "%s, item=%d: unexpected runtime command" context i
            | _ -> failtestf "%s: unexpected runtime command" context

        let private canonical context count command =
            Expect.equal (obj.ReferenceEquals(command, RenderCommand.Empty)) (count = 0) (context + ": canonical empty command")

        let private collect (failures : ResizeArray<string>) context action =
            try action()
            with error -> failures.Add($"{context}: {error.Message}")

        let private consume kind =
            let failures = ResizeArray<string>()
            for route in [UnorderedScenes; OrderedCommands; OrderedScenes] do
                for count in [0; 1; 5] do
                    let context = $"route={route}, count={count}, producer={kind}"
                    collect failures context (fun () ->
                        let items = inputs count
                        let source = Producer(items, kind)
                        let command = construct route source
                        canonical context count command
                        source.Poison <- true
                        for _ in 1 .. 3 do checkOutput context route items command
                        Expect.equal source.Enumerations 1 (context + ": one eager enumeration, with no later source access")
                        Expect.equal source.Disposals 1 (context + ": enumerator disposed once during construction")
                        Expect.sequenceEqual (source.Yielded |> Seq.map _.Object) (items |> Array.map _.Object) (context + ": producer effects occur once per item")
                        if kind = Drain then Expect.equal source.Remaining 0 (context + ": queue drained without dropping an item")
                    )
            Expect.isEmpty failures (String.concat "\n" failures)

        let queueStreams _ = consume Drain
        let singleUseStreams _ = consume Once
        let sideEffects _ = consume Effects

        let producerFailures _ =
            let failures = ResizeArray<string>()
            for route in [UnorderedScenes; OrderedCommands; OrderedScenes] do
                for count in [0; 1; 5] do
                    let faults = [ yield OnGet; yield OnDispose
                                   for i in 0 .. count do yield OnMove i
                                   for i in 0 .. count - 1 do yield OnCurrent i ]
                    for fault in faults do
                        let context = $"route={route}, count={count}, fault={fault}"
                        collect failures context (fun () ->
                            let source = Producer(inputs count, Effects)
                            let expected = ApplicationException(context)
                            source.Error <- expected
                            source.Fault <- Some fault
                            let mutable published = false
                            let actual = try construct route source |> ignore; published <- true; None with error -> Some error
                            Expect.isFalse published (context + ": producer failure is eager")
                            match actual with
                            | Some error ->
                                Expect.isTrue (obj.ReferenceEquals(expected, error)) (context + ": original producer exception identity")
                                Expect.stringContains error.StackTrace "throwProducer" (context + ": original producer throw site")
                            | None -> failtest (context + ": producer exception was lost")
                            Expect.equal source.Enumerations 1 (context + ": no retry enumeration after failure")
                            Expect.equal source.Disposals (if fault = OnGet then 0 else 1) (context + ": dispose every acquired enumerator")
                        )
            Expect.isEmpty failures (String.concat "\n" failures)

        let private collection kind (values : 'T[]) : seq<'T> =
            match kind with
            | 0 -> values :> seq<_>
            | 1 -> Array.toList values :> seq<_>
            | _ -> seq { yield! values }

        let private listCommand route (items : Item[]) =
            match route with
            | UnorderedScenes -> RenderCommand.Unordered(items |> Array.map _.Scene |> Array.toList)
            | OrderedCommands -> RenderCommand.Ordered(items |> Array.map _.Command |> Array.toList)
            | OrderedScenes -> RenderCommand.Ordered(items |> Array.map _.Scene |> Array.toList)

        let private adaptiveCommand route =
            match route with
            | UnorderedScenes ->
                let scenes = cset<ISg>()
                RenderCommand.Unordered(scenes :> aset<_>), (fun (items : Item[]) -> transact (fun () -> scenes.Value <- HashSet.ofArray (items |> Array.map _.Scene)))
            | OrderedCommands ->
                let commands = clist<RenderCommand>()
                RenderCommand.Ordered(commands :> alist<_>), (fun (items : Item[]) -> transact (fun () -> commands.Value <- IndexList.ofArray (items |> Array.map _.Command)))
            | OrderedScenes ->
                let scenes = clist<ISg>()
                RenderCommand.Ordered(scenes :> alist<_>), (fun (items : Item[]) -> transact (fun () -> scenes.Value <- IndexList.ofArray (items |> Array.map _.Scene)))

        let repeatableControls _ =
            for route in [UnorderedScenes; OrderedCommands; OrderedScenes] do
                for count in [0; 1; 5] do
                    let items = inputs count
                    for kind in [0; 1; 2] do
                        let context = $"route={route}, count={count}, array/list/lazy={kind}"
                        let scenes, commands = items |> Array.map _.Scene, items |> Array.map _.Command
                        let command =
                            match route with
                            | UnorderedScenes -> RenderCommand.Unordered(collection kind scenes)
                            | OrderedCommands -> RenderCommand.Ordered(collection kind commands)
                            | OrderedScenes -> RenderCommand.Ordered(collection kind scenes)
                        canonical context count command
                        // Even the lazy producer must have been snapshotted before its backing data changes.
                        Array.fill scenes 0 scenes.Length Sg.empty
                        Array.fill commands 0 commands.Length RenderCommand.Empty
                        for _ in 1 .. 3 do checkOutput context route items command
                    let direct = listCommand route items
                    canonical $"route={route}, list overload, count={count}" count direct
                    checkOutput $"route={route}, list overload, count={count}" route items direct
                let adaptive, update = adaptiveCommand route
                Expect.isFalse (obj.ReferenceEquals(adaptive, RenderCommand.Empty)) $"route={route}: dynamic empty input is not frozen"
                for count in [0; 5; 1; 0] do
                    let items = inputs count
                    update items
                    checkOutput $"route={route}, adaptive count={count}" route items adaptive

    module Cases =

        let inline private renderAndCheck (expectedColor: aval<'c4f>) (task: IRenderTask) =
            let output = task |> RenderTask.renderToColor (~~V2i(256))
            output.Acquire()

            try
                let result = output.GetValue().Download().AsPixImage<float32>()
                let expectedColor = c4f <| expectedColor.GetValue()
                result |> PixImage.isColor (expectedColor.ToArray())
            finally
                output.Release()

        let private testCommand (runtime: IRuntime) (asSceneGraph: bool) (test: IRenderTask -> 'T) (command: aval<TestCommand>) =
            use signature = runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba32f
            ]

            let sg =
                if asSceneGraph then
                    command |> AVal.map (_.Scene)
                else
                    command |> AVal.map (_.RenderCommand >> Sg.execute)

            use task =
                sg
                |> Sg.dynamic
                |> Sg.compile runtime signature

            test task

        let ordered (runtime: IRuntime) =
            let colors = clist (Array.init 16 (ignore >> Rnd.c4f))
            let command = colors |> AList.map Render |> Ordered
            let expectedColor = command.ExpectedColor

            ~~command |> testCommand runtime false (fun task ->
                task |> renderAndCheck expectedColor

                transact (fun _ ->
                    colors.Value <- colors.Value |> IndexList.sortBy _.R
                )

                task |> renderAndCheck expectedColor
            )

        let ifThenElse (runtime: IRuntime) =
            let flag = AVal.init true
            let command = IfThenElse(flag, Render <| Rnd.c4f(), Render <| Rnd.c4f())
            let expectedColor = command.ExpectedColor

            ~~command |> testCommand runtime false (fun task ->
                task |> renderAndCheck expectedColor

                transact (fun _ ->
                    flag.Value <- not flag.Value
                )

                task |> renderAndCheck expectedColor
            )

        let clear (runtime: IRuntime) =
            let color = AVal.init <| Rnd.c4f()
            let command = Clear color

            ~~command |> testCommand runtime false (fun task ->
                task |> renderAndCheck color

                transact (fun _ ->
                    color.Value <- Rnd.c4f()
                )

                task |> renderAndCheck color
            )

        let nested (asSceneGraph: bool) (iterations: int) (runtime: IRuntime) =
            let tree = AVal.init <| TestCommandTree.Generate 4
            let expectedColor = tree |> AVal.bind _.Root.ExpectedColor

            tree |> AVal.map _.Root |> testCommand runtime asSceneGraph (fun task ->
                for _ = 1 to iterations do
                    task |> renderAndCheck expectedColor

                    transact (fun _ ->
                        if Rnd.bool() then
                            tree.Value <- TestCommandTree.Generate 4
                        else
                            tree.Value.Randomize()
                    )
            )

    let tests (target: TestTarget) =
        [
            if target.IsGpu then
                "Ordered",                  Cases.ordered
                "IfThenElse",               Cases.ifThenElse
                "Clear",                    Cases.clear
                "Nested",                   Cases.nested false 16
                "Nested (as scene graph)",  Cases.nested true 16

            if target = TestTarget.Cpu then
                "Sequences.Queue streams retain every item",       Sequences.queueStreams
                "Sequences.Single-use streams enumerate once",     Sequences.singleUseStreams
                "Sequences.Producer effects occur once",           Sequences.sideEffects
                "Sequences.Producer failures dispose and survive", Sequences.producerFailures
                "Sequences.Repeatable snapshots and live inputs",  Sequences.repeatableControls
        ]
        |> prepareCases "Commands" target