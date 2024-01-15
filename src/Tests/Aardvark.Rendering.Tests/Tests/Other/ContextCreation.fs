namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.GL

open System.Threading
open FSharp.Data.Adaptive
open Expecto
open FShade
open OpenTK.Graphics.OpenGL4

module ``ContextCreation Tests`` =

    module Cases =

        let private checkShared (h : ContextHandle) (ctx : Context) =

            let createTexture() =
                use __ = ctx.ResourceLock
                let t = GL.GenTexture()
                GL.Check "failed to create texture"
                t

            let t = createTexture()

            use __ = ctx.RenderingLock h

            GL.BindTexture(TextureTarget.Texture2D, t)
            GL.Check "failed to bind texture"

            GL.DeleteTexture t
            GL.Check "failed to delete texture"

        let createAfterDispose (runtime : IRuntime) =
            let ctx = (unbox<Runtime> runtime).Context

            ctx.CreateContext().Dispose()
            use h = ctx.CreateContext()
            checkShared h ctx

        let createAfterMakeCurrent (runtime : IRuntime) =
            let ctx = (unbox<Runtime> runtime).Context

            use p = ctx.CreateContext()

            let mutable stop = false
            use event = new ManualResetEventSlim(false)

            let thread =
                startThread (fun _ ->
                    use __ = ctx.RenderingLock p

                    event.Set()

                    while not stop do
                        Thread.Sleep 10
                )

            try
                event.Wait()
                use h = ctx.CreateContext()
                checkShared h ctx

            finally
                stop <- true
                thread.Join()

    let private testsWithFramework (framework : Framework) =
        [
            // Tests context sharing and demonstrates that using the
            // last context as parent is not a good strategy. When sharing the
            // parent context must exist (duh!) and not be current on another thread.
            "Create after dispose",      Cases.createAfterDispose
            "Create after make current", Cases.createAfterMakeCurrent
        ]
        |> List.map (fun (name, test) ->
            let name = $"[{framework}] {name}"

            testCase name (fun () -> TestApplication.createUse test (TestBackend.GL framework))
            |> testSequenced
        )

    [<Tests>]
    let tests =
        testList "Context.Creation" [
            yield! testsWithFramework Framework.GLFW
            yield! testsWithFramework Framework.OpenTK
        ]