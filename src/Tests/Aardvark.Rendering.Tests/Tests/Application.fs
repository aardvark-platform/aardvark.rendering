namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open OpenTK.Graphics.OpenGL4

open System
open System.Reflection

type TestApplication(runtime : IRuntime, disposable : IDisposable) =
    do runtime.ShaderCachePath <- None

    member x.Runtime = runtime
    member x.Dispose() = disposable.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

module TestApplication =

    module private GL =
        open OpenTK
        open Aardvark.Rendering.GL

        let create (debug : IDebugConfig) =
            Config.MajorVersion <- 4
            Config.MinorVersion <- 6
            RuntimeConfig.UseNewRenderTask <- true
            RuntimeConfig.PreferHostSideTextureCompression <- true

            let toolkit = Toolkit.Init(ToolkitOptions(Backend = PlatformBackend.PreferNative))

            let runtime = new Runtime(debug)
            let ctx = new Context(runtime, fun () -> ContextHandleOpenTK.create runtime.DebugConfig)

            runtime.Initialize(ctx)

            let checkForErrors() =
                use __ = ctx.ResourceLock
                let err = GL.GetError()

                if err <> ErrorCode.NoError then
                    failwithf "OpenGL returned error: %A" err

            let checkForDebugErrors() =
                let failed, errors =
                    let br = Environment.NewLine + Environment.NewLine
                    let msgs = ctx.GetDebugErrors()

                    msgs.Length > 0,
                    msgs |> String.concat br |> (+) br

                if failed then
                    failwithf "OpenGL debug output reported errors: %s" errors

            new TestApplication(
                runtime,
                { new IDisposable with
                    member x.Dispose() =
                        checkForErrors()
                        runtime.Dispose()
                        checkForDebugErrors()
                        toolkit.Dispose()
                }
            )

    module private Vulkan =
        open Aardvark.Rendering.Vulkan

        let create (debug : IDebugConfig) =
            CustomDeviceChooser.Register Seq.head
            let app = new HeadlessVulkanApplication(debug)
            let onExit =
                { new IDisposable with
                    member x.Dispose() =
                        app.Dispose()

                        let failed, errors =
                            let br = Environment.NewLine + Environment.NewLine
                            let msgs = app.Instance.DebugSummary.ErrorMessages

                            msgs.Length > 0,
                            msgs |> String.concat br |> (+) br

                        if failed then
                            failwithf "Vulkan validation triggered errors: %s" errors
                }

            new TestApplication(
                app.Runtime, onExit
            )

    let mutable private aardvarkInitialized = false

    let create' (debug : IDebugConfig) (backend : Backend) =
        if not aardvarkInitialized then
            IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
            Aardvark.Init()
            aardvarkInitialized <- true

        match backend with
        | Backend.GL -> GL.create debug
        | Backend.Vulkan -> Vulkan.create debug

    let create (backend : Backend) =
        let config : IDebugConfig =
            if backend = Backend.Vulkan then
                { Vulkan.DebugConfig.Normal with
                    ValidationLayer = Some Vulkan.ValidationLayerConfig.Full }
            else
                { GL.DebugConfig.Normal with
                    DebugRenderTasks = true
                    DebugComputeTasks = true }

        backend |> create' config

    let createUse f backend =
        let app = create backend
        f app.Runtime
        app.Dispose()
