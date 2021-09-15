namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application

open System
open System.Reflection

type TestApplication(runtime : IRuntime, disposable : IDisposable) =
    member x.Runtime = runtime
    member x.Dispose() = disposable.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

module TestApplication =

    module private GL =
        open OpenTK
        open Aardvark.Rendering.GL

        let create() =
            Config.MajorVersion <- 4
            Config.MinorVersion <- 6
            Config.CheckErrors <- true
            Config.UsePixelUnpackBuffers <- true

            Toolkit.Init(ToolkitOptions(Backend = PlatformBackend.PreferNative)) |> ignore

            let runtime = new Runtime()
            let ctx = new Context(runtime, fun () -> ContextHandleOpenTK.create true)

            runtime.Initialize(ctx)

            new TestApplication(
                runtime,
                { new IDisposable with
                    member x.Dispose() =
                        runtime.Dispose()
                        ctx.Dispose()
                }
            )

    module private Vulkan =
        open Aardvark.Rendering.Vulkan

        let create() =
            let app = new HeadlessVulkanApplication(debug = true)

            new TestApplication(
                app.Runtime, app :> IDisposable
            )

    let create (backend : Backend) =
        IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
        Aardvark.Init()

        match backend with
        | Backend.GL -> GL.create()
        | Backend.Vulkan -> Vulkan.create()

    let createUse f backend =
        use app = create backend
        f app.Runtime
