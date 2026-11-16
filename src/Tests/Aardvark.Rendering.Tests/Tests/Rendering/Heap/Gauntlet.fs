namespace Aardvark.Rendering.Tests.Rendering.Heap

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

/// The heap gauntlet: the pixel-exact and lifecycle probes of the HeapSpike example,
/// each a bool-returning test that logs its own verdict. The probes run on the test
/// framework's per-case Vulkan runtime (validation layers on, shader cache off) and
/// are sequenced by the framework.
module Gauntlet =

    module private Cases =

        let private check (run : IRuntime -> bool) (runtime : IRuntime) =
            Expect.isTrue (run runtime) "heap gauntlet probe failed (see log above)"

        /// Probes are process-global neighbors through the heap's statics: the page-size
        /// override some probes set is cleared before and after each one, so it cannot
        /// leak into the page layout of later probes.
        let probe (run : IRuntime -> bool) (runtime : IRuntime) =
            EnvVar.AARDVARK_HEAP_PAGE_WORDS <- null
            try check run runtime
            finally EnvVar.AARDVARK_HEAP_PAGE_WORDS <- null

        /// Like `probe` but with a page-size override: tiny pages force cross-page
        /// allocation and per-page draws. This configuration once caught a misrender
        /// on an Arc B580 and stays as the canary.
        let probeSmallPages (pageWords : int) (run : IRuntime -> bool) (runtime : IRuntime) =
            EnvVar.AARDVARK_HEAP_PAGE_WORDS <- string pageWords
            try check run runtime
            finally EnvVar.AARDVARK_HEAP_PAGE_WORDS <- null

    let tests (target : TestTarget) =
        [
            // core pixel-equivalence vs classic
            "golden",             Cases.probe Golden.run
            "plain",              Cases.probe Golden.plainTest
            "autofields",         Cases.probe Golden.autoFieldsTest
            "sgheap",             Cases.probe Golden.sgHeapTest
            "sgsphere",           Cases.probe Golden.sgSphereTest
            "sgprec",             Cases.probe Golden.sgPrecisionTest
            "visibility",         Cases.probe Golden.visibilityTest
            "bucketing",          Cases.probe Golden.bucketingTest
            "mode-rules",         Cases.probe Golden.modeRulesTest
            "msaa",               Cases.probe Golden.msaaTest
            "non-indexed",        Cases.probe Golden.nonIndexedTest
            "already-instanced",  Cases.probe Golden.alreadyInstancedTest
            // deferred / signature-dependent path
            "deferred",           Cases.probe Deferred.run
            // membership churn / lifecycle / leaks
            "churn",              Cases.probe Churn.run
            // 27648-word pages: the configuration in which the Arc B580 misrender
            // reproduces (4096-word pages do not)
            "churn-smallpages",   Cases.probeSmallPages 27648 Churn.run
            "churn-probe",        Cases.probe Golden.churnProbeTest
            "lifetime",           Cases.probe Golden.lifetimeTest
            "submit-stress",      Cases.probe Golden.submitStressTest
            "chain-leak",         Cases.probe Golden.chainLeakProbeTest
            "host-box-crash",     Cases.probe Golden.hostBoxCrashTest
            // dynamic (reactive) values
            "dyngeom",            Cases.probe Churn.dynGeom
            "dynpick",            Cases.probe Churn.dynPick
            "dynperf",            Cases.probe Churn.dynPerf
            "geom-value-dedup",   Cases.probe Golden.geomValueDedupTest
            "geom-churn",         Cases.probe Golden.geomChurnTest
            "geom-drift",         Cases.probe Golden.geomDriftTest
            // typed partitions / picking splits
            "mixedtypes",         Cases.probe Churn.mixedTypes
            "picksplit",          Cases.probe Churn.pickSplit
            "picksplit2",         Cases.probe Churn.pickSplit2
            // trafo chains
            "livechain",          Cases.probe Golden.liveChainTest
            "livechain-deep",     Cases.probe Golden.liveChainDeepTest
            "sgchain",            Cases.probe Golden.sgChainTest
            // ingestion / value plumbing
            "passthrough",        Cases.probe Golden.passthroughTest
            "native-buffer",      Cases.probe Golden.nativeBufTest
            "var-type",           Cases.probe Golden.varTypeTest
            "demo-shot",          Cases.probe Golden.demoShotTest
            // per-object textures (incl. swap = texture churn)
            "tex-heap",           Cases.probe Golden.texHeapTest
            "tex-swap",           Cases.probe Golden.texSwapTest
            "tex-state",          Cases.probe Golden.texStateTest
            "tex-cube",           Cases.probe Golden.texCubeTest
            // bindless geometry / SSBO arrays / textures / atlas
            "gpugeom",            Cases.probe Golden.gpuGeomTest
            "ssboarray",          Cases.probe Golden.ssboArrayTest
            "ssboarray2",         Cases.probe Golden.ssboArray2Test
            "ssboarray3",         Cases.probe Golden.ssboArray3Test
            "ssboarray4",         Cases.probe Golden.ssboArray4Test
            "ssboarray5",         Cases.probe Golden.ssboArray5Test
            "bindless-overcap",   Cases.probe Golden.bindlessOverCapacityTest
            "bindless-clean-box", Cases.probe Golden.bindlessCleanBoxTest
            "atlas-build",        Cases.probe Golden.atlasBuildTest
            "atlas-heap",         Cases.probe Golden.atlasHeapTest
            "atlas-pool",         Cases.probe Golden.atlasPoolTest
            "glyph-wedge",        Cases.probe Golden.glyphWedgeTest
        ]
        |> prepareCasesFor "Heap.Gauntlet" target [TestTarget.Vulkan]
