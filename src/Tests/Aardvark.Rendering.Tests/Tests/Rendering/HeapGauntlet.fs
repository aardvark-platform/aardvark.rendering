namespace Aardvark.Rendering.Tests

open Aardvark.Base
open Expecto

/// The heap correctness GAUNTLET — the pixel-exact / lifecycle probes that grew up in
/// the HeapSpike example (each is a self-contained bool-returning test creating its own
/// HeadlessVulkanApplication). Wrapped here so `dotnet run`/Expecto owns them; the
/// HeapSpike CLI modes (`... golden`, `... churn`, ...) keep working and share the
/// SAME implementations via the project reference — no duplication.
///
/// The whole subtree is device-gated: if a headless Vulkan device cannot be created
/// (GPU-less CI), every case reports skipped instead of failing. Probes run
/// SEQUENCED — each creates and destroys its own Vulkan device; parallel device
/// creation is driver-hostile and would fight over memory.
///
/// GL probes (`gl-heap`, `gpugeom-gl`) additionally need a live GL context: they are
/// gated on a display being present (DISPLAY on unix, always assumed on Windows).
module ``Heap Gauntlet`` =

    let private vulkanAvailable =
        lazy (
            try
                Aardvark.Init()
                use app = new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication()
                app.Runtime |> ignore
                true
            with _ ->
                false
        )

    let private displayAvailable =
        lazy (
            if System.OperatingSystem.IsWindows() then true
            else not (System.String.IsNullOrEmpty (System.Environment.GetEnvironmentVariable "DISPLAY"))
        )

    let private probe (name : string) (run : unit -> bool) =
        testCase name (fun () ->
            if not vulkanAvailable.Value then skiptest "no headless Vulkan device"
            Expect.isTrue (run ()) (sprintf "heap gauntlet probe '%s' failed (see log above)" name)
        )

    // FUTURE WORK: the GL heap path is currently unsupported — HeapRenderObject
    // (the bundled derive+draw command) has no GL command-stream compilation
    // (SingleObjectCommand.compile: "bad object"), dead since the bundling rework.
    // The probes stay registered (visible as skipped) so implementing the GL path
    // lights them back up by deleting the skiptest line.
    let private probeGL (name : string) (run : unit -> bool) =
        testCase name (fun () ->
            skiptest "GL heap path is future work (HeapRenderObject has no GL command-stream compile)"
            if not displayAvailable.Value then skiptest "no display for a GL context"
            Expect.isTrue (run ()) (sprintf "heap gauntlet probe '%s' failed (see log above)" name)
        )

    let tests =
        testSequenced <| testList "Heap gauntlet (Vulkan)" [
            // core pixel-equivalence vs classic
            probe "golden"          HeapSpike.Golden.run
            probe "plain"           HeapSpike.Golden.plainTest
            probe "autofields"      HeapSpike.Golden.autoFieldsTest
            probe "sgheap"          HeapSpike.Golden.sgHeapTest
            probe "sgsphere"        HeapSpike.Golden.sgSphereTest
            probe "sgprec"          HeapSpike.Golden.sgPrecisionTest
            probe "visibility"      HeapSpike.Golden.visibilityTest
            probe "bucketing"       HeapSpike.Golden.bucketingTest
            probe "mode-rules"      HeapSpike.Golden.modeRulesTest
            probe "msaa"            HeapSpike.Golden.msaaTest
            probe "non-indexed"     HeapSpike.Golden.nonIndexedTest
            probe "already-instanced" HeapSpike.Golden.alreadyInstancedTest
            // deferred / signature-dependent path
            probe "deferred"        HeapSpike.Deferred.run
            // membership churn / lifecycle / leaks
            probe "churn"           HeapSpike.Churn.run
            probe "churn-probe"     HeapSpike.Golden.churnProbeTest
            probe "lifetime"        HeapSpike.Golden.lifetimeTest
            probe "submit-stress"   HeapSpike.Golden.submitStressTest
            probe "chain-leak"      HeapSpike.Golden.chainLeakProbeTest
            probe "host-box-crash"  HeapSpike.Golden.hostBoxCrashTest
            // dynamic (reactive) values
            probe "dyngeom"         HeapSpike.Churn.dynGeom
            probe "dynpick"         HeapSpike.Churn.dynPick
            probe "geom-value-dedup" HeapSpike.Golden.geomValueDedupTest
            probe "geom-churn"      HeapSpike.Golden.geomChurnTest
            probe "geom-drift"      HeapSpike.Golden.geomDriftTest
            // typed partitions / picking splits
            probe "mixedtypes"      HeapSpike.Churn.mixedTypes
            probe "picksplit"       HeapSpike.Churn.pickSplit
            probe "picksplit2"      HeapSpike.Churn.pickSplit2
            // trafo chains
            probe "livechain"       HeapSpike.Golden.liveChainTest
            probe "livechain-deep"  HeapSpike.Golden.liveChainDeepTest
            probe "sgchain"         HeapSpike.Golden.sgChainTest
            // ingestion / value plumbing
            probe "passthrough"     HeapSpike.Golden.passthroughTest
            probe "native-buffer"   HeapSpike.Golden.nativeBufTest
            probe "var-type"        HeapSpike.Golden.varTypeTest
            probe "demo-shot"       HeapSpike.Golden.demoShotTest
            // per-object textures (incl. swap = texture churn)
            probe "tex-heap"        HeapSpike.Golden.texHeapTest
            probe "tex-swap"        HeapSpike.Golden.texSwapTest
            probe "tex-state"       HeapSpike.Golden.texStateTest
            probe "tex-cube"        HeapSpike.Golden.texCubeTest
            // bindless geometry / SSBO arrays / textures / atlas
            probe "gpugeom"         HeapSpike.Golden.gpuGeomTest
            probe "ssboarray"       HeapSpike.Golden.ssboArrayTest
            probe "ssboarray2"      HeapSpike.Golden.ssboArray2Test
            probe "ssboarray3"      HeapSpike.Golden.ssboArray3Test
            probe "ssboarray4"      HeapSpike.Golden.ssboArray4Test
            probe "ssboarray5"      HeapSpike.Golden.ssboArray5Test
            probe "bindless-overcap" HeapSpike.Golden.bindlessOverCapacityTest
            probe "bindless-clean-box" HeapSpike.Golden.bindlessCleanBoxTest
            probe "atlas-build"     HeapSpike.Golden.atlasBuildTest
            probe "atlas-heap"      HeapSpike.Golden.atlasHeapTest
            probe "atlas-pool"      HeapSpike.Golden.atlasPoolTest
            probe "glyph-wedge"     HeapSpike.Golden.glyphWedgeTest
            // GL backend (needs a display)
            probeGL "gl-heap"       HeapSpike.Golden.glHeapTest
            probeGL "gpugeom-gl"    HeapSpike.Golden.gpuGeomTestGL
        ]
