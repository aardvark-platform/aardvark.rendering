namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

/// Weighted Blended Order-Independent Transparency (McGuire/Bavoil 2013).
///
/// Reusable building blocks for the OIT technique:
///   - default semantic symbols for the accum and revealage attachments
///   - blend modes used by the transparent pass
///   - FShade effects that produce / consume the OIT buffers
///   - a helper to compose the OIT fragment writer onto an existing Surface
///
/// The RenderTask wrapper (TransparencyRenderTask) uses these to transform
/// every RenderObject with IsTransparent=true.
module private WeightedBlendedOIT =
    open FShade

    module Semantic =
        /// RGBA16F target: sum of (color.rgb * alpha * weight, alpha * weight).
        let Accum = Symbol.Create "Accum"

        /// R32F target: the product of (1 - alpha) over all fragments, the transmittance.
        let Revealage = Symbol.Create "Revealage"

        /// Sampler name read by the composite shader (the Accum texture from the OIT pass).
        let AccumBuffer = Symbol.Create "AccumBuffer"

        /// Sampler name read by the composite shader (the Revealage texture from the OIT pass).
        let RevealageBuffer = Symbol.Create "RevealageBuffer"

    module BlendModes =
        /// Additive blend for the Accum attachment (one source + one destination).
        let accum = BlendMode.Add

        /// Multiplicative transmittance for the Revealage attachment:
        ///   result = 0*src + (1 - src.r) * dst
        let revealage =
            { BlendMode.Blend with
                SourceColorFactor      = BlendFactor.Zero
                DestinationColorFactor = BlendFactor.InvSourceColor
                SourceAlphaFactor      = BlendFactor.Zero
                DestinationAlphaFactor = BlendFactor.InvSourceAlpha }

        /// Final composite blend: standard (straight) alpha blending. The composite
        /// shader outputs a non-premultiplied color with alpha = 1 - revealage.
        let composite = BlendMode.Blend

    [<AutoOpen>]
    module Shaders =

        type Fragment = {
            [<Color>]     color  : V4f
            [<FragCoord>] coord  : V4f
            [<SampleId>]  sample : int
        }

        /// Fragment writer for the transparent pass. Reads the upstream Colors output
        /// (RGBA, alpha = coverage) and produces the Accum and Revealage outputs.
        /// Appended onto each transparent render object's surface by the wrapper.
        let weightedBlend (f : Fragment) =
            fragment {
                let a = f.color.W * 8.0f + 0.01f
                let b = -f.coord.Z * 0.95f + 1.0f
                let w = clamp 1e-2f 3e2f (a * a * a * 1e8f * b * b * b)

                let alpha = f.color.W
                let color = V4f(f.color.XYZ * alpha, alpha) * w

                return {| Accum = color
                          Revealage = alpha |}
            }

        let private accumSampler =
            sampler2d {
                texture uniform?AccumBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let private revealageSampler =
            sampler2d {
                texture uniform?RevealageBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let private accumSamplerMS =
            sampler2dMS {
                texture uniform?AccumBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let private revealageSamplerMS =
            sampler2dMS {
                texture uniform?RevealageBuffer
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        /// Fragment shader for the fullscreen composite pass. Reads the accum + revealage
        /// textures from the OIT pass and writes the resolved transparent color.
        /// `samples` is the multisample count of the OIT attachments (1 for non-MS).
        let composite (samples : int) (f : Fragment) =
            fragment {
                let mutable accum = V4f.Zero
                let mutable revealage = V4f.Zero

                if samples > 1 then
                    for i in 0 .. samples - 1 do
                        accum <- accum + accumSamplerMS.Read(V2i f.coord.XY, i)
                        revealage <- revealage + revealageSamplerMS.Read(V2i f.coord.XY, i)
                    accum <- accum / float32 samples
                    revealage <- revealage / float32 samples
                else
                    accum <- accumSampler.Read(V2i f.coord.XY, 0)
                    revealage <- revealageSampler.Read(V2i f.coord.XY, 0)

                let accum =
                    if isInfinity accum then V4f(accum.W)
                    else accum

                return V4f(accum.XYZ / max accum.W 1e-5f, 1.0f - revealage.X)
            }

    /// Effect form of `weightedBlend`, composable with other effects.
    let weightedBlendEffect : Effect = Effect.ofFunction weightedBlend

    /// Builds the composite effect for the given sample count.
    let compositeEffect (samples : int) : Effect =
        Effect.ofFunction (composite samples)

    /// Composes the OIT fragment writer onto an existing surface. Used by the
    /// render task wrapper to transform every transparent render object.
    ///   - Surface.Effect e: appends weightedBlend after e.
    ///   - Surface.Dynamic: fails, dynamic surfaces are not supported yet.
    ///   - Surface.Backend or Surface.None: fails, transparent objects need a composable surface.
    let composeSurface (surface : Surface) : Surface =
        match surface with
        | Surface.Effect e ->
            Surface.Effect (Effect.compose [e; weightedBlendEffect])
        | Surface.Dynamic _ ->
            failwith "[OIT] dynamic surfaces are not yet supported for transparent objects"
        | Surface.Backend _ ->
            failwith "[OIT] backend (pre-compiled) surfaces cannot be marked transparent"
        | Surface.None ->
            failwith "[OIT] transparent objects need a surface"

/// Exact order-independent transparency via a bounded per-pixel k-buffer
/// (A-buffer). Transparent fragments are inserted into a fixed-size per-pixel
/// array inside a fragment-shader-interlock critical section; a fullscreen
/// resolve sorts them by depth and composites them front-to-back exactly.
///
/// Storage layout, two images in total:
///   ABufferCount : R32UI, the number of fragments stored (at most Capacity)
///   ABufferSlot  : RGBA32UI 2D array with Capacity layers, one slot per layer:
///                    X = gl_FragCoord.z bit-cast to uint
///                    Y = packUnorm4x8(premultiplied RGBA)
///                    Z = gl_SampleMaskIn[0] coverage mask
///                    W = unused
///   Storing the mask alongside depth and color lets the resolve pass run per
///   sample and pick exactly the fragments that covered this sample, which fixes
///   the double-insert seam at MSAA triangle edges.
///
/// Keeping the number of images at two keeps the descriptor-set binding count
/// small: the binding allocator assigns bindings per resource type as in GL,
/// which collapse into a single Vulkan binding namespace, so storage images,
/// samplers and storage buffers on the same descriptor set can silently collide.
///
/// The slot store is a texture array (W x H, Capacity layers) rather than a wide
/// (W * Capacity) x H 2D image: for wide windows (width 2048 and more) the flat
/// layout reaches AMD's maxImageDimension2D of 16384 and image creation fails.
/// The array layout is also the natural fit, one layer per slot, and lifts the
/// width cap to the per-axis limit (16k on AMD).
///
/// Concurrency: fragments of the same pixel serialize through FShade's
/// fragment-shader interlock (pixel-unordered) with early fragment tests, so
/// occluded fragments never enter the critical section, and the storage images
/// are declared coherent.
module private ABufferOIT =
    open FShade

    /// Maximum number of transparent fragments stored per pixel. When a pixel is
    /// full, a nearer fragment replaces the farthest stored one and a farther
    /// fragment is dropped.
    [<Literal>]
    let Capacity = 8

    module Semantic =
        let ABufferCount = Symbol.Create "ABufferCount"
        let ABufferSlot  = Symbol.Create "ABufferSlot"

    // Slot s of pixel (x, y) lives at layer s of (x, y) in a uimage2DArray.
    // The previous flat (W * Capacity) x H layout failed image creation on AMD
    // for window widths of 2048 and more (W * 8 reaching maxImageDimension2D = 16384).
    type UniformScope with
        [<Coherent>]
        member x.ABufferCount : UIntImage2d<Formats.r32ui>         = x?ABufferCount
        [<Coherent>]
        member x.ABufferSlot  : UIntImage2dArray<Formats.rgba32ui> = x?ABufferSlot
        /// Diagnostic toggle. 0 = normal composite. 1 = visualize the stored
        /// gl_SampleMaskIn coverage of slot 0 as a gray intensity (full coverage
        /// 0xFF is white, partial-coverage edge pixels are darker). If this shows
        /// uniform white even along the edges between the triangles of a mesh,
        /// the backend's gl_SampleMaskIn does not produce per-primitive coverage
        /// (suspected on MoltenVK under fragment shader interlock). 2 = output the
        /// sample index to test that the resolve runs per sample.
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
    [<EarlyFragmentTests>]
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

            beginInvocationInterlock FragmentInterlockMode.PixelUnordered

            let count = int uniform.ABufferCount.[px].X

            if count < Capacity then
                uniform.ABufferSlot.[px, count] <- slot
                uniform.ABufferCount.[px] <- V4ui(uint32 (count + 1), 0u, 0u, 0u)
            else
                // full: replace the farthest slot if this fragment is nearer
                let mutable maxSlot = 0
                let mutable maxDepth = 0u
                for i in 0 .. Capacity - 1 do
                    let d = uniform.ABufferSlot.[px, i].X
                    if d > maxDepth then
                        maxDepth <- d
                        maxSlot <- i
                if dz < maxDepth then
                    uniform.ABufferSlot.[px, maxSlot] <- slot

            endInvocationInterlock()

            return f.color
        }

    type ResolveFragment = {
        [<Color>]     color  : V4f
        [<FragCoord>] coord  : V4f
        /// `gl_SampleID`. Declaring and reading it forces sample-rate fragment
        /// shader invocation: FShade emits gl_SampleID and the Vulkan pipeline
        /// gets sampleShadingEnable = true. Each invocation only includes the
        /// fragments whose stored coverage mask covers this sample, which is the
        /// point of storing the per-fragment mask.
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
            let count = min Capacity (int uniform.ABufferCount.[px].X)
            let sampleBit = 1u <<< f.sample

            // Diagnostic: visualize the stored coverage mask of slot 0. Full coverage
            // (0xFF) is white, partial-coverage edge pixels are darker. Used to verify
            // that gl_SampleMaskIn produces per-primitive coverage on each backend
            // (EnvVar.AARDVARK_OIT_ABUFFER_DEBUG = 1; 2 tests per-sample firing).
            let mutable dbg = V4f.Zero
            let mutable isDbg = false
            if uniform.ABufferDebug = 2 then
                // Per-sample firing test: output gl_SampleID / 7. If the resolve
                // runs per sample, the samples of each pixel carry 0 .. N-1 and the
                // MSAA resolve averages them to a mid gray; if it runs per pixel
                // (sampleShadingEnable not honored), only sample 0 runs and the
                // output is uniformly black. Black everywhere means per-sample
                // shading is broken.
                isDbg <- true
                let v = float32 f.sample / 7.0f
                dbg <- V4f(v, v, v, 1.0f)
            elif uniform.ABufferDebug = 1 then
                isDbg <- true
                if count > 0 then
                    // popcount of slot-0's coverage mask, normalized by 8 so the
                    // value is independent of subtle bit-position differences:
                    // full 8x coverage -> 1.0, full 4x -> 0.5, partial-coverage
                    // edge pixels -> proportionally less. If MoltenVK's
                    // gl_SampleMaskIn is broken this is uniform everywhere; if it
                    // works, triangle-interior edges read darker than interiors.
                    let mutable m = uniform.ABufferSlot.[px, 0].Z &&& 0xFFu
                    let mutable pc = 0u
                    for _i in 0 .. 7 do
                        pc <- pc + (m &&& 1u)
                        m <- m >>> 1
                    let v = float32 pc / 8.0f
                    dbg <- V4f(v, v, v, 1.0f)

            // gather only the slots whose coverage includes this sample
            let depths = Arr<N<Capacity>, uint32>()
            let colors = Arr<N<Capacity>, uint32>()
            let mutable n = 0
            for i in 0 .. count - 1 do
                let s = uniform.ABufferSlot.[px, i]
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

/// Wraps a set of render objects into an order-independent transparency pipeline:
///   1. the user framebuffer is copied into an intermediate framebuffer that
///      matches the user-supplied signature (colors + depth), and the opaque
///      objects render into it;
///   2. the transparent objects (RenderObject.IsTransparent = true) render
///      either into a dedicated weighted-blended framebuffer (Accum + Revealage
///      attachments) that shares the intermediate's depth attachment, or into
///      the A-buffer storage images with color writes masked;
///   3. a fullscreen quad composites (weighted-blended) or resolves (A-buffer)
///      the result onto the intermediate's Colors attachment;
///   4. the transparent objects render once more with their original surfaces,
///      depth writes on and Colors masked, so that the extra attachments and the
///      depth receive the values of the nearest transparent fragment;
///   5. the intermediate is finally copied onto the user-supplied framebuffer.
///
/// Each backend constructs this from its CompileRender entry point; the
/// sub-tasks are compiled through runtime.CompileRender(..., Transparency.None).
module TransparencyRenderTask =

    /// Resolves the technique a runtime actually runs for the requested transparency:
    /// Auto picks ABuffer where fragment-shader interlock is available; an explicit
    /// ABuffer without interlock is not supported. EnvVar.AARDVARK_OIT overrides the
    /// request with "auto", "wb", "abuffer" or "off" (also "0" or "false"); it is read
    /// whenever a render task is compiled, so it can be set programmatically.
    let private resolveMode (runtime : IRuntime) (mode : Transparency) =
        let mode =
            match EnvVar.AARDVARK_OIT with
            | value when EnvVar.IsSet value ->
                match value.Trim().ToLowerInvariant() with
                | "off" | "0" | "false" -> Transparency.None
                | "auto" -> Transparency.Auto
                | "wb" -> Transparency.WeightedBlended
                | "abuffer" -> Transparency.ABuffer
                | _ -> mode
            | _ -> mode
        match mode with
        | Transparency.Auto ->
            if runtime.FragmentShaderInterlock then Transparency.ABuffer else Transparency.WeightedBlended
        | Transparency.ABuffer when not runtime.FragmentShaderInterlock ->
            raise <| System.NotSupportedException("A-buffer transparency requires fragment shader interlock, which the runtime does not support.")
        | mode -> mode

    /// True if the given render object should be routed through the OIT pass.
    let private isTransparent (o : IRenderObject) =
        match o with
        | :? RenderObject as r -> r.IsTransparent
        | _ -> false

    /// Per-attachment blend-mode map for the OIT pass. Only Accum and
    /// Revealage exist in the OIT framebuffer; extras are not bound to
    /// it. Accum: additive. Revealage: multiplicative transmittance.
    let private transparentAttachmentBlend =
        Map.ofList [
            WeightedBlendedOIT.Semantic.Accum,     WeightedBlendedOIT.BlendModes.accum
            WeightedBlendedOIT.Semantic.Revealage, WeightedBlendedOIT.BlendModes.revealage
        ]

    /// Clones a transparent render object and rewrites its pipeline state for
    /// the OIT compositing pass: composes the weighted-blend writer onto its
    /// surface, forces depth writes off, applies the Accum/Revealage blend
    /// modes, and clears IsTransparent on the clone to avoid recursive
    /// wrapping. The OIT framebuffer only has Accum and Revealage as color
    /// attachments; any extra outputs the user's shader writes are dropped by
    /// FShade compilation, since they are not in the OIT signature.
    let private transformTransparent (ro : RenderObject) : IRenderObject =
        let copy = RenderObject(ro)
        copy.IsTransparent <- false
        copy.Surface       <- WeightedBlendedOIT.composeSurface ro.Surface
        copy.DepthState    <- { ro.DepthState with WriteMask = AVal.constant false }
        copy.BlendState    <- { ro.BlendState with
                                  AttachmentMode = AVal.constant transparentAttachmentBlend }
        copy

    /// Builds the per-attachment write-mask map for the transparent depth/extras
    /// pass: Colors gets ColorMask.None (writes suppressed), all other
    /// attachments keep their existing mask (default = All).
    let private transparentPickWriteMask (existing : Map<Symbol, ColorMask>) =
        existing |> Map.add DefaultSemantic.Colors ColorMask.None

    /// Clones a transparent render object for the depth + extras pass. The
    /// shader is left unmodified (the user's original surface); depth writes are
    /// enabled so the depth test alone selects the closest transparent
    /// fragment per pixel. The Colors attachment is write-masked so only the
    /// extras (PickData and friends) actually land in the framebuffer.
    /// Blending is disabled; only one fragment survives per pixel through
    /// the depth test.
    let private transformTransparentPick (ro : RenderObject) : IRenderObject =
        let copy = RenderObject(ro)
        copy.IsTransparent <- false
        copy.DepthState    <- { ro.DepthState with WriteMask = AVal.constant true }
        copy.BlendState    <- { ro.BlendState with
                                  Mode                = AVal.constant BlendMode.None
                                  AttachmentMode      = AVal.constant Map.empty
                                  AttachmentWriteMask = ro.BlendState.AttachmentWriteMask |> AVal.map transparentPickWriteMask }
        copy

    let private fullscreenPositions =
        [| V3f(-1.0f, -1.0f, 0.0f)
           V3f( 1.0f, -1.0f, 0.0f)
           V3f(-1.0f,  1.0f, 0.0f)
           V3f( 1.0f,  1.0f, 0.0f) |]

    /// Builds the fullscreen-quad render object that runs the composite shader.
    /// The accum and revealage textures are exposed as adaptive uniforms so the
    /// task can swap them in when the OIT framebuffer is reallocated.
    let private buildCompositeObject (samples : int)
                                     (accum : aval<ITexture>) (revealage : aval<ITexture>) : RenderObject =
        let view = BufferView fullscreenPositions

        let attrs =
            AttributeProvider.ofList [
                DefaultSemantic.Positions, view
            ]

        let uniforms =
            UniformProvider.ofList [
                WeightedBlendedOIT.Semantic.AccumBuffer,     accum :> IAdaptiveValue
                WeightedBlendedOIT.Semantic.RevealageBuffer, revealage :> IAdaptiveValue
            ]

        let drawCall = DrawCallInfo(FaceVertexCount = 4, InstanceCount = 1)

        let ro = RenderObject()
        ro.AttributeScope   <- Ag.Scope.Root
        ro.Mode             <- IndexedGeometryMode.TriangleStrip
        ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| drawCall |])
        ro.VertexAttributes <- attrs
        ro.Uniforms         <- uniforms
        ro.Surface          <- Surface.Effect (WeightedBlendedOIT.compositeEffect samples)
        ro.DepthState       <- { DepthState.Default with
                                    Test      = AVal.constant DepthTest.None
                                    WriteMask = AVal.constant false }
        ro.BlendState       <- { BlendState.Default with
                                    Mode = AVal.constant WeightedBlendedOIT.BlendModes.composite }
        ro

    // A-buffer (exact k-buffer) technique

    /// Premultiplied "over" blend for compositing the resolved A-buffer color
    /// onto the opaque scene: result = src + (1 - src.a) * dst.
    let private premultipliedOver =
        { BlendMode.Blend with
            SourceColorFactor      = BlendFactor.One
            DestinationColorFactor = BlendFactor.InvSourceAlpha
            SourceAlphaFactor      = BlendFactor.One
            DestinationAlphaFactor = BlendFactor.InvSourceAlpha }

    /// Diagnostic mode of the resolve pass from EnvVar.AARDVARK_OIT_ABUFFER_DEBUG: 1 visualizes
    /// the stored gl_SampleMaskIn coverage, 2 the sample index (per-sample firing test);
    /// anything else composites normally.
    let private aBufferDebug =
        match EnvVar.AARDVARK_OIT_ABUFFER_DEBUG with
        | value when EnvVar.IsSet value ->
            match System.Int32.TryParse(value.Trim()) with
            | true, mode when mode = 1 || mode = 2 -> mode
            | _ -> 0
        | _ -> 0

    /// Builds a uniform provider exposing the two A-buffer storage images.
    /// All per-slot data (depth, color, mask) lives in one RGBA32UI image;
    /// see the ABufferOIT module for the binding-count rationale.
    let private aBufferImageUniforms (count : aval<ITexture>) (slot : aval<ITexture>) =
        UniformProvider.ofList [
            ABufferOIT.Semantic.ABufferCount, count :> IAdaptiveValue
            ABufferOIT.Semantic.ABufferSlot,  slot  :> IAdaptiveValue
            Symbol.Create "ABufferDebug",     AVal.constant aBufferDebug :> IAdaptiveValue
        ]

    /// Clones a transparent render object for the A-buffer build pass: composes
    /// the interlocked insert writer onto its surface, masks color writes,
    /// disables depth writes (the depth test still occludes against the opaque
    /// geometry), binds the storage images, and clears IsTransparent on the clone.
    let private transformTransparentABuffer
                    (count : aval<ITexture>) (slot : aval<ITexture>)
                    (ro : RenderObject) : IRenderObject =
        let copy = RenderObject(ro)
        copy.IsTransparent <- false
        copy.Surface       <- ABufferOIT.composeSurface ro.Surface
        copy.DepthState    <- { ro.DepthState with WriteMask = AVal.constant false }
        copy.BlendState    <- { ro.BlendState with
                                  Mode                = AVal.constant BlendMode.None
                                  AttachmentMode      = AVal.constant Map.empty
                                  ColorWriteMask      = AVal.constant ColorMask.None
                                  AttachmentWriteMask = AVal.constant Map.empty }
        copy.Uniforms      <- UniformProvider.union (aBufferImageUniforms count slot) ro.Uniforms
        copy :> IRenderObject

    /// Fullscreen quad that resolves the A-buffer and composites it over the
    /// opaque scene with premultiplied "over".
    let private buildABufferResolveObject
                    (count : aval<ITexture>) (slot : aval<ITexture>) : RenderObject =
        let buffer = AVal.constant (ArrayBuffer fullscreenPositions :> IBuffer)
        let view   = BufferView(buffer, typeof<V3f>)
        let attrs  = AttributeProvider.ofList [ DefaultSemantic.Positions, view ]
        let drawCall = DrawCallInfo(FaceVertexCount = 4, InstanceCount = 1)

        let ro = RenderObject()
        ro.AttributeScope   <- Ag.Scope.Root
        ro.Mode             <- IndexedGeometryMode.TriangleStrip
        ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| drawCall |])
        ro.VertexAttributes <- attrs
        ro.Uniforms         <- aBufferImageUniforms count slot
        ro.Surface          <- Surface.Effect ABufferOIT.resolveEffect
        ro.DepthState       <- { DepthState.Default with
                                    Test      = AVal.constant DepthTest.None
                                    WriteMask = AVal.constant false }
        ro.BlendState       <- { BlendState.Default with
                                    Mode = AVal.constant premultipliedOver }
        ro

    /// Cached per-(size, samples) bundle of framebuffer resources used by the
    /// wrapped path. Built lazily on demand and kept alive across frames, so
    /// switching between known sizes does not dispose and rebuild.
    type private FboBundle = {
        IntermediateFb : IFramebuffer
        OitFb          : IFramebuffer
        CompositeFb    : IFramebuffer
        DepthTex       : IBackendTexture
        AccumT         : IBackendTexture
        RevealageT     : IBackendTexture
        ColorTex       : Map<Symbol, IBackendTexture>
        // A-buffer storage (only allocated when technique = ABuffer)
        AbCount        : IBackendTexture voption
        AbSlot         : IBackendTexture voption    // RGBA32UI per slot: X=depth, Y=color, Z=gl_SampleMaskIn
    }

    type private WrappedTask(runtime   : IRuntime,
                             mode      : Transparency,
                             userSig   : IFramebufferSignature,
                             objects   : aset<IRenderObject>) =
        inherit AbstractRenderTask()


        let depthFormat =
            userSig.DepthStencilAttachment
            |> Option.defaultValue TextureFormat.Depth24Stencil8

        // The intermediate framebuffer mirrors the user signature exactly
        // (all color attachments + DepthStencil). Opaque writes go there and
        // pass 3 updates extras + depth based on depth-test selection. The
        // OIT framebuffer is separate and only carries Accum + Revealage +
        // a shared DepthStencil; extras are not bound for pass 2.
        let userColorAtts =
            userSig.ColorAttachments
            |> Map.toList
            |> List.map (fun (_, att) -> att.Name, att.Format)

        let opaqueSet = objects |> ASet.filter (not << isTransparent)
        let transparentSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r -> Some (transformTransparent r)
                    | _ -> None
                else None)

        // Pass-3 set: same transparent objects, but cloned with depth-write on,
        // Colors masked out, and the user's original surface intact. Drives the
        // "as-if-depth-test" pass that updates extras + depth based purely on
        // depth ordering.
        let transparentPickSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r -> Some (transformTransparentPick r)
                    | _ -> None
                else None)

        // Snapshot of the current transparent objects, used at run time to skip
        // the OIT sub-passes whenever there are none.
        let transparentContent = transparentSet.Content

        // Adaptive holders for the OIT textures consumed by the composite quad.
        let accumTex     : cval<ITexture> = cval NullTexture.Instance
        let revealageTex : cval<ITexture> = cval NullTexture.Instance

        // Adaptive holders for the A-buffer storage images.
        let abCountTex : cval<ITexture> = cval NullTexture.Instance
        let abSlotTex  : cval<ITexture> = cval NullTexture.Instance

        // A-buffer build/resolve object sets (transparent objects with the
        // interlocked insert composed; a fullscreen resolve quad).
        let aBufferBuildSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r ->
                        Some (transformTransparentABuffer abCountTex abSlotTex r)
                    | _ -> None
                else None)

        // Direct opaque-only task compiled against the user signature.
        // Used in every frame in which the transparent set is currently empty:
        // render straight to the user framebuffer, no intermediate framebuffer
        // and no copies. Built lazily in the first frame that takes the direct
        // path and kept alive afterwards, so re-entering the direct path is free.
        let mutable directOpaqueTask : IRenderTask voption = ValueNone

        // Sample-count-dependent state. The OIT composite shader bakes in the
        // sample count and the framebuffer signatures bake in their sample
        // count, so all of this is rebuilt whenever the output framebuffer's
        // sample count changes.
        let mutable currentSamples      = 0
        let mutable intermediateSig     : IFramebufferSignature voption = ValueNone
        let mutable oitSig              : IFramebufferSignature voption = ValueNone
        // The composite shader only writes Colors. We compile it against a
        // dedicated signature with just Colors + DepthStencil so FShade
        // doesn't synthesize varying passthroughs for the user's extra
        // attachments (which would then need to come from vertex attributes).
        let mutable compositeSig        : IFramebufferSignature voption = ValueNone
        let mutable opaqueTask          : IRenderTask voption = ValueNone
        let mutable transparentTask     : IRenderTask voption = ValueNone
        let mutable compositeTask       : IRenderTask voption = ValueNone
        let mutable transparentPickTask : IRenderTask voption = ValueNone
        let mutable aBufferBuildTask    : IRenderTask voption = ValueNone
        let mutable aBufferResolveTask  : IRenderTask voption = ValueNone

        // Bounded LRU cache of framebuffer bundles, one per (size, samples) the
        // wrapper has seen. Render tasks typically see only a handful of
        // distinct sizes (main window, preview pane, ...); caching avoids
        // dispose-and-rebuild thrashing when the task alternates between them.
        // If an app churns through more sizes than `fboCacheCapacity`, the
        // least-recently-used bundle is evicted.
        let fboCacheCapacity = 4
        let fboCache =
            System.Collections.Generic.Dictionary<struct (V2i * int), FboBundle>()
        // Most-recently-used at head.
        let mutable fboLru : list<struct (V2i * int)> = []

        let mutable currentBundle : FboBundle voption = ValueNone

        let disposeBundle (b : FboBundle) =
            b.IntermediateFb.Dispose()
            b.OitFb.Dispose()
            b.CompositeFb.Dispose()
            b.DepthTex.Dispose()
            b.AccumT.Dispose()
            b.RevealageT.Dispose()
            for KeyValue(_, t) in b.ColorTex do t.Dispose()
            b.AbCount |> ValueOption.iter _.Dispose()
            b.AbSlot  |> ValueOption.iter _.Dispose()

        let releaseFbos () =
            for kvp in fboCache do disposeBundle kvp.Value
            fboCache.Clear()
            fboLru <- []
            currentBundle <- ValueNone

        let releaseTasksAndSignatures () =
            opaqueTask          |> ValueOption.iter _.Dispose()
            transparentTask     |> ValueOption.iter _.Dispose()
            compositeTask       |> ValueOption.iter _.Dispose()
            transparentPickTask |> ValueOption.iter _.Dispose()
            aBufferBuildTask    |> ValueOption.iter _.Dispose()
            aBufferResolveTask  |> ValueOption.iter _.Dispose()
            intermediateSig     |> ValueOption.iter _.Dispose()
            oitSig              |> ValueOption.iter _.Dispose()
            compositeSig        |> ValueOption.iter _.Dispose()
            opaqueTask          <- ValueNone
            transparentTask     <- ValueNone
            compositeTask       <- ValueNone
            transparentPickTask <- ValueNone
            aBufferBuildTask    <- ValueNone
            aBufferResolveTask  <- ValueNone
            intermediateSig     <- ValueNone
            oitSig              <- ValueNone
            compositeSig        <- ValueNone

        let releaseDirectTask () =
            directOpaqueTask |> ValueOption.iter (fun t -> t.Dispose())
            directOpaqueTask <- ValueNone

        let ensureForSamples (samples : int) =
            if samples <> currentSamples then
                // Tasks and signatures depend on the sample count, so rebuild
                // everything (also forces a framebuffer rebuild via releaseFbos).
                releaseFbos ()
                releaseTasksAndSignatures ()

                let interSig =
                    let entries = seq {
                        yield! userColorAtts
                        yield DefaultSemantic.DepthStencil, depthFormat
                    }
                    runtime.CreateFramebufferSignature(entries, samples = samples)

                // OIT signature: Accum + Revealage + DepthStencil only. The
                // composed transparent shader's outputs are pared down by
                // FShade to exactly these; any extras the user's shader
                // produces are dropped for this pass. Pass 3 is where extras
                // get written, with proper depth-test selection.
                let oS =
                    runtime.CreateFramebufferSignature(
                        [ WeightedBlendedOIT.Semantic.Accum,     TextureFormat.Rgba16f
                          WeightedBlendedOIT.Semantic.Revealage, TextureFormat.R32f
                          DefaultSemantic.DepthStencil,          depthFormat ],
                        samples = samples)

                // Composite signature: only Colors + DepthStencil. FShade's
                // effect linker compiles outputs for every color attachment in
                // the signature; if extras (e.g. PickData) were included here,
                // the composite shader (which writes only Colors) would have
                // its missing outputs synthesized as varying passthroughs,
                // requiring matching vertex attributes that the fullscreen
                // quad doesn't have.
                let cS =
                    runtime.CreateFramebufferSignature(
                        [ DefaultSemantic.Colors,       userColorAtts
                                                       |> List.tryFind (fun (n, _) -> n = DefaultSemantic.Colors)
                                                       |> Option.map snd
                                                       |> Option.defaultValue TextureFormat.Rgba8
                          DefaultSemantic.DepthStencil, depthFormat ],
                        samples = samples)

                let compositeObject = buildCompositeObject samples accumTex revealageTex
                let compositeSet    = ASet.single (compositeObject :> IRenderObject)

                intermediateSig     <- ValueSome interSig
                oitSig              <- ValueSome oS
                compositeSig        <- ValueSome cS
                opaqueTask          <- ValueSome (runtime.CompileRender(interSig, opaqueSet, Transparency.None))
                transparentPickTask <- ValueSome (runtime.CompileRender(interSig, transparentPickSet, Transparency.None))

                match mode with
                | Transparency.WeightedBlended ->
                    transparentTask <- ValueSome (runtime.CompileRender(oS, transparentSet, Transparency.None))
                    compositeTask   <- ValueSome (runtime.CompileRender(cS, compositeSet, Transparency.None))
                | _ -> // Transparency.ABuffer
                    // build + resolve both render to the Colors+DepthStencil
                    // composite signature (the storage is bound via uniforms,
                    // not attachments).
                    let resolveObject = buildABufferResolveObject abCountTex abSlotTex
                    let resolveSet    = ASet.single (resolveObject :> IRenderObject)
                    aBufferBuildTask   <- ValueSome (runtime.CompileRender(cS, aBufferBuildSet, Transparency.None))
                    aBufferResolveTask <- ValueSome (runtime.CompileRender(cS, resolveSet, Transparency.None))

                currentSamples      <- samples

        let buildBundle (size : V2i) (samples : int) =
            let dt = runtime.CreateTexture2D(size, depthFormat, samples = samples)

            let ct =
                userSig.ColorAttachments
                |> Map.toList
                |> List.map (fun (_, att) ->
                    att.Name, runtime.CreateTexture2D(size, att.Format, samples = samples))
                |> Map.ofList

            let at = runtime.CreateTexture2D(size, TextureFormat.Rgba16f, samples = samples)
            let rt = runtime.CreateTexture2D(size, TextureFormat.R32f,    samples = samples)

            let interAtts =
                ct
                |> Map.map (fun _ t -> t.GetOutputView())
                |> Map.add DefaultSemantic.DepthStencil (dt.GetOutputView())

            // OIT framebuffer: only Accum + Revealage + shared DepthStencil.
            // Extras are not bound here; pass 3 (against the intermediate
            // framebuffer) is where the depth test selects the closest
            // transparent fragment's extras.
            let oitAtts =
                Map.ofList [
                    WeightedBlendedOIT.Semantic.Accum,     at.GetOutputView()
                    WeightedBlendedOIT.Semantic.Revealage, rt.GetOutputView()
                    DefaultSemantic.DepthStencil,          dt.GetOutputView()
                ]

            // Composite framebuffer: only Colors + DepthStencil, but
            // referencing the same backing textures as the intermediate
            // framebuffer so the composite's writes to Colors land in the
            // shared texture.
            let compositeAtts =
                Map.ofList [
                    DefaultSemantic.Colors,
                        (ct |> Map.tryFind DefaultSemantic.Colors
                            |> Option.map _.GetOutputView()
                            |> Option.defaultValue (dt.GetOutputView()))
                    DefaultSemantic.DepthStencil, dt.GetOutputView()
                ]

            // A-buffer storage: count (W x H, R32UI) + slot (W x H with Capacity
            // layers, RGBA32UI). A slot packs depth, color and gl_SampleMaskIn
            // into one image (X = depth, Y = color, Z = mask); see the ABufferOIT
            // module for the binding-count rationale. The slot store is a 2D
            // array so that wide windows do not exceed AMD's maxImageDimension2D
            // of 16384 (the earlier flat (W * Capacity) x H layout failed at
            // widths of 2048 and more). Only allocated when the technique uses it.
            let abCount, abSlot =
                match mode with
                | Transparency.ABuffer ->
                    let k = ABufferOIT.Capacity
                    let cnt = runtime.CreateTexture2D(size, TextureFormat.R32ui)
                    let sl  = runtime.CreateTexture2DArray(size, TextureFormat.Rgba32ui, count = k)
                    ValueSome cnt, ValueSome sl
                | _ -> // Transparency.WeightedBlended
                    ValueNone, ValueNone

            { IntermediateFb = runtime.CreateFramebuffer(intermediateSig.Value, interAtts)
              OitFb          = runtime.CreateFramebuffer(oitSig.Value, oitAtts)
              CompositeFb    = runtime.CreateFramebuffer(compositeSig.Value, compositeAtts)
              DepthTex       = dt
              AccumT         = at
              RevealageT     = rt
              ColorTex       = ct
              AbCount        = abCount
              AbSlot         = abSlot }

        let touchLru (key : struct (V2i * int)) =
            // Move `key` to the front of the LRU list. If we exceed the
            // capacity, drop the tail entry and dispose its bundle.
            fboLru <- key :: (fboLru |> List.filter (fun k -> k <> key))
            if List.length fboLru > fboCacheCapacity then
                let rec dropTail = function
                    | [] | [_] as l -> l
                    | x :: rest -> x :: dropTail rest
                let evicted = List.last fboLru
                fboLru <- dropTail fboLru
                match fboCache.TryGetValue evicted with
                | true, b ->
                    fboCache.Remove evicted |> ignore
                    disposeBundle b
                | _ -> ()

        let ensureFbos (size : V2i) (samples : int) =
            let key = struct (size, samples)
            let bundle =
                match fboCache.TryGetValue key with
                | true, b -> b
                | _ ->
                    let b = buildBundle size samples
                    fboCache.[key] <- b
                    b
            touchLru key

            // Swap the adaptive texture references the composite quad reads
            // through so it picks up the right accum/revealage for this size.
            let mustTransact =
                match currentBundle with
                | ValueSome cur ->
                    not (obj.ReferenceEquals(cur.AccumT, bundle.AccumT))
                | ValueNone -> true

            if mustTransact then
                transact (fun () ->
                    accumTex.Value     <- bundle.AccumT
                    revealageTex.Value <- bundle.RevealageT
                    bundle.AbCount |> ValueOption.iter (fun t -> abCountTex.Value <- t)
                    bundle.AbSlot  |> ValueOption.iter (fun t -> abSlotTex.Value <- t)
                )

            currentBundle <- ValueSome bundle

        override x.FramebufferSignature = Some userSig
        override x.Runtime = Some runtime
        override x.Use f = f ()

        override x.PerformUpdate(token, rt) =
            directOpaqueTask    |> ValueOption.iter _.Update(token, rt)
            opaqueTask          |> ValueOption.iter _.Update(token, rt)
            transparentTask     |> ValueOption.iter _.Update(token, rt)
            compositeTask       |> ValueOption.iter _.Update(token, rt)
            transparentPickTask |> ValueOption.iter _.Update(token, rt)
            aBufferBuildTask    |> ValueOption.iter _.Update(token, rt)
            aBufferResolveTask  |> ValueOption.iter _.Update(token, rt)

        override x.Perform(token, rt, output) =
            let outFb = output.Framebuffer
            let outSamples = outFb.Signature.Samples

            // Read through the current AdaptiveToken so this wrapper is
            // marked dirty when transparent objects appear or disappear.
            // This matters for dirty-driven render loops that only re-run
            // a task when one of its dependencies changes.
            let hasTransparent =
                not (HashSet.isEmpty (transparentContent.GetValue token))

            if not hasTransparent then
                // Fast direct path: no transparent objects right now. Render
                // the opaque set straight to the user framebuffer: no
                // intermediate framebuffer, no copies, no OIT machinery. The
                // wrapped resources (if previously built) stay alive for the
                // next frame in which transparent objects reappear.
                match directOpaqueTask with
                | ValueNone ->
                    directOpaqueTask <- ValueSome (runtime.CompileRender(userSig, opaqueSet, Transparency.None))
                | _ -> ()
                directOpaqueTask.Value.Run(token, rt, output)
            else

            // Wrapped path: build (or reuse) the intermediate, OIT and composite
            // signatures, sub-tasks and framebuffers and run the full sequence
            // of passes. The direct task is kept alive for the next opaque-only
            // frame.
            ensureForSamples outSamples
            ensureFbos outFb.Size outSamples

            let bundle = currentBundle.Value
            let inter = bundle.IntermediateFb

            // Always: seed the intermediate from the user framebuffer (preserves
            // any prior clear) and run the opaque pass into it.
            runtime.Copy(outFb, inter)
            opaqueTask.Value.Run(token, rt, { output with Framebuffer = inter })

            if hasTransparent then
                match mode with
                | Transparency.WeightedBlended ->
                    let oit = bundle.OitFb

                    // Pass 2: OIT color compositing.
                    // Clear only Accum + Revealage on the OIT framebuffer; the
                    // shared depth from the opaque pass is preserved for depth
                    // testing.
                    let oitClear =
                        clear {
                            colors [
                                WeightedBlendedOIT.Semantic.Accum,     C4f.Zero
                                WeightedBlendedOIT.Semantic.Revealage, C4f.White
                            ]
                        }
                    runtime.Clear(oit, oitClear)
                    transparentTask.Value.Run(token, rt, { output with Framebuffer = oit })
                    compositeTask.Value.Run(token, rt, { output with Framebuffer = bundle.CompositeFb })

                | _ -> // Transparency.ABuffer
                    // Clear the per-pixel fragment count to 0, build the
                    // k-buffer (interlocked insert; depth-tests against the
                    // opaque depth, colour writes masked), then resolve it over
                    // the opaque scene. Each step is a separate task; the
                    // runtime completes one before the next starts.
                    bundle.AbCount |> ValueOption.iter (fun c ->
                        runtime.Clear(c, clear { color C4ui.Zero }))
                    aBufferBuildTask.Value.Run(token, rt, { output with Framebuffer = bundle.CompositeFb })
                    aBufferResolveTask.Value.Run(token, rt, { output with Framebuffer = bundle.CompositeFb })

                // Pass 3: transparent depth + extras pass.
                // Renders the *unmodified* user surfaces with depth-write on and
                // Colors write-masked. The depth test alone selects the closest
                // transparent fragment per pixel, so extras (PickData, etc.)
                // and the shared depth end up with the closest-fragment's
                // values regardless of draw order.
                transparentPickTask.Value.Run(token, rt, { output with Framebuffer = inter })

            // Final copy: intermediate to user framebuffer.
            runtime.Copy(inter, outFb)

        override x.Release() =
            releaseDirectTask ()
            releaseTasksAndSignatures ()
            releaseFbos ()

    /// Compiles a render task for the given objects, rendering the ones marked
    /// IsTransparent with the requested transparency mode (see resolveMode for
    /// the fallbacks and the AARDVARK_OIT override).
    let create (runtime   : IRuntime)
               (signature : IFramebufferSignature)
               (mode      : Transparency)
               (objects   : aset<IRenderObject>) : IRenderTask =
        match resolveMode runtime mode with
        | Transparency.None -> runtime.CompileRender(signature, objects, Transparency.None)
        | mode -> new WrappedTask(runtime, mode, signature, objects)