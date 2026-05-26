namespace Aardvark.Rendering

open Aardvark.Base
open FShade
open FSharp.Data.Adaptive

/// Exact order-independent transparency via a bounded per-pixel k-buffer
/// (A-buffer). Transparent fragments are inserted into a fixed-size,
/// depth-sorted per-pixel array using a fragment-shader-interlock critical
/// section; a fullscreen resolve composites them front-to-back exactly.
///
/// This is the accurate (and heavier) alternative to WeightedBlendedOIT.
/// Selection between the two happens at TransparencyRenderTask compile time.
///
/// Storage (all per-pixel, K slots):
///   ABufferCount : R32UI         — number of stored fragments (clamped to K)
///   ABufferDepth : R32UI × K     — gl_FragCoord.z bit-cast to uint (positive
///                                  floats keep monotonic uint ordering)
///   ABufferColor : R32UI × K     — premultiplied RGBA packed via packUnorm4x8
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
        let ABufferDepth = Symbol.Create "ABufferDepth"
        let ABufferColor = Symbol.Create "ABufferColor"

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
        member x.ABufferCount : UIntImage2d<Formats.r32ui> = x?ABufferCount
        member x.ABufferDepth : UIntImage2d<Formats.r32ui> = x?ABufferDepth
        member x.ABufferColor : UIntImage2d<Formats.r32ui> = x?ABufferColor

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

    type Fragment = {
        [<Color>]     color : V4f
        [<FragCoord>] coord : V4f
    }

    /// Insert writer composed onto every transparent object's surface. Takes
    /// the upstream Colors output, premultiplies, and stores it into the
    /// per-pixel k-buffer inside an interlock critical section. Color writes
    /// are masked off by the render state; this stage only touches storage.
    let insert (f : Fragment) =
        fragment {
            let px = V2i f.coord.XY

            // premultiplied color for exact "over" compositing later
            let a = f.color.W
            let pc = V4f(f.color.XYZ * a, a)
            let dz = packDepth f.coord.Z
            let cc = packColor pc

            beginInterlock()

            let count = int (uniform.ABufferCount.[px].X)

            if count < Capacity then
                uniform.ABufferDepth.[V2i(px.X * Capacity + count, px.Y)] <- V4ui(dz, 0u, 0u, 0u)
                uniform.ABufferColor.[V2i(px.X * Capacity + count, px.Y)] <- V4ui(cc, 0u, 0u, 0u)
                uniform.ABufferCount.[px] <- V4ui(uint32 (count + 1), 0u, 0u, 0u)
            else
                // full: replace the farthest slot if this fragment is nearer
                let mutable maxSlot = 0
                let mutable maxDepth = 0u
                for i in 0 .. Capacity - 1 do
                    let d = uniform.ABufferDepth.[V2i(px.X * Capacity + i, px.Y)].X
                    if d > maxDepth then
                        maxDepth <- d
                        maxSlot <- i
                if dz < maxDepth then
                    uniform.ABufferDepth.[V2i(px.X * Capacity + maxSlot, px.Y)] <- V4ui(dz, 0u, 0u, 0u)
                    uniform.ABufferColor.[V2i(px.X * Capacity + maxSlot, px.Y)] <- V4ui(cc, 0u, 0u, 0u)

            endInterlock()

            return f.color
        }

    /// Fullscreen resolve. Reads the per-pixel k-buffer, sorts by depth, and
    /// composites the fragments front-to-back with premultiplied "over" into
    /// an output that the wrapper then alpha-blends over the opaque scene.
    let resolve (f : Fragment) =
        fragment {
            let px = V2i f.coord.XY
            let count = min Capacity (int (uniform.ABufferCount.[px].X))

            // load into local arrays
            let depths = Arr<N<8>, uint32>()
            let colors = Arr<N<8>, uint32>()
            for i in 0 .. count - 1 do
                depths.[i] <- uniform.ABufferDepth.[V2i(px.X * Capacity + i, px.Y)].X
                colors.[i] <- uniform.ABufferColor.[V2i(px.X * Capacity + i, px.Y)].X

            // insertion sort by depth (ascending = front to back)
            for i in 1 .. count - 1 do
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
            for i in 0 .. count - 1 do
                let src = unpackColor colors.[i]
                let t = 1.0f - accum.W
                accum <- accum + t * src

            return accum
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
