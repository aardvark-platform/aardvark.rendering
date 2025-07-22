namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open OpenTK.Graphics.OpenGL4

open System
open System.Reflection

[<Struct; RequireQualifiedAccess>]
type Framework =
    | GLFW
    | OpenTK

[<Struct; RequireQualifiedAccess>]
type TestBackend =
    | GL of Framework
    | Vulkan

type ITestApplication =
    inherit IDisposable
    abstract member Runtime : IRuntime

type TestApplication(inner : ITestApplication, onExit : bool -> unit) =
    do inner.Runtime.ShaderCachePath <- None
    let mutable completed = false

    member x.Runtime = inner.Runtime
    member x.Complete() = completed <- true
    member x.Dispose() = onExit completed

    interface IDisposable with
        member x.Dispose() = x.Dispose()

module TestApplication =

    module private GL =
        open OpenTK
        open Aardvark.Rendering.GL

        let private createOpenTK (debug : IDebugConfig) =
            let toolkit = Toolkit.Init(ToolkitOptions(Backend = PlatformBackend.PreferNative))

            let runtime = new Runtime(debug)
            let ctx = new Context(runtime, ContextHandleOpenTK.createWithParent runtime.DebugConfig)

            runtime.Initialize(ctx)

            { new ITestApplication with
                member x.Runtime = runtime
                member x.Dispose() =
                    runtime.Dispose()
                    toolkit.Dispose() }

        let private createGLFW (debug : IDebugConfig) =
            let app = new Slim.OpenGlApplication(debug)

            { new ITestApplication with
                member x.Runtime = app.Runtime
                member x.Dispose() = app.Dispose() }

        let create (framework : Framework) (debug : IDebugConfig) =
            Config.MajorVersion <- 4
            Config.MinorVersion <- 6
            RuntimeConfig.UseNewRenderTask <- true
            RuntimeConfig.RobustContextSharing <- true
            RuntimeConfig.PreferHostSideTextureCompression <- true

            let app =
                match framework with
                | Framework.GLFW -> createGLFW debug
                | Framework.OpenTK -> createOpenTK debug

            let ctx = (unbox<Runtime> app.Runtime).Context

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
                app,
                fun completed ->
                    if completed then checkForErrors()
                    app.Dispose()
                    if completed then checkForDebugErrors()
            )

    module private Vulkan =
        open Aardvark.Rendering.Vulkan

        let create (debug : IDebugConfig) =
            let headless = new HeadlessVulkanApplication(debug)

            let app =
                { new ITestApplication with
                    member x.Runtime = headless.Runtime
                    member x.Dispose() = headless.Dispose() }

            let onExit completed =
                app.Dispose()

                if completed then
                    let failed, errors =
                        let br = Environment.NewLine + Environment.NewLine
                        let msgs = headless.Instance.DebugSummary.ErrorMessages

                        msgs.Length > 0,
                        msgs |> String.concat br |> (+) br

                    if failed then
                        failwithf "Vulkan validation triggered errors: %s" errors

            new TestApplication(
                app, onExit
            )

    let mutable private aardvarkInitialized = false

    let create' (debug : IDebugConfig) (backend : TestBackend) =
        if not aardvarkInitialized then
            IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
            Aardvark.Init()
            aardvarkInitialized <- true

        match backend with
        | TestBackend.GL f -> GL.create f debug
        | TestBackend.Vulkan -> Vulkan.create debug

    let create (backend : TestBackend) =
        let config : IDebugConfig =
            if backend = TestBackend.Vulkan then
                // Disable GPU-AV (producing sporadic false positives with Vulkan SDK 1.3.280)
                { Vulkan.DebugConfig.Normal with
                    ValidationLayer = Some { Vulkan.ValidationLayerConfig.Full with ShaderBasedValidation = Vulkan.ShaderValidation.Disabled } }
            else
                { GL.DebugConfig.Normal with
                    DebugRenderTasks = true
                    DebugComputeTasks = true }

        backend |> create' config

    let createUse f backend =
        use app = create backend
        let result = f app.Runtime
        app.Complete()
        result
