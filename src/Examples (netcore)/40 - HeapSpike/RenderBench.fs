namespace HeapSpike

// GPU render-time benchmark on SYNTHETIC data: what does the heap's per-vertex
// decode/gather actually cost vs the LOWER-BOUND baseline — ONE baked world-space
// soup mesh drawn with a plain VP-only shader (the CadSceneDemo `Baseline` floor)?
// Both paths shade the SAME vertex count non-indexed and the same pixels; the
// heap additionally pays: index decode, attribute pulls, per-draw uniform gathers
// (ModelTrafo is a real DERIVED field — fp64 compute collapse — like production),
// and N indirect draw records instead of 1 draw.
//
//   renderbench [--n 100000] [--size 1024] [--frames 60] [--min-tris 10] [--max-tris 100] [--classic]
//
// Measures REAL GPU time per frame via time queries (RenderToken.Queries — same
// method as CadSceneDemo's Offscreen.run), plus task.Run CPU time. Variants run
// SEQUENTIALLY (each torn down before the next) so peak memory doesn't stack.
//
// Vienna-shaped data: every object carries its OWN unique geometry (the heap
// dedups nothing) with a random triangle count in [--min-tris, --max-tris] —
// like a CAD scene of many small distinct parts.

module RenderBench =

    open System
    open System.Diagnostics
    open Aardvark.Base
    open Aardvark.Rendering
    open Aardvark.SceneGraph
    open FSharp.Data.Adaptive
    open Aardvark.Application
    open FShade

    [<AutoOpen>]
    module private Sh =
        type V = {
            [<Position>] pos : V4f
            [<Normal>]   n   : V3f
            [<Color>]    c   : V4f
        }

        /// heap path: per-object ModelTrafo (derived, compute-collapsed) + shared ViewProjTrafo
        let heapVert (v : V) =
            vertex {
                let wp = uniform.ModelTrafo * v.pos
                return { v with pos = uniform.ViewProjTrafo * wp; n = uniform.ModelTrafo.TransformDir v.n }
            }

        /// like heapVert plus a SECOND per-object matrix gather (`--second-matrix`):
        /// measures the marginal cost of one more per-vertex M44f field read.
        let heapVert2 (v : V) =
            vertex {
                let m2 : M44f = uniform?SecondTrafo
                let wp = uniform.ModelTrafo * (m2 * v.pos)
                return { v with pos = uniform.ViewProjTrafo * wp; n = uniform.ModelTrafo.TransformDir v.n }
            }

        /// baseline path: positions/normals baked to world space, VP only
        let bakedVert (v : V) =
            vertex {
                return { v with pos = uniform.ViewProjTrafo * v.pos }
            }

        type PV = {
            [<Position>]   pos : V4f
            [<Normal>]     n   : V3f
            [<Color>]      c   : V4f
            [<InstanceId>] iid : int
            [<VertexId>]   vtx : int
        }

        /// `--pack-probe` variant A — SCATTERED (mimics the heap today): a per-slot
        /// header hop yields OFFSETS, then the matrix/color come as SCALAR gathers
        /// from a separate region and geometry is pulled via header-provided bases.
        let scatVert (v : PV) =
            vertex {
                let hdr : int[]     = uniform?StorageBuffer?Hdr
                let mf  : float32[] = uniform?StorageBuffer?MatF
                let af  : float32[] = uniform?StorageBuffer?ArenaF
                let ii  : int[]     = uniform?StorageBuffer?IdxI
                let s = v.iid
                let matOff = hdr.[s * 8 + 0]
                let colOff = hdr.[s * 8 + 1]
                let idxB   = hdr.[s * 8 + 2]
                let vB     = hdr.[s * 8 + 3]
                let m =
                    M44f(mf.[matOff+0],  mf.[matOff+1],  mf.[matOff+2],  mf.[matOff+3],
                         mf.[matOff+4],  mf.[matOff+5],  mf.[matOff+6],  mf.[matOff+7],
                         mf.[matOff+8],  mf.[matOff+9],  mf.[matOff+10], mf.[matOff+11],
                         mf.[matOff+12], mf.[matOff+13], mf.[matOff+14], mf.[matOff+15])
                let col = V4f(mf.[colOff+0], mf.[colOff+1], mf.[colOff+2], mf.[colOff+3])
                let vid = ii.[idxB + v.vtx]
                let o = (vB + vid) * 6
                let p   = V4f(af.[o], af.[o+1], af.[o+2], 1.0f)
                let nrm = V3f(af.[o+3], af.[o+4], af.[o+5])
                let wp = m * p
                return { v with pos = uniform.ViewProjTrafo * wp; n = m.TransformDir nrm; c = col }
            }

        /// `--pack-probe` variant B — PACKED: everything per-draw (matrix rows, color,
        /// geometry bases) sits in ONE contiguous record at preparedUniforms[drawId] —
        /// vec4 loads, no header hop. Geometry pulls are IDENTICAL to variant A, so
        /// the delta is purely the uniform/header access pattern.
        let packVert (v : PV) =
            vertex {
                let pv : V4f[]      = uniform?StorageBuffer?PrepV
                let pi : int[]      = uniform?StorageBuffer?PrepI
                let af : float32[]  = uniform?StorageBuffer?ArenaF
                let ii : int[]      = uniform?StorageBuffer?IdxI
                let s = v.iid
                let r0 = pv.[s * 6 + 0]
                let r1 = pv.[s * 6 + 1]
                let r2 = pv.[s * 6 + 2]
                let r3 = pv.[s * 6 + 3]
                let col = pv.[s * 6 + 4]
                let idxB = pi.[s * 4 + 0]
                let vB   = pi.[s * 4 + 1]
                let m =
                    M44f(r0.X, r0.Y, r0.Z, r0.W,
                         r1.X, r1.Y, r1.Z, r1.W,
                         r2.X, r2.Y, r2.Z, r2.W,
                         r3.X, r3.Y, r3.Z, r3.W)
                let vid = ii.[idxB + v.vtx]
                let o = (vB + vid) * 6
                let p   = V4f(af.[o], af.[o+1], af.[o+2], 1.0f)
                let nrm = V3f(af.[o+3], af.[o+4], af.[o+5])
                let wp = m * p
                return { v with pos = uniform.ViewProjTrafo * wp; n = m.TransformDir nrm; c = col }
            }

        /// probe: per-object translation via gl_InstanceIndex -> SSBO. The SAME shader
        /// serves BOTH probe variants: n records with FirstInstance = i (gl_InstanceIndex
        /// = i) and ONE record with InstanceCount = n (hardware instance loop) — only
        /// the record structure differs, isolating the front-end's per-record cost.
        let probeVert (v : PV) =
            vertex {
                let offs : V4f[] = uniform?StorageBuffer?Offsets
                let p = v.pos + offs.[v.iid]
                return { v with pos = uniform.ViewProjTrafo * p }
            }

        let lit (v : V) =
            fragment {
                let l = Vec.normalize (V3f(1.0f, 2.0f, 3.0f))
                let d = 0.25f + 0.75f * max 0.0f (Vec.dot (Vec.normalize v.n) l)
                return V4f(v.c.XYZ * d, 1.0f)
            }

        // ── faithful heap-decode mimics for `--pack-probe` variants C/D ──
        // C reproduces the real chain: header cell -> 4-word ALLOCATION header
        // (typeId/length) -> data, with HeapPool's decode branch ladder.
        // D keeps the SAME ladder (full runtime flexibility) but reads
        // (typeId, length, dataOffset) DIRECTLY from the draw header — 1 indirection.
        type UniformScope with
            member x.PArenaF : float32[] = uniform?StorageBuffer?PArenaF
            member x.PArenaI : int[]     = uniform?StorageBuffer?PArenaI
            member x.PHdr    : int[]     = uniform?StorageBuffer?PHdr

        [<ReflectedDefinition>]
        let pF64 (p : int) : float32 =
            let lo = uniform.PArenaI.[p]
            let hi = uniform.PArenaI.[p + 1]
            let e = ((hi >>> 20) &&& 0x7FF) - 896
            let s = (hi >>> 31) <<< 31
            if e >= 255 then Fun.FloatFromBits(s ||| 0x7F800000)
            elif e <= 0 then 0.0f
            else
                let m = ((hi &&& 0xFFFFF) <<< 3) ||| ((lo >>> 29) &&& 0x7)
                Fun.FloatFromBits((s ||| (e <<< 23) ||| m) + ((lo >>> 28) &&& 1))

        /// the decode ladder over EXPLICIT (tid, len, dataOff) — shared by C (which
        /// loads them from the allocation header) and D (from the draw header).
        [<ReflectedDefinition>]
        let pDecodeV4f (tid : int) (len : int) (d : int) (v : int) : V4f =
            let e = v % len
            if tid = 13 then
                let o = d + e * 3
                V4f(uniform.PArenaF.[o], uniform.PArenaF.[o + 1], uniform.PArenaF.[o + 2], 1.0f)
            elif tid = 14 then
                let o = d + e * 4
                V4f(uniform.PArenaF.[o], uniform.PArenaF.[o + 1], uniform.PArenaF.[o + 2], uniform.PArenaF.[o + 3])
            elif tid = 40 then
                let w = uniform.PArenaI.[d + e]
                V4f(float32 ((w >>> 16) &&& 0xFF), float32 ((w >>> 8) &&& 0xFF), float32 (w &&& 0xFF), float32 ((w >>> 24) &&& 0xFF)) / 255.0f
            elif tid = 12 then
                let o = d + e * 2
                V4f(uniform.PArenaF.[o], uniform.PArenaF.[o + 1], 0.0f, 1.0f)
            elif tid = 11 then
                V4f(uniform.PArenaF.[d + e], 0.0f, 0.0f, 1.0f)
            elif tid = 33 then
                let o = d + e * 6
                V4f(pF64 o, pF64 (o + 2), pF64 (o + 4), 1.0f)
            elif tid = 34 then
                let o = d + e * 8
                V4f(pF64 o, pF64 (o + 2), pF64 (o + 4), pF64 (o + 6))
            elif tid = 23 then
                let o = d + e * 3
                V4f(float32 uniform.PArenaI.[o], float32 uniform.PArenaI.[o + 1], float32 uniform.PArenaI.[o + 2], 1.0f)
            else V4f(0.0f, 0.0f, 0.0f, 1.0f)

        [<ReflectedDefinition>]
        let pDecodeIdx (tid : int) (d : int) (v : int) : int =
            if tid < 0 then v
            elif tid = 2 then (uniform.PArenaI.[d + (v >>> 1)] >>> ((v &&& 1) <<< 4)) &&& 0xFFFF
            else uniform.PArenaI.[d + v]

        [<ReflectedDefinition>]
        let pMat (o : int) : M44f =
            M44f(uniform.PArenaF.[o+0],  uniform.PArenaF.[o+1],  uniform.PArenaF.[o+2],  uniform.PArenaF.[o+3],
                 uniform.PArenaF.[o+4],  uniform.PArenaF.[o+5],  uniform.PArenaF.[o+6],  uniform.PArenaF.[o+7],
                 uniform.PArenaF.[o+8],  uniform.PArenaF.[o+9],  uniform.PArenaF.[o+10], uniform.PArenaF.[o+11],
                 uniform.PArenaF.[o+12], uniform.PArenaF.[o+13], uniform.PArenaF.[o+14], uniform.PArenaF.[o+15])

        /// variant C — 3-deep faithful: header cell -> allocation header (tid/len) -> data.
        /// hdr stride 8: matOff, vptOff, colRef, posRef, nrmRef, idxRef
        let deepVert (v : PV) =
            vertex {
                let s = v.iid
                let matOff = uniform.PHdr.[s * 8 + 0]
                let vptOff = uniform.PHdr.[s * 8 + 1]
                let colRef = uniform.PHdr.[s * 8 + 2]
                let posRef = uniform.PHdr.[s * 8 + 3]
                let nrmRef = uniform.PHdr.[s * 8 + 4]
                let idxRef = uniform.PHdr.[s * 8 + 5]
                let vid =
                    if idxRef < 0 then v.vtx
                    else pDecodeIdx uniform.PArenaI.[idxRef] (idxRef + 4) v.vtx
                let p   = pDecodeV4f uniform.PArenaI.[posRef] uniform.PArenaI.[posRef + 1] (posRef + 4) vid
                let nrm = pDecodeV4f uniform.PArenaI.[nrmRef] uniform.PArenaI.[nrmRef + 1] (nrmRef + 4) vid
                let col = pDecodeV4f uniform.PArenaI.[colRef] uniform.PArenaI.[colRef + 1] (colRef + 4) vid
                let m  = pMat matOff
                let vp = pMat vptOff
                let wp = m * V4f(p.XYZ, 1.0f)
                return { v with pos = vp * wp; n = m.TransformDir nrm.XYZ; c = col }
            }

        /// pDecodeV4f WITHOUT the integer modulo: singleton broadcast via
        /// compare-select (len == 1 -> element 0) — same generality, no emulated
        /// integer division per attribute per vertex.
        [<ReflectedDefinition>]
        let pDecodeV4fNoMod (tid : int) (len : int) (d : int) (v : int) : V4f =
            let e = if len = 1 then 0 else v
            if tid = 13 then
                let o = d + e * 3
                V4f(uniform.PArenaF.[o], uniform.PArenaF.[o + 1], uniform.PArenaF.[o + 2], 1.0f)
            elif tid = 14 then
                let o = d + e * 4
                V4f(uniform.PArenaF.[o], uniform.PArenaF.[o + 1], uniform.PArenaF.[o + 2], uniform.PArenaF.[o + 3])
            elif tid = 40 then
                let w = uniform.PArenaI.[d + e]
                V4f(float32 ((w >>> 16) &&& 0xFF), float32 ((w >>> 8) &&& 0xFF), float32 (w &&& 0xFF), float32 ((w >>> 24) &&& 0xFF)) / 255.0f
            elif tid = 12 then
                let o = d + e * 2
                V4f(uniform.PArenaF.[o], uniform.PArenaF.[o + 1], 0.0f, 1.0f)
            elif tid = 11 then
                V4f(uniform.PArenaF.[d + e], 0.0f, 0.0f, 1.0f)
            elif tid = 33 then
                let o = d + e * 6
                V4f(pF64 o, pF64 (o + 2), pF64 (o + 4), 1.0f)
            elif tid = 34 then
                let o = d + e * 8
                V4f(pF64 o, pF64 (o + 2), pF64 (o + 4), pF64 (o + 6))
            elif tid = 23 then
                let o = d + e * 3
                V4f(float32 uniform.PArenaI.[o], float32 uniform.PArenaI.[o + 1], float32 uniform.PArenaI.[o + 2], 1.0f)
            else V4f(0.0f, 0.0f, 0.0f, 1.0f)

        type UniformScope with
            member x.PMatV : V4f[] = uniform?StorageBuffer?PMatV

        [<ReflectedDefinition>]
        let pMatV4 (row : int) : M44f =
            let r0 = uniform.PMatV.[row + 0]
            let r1 = uniform.PMatV.[row + 1]
            let r2 = uniform.PMatV.[row + 2]
            let r3 = uniform.PMatV.[row + 3]
            M44f(r0.X, r0.Y, r0.Z, r0.W,
                 r1.X, r1.Y, r1.Z, r1.W,
                 r2.X, r2.Y, r2.Z, r2.W,
                 r3.X, r3.Y, r3.Z, r3.W)

        /// variant E — flat header + NO modulo (generic; full ladder kept)
        let flatNoModVert (v : PV) =
            vertex {
                let s = v.iid
                let b = s * 20
                let matOff = uniform.PHdr.[b + 0]
                let vptOff = uniform.PHdr.[b + 1]
                let vid = pDecodeIdx uniform.PHdr.[b + 11] uniform.PHdr.[b + 12] v.vtx
                let p   = pDecodeV4fNoMod uniform.PHdr.[b + 5] uniform.PHdr.[b + 6] uniform.PHdr.[b + 7] vid
                let nrm = pDecodeV4fNoMod uniform.PHdr.[b + 8] uniform.PHdr.[b + 9] uniform.PHdr.[b + 10] vid
                let col = pDecodeV4fNoMod uniform.PHdr.[b + 2] uniform.PHdr.[b + 3] uniform.PHdr.[b + 4] vid
                let m  = pMat matOff
                let vp = pMat vptOff
                let wp = m * V4f(p.XYZ, 1.0f)
                return { v with pos = vp * wp; n = m.TransformDir nrm.XYZ; c = col }
            }

        /// variant F — E + matrices as vec4 loads from an ALIGNED V4f view (generic)
        let flatNoModVec4Vert (v : PV) =
            vertex {
                let s = v.iid
                let b = s * 20
                let matRow = uniform.PHdr.[b + 0]
                let vptRow = uniform.PHdr.[b + 1]
                let vid = pDecodeIdx uniform.PHdr.[b + 11] uniform.PHdr.[b + 12] v.vtx
                let p   = pDecodeV4fNoMod uniform.PHdr.[b + 5] uniform.PHdr.[b + 6] uniform.PHdr.[b + 7] vid
                let nrm = pDecodeV4fNoMod uniform.PHdr.[b + 8] uniform.PHdr.[b + 9] uniform.PHdr.[b + 10] vid
                let col = pDecodeV4fNoMod uniform.PHdr.[b + 2] uniform.PHdr.[b + 3] uniform.PHdr.[b + 4] vid
                let m  = pMatV4 matRow
                let vp = pMatV4 vptRow
                let wp = m * V4f(p.XYZ, 1.0f)
                return { v with pos = vp * wp; n = m.TransformDir nrm.XYZ; c = col }
            }

        /// one component of a DATA-DRIVEN decode: typeIds are arithmetic
        /// (class = tid/10: 1=f32, 2=i32, 3=f64; comps = tid%10), so component k
        /// loads via two selects instead of a 9-arm ladder.
        [<ReflectedDefinition>]
        let pLoadComp (cls : int) (o : int) (k : int) (comps : int) : float32 =
            if k >= comps then (if k = 3 then 1.0f else 0.0f)
            elif cls = 1 then uniform.PArenaF.[o + k]
            elif cls = 2 then float32 uniform.PArenaI.[o + k]
            else pF64 (o + k * 2)

        /// variant G — data-driven decode: SAME types, SAME flexibility, one shader,
        /// but the ladder collapses to tid arithmetic + 4 selecting component loads
        /// (C4b keeps its one special arm).
        [<ReflectedDefinition>]
        let pDecodeV4fDD (tid : int) (len : int) (d : int) (v : int) : V4f =
            let e = if len = 1 then 0 else v
            if tid = 40 then
                let w = uniform.PArenaI.[d + e]
                V4f(float32 ((w >>> 16) &&& 0xFF), float32 ((w >>> 8) &&& 0xFF), float32 (w &&& 0xFF), float32 ((w >>> 24) &&& 0xFF)) / 255.0f
            else
                let cls = tid / 10
                let comps = tid - cls * 10
                let o = d + e * comps * (if cls = 3 then 2 else 1)
                V4f(pLoadComp cls o 0 comps, pLoadComp cls o 1 comps, pLoadComp cls o 2 comps, pLoadComp cls o 3 comps)

        let flatDDVert (v : PV) =
            vertex {
                let s = v.iid
                let b = s * 20
                let matOff = uniform.PHdr.[b + 0]
                let vptOff = uniform.PHdr.[b + 1]
                let vid = pDecodeIdx uniform.PHdr.[b + 11] uniform.PHdr.[b + 12] v.vtx
                let p   = pDecodeV4fDD uniform.PHdr.[b + 5] uniform.PHdr.[b + 6] uniform.PHdr.[b + 7] vid
                let nrm = pDecodeV4fDD uniform.PHdr.[b + 8] uniform.PHdr.[b + 9] uniform.PHdr.[b + 10] vid
                let col = pDecodeV4fDD uniform.PHdr.[b + 2] uniform.PHdr.[b + 3] uniform.PHdr.[b + 4] vid
                let m  = pMat matOff
                let vp = pMat vptOff
                let wp = m * V4f(p.XYZ, 1.0f)
                return { v with pos = vp * wp; n = m.TransformDir nrm.XYZ; c = col }
            }

        /// variant D — FLAT header, same runtime flexibility: (tid, len, dataOff) per
        /// attribute directly in the draw header (ONE indirection to data).
        /// hdr stride 20: matOff, vptOff, col(tid,len,off), pos(...), nrm(...), idx(tid,off)
        let flatVert (v : PV) =
            vertex {
                let s = v.iid
                let b = s * 20
                let matOff = uniform.PHdr.[b + 0]
                let vptOff = uniform.PHdr.[b + 1]
                let vid = pDecodeIdx uniform.PHdr.[b + 11] uniform.PHdr.[b + 12] v.vtx
                let p   = pDecodeV4f uniform.PHdr.[b + 5] uniform.PHdr.[b + 6] uniform.PHdr.[b + 7] vid
                let nrm = pDecodeV4f uniform.PHdr.[b + 8] uniform.PHdr.[b + 9] uniform.PHdr.[b + 10] vid
                let col = pDecodeV4f uniform.PHdr.[b + 2] uniform.PHdr.[b + 3] uniform.PHdr.[b + 4] vid
                let m  = pMat matOff
                let vp = pMat vptOff
                let wp = m * V4f(p.XYZ, 1.0f)
                return { v with pos = vp * wp; n = m.TransformDir nrm.XYZ; c = col }
            }

    /// GPU ms/frame (time query) + task.Run CPU ms. Bandwidth-bound passes are very
    /// sensitive to the GPU's memory-clock power state, so: (1) a 30-frame WARMUP
    /// ramps the clocks before anything is timed, (2) `frames` timed frames are
    /// collected in 3 ROUNDS, (3) the MEDIAN round is reported (min in parens shows
    /// the full-clock floor). Heap-vs-baseline ratios use the medians.
    let private measure (runtime : IRuntime) (task : IRenderTask) (fbo : IFramebuffer) (frames : int) =
        // HEAPSPIKE_NO_GPU_QUERY=1: skip time queries entirely (KosmicKrisp beta
        // device-loses on them); task.Run blocks on the fence, so the reported CPU
        // ms ~= GPU + ~1ms submit overhead.
        let noQuery = System.Environment.GetEnvironmentVariable "HEAPSPIKE_NO_GPU_QUERY" = "1"
        let gpuQuery = if noQuery then Unchecked.defaultof<_> else runtime.CreateTimeQuery()
        let getResult () = if noQuery then 0.0 else (gpuQuery.GetResult((), reset = true)).TotalMilliseconds
        let token = if noQuery then RenderToken.Empty else { RenderToken.Empty with Queries = [ gpuQuery ] }
        let output = OutputDescription.ofFramebuffer fbo
        task.Run(AdaptiveToken.Top, token, output)
        getResult () |> ignore                                               // build + first submit
        for _ in 1 .. 30 do                                                  // clock ramp-up
            task.Run(AdaptiveToken.Top, token, output)
            getResult () |> ignore
        let round () =
            let sw = Stopwatch()
            let mutable gpu = 0.0
            for _ in 1 .. frames do
                sw.Start()
                task.Run(AdaptiveToken.Top, token, output)
                sw.Stop()
                gpu <- gpu + getResult ()
            gpu / float frames, sw.Elapsed.TotalMilliseconds / float frames
        let rounds = Array.init 3 (fun _ -> round ())
        let byGpu = rounds |> Array.sortBy fst
        let (medGpu, medCpu) = byGpu.[1]
        let (minGpu, _) = byGpu.[0]
        medGpu, medCpu, minGpu

    let run (argv : string[]) =
        let arg (name : string) (dflt : int) =
            match argv |> Array.tryFindIndex ((=) name) with
            | Some i when i + 1 < argv.Length -> int argv.[i + 1]
            | _ -> dflt
        let n        = arg "--n" 100000
        let sizePx   = arg "--size" 1024
        let frames   = arg "--frames" 60
        let minTris  = max 1 (arg "--min-tris" 10)
        let maxTris  = max minTris (arg "--max-tris" 100)
        let classic  = argv |> Array.contains "--classic"
        let secondMatrix = argv |> Array.contains "--second-matrix"

        Aardvark.Init()

        // ── synthetic scene (Vienna-shaped): n objects in a grid, EVERY object its
        //    own UNIQUE geometry (fresh arrays — the heap dedups nothing) with a
        //    random triangle count in [minTris, maxTris]. Each mesh is a cone fan:
        //    apex + ring, T triangles, indexed. ──
        let s = int (ceil (sqrt (float n)))
        let extent = float s * 1.2
        let posOf (i : int) = V3d(float (i % s - s/2) * 1.2, float (i / s - s/2) * 1.2, 0.0)
        let palette = [| C4b(230,60,60); C4b(60,200,60); C4b(60,120,230); C4b(230,200,40); C4b(210,60,210); C4b(40,210,210) |]
        let rnd = RandomSystem 42
        let mkMesh () =
            let t = minTris + rnd.UniformInt (maxTris - minTris + 1)
            let ps = Array.zeroCreate<V3f> (t + 1)
            let ns = Array.zeroCreate<V3f> (t + 1)
            ps.[0] <- V3f(0.0f, 0.0f, 0.45f)
            ns.[0] <- V3f.OOI
            for k in 0 .. t - 1 do
                let a = float32 k / float32 t * float32 Constant.PiTimesTwo
                ps.[k + 1] <- V3f(0.4f * cos a, 0.4f * sin a, 0.0f)
                ns.[k + 1] <- Vec.normalize (V3f(cos a, sin a, 0.7f))
            let idx = Array.zeroCreate<int> (t * 3)
            for k in 0 .. t - 1 do
                idx.[k * 3]     <- 0
                idx.[k * 3 + 1] <- 1 + k
                idx.[k * 3 + 2] <- 1 + ((k + 1) % t)
            ps, ns, idx
        let meshes = Array.init n (fun _ -> mkMesh ())
        let totalDrawnVerts = meshes |> Array.sumBy (fun (_, _, idx) -> int64 idx.Length)
        Log.line "renderbench: n=%d  tris/obj in [%d, %d] (unique geometry each)  %.1f M drawn verts total  %dx%d px  %d frames"
            n minTris maxTris (float totalDrawnVerts / 1e6) sizePx sizePx frames

        let bv (a : Array) (t : Type) = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let heapEffect  =
            Effect.compose [
                (if secondMatrix then Effect.ofFunction Sh.heapVert2 else Effect.ofFunction Sh.heapVert)
                Effect.ofFunction Sh.lit ]
        let bakedEffect = Effect.compose [ Effect.ofFunction Sh.bakedVert; Effect.ofFunction Sh.lit ]

        let mkHeapRO (viewProj : IAdaptiveValue) (i : int) =
            let (ps, ns, idx) = meshes.[i]
            let ro = RenderObject()
            ro.Surface <- Surface.Effect heapEffect
            ro.Mode    <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <-
                AttributeProvider.ofList [
                    DefaultSemantic.Positions, bv ps typeof<V3f>
                    DefaultSemantic.Normals,   bv ns typeof<V3f>
                    // per-object singleton color attribute (length-1 broadcast)
                    DefaultSemantic.Colors,    BufferView(SingleValueBuffer<C4b>(AVal.constant palette.[i % palette.Length]), typeof<C4b>) ]
            ro.Indices   <- Some (bv idx typeof<int>)
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = idx.Length, InstanceCount = 1) |])
            ro.Uniforms  <-
                UniformProvider.ofList [
                    yield Symbol.Create "ModelTrafo",    (AVal.constant (Trafo3d.Translation (posOf i)) :> IAdaptiveValue)
                    yield Symbol.Create "ViewProjTrafo", viewProj
                    // fresh aval per RO: DISTINCT region per slot (dedup is by aval
                    // identity), so the gather reads per-object offsets like a real
                    // per-part matrix would
                    if secondMatrix then
                        yield Symbol.Create "SecondTrafo", (AVal.constant M44f.Identity :> IAdaptiveValue) ]
            ro :> IRenderObject

        if argv |> Array.contains "--window" then
            // ── `--window`: the heap scene in a REAL GameWindow with a turntable
            //    camera — for Nsight GPU Trace (present-based frame delimiters,
            //    F11 hotkey) and eyeballing. Runs until the window closes. ──
            let win = window { backend Backend.Vulkan; display Display.Mono; debug false; samples 1 }
            let sw = Stopwatch.StartNew()
            let viewProj =
                (win.Sizes, win.Time) ||> AVal.map2 (fun szw _ ->
                    let a = sw.Elapsed.TotalSeconds * 0.25
                    let eye = V3d(cos a, sin a, 0.55) * extent
                    let view = CameraView.lookAt eye V3d.Zero V3d.OOI |> CameraView.viewTrafo
                    let proj = Frustum.perspective 70.0 0.1 (extent * 10.0) (float szw.X / float szw.Y) |> Frustum.projTrafo
                    view * proj)
            let objs = Array.init n (mkHeapRO (viewProj :> IAdaptiveValue))
            win.Scene <- Sg.renderObjectSet (Heap.ofRenderObjectsAuto (ASet.ofArray objs))
            win.Run()
            0
        else

        // `--gpu <substring>`: pick the physical device by (case-insensitive) name
        // match instead of wrestling with VK_DRIVER_FILES/ICD paths. No match ->
        // list what's there and fail loudly.
        let chooser : Aardvark.Rendering.Vulkan.IDeviceChooser =
            match argv |> Array.tryFindIndex ((=) "--gpu") with
            | Some i when i + 1 < argv.Length ->
                let wanted = argv.[i + 1]
                { new Aardvark.Rendering.Vulkan.IDeviceChooser with
                    member _.Run devices =
                        match devices |> Array.tryFind (fun d -> d.Name.ToLowerInvariant().Contains(wanted.ToLowerInvariant())) with
                        | Some d ->
                            Log.line "renderbench: --gpu '%s' -> %s" wanted d.Name
                            d
                        | None ->
                            for d in devices do Log.warn "available GPU: %s" d.Name
                            failwithf "renderbench: no Vulkan device matches --gpu '%s'" wanted }
            | _ -> null
        // `--dump-glsl`: print every compiled shader (use with a small --n and pipe to a file)
        // HEADLESS: renderbench renders into its own FBO — no window, no GLFW. (The
        // GLFW-backed Slim application crashed on machines with a pre-3.4 system
        // libglfw: missing glfwInitVulkanLoader — seen on the 5090 arcbench run.)
        use app =
            if argv |> Array.contains "--dump-glsl" then
                new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication(
                    { Aardvark.Rendering.Vulkan.DebugConfig.None with PrintShaderCode = true } :> IDebugConfig,
                    deviceChooser = chooser)
            else
                new Aardvark.Rendering.Vulkan.HeadlessVulkanApplication(false, deviceChooser = chooser)
        Log.line "renderbench: device = %s" app.Runtime.Device.PhysicalDevice.Name
        let runtime = app.Runtime :> IRuntime
        let size = V2i(sizePx, sizePx)
        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]

        let view = CameraView.lookAt (V3d(0.0, -0.9, 0.75) * extent) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 (extent * 10.0) 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue

        let clearVals = clear { color (C4f(0.1f, 0.1f, 0.15f, 1.0f)); depth 1.0 }
        let renderWith (label : string) (objects : aset<IRenderObject>) =
            use colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba8)
            use depthTex = runtime.CreateTexture2D(size, TextureFormat.Depth24Stencil8)
            use fbo =
                runtime.CreateFramebuffer(signature, [
                    DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                    DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ])
            use task =
                RenderTask.ofList [
                    runtime.CompileClear(signature, clearVals)
                    runtime.CompileRender(signature, objects) ]
            let gpu, cpu, minGpu = measure runtime task fbo frames
            Log.line "renderbench[%s]: GPU %.2f ms/frame (min %.2f)   task.Run CPU %.2f ms/frame" label gpu minGpu cpu
            gpu

        if argv |> Array.contains "--pack-probe" then
            // ── does CPU-PRE-PACKED per-draw data beat the heap's scattered
            //    header-hop + scalar gathers? Same unique meshes, same records,
            //    IDENTICAL geometry pulls — only the uniform/header access differs. ──
            let totalIdx = int totalDrawnVerts
            let totalUnique = meshes |> Array.sumBy (fun (ps, _, _) -> ps.Length)
            let arenaF = Array.zeroCreate<float32> (totalUnique * 6)
            let idxI   = Array.zeroCreate<int> totalIdx
            let hdr    = Array.zeroCreate<int> (n * 8)
            let matF   = Array.zeroCreate<float32> (n * 20)
            let prepV  = Array.zeroCreate<V4f> (n * 6)
            let prepI  = Array.zeroCreate<int> (n * 4)
            let recs   = Array.zeroCreate<DrawCallInfo> n
            let mutable ib = 0
            let mutable vb = 0
            for i in 0 .. n - 1 do
                let (ps, ns, idx) = meshes.[i]
                let t = V3f (posOf i)
                let col = palette.[i % palette.Length].ToC4f()
                // row-major translation matrix + color, scalar region (variant A)
                let mo = i * 20
                matF.[mo+0] <- 1.0f;  matF.[mo+5] <- 1.0f;  matF.[mo+10] <- 1.0f;  matF.[mo+15] <- 1.0f
                matF.[mo+3] <- t.X;   matF.[mo+7] <- t.Y;   matF.[mo+11] <- t.Z
                matF.[mo+16] <- col.R; matF.[mo+17] <- col.G; matF.[mo+18] <- col.B; matF.[mo+19] <- col.A
                hdr.[i*8+0] <- mo; hdr.[i*8+1] <- mo + 16; hdr.[i*8+2] <- ib; hdr.[i*8+3] <- vb
                // packed record (variant B): 4 matrix rows + color + geometry bases
                prepV.[i*6+0] <- V4f(1.0f, 0.0f, 0.0f, t.X)
                prepV.[i*6+1] <- V4f(0.0f, 1.0f, 0.0f, t.Y)
                prepV.[i*6+2] <- V4f(0.0f, 0.0f, 1.0f, t.Z)
                prepV.[i*6+3] <- V4f(0.0f, 0.0f, 0.0f, 1.0f)
                prepV.[i*6+4] <- V4f(col.R, col.G, col.B, col.A)
                prepI.[i*4+0] <- ib; prepI.[i*4+1] <- vb
                for k in 0 .. idx.Length - 1 do idxI.[ib + k] <- idx.[k]
                for j in 0 .. ps.Length - 1 do
                    let o = (vb + j) * 6
                    arenaF.[o]   <- ps.[j].X; arenaF.[o+1] <- ps.[j].Y; arenaF.[o+2] <- ps.[j].Z
                    arenaF.[o+3] <- ns.[j].X; arenaF.[o+4] <- ns.[j].Y; arenaF.[o+5] <- ns.[j].Z
                recs.[i] <- DrawCallInfo(FaceVertexCount = idx.Length, FirstIndex = 0, BaseVertex = 0, FirstInstance = i, InstanceCount = 1)
                ib <- ib + idx.Length
                vb <- vb + ps.Length
            let mkVariantCalls (calls : DrawCallInfo[]) (label : string) (eff : Effect) (uniforms : (string * IAdaptiveValue) list) =
                let ro = RenderObject()
                ro.Surface <- Surface.Effect eff
                ro.Mode    <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- AttributeProvider.ofList ([] : (Symbol * BufferView) list)
                ro.DrawCalls <- DrawCalls.Indirect (AVal.constant (IndirectBuffer.ofArray calls))
                ro.Uniforms  <- UniformProvider.ofList [ for (nm, v) in uniforms -> Symbol.Create nm, v ]
                renderWith label (ASet.single (ro :> IRenderObject))
            let mkVariant = mkVariantCalls recs
            let cAF = AVal.constant arenaF :> IAdaptiveValue
            let cII = AVal.constant idxI :> IAdaptiveValue
            Log.line "pack-probe: %d objects, %.1f M drawn verts, identical geometry pulls" n (float totalIdx / 1e6)
            let scat =
                mkVariant "scattered (heap-like)"
                    (Effect.compose [ Effect.ofFunction Sh.scatVert; Effect.ofFunction Sh.lit ])
                    [ "ViewProjTrafo", viewProj
                      "Hdr", (AVal.constant hdr :> IAdaptiveValue); "MatF", (AVal.constant matF :> IAdaptiveValue)
                      "ArenaF", cAF; "IdxI", cII ]
            let packed =
                mkVariant "packed (preparedUniforms[drawId])"
                    (Effect.compose [ Effect.ofFunction Sh.packVert; Effect.ofFunction Sh.lit ])
                    [ "ViewProjTrafo", viewProj
                      "PrepV", (AVal.constant prepV :> IAdaptiveValue); "PrepI", (AVal.constant prepI :> IAdaptiveValue)
                      "ArenaF", cAF; "IdxI", cII ]
            Log.line "pack-probe: scattered / packed = %.2fx" (scat / packed)

            // ── variants C (3-deep faithful: header cell -> allocation header -> data,
            //    real decode ladder, VPT gathered from the arena like the heap) and
            //    D (SAME ladder + flexibility, but tid/len/dataOff sit DIRECTLY in the
            //    draw header — one indirection). C validates the mimic against the
            //    real heap; D answers: how much do the extra indirections cost? ──
            let vCounts = meshes |> Array.map (fun (ps, _, _) -> ps.Length)
            let words =
                16 + (Array.init n (fun i ->
                    let (ps, _, idx) = meshes.[i]
                    16 + 2 * (4 + ps.Length * 3) + 5 + 4 + idx.Length) |> Array.sum)
            let aF = Array.zeroCreate<float32> words
            let aI = Array.zeroCreate<int> words
            let inline wf (w : int) (f : float32) = aF.[w] <- f; aI.[w] <- BitConverter.SingleToInt32Bits f
            let inline wi (w : int) (x : int) = aI.[w] <- x; aF.[w] <- BitConverter.Int32BitsToSingle x
            let hdrC = Array.zeroCreate<int> (n * 8)
            let hdrD = Array.zeroCreate<int> (n * 20)
            // shared ViewProjTrafo region at word 0 (dedup like the heap's shared aval)
            let vpM = M44f.op_Explicit (view * proj).Forward
            let packMat (w : int) (m : M44f) =
                wf (w+0)  m.M00; wf (w+1)  m.M01; wf (w+2)  m.M02; wf (w+3)  m.M03
                wf (w+4)  m.M10; wf (w+5)  m.M11; wf (w+6)  m.M12; wf (w+7)  m.M13
                wf (w+8)  m.M20; wf (w+9)  m.M21; wf (w+10) m.M22; wf (w+11) m.M23
                wf (w+12) m.M30; wf (w+13) m.M31; wf (w+14) m.M32; wf (w+15) m.M33
            packMat 0 vpM
            let mutable w = 16
            for i in 0 .. n - 1 do
                let (ps, ns, idx) = meshes.[i]
                let t = V3f (posOf i)
                let c = palette.[i % palette.Length]
                let colorBits = (int c.B) ||| (int c.G <<< 8) ||| (int c.R <<< 16) ||| (int c.A <<< 24)
                let matOff = w
                packMat w (M44f(1.0f, 0.0f, 0.0f, t.X,
                                0.0f, 1.0f, 0.0f, t.Y,
                                0.0f, 0.0f, 1.0f, t.Z,
                                0.0f, 0.0f, 0.0f, 1.0f))
                w <- w + 16
                let posRef = w
                wi w 13; wi (w+1) ps.Length; wi (w+2) 12; wi (w+3) 0
                w <- w + 4
                for j in 0 .. ps.Length - 1 do wf (w+j*3) ps.[j].X; wf (w+j*3+1) ps.[j].Y; wf (w+j*3+2) ps.[j].Z
                w <- w + ps.Length * 3
                let nrmRef = w
                wi w 13; wi (w+1) ns.Length; wi (w+2) 12; wi (w+3) 0
                w <- w + 4
                for j in 0 .. ns.Length - 1 do wf (w+j*3) ns.[j].X; wf (w+j*3+1) ns.[j].Y; wf (w+j*3+2) ns.[j].Z
                w <- w + ns.Length * 3
                let colRef = w
                wi w 40; wi (w+1) 1; wi (w+2) 4; wi (w+3) 0; wi (w+4) colorBits
                w <- w + 5
                let idxRef = w
                wi w 1; wi (w+1) idx.Length; wi (w+2) 4; wi (w+3) 0
                w <- w + 4
                for j in 0 .. idx.Length - 1 do wi (w+j) idx.[j]
                w <- w + idx.Length
                // C: refs to allocation headers
                hdrC.[i*8+0] <- matOff; hdrC.[i*8+1] <- 0
                hdrC.[i*8+2] <- colRef; hdrC.[i*8+3] <- posRef; hdrC.[i*8+4] <- nrmRef; hdrC.[i*8+5] <- idxRef
                // D: tid/len/dataOff directly in the draw header
                let b = i * 20
                hdrD.[b+0] <- matOff; hdrD.[b+1] <- 0
                hdrD.[b+2] <- 40; hdrD.[b+3] <- 1;         hdrD.[b+4] <- colRef + 4
                hdrD.[b+5] <- 13; hdrD.[b+6] <- ps.Length; hdrD.[b+7] <- posRef + 4
                hdrD.[b+8] <- 13; hdrD.[b+9] <- ns.Length; hdrD.[b+10] <- nrmRef + 4
                hdrD.[b+11] <- 1; hdrD.[b+12] <- idxRef + 4
            let cF = AVal.constant aF :> IAdaptiveValue
            let cI = AVal.constant aI :> IAdaptiveValue
            let deep =
                mkVariant "3-deep faithful (heap chain)"
                    (Effect.compose [ Effect.ofFunction Sh.deepVert; Effect.ofFunction Sh.lit ])
                    [ "PArenaF", cF; "PArenaI", cI; "PHdr", (AVal.constant hdrC :> IAdaptiveValue) ]
            let flat =
                mkVariant "flat header (tid/len/off in draw header)"
                    (Effect.compose [ Effect.ofFunction Sh.flatVert; Effect.ofFunction Sh.lit ])
                    [ "PArenaF", cF; "PArenaI", cI; "PHdr", (AVal.constant hdrD :> IAdaptiveValue) ]
            Log.line "pack-probe: 3-deep / flat-header = %.2fx   (flat keeps the full decode ladder)" (deep / flat)

            // ── variants E (flat + NO integer modulo: singleton via compare-select)
            //    and F (E + matrices as ALIGNED vec4 loads) — both fully generic,
            //    same one shader for any bucket content. ──
            // matrices in a V4f row array: vpt rows [0..3], object i rows [4 + i*4 ..]
            let matV = Array.zeroCreate<V4f> ((n + 1) * 4)
            matV.[0] <- vpM.R0; matV.[1] <- vpM.R1; matV.[2] <- vpM.R2; matV.[3] <- vpM.R3
            let hdrE = Array.copy hdrD
            for i in 0 .. n - 1 do
                let t = V3f (posOf i)
                matV.[4 + i*4 + 0] <- V4f(1.0f, 0.0f, 0.0f, t.X)
                matV.[4 + i*4 + 1] <- V4f(0.0f, 1.0f, 0.0f, t.Y)
                matV.[4 + i*4 + 2] <- V4f(0.0f, 0.0f, 1.0f, t.Z)
                matV.[4 + i*4 + 3] <- V4f(0.0f, 0.0f, 0.0f, 1.0f)
            let hdrF = Array.copy hdrD
            for i in 0 .. n - 1 do
                hdrF.[i*20 + 0] <- 4 + i * 4        // matRow in the V4f view
                hdrF.[i*20 + 1] <- 0                // vptRow
            let noMod =
                mkVariant "flat + no-modulo (compare-select singleton)"
                    (Effect.compose [ Effect.ofFunction Sh.flatNoModVert; Effect.ofFunction Sh.lit ])
                    [ "PArenaF", cF; "PArenaI", cI; "PHdr", (AVal.constant hdrE :> IAdaptiveValue) ]
            let vec4 =
                mkVariant "flat + no-modulo + vec4 matrices"
                    (Effect.compose [ Effect.ofFunction Sh.flatNoModVec4Vert; Effect.ofFunction Sh.lit ])
                    [ "PArenaF", cF; "PArenaI", cI
                      "PHdr", (AVal.constant hdrF :> IAdaptiveValue)
                      "PMatV", (AVal.constant matV :> IAdaptiveValue) ]
            let dd =
                mkVariant "flat + no-mod + data-driven decode"
                    (Effect.compose [ Effect.ofFunction Sh.flatDDVert; Effect.ofFunction Sh.lit ])
                    [ "PArenaF", cF; "PArenaI", cI; "PHdr", (AVal.constant hdrE :> IAdaptiveValue) ]
            Log.line "pack-probe: generic-shader ladder — 3-deep %.2f | flat %.2f | +no-mod %.2f | +vec4-mat %.2f | +data-driven %.2f (vs typed 2-deep %.2f)"
                deep flat noMod vec4 dd scat

            // ── CLUSTER probe: same HEAVY gather shader, ONE instanced record instead of
            //    n records. Only valid when all objects share a vertex count
            //    (--min-tris = --max-tris). If warp RESIDENCY (not FE throughput) is what
            //    small records cost, the heavy shader shows it where the trivial-shader
            //    --probe could not. ──
            if minTris = maxTris then
                let one = [| DrawCallInfo(FaceVertexCount = minTris * 3, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = n) |]
                let deepInst =
                    mkVariantCalls one "3-deep faithful INSTANCED (1 record)"
                        (Effect.compose [ Effect.ofFunction Sh.deepVert; Effect.ofFunction Sh.lit ])
                        [ "PArenaF", cF; "PArenaI", cI; "PHdr", (AVal.constant hdrC :> IAdaptiveValue) ]
                Log.line "pack-probe: heavy shader n-records / 1-instanced = %.2fx  (cluster idea pays if >> 1)" (deep / deepInst)
            0
        elif argv |> Array.contains "--probe" then
            // ── FE probe: IDENTICAL vertex work, identical shader — n indirect records
            //    (FirstInstance = i, InstanceCount = 1) vs ONE instanced record
            //    (InstanceCount = n). Isolates the GPU front-end's per-record cost
            //    against the primitive distributor's per-instance cost — the merged-
            //    record design bets the latter is far cheaper. ──
            let t = (minTris + maxTris) / 2
            let (ps0, ns0, idx0) =
                // fixed-size cone (deterministic): exactly t triangles
                let ps = Array.init (t + 1) (fun k ->
                    if k = 0 then V3f(0.0f, 0.0f, 0.45f)
                    else let a = float32 (k-1) / float32 t * float32 Constant.PiTimesTwo in V3f(0.4f * cos a, 0.4f * sin a, 0.0f))
                let ns = Array.init (t + 1) (fun k ->
                    if k = 0 then V3f.OOI
                    else let a = float32 (k-1) / float32 t * float32 Constant.PiTimesTwo in Vec.normalize (V3f(cos a, sin a, 0.7f)))
                let idx = Array.init (t * 3) (fun j ->
                    let k = j / 3
                    match j % 3 with 0 -> 0 | 1 -> 1 + k | _ -> 1 + ((k + 1) % t))
                ps, ns, idx
            let vc = t * 3
            let soupP = Array.init vc (fun k -> ps0.[idx0.[k]])
            let soupN = Array.init vc (fun k -> ns0.[idx0.[k]])
            let soupC = Array.create vc C4b.White
            let offsets = Array.init n (fun i -> V4f(V3f (posOf i), 0.0f))
            let probeEffect = Effect.compose [ Effect.ofFunction Sh.probeVert; Effect.ofFunction Sh.lit ]
            let probeRO (calls : DrawCalls) =
                let ro = RenderObject()
                ro.Surface <- Surface.Effect probeEffect
                ro.Mode    <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <-
                    AttributeProvider.ofList [
                        DefaultSemantic.Positions, bv soupP typeof<V3f>
                        DefaultSemantic.Normals,   bv soupN typeof<V3f>
                        DefaultSemantic.Colors,    bv soupC typeof<C4b> ]
                ro.DrawCalls <- calls
                ro.Uniforms  <-
                    UniformProvider.ofList [
                        Symbol.Create "ViewProjTrafo", viewProj
                        Symbol.Create "Offsets", (AVal.constant offsets :> IAdaptiveValue) ]
                ro :> IRenderObject
            let records =
                Array.init n (fun i -> DrawCallInfo(FaceVertexCount = vc, FirstIndex = 0, BaseVertex = 0, FirstInstance = i, InstanceCount = 1))
            let one =
                [| DrawCallInfo(FaceVertexCount = vc, FirstIndex = 0, BaseVertex = 0, FirstInstance = 0, InstanceCount = n) |]
            Log.line "probe: %d objects x %d verts (%.1f M verts), identical shader" n vc (float n * float vc / 1e6)
            let mdi  = renderWith "probe-n-records"   (ASet.single (probeRO (DrawCalls.Indirect (AVal.constant (IndirectBuffer.ofArray records)))))
            let inst = renderWith "probe-1-instanced" (ASet.single (probeRO (DrawCalls.Indirect (AVal.constant (IndirectBuffer.ofArray one)))))
            Log.line "probe: n-records / instanced = %.2fx" (mdi / inst)
            0
        elif argv |> Array.contains "--churnperf" then
            // ── LOCALITY-DECAY probe: does churn (free-list reuse scattering later
            //    slots' regions into old holes) measurably slow the FRAME? Fresh
            //    ingest is measured, then `--churn-rounds` rounds each remove every
            //    2nd live object and re-add a FRESH RO for the same index (new
            //    BufferViews/avals -> dedup misses -> regions reallocate from holes,
            //    maximal scatter), then the SAME task is measured again. The delta
            //    is the win a slot-major compaction placement could recover. ──
            let rounds = arg "--churn-rounds" 6
            let current = Array.init n (mkHeapRO viewProj)
            let live = cset<IRenderObject> current
            use colorTex = runtime.CreateTexture2D(size, TextureFormat.Rgba8)
            use depthTex = runtime.CreateTexture2D(size, TextureFormat.Depth24Stencil8)
            use fbo2 =
                runtime.CreateFramebuffer(signature, [
                    DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                    DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ])
            use task =
                RenderTask.ofList [
                    runtime.CompileClear(signature, clearVals)
                    runtime.CompileRender(signature, Heap.ofRenderObjectsAuto live) ]
            let gpuF, _, minF = measure runtime task fbo2 frames
            let c0 = Heap.compactionCount
            Log.line "churnperf[fresh  ]: GPU %.2f ms/frame (min %.2f)" gpuF minF
            let out = OutputDescription.ofFramebuffer fbo2
            let frame () = task.Run(AdaptiveToken.Top, RenderToken.Empty, out)
            for r in 1 .. rounds do
                let parity = r % 2
                let victims = [ for i in 0 .. n - 1 do if i % 2 = parity then yield current.[i] ]
                transact (fun () -> for v in victims do live.Remove v |> ignore)
                frame ()                                       // flush the frees
                for i in 0 .. n - 1 do
                    if i % 2 = parity then current.[i] <- mkHeapRO viewProj i
                transact (fun () -> for i in 0 .. n - 1 do (if i % 2 = parity then live.Add current.[i] |> ignore))
                frame ()
                Log.line "churnperf: round %d done (compactions so far: %d)" r (Heap.compactionCount - c0)
            let gpuC, _, minC = measure runtime task fbo2 frames
            Log.line "churnperf[churned]: GPU %.2f ms/frame (min %.2f)   compactions during churn: %d" gpuC minC (Heap.compactionCount - c0)
            Log.line "churnperf: churned/fresh = %.3fx  (locality decay recoverable by slot-major compaction)" (gpuC / gpuF)
            0
        else

        // ── heap: n objects -> bucket indirect draws ──
        let heapObjs = Array.init n (mkHeapRO viewProj)
        let heapGpu = renderWith "heap" (Heap.ofRenderObjectsAuto (ASet.ofArray heapObjs))
        Log.line "renderbench[heap]: %d bucket(s)" Heap.lastBucketCount

        // ── baseline: ONE baked world-space soup mesh, single draw (lower bound) ──
        let totalV = int totalDrawnVerts
        let bp = Array.zeroCreate<V3f> totalV
        let bn = Array.zeroCreate<V3f> totalV
        let bc = Array.zeroCreate<C4b> totalV
        let mutable o = 0
        for i in 0 .. n - 1 do
            let (ps, ns, idx) = meshes.[i]
            let t = V3f (posOf i)
            let col = palette.[i % palette.Length]
            for k in 0 .. idx.Length - 1 do
                bp.[o] <- ps.[idx.[k]] + t
                bn.[o] <- ns.[idx.[k]]
                bc.[o] <- col
                o <- o + 1
        let bakedRO =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect bakedEffect
            ro.Mode    <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes <-
                AttributeProvider.ofList [
                    DefaultSemantic.Positions, bv bp typeof<V3f>
                    DefaultSemantic.Normals,   bv bn typeof<V3f>
                    DefaultSemantic.Colors,    bv bc typeof<C4b> ]
            ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = totalV, InstanceCount = 1) |])
            ro.Uniforms  <- UniformProvider.ofList [ Symbol.Create "ViewProjTrafo", viewProj ]
            ro :> IRenderObject
        let baseGpu = renderWith "baked-baseline" (ASet.single bakedRO)

        // ── optional: classic N individual draws (slow to prepare at large n) ──
        if classic then
            renderWith "classic-n-draws" (ASet.ofArray (Array.init n (mkHeapRO viewProj))) |> ignore

        Log.line "renderbench: heap/baseline = %.2fx   (target < 2x)" (heapGpu / baseGpu)
        0
