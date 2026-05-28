namespace Aardvark.Rendering

open Aardvark.Base
open FShade
open FSharp.Data.Adaptive

/// Exact order-independent transparency via a bounded per-pixel k-buffer
/// (A-buffer). Transparent fragments are inserted into a fixed-size,
/// depth-sorted per-pixel array using a fragment-shader-interlock critical
/// section; a fullscreen resolve composites them front-to-back exactly.
///
/// Storage layout (TWO images total — keep the descriptor-set binding count
/// minimal so we don't clash with the heap path's SSBOs / sampler arrays on
/// the same descriptor set; the binding allocator allocates per resource-type
/// in GL terms which collapses to one Vulkan namespace and silently collides
/// when storage images, samplers and SSBOs all sit on the same set):
///   ABufferCount : R32UI         — number of stored fragments (clamped to K)
///   ABufferSlot  : RGBA32UI × K  — all per-slot data packed into one image:
///                                    X = gl_FragCoord.z bit-cast to uint
///                                    Y = packUnorm4x8(premultiplied RGBA)
///                                    Z = gl_SampleMaskIn[0] coverage mask
///                                    W = unused
///                                  Packing the mask alongside depth/color
///                                  lets the resolve pass run per-sample and
///                                  pick exactly the fragments that covered
///                                  this sample — the fix for the MSAA
///                                  triangle-edge double-insert seam.
///
/// Concurrency: same-pixel fragments serialize through
/// begin/endInvocationInterlockARB. The required execution-mode layout
/// (`layout(pixel_interlock_unordered) in;`) is injected into the generated
/// GLSL by the backends' shader-compilation step (it can't be expressed in
/// FShade directly), keyed on the presence of the interlock intrinsic.
module ABufferOIT =

    /// Maximum number of transparent fragments stored per pixel. Fragments
    /// beyond this are merged into the farthest slot (graceful tail).
    [<Literal>]
    let Capacity = 8

    module Semantic =
        let ABufferCount = Symbol.Create "ABufferCount"
        let ABufferSlot  = Symbol.Create "ABufferSlot"

    [<AutoOpen>]
    module private Intrinsics =
        // The required-extension string makes FShade emit the matching
        // #extension directive. The beginInvocationInterlockARB function is
        // the ARB-named entry point; glslang and NVIDIA GL both recognise it
        // under GL_ARB_fragment_shader_interlock (the EXT extension only adds
        // the EXT-named aliases, which we don't use, and NVIDIA GL warns it is
        // unsupported — so we declare ARB only).
        // KeepCall stops FShade's optimizer from eliminating these
        // unit-returning, side-effecting calls (they have no usable result).
        [<KeepCall>]
        [<GLSLIntrinsic("beginInvocationInterlockARB()", "GL_ARB_fragment_shader_interlock")>]
        let beginInterlock() : unit = failwith "only in shader code"

        [<KeepCall>]
        [<GLSLIntrinsic("endInvocationInterlockARB()", "GL_ARB_fragment_shader_interlock")>]
        let endInterlock() : unit = failwith "only in shader code"

    // Storage uses wide 2D images (Capacity slots laid out along X) rather
    // than 2D-array images, because a plain image2D binds through the proven
    // graphics image-binding path; array images would need layered binding.
    // Slot s of pixel (x, y) lives at (x * Capacity + s, y).
    type UniformScope with
        member x.ABufferCount : UIntImage2d<Formats.r32ui>    = x?ABufferCount
        member x.ABufferSlot  : UIntImage2d<Formats.rgba32ui> = x?ABufferSlot
        /// Diagnostic toggle. 0 = normal composite. 1 = visualize the stored
        /// gl_SampleMaskIn coverage of slot 0 as a red intensity (full coverage
        /// 0xFF → bright red; partial-coverage edge pixels → dark red). If this
        /// shows uniform bright red even on triangle-interior edges, the
        /// backend's gl_SampleMaskIn isn't producing per-primitive coverage
        /// (suspected on MoltenVK under fragment_shader_interlock).
        member x.ABufferDebug : int = x?ABufferDebug

    [<AutoOpen>]
    module private Packing =
        // gl_FragCoord.z is in [0, 1]; bit-cast keeps uint ordering monotone
        // with depth for positive floats, so a uint compare = a depth compare.
        [<ReflectedDefinition; Inline>]
        let packDepth (z : float32) = Bitwise.FloatBitsToUInt z

        [<ReflectedDefinition; Inline>]
        let packColor (c : V4f) = packUnorm4x8 c

        [<ReflectedDefinition; Inline>]
        let unpackColor (u : uint32) = unpackUnorm4x8 u

    type InsertFragment = {
        [<Color>]      color : V4f
        [<FragCoord>]  coord : V4f
        /// `gl_SampleMaskIn[0]`. In MSAA, two triangles that touch at an edge
        /// each invoke the fragment shader once per pixel with a partial mask
        /// (e.g. samples 0..3 and 4..7). Storing the mask lets the resolve
        /// pick the right fragment per sample instead of compositing both as
        /// if each fully covered the pixel.
        [<SampleMask>] mask  : Arr<N<1>, int>
    }

    /// Insert writer composed onto every transparent object's surface. Takes
    /// the upstream Colors output, premultiplies, and stores it into the
    /// per-pixel k-buffer inside an interlock critical section. Color writes
    /// are masked off by the render state; this stage only touches storage.
    let insert (f : InsertFragment) =
        fragment {
            let px = V2i f.coord.XY

            // premultiplied color for exact "over" compositing later
            let a = f.color.W
            let pc = V4f(f.color.XYZ * a, a)
            let dz = packDepth f.coord.Z
            let cc = packColor pc
            let mm = uint32 f.mask.[0]
            let slot = V4ui(dz, cc, mm, 0u)

            beginInterlock()

            let count = int (uniform.ABufferCount.[px].X)

            if count < Capacity then
                uniform.ABufferSlot.[V2i(px.X * Capacity + count, px.Y)] <- slot
                uniform.ABufferCount.[px] <- V4ui(uint32 (count + 1), 0u, 0u, 0u)
            else
                // full: replace the farthest slot if this fragment is nearer
                let mutable maxSlot = 0
                let mutable maxDepth = 0u
                for i in 0 .. Capacity - 1 do
                    let d = uniform.ABufferSlot.[V2i(px.X * Capacity + i, px.Y)].X
                    if d > maxDepth then
                        maxDepth <- d
                        maxSlot <- i
                if dz < maxDepth then
                    uniform.ABufferSlot.[V2i(px.X * Capacity + maxSlot, px.Y)] <- slot

            endInterlock()

            return f.color
        }

    type ResolveFragment = {
        [<Color>]     color  : V4f
        [<FragCoord>] coord  : V4f
        /// `gl_SampleID`. Declaring this AND reading it forces sample-rate
        /// fragment-shader invocation: FShade emits gl_SampleID, the Vulkan
        /// pipeline gets sampleShadingEnable=true. Per invocation we only
        /// include the fragments whose stored coverage mask covers this
        /// sample — which is the whole point of the per-fragment mask.
        [<SampleId>]  sample : int
    }

    /// Fullscreen resolve. Per sample: walk the per-pixel k-buffer, take only
    /// fragments whose mask covers this sample, sort by depth, composite
    /// front-to-back with premultiplied "over". Output is per-sample; the
    /// blend pipeline composites it sample-by-sample over the opaque scene
    /// (which itself was written per-sample), so MSAA edges look right.
    let resolve (f : ResolveFragment) =
        fragment {
            let px = V2i f.coord.XY
            let count = min Capacity (int (uniform.ABufferCount.[px].X))
            let sampleBit = 1u <<< f.sample

            // DIAGNOSTIC: visualize slot-0's stored coverage mask. Full coverage
            // (0xFF) → bright red; partial-coverage edge pixels → dark red. Used
            // to verify gl_SampleMaskIn produces per-primitive coverage per
            // backend (toggle via AARDVARK_ABUFFER_DEBUG=1).
            let mutable dbg = V4f.Zero
            let mutable isDbg = false
            if uniform.ABufferDebug <> 0 then
                isDbg <- true
                if count > 0 then
                    // popcount of slot-0's coverage mask, normalized by 8 so the
                    // value is independent of subtle bit-position differences:
                    // full 8x coverage -> 1.0, full 4x -> 0.5, partial-coverage
                    // edge pixels -> proportionally less. If MoltenVK's
                    // gl_SampleMaskIn is broken this is uniform everywhere; if it
                    // works, triangle-interior edges read darker than interiors.
                    let mutable m = uniform.ABufferSlot.[V2i(px.X * Capacity, px.Y)].Z &&& 0xFFu
                    let mutable pc = 0u
                    for _i in 0 .. 7 do
                        pc <- pc + (m &&& 1u)
                        m <- m >>> 1
                    let v = float32 pc / 8.0f
                    dbg <- V4f(v, v, v, 1.0f)

            // gather only the slots whose coverage includes this sample
            let depths = Arr<N<8>, uint32>()
            let colors = Arr<N<8>, uint32>()
            let mutable n = 0
            for i in 0 .. count - 1 do
                let s = uniform.ABufferSlot.[V2i(px.X * Capacity + i, px.Y)]
                if (s.Z &&& sampleBit) <> 0u then
                    depths.[n] <- s.X
                    colors.[n] <- s.Y
                    n <- n + 1

            // insertion sort by depth (ascending = front to back)
            for i in 1 .. n - 1 do
                let dk = depths.[i]
                let ck = colors.[i]
                let mutable j = i - 1
                while j >= 0 && depths.[j] > dk do
                    depths.[j + 1] <- depths.[j]
                    colors.[j + 1] <- colors.[j]
                    j <- j - 1
                depths.[j + 1] <- dk
                colors.[j + 1] <- ck

            // premultiplied front-to-back "over"
            let mutable accum = V4f.Zero    // premultiplied rgb, alpha = coverage
            for i in 0 .. n - 1 do
                let src = unpackColor colors.[i]
                let t = 1.0f - accum.W
                accum <- accum + t * src

            return (if isDbg then dbg else accum)
        }

    /// Effect form of the fullscreen resolve.
    let resolveEffect : Effect = Effect.ofFunction resolve

    /// Composes the interlocked insert writer onto an existing surface (used
    /// by the RenderTask wrapper to transform transparent objects for the
    /// A-buffer build pass).
    let composeSurface (surface : Surface) : Surface =
        match surface with
        | Surface.Effect e -> Surface.Effect (Effect.compose [e; Effect.ofFunction insert])
        | Surface.Dynamic _ -> failwith "[A-buffer] dynamic surfaces are not yet supported for transparent objects"
        | Surface.Backend _ -> failwith "[A-buffer] backend surfaces cannot be marked transparent"
        | Surface.None -> failwith "[A-buffer] transparent objects need a surface"
