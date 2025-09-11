namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.SceneGraph
open Aardvark.Application
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

    let tests (backend: Backend) =
        [
            "Ordered",                  Cases.ordered
            "IfThenElse",               Cases.ifThenElse
            "Clear",                    Cases.clear
            "Nested",                   Cases.nested false 16
            "Nested (as scene graph)",  Cases.nested true 16
        ]
        |> prepareCases backend "Commands"