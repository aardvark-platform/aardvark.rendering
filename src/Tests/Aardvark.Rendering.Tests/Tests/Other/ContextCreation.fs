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

        module private TestTexture =

            let private textureData : uint8[] =
                Array.init 16 uint8

            let create() =
                let t = GL.GenTexture()
                GL.Check "failed to create texture"

                GL.BindTexture(TextureTarget.Texture1D, t)
                GL.Check "failed to bind texture"

                GL.TexImage1D(TextureTarget.Texture1D, 0, PixelInternalFormat.R8ui, textureData.Length, 0, PixelFormat.RedInteger, PixelType.UnsignedByte, textureData)
                GL.Check "failed to allocate texture"

                t

            let getAndCheck (texture : int) =
                GL.BindTexture(TextureTarget.Texture1D, texture)
                GL.Check "failed to bind texture"

                let result = Array.zeroCreate<uint8> textureData.Length
                GL.GetTexImage(TextureTarget.Texture1D, 0, PixelFormat.RedInteger, PixelType.UnsignedByte, result)
                GL.Check "failed to get texture image"

                if result <> textureData then
                    failwithf "Data retrieved from texture is wrong: %A" result

        module private TextureSharing =

            let check (ctx : Context) (handle : ContextHandle) =
                let texture =
                    use __ = ctx.ResourceLock
                    TestTexture.create()

                use __ = ctx.RenderingLock handle
                TestTexture.getAndCheck texture

                GL.DeleteTexture texture
                GL.Check "failed to delete texture"

        let createAfterDispose (runtime : IRuntime) =
            let ctx = (unbox<Runtime> runtime).Context

            ctx.CreateContext().Dispose()
            use h = ctx.CreateContext()
            TextureSharing.check ctx h

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
                TextureSharing.check ctx h

            finally
                stop <- true
                thread.Join()

        let createAfterMakeCurrentAll (runtime : IRuntime) =
            let ctx = (unbox<Runtime> runtime).Context

            let t =
                use __ = ctx.ResourceLock
                TestTexture.create()

            let count = RuntimeConfig.NumberOfResourceContexts
            let mutable stop = false
            use semaphore = new SemaphoreSlim(0, count)

            let threads =
                Array.init count (fun _ ->
                    startThread (fun _ ->
                        use __ = ctx.ResourceLock

                        semaphore.Release() |> ignore

                        while not stop do
                            Thread.Sleep 10
                    )
                )

            try
                for _ = 1 to count do
                    semaphore.Wait()

                use h = ctx.CreateContext()
                use __ = ctx.RenderingLock h

                TestTexture.getAndCheck t

                GL.DeleteTexture t
                GL.Check "failed to delete texture"

            finally
                stop <- true
                for t in threads do
                    t.Join()

    let private testsWithFramework (framework : Framework) =
        [
            // Tests context sharing and demonstrates that using the
            // last context as parent is not a good strategy. When sharing the
            // parent context must exist (duh!) and not be current on another thread.
            "Create after dispose",             Cases.createAfterDispose
            "Create after make current",        Cases.createAfterMakeCurrent

            // Worst case scenario in which all contexts are currently used.
            "Create after make current all",    Cases.createAfterMakeCurrentAll
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