namespace Aardvark.Rendering.Tests.Other

open System
open System.Runtime.CompilerServices
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Rendering.Vulkan
open Expecto

module DeviceDisposal =

    module Cases =

        // Not inlined, so no local of the caller keeps the application or its runtime alive.
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let private createAndDispose () =
            let app = new HeadlessVulkanApplication(false)
            let runtime = WeakReference(app.Runtime)
            app.Dispose()
            runtime

        // Applications created and disposed on the test thread, which outlives them: their runtimes must be collected
        // while the thread still runs.
        let runtimesCollected (_ : IRuntime) =
            let runtimes = List.init 4 (fun _ -> createAndDispose ())
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            let alive = runtimes |> List.filter _.IsAlive |> List.length
            Expect.equal alive 0 $"{alive} of {runtimes.Length} runtimes of disposed applications are still alive"

    let tests (target : TestTarget) =
        [
            "Runtimes of disposed applications are collected", Cases.runtimesCollected
        ]
        |> prepareCasesFor "Device disposal" target [TestTarget.Vulkan]
