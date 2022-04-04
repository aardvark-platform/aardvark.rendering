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

        let create (debug : DebugLevel) =
            Config.MajorVersion <- 4
            Config.MinorVersion <- 6
            RuntimeConfig.UseNewRenderTask <- true
            RuntimeConfig.PreferHostSideTextureCompression <- true

            Toolkit.Init(ToolkitOptions(Backend = PlatformBackend.PreferNative)) |> ignore

            let runtime = new Runtime(debug)
            let ctx = new Context(runtime, fun () -> ContextHandleOpenTK.create runtime.DebugLevel)

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

        let create (debug : DebugLevel) =
            let app = new HeadlessVulkanApplication(debug)

            new TestApplication(
                app.Runtime, app :> IDisposable
            )

    let create' (debug : DebugLevel) (backend : Backend) =
        IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
        Aardvark.Init()

        match backend with
        | Backend.GL -> GL.create debug
        | Backend.Vulkan -> Vulkan.create debug

    let create (backend : Backend) =
        backend |> create' DebugLevel.Normal

    let createUse f backend =
        use app = create backend
        f app.Runtime
