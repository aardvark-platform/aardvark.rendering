namespace HeapSpike

// ── fsgather: SYNTHETIC WORST CASE for per-fragment dependent uniform gathers ──
//
// Question (2026-07-08): the heap's rewritten FS resolves every per-draw uniform
// via a dependent chain per fragment (flat slot -> header word -> arena offset ->
// value). With TINY triangles the rasterizer packs quads from DIFFERENT instances
// into one warp, so the chain is divergent AND serialized — the one place the
// "dependent reads are warp-uniform, hence free" argument does not hold. How bad
// is it really, and what do the standard remedies buy?
//
// Scene: a G×G grid of ~quadPx-sized screen-space quads (procedural, no vertex
// buffers), `layers` full-screen overdraw passes (depth test OFF), slot = the
// per-draw InstanceId. Every slot owns K=8 V4f params living SCATTERED in a value
// arena (random permutation — dedup means values sit anywhere), reached through a
// per-slot header table (heap-like two-hop).
//
// Variants (identical fragments, identical data, identical output):
//   fs2hop   — heap TODAY: FS does slot -> hdr[slot*8+i] -> arena[off], 8×, per fragment
//   fs1hop   — proposed fix: VS resolves the 8 offsets, passes them as TWO FLAT
//              ivec4 varyings (vec4-location packing!); FS does 8 independent
//              direct loads
//   vsfetch  — VS does the whole two-hop gather, passes 8 flat vec4 VALUES; FS
//              touches no memory
//   instattr — classic flat MDI: the 8 params are per-instance VERTEX ATTRIBUTES
//              (input assembler), passed through flat; FS touches no memory
module FsGather =
    open System
    open System.Diagnostics
    open Aardvark.Base
    open Aardvark.Rendering
    open FSharp.Data.Adaptive
    open FShade

    [<AutoOpen>]
    module private Sh =
        type UniformScope with
            member x.GridN  : int = uniform?GridN

        [<ReflectedDefinition>]
        let corner (vid : int) =
            if   vid = 0 then V2f(0.0f, 0.0f)
            elif vid = 1 then V2f(1.0f, 0.0f)
            elif vid = 2 then V2f(1.0f, 1.0f)
            elif vid = 3 then V2f(0.0f, 0.0f)
            elif vid = 4 then V2f(1.0f, 1.0f)
            else              V2f(0.0f, 1.0f)

        [<ReflectedDefinition>]
        let quadPos (vid : int) (slot : int) (g : int) =
            let cell = 2.0f / float32 g
            let c = corner vid
            let x = -1.0f + (float32 (slot % g) + c.X) * cell
            let y = -1.0f + (float32 (slot / g) + c.Y) * cell
            V4f(x, y, 0.0f, 1.0f)

        // ── fs2hop: heap today — the FS runs the whole dependent chain ──
        type VtxA = {
            [<Position>] pos : V4f
            [<VertexId>] vid : int
            [<InstanceId>] iid : int
            [<Semantic("Slot"); Interpolation(InterpolationMode.Flat)>] slot : int
        }
        let vsA (v : VtxA) =
            vertex {
                let g = uniform.GridN
                return { v with pos = quadPos v.vid (v.iid % (g*g)) g; slot = v.iid }
            }
        let fsA (v : VtxA) =
            fragment {
                let hdr   : int[] = uniform?StorageBuffer?Hdr
                let arena : V4f[] = uniform?StorageBuffer?Arena
                let mutable acc = V4f.Zero
                for i in 0 .. 7 do
                    acc <- acc + arena.[hdr.[v.slot * 8 + i]]
                return acc * 0.125f
            }

        // ── fscoher: FS direct loads from a LINEAR per-slot layout (no header,
        //    no permutation) — separates the fetch MECHANISM from the memory
        //    LAYOUT: same coherence as instattr, but per-fragment SSBO reads. ──
        let fsCoh (v : VtxA) =
            fragment {
                let lin : V4f[] = uniform?StorageBuffer?Lin
                let mutable acc = V4f.Zero
                for i in 0 .. 7 do
                    acc <- acc + lin.[v.slot * 8 + i]
                return acc * 0.125f
            }

        // ── fs1hop: VS resolves offsets, FS loads directly (flat ivec4-packed) ──
        type VtxC = {
            [<Position>] pos : V4f
            [<VertexId>] vid : int
            [<InstanceId>] iid : int
            [<Semantic("Offs0"); Interpolation(InterpolationMode.Flat)>] o0 : V4i
            [<Semantic("Offs1"); Interpolation(InterpolationMode.Flat)>] o1 : V4i
        }
        let vsC (v : VtxC) =
            vertex {
                let hdr : int[] = uniform?StorageBuffer?Hdr
                let g = uniform.GridN
                let s = v.iid
                let o0 = V4i(hdr.[s*8+0], hdr.[s*8+1], hdr.[s*8+2], hdr.[s*8+3])
                let o1 = V4i(hdr.[s*8+4], hdr.[s*8+5], hdr.[s*8+6], hdr.[s*8+7])
                return { v with pos = quadPos v.vid (v.iid % (g*g)) g; o0 = o0; o1 = o1 }
            }
        let fsC (v : VtxC) =
            fragment {
                let arena : V4f[] = uniform?StorageBuffer?Arena
                let acc =
                    arena.[v.o0.X] + arena.[v.o0.Y] + arena.[v.o0.Z] + arena.[v.o0.W] +
                    arena.[v.o1.X] + arena.[v.o1.Y] + arena.[v.o1.Z] + arena.[v.o1.W]
                return acc * 0.125f
            }

        // ── vsfetch: VS gathers the VALUES, FS reads only interpolants ──
        type VtxB = {
            [<Position>] pos : V4f
            [<VertexId>] vid : int
            [<InstanceId>] iid : int
            [<Semantic("Par0"); Interpolation(InterpolationMode.Flat)>] p0 : V4f
            [<Semantic("Par1"); Interpolation(InterpolationMode.Flat)>] p1 : V4f
            [<Semantic("Par2"); Interpolation(InterpolationMode.Flat)>] p2 : V4f
            [<Semantic("Par3"); Interpolation(InterpolationMode.Flat)>] p3 : V4f
            [<Semantic("Par4"); Interpolation(InterpolationMode.Flat)>] p4 : V4f
            [<Semantic("Par5"); Interpolation(InterpolationMode.Flat)>] p5 : V4f
            [<Semantic("Par6"); Interpolation(InterpolationMode.Flat)>] p6 : V4f
            [<Semantic("Par7"); Interpolation(InterpolationMode.Flat)>] p7 : V4f
        }
        let vsB (v : VtxB) =
            vertex {
                let hdr   : int[] = uniform?StorageBuffer?Hdr
                let arena : V4f[] = uniform?StorageBuffer?Arena
                let g = uniform.GridN
                let s = v.iid
                return { v with
                            pos = quadPos v.vid (v.iid % (g*g)) g
                            p0 = arena.[hdr.[s*8+0]]; p1 = arena.[hdr.[s*8+1]]
                            p2 = arena.[hdr.[s*8+2]]; p3 = arena.[hdr.[s*8+3]]
                            p4 = arena.[hdr.[s*8+4]]; p5 = arena.[hdr.[s*8+5]]
                            p6 = arena.[hdr.[s*8+6]]; p7 = arena.[hdr.[s*8+7]] }
            }
        let fsB (v : VtxB) =
            fragment {
                let acc = v.p0 + v.p1 + v.p2 + v.p3 + v.p4 + v.p5 + v.p6 + v.p7
                return acc * 0.125f
            }

        // ── instattr: params come in as per-instance vertex attributes (IA) ──
        type VtxI = {
            [<Position>] pos : V4f
            [<VertexId>] vid : int
            [<InstanceId>] iid : int
            [<Semantic("Ia0"); Interpolation(InterpolationMode.Flat)>] i0 : V4f
            [<Semantic("Ia1"); Interpolation(InterpolationMode.Flat)>] i1 : V4f
            [<Semantic("Ia2"); Interpolation(InterpolationMode.Flat)>] i2 : V4f
            [<Semantic("Ia3"); Interpolation(InterpolationMode.Flat)>] i3 : V4f
            [<Semantic("Ia4"); Interpolation(InterpolationMode.Flat)>] i4 : V4f
            [<Semantic("Ia5"); Interpolation(InterpolationMode.Flat)>] i5 : V4f
            [<Semantic("Ia6"); Interpolation(InterpolationMode.Flat)>] i6 : V4f
            [<Semantic("Ia7"); Interpolation(InterpolationMode.Flat)>] i7 : V4f
        }
        let vsI (v : VtxI) =
            vertex {
                let g = uniform.GridN
                return { v with pos = quadPos v.vid (v.iid % (g*g)) g }
            }
        let fsI (v : VtxI) =
            fragment {
                let acc = v.i0 + v.i1 + v.i2 + v.i3 + v.i4 + v.i5 + v.i6 + v.i7
                return acc * 0.125f
            }

    /// renderbench-style GPU timing: warmup, 3 rounds, median (min shown).
    let private measure (runtime : IRuntime) (task : IRenderTask) (fbo : IFramebuffer) (frames : int) =
        let gpuQuery = runtime.CreateTimeQuery()
        let getResult () = (gpuQuery.GetResult((), reset = true)).TotalMilliseconds
        let token = { RenderToken.Empty with Queries = [ gpuQuery ] }
        let output = OutputDescription.ofFramebuffer fbo
        task.Run(AdaptiveToken.Top, token, output)
        getResult () |> ignore
        for _ in 1 .. 30 do
            task.Run(AdaptiveToken.Top, token, output)
            getResult () |> ignore
        let round () =
            let mutable gpu = 0.0
            for _ in 1 .. frames do
                task.Run(AdaptiveToken.Top, token, output)
                gpu <- gpu + getResult ()
            gpu / float frames
        let rounds = Array.init 3 (fun _ -> round ()) |> Array.sort
        rounds.[1], rounds.[0]

    let run (argv : string[]) =
        let arg (name : string) (dflt : int) =
            match argv |> Array.tryFindIndex ((=) name) with
            | Some i when i + 1 < argv.Length -> int argv.[i + 1]
            | _ -> dflt
        let sizePx = arg "--size" 1024
        let quadPx = max 2 (arg "--quad" 6)
        let layers = max 1 (arg "--layers" 8)
        let frames = arg "--frames" 100
        let cold   = argv |> Array.contains "--coldslots"    // each layer reads a DISTINCT slot range (arena can exceed L2)

        Aardvark.Init()
        let chooser : Aardvark.Rendering.Vulkan.IDeviceChooser =
            match argv |> Array.tryFindIndex ((=) "--gpu") with
            | Some i when i + 1 < argv.Length ->
                let wanted = argv.[i + 1]
                { new Aardvark.Rendering.Vulkan.IDeviceChooser with
                    member _.Run devices =
                        match devices |> Array.tryFind (fun d -> d.Name.ToLowerInvariant().Contains(wanted.ToLowerInvariant())) with
                        | Some d -> d
                        | None ->
                            for d in devices do Log.warn "available GPU: %s" d.Name
                            failwithf "fsgather: no Vulkan device matches --gpu '%s'" wanted }
            | _ -> null
        use app = new Aardvark.Application.Slim.VulkanApplication(false, deviceChooser = chooser)
        let runtime = app.Runtime :> IRuntime
        Log.line "fsgather: device = %s" app.Runtime.Device.PhysicalDevice.Name

        let g = max 8 (sizePx / quadPx)          // grid dim
        let n = g * g * (if cold then layers else 1)   // data slots (position wraps mod g*g)
        let k = 8                                 // V4f params per slot (fixed in the shaders)
        Log.line "fsgather: %dx%d px, %dx%d grid (%d data slots%s, ~%dpx quads), %d overdraw layers, K=%d v4 params, %d frames"
            sizePx sizePx g g n (if cold then ", COLD" else "") quadPx layers k frames
        Log.line "fsgather: header %.1f MB + arena %.1f MB" (float (n*k*4) / 1e6) (float (n*k*16) / 1e6)
        Log.line "fsgather: ~%.1f M fragment invocations/frame, %d dependent loads each in fs2hop"
            (float (sizePx * sizePx * layers) / 1e6) (2 * k)

        // per-slot params scattered through the arena by a random permutation
        // (value-dedup means a slot's params sit ANYWHERE — the worst case).
        let rnd = RandomSystem 42
        let perm = Array.init (n * k) id
        for i in (n * k) - 1 .. -1 .. 1 do
            let j = rnd.UniformInt (i + 1)
            let t = perm.[i] in perm.[i] <- perm.[j]; perm.[j] <- t
        let hdr   = perm
        let arena = Array.zeroCreate<V4f> (n * k)
        let iattr = Array.init k (fun _ -> Array.zeroCreate<V4f> n)
        let linear = Array.zeroCreate<V4f> (n * k)          // coherent per-slot layout (fscoher)
        for s in 0 .. n - 1 do
            for i in 0 .. k - 1 do
                let v = V4f(float32 ((s * 31 + i * 7) % 255) / 255.0f, 0.25f, 0.5f, 1.0f)
                arena.[hdr.[s * k + i]] <- v
                iattr.[i].[s] <- v
                linear.[s * k + i] <- v

        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        use colorTex = runtime.CreateTexture2D(V2i sizePx, TextureFormat.Rgba8)
        use depthTex = runtime.CreateTexture2D(V2i sizePx, TextureFormat.Depth24Stencil8)
        use fbo =
            runtime.CreateFramebuffer(signature, [
                DefaultSemantic.Colors, colorTex.[TextureAspect.Color, 0, 0] :> IFramebufferOutput
                DefaultSemantic.DepthStencil, depthTex.[TextureAspect.DepthStencil, 0, 0] :> IFramebufferOutput ])

        // `layers` instanced draws of the full grid; InstanceId restarts per draw,
        // so slot = iid in every variant (incl. the instance-attribute one).
        let perDraw = g * g
        let calls = Array.init layers (fun l -> DrawCallInfo(FaceVertexCount = 6, InstanceCount = perDraw, FirstInstance = (if cold then l * perDraw else 0)))
        let bv (a : Array) (t : Type) = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)

        let variant (label : string) (eff : list<Effect>) (uniforms : (string * IAdaptiveValue) list) (instAttrs : (string * BufferView) list) =
            let ro = RenderObject()
            ro.Surface <- Surface.Effect (Effect.compose eff)
            ro.Mode    <- IndexedGeometryMode.TriangleList
            ro.VertexAttributes   <- AttributeProvider.ofList ([] : (Symbol * BufferView) list)
            ro.InstanceAttributes <- AttributeProvider.ofList [ for (nm, b) in instAttrs -> Symbol.Create nm, b ]
            ro.DrawCalls  <- DrawCalls.Direct (AVal.constant calls)
            // overdraw must actually shade: no depth test, no depth writes
            ro.DepthState <- { DepthState.Default with Test = AVal.constant DepthTest.None; WriteMask = AVal.constant false }
            ro.Uniforms   <- UniformProvider.ofList [ for (nm, v) in uniforms -> Symbol.Create nm, v ]
            use task =
                RenderTask.ofList [
                    runtime.CompileClear(signature, clear { color C4f.Black; depth 1.0 })
                    runtime.CompileRender(signature, ASet.single (ro :> IRenderObject)) ]
            let med, mn = measure runtime task fbo frames
            Log.line "fsgather[%-8s]: GPU %6.3f ms/frame (min %6.3f)" label med mn
            med

        let gridU  = "GridN", (AVal.constant g :> IAdaptiveValue)
        let hdrU   = "Hdr",   (AVal.constant hdr :> IAdaptiveValue)
        let arenaU = "Arena", (AVal.constant arena :> IAdaptiveValue)

        let linU = "Lin", (AVal.constant linear :> IAdaptiveValue)
        // identity hop: SAME two-hop shader as fs2hop, but the header points at a
        // LINEAR per-slot layout — measures the hop itself once its target is
        // coherent (the "make it local by allocation policy, keep the code" idea).
        let idHdr = Array.init (n * k) id
        let a  = variant "fs2hop"   [ Effect.ofFunction vsA; Effect.ofFunction fsA ] [ gridU; hdrU; arenaU ] []
        let ih = variant "fsidhop"  [ Effect.ofFunction vsA; Effect.ofFunction fsA ] [ gridU; ("Hdr", (AVal.constant idHdr :> IAdaptiveValue)); ("Arena", linU |> snd) ] []
        let ch = variant "fscoher"  [ Effect.ofFunction vsA; Effect.ofFunction fsCoh ] [ gridU; linU ] []
        let c  = variant "fs1hop"   [ Effect.ofFunction vsC; Effect.ofFunction fsC ] [ gridU; hdrU; arenaU ] []
        let b  = variant "vsfetch"  [ Effect.ofFunction vsB; Effect.ofFunction fsB ] [ gridU; hdrU; arenaU ] []
        let i  = variant "instattr" [ Effect.ofFunction vsI; Effect.ofFunction fsI ] [ gridU ]
                    [ for j in 0 .. k - 1 -> (sprintf "Ia%d" j), bv iattr.[j] typeof<V4f> ]

        Log.line "fsgather: fs2hop/instattr = %.2fx   fs2hop/fs1hop = %.2fx   fs2hop/vsfetch = %.2fx   fs2hop/fscoher = %.2fx"
            (a / i) (a / c) (a / b) (a / ch)
        Log.line "fsgather: the fs2hop->fs1hop delta is what the heap could recover by passing"
        Log.line "fsgather: resolved offsets VS->FS; fs1hop->vsfetch/instattr is the residual cost"
        Log.line "fsgather: of ANY per-fragment fetch vs pure interpolant consumption."
        0
